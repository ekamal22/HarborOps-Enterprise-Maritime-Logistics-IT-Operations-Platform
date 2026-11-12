using Microsoft.Extensions.Hosting;

namespace HarborOps.Api.Observability;

public sealed class M17HeartbeatService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        M17Telemetry.Heartbeat();
        while (await timer.WaitForNextTickAsync(stoppingToken))
            M17Telemetry.Heartbeat();
    }
}
