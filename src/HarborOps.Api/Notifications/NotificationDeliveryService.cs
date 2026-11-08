using HarborOps.Infrastructure.Notifications;

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
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<NotificationStore>();
                var count = await store.DeliverPendingAsync(stoppingToken);
                await Task.Delay(count == 0
                    ? TimeSpan.FromSeconds(15) : TimeSpan.FromMilliseconds(300), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
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
