using OpenTelemetry.Trace;

namespace HarborOps.Worker.Observability;

// A bounded, nonblocking head sampler. Untrusted remote sampled flags do not
// override the local trace-ID decision or the 200-span-per-second budget.
public sealed class M17WorkerBoundedSampler : Sampler
{
    private readonly Sampler sampler = new TraceIdRatioBasedSampler(0.10);
    private readonly object gate = new();
    private long currentSecond = -1;
    private int admitted;

    public M17WorkerBoundedSampler() =>
        Description = "M17 worker trace-ID rate-bound sampler";

    public override SamplingResult ShouldSample(in SamplingParameters parameters)
    {
        var decision = sampler.ShouldSample(in parameters);
        if (decision.Decision != SamplingDecision.RecordAndSample) return decision;
        lock (gate)
        {
            var second = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (second != currentSecond)
            {
                currentSecond = second;
                admitted = 0;
            }
            if (admitted >= 200)
            {
                M17WorkerTelemetry.RecordSamplingDrop();
                return new SamplingResult(SamplingDecision.Drop);
            }
            admitted++;
            return decision;
        }
    }
}
