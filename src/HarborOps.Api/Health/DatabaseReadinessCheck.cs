using HarborOps.Domain;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HarborOps.Api.Health;

public sealed class DatabaseReadinessCheck(HarborOpsDbContext database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await database.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("Database unavailable");

            var seeded = await database.BootstrapRecords.AsNoTracking()
                .AnyAsync(record => record.Key == BaselineMetadata.RecordKey, cancellationToken);
            return seeded ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Baseline not seeded");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Dependency unavailable");
        }
    }
}
