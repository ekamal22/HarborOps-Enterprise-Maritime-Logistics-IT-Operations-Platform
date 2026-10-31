using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.References;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class ReferenceHistoricalUpgradeTests
{
    [Fact]
    public async Task M2SeedAndAuthenticatedSessionSurviveM3UpgradeAndRepeat()
    {
        // The guarded database is disposable. A uniquely named schema avoids touching
        // any existing M0-M3 records or EF migration history in the test database.
        var connectionString = TestDatabase.ConnectionString();
        var schema = "g3_" + Guid.NewGuid().ToString("N");
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
        await using var server = new NpgsqlConnection(connectionString);
        await server.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", server))
            await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(builder.ConnectionString).Options;
            await using var db = new HarborOpsDbContext(options);
            await db.Database.MigrateAsync("20261031020000_OrganizationMemberships");
            await BootstrapSeeder.SeedAsync(db, CancellationToken.None);
            var identity = new IdentityStore(db, new PasswordHasher(), TimeProvider.System);
            var organizations = new OrganizationStore(db, TimeProvider.System);
            var email = "m3-upgrade-" + Guid.NewGuid().ToString("N") + "@example.test";
            var password = "Synthetic-strong-migration-test-password-123";
            await identity.ProvisionAsync(email, "Synthetic Upgrade Account", password, CancellationToken.None);
            var organization = await organizations.BootstrapAsync(
                "g3-upgrade-" + Guid.NewGuid().ToString("N")[..12], "Synthetic Upgrade Tenant",
                email, CancellationToken.None);
            var session = await identity.LoginAsync(email, password, "g3-historical-upgrade", CancellationToken.None);
            Assert.NotNull(session);
            var selected = await organizations.SelectAsync(session!.UserId, session.SessionId,
                organization.Id, CancellationToken.None);
            Assert.NotNull(selected.Value);
            var before = await identity.ValidateAsync(session.UserId, session.SessionId,
                session.SecurityVersion, CancellationToken.None);
            Assert.NotNull(before);
            var seeded = await db.BootstrapRecords.AsNoTracking().SingleAsync();
            var users = await db.AuthUsers.CountAsync();
            var memberships = await db.OrganizationMemberships.CountAsync();

            await db.Database.MigrateAsync();
            Assert.Equal(4, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(seeded.DatasetVersion,
                (await db.BootstrapRecords.AsNoTracking().SingleAsync()).DatasetVersion);
            Assert.Equal(users, await db.AuthUsers.CountAsync());
            Assert.Equal(memberships, await db.OrganizationMemberships.CountAsync());
            Assert.Equal(organization.Id,
                (await db.AuthSessions.AsNoTracking().SingleAsync(s => s.Id == session.SessionId))
                    .SelectedOrganizationId);
            Assert.NotNull(await identity.ValidateAsync(session.UserId, session.SessionId,
                session.SecurityVersion, CancellationToken.None));
            Assert.Equal(0, await db.Ports.CountAsync());
            Assert.Equal(0, await db.Vessels.CountAsync());

            await db.Database.MigrateAsync();
            Assert.Equal(4, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(users, await db.AuthUsers.CountAsync());
            Assert.Equal(memberships, await db.OrganizationMemberships.CountAsync());
        }
        finally
        {
            await using var remove = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", server);
            await remove.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task DirectSqlConstraintsAndIdentityTriggersRejectInvalidMutations()
    {
        var connectionString = TestDatabase.ConnectionString();
        var schema = "g3_" + Guid.NewGuid().ToString("N");
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
        await using var server = new NpgsqlConnection(connectionString);
        await server.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", server))
            await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(builder.ConnectionString).Options;
            await using var db = new HarborOpsDbContext(options);
            await db.Database.MigrateAsync();
            var identity = new IdentityStore(db, new PasswordHasher(), TimeProvider.System);
            var organizations = new OrganizationStore(db, TimeProvider.System);
            var email = "m3-constraints-" + Guid.NewGuid().ToString("N") + "@example.test";
            await identity.ProvisionAsync(email, "Synthetic Constraint User",
                "Synthetic-constraint-test-password-123", CancellationToken.None);
            var org = await organizations.BootstrapAsync("g3-constraint-" +
                Guid.NewGuid().ToString("N")[..10], "Synthetic Constraint Tenant",
                email, CancellationToken.None);
            var now = DateTimeOffset.UtcNow;
            var port = new PortReference
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ReferenceCode = "FIXED-PORT",
                Name = "Synthetic Port", CountryCode = "US", Status = "Active",
                Version = 1, SourceKind = "Synthetic", CreatedAtUtc = now, UpdatedAtUtc = now
            };
            var vessel = new VesselReference
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ReferenceCode = "FIXED-SHIP",
                Name = "Synthetic Vessel", Status = "Active", SourceKind = "Synthetic",
                Version = 1, CreatedAtUtc = now, UpdatedAtUtc = now
            };
            db.Ports.Add(port); db.Vessels.Add(vessel);
            await db.SaveChangesAsync();

            async Task MustFail(string sql)
            {
                var error = await Record.ExceptionAsync(() => db.Database.ExecuteSqlRawAsync(sql));
                Assert.NotNull(error);
                var pg = error as PostgresException ?? error.InnerException as PostgresException;
                Assert.NotNull(pg);
                Assert.Contains(pg!.SqlState, new[] { "23503", "23505", "23514" });
            }
            await MustFail($"UPDATE ports SET organization_id = '{Guid.NewGuid()}' WHERE id = '{port.Id}'");
            await MustFail($"UPDATE ports SET reference_code = 'OTHER-PORT' WHERE id = '{port.Id}'");
            await MustFail($"UPDATE vessels SET reference_code = 'OTHER-SHIP' WHERE id = '{vessel.Id}'");
            await MustFail($"UPDATE vessels SET source_kind = 'UserProvided' WHERE id = '{vessel.Id}'");
            await MustFail($"UPDATE ports SET version = 0 WHERE id = '{port.Id}'");
            await MustFail($"UPDATE vessels SET status = 'Removed' WHERE id = '{vessel.Id}'");
            await MustFail($"UPDATE ports SET country_code = 'USA' WHERE id = '{port.Id}'");
            await MustFail($"UPDATE vessels SET imo_number = '9074728' WHERE id = '{vessel.Id}'");
            await MustFail($"UPDATE vessels SET mmsi = '000000000' WHERE id = '{vessel.Id}'");
            Assert.Equal("FIXED-PORT",
                (await db.Ports.AsNoTracking().SingleAsync(x => x.Id == port.Id)).ReferenceCode);
            Assert.Equal("FIXED-SHIP",
                (await db.Vessels.AsNoTracking().SingleAsync(x => x.Id == vessel.Id)).ReferenceCode);
        }
        finally
        {
            await using var remove = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", server);
            await remove.ExecuteNonQueryAsync();
        }
    }
}
