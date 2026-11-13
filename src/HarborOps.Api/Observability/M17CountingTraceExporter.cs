using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using HarborOps.Infrastructure.Observability;

namespace HarborOps.Api.Observability;

// The underlying SDK provides batch queue loss metrics. This exporter records
// failed OTLP batch submissions separately, never counting a successful batch
// as delivered until its actual exporter returned success.
public sealed class M17CountingTraceExporter : OtlpTraceExporter
{
    public M17CountingTraceExporter(OtlpExporterOptions options) : base(options)
    {
        M17TracePrivacyGuard.RequireSafeExporterLimits();
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        var result = base.Export(in batch);
        if (result != ExportResult.Success)
            M17Telemetry.RecordExporterFailure(batch.Count);
        return result;
    }
}
