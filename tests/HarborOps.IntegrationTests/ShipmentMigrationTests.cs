using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.References;
using HarborOps.Infrastructure.Maritime;
using HarborOps.Infrastructure.Shipments;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class ShipmentMigrationTests
{
    [Fact]
    public async Task SeededM4HistorySurvivesM5UpgradeRollbackAndReapply()
    {
        var source = TestDatabase.ConnectionString();
        var schema = "g5_" + Guid.NewGuid().ToString("N");
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
            await db.Database.MigrateAsync("20261101010000_VoyageContainerReferences");
            Assert.Equal(5, (await db.Database.GetAppliedMigrationsAsync()).Count());
            var identity = new IdentityStore(db, new PasswordHasher(), TimeProvider.System);
            var organizations = new OrganizationStore(db, TimeProvider.System);
            var nonce = Guid.NewGuid().ToString("N");
            var email = "g5-history-" + nonce + "@example.test";
            const string passphrase = "Synthetic-G5-migration-passphrase-123";
            await identity.ProvisionAsync(email, "Synthetic G5", passphrase, CancellationToken.None);
            var org = await organizations.BootstrapAsync("g5-hist-" + nonce[..10],
                "G5 Historical Tenant", email, CancellationToken.None);
            var session = (await identity.LoginAsync(email, passphrase, "g5-upgrade",
                CancellationToken.None))!;
            Assert.NotNull((await organizations.SelectAsync(session.UserId, session.SessionId,
                org.Id, CancellationToken.None)).Value);
            var now = DateTimeOffset.UtcNow;
            var portA = new PortReference {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ReferenceCode = "G5-PORT-A",
                Name = "Historical Origin", CountryCode = "US", CreatedAtUtc = now, UpdatedAtUtc = now
            };
            var portB = new PortReference {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ReferenceCode = "G5-PORT-B",
                Name = "Historical Destination", CountryCode = "US", CreatedAtUtc = now, UpdatedAtUtc = now
            };
            var container = new ContainerRecord {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ReferenceCode = "G5-BOX",
                IdentifierKind = "Synthetic", ContainerIdentifier = "SYN-BOX-G5UPGRADE",
                EquipmentType = "Dry20", SourceKind = "Synthetic",
                CreatedAtUtc = now, UpdatedAtUtc = now
            };
            var vessel = new VesselReference {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ReferenceCode = "G5-HIST-VESSEL",
                Name = "Preserved Vessel", SourceKind = "Synthetic",
                CreatedAtUtc = now, UpdatedAtUtc = now
            };
            var voyage = new VoyageRecord {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ReferenceCode = "G5-HIST-VOY",
                VesselId = vessel.Id, OriginPortId = portA.Id, DestinationPortId = portB.Id,
                SourceKind = "Synthetic", CreatedAtUtc = now, UpdatedAtUtc = now
            };
            var containerB = new ContainerRecord {
                Id = Guid.NewGuid(), OrganizationId = org.Id, ReferenceCode = "G5-BOX-B",
                IdentifierKind = "Synthetic", ContainerIdentifier = "SYN-BOX-G5UPGRADEB",
                EquipmentType = "Dry40", SourceKind = "Synthetic",
                CreatedAtUtc = now, UpdatedAtUtc = now
            };
            db.Ports.AddRange(portA, portB);
            db.Vessels.Add(vessel);
            db.Voyages.Add(voyage);
            db.Containers.AddRange(container,containerB);
            await db.SaveChangesAsync();
            var beforeApplied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            Assert.Equal(5,beforeApplied.Length);
            var before = await db.AuthSessions.AsNoTracking()
                .SingleAsync(s => s.Id == session.SessionId);
            await db.Database.MigrateAsync("20261101180000_ShipmentLifecycle");
            Assert.Equal(6, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Equal(org.Id, (await db.AuthSessions.AsNoTracking()
                .SingleAsync(s => s.Id == session.SessionId)).SelectedOrganizationId);
            Assert.Equal(before.SecurityVersion, (await db.AuthSessions.AsNoTracking()
                .SingleAsync(s => s.Id == session.SessionId)).SecurityVersion);
            Assert.Equal(2, await db.Containers.AsNoTracking().CountAsync());
            Assert.Equal(container.ContainerIdentifier,
                (await db.Containers.AsNoTracking().SingleAsync(x=>x.Id==container.Id)).ContainerIdentifier);
            Assert.Equal(vessel.Id, (await db.Vessels.AsNoTracking().SingleAsync()).Id);
            Assert.Equal(voyage.Id, (await db.Voyages.AsNoTracking().SingleAsync()).Id);
            Assert.Equal(vessel.Id, (await db.Voyages.AsNoTracking().SingleAsync()).VesselId);
            Assert.Equal(portA.Id, (await db.Voyages.AsNoTracking().SingleAsync()).OriginPortId);
            Assert.Equal(portB.Id, (await db.Voyages.AsNoTracking().SingleAsync()).DestinationPortId);
            Assert.Equal(beforeApplied, (await db.Database.GetAppliedMigrationsAsync()).Take(5));
            Assert.Empty(await db.Shipments.AsNoTracking().ToListAsync());
            var shipment = new ShipmentRecord {
                Id = Guid.NewGuid(), OrganizationId = org.Id,
                ReferenceCode = "G5-MIGRATION-ONE",
                OriginPortId = portA.Id, DestinationPortId = portB.Id,
                CreatedAtUtc = now, UpdatedAtUtc = now, ChangedByUserId = session.UserId
            };
            db.Shipments.Add(shipment);
            await db.SaveChangesAsync();
            Assert.Equal(1, await db.ShipmentStatusChanges.AsNoTracking()
                .CountAsync(s => s.ShipmentId == shipment.Id && s.ToStatus == "Draft"));
            await db.Database.MigrateAsync("20261101010000_VoyageContainerReferences");
            Assert.Equal(5, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(2, await db.Ports.AsNoTracking().CountAsync());
            Assert.Equal(2, await db.Containers.AsNoTracking().CountAsync());
            Assert.Equal(voyage.Id, (await db.Voyages.AsNoTracking().SingleAsync()).Id);
            Assert.Equal(vessel.Id, (await db.Vessels.AsNoTracking().SingleAsync()).Id);
            Assert.Equal(org.Id, (await db.AuthSessions.AsNoTracking()
                .SingleAsync(s => s.Id == session.SessionId)).SelectedOrganizationId);
            await db.Database.MigrateAsync("20261101180000_ShipmentLifecycle");
            Assert.Equal(6, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Empty(await db.Shipments.AsNoTracking().ToListAsync());
            Assert.Equal(2, await db.Ports.AsNoTracking().CountAsync());
            Assert.Equal(2, await db.Containers.AsNoTracking().CountAsync());
            Assert.Equal(voyage.Id, (await db.Voyages.AsNoTracking().SingleAsync()).Id);
            Assert.Equal(vessel.Id, (await db.Vessels.AsNoTracking().SingleAsync()).Id);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
