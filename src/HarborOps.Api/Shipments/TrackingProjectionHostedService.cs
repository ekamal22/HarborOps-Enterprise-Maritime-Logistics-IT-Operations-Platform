using HarborOps.Infrastructure.Shipments;

namespace HarborOps.Api.Shipments;

// Disabled by default. Enable only after an explicit operational decision.
public sealed class TrackingProjectionHostedService(
    IServiceScopeFactory scopes, ILogger<TrackingProjectionHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<(Guid Organization, Guid Shipment)> dirty;
                await using (var scope = scopes.CreateAsyncScope())
                    dirty = await scope.ServiceProvider.GetRequiredService<ShipmentTimelineStore>()
                        .DirtyAsync(16, stop);
                foreach (var (org, shipment) in dirty)
                {
                    stop.ThrowIfCancellationRequested();
                    await using var scope = scopes.CreateAsyncScope();
                    var result = await scope.ServiceProvider.GetRequiredService<ShipmentTimelineStore>()
                        .RebuildAsync(org, shipment, stop);
                    if (result.Result != TrackingMutationResult.Applied)
                        logger.LogWarning("Projection rebuild deferred ({Result})", result.Result);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (Exception failure)
            {
                logger.LogWarning("Projection sweep deferred ({Type})", failure.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stop); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
        }
    }
}
