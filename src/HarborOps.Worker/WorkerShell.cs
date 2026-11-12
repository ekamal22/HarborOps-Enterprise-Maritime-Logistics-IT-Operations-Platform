using HarborOps.Domain.Tracking;
using HarborOps.Worker.Observability;
using System.Diagnostics;
using HarborOps.Infrastructure.Integrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace HarborOps.Worker;

public sealed class WorkerShell(
    IServiceScopeFactory scopes, TrackingWorkerSettings settings, ILogger<WorkerShell> logger)
    : BackgroundService
{
    private Guid? cursor;
    private long lastBacklogSample;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (M17WorkerTelemetry.Enabled) M17WorkerTelemetry.SetWorkerRunning(true);
        logger.LogInformation("Tracking work polling started");
        try
        {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (M17WorkerTelemetry.Enabled &&
                (lastBacklogSample == 0 ||
                 Stopwatch.GetElapsedTime(lastBacklogSample) >= TimeSpan.FromSeconds(15)))
                await SampleBacklogAsync(stoppingToken);
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                // Never log exception messages or database connection strings.
                logger.LogWarning("Tracking work sweep failed ({Type})", error.GetType().Name);
            }
            try
            {
                await Task.Delay(settings.PollMilliseconds, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
        }
        finally
        {
            if (M17WorkerTelemetry.Enabled) M17WorkerTelemetry.SetWorkerRunning(false);
        }
        logger.LogInformation("Tracking work polling stopped");
    }

    private async Task SampleBacklogAsync(CancellationToken cancellationToken)
    {
        lastBacklogSample = Stopwatch.GetTimestamp();
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<TrackingWorkStore>();
            var age = await store.OldestEligibleBacklogAgeSecondsAsync(cancellationToken);
            M17WorkerTelemetry.SetBacklogAge(age ?? 0);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The worker is stopping, not a durable backlog failure.
        }
        catch (Exception)
        {
            M17WorkerTelemetry.SetBacklogAge(null);
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        List<Guid> organizations;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<TrackingWorkStore>();
            organizations = await store.CandidateOrganizationsAsync(cursor, ct);
            if (organizations.Count == 0 && cursor is not null)
            {
                cursor = null;
                organizations = await store.CandidateOrganizationsAsync(null, ct);
            }
        }
        if (organizations.Count == 0) return;
        organizations = organizations.Take(settings.BatchSize).ToList();
        cursor = organizations[^1];

        // Each task owns its own scoped context and a short transaction.
        foreach (var batch in organizations.Chunk(settings.Concurrency))
        {
            ct.ThrowIfCancellationRequested();
            await Task.WhenAll(batch.Select(organization => ProcessOneAsync(organization, ct)));
        }
    }

    private async Task ProcessOneAsync(Guid organizationId, CancellationToken ct)
    {
        TrackingWorkClaim? claim;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<TrackingWorkStore>();
            claim = await store.ClaimOneAsync(organizationId, ct);
        }
        if (claim is null) return;

        using var span = M17WorkerTelemetry.Traces.StartActivity("tracking.process");
        span?.SetTag("harborops.operation", "tracking.process");
        var startTimestamp = Stopwatch.GetTimestamp();
        var disposition = "other";
        using var renewalCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var heartbeat = MaintainLeaseAsync(claim, renewalCancellation.Token);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<TrackingWorkStore>();
            var result = await store.FinalizeAsync(claim, ct);
            disposition = result switch
            {
                TrackingWorkDisposition.Processed => "processed",
                TrackingWorkDisposition.NeedsReconciliation => "needs_reconciliation",
                TrackingWorkDisposition.Stale => "stale",
                _ => "other"
            };
            logger.LogInformation("Tracking worker disposition {Disposition}", result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { disposition = "cancelled"; }
        catch (PostgresException conflict) when (conflict.SqlState == PostgresErrorCodes.CheckViolation &&
            conflict.ConstraintName == "CK_m9_effect_reference")
        {
            disposition = "reference_conflict";
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<TrackingWorkStore>();
                await store.ParkReferenceConflictAsync(claim, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception failure)
            {
                logger.LogWarning("Reference reconciliation deferred ({Type})", failure.GetType().Name);
            }
        }
        catch (Exception error)
        {
            var retryable = error switch
            {
                PostgresException pg => TrackingWorkPolicy.IsRetryableSqlState(pg.SqlState),
                NpgsqlException => true,
                TimeoutException => true,
                _ => false
            };
            var code = retryable ? "transient_dependency" : "processor_error";
            disposition = retryable ? "retry" : "failure";
            logger.LogWarning("Tracking worker failure ({Type}, {Code})",
                error.GetType().Name, code);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<TrackingWorkStore>();
                await store.FailAsync(claim, code, retryable, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception failure)
            {
                // The lease remains durably reclaimable after a database outage.
                logger.LogWarning("Tracking failure disposition deferred ({Type})",
                    failure.GetType().Name);
            }
        }
        finally
        {
            span?.SetTag("harborops.outcome", disposition);
            M17WorkerTelemetry.ObserveAttempt(disposition, Stopwatch.GetElapsedTime(startTimestamp));
            renewalCancellation.Cancel();
            try { await heartbeat; }
            catch (OperationCanceledException) when (renewalCancellation.IsCancellationRequested) { }
            catch (Exception leaseError)
            {
                logger.LogWarning("Tracking lease renewal stopped ({Type})", leaseError.GetType().Name);
            }
        }
    }

    private async Task MaintainLeaseAsync(TrackingWorkClaim claim, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(settings.HeartbeatSeconds), ct);
            await using var scope = scopes.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<TrackingWorkStore>();
            if (!await store.HeartbeatAsync(claim, ct)) return;
        }
    }
}
