using HarborOps.Domain;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class BaselinePersistenceTests
{
    [Fact]
    public async Task MigrationAndSeed_CanRunTwiceWithoutDuplicatingRecord()
    {
        var connectionString = Environment.GetEnvironmentVariable("HARBOROPS_TEST_DB")
            ?? throw new InvalidOperationException("HARBOROPS_TEST_DB must point to a disposable PostgreSQL database");

        var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new HarborOpsDbContext(options);
        await db.Database.MigrateAsync();
        await BootstrapSeeder.SeedAsync(db, CancellationToken.None);
        await BootstrapSeeder.SeedAsync(db, CancellationToken.None);
        var records = await db.BootstrapRecords.AsNoTracking()
            .Where(x => x.Key == BaselineMetadata.RecordKey).ToListAsync();

        Assert.Single(records);
        Assert.Equal(BaselineMetadata.DatasetVersion, records[0].DatasetVersion);
        Assert.Equal(TimeSpan.Zero, records[0].SeededAtUtc.Offset);
    }
}
