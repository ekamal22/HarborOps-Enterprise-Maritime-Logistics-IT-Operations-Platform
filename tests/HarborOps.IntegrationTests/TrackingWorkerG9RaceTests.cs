using System.Diagnostics;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

/// <summary>
/// Deterministic races: a reference changes while a real worker is inside its effect insert (CA-04),
/// and a security-version change commits while a redrive waits on the organization lock (CA-08).
/// Row locks held by an uncommitted transaction create the overlap; no sleeps are used as proof.
/// </summary>
public sealed class TrackingWorkerG9RaceTests
{
    // CA-04: the assignment is closed before the event time after the worker resolved the reference but
    // before its effect insert is validated. The database rejects the effect and the real worker parks the
    // still-valid lease in NeedsReconciliation instead of dead-lettering it or recording a false success.
    [Fact]
    public async Task ReferenceChangedWhileWorkerFinalizesIsParkedForReconciliation()
    {
        var original = TestDatabase.ConnectionString();
        var nonce = Guid.NewGuid().ToString("N");
        var schema = "g9_refrace_" + nonce;
        var scoped = new NpgsqlConnectionStringBuilder(original) { SearchPath = schema + ",public" };
        await using var administration = new NpgsqlConnection(original);
        await administration.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", administration))
            await create.ExecuteNonQueryAsync();

        var executable = TrackingWorkerG9ProcessTests.WorkerExecutable();
        Process? worker = null;
        NpgsqlTransaction? blockerTx = null;
        try
        {
            using var factory = new ReferenceDataHttpTests.ApiFactory(scoped.ConnectionString);
            var fixture = await ReferenceDataHttpTests.Setup(factory);
            using var admin = fixture.Admin;
            using var manager = fixture.Manager;
            using var analyst = fixture.Analyst;
            var target = await G9Support.SeedResolvable(factory, fixture);
            var source = await G9Support.NewSource(factory, fixture.A, "g9_refrace");
            var receipt = await G9Support.Accept(factory, source,
                G9Support.EquipmentPayload("g9-refrace-" + nonce[..12], target.Reference));
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var managerId = (await db.AuthUsers.AsNoTracking().SingleAsync(x =>
                x.NormalizedEmail == IdentityStore.NormalizeEmail(fixture.ManagerEmail))).Id;

            // Hold an uncommitted change that closes the assignment before the event time.
            await using var blocker = new NpgsqlConnection(scoped.ConnectionString);
            await blocker.OpenAsync();
            blockerTx = await blocker.BeginTransactionAsync();
            await using (var close = new NpgsqlCommand("""
                UPDATE shipment_container_assignments
                   SET ends_at_utc = '2026-11-03T09:30:00Z', closed_by_user_id = @user,
                       version = version + 1, updated_at_utc = now()
                 WHERE organization_id = @org AND container_id = @container AND ends_at_utc IS NULL
                """, blocker, blockerTx))
            {
                close.Parameters.AddWithValue("user", managerId);
                close.Parameters.AddWithValue("org", fixture.A);
                close.Parameters.AddWithValue("container", target.Container);
                Assert.Equal(1, await close.ExecuteNonQueryAsync());
            }

            var name = "g9-refrace-w-" + nonce[..10];
            worker = TrackingWorkerG9ProcessTests.StartWorker(executable, scoped.ConnectionString, name);
            var waiting = await TrackingWorkerG9ProcessTests.WaitFor(async () =>
                await db.TrackingInboxWorkItems.AsNoTracking().AnyAsync(x => x.ReceiptId == receipt && x.State == "Leased") &&
                await G9Support.Count(original, """
                    SELECT count(*) FROM pg_stat_activity
                    WHERE application_name=@name AND wait_event_type='Lock'
                    """, ("name", name)) >= 1, TimeSpan.FromSeconds(60));
            Assert.True(waiting, "The worker must be inside its effect insert, waiting on the changing reference");
            Assert.Equal(0, await db.TrackingProcessingEffects.CountAsync(x => x.ReceiptId == receipt));

            await blockerTx.CommitAsync();
            blockerTx = null;
            var parked = await TrackingWorkerG9ProcessTests.WaitFor(async () =>
                await db.TrackingInboxWorkItems.AsNoTracking()
                    .AnyAsync(x => x.ReceiptId == receipt && x.State == "NeedsReconciliation"), TimeSpan.FromSeconds(60));
            Assert.True(parked, "The worker must park the work instead of failing it");

            var work = await db.TrackingInboxWorkItems.AsNoTracking().SingleAsync(x => x.ReceiptId == receipt);
            Assert.Equal("reference_changed", work.LastErrorCode);
            Assert.Equal(1, work.AttemptCount);
            Assert.Null(work.LeaseToken);
            Assert.Equal(0, await db.TrackingProcessingEffects.CountAsync(x => x.ReceiptId == receipt));
            Assert.Equal(1, await db.TrackingWorkAttemptEvents.CountAsync(x => x.ReceiptId == receipt && x.EventKind == "Claimed"));
            Assert.Equal(1, await db.TrackingWorkAttemptEvents.CountAsync(x => x.ReceiptId == receipt && x.EventKind == "ReconciliationRequired"));
            Assert.Equal(0, await db.TrackingWorkAttemptEvents.CountAsync(x => x.ReceiptId == receipt && x.EventKind == "DeadLettered"));
            Assert.Equal(1, await db.TrackingInboxReceipts.CountAsync(x => x.Id == receipt));
            Assert.Equal(1, await db.TrackingAcceptedEvents.CountAsync(x => x.ReceiptId == receipt));
        }
        finally
        {
            if (blockerTx is not null)
            {
                try { await blockerTx.RollbackAsync(); } catch (InvalidOperationException) { }
            }
            await TrackingWorkerG9ProcessTests.StopAsync(worker);
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", administration);
            await drop.ExecuteNonQueryAsync();
        }
    }

