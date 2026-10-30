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
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
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
            new[] { "20261030170000_InitialBootstrap", "20261031010000_IdentitySessions" },
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
