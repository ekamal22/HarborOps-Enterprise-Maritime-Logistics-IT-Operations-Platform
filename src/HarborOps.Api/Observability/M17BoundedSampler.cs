using OpenTelemetry.Trace;

namespace HarborOps.Api.Observability;

// Deterministic 10% trace-ID sampling with a hard per-process admission ceiling.
// Caller-supplied sampled flags cannot bypass the bound.
public sealed class M17BoundedSampler : Sampler
{
    private readonly Sampler inner = new TraceIdRatioBasedSampler(0.10);
    private readonly object gate = new();
    private long second = -1;
    private int admitted;

    public M17BoundedSampler() => Description = "M17 10-percent trace-ID rate-bound sampler";

    public override SamplingResult ShouldSample(in SamplingParameters parameters)
    {
        var choice = inner.ShouldSample(in parameters);
        if (choice.Decision != SamplingDecision.RecordAndSample)
            return choice;

        lock (gate)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now != second)
            {
                second = now;
                admitted = 0;
            }
            if (admitted >= 200)
            {
                M17Telemetry.RecordSamplingDrop();
                return new SamplingResult(SamplingDecision.Drop);
            }
            admitted++;
            return choice;
        }
    }

}
