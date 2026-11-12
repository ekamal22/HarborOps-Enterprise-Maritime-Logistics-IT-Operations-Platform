using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.Extensions.Configuration;
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
    private static readonly Counter<long> Attempts =
        Meter.CreateCounter<long>("harborops.worker.attempts", "{attempts}");
    private static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("harborops.worker.operation.duration", "s");

    public static void ObserveAttempt(string outcome, TimeSpan elapsed)
    {
        // Outcome set is fixed, and no organization or shipment identifier is emitted.
        var safe = outcome is "processed" or "cancelled" or "reference_conflict"
            or "retry" or "failure" ? outcome : "other";
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

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("harborops-tracking-worker"))
            .WithMetrics(m => m.AddMeter(MeterName)
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = endpoint;
                    o.Protocol = OtlpExportProtocol.Grpc;
                    o.TimeoutMilliseconds = 1000;
                }))
            .WithTracing(t => t
                .SetSampler(new TraceIdRatioBasedSampler(0.10))
                .AddSource(SourceName)
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
