using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HarborOps.Api.Observability;

// All metric dimensions are finite source-controlled values. Never add tenant,
// user, shipment, session, URL, SQL, arbitrary exception or correlation labels.
public static class M17Telemetry
{
    public const string MeterName = "HarborOps.Observability";
    public const string TraceSourceName = "HarborOps.Observability";
    public static readonly Meter Meter = new(MeterName, "1.0.0");
    public static readonly ActivitySource Traces = new(TraceSourceName, "1.0.0");

    private static readonly Counter<long> FailedRequests =
        Meter.CreateCounter<long>("harborops.request.failures", "{requests}");
    private static readonly Histogram<double> DatabaseDuration =
        Meter.CreateHistogram<double>("harborops.db.operation.duration", "s");
    private static readonly Counter<long> WorkerAttempts =
        Meter.CreateCounter<long>("harborops.worker.attempts", "{attempts}");
    private static readonly Counter<long> Dropped =
        Meter.CreateCounter<long>("harborops.telemetry.dropped", "{records}");
    private static readonly Counter<long> Heartbeats =
        Meter.CreateCounter<long>("harborops.telemetry.heartbeat", "{heartbeats}");

    // -1 means unknown or intentionally held, 0 unhealthy/disabled, 1 healthy/active.
    private static int databaseReadiness = -1;
    private static int notificationWorkerActive;
    static M17Telemetry()
    {
        _ = Meter.CreateObservableGauge<int>("harborops.db.readiness",
            () => Volatile.Read(ref databaseReadiness), "1");
        _ = Meter.CreateObservableGauge<int>("harborops.service.running",
            () => new Measurement<int>[]
            {
                new(Volatile.Read(ref notificationWorkerActive),
                    new KeyValuePair<string, object?>("service", "notifications")),
                new(-1, new KeyValuePair<string, object?>("service", "tracking_held"))
            }, "1");
    }

    public static void SetNotificationWorkerEnabled(bool enabled) =>
        Volatile.Write(ref notificationWorkerActive, enabled ? 1 : 0);

    public static string SafeRoute(HttpContext context)
    {
        if (context.GetEndpoint() is not RouteEndpoint endpoint)
            return "unmatched";
        var route = endpoint.RoutePattern.RawText;
        if (string.IsNullOrWhiteSpace(route) || route.Length > 120 ||
            route.Any(char.IsControl))
            return "other";
        return route;
    }

    public static void ObserveRequest(HttpContext context)
    {
        var code = context.Response.StatusCode;
        if (code < 500) return;
        var family = code >= 600 ? "other" : "5xx";
        FailedRequests.Add(1,
            new KeyValuePair<string, object?>("route", SafeRoute(context)),
            new KeyValuePair<string, object?>("status_class", family));
    }

    public static void ObserveDatabaseReadiness(bool healthy, TimeSpan elapsed)
    {
        Volatile.Write(ref databaseReadiness, healthy ? 1 : 0);
        DatabaseDuration.Record(Math.Max(0, elapsed.TotalSeconds),
            new KeyValuePair<string, object?>("operation", "readiness"),
            new KeyValuePair<string, object?>("outcome", healthy ? "healthy" : "unavailable"));
    }

    public static void ObserveNotificationDelivery(bool successful)
    {
        WorkerAttempts.Add(1,
            new KeyValuePair<string, object?>("worker", "notifications"),
            new KeyValuePair<string, object?>("outcome", successful ? "success" : "failure"));
    }

    public static void RecordSamplingDrop() =>
        Dropped.Add(1, new KeyValuePair<string, object?>("signal", "trace"),
            new KeyValuePair<string, object?>("reason", "rate_limit"));

    public static void Heartbeat() => Heartbeats.Add(1);
}
