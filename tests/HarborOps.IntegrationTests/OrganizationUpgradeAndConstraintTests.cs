using HarborOps.Domain;
using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class OrganizationUpgradeAndConstraintTests
{
    private const string Password = "Synthetic-G2-migration-passphrase-123";

    [Fact]
    public async Task RealM0M1M2UpgradePreservesLegacyAccountSessionAndBaselineOnRepeat()
    {
        // The administrator connection is restricted to the disposable integration server.
        var source = new NpgsqlConnectionStringBuilder(TestDatabase.ConnectionString());
        var dbName = "harborops_m2_rehearsal_" + Guid.NewGuid().ToString("N")[..16];
        source.Database = "postgres";
        source.Pooling = false;
        await using var admin = new NpgsqlConnection(source.ConnectionString);
        await admin.OpenAsync();
        // The name is generated entirely from a fixed prefix and random hex.
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{dbName}\"", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            source.Database = dbName;
            var connection = source.ConnectionString;
            using var provider = new ServiceCollection().AddHarborOpsPersistence(connection)
                .BuildServiceProvider();
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            await db.Database.MigrateAsync("20261030170000_InitialBootstrap");
            await BootstrapSeeder.SeedAsync(db, CancellationToken.None);
            Assert.Single(await db.BootstrapRecords.ToListAsync());

            await db.Database.MigrateAsync("20261031010000_IdentitySessions");
            Assert.Equal(2, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.False(await db.Database.SqlQueryRaw<int>("""
                SELECT count(*)::integer AS "Value" FROM information_schema.columns
                WHERE table_name = 'auth_sessions' AND column_name = 'selected_organization_id'
                """).SingleAsync() > 0);

            var user = Guid.NewGuid();
            var session = Guid.NewGuid();
            var email = "legacy-" + Guid.NewGuid().ToString("N") + "@example.test";
            var hash = await scope.ServiceProvider.GetRequiredService<PasswordHasher>()
                .HashAsync(Password, CancellationToken.None);
            var now = DateTimeOffset.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO users (id, email, normalized_email, display_name, status, security_version, created_at_utc)
                VALUES ({user}, {email}, {IdentityStore.NormalizeEmail(email)}, {"Existing M1 User"}, {"Active"}, {1L}, {now});
                INSERT INTO user_credentials (user_id, password_hash, hash_algorithm, password_changed_at_utc, created_at_utc)
                VALUES ({user}, {hash}, {"argon2id"}, {now}, {now});
                INSERT INTO auth_sessions
                (id, user_id, issued_at_utc, last_seen_at_utc, idle_expires_at_utc, absolute_expires_at_utc,
                 security_version, revoked_at_utc)
                VALUES ({session}, {user}, {now}, {now}, {now.AddMinutes(30)},
                        {now.AddHours(12)}, {1L}, {(DateTimeOffset?)null});
                """);

            await db.Database.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(
                [
                    "20261030170000_InitialBootstrap", "20261031010000_IdentitySessions",
                    "20261031020000_OrganizationMemberships", "20261031110000_PortVesselReferences"
                ],
                (await db.Database.GetAppliedMigrationsAsync()).ToArray());
            var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
            var legacy = await identity.ValidateAsync(user, session, 1, CancellationToken.None);
            Assert.NotNull(legacy);
            Assert.Equal(email, legacy!.Email);
            var login = await identity.LoginAsync(email, Password, "legacy-upgrade", CancellationToken.None);
            Assert.NotNull(login);
            var store = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
            var org = await store.BootstrapAsync("legacy-" + Guid.NewGuid().ToString("N")[..12],
                "Legacy Account Organization", email, CancellationToken.None);
            Assert.NotNull((await store.SelectAsync(user, session, org.Id, CancellationToken.None)).Value);
            Assert.NotNull((await store.ResolveAsync(user, session, org.Id,
                OrganizationPermission.ManageMemberships, CancellationToken.None)).Value);
            await db.Database.MigrateAsync();
            await BootstrapSeeder.SeedAsync(db, CancellationToken.None);
            Assert.Single(await db.BootstrapRecords.ToListAsync());
            Assert.Single(await db.AuthUsers.Where(x => x.Id == user).ToListAsync());
            Assert.Equal(2, await db.AuthSessions.CountAsync(x => x.UserId == user));
            Assert.Single(await db.Organizations.Where(x => x.Id == org.Id).ToListAsync());
            Assert.Single(await db.OrganizationMemberships.Where(x => x.OrganizationId == org.Id).ToListAsync());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            // Only the unique disposable database created by this test is deleted.
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{dbName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task InvalidForeignKeysDuplicateSlugAndDisabledBootstrapAreRejected()
    {
        using var provider = new ServiceCollection()
            .AddHarborOpsPersistence(TestDatabase.ConnectionString()).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var organizations = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var email = "fk-" + nonce + "@example.test";
        var disabledEmail = "disabled-" + nonce + "@example.test";
        await identity.ProvisionAsync(email, "Active Person", Password, CancellationToken.None);
        await identity.ProvisionAsync(disabledEmail, "Disabled Person", Password, CancellationToken.None);
        var disabledId = await db.AuthUsers.Where(x => x.NormalizedEmail ==
            IdentityStore.NormalizeEmail(disabledEmail)).Select(x => x.Id).SingleAsync();
        await db.AuthUsers.Where(x => x.Id == disabledId).ExecuteUpdateAsync(
            p => p.SetProperty(x => x.Status, "Disabled").SetProperty(x => x.DisabledAtUtc, DateTimeOffset.UtcNow));
        var slug = "g2-fk-" + nonce[..12];
        await Assert.ThrowsAsync<ArgumentException>(() =>
            organizations.BootstrapAsync(slug, "Must Fail", disabledEmail, CancellationToken.None));
        var org = await organizations.BootstrapAsync(slug, "Valid Org", email, CancellationToken.None);
        var now = DateTimeOffset.UtcNow;

        async Task<PostgresException> Reject(Func<Task<int>> action, string expected) {
            var ex = await Assert.ThrowsAsync<PostgresException>(action);
            Assert.Equal(expected, ex.SqlState);
            return ex;
        }

        await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO organizations (id, slug, display_name, status, version, created_at_utc, updated_at_utc)
            VALUES ({Guid.NewGuid()}, {slug}, {"Duplicate"}, {"Active"}, {1L}, {now}, {now})
            """), PostgresErrorCodes.UniqueViolation);
        await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO organization_memberships (id, organization_id, user_id, role, status, version, created_at_utc, updated_at_utc)
            VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, {disabledId}, {"Analyst"}, {"Active"}, {1L}, {now}, {now})
            """), PostgresErrorCodes.ForeignKeyViolation);
        await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO organization_memberships (id, organization_id, user_id, role, status, version, created_at_utc, updated_at_utc)
            VALUES ({Guid.NewGuid()}, {org.Id}, {Guid.NewGuid()}, {"Analyst"}, {"Active"}, {1L}, {now}, {now})
            """), PostgresErrorCodes.ForeignKeyViolation);
        await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO organizations (id, slug, display_name, status, version, created_at_utc, updated_at_utc)
            VALUES ({Guid.NewGuid()}, {"g2-blank-" + nonce[..10]}, {" "}, {"Active"}, {1L}, {now}, {now})
            """), PostgresErrorCodes.CheckViolation);
        Assert.Single(await db.Organizations.Where(x => x.Slug == slug).ToListAsync());
        Assert.Single(await db.OrganizationMemberships.Where(x => x.OrganizationId == org.Id).ToListAsync());
    }
}
