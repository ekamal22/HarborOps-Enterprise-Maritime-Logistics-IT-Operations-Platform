using HarborOps.Domain;
using Microsoft.EntityFrameworkCore;

namespace HarborOps.Infrastructure.Persistence;

public static class BootstrapSeeder
{
    public static async Task SeedAsync(HarborOpsDbContext database, CancellationToken cancellationToken)
    {
        var exists = await database.BootstrapRecords
            .AnyAsync(record => record.Key == BaselineMetadata.RecordKey, cancellationToken);

        if (exists)
            return;

        database.BootstrapRecords.Add(new BootstrapRecord
        {
            Key = BaselineMetadata.RecordKey,
            DatasetVersion = BaselineMetadata.DatasetVersion,
            SeededAtUtc = new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero)
        });
        await database.SaveChangesAsync(cancellationToken);
    }
}
