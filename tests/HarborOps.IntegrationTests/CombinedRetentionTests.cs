using System.Text;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

/// <summary>
/// Combined M9/M16 retention: immutable M9 attempt history must block a destructive downgrade even when the
/// work item currently looks like untouched M8 work (a redriven item is Pending with attempt count zero).
/// The M8 constraint that is re-added by the downgrade would not catch that case on its own.
/// </summary>
public sealed class CombinedRetentionTests
{
    [Fact]
    public async Task RetainedAttemptHistoryOnRedrivenPendingWorkBlocksTheM9Downgrade()
    {
        var connection = TestDatabase.ConnectionString();
        var schema = "cg16_retention_" + Guid.NewGuid().ToString("N");
        var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema + ",public" };
        await using var administration = new NpgsqlConnection(connection);
        await administration.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", administration))
            await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new HarborOpsDbContext(new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(scoped.ConnectionString).Options);
            await db.Database.MigrateAsync();
            Assert.Equal(16, (await db.Database.GetAppliedMigrationsAsync()).Count());

            var nonce = Guid.NewGuid().ToString("N");
            var email = "cg16-retention-" + nonce + "@example.test";
            await new IdentityStore(db, new PasswordHasher(), TimeProvider.System).ProvisionAsync(
                email, "Synthetic Retention Operator", "Synthetic-cg16-retention-password-123", CancellationToken.None);
            var user = await db.AuthUsers.AsNoTracking().SingleAsync(x =>
                x.NormalizedEmail == IdentityStore.NormalizeEmail(email));
            var org = await new OrganizationStore(db, TimeProvider.System).BootstrapAsync(
                "cg16-ret-" + nonce[..10], "Retention Tenant", email, CancellationToken.None);
            var source = await new IntegrationSourceStore(db, TimeProvider.System).CreateAsync(
                org.Id, "cg16_retention", "Retention Source", CancellationToken.None);
            var raw = Encoding.UTF8.GetBytes("""
                {"sourceEventId":"cg16-retention-original","eventFamily":"Equipment",
                 "eventCode":"GTIN","classifier":"ACT","occurredAtUtc":"2026-11-03T10:00:00Z",
                 "equipmentReference":"SYN-BOX-UNKNOWN","externalSchemaVersion":"2.2.0"}
                """);
            var value = TrackingEventPolicy.Parse(raw, TimeProvider.System.GetUtcNow()).Value!;
            var received = await new TrackingInboxStore(db, TimeProvider.System,
                new ConfigurationBuilder().Build(), new NoopTrackingReceiptCommitHook())
                .ReceiveAsync(value.Bind(source.Id, org.Id), source.Bearer, raw, "cg16-retention",
                    CancellationToken.None);
            Assert.Equal(TrackingReceiptOutcome.Received, received.Outcome);

            var worker = new TrackingWorkStore(db, new TrackingWorkerSettings());
            var claim = await worker.ClaimOneAsync(org.Id, CancellationToken.None);
            Assert.NotNull(claim);
            Assert.Equal(TrackingWorkDisposition.NeedsReconciliation,
                await worker.FinalizeAsync(claim!, CancellationToken.None));

            // Attributed manual redrive, exactly as the Manager transaction does it.
            var parked = await db.TrackingInboxWorkItems.AsNoTracking()
                .SingleAsync(x => x.ReceiptId == received.ReceiptId);
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE tracking_inbox_work_items
                       SET state='Pending', attempt_count=0, redrive_generation=redrive_generation+1,
                           lease_epoch=lease_epoch+1, lease_token=NULL, lease_expires_at_utc=NULL,
                           available_at_utc=clock_timestamp(), last_error_code=NULL, completed_at_utc=NULL,
                           resolution_kind=NULL, updated_at_utc=clock_timestamp()
                     WHERE receipt_id={received.ReceiptId}
                    """);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO tracking_work_attempt_events
                        (id,receipt_id,organization_id,source_id,generation,attempt_count,lease_epoch,
                         event_kind,reason_code,actor_user_id,redrive_reason,occurred_at_utc)
                    VALUES ({Guid.NewGuid()},{received.ReceiptId},{org.Id},{source.Id},{parked.RedriveGeneration + 1},0,
                            {parked.LeaseEpoch + 1},'Redriven','manual_redrive',{user.Id},
                            'operator redrive for retention test',clock_timestamp())
                    """);
                await tx.CommitAsync();
            }
            var pending = await db.TrackingInboxWorkItems.AsNoTracking()
                .SingleAsync(x => x.ReceiptId == received.ReceiptId);
            Assert.Equal("Pending", pending.State);
            Assert.Equal(0, pending.AttemptCount);
            Assert.Equal(3, await db.TrackingWorkAttemptEvents.CountAsync(x => x.ReceiptId == received.ReceiptId));

            // The only thing that can stop this downgrade is the M9 retention guard itself.
            var blocked = await Assert.ThrowsAsync<PostgresException>(() =>
                db.Database.MigrateAsync("20261103190000_TrackingInboxAndHistory"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, blocked.SqlState);
            Assert.Contains("M9 work evidence prevents downgrade", blocked.MessageText);
            Assert.Equal(11, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(3, await db.TrackingWorkAttemptEvents.CountAsync(x => x.ReceiptId == received.ReceiptId));
            Assert.Equal(1, await db.TrackingInboxReceipts.CountAsync(x => x.Id == received.ReceiptId));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", administration);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
