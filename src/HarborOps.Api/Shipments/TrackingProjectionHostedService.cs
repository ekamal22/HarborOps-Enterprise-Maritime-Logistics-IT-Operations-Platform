using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.Shipments;
using Microsoft.EntityFrameworkCore;

namespace HarborOps.Api.Shipments;

// Disabled by default. Requires separate, least-privilege PostgreSQL credentials.
public sealed class TrackingProjectionHostedService(
    IConfiguration configuration, ILogger<TrackingProjectionHostedService> logger)
    : BackgroundService
{
    private HarborOpsDbContext CreateContext()
    {
        var connection = configuration.GetConnectionString("HarborOpsProjector")
            ?? throw new InvalidOperationException("Projector database configuration is missing");
        var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
            .UseNpgsql(connection).Options;
        return new HarborOpsDbContext(options);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<(Guid Organization, Guid Shipment)> dirty;
                await using (var db = CreateContext())
                    dirty = await new ShipmentTimelineStore(db).DirtyAsync(16, stoppingToken);
                foreach (var (org, shipment) in dirty)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    await using var db = CreateContext();
                    var result = await new ShipmentTimelineStore(db)
                        .RebuildAsync(org, shipment, stoppingToken);
                    if (result.Result != TrackingMutationResult.Applied)
                        logger.LogWarning("Projection rebuild deferred ({Result})", result.Result);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception failure)
            {
                logger.LogWarning("Projection sweep deferred ({Type})", failure.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
