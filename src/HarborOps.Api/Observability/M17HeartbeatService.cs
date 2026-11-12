using System.Net.Sockets;

namespace HarborOps.Api.Observability;

// A lightweight, bounded transport reachability signal. This is NOT proof
// that a trace or metric batch reached the collector. SDK counters report
// actual full-queue losses separately.
public sealed class M17HeartbeatService(M17TelemetryOptions options) : BackgroundService
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
        M17Telemetry.Heartbeat();
        try
        {
            using var socket = new TcpClient();
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(TimeSpan.FromMilliseconds(750));
            await socket.ConnectAsync(options.Endpoint.Host, options.Endpoint.Port, limit.Token);
            M17Telemetry.SetCollectorReachable(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Graceful stop: no failure classification.
        }
        catch (Exception)
        {
            M17Telemetry.SetCollectorReachable(false);
        }
    }
}
