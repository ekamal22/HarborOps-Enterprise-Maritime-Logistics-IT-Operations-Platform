using HarborOps.Infrastructure.Exceptions;

namespace HarborOps.Api.Exceptions;

// Optional local evaluator. It is separate from the M9 tracking worker and
// never changes work-item disposition or the transport backlog hold.
public sealed class ShipmentExceptionEvaluationService(
    IServiceScopeFactory scopes, ILogger<ShipmentExceptionEvaluationService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Guid? afterOrg = null, afterShipment = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<ShipmentExceptionStore>();
                var batch = await store.DueAsync(afterOrg, afterShipment, 25, stoppingToken);
                if (batch.Count == 0)
                {
                    afterOrg = null;
                    afterShipment = null;
                    await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                    continue;
                }
                foreach (var (org, shipment) in batch)
                {
                    var result = await store.EvaluateAsync(org, shipment, stoppingToken);
                    if (result.Result is not (M11StoreResult.Applied or M11StoreResult.NotFound))
                        logger.LogWarning("Exception evaluation returned {Result}", result.Result);
                }
                var last = batch[^1];
                afterOrg = last.Organization;
                afterShipment = last.Shipment;
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogWarning("Exception evaluation unavailable ({ErrorType})",
                    error.GetType().Name);
                try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
