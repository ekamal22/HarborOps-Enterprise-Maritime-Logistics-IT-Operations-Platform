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

    // Do not trust the username in a connection string: inspect the actual role.
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var db = CreateContext();
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var cmd = new NpgsqlCommand("""
                SELECT current_user='harborops_projector'
                  AND session_user='harborops_projector'
                  AND has_function_privilege(current_user,'m10_lock_projector_scope(uuid,uuid)','EXECUTE')
                  AND has_table_privilege(current_user,'tracking_processing_effects','SELECT')
                  AND has_table_privilege(current_user,'shipment_tracking_input_versions','SELECT')
                  AND has_table_privilege(current_user,'shipment_tracking_correction_heads','SELECT')
                  AND has_table_privilege(current_user,'shipment_tracking_timeline_entries','SELECT')
                  AND has_table_privilege(current_user,'shipment_tracking_projections','UPDATE')
                  AND has_table_privilege(current_user,'shipment_tracking_projections','INSERT')
                  AND has_table_privilege(current_user,'shipment_projection_applications','INSERT')
                  AND NOT has_table_privilege(current_user,'tracking_processing_effects','INSERT')
                  AND NOT has_table_privilege(current_user,'tracking_processing_effects','UPDATE')
                  AND NOT has_table_privilege(current_user,'tracking_processing_effects','DELETE')
                  AND NOT has_table_privilege(current_user,'tracking_accepted_events','INSERT')
                  AND NOT has_table_privilege(current_user,'tracking_accepted_events','UPDATE')
                  AND NOT has_table_privilege(current_user,'tracking_inbox_receipts','DELETE')
                  AND NOT has_table_privilege(current_user,'shipments','UPDATE')
                  AND NOT has_table_privilege(current_user,'organizations','UPDATE')
                """, (NpgsqlConnection)db.Database.GetDbConnection());
            if (await cmd.ExecuteScalarAsync(cancellationToken) is not true)
                throw new InvalidOperationException("M10 projector database identity is not restricted");
        }
        finally { await db.Database.CloseConnectionAsync(); }
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        Guid? cursorOrganization = null;
        Guid? cursorShipment = null;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<(Guid Organization, Guid Shipment)> dirty;
                await using (var db = CreateContext())
                    dirty = await new ShipmentTimelineStore(db, restrictedProjector: true)
                        .DirtyAsync(16, stop, cursorOrganization, cursorShipment);
                if (dirty.Count == 0)
                {
                    // At the end of the sorted set, begin a fresh round. A failing
                    // first shipment can never prevent later shipments from running.
                    cursorOrganization = null;
                    cursorShipment = null;
                }
                foreach (var (org, shipment) in dirty)
                {
                    stop.ThrowIfCancellationRequested();
                    cursorOrganization = org;
                    cursorShipment = shipment;
                    try
                    {
                        await using var db = CreateContext();
                        var result = await new ShipmentTimelineStore(db, restrictedProjector: true)
                            .RebuildAsync(org, shipment, stop);
                        if (result.Result != TrackingMutationResult.Applied)
                            logger.LogWarning("M10 projection sweep deferred ({Result})", result.Result);
                    }
                    catch (OperationCanceledException) when (stop.IsCancellationRequested) { throw; }
                    catch (Exception failure)
                    {
                        // Continue to the next scoped shipment even if one is corrupt.
                        logger.LogWarning("M10 projection item deferred ({Type})",
                            failure.GetType().Name);
                    }
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (Exception failure)
            {
                logger.LogWarning("M10 projection discovery deferred ({Type})",
                    failure.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stop); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
        }
    }
}
