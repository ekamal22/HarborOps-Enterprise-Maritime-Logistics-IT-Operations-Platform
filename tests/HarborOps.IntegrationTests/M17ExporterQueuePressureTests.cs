using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace HarborOps.IntegrationTests;

// Exercises the pinned production SDK batch implementation with exactly the
// M17 2,048-slot queue. The exporter is held, not the business request thread.
public sealed class M17ExporterQueuePressureTests
{
    private sealed class HeldExporter(ManualResetEventSlim release) : BaseExporter<Activity>
    {
        private int attempted;
        public int Attempted => Volatile.Read(ref attempted);

        public override ExportResult Export(in Batch<Activity> batch)
        {
            Interlocked.Increment(ref attempted);
            release.Wait(TimeSpan.FromSeconds(15));
            return ExportResult.Success;
        }
    }

    [Fact]
    public void QueueFullDropsAreCountedWithoutBlockingSpanProducers()
    {
        using var gate=new ManualResetEventSlim(false);
        var exporter=new HeldExporter(gate);
        long drops=0;
        using var meter=new MeterListener();
        meter.InstrumentPublished=(instrument,listener)=>{
            if(instrument.Meter.Name=="otel.sdk.experimental"&&
                instrument.Name=="otel.sdk.processor.span.processed")
                listener.EnableMeasurementEvents(instrument);
        };
        meter.SetMeasurementEventCallback<long>((instrument,value,tags,_)=>{
            if(instrument.Name=="otel.sdk.processor.span.processed"&&
                tags.ToArray().Any(tag=>tag.Key=="error.type"&&
                    Equals(tag.Value,"queue_full")))
                Interlocked.Add(ref drops,value);
        });
        meter.Start();
        using var activitySource=new ActivitySource("G17.RealQueueProbe");
        using var provider=Sdk.CreateTracerProviderBuilder()
            .AddSource("G17.RealQueueProbe")
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(new BatchActivityExportProcessor(exporter,
                maxQueueSize:2048,
                scheduledDelayMilliseconds:1000,
                exporterTimeoutMilliseconds:1000,
                maxExportBatchSize:256))
            .Build();
        try
        {
            var started=Stopwatch.GetTimestamp();
            for(var i=0;i<10_000;i++)
            {
                using var span=activitySource.StartActivity("bounded");
                Assert.NotNull(span);
                span.SetTag("harborops.operation","other");
            }
            var elapsed=Stopwatch.GetElapsedTime(started);
            Assert.True(elapsed<TimeSpan.FromSeconds(12),
                "Export queue saturation blocked diagnostic span producers");
            Assert.True(Volatile.Read(ref drops)>0,
                "The real SDK queue did not report a queue_full drop");
        }
        finally
        {
            gate.Set();
        }
    }
}
