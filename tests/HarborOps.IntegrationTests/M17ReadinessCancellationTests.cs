using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;
using HarborOps.Api.Health;
using HarborOps.Api.Observability;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace HarborOps.IntegrationTests;

// A readiness check that the caller abandoned (client disconnect, host shutdown) learned nothing
// about PostgreSQL. It must not publish a "database unavailable" sample, while a check that really
// failed, or hit its own five second limit, must still publish exactly that.
public sealed class M17ReadinessCancellationTests
{
    private sealed record Sample(string Operation, string Outcome);

    private static DatabaseReadinessCheck CheckAgainst(int port) => new(new HarborOpsDbContext(
        new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(
            $"Host=127.0.0.1;Port={port};Database=harborops_unreachable;Username=u;Password=p;" +
            "SSL Mode=Disable;Pooling=false;Timeout=8;Command Timeout=8").Options));

    private static ConcurrentQueue<Sample> Listen(MeterListener listener)
    {
        var samples = new ConcurrentQueue<Sample>();
        listener.InstrumentPublished = (instrument, observer) =>
        {
            if (instrument.Meter.Name == M17Telemetry.MeterName)
                observer.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
        {
            if (instrument.Name != "harborops.db.operation.duration") return;
            var copy = tags.ToArray();
            string Tag(string key) => copy.FirstOrDefault(t => t.Key == key).Value?.ToString() ?? "";
            samples.Enqueue(new Sample(Tag("operation"), Tag("outcome")));
        });
        listener.Start();
        return samples;
    }

    private static int ClosedPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static HealthCheckContext Context(DatabaseReadinessCheck check) => new()
    {
        Registration = new HealthCheckRegistration("database", check, null, ["ready"])
    };

    [Fact]
    public async Task CallerAbandonedReadinessCheckDoesNotPublishDatabaseUnavailable()
    {
        // The server accepts the connection and never answers, so only the caller can end the check.
        var silent = new TcpListener(IPAddress.Loopback, 0);
        silent.Start();
        try
        {
            var check = CheckAgainst(((IPEndPoint)silent.LocalEndpoint).Port);
            using var listener = new MeterListener();
            var samples = Listen(listener);
            using var caller = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
            try
            {
                var result = await check.CheckHealthAsync(Context(check), caller.Token);
                Assert.NotEqual(HealthStatus.Healthy, result.Status);
            }
            catch (OperationCanceledException)
            {
                // Either ending is acceptable. Publishing a database sample is not.
            }
            Assert.True(caller.IsCancellationRequested, "the caller token must have ended the check");
            Assert.DoesNotContain(samples, sample => sample.Operation == "readiness");
        }
        finally { silent.Stop(); }
    }

    [Fact]
    public async Task AlreadyCancelledCallerPublishesNoReadinessSample()
    {
        var check = CheckAgainst(ClosedPort());
        using var listener = new MeterListener();
        var samples = Listen(listener);
        using var caller = new CancellationTokenSource();
        await caller.CancelAsync();
        try { await check.CheckHealthAsync(Context(check), caller.Token); }
        catch (OperationCanceledException) { }
        Assert.DoesNotContain(samples, sample => sample.Operation == "readiness");
    }

    [Fact]
    public async Task RefusedConnectionStillPublishesExactlyOneUnavailableSample()
    {
        // Negative control: the observation path is live and a real failure is still reported.
        var check = CheckAgainst(ClosedPort());
        using var listener = new MeterListener();
        var samples = Listen(listener);
        var result = await check.CheckHealthAsync(Context(check), CancellationToken.None);
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal([new Sample("readiness", "unavailable")], samples.ToArray());
        listener.RecordObservableInstruments();
    }

    [Fact]
    public async Task OwnFiveSecondLimitStillPublishesUnavailableWhenCallerWaits()
    {
        var silent = new TcpListener(IPAddress.Loopback, 0);
        silent.Start();
        try
        {
            var check = CheckAgainst(((IPEndPoint)silent.LocalEndpoint).Port);
            using var listener = new MeterListener();
            var samples = Listen(listener);
            var result = await check.CheckHealthAsync(Context(check), CancellationToken.None);
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Equal([new Sample("readiness", "unavailable")], samples.ToArray());
        }
        finally { silent.Stop(); }
    }
}
