using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.Shipments;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

    // Refuse to start if the configured identity is unavailable or can mutate
    // immutable M8/M9 evidence. A configuration string alone is not proof of isolation.
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var db = CreateContext();
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var cmd = new NpgsqlCommand("""
                SELECT current_user='harborops_projector'
                  AND has_table_privilege(current_user,'tracking_processing_effects','SELECT')
                  AND has_table_privilege(current_user,'shipment_tracking_projections','UPDATE')
                  AND has_table_privilege(current_user,'shipment_projection_applications','INSERT')
                  AND NOT has_table_privilege(current_user,'tracking_processing_effects','INSERT')
                  AND NOT has_table_privilege(current_user,'tracking_processing_effects','UPDATE')
                  AND NOT has_table_privilege(current_user,'tracking_processing_effects','DELETE')
                  AND NOT has_table_privilege(current_user,'tracking_accepted_events','INSERT')
                  AND NOT has_table_privilege(current_user,'tracking_accepted_events','UPDATE')
                  AND NOT has_table_privilege(current_user,'tracking_inbox_receipts','DELETE')
                """, (NpgsqlConnection)db.Database.GetDbConnection());
            if (await cmd.ExecuteScalarAsync(cancellationToken) is not true)
                throw new InvalidOperationException("M10 projector database identity is not restricted");
        }
        finally { await db.Database.CloseConnectionAsync(); }
        await base.StartAsync(cancellationToken);
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