    // CA-08: a redrive that has already passed the session and permission checks waits on the organization
    // lock while the Manager's security version changes. After the lock is granted the transaction must
    // recheck the live account and refuse, leaving the work and its history untouched.
    [Fact]
    public async Task SecurityVersionChangeWhileRedriveWaitsOnTheOrganizationLockIsDenied()
    {
        var connection = TestDatabase.ConnectionString();
        using var factory = new ReferenceDataHttpTests.ApiFactory(connection);
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var source = await G9Support.NewSource(factory, fixture.A, "g9_secrace");
        var control = await TrackingWorkerG9AuthorizationTests.ParkedReceipt(factory, fixture.A, source, "g9-secrace-ctl");
        var raced = await TrackingWorkerG9AuthorizationTests.ParkedReceipt(factory, fixture.A, source, "g9-secrace-race");

        Guid managerId;
        await using (var lookup = factory.Services.CreateAsyncScope())
        {
            var lookupDb = lookup.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            managerId = (await lookupDb.AuthUsers.AsNoTracking().SingleAsync(x =>
                x.NormalizedEmail == IdentityStore.NormalizeEmail(fixture.ManagerEmail))).Id;
        }
        var sessionId = await ScalarGuid(connection, """
            SELECT id FROM auth_sessions
            WHERE user_id=@user AND revoked_at_utc IS NULL AND selected_organization_id=@org
            ORDER BY issued_at_utc DESC LIMIT 1
            """, ("user", managerId), ("org", fixture.A));

        async Task<TrackingRedriveOutcome> Redrive(Guid receipt, string reason)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var store = new TrackingWorkAdminStore(db, new ConfigurationBuilder().Build());
            return await store.RedriveAsync(fixture.A, managerId, sessionId, receipt, reason, CancellationToken.None);
        }

        // Control: the same call succeeds while the account is unchanged.
        Assert.Equal(TrackingRedriveOutcome.Redriven, await Redrive(control, "control redrive succeeds"));

        await using var blocker = new NpgsqlConnection(connection);
        await blocker.OpenAsync();
        await using var blockerTx = await blocker.BeginTransactionAsync();
        await using (var hold = new NpgsqlCommand(
            "SELECT id FROM organizations WHERE id=@org FOR UPDATE", blocker, blockerTx))
        {
            hold.Parameters.AddWithValue("org", fixture.A);
            Assert.Equal(fixture.A, await hold.ExecuteScalarAsync());
        }
        await using (var bump = new NpgsqlCommand(
            "UPDATE users SET security_version = security_version + 1 WHERE id=@user", blocker, blockerTx))
        {
            bump.Parameters.AddWithValue("user", managerId);
            Assert.Equal(1, await bump.ExecuteNonQueryAsync());
        }

        var racing = Task.Run(() => Redrive(raced, "racing redrive must be denied"));
        var waiting = false;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline && !waiting)
        {
            waiting = await G9Support.Count(connection, """
                SELECT count(*) FROM pg_stat_activity
                WHERE datname=current_database() AND wait_event_type='Lock' AND pid<>pg_backend_pid()
                """) >= 1;
            if (!waiting) await Task.Delay(100);
        }
        Assert.True(waiting, "The redrive must be waiting on the organization lock");
        Assert.False(racing.IsCompleted);

        await blockerTx.CommitAsync();
        Assert.Equal(TrackingRedriveOutcome.AccessDenied, await racing.WaitAsync(TimeSpan.FromSeconds(30)));

        await using var verify = factory.Services.CreateAsyncScope();
        var readDb = verify.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var untouched = await readDb.TrackingInboxWorkItems.AsNoTracking().SingleAsync(x => x.ReceiptId == raced);
        Assert.Equal("NeedsReconciliation", untouched.State);
        Assert.Equal(0, untouched.RedriveGeneration);
        Assert.Equal(0, await readDb.TrackingWorkAttemptEvents.CountAsync(x => x.ReceiptId == raced && x.EventKind == "Redriven"));
        var redriven = await readDb.TrackingInboxWorkItems.AsNoTracking().SingleAsync(x => x.ReceiptId == control);
        Assert.Equal("Pending", redriven.State);
        Assert.Equal(1, redriven.RedriveGeneration);
        Assert.Equal(1, await readDb.TrackingWorkAttemptEvents.CountAsync(x => x.ReceiptId == control && x.EventKind == "Redriven"));
    }

    private static async Task<Guid> ScalarGuid(string connection, string sql, params (string, object)[] values)
    {
        await using var direct = new NpgsqlConnection(connection);
        await direct.OpenAsync();
        await using var command = new NpgsqlCommand(sql, direct);
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }
}
