using HarborOps.Infrastructure.Assignments;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Maritime;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.References;
using HarborOps.Infrastructure.Shipments;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class AssignmentMigrationTests
{
    [Fact]
    public async Task ExistingM5RowsSurviveM6UpgradeRollbackAndReapply()
    {
        var source = TestDatabase.ConnectionString();
        var schema = "g6_" + Guid.NewGuid().ToString("N");
        var builder = new NpgsqlConnectionStringBuilder(source) { SearchPath = schema + ",public" };
        await using var admin = new NpgsqlConnection(source);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin))
            await command.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(builder.ConnectionString).Options;
            await using var db = new HarborOpsDbContext(options);
            await db.Database.MigrateAsync("20261101180000_ShipmentLifecycle");
            Assert.Equal(6, (await db.Database.GetAppliedMigrationsAsync()).Count());

            var nonce = Guid.NewGuid().ToString("N");
            var email = "m6-upgrade-" + nonce + "@example.test";
            var identity = new IdentityStore(db, new PasswordHasher(), TimeProvider.System);
            var orgStore = new OrganizationStore(db, TimeProvider.System);
            const string password = "Synthetic-G6-migration-passphrase-123";
            await identity.ProvisionAsync(email, "Synthetic G6 Migration", password,
                CancellationToken.None);
            var organization = await orgStore.BootstrapAsync("g6-hist-" + nonce[..10],
                "Historical M5 Tenant", email, CancellationToken.None);
            var session = (await identity.LoginAsync(email, password, "g6-upgrade",
                CancellationToken.None))!;
            Assert.NotNull((await orgStore.SelectAsync(session.UserId, session.SessionId,
                organization.Id, CancellationToken.None)).Value);
            var now = DateTimeOffset.UtcNow;
            var origin = new PortReference { Id = Guid.NewGuid(),
                OrganizationId = organization.Id, ReferenceCode = "G6-ORIGIN",
                Name = "Historical Origin", CountryCode = "US",
                CreatedAtUtc = now, UpdatedAtUtc = now };
            var destination = new PortReference { Id = Guid.NewGuid(),
                OrganizationId = organization.Id, ReferenceCode = "G6-DEST",
                Name = "Historical Destination", CountryCode = "US",
                CreatedAtUtc = now, UpdatedAtUtc = now };
            var vessel = new VesselReference { Id = Guid.NewGuid(),
                OrganizationId = organization.Id, ReferenceCode = "G6-VESSEL",
                Name = "Historical Vessel", CreatedAtUtc = now, UpdatedAtUtc = now };
            var container = new ContainerRecord { Id = Guid.NewGuid(),
                OrganizationId = organization.Id, ReferenceCode = "G6-CONT",
                ContainerIdentifier = "SYN-BOX-G6HISTORY", IdentifierKind = "Synthetic",
                EquipmentType = "Dry20", SourceKind = "Synthetic",
                CreatedAtUtc = now, UpdatedAtUtc = now };
            var voyage = new VoyageRecord { Id = Guid.NewGuid(),
                OrganizationId = organization.Id, ReferenceCode = "G6-VOYAGE",
                VesselId = vessel.Id, OriginPortId = origin.Id, DestinationPortId = destination.Id,
                CreatedAtUtc = now, UpdatedAtUtc = now };
            var shipment = new ShipmentRecord { Id = Guid.NewGuid(),
                OrganizationId = organization.Id, ReferenceCode = "G6-SHIP",
                OriginPortId = origin.Id, DestinationPortId = destination.Id,
                ChangedByUserId = session.UserId, CreatedAtUtc = now, UpdatedAtUtc = now };
            db.Ports.AddRange(origin, destination);
            db.Vessels.Add(vessel);
            db.Containers.Add(container);
            db.Voyages.Add(voyage);
            db.Shipments.Add(shipment);
            await db.SaveChangesAsync();
            Assert.Single(await db.ShipmentStatusChanges.AsNoTracking()
                .Where(x => x.ShipmentId == shipment.Id).ToListAsync());

            await db.Database.MigrateAsync("20261102080000_ShipmentAssignments");
            Assert.Equal(7, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Contains("20261103090000_IntegrationSources",
                await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Equal(shipment.Id, (await db.Shipments.AsNoTracking().SingleAsync()).Id);
            Assert.Equal(container.Id, (await db.Containers.AsNoTracking().SingleAsync()).Id);
            db.ShipmentContainerAssignments.Add(new ShipmentContainerAssignment {
                Id = Guid.NewGuid(), OrganizationId = organization.Id, ShipmentId = shipment.Id,
                ContainerId = container.Id, StartsAtUtc = now.AddMinutes(1),
                CreatedByUserId = session.UserId, CreatedAtUtc = now, UpdatedAtUtc = now
            });
            await db.SaveChangesAsync();
            Assert.Single(await db.ShipmentContainerAssignments.AsNoTracking().ToListAsync());

            // Only the disposable schema is downgraded, never a retained primary database.
            db.ChangeTracker.Clear();
            await db.Database.MigrateAsync("20261101180000_ShipmentLifecycle");
            Assert.Equal(6, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(shipment.Id, (await db.Shipments.AsNoTracking().SingleAsync()).Id);
            Assert.Equal(1, await db.ShipmentStatusChanges.AsNoTracking().CountAsync());
            await db.Database.MigrateAsync("20261102080000_ShipmentAssignments");
            Assert.Equal(7, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Empty(await db.ShipmentContainerAssignments.AsNoTracking().ToListAsync());
            Assert.Equal(voyage.Id, (await db.Voyages.AsNoTracking().SingleAsync()).Id);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
