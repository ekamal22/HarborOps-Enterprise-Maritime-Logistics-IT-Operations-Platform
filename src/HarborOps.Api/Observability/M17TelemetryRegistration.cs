using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace HarborOps.Api.Observability;

public static class M17TelemetryRegistration
{
    public static bool AddM17Telemetry(this WebApplicationBuilder builder)
    {
        var options = M17TelemetryOptions.FromConfiguration(builder.Configuration);
        if (!options.Enabled) return false;

        // Parse W3C trace context only; never propagate arbitrary caller baggage.
        Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());
        builder.Services.AddHostedService<M17HeartbeatService>();

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("harborops-api"))
            .WithMetrics(metrics => metrics
                // Custom bounded operation/status categories prevent hostile HTTP
                // method/Host/path values from multiplying native time-series.
                .AddMeter(M17Telemetry.MeterName)
                .AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = options.Endpoint;
                    exporter.Protocol = OtlpExportProtocol.Grpc;
                    exporter.TimeoutMilliseconds = 1000;
                }))
            .WithTracing(traces => traces
                .SetSampler(new M17BoundedSampler())
                .AddSource(M17Telemetry.TraceSourceName)
                .AddAspNetCoreInstrumentation(instrumentation =>
                {
                    instrumentation.RecordException = false;
                    instrumentation.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health");
                })
                .AddProcessor(new M17TraceSanitizer())
                .AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = options.Endpoint;
                    exporter.Protocol = OtlpExportProtocol.Grpc;
                    exporter.TimeoutMilliseconds = 1000;
                    exporter.ExportProcessorType = ExportProcessorType.Batch;
                    exporter.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>
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
