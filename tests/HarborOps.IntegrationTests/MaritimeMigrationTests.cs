using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Maritime;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.References;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class MaritimeMigrationTests
{
    [Fact]
    public async Task SeededM3HistorySurvivesM4UpgradeRollbackAndReapply()
    {
        var source = TestDatabase.ConnectionString();
        var schema = "g4_" + Guid.NewGuid().ToString("N");
        var cs = new NpgsqlConnectionStringBuilder(source) { SearchPath = schema };
        await using var admin = new NpgsqlConnection(source);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(cs.ConnectionString).Options;
            await using var db = new HarborOpsDbContext(options);
            await db.Database.MigrateAsync("20261031110000_PortVesselReferences");
            var identities = new IdentityStore(db, new PasswordHasher(), TimeProvider.System);
            var organizations = new OrganizationStore(db, TimeProvider.System);
            var email = "g4-upgrade-" + Guid.NewGuid().ToString("N") + "@example.test";
            await identities.ProvisionAsync(email, "M4 Migration Fixture",
                "Synthetic-G4-strong-test-password-123", CancellationToken.None);
            var org = await organizations.BootstrapAsync(
                "g4-upgrade-" + Guid.NewGuid().ToString("N")[..12],
                "M4 Upgrade Organization", email, CancellationToken.None);
            var now = DateTimeOffset.UtcNow;
            var port = new PortReference {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ReferenceCode = "HIST-PORT",
                Name = "Historical Port", CountryCode = "US", SourceKind = "Synthetic",
                CreatedAtUtc = now, UpdatedAtUtc = now
            };
            var vessel = new VesselReference {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ReferenceCode = "HIST-VESSEL",
                Name = "Historical Vessel", SourceKind = "Synthetic",
                CreatedAtUtc = now, UpdatedAtUtc = now
            };
            db.Ports.Add(port); db.Vessels.Add(vessel);
            await db.SaveChangesAsync();
            await db.Database.MigrateAsync();
            Assert.Equal(5, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal("HIST-PORT", (await db.Ports.AsNoTracking().SingleAsync()).ReferenceCode);
            Assert.Equal("HIST-VESSEL", (await db.Vessels.AsNoTracking().SingleAsync()).ReferenceCode);
            Assert.Empty(await db.Voyages.ToListAsync());
            Assert.Empty(await db.Containers.ToListAsync());
            await db.Database.MigrateAsync();
            Assert.Equal(5, (await db.Database.GetAppliedMigrationsAsync()).Count());
            await db.Database.MigrateAsync("20261031110000_PortVesselReferences");
            Assert.Equal(4, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(port.Id, (await db.Ports.SingleAsync()).Id);
            Assert.Equal(vessel.Id, (await db.Vessels.SingleAsync()).Id);
            await db.Database.MigrateAsync();
            Assert.Equal(5, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(1, await db.AuthUsers.CountAsync());
            Assert.Equal(1, await db.OrganizationMemberships.CountAsync());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task DirectSqlRejectsCrossTenantVoyagesBadIdentifiersAndMutatedIdentity()
    {
        var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
            .UseNpgsql(TestDatabase.ConnectionString()).Options;
        await using var db = new HarborOpsDbContext(options);
        await db.Database.MigrateAsync();
        var identities = new IdentityStore(db, new PasswordHasher(), TimeProvider.System);
        var organizations = new OrganizationStore(db, TimeProvider.System);
        var nonce = Guid.NewGuid().ToString("N");
        var emailA = "g4-sql-a-" + nonce + "@example.test";
        var emailB = "g4-sql-b-" + nonce + "@example.test";
        await identities.ProvisionAsync(emailA, "M4 SQL A", "Synthetic-SQL-passphrase-123", CancellationToken.None);
        await identities.ProvisionAsync(emailB, "M4 SQL B", "Synthetic-SQL-passphrase-123", CancellationToken.None);
        var a = await organizations.BootstrapAsync("g4-sql-a-" + nonce[..10], "M4 SQL A", emailA, CancellationToken.None);
        var b = await organizations.BootstrapAsync("g4-sql-b-" + nonce[..10], "M4 SQL B", emailB, CancellationToken.None);
        var now = DateTimeOffset.UtcNow;
        var p = new PortReference {
            Id = Guid.NewGuid(), OrganizationId = a.Id, ReferenceCode = "SQL-PORT-A", Name = "Port A",
            CountryCode = "US", SourceKind = "Synthetic", CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var v = new VesselReference {
            Id = Guid.NewGuid(), OrganizationId = b.Id, ReferenceCode = "SQL-VESSEL-B", Name = "Vessel B",
            SourceKind = "Synthetic", CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.Ports.Add(p); db.Vessels.Add(v); await db.SaveChangesAsync();
        var crossSql = $"""
            INSERT INTO voyages (id,organization_id,reference_code,vessel_id,origin_port_id,destination_port_id,
              voyage_status,record_status,source_kind,version,created_at_utc,updated_at_utc)
            VALUES ('{Guid.NewGuid()}','{a.Id}','SQL-FOREIGN','{v.Id}','{p.Id}','{Guid.NewGuid()}',
              'Planned','Active','Synthetic',1,now(),now())
            """;
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(crossSql));
        var badIsoSql = $"""
            INSERT INTO containers (id,organization_id,reference_code,identifier_kind,container_identifier,
              equipment_type,record_status,source_kind,version,created_at_utc,updated_at_utc)
            VALUES ('{Guid.NewGuid()}','{a.Id}','SQL-INVALID','ISO6346','CSQU3054382',
              'Dry20','Active','UserProvided',1,now(),now())
            """;
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(badIsoSql));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        var container = new ContainerRecord {
            Id = Guid.NewGuid(), OrganizationId = a.Id,
            ReferenceCode = "SQL-VALID", IdentifierKind = "Synthetic",
            ContainerIdentifier = "SYN-BOX-VALID", EquipmentType = "Dry20",
            SourceKind = "Synthetic", CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.Containers.Add(container);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE containers SET container_identifier = {"SYN-BOX-TAMPER"}, version = {2L} WHERE id = {container.Id}"));
        Assert.Equal("SYN-BOX-VALID",
            (await db.Containers.AsNoTracking().SingleAsync(x => x.Id == container.Id)).ContainerIdentifier);
    }
}
