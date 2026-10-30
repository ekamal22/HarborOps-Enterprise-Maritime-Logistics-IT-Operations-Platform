using HarborOps.Domain;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class BaselinePersistenceTests
{
    [Fact]
    public async Task MigrationAndSeed_CanRunTwiceWithoutDuplicatingRecord()
    {
        var rawConnectionString = Environment.GetEnvironmentVariable("HARBOROPS_TEST_DB")
            ?? throw new InvalidOperationException(
                "HARBOROPS_TEST_DB must point to a disposable local harborops integration database");

        // This test runs EF migrations. Fail closed before opening a connection to
        // avoid accidentally migrating a shared or production database.
        var connection = new NpgsqlConnectionStringBuilder(rawConnectionString);
        if (!string.Equals(connection.Database, "harborops_integration", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(connection.Database, "harborops_test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Integration tests require database harborops_integration or harborops_test");
        }

        var host = connection.Host?.Trim();
        if (host is not ("localhost" or "127.0.0.1" or "::1"))
        {
            throw new InvalidOperationException(
                "Integration tests require a local PostgreSQL host");
        }

        var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
            .UseNpgsql(connection.ConnectionString)
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
    }
}
