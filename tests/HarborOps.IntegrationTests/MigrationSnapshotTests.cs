using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class MigrationSnapshotTests
{
    /// <summary>
    /// Fails when the entity model has changed without a matching migration. Needs no
    /// database connection, so it runs even when PostgreSQL is not available.
    /// </summary>
    [Fact]
    public void ModelSnapshot_MatchesCurrentEntityModel()
    {
        var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=snapshot_only;Username=snapshot;Password=snapshot")
            .Options;

        using var db = new HarborOpsDbContext(options);
        var services = ((IInfrastructure<IServiceProvider>)db).Instance;

        var snapshot = services.GetRequiredService<IMigrationsAssembly>().ModelSnapshot;
        Assert.NotNull(snapshot);

        var snapshotModel = services.GetRequiredService<IModelRuntimeInitializer>()
            .Initialize(snapshot!.Model);
        var currentModel = services.GetRequiredService<IDesignTimeModel>().Model;
        var differ = services.GetRequiredService<IMigrationsModelDiffer>();

        var differences = differ.GetDifferences(
            snapshotModel.GetRelationalModel(), currentModel.GetRelationalModel());

        Assert.Empty(differences);
    }

    [Fact]
    public void Migrations_AreDiscoveredInOrder()
    {
        var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=snapshot_only;Username=snapshot;Password=snapshot")
            .Options;

        using var db = new HarborOpsDbContext(options);
        var migrations = ((IInfrastructure<IServiceProvider>)db).Instance
            .GetRequiredService<IMigrationsAssembly>().Migrations.Keys.ToArray();

        Assert.Equal(new[] { "20261030170000_InitialBootstrap" }, migrations);
    }
}
