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
        var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
            .UseNpgsql(TestDatabase.ConnectionString())
            .Options;

        await using var db = new HarborOpsDbContext(options);
        await db.Database.MigrateAsync();
        await BootstrapSeeder.SeedAsync(db, CancellationToken.None);

        var first = await db.BootstrapRecords.AsNoTracking()
            .SingleAsync(x => x.Key == BaselineMetadata.RecordKey);

        await BootstrapSeeder.SeedAsync(db, CancellationToken.None);
        await db.Database.MigrateAsync();

        var records = await db.BootstrapRecords.AsNoTracking()
            .Where(x => x.Key == BaselineMetadata.RecordKey)
            .ToListAsync();

        var record = Assert.Single(records);
        Assert.Equal(BaselineMetadata.DatasetVersion, record.DatasetVersion);
        Assert.Equal(TimeSpan.Zero, record.SeededAtUtc.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero), record.SeededAtUtc);
        Assert.Equal(first.SeededAtUtc, record.SeededAtUtc);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }
}
