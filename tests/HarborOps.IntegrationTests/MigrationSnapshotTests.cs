using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class MigrationSnapshotTests
{
    [Fact]
    public void ModelSnapshot_MatchesCurrentEntityModel()
    {
        using var db = SnapshotOnlyContext();
        var snapshot = db.GetService<IModelRuntimeInitializer>()
            .Initialize(db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
        var current = db.GetService<IDesignTimeModel>().Model;
        var changes = db.GetService<IMigrationsModelDiffer>()
            .GetDifferences(snapshot.GetRelationalModel(), current.GetRelationalModel());
        var details = string.Join("; ", changes.Select(change =>
            change.GetType().Name + " " +
            change.GetType().GetProperty("Table")?.GetValue(change) + "." +
            change.GetType().GetProperty("Name")?.GetValue(change)));
        Assert.False(db.Database.HasPendingModelChanges(), details);
    }

    [Fact]
    public void Migrations_AreDiscoveredInOrder()
    {
        using var db = SnapshotOnlyContext();
        Assert.Equal(
            new[] { "20261030170000_InitialBootstrap", "20261031010000_IdentitySessions", "20261031020000_OrganizationMemberships", "20261031110000_PortVesselReferences", "20261101010000_VoyageContainerReferences", "20261101180000_ShipmentLifecycle", "20261102080000_ShipmentAssignments", "20261103090000_IntegrationSources", "20261103190000_TrackingInboxAndHistory", "20261104190000_TrackingWorker" },
            db.Database.GetMigrations().ToArray());
    }

    private static HarborOpsDbContext SnapshotOnlyContext()
    {
        var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=snapshot_only;Username=snapshot;Password=snapshot")
            .Options;
        return new HarborOpsDbContext(options);
    }
}
