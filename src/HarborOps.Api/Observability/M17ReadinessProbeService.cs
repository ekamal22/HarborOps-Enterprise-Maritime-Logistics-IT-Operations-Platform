using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HarborOps.Api.Observability;

// Separate from /health/ready. Measurements do not alter endpoint contracts
// or make the collector a required dependency.
public sealed class M17ReadinessProbeService(HealthCheckService health) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProbeAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ProbeAsync(stoppingToken);
    }

    private async Task ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            // DatabaseReadinessCheck already records the result and duration.
            // Filtering by the ready tag excludes any optional monitoring service.
            await health.CheckHealthAsync(
                registration => registration.Tags.Contains("ready"), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Graceful stop does not mean database failure.
        }
        catch (Exception)
        {
            // Never emit provider exception text or credentials.
            M17Telemetry.ObserveDatabaseReadiness(false, TimeSpan.Zero);
        }
    }
}
