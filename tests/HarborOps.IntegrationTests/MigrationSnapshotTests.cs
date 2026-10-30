using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class MigrationSnapshotTests
{
    [Fact]
    public void ModelSnapshot_MatchesCurrentEntityModel()
    {
        using var db = SnapshotOnlyContext();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Migrations_AreDiscoveredInOrder()
    {
        using var db = SnapshotOnlyContext();
        Assert.Equal(
            new[] { "20261030170000_InitialBootstrap" },
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
