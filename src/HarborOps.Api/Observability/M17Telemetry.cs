using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HarborOps.Api.Observability;

// Every dimension is drawn from a finite allowlist. Never use request paths,
// tenant names, references, cookies, SQL, personal details or raw exceptions.
public static class M17Telemetry
{
    public const string MeterName = "HarborOps.Observability";
    public const string TraceSourceName = "HarborOps.Observability";
    public static readonly Meter Meter = new(MeterName, "1.0.0");
    public static readonly ActivitySource Traces = new(TraceSourceName, "1.0.0");

    private static readonly Counter<long> FailedRequests =
        Meter.CreateCounter<long>("harborops.request.failures");
    private static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>("harborops.request.duration", "s");
    private static readonly Histogram<double> DatabaseDuration =
        Meter.CreateHistogram<double>("harborops.db.operation.duration", "s");
    private static readonly Counter<long> DeliveryAttempts =
        Meter.CreateCounter<long>("harborops.worker.attempts");
    private static readonly Counter<long> SamplingDrops =
        Meter.CreateCounter<long>("harborops.telemetry.dropped");
    private static readonly Counter<long> Heartbeats =
        Meter.CreateCounter<long>("harborops.telemetry.heartbeat");
    private static readonly Counter<long> TransportFailures =
        Meter.CreateCounter<long>("harborops.telemetry.transport.failures");

    private static int databaseReadiness = -1;
    private static long databaseSampleUnixSeconds;
    private static int notificationExpected;
    private static int notificationRunning;
    private static int collectorReachable = -1;

    static M17Telemetry()
    {
        _ = Meter.CreateObservableGauge<int>("harborops.db.readiness",
            () => Volatile.Read(ref databaseReadiness), "1");
        _ = Meter.CreateObservableGauge<long>("harborops.db.readiness_sample_unix_seconds",
            () => Interlocked.Read(ref databaseSampleUnixSeconds));
        _ = Meter.CreateObservableGauge<int>("harborops.telemetry.collector_reachable",
            () => Volatile.Read(ref collectorReachable), "1");
        _ = Meter.CreateObservableGauge<int>("harborops.service.running",
            () => new Measurement<int>[]
            {
                new(Volatile.Read(ref notificationExpected) == 0 ? 0 :
                    Volatile.Read(ref notificationRunning) == 1 ? 1 : -1,
                    new KeyValuePair<string, object?>("service", "notifications")),
                new(-1, new KeyValuePair<string, object?>("service", "tracking_held"))
            }, "1");
    }

    public static void SetNotificationWorkerEnabled(bool enabled) =>
        Volatile.Write(ref notificationExpected, enabled ? 1 : 0);

    public static void SetNotificationWorkerRunning(bool running) =>
        Volatile.Write(ref notificationRunning, running ? 1 : 0);

    public static void SetCollectorReachable(bool reachable)
    {
        Volatile.Write(ref collectorReachable, reachable ? 1 : 0);
        if (!reachable) TransportFailures.Add(1);
    }

    public static string SafeRoute(HttpContext context)
    {
        if (context.GetEndpoint() is not RouteEndpoint endpoint)
            return "unmatched";
        var route = endpoint.RoutePattern.RawText;
        return string.IsNullOrWhiteSpace(route) || route.Length > 120 ||
            route.Any(char.IsControl) ? "other" : route;
    }

    private static string Operation(HttpContext context)
    {
        var route = SafeRoute(context);
        if (route.StartsWith("/api/v1/auth", StringComparison.Ordinal)) return "auth";
        if (route.StartsWith("/api/v1/organizations", StringComparison.Ordinal)) return "organization";
        if (route.StartsWith("/integrations/", StringComparison.Ordinal)) return "tracking";
        if (route.StartsWith("/health/", StringComparison.Ordinal)) return "health";
        if (route.StartsWith("/api/v1/", StringComparison.Ordinal)) return "business";
        return "other";
    }

    public static void ObserveRequest(HttpContext context, TimeSpan elapsed)
    {
        var code = context.Response.StatusCode;
        var statusClass = code is >= 100 and < 600 ? $"{code / 100}xx" : "other";
        var operation = Operation(context);
        RequestDuration.Record(Math.Max(0, elapsed.TotalSeconds),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status_class", statusClass));
        if (code is >= 500 and < 600)
            FailedRequests.Add(1,
                new KeyValuePair<string, object?>("operation", operation),
                new KeyValuePair<string, object?>("status_class", "5xx"));
    }

    public static void ObserveDatabaseReadiness(bool healthy, TimeSpan elapsed)
    {
        Volatile.Write(ref databaseReadiness, healthy ? 1 : 0);
        Interlocked.Exchange(ref databaseSampleUnixSeconds, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        DatabaseDuration.Record(Math.Max(0, elapsed.TotalSeconds),
            new KeyValuePair<string, object?>("operation", "readiness"),
            new KeyValuePair<string, object?>("outcome", healthy ? "healthy" : "unavailable"));
    }

    public static void ObserveBaselineRead(bool successful, TimeSpan elapsed)
    {
        DatabaseDuration.Record(Math.Max(0, elapsed.TotalSeconds),
            new KeyValuePair<string, object?>("operation", "baseline_read"),
            new KeyValuePair<string, object?>("outcome", successful ? "success" : "unavailable"));
    }

    public static void ObserveNotificationDelivery(int delivered)
    {
        if (delivered > 0)
            DeliveryAttempts.Add(delivered,
                new KeyValuePair<string, object?>("worker", "notifications"),
                new KeyValuePair<string, object?>("outcome", "success"));
    }

    public static void ObserveNotificationFailure() =>
        DeliveryAttempts.Add(1,
            new KeyValuePair<string, object?>("worker", "notifications"),
            new KeyValuePair<string, object?>("outcome", "failure"));

    public static void RecordExporterFailure(int spanCount)
    {
        if (spanCount > 0)
            SamplingDrops.Add(spanCount,
                new KeyValuePair<string, object?>("signal", "trace"),
                new KeyValuePair<string, object?>("reason", "exporter_failure"));
    }

    public static void RecordSamplingDrop() =>
        SamplingDrops.Add(1,
            new KeyValuePair<string, object?>("signal", "trace"),
            new KeyValuePair<string, object?>("reason", "rate_limit"));

    public static void Heartbeat()
    {
        SeedCounterSeries();
        Heartbeats.Add(1);
    }

    // Prometheus increase() needs a baseline sample. A counter that first appears
    // already holding its loss count would never raise the drop alert. Recording a
    // zero on every heartbeat creates the series after the exporter is subscribed
    // (a zero added during static initialization is lost before the listener exists).
    private static void SeedCounterSeries()
    {
        // A first burst of 5xx responses may complete between export scrapes.
        // Seed the bounded business/5xx series while the application is healthy
        // so the first incident has a zero baseline for Prometheus rate().
        FailedRequests.Add(0,
            new KeyValuePair<string, object?>("operation", "business"),
            new KeyValuePair<string, object?>("status_class", "5xx"));
        SamplingDrops.Add(0,
            new KeyValuePair<string, object?>("signal", "trace"),
            new KeyValuePair<string, object?>("reason", "exporter_failure"));
        SamplingDrops.Add(0,
            new KeyValuePair<string, object?>("signal", "trace"),
            new KeyValuePair<string, object?>("reason", "rate_limit"));
    }

}
