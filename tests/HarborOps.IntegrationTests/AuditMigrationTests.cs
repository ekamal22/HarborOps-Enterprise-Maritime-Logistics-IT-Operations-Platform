using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class AuditMigrationTests
{
    [Fact]
    public async Task AcceptedM8UpgradeAddsImmutableAuditWithoutChangingPriorRecords()
    {
        var connection = TestDatabase.ConnectionString();
        var schema = "g16_history_" + Guid.NewGuid().ToString("N");
        var scoped = new NpgsqlConnectionStringBuilder(connection)
        {
            SearchPath = schema + ",public"
        };
        await using var administrator = new NpgsqlConnection(connection);
        await administrator.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", administrator))
            await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(scoped.ConnectionString).Options;
            await using var database = new HarborOpsDbContext(options);
            await database.Database.MigrateAsync("20261103190000_TrackingInboxAndHistory");
            Assert.Equal(9, (await database.Database.GetAppliedMigrationsAsync()).Count());

            var email = "g16-history-" + Guid.NewGuid().ToString("N") + "@example.test";
            var identities = new IdentityStore(database, new PasswordHasher(), TimeProvider.System);
            var organizations = new OrganizationStore(database, TimeProvider.System);
            await identities.ProvisionAsync(email, "Synthetic Audit History User",
                "Synthetic-audit-upgrade-password-123", CancellationToken.None);
            var org = await organizations.BootstrapAsync("g16-history-" +
                Guid.NewGuid().ToString("N")[..12], "Original Organization",
                email, CancellationToken.None);
            var existing = await database.Organizations.AsNoTracking()
                .SingleAsync(x => x.Id == org.Id);
            Assert.Equal("Original Organization", existing.DisplayName);

            await database.Database.MigrateAsync();
            Assert.Equal(13, (await database.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Empty(await database.Database.GetPendingMigrationsAsync());
            Assert.Empty(await database.AuditEntries.AsNoTracking().ToListAsync());
            Assert.Equal("Original Organization", (await database.Organizations.AsNoTracking()
                .SingleAsync(x => x.Id == org.Id)).DisplayName);

            var account = await identities.LoginAsync(email,
                "Synthetic-audit-upgrade-password-123", "g16-login", CancellationToken.None);
            Assert.NotNull(account);
            var selected = await organizations.SelectAsync(account!.UserId, account.SessionId,
                org.Id, CancellationToken.None);
            Assert.NotNull(selected.Value);
            var change = await organizations.UpdateProfileAsync(account.UserId,
                account.SessionId, org.Id, "Audited Organization", org.Version,
                "g16-update", CancellationToken.None);
            Assert.NotNull(change.Value);
            var entry = await database.AuditEntries.AsNoTracking().SingleAsync(e =>
                e.OrganizationId == org.Id && e.EventType == "organizations.update");
            Assert.Equal(account.UserId, entry.ActorUserId);
            Assert.Equal(account.SessionId, entry.ActorSessionId);
            Assert.Equal("g16-update", entry.CorrelationId);
            Assert.Equal("Succeeded", entry.Outcome);

            var before = await database.AuditEntries.CountAsync();
            await Assert.ThrowsAsync<PostgresException>(() =>
                database.Database.MigrateAsync("20261103190000_TrackingInboxAndHistory"));
            Assert.Equal(10, (await database.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(before, await database.AuditEntries.CountAsync());
            Assert.Equal("Audited Organization", (await database.Organizations.AsNoTracking()
                .SingleAsync(x => x.Id == org.Id)).DisplayName);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", administrator);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
