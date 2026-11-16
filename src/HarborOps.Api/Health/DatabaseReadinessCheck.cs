using HarborOps.Domain;
using HarborOps.Api.Observability;
using System.Diagnostics;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace HarborOps.Api.Health;

public sealed class DatabaseReadinessCheck(HarborOpsDbContext database) : IHealthCheck
{
    // The approved readiness bound (P08) is five seconds for the whole check. Cancelling a running command is not
    // instant: the provider sends a cancel request and waits for it (measured: about one second against a slow server),
    // so the check's own token fires at four seconds and the complete check, cancellation included, ends inside five.
    // The linked token enforces it for every phase in which the provider honors cancellation (a blocked query).
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(4);

    // Npgsql does not honor a cancellation token while it waits for a server that accepted the connection and never
    // answers: measured against a silent server, a token cancelled after 50 ms and the five second limit both left the
    // check waiting for the full provider timeout (30 seconds for a 30 second setting). The provider's own ceilings
    // are therefore lowered below the readiness limit for this check only, so the provider itself ends a stalled
    // connect or command. The application connection string, pool and other contexts are not touched.
    private const int ProviderConnectSeconds = 3;
    private const int ProviderCommandSeconds = 3;
    private const int ProviderCancellationMilliseconds = 300;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CheckTimeout);

        var stopwatch = Stopwatch.StartNew();
        var healthy = false;
        try
        {
            CapProviderLimits();
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

    private void CapProviderLimits()
    {
        var current = database.Database.GetConnectionString();
        if (string.IsNullOrEmpty(current)) return;
        var builder = new NpgsqlConnectionStringBuilder(current);
        // Zero means "no limit" to Npgsql, so it is replaced as well as any value above the ceiling.
        builder.Timeout = builder.Timeout is > 0 and <= ProviderConnectSeconds ? builder.Timeout : ProviderConnectSeconds;
        builder.CommandTimeout = builder.CommandTimeout is > 0 and <= ProviderCommandSeconds
            ? builder.CommandTimeout : ProviderCommandSeconds;
        builder.CancellationTimeout = builder.CancellationTimeout is >= 0 and <= ProviderCancellationMilliseconds
            ? builder.CancellationTimeout : ProviderCancellationMilliseconds;
        database.Database.SetConnectionString(builder.ConnectionString);
    }
}
