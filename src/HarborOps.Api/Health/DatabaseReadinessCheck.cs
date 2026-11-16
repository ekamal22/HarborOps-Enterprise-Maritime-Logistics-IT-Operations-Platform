using HarborOps.Domain;
using HarborOps.Api.Observability;
using System.Diagnostics;
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

        var stopwatch = Stopwatch.StartNew();
        var healthy = false;
        try
        {
            if (!await database.Database.CanConnectAsync(timeout.Token))
                return HealthCheckResult.Unhealthy("Database unavailable");

            var seeded = await database.BootstrapRecords.AsNoTracking()
                .AnyAsync(record => record.Key == BaselineMetadata.RecordKey, timeout.Token);
            healthy = seeded;
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
        finally
        {
            // A caller that abandoned the check (client disconnect, host shutdown) learned nothing
            // about PostgreSQL, so an interrupted check must not publish "database unavailable".
            // The check's own five second limit is not a caller cancellation and is still reported.
            if (healthy || !cancellationToken.IsCancellationRequested)
                M17Telemetry.ObserveDatabaseReadiness(healthy, stopwatch.Elapsed);
        }
    }
}
