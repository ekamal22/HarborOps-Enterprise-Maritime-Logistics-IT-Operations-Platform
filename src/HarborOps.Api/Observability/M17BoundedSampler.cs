using OpenTelemetry.Trace;

namespace HarborOps.Api.Observability;

// 10% root head sampling with a hard per-process admission ceiling. The
// upper bound is deliberately conservative; child spans are also counted.
public sealed class M17BoundedSampler : Sampler
{
    private readonly Sampler inner = new ParentBasedSampler(new TraceIdRatioBasedSampler(0.10));
    private readonly object gate = new();
    private long second = -1;
    private int admitted;

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

    public override string Description => "M17 10-percent parent-based rate-bound sampler";
}
