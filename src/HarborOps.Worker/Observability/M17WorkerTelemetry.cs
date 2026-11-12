using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.Extensions.Configuration;
using HarborOps.Infrastructure.Observability;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace HarborOps.Worker.Observability;

// This is intentionally separate from the API's service: the tracking worker
// remains an opt-in process, and it must never inherit API credentials.
public static class M17WorkerTelemetry
{
    public const string MeterName = "HarborOps.Worker.Observability";
    public const string SourceName = "HarborOps.Worker.Observability";
    public static readonly ActivitySource Traces = new(SourceName, "1.0.0");
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static int enabled;
    private static byte[]? linkKey;
    private static int running;
    private static double backlogAgeSeconds = -1;
    public static bool Enabled => Volatile.Read(ref enabled) == 1;

    public static string? LinkDigest(string? correlation) =>
        linkKey is { } key ? M17CorrelationDigest.FromServerCorrelation(correlation, key) : null;
    private static readonly Counter<long> SamplingDrops =
        Meter.CreateCounter<long>("harborops.telemetry.dropped", "{records}");
    private static readonly Counter<long> Attempts =
        Meter.CreateCounter<long>("harborops.worker.attempts", "{attempts}");
    private static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("harborops.worker.operation.duration", "s");

    static M17WorkerTelemetry()
    {
        // The tracking process itself exists only under the explicit worker profile.
        _ = Meter.CreateObservableGauge<int>("harborops.service.running",
            () => new Measurement<int>(Volatile.Read(ref running),
                new KeyValuePair<string, object?>("service", "tracking")), "1");
        _ = Meter.CreateObservableGauge<double>("harborops.worker.backlog.age",
            () => new Measurement<double>(Volatile.Read(ref backlogAgeSeconds),
                new KeyValuePair<string, object?>("worker", "tracking")), "s");
    }

    public static void SetWorkerRunning(bool active) =>
        Volatile.Write(ref running, active ? 1 : 0);

    // Negative means sample unavailable. Zero means no eligible work.
    public static void SetBacklogAge(double? seconds) =>
        Volatile.Write(ref backlogAgeSeconds, seconds.HasValue ? Math.Max(0, seconds.Value) : -1);

    public static void RecordSamplingDrop() => SamplingDrops.Add(1,
        new KeyValuePair<string, object?>("signal", "trace"),
        new KeyValuePair<string, object?>("reason", "rate_limit"));

    public static void ObserveAttempt(string outcome, TimeSpan elapsed)
    {
        // Outcome set is fixed, and no organization or shipment identifier is emitted.
        var safe = outcome is "processed" or "cancelled" or "reference_conflict"
            or "retry" or "failure" or "needs_reconciliation" or "stale"
            ? outcome : "other";
        Attempts.Add(1,
            new KeyValuePair<string, object?>("worker", "tracking"),
            new KeyValuePair<string, object?>("outcome", safe));
        Duration.Record(Math.Max(0, elapsed.TotalSeconds),
            new KeyValuePair<string, object?>("worker", "tracking"),
            new KeyValuePair<string, object?>("outcome", safe));
    }

    public static bool AddToHost(HostApplicationBuilder builder)
    {
        if (!builder.Configuration.GetValue<bool>("M17:Enabled")) return false;

        var raw = builder.Configuration["M17:OtlpEndpoint"];
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttp || endpoint.Port != 4317 ||
            endpoint.UserInfo.Length != 0 || endpoint.AbsolutePath != "/" ||
            endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
            !(string.Equals(endpoint.Host, "otelcol", StringComparison.OrdinalIgnoreCase) ||
              (IPAddress.TryParse(endpoint.Host.Trim('[', ']'), out var ip) &&
               IPAddress.IsLoopback(ip))))
            throw new InvalidOperationException(
                "M17 worker export requires the internal otelcol:4317 endpoint or loopback:4317");

        linkKey = M17CorrelationDigest.RequireKey(builder.Configuration["M17:LinkKey"]);
        Volatile.Write(ref enabled, 1);
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("harborops-tracking-worker"))
            .WithMetrics(m => m.AddMeter(MeterName).AddMeter("otel.sdk.experimental")
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = endpoint;
                    o.Protocol = OtlpExportProtocol.Grpc;
                    o.TimeoutMilliseconds = 1000;
                }))
            .WithTracing(t => t
                .SetSampler(new M17WorkerBoundedSampler())
                .AddSource(SourceName)
                .AddProcessor(new M17WorkerTraceSanitizer())
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = endpoint;
                    o.Protocol = OtlpExportProtocol.Grpc;
                    o.TimeoutMilliseconds = 1000;
                    o.ExportProcessorType = ExportProcessorType.Batch;
                    o.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>
                    {
                        MaxQueueSize = 2048,
                        MaxExportBatchSize = 256,
                        ExporterTimeoutMilliseconds = 1000,
                        ScheduledDelayMilliseconds = 1000
                    };
                }));
        return true;
    }
}
