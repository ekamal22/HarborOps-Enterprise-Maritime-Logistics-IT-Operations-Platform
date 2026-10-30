using HarborOps.Application.Baseline;
using HarborOps.Domain;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HarborOps.Infrastructure.Baseline;

internal sealed class EfBaselineReadService(HarborOpsDbContext database) : IBaselineReadService
{
    public async Task<BaselineStatus?> ReadAsync(CancellationToken cancellationToken)
    {
        var record = await database.BootstrapRecords.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Key == BaselineMetadata.RecordKey, cancellationToken);
        return record is null
            ? null
            : new BaselineStatus("HarborOps", record.DatasetVersion, record.SeededAtUtc, "synthetic");
    }
}
