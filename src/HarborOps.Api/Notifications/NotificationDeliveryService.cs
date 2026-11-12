using HarborOps.Infrastructure.Notifications;
using HarborOps.Api.Observability;

namespace HarborOps.Api.Notifications;

// Opt-in and independent of M9's transport worker and M11's exception evaluator.
// Every batch is atomic in PostgreSQL and source-history/recipient uniqueness
// prevents duplicated persisted delivery after restart or concurrent dispatchers.
public sealed class NotificationDeliveryService(
    IServiceScopeFactory scopes, ILogger<NotificationDeliveryService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        long lastQuarantined = -1;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<NotificationStore>();
                var count = await store.DeliverPendingAsync(stoppingToken);
                M17Telemetry.ObserveNotificationDelivery(true);
                if (count == 0)
                {
                    var status = await store.DeliveryStatusAsync(stoppingToken);
                    if (status.Quarantined > 0 && status.Quarantined != lastQuarantined)
                        logger.LogWarning(
                            "Notification delivery has {Quarantined} quarantined intents and {Deferred} deferred retries",
                            status.Quarantined,status.Deferred);
                    lastQuarantined = status.Quarantined;
                }
                await Task.Delay(count == 0
                    ? TimeSpan.FromSeconds(15) : TimeSpan.FromMilliseconds(300), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                M17Telemetry.ObserveNotificationDelivery(false);
                logger.LogWarning("Notification delivery unavailable ({ErrorType})",
                    error.GetType().Name);
                try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
