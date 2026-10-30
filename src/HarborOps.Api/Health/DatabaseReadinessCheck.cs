using HarborOps.Domain;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HarborOps.Api.Health;

public sealed class DatabaseReadinessCheck(HarborOpsDbContext database) : IHealthCheck
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CheckTimeout);

        try
        {
            if (!await database.Database.CanConnectAsync(timeout.Token))
                return HealthCheckResult.Unhealthy("Database unavailable");

            var seeded = await database.BootstrapRecords.AsNoTracking()
                .AnyAsync(record => record.Key == BaselineMetadata.RecordKey, timeout.Token);
            return seeded ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Baseline not seeded");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("Dependency unavailable");
        }
    }
}
