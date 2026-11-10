using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HarborOps.Infrastructure.Reporting;

// Performs bounded maintenance independently of report traffic. This process
// never opens notification, replay, audit or source-history cleanup paths.
public sealed class OperationalKpiCleanupService(
    IServiceScopeFactory scopes, ILogger<OperationalKpiCleanupService> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
    private const int BatchSize = 16;
    private const int MaximumBatchesPerPoll = 8;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Wait before the first pass. Starting a TestServer or new API
            // instance must not immediately prune a fixture prepared by a
            // separate transaction. Subsequent polls remain bounded.
            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            { break; }
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                // Do not include database error messages or connection secrets.
                logger.LogWarning("M13 snapshot cleanup failed ({FailureType})",
                    error.GetType().Name);
            }

        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await database.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (NpgsqlConnection)database.Database.GetDbConnection();
            var removed = 0;
            var saturated = false;
            for (var attempt = 0; attempt < MaximumBatchesPerPoll; attempt++)
            {
                await using var command = new NpgsqlCommand(
                    "SELECT m13_prune_expired_idle(@limit)", connection)
                    { CommandTimeout = 15 };
                command.Parameters.AddWithValue("limit", BatchSize);
                var count = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
                removed += count;
                saturated = count == BatchSize;
                if (!saturated) break;
            }
            if (saturated)
                logger.LogWarning("M13 expired snapshot backlog exceeds one bounded sweep");
            if (removed > 0)
                logger.LogInformation("M13 expired reporting snapshots removed: {Count}",
                    removed);
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }
    }
}
