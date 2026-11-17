using HarborOps.Domain;
using HarborOps.Api.Observability;
using System.Diagnostics;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace HarborOps.Api.Health;

public sealed class DatabaseReadinessCheck(DbContextOptions<HarborOpsDbContext> options) : IHealthCheck
{
    // The approved readiness bound (P08) is five seconds for the whole check, measured by the caller. Three layers
    // enforce it, from the gentlest to the one that cannot be bypassed:
    //  1. The provider's own connect and command ceilings are lowered below the bound (CapProviderLimits).
    //  2. A linked token cancels a running command at four seconds. Cancelling is not instant: the provider sends a
    //     cancel request and waits for it (measured: about one second against a slow server).
    //  3. A hard wall-clock limit of 4.5 seconds abandons the provider call itself. Measured on the real stack with a
    //     stopped PostgreSQL and ten concurrent probes, the first two layers alone left checks at 6.8 and 7.8 seconds,
    //     because name resolution and queued connection attempts are not covered by the provider's timeouts.
    // An abandoned provider call can only run on until its own three second ceiling, and its outcome is observed and
    // discarded, so nothing is left running beyond that and nothing is reported twice.
    // The probe owns its own short-lived DbContext. It must never share the request scope's context: abandoning a call
    // leaves the connection in the Connecting state, and disposing a context in that state throws, which a shared
    // scoped context would turn into an HTTP 500 for the readiness endpoint (reproduced on the real stack).
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan HardLimit = TimeSpan.FromSeconds(4.5);

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
        Task<(HealthCheckResult Result, bool Healthy)>? probe = null;
        try
        {
            probe = ProbeAsync(timeout.Token);
            var outcome = await probe.WaitAsync(HardLimit, cancellationToken);
            healthy = outcome.Healthy;
            return outcome.Result;
        }
        catch (TimeoutException)
        {
            // The provider did not answer inside the limit. Stop what can be stopped; the abandoned call ends on
            // its own provider ceiling.
            timeout.Cancel();
            Observe(probe);
            return HealthCheckResult.Unhealthy("Dependency unavailable");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller gave up: stop the provider call where it honors cancellation and let it end on its own.
            timeout.Cancel();
            Observe(probe);
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
            // The check's own limit is not a caller cancellation and is still reported.
            if (healthy || !cancellationToken.IsCancellationRequested)
                M17Telemetry.ObserveDatabaseReadiness(healthy, stopwatch.Elapsed);
        }
    }

    // An abandoned call's late failure is observed here so it can never surface as an unobserved task exception.
    private static void Observe(Task? abandoned)
    {
        if (abandoned is null) return;
        _ = abandoned.ContinueWith(static task => _ = task.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private async Task<(HealthCheckResult Result, bool Healthy)> ProbeAsync(CancellationToken token)
    {
        try
        {
            await using var database = new HarborOpsDbContext(options);
            var capped = CappedConnectionString(database.Database.GetConnectionString());
            if (capped is not null) database.Database.SetConnectionString(capped);
            if (!await database.Database.CanConnectAsync(token))
                return (HealthCheckResult.Unhealthy("Database unavailable"), false);

            var seeded = await database.BootstrapRecords.AsNoTracking()
                .AnyAsync(record => record.Key == BaselineMetadata.RecordKey, token);
            return (seeded ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Baseline not seeded"), seeded);
        }
        catch (Exception)
        {
            return (HealthCheckResult.Unhealthy("Dependency unavailable"), false);
        }
    }

    // The connection string with the provider ceilings lowered below the readiness bound (never raised). Pure, so the
    // arithmetic is tested directly.
    public static string? CappedConnectionString(string? current)
    {
        if (string.IsNullOrEmpty(current)) return null;
        var builder = new NpgsqlConnectionStringBuilder(current);
        // Zero means "no limit" to Npgsql, so it is replaced as well as any value above the ceiling.
        builder.Timeout = builder.Timeout is > 0 and <= ProviderConnectSeconds ? builder.Timeout : ProviderConnectSeconds;
        builder.CommandTimeout = builder.CommandTimeout is > 0 and <= ProviderCommandSeconds
            ? builder.CommandTimeout : ProviderCommandSeconds;
        builder.CancellationTimeout = builder.CancellationTimeout is >= 0 and <= ProviderCancellationMilliseconds
            ? builder.CancellationTimeout : ProviderCancellationMilliseconds;
        return builder.ConnectionString;
    }
}
