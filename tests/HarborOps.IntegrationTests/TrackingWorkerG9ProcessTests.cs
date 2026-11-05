using System.Diagnostics;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

/// <summary>G9 crash-window and ambiguous-COMMIT scenarios with real processes and a real database wire.</summary>
public sealed class TrackingWorkerG9ProcessTests
{
    internal static string WorkerExecutable()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "HarborOps.sln")))
            root = root.Parent;
        Assert.NotNull(root);
        var executable = Path.Combine(root!.FullName, "src", "HarborOps.Worker", "bin",
            "Release", "net10.0", "HarborOps.Worker.dll");
        Assert.True(File.Exists(executable), "Build the worker in Release before the process suite");
        return executable;
    }

    internal static Process StartWorker(string executable, string connectionString, string applicationName)
    {
        var connection = new NpgsqlConnectionStringBuilder(connectionString) { ApplicationName = applicationName };
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(executable);
        start.Environment["ConnectionStrings__HarborOps"] = connection.ConnectionString;
        start.Environment["TrackingWorker__PollMilliseconds"] = "100";
        start.Environment["TrackingWorker__LeaseSeconds"] = "10";
        start.Environment["TrackingWorker__HeartbeatSeconds"] = "2";
        return Process.Start(start) ?? throw new InvalidOperationException("Worker process failed to start");
    }

    internal static async Task StopAsync(Process? process)
    {
        if (process is null) return;
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException) { /* already stopped and disposed */ }
        finally { process.Dispose(); }
    }

    internal static async Task<bool> WaitFor(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition()) return true;
            await Task.Delay(200);
        }
        return false;
    }

    // G9-S10 and G9-S11: a worker killed after its claim committed and while its effect insert is
    // in flight loses the uncommitted effect, keeps the durable lease, and a new worker commits once.
    [Fact]
    public async Task KilledWorkerAfterClaimAndBeforeEffectCommitIsRecoveredWithOneEffect()
    {
        var original = TestDatabase.ConnectionString();
        var nonce = Guid.NewGuid().ToString("N");
        var schema = "g9_kill_" + nonce;
        var scoped = new NpgsqlConnectionStringBuilder(original) { SearchPath = schema + ",public" };
        await using var administration = new NpgsqlConnection(original);
        await administration.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", administration))
            await create.ExecuteNonQueryAsync();

        var executable = WorkerExecutable();
        Process? first = null, second = null;
        NpgsqlTransaction? blockerTx = null;
        try
        {
            using var factory = new ReferenceDataHttpTests.ApiFactory(scoped.ConnectionString);
            var fixture = await ReferenceDataHttpTests.Setup(factory);
            using var admin = fixture.Admin;
            using var manager = fixture.Manager;
            using var analyst = fixture.Analyst;
            var target = await G9Support.SeedResolvable(factory, fixture);
            var source = await G9Support.NewSource(factory, fixture.A, "g9_kill");
            var receipt = await G9Support.Accept(factory, source,
                G9Support.EquipmentPayload("g9-kill-" + nonce[..12], target.Reference));
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();

            // Hold the container row so the worker's effect insert blocks inside its finalize transaction.
            await using var blocker = new NpgsqlConnection(scoped.ConnectionString);
            await blocker.OpenAsync();
            blockerTx = await blocker.BeginTransactionAsync();
            await using (var hold = new NpgsqlCommand(
                "SELECT id FROM containers WHERE organization_id=@o AND id=@c FOR UPDATE", blocker, blockerTx))
            {
                hold.Parameters.AddWithValue("o", fixture.A);
                hold.Parameters.AddWithValue("c", target.Container);
                Assert.Equal(target.Container, await hold.ExecuteScalarAsync());
            }

            var firstName = "g9-kill-a-" + nonce[..10];
            first = StartWorker(executable, scoped.ConnectionString, firstName);
            var blocked = await WaitFor(async () =>
            {
                var leased = await db.TrackingInboxWorkItems.AsNoTracking()
                    .AnyAsync(x => x.ReceiptId == receipt && x.State == "Leased");
                if (!leased || first.HasExited) return false;
                return await G9Support.Count(original, """
                    SELECT count(*) FROM pg_stat_activity
                    WHERE application_name=@name AND wait_event_type='Lock'
                    """, ("name", firstName)) >= 1;
            }, TimeSpan.FromSeconds(60));
            Assert.True(blocked, "The worker must hold a committed lease and wait inside its effect insert");
            var held = await db.TrackingInboxWorkItems.AsNoTracking().SingleAsync(x => x.ReceiptId == receipt);
            Assert.Equal(1, held.AttemptCount);
            Assert.Equal(1L, held.LeaseEpoch);
            Assert.NotNull(held.LeaseToken);
            Assert.Equal(0, await db.TrackingProcessingEffects.CountAsync(x => x.ReceiptId == receipt));

            await StopAsync(first);
            await blockerTx.RollbackAsync();
            blockerTx = null;
            var gone = await WaitFor(async () => await G9Support.Count(original,
                "SELECT count(*) FROM pg_stat_activity WHERE application_name=@name", ("name", firstName)) == 0,
                TimeSpan.FromSeconds(30));
            Assert.True(gone, "The killed worker's database sessions must end without committing");

            var afterKill = await db.TrackingInboxWorkItems.AsNoTracking().SingleAsync(x => x.ReceiptId == receipt);
            Assert.Equal("Leased", afterKill.State);
            Assert.Equal(held.LeaseToken, afterKill.LeaseToken);
            Assert.Equal(0, await db.TrackingProcessingEffects.CountAsync(x => x.ReceiptId == receipt));
            Assert.Equal(1, await db.TrackingInboxReceipts.CountAsync(x => x.Id == receipt));
            Assert.Equal(1, await db.TrackingAcceptedEvents.CountAsync(x => x.ReceiptId == receipt));

            second = StartWorker(executable, scoped.ConnectionString, "g9-kill-b-" + nonce[..10]);
            var processed = await WaitFor(async () => await db.TrackingInboxWorkItems.AsNoTracking()
                .AnyAsync(x => x.ReceiptId == receipt && x.State == "Processed"), TimeSpan.FromSeconds(90));
            Assert.True(processed, "A replacement worker must recover the expired lease and finish the work");
            var done = await db.TrackingInboxWorkItems.AsNoTracking().SingleAsync(x => x.ReceiptId == receipt);
            Assert.Equal(2, done.AttemptCount);
            Assert.Equal(2L, done.LeaseEpoch);
            Assert.Equal(1, await db.TrackingProcessingEffects.CountAsync(x => x.ReceiptId == receipt));
            Assert.Equal(1, await db.TrackingWorkAttemptEvents.CountAsync(x => x.ReceiptId == receipt && x.EventKind == "Claimed"));
            Assert.Equal(1, await db.TrackingWorkAttemptEvents.CountAsync(x => x.ReceiptId == receipt && x.EventKind == "LeaseRecovered"));
            Assert.Equal(1, await db.TrackingWorkAttemptEvents.CountAsync(x => x.ReceiptId == receipt && x.EventKind == "Processed"));
        }
        finally
        {
            if (blockerTx is not null)
            {
                try { await blockerTx.RollbackAsync(); } catch (InvalidOperationException) { }
            }
            await StopAsync(first);
            await StopAsync(second);
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", administration);
            await drop.ExecuteNonQueryAsync();
        }
    }

    // G9-S12: the effect-finalization COMMIT is interrupted on the PostgreSQL wire before the server
    // sees it, and after the server processed it but before the client sees the acknowledgment.
    [Theory]
    [InlineData(TrackingCommitSocketProxy.CutPoint.BeforeCommitForward, false)]
    [InlineData(TrackingCommitSocketProxy.CutPoint.AfterCommitServerReady, true)]
    public async Task AmbiguousFinalizeCommitIsReconstructedFromDurableState(
        TrackingCommitSocketProxy.CutPoint cutPoint, bool expectedCommit)
    {
        var direct = TestDatabase.ConnectionString();
        using var factory = new ReferenceDataHttpTests.ApiFactory(direct);
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var target = await G9Support.SeedResolvable(factory, fixture);
        var source = await G9Support.NewSource(factory, fixture.A, "g9_wire");
        var receipt = await G9Support.Accept(factory, source,
            G9Support.EquipmentPayload("g9-wire-" + cutPoint.ToString().ToLowerInvariant() + "-" +
                Guid.NewGuid().ToString("N")[..8], target.Reference));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var clean = new TrackingWorkStore(db, new TrackingWorkerSettings());
        var claim = await clean.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.NotNull(claim);
        Assert.Equal(receipt, claim!.ReceiptId);

        await using var proxy = new TrackingCommitSocketProxy(direct, cutPoint,
            "INSERT INTO tracking_processing_effects");
        using var faultyFactory = new ReferenceDataHttpTests.ApiFactory(proxy.ProxiedConnectionString(direct));
        await using var faultyScope = faultyFactory.Services.CreateAsyncScope();
        var faultyDb = faultyScope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var faulty = new TrackingWorkStore(faultyDb, new TrackingWorkerSettings());
        await Assert.ThrowsAnyAsync<Exception>(() => faulty.FinalizeAsync(claim, CancellationToken.None));
        Assert.True(proxy.Triggered, "A real COMMIT wire exchange must have been interrupted");

        // The independent direct connection is the oracle, never the failed client call.
        var state = (await db.TrackingInboxWorkItems.AsNoTracking().SingleAsync(x => x.ReceiptId == receipt)).State;
        var effects = await db.TrackingProcessingEffects.CountAsync(x => x.ReceiptId == receipt);
        Assert.Equal(expectedCommit ? "Processed" : "Leased", state);
        Assert.Equal(expectedCommit ? 1 : 0, effects);

        if (expectedCommit)
        {
            // The failure path must not downgrade a durable success, and a retry must not repeat it.
            Assert.False(await clean.FailAsync(claim, "transient_dependency", true, CancellationToken.None));
            Assert.Equal(TrackingWorkDisposition.Stale, await clean.FinalizeAsync(claim, CancellationToken.None));
        }
        else
        {
            Assert.Equal(TrackingWorkDisposition.Processed, await clean.FinalizeAsync(claim, CancellationToken.None));
            Assert.Equal(TrackingWorkDisposition.Stale, await clean.FinalizeAsync(claim, CancellationToken.None));
        }
        Assert.Equal(1, await db.TrackingProcessingEffects.CountAsync(x => x.ReceiptId == receipt));
        Assert.Equal("Processed", (await db.TrackingInboxWorkItems.AsNoTracking()
            .SingleAsync(x => x.ReceiptId == receipt)).State);
        Assert.Equal(1, await db.TrackingWorkAttemptEvents.CountAsync(x => x.ReceiptId == receipt && x.EventKind == "Processed"));
        Assert.Equal(1, await db.TrackingInboxReceipts.CountAsync(x => x.Id == receipt));
    }
}
