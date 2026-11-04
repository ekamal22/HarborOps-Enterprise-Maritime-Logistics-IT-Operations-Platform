using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Integrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace HarborOps.Worker;

public sealed class WorkerShell(
    IServiceScopeFactory scopes, TrackingWorkerSettings settings, ILogger<WorkerShell> logger)
    : BackgroundService
{
    private Guid? cursor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Tracking work polling started");
        while (!stoppingToken.IsCancellationRequested)
        {
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
        logger.LogInformation("Tracking work polling stopped");
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

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<TrackingWorkStore>();
            var result = await store.FinalizeAsync(claim, ct);
            logger.LogInformation("Tracking worker disposition {Disposition}", result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception error)
        {
            var code = error is PostgresException pg && TrackingWorkPolicy.IsRetryableSqlState(pg.SqlState)
                ? "transient_database" : error is TimeoutException or NpgsqlException
                    ? "transient_dependency" : "processor_error";
            logger.LogWarning("Tracking worker failure ({Type}, {Code})",
                error.GetType().Name, code);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<TrackingWorkStore>();
                await store.FailAsync(claim, code, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception failure)
            {
                // The lease remains durably reclaimable after a database outage.
                logger.LogWarning("Tracking failure disposition deferred ({Type})",
                    failure.GetType().Name);
            }
        }
    }
}
