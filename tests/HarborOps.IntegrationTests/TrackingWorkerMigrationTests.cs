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

public sealed class TrackingWorkerMigrationTests
{
    [Fact]
    public async Task PopulatedM8ReceiptSurvivesM9UpgradeAndClaimedWorkPreventsDowngrade()
    {
        var connection = TestDatabase.ConnectionString();
        var schema = "g9_upgrade_" + Guid.NewGuid().ToString("N");
        var scoped = new NpgsqlConnectionStringBuilder(connection)
        {
            SearchPath = schema + ",public"
        };
        await using var administration = new NpgsqlConnection(connection);
        await administration.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", administration))
            await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(scoped.ConnectionString).Options;
            await using var db = new HarborOpsDbContext(options);
            await db.Database.MigrateAsync("20261103190000_TrackingInboxAndHistory");
            Assert.Equal(9, (await db.Database.GetAppliedMigrationsAsync()).Count());
            var nonce = Guid.NewGuid().ToString("N");
            var email = "g9-upgrade-" + nonce + "@example.test";
            var identity = new IdentityStore(db, new PasswordHasher(), TimeProvider.System);
            await identity.ProvisionAsync(email, "Synthetic Upgrade Operator",
                "Synthetic-g9-upgrade-password-123", CancellationToken.None);
            var organizations = new OrganizationStore(db, TimeProvider.System);
            var org = await organizations.BootstrapAsync("g9-upgrade-" + nonce[..10],
                "G9 Upgrade Tenant", email, CancellationToken.None);
            var sources = new IntegrationSourceStore(db, TimeProvider.System);
            var source = await sources.CreateAsync(org.Id, "g9_migrate", "Upgrade Source",
                CancellationToken.None);
            var raw = Encoding.UTF8.GetBytes("""
                {"sourceEventId":"g9-preserve-original","eventFamily":"Equipment",
                 "eventCode":"GTIN","classifier":"ACT","occurredAtUtc":"2026-11-03T10:00:00Z",
                 "equipmentReference":"SYN-BOX-ABCD","externalSchemaVersion":"2.2.0"}
                """);
            var value = TrackingEventPolicy.Parse(raw, TimeProvider.System.GetUtcNow()).Value!;
            var received = await new TrackingInboxStore(db, TimeProvider.System,
                new ConfigurationBuilder().Build(), new NoopTrackingReceiptCommitHook())
                .ReceiveAsync(value.Bind(source.Id, org.Id), source.Bearer, raw,
                    "g9-migration", CancellationToken.None);
            Assert.Equal(TrackingReceiptOutcome.Received, received.Outcome);
            var original = await db.TrackingInboxReceipts.AsNoTracking()
                .SingleAsync(x => x.Id == received.ReceiptId);
            var eventOriginal = await db.TrackingAcceptedEvents.AsNoTracking()
                .SingleAsync(x => x.ReceiptId == received.ReceiptId);

            await db.Database.MigrateAsync();
            Assert.Equal(10, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            var after = await db.TrackingInboxReceipts.AsNoTracking()
                .SingleAsync(x => x.Id == received.ReceiptId);
            var afterEvent = await db.TrackingAcceptedEvents.AsNoTracking()
                .SingleAsync(x => x.ReceiptId == received.ReceiptId);
            Assert.Equal(original.CanonicalDigest, after.CanonicalDigest);
            Assert.Equal(original.CanonicalPayloadUtf8, after.CanonicalPayloadUtf8);
            Assert.Equal(eventOriginal.RawPayloadUtf8, afterEvent.RawPayloadUtf8);
            var pending = await db.TrackingInboxWorkItems.AsNoTracking()
                .SingleAsync(x => x.ReceiptId == received.ReceiptId);
            Assert.Equal("Pending", pending.State);
            Assert.Equal(0, pending.AttemptCount);
            Assert.True(pending.AvailableAtUtc > DateTimeOffset.MinValue);

            var token = Guid.NewGuid();
            var updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE tracking_inbox_work_items
                   SET state={"Leased"},attempt_count={1},lease_epoch={1L},
                       lease_token={token},
                       lease_expires_at_utc=clock_timestamp()+interval '60 seconds',
                       updated_at_utc=clock_timestamp()
                 WHERE receipt_id={received.ReceiptId}
                """);
            Assert.Equal(1, updated);
            var blocked = await Assert.ThrowsAsync<PostgresException>(() =>
                db.Database.MigrateAsync("20261103190000_TrackingInboxAndHistory"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, blocked.SqlState);
            Assert.Equal(10, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(1, await db.TrackingInboxReceipts.CountAsync(x => x.Id == received.ReceiptId));
            Assert.Equal(1, await db.TrackingAcceptedEvents.CountAsync(x => x.ReceiptId == received.ReceiptId));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", administration);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
