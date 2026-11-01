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
            var session = (await identities.LoginAsync(email,
                "Synthetic-G4-strong-test-password-123", "g4-historical-upgrade",
                CancellationToken.None))!;
            Assert.NotNull((await organizations.SelectAsync(session.UserId, session.SessionId,
                org.Id, CancellationToken.None)).Value);
            var beforeSession = await db.AuthSessions.AsNoTracking()
                .SingleAsync(x => x.Id == session.SessionId);
            Assert.Equal(org.Id, beforeSession.SelectedOrganizationId);
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
            await db.Database.MigrateAsync("20261101010000_VoyageContainerReferences");
            Assert.Equal(5, (await db.Database.GetAppliedMigrationsAsync()).Count());
            var upgradedSession = await db.AuthSessions.AsNoTracking()
                .SingleAsync(x => x.Id == session.SessionId);
            Assert.Equal(beforeSession.UserId, upgradedSession.UserId);
            Assert.Equal(beforeSession.SelectedOrganizationId, upgradedSession.SelectedOrganizationId);
            Assert.Equal(beforeSession.SecurityVersion, upgradedSession.SecurityVersion);
            Assert.Null(upgradedSession.RevokedAtUtc);
            Assert.Equal("HIST-PORT", (await db.Ports.AsNoTracking().SingleAsync()).ReferenceCode);
            Assert.Equal("HIST-VESSEL", (await db.Vessels.AsNoTracking().SingleAsync()).ReferenceCode);
            Assert.Empty(await db.Voyages.ToListAsync());
            Assert.Empty(await db.Containers.ToListAsync());
            await db.Database.MigrateAsync("20261101010000_VoyageContainerReferences");
            Assert.Equal(5, (await db.Database.GetAppliedMigrationsAsync()).Count());
            await db.Database.MigrateAsync("20261031110000_PortVesselReferences");
            Assert.Equal(4, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(port.Id, (await db.Ports.SingleAsync()).Id);
            Assert.Equal(vessel.Id, (await db.Vessels.SingleAsync()).Id);
            Assert.Equal(org.Id, (await db.AuthSessions.AsNoTracking()
                .SingleAsync(x => x.Id == session.SessionId)).SelectedOrganizationId);
            await db.Database.MigrateAsync("20261101010000_VoyageContainerReferences");
            Assert.Equal(5, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(1, await db.AuthUsers.CountAsync());
            Assert.Equal(1, await db.OrganizationMemberships.CountAsync());
            Assert.Equal(org.Id, (await db.AuthSessions.AsNoTracking()
                .SingleAsync(x => x.Id == session.SessionId)).SelectedOrganizationId);
            Assert.Equal(1, await db.AuthSessions.CountAsync(x => x.UserId == session.UserId));
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
        var portA = new PortReference {
            Id = Guid.NewGuid(), OrganizationId = a.Id, ReferenceCode = "SQL-PORT-A",
            Name = "Port A", CountryCode = "US", SourceKind = "Synthetic",
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var portA2 = new PortReference {
            Id = Guid.NewGuid(), OrganizationId = a.Id, ReferenceCode = "SQL-PORT-A2",
            Name = "Port A2", CountryCode = "US", SourceKind = "Synthetic",
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var portB = new PortReference {
            Id = Guid.NewGuid(), OrganizationId = b.Id, ReferenceCode = "SQL-PORT-B",
            Name = "Port B", CountryCode = "US", SourceKind = "Synthetic",
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var vesselA = new VesselReference {
            Id = Guid.NewGuid(), OrganizationId = a.Id, ReferenceCode = "SQL-VESSEL-A",
            Name = "Vessel A", SourceKind = "Synthetic",
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var vesselB = new VesselReference {
            Id = Guid.NewGuid(), OrganizationId = b.Id, ReferenceCode = "SQL-VESSEL-B",
            Name = "Vessel B", SourceKind = "Synthetic",
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.Ports.AddRange(portA, portA2, portB);
        db.Vessels.AddRange(vesselA, vesselB);
        await db.SaveChangesAsync();

        Task<int> InsertVoyage(string code, Guid vesselId, Guid fromId, Guid toId) =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO voyages (id,organization_id,reference_code,vessel_id,
                  origin_port_id,destination_port_id,voyage_status,record_status,
                  source_kind,version,created_at_utc,updated_at_utc)
                VALUES ({Guid.NewGuid()},{a.Id},{code},{vesselId},{fromId},{toId},
                  'Planned','Active','Synthetic',1,now(),now())
                """);

        // Positive control: the same insert succeeds when all three references belong to A.
        Assert.Equal(1, await InsertVoyage("SQL-VALID-ROUTE",
            vesselA.Id, portA.Id, portA2.Id));
        var before = await db.Voyages.CountAsync(x => x.OrganizationId == a.Id);
        Assert.Equal(1, before);

        // Each foreign parent is existing and valid in B; exactly one FK differs per case.
        foreach (var (code, vesselId, fromId, toId) in new[] {
            ("SQL-FOREIGN-VESSEL", vesselB.Id, portA.Id, portA2.Id),
            ("SQL-FOREIGN-ORIGIN", vesselA.Id, portB.Id, portA2.Id),
            ("SQL-FOREIGN-DEST", vesselA.Id, portA.Id, portB.Id)
        })
        {
            var denied = await Assert.ThrowsAsync<PostgresException>(() =>
                InsertVoyage(code, vesselId, fromId, toId));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, denied.SqlState);
            Assert.Equal(before, await db.Voyages.CountAsync(x => x.OrganizationId == a.Id));
            Assert.False(await db.Voyages.AnyAsync(x =>
                x.OrganizationId == a.Id && x.ReferenceCode == code));
        }
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
