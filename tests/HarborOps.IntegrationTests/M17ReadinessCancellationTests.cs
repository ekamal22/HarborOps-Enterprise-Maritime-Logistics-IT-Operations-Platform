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

    private static DatabaseReadinessCheck CheckAgainst(int port, string providerTimeouts = "Timeout=8;Command Timeout=8") =>
        new(new HarborOpsDbContext(new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(
            $"Host=127.0.0.1;Port={port};Database=harborops_unreachable;Username=u;Password=p;" +
            $"SSL Mode=Disable;Pooling=false;{providerTimeouts}").Options));

    // The real, disposable integration database with the given provider timeouts.
    private static DatabaseReadinessCheck CheckAgainstDatabase(
        Npgsql.NpgsqlConnectionStringBuilder database, string providerTimeouts) =>
        new(new HarborOpsDbContext(new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(
            database.ConnectionString + ";" + providerTimeouts).Options));

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

    [Fact(Timeout = 30000)]
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

    [Fact(Timeout = 30000)]
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

    [Fact(Timeout = 30000)]
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

    // ---- Wall-clock enforcement of the five second readiness limit (P08) ----------------------------------
    // The provider timeouts below are deliberately far longer than the readiness limit, so only the check's
    // own enforcement can end the attempt on time. A provider timeout set near the limit would prove nothing.
    private const string LongProviderTimeouts = "Timeout=30;Command Timeout=30";

    // The approved readiness bound: the whole check, provider cancellation included, ends inside five seconds.
    private static readonly TimeSpan EnforcedBound = TimeSpan.FromSeconds(5);

    private static async Task<(HealthCheckResult Result, TimeSpan Elapsed)> TimedAsync(
        DatabaseReadinessCheck check, CancellationToken token)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var result = await check.CheckHealthAsync(Context(check), token);
        return (result, clock.Elapsed);
    }

    // The server accepts the TCP connection and never replies, which is how a wedged PostgreSQL or a
    // black-holed network path looks to the API. Every accepted socket is kept so a leaked connection shows.
    private sealed class SilentServer : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly ConcurrentQueue<Socket> accepted = new();
        private readonly CancellationTokenSource stop = new();
        private readonly Task loop;

        public SilentServer()
        {
            listener.Start();
            loop = Task.Run(async () =>
            {
                try { while (true) accepted.Enqueue(await listener.AcceptSocketAsync(stop.Token)); }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { }
                catch (SocketException) { }
            });
        }

        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        public int Accepted => accepted.Count;

        // True when the client has closed every connection it opened. The client's startup bytes are first drained,
        // then the server must see end of stream (a zero length read) or a reset.
        public async Task<bool> AllClientConnectionsClosedAsync(TimeSpan limit)
        {
            var until = DateTime.UtcNow + limit;
            var buffer = new byte[1024];
            while (DateTime.UtcNow < until)
            {
                var open = accepted.Count(socket =>
                {
                    try
                    {
                        socket.ReceiveTimeout = 100;
                        while (true) { if (socket.Receive(buffer) == 0) return false; }
                    }
                    catch (ObjectDisposedException) { return false; }
                    catch (SocketException error) { return error.SocketErrorCode == SocketError.TimedOut; }
                });
                if (open == 0) return true;
                await Task.Delay(100);
            }
            return false;
        }

        public void Dispose()
        {
            stop.Cancel();
            listener.Stop();
            foreach (var socket in accepted) socket.Dispose();
            try { loop.Wait(2000); } catch (AggregateException) { }
            stop.Dispose();
        }
    }

    [Fact(Timeout = 30000)]
    public async Task SilentServerIsEndedByTheFiveSecondLimitEvenWhenTheProviderWouldWaitThirtySeconds()
    {
        using var server = new SilentServer();
        var check = CheckAgainst(server.Port, LongProviderTimeouts);
        using var listener = new MeterListener();
        var samples = Listen(listener);

        var (result, elapsed) = await TimedAsync(check, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.True(elapsed < EnforcedBound, $"readiness took {elapsed.TotalSeconds:F2}s against a five second limit");
        Assert.True(elapsed > TimeSpan.FromSeconds(1), "the attempt must really wait on the silent server, not fail fast");
        Assert.Equal([new Sample("readiness", "unavailable")], samples.ToArray());
        Assert.True(server.Accepted >= 1, "the silent server must have been reached");
        Assert.True(await server.AllClientConnectionsClosedAsync(TimeSpan.FromSeconds(3)),
            "a connection opened by the timed out check was left open");
    }

    [Theory(Timeout = 30000)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(400)]
    [InlineData(2500)]
    public async Task CallerCancellationEndsASilentServerCheckPromptlyWithoutAnUnavailableSample(int milliseconds)
    {
        using var server = new SilentServer();
        var check = CheckAgainst(server.Port, LongProviderTimeouts);
        using var listener = new MeterListener();
        var samples = Listen(listener);
        using var caller = new CancellationTokenSource(TimeSpan.FromMilliseconds(milliseconds));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try { await check.CheckHealthAsync(Context(check), caller.Token); }
        catch (OperationCanceledException) { }
        clock.Stop();

        Assert.True(caller.IsCancellationRequested);
        // Npgsql does not honor the token while it waits on a silent server, so the effect is bounded by the capped
        // provider timeout (3 s), not by the token. Thirty seconds, the provider setting, would be a defect.
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(4.6),
            $"cancellation after {milliseconds} ms took {clock.Elapsed.TotalSeconds:F2}s to take effect");
        Assert.DoesNotContain(samples, sample => sample.Operation == "readiness");
        Assert.True(await server.AllClientConnectionsClosedAsync(TimeSpan.FromSeconds(3)),
            "a connection opened by the cancelled check was left open");
    }

    [Theory(Timeout = 30000)]
    [InlineData("Timeout=2;Command Timeout=3", 2, 3, 1.5, 3.5)]
    [InlineData("Timeout=0;Command Timeout=0", 3, 3, 2.5, 4.6)]
    [InlineData("Timeout=30;Command Timeout=30", 3, 3, 2.5, 4.6)]
    public async Task ProviderCeilingsAreLoweredButNeverRaised(
        string configured, int connectSeconds, int commandSeconds, double minimumSeconds, double maximumSeconds)
    {
        using var server = new SilentServer();
        var context = new HarborOpsDbContext(new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(
            $"Host=127.0.0.1;Port={server.Port};Database=harborops_unreachable;Username=u;Password=p;" +
            $"SSL Mode=Disable;Pooling=false;{configured}").Options);
        var check = new DatabaseReadinessCheck(context);
        var (result, elapsed) = await TimedAsync(check, CancellationToken.None);
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.InRange(elapsed.TotalSeconds, minimumSeconds, maximumSeconds);
        var applied = new Npgsql.NpgsqlConnectionStringBuilder(context.Database.GetConnectionString());
        Assert.Equal(connectSeconds, applied.Timeout);
        Assert.Equal(commandSeconds, applied.CommandTimeout);
        Assert.InRange(applied.CancellationTimeout, 0, 300);
    }

    [Fact(Timeout = 30000)]
    public async Task ReadinessNeverExceedsItsLimitWhenTheRealDatabaseBlocksTheBaselineQuery()
    {
        // A real PostgreSQL that is up but blocks the one table readiness reads: the connection succeeds, so only the
        // query phase can hang. Another connection holds an exclusive lock on that table.
        var baseline = new Npgsql.NpgsqlConnectionStringBuilder(TestDatabase.ConnectionString()) { Pooling = false };
        await using (var prepare = new HarborOpsDbContext(
            new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(baseline.ConnectionString).Options))
        {
            await prepare.Database.MigrateAsync();
            await BootstrapSeeder.SeedAsync(prepare, CancellationToken.None);
        }

        using var listener = new MeterListener();
        var samples = Listen(listener);
        var control = await TimedAsync(CheckAgainstDatabase(baseline, LongProviderTimeouts), CancellationToken.None);
        Assert.Equal(HealthStatus.Healthy, control.Result.Status);
        Assert.Equal([new Sample("readiness", "healthy")], samples.ToArray());
        while (samples.TryDequeue(out _)) { }

        await using var locker = new Npgsql.NpgsqlConnection(baseline.ConnectionString);
        await locker.OpenAsync();
        await using (var begin = new Npgsql.NpgsqlCommand(
            "BEGIN; LOCK TABLE bootstrap_records IN ACCESS EXCLUSIVE MODE;", locker))
            await begin.ExecuteNonQueryAsync();
        try
        {
            var blocked = await TimedAsync(CheckAgainstDatabase(baseline, LongProviderTimeouts), CancellationToken.None);
            Assert.Equal(HealthStatus.Unhealthy, blocked.Result.Status);
            Assert.True(blocked.Elapsed < EnforcedBound,
                $"readiness took {blocked.Elapsed.TotalSeconds:F2}s while the baseline table was locked");
            Assert.True(blocked.Elapsed > TimeSpan.FromSeconds(1), "the blocked query must really have waited");
            Assert.Equal([new Sample("readiness", "unavailable")], samples.ToArray());
        }
        finally
        {
            await using var rollback = new Npgsql.NpgsqlCommand("ROLLBACK;", locker);
            await rollback.ExecuteNonQueryAsync();
        }

        // Recovery: once the lock is released the same check reports healthy again.
        while (samples.TryDequeue(out _)) { }
        var recovered = await TimedAsync(CheckAgainstDatabase(baseline, LongProviderTimeouts), CancellationToken.None);
        Assert.Equal(HealthStatus.Healthy, recovered.Result.Status);
        Assert.Equal([new Sample("readiness", "healthy")], samples.ToArray());
    }

    // Forwards to the real PostgreSQL after a fixed delay, so a connection can be slow without being refused.
    private sealed class DelayingProxy : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private readonly Task loop;

        public DelayingProxy(int targetPort, TimeSpan delay)
        {
            listener.Start();
            loop = Task.Run(async () =>
            {
                try
                {
                    while (!stop.IsCancellationRequested)
                    {
                        var client = await listener.AcceptTcpClientAsync(stop.Token);
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using (client)
                                {
                                    await Task.Delay(delay, stop.Token);
                                    using var upstream = new TcpClient();
                                    await upstream.ConnectAsync(IPAddress.Loopback, targetPort, stop.Token);
                                    var toServer = client.GetStream().CopyToAsync(upstream.GetStream(), stop.Token);
                                    var toClient = upstream.GetStream().CopyToAsync(client.GetStream(), stop.Token);
                                    await Task.WhenAny(toServer, toClient);
                                }
                            }
                            catch (Exception) { /* a closed or cancelled relay ends quietly */ }
                        });
                    }
                }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { }
                catch (SocketException) { }
            });
        }

        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

        public void Dispose()
        {
            stop.Cancel();
            listener.Stop();
            try { loop.Wait(2000); } catch (AggregateException) { }
            stop.Dispose();
        }
    }

    [Fact(Timeout = 40000)]
    public async Task TheFiveSecondLimitSpansTheSlowConnectAndTheBlockedQueryTogether()
    {
        // Two seconds to connect (inside the capped connect time), then a blocked baseline query that the capped
        // command time alone would hold for three more seconds. Only the check's own five second limit ends it on time.
        var direct = new Npgsql.NpgsqlConnectionStringBuilder(TestDatabase.ConnectionString()) { Pooling = false };
        await using (var prepare = new HarborOpsDbContext(
            new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(direct.ConnectionString).Options))
        {
            await prepare.Database.MigrateAsync();
            await BootstrapSeeder.SeedAsync(prepare, CancellationToken.None);
        }
        using var proxy = new DelayingProxy(direct.Port, TimeSpan.FromSeconds(2));
        var viaProxy = new Npgsql.NpgsqlConnectionStringBuilder(direct.ConnectionString) { Port = proxy.Port };

        await using var locker = new Npgsql.NpgsqlConnection(direct.ConnectionString);
        await locker.OpenAsync();
        await using (var begin = new Npgsql.NpgsqlCommand("BEGIN; LOCK TABLE bootstrap_records IN ACCESS EXCLUSIVE MODE;", locker))
            await begin.ExecuteNonQueryAsync();
        try
        {
            using var listener = new MeterListener();
            var samples = Listen(listener);
            var outcome = await TimedAsync(CheckAgainstDatabase(viaProxy, LongProviderTimeouts), CancellationToken.None);
            Assert.Equal(HealthStatus.Unhealthy, outcome.Result.Status);
            Assert.True(outcome.Elapsed > TimeSpan.FromSeconds(3.5), $"the slow connect plus the blocked query took only {outcome.Elapsed.TotalSeconds:F2}s");
            Assert.True(outcome.Elapsed < TimeSpan.FromSeconds(4.9), $"readiness took {outcome.Elapsed.TotalSeconds:F2}s against a five second limit");
            Assert.Equal([new Sample("readiness", "unavailable")], samples.ToArray());
        }
        finally
        {
            await using var rollback = new Npgsql.NpgsqlCommand("ROLLBACK;", locker);
            await rollback.ExecuteNonQueryAsync();
        }
    }
}
