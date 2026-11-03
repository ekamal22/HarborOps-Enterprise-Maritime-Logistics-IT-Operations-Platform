using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class TrackingInboxMigrationTests
{
    [Fact]
    public async Task ExistingM7SourceAndCredentialSurviveM8UpgradeAndEmptyRollback()
    {
        var connection = TestDatabase.ConnectionString();
        var schema = "g8_" + Guid.NewGuid().ToString("N");
        var scoped = new NpgsqlConnectionStringBuilder(connection)
        {
            SearchPath = schema + ",public"
        };
        await using var administrative = new NpgsqlConnection(connection);
        await administrative.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", administrative))
            await command.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(scoped.ConnectionString).Options;
            await using var db = new HarborOpsDbContext(options);
            await db.Database.MigrateAsync("20261103090000_IntegrationSources");
            Assert.Equal(8, (await db.Database.GetAppliedMigrationsAsync()).Count());

            var email = "g8-upgrade-" + Guid.NewGuid().ToString("N") + "@example.test";
            var identity = new IdentityStore(db, new PasswordHasher(), TimeProvider.System);
            var orgs = new OrganizationStore(db, TimeProvider.System);
            await identity.ProvisionAsync(email, "Synthetic Upgrade Operator",
                "Synthetic-g8-upgrade-passphrase-123", CancellationToken.None);
            var org = await orgs.BootstrapAsync("g8-upgrade-" +
                Guid.NewGuid().ToString("N")[..10], "G8 Upgrade",
                email, CancellationToken.None);
            var store = new IntegrationSourceStore(db, TimeProvider.System);
            var source = await store.CreateAsync(org.Id, "m8_upgrade", "Upgrade Source",
                CancellationToken.None);
            var before = await db.IntegrationCredentials.AsNoTracking()
                .SingleAsync(x => x.Id == source.CredentialId);
            Assert.NotNull(await store.ValidateAsync(source.Bearer, CancellationToken.None));

            await db.Database.MigrateAsync();
            Assert.Equal(9, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            var after = await db.IntegrationCredentials.AsNoTracking()
                .SingleAsync(x => x.Id == source.CredentialId);
            Assert.Equal(before.SecretDigest, after.SecretDigest);
            Assert.Equal(before.SourceId, after.SourceId);
            Assert.Equal(before.ExpiresAtUtc, after.ExpiresAtUtc);
            Assert.NotNull(await store.ValidateAsync(source.Bearer, CancellationToken.None));
            Assert.Empty(await db.TrackingInboxReceipts.ToListAsync());

            // This test owns a disposable schema and has not inserted M8 events.
            // Never run a Down migration against retained accepted receipt history.
            await db.Database.MigrateAsync("20261103090000_IntegrationSources");
            Assert.Equal(8, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.NotNull(await store.ValidateAsync(source.Bearer, CancellationToken.None));
            await db.Database.MigrateAsync();
            Assert.Equal(9, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.NotNull(await store.ValidateAsync(source.Bearer, CancellationToken.None));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", administrative);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
