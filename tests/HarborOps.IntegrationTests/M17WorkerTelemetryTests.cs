using System.Diagnostics;
using System.Diagnostics.Metrics;
using HarborOps.Api.Observability;
using HarborOps.Infrastructure.Observability;
using HarborOps.Worker.Observability;
using OpenTelemetry.Trace;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class M17WorkerTelemetryTests
{
    [Fact]
    public void WorkerDispositionsAreMeasuredWithExactFiniteOutcomes()
    {
        var outcomes = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, observer) =>
        {
            if (instrument.Meter.Name == M17WorkerTelemetry.MeterName)
                observer.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (instrument.Name == "harborops.worker.attempts")
            {
                var outcome = tags.ToArray().FirstOrDefault(t => t.Key == "outcome").Value;
                if (outcome is string text) outcomes.Add(text);
            }
        });
        listener.Start();
        foreach (var item in new[]
            {"processed", "needs_reconciliation", "stale", "retry", "unexpected"})
            M17WorkerTelemetry.ObserveAttempt(item, TimeSpan.FromMilliseconds(20));
        Assert.Equal(new[] {"processed", "needs_reconciliation", "stale", "retry", "other"},
            outcomes);
    }

    [Fact]
    public void KeyedReceiptAndWorkerSamplersAgreeWithoutASharedTraceParent()
    {
        var key = M17CorrelationDigest.RequireKey(Convert.ToBase64String(
            Enumerable.Range(1,32).Select(n => (byte)n).ToArray()));
        var found = 0;
        for (var i = 0; i < 1000; i++)
        {
            var digest = M17CorrelationDigest.FromServerCorrelation(i.ToString("x32"), key)!;
            var tags = new KeyValuePair<string, object?>[] {
                new("harborops.link_digest", digest)
            };
            var apiParameters = new SamplingParameters(default,
                ActivityTraceId.CreateFromString("abcdefabcdefabcdefabcdefabcdefab".AsSpan()),
                "tracking.receipt", ActivityKind.Internal, tags);
            var workerParameters = new SamplingParameters(default,
                ActivityTraceId.CreateFromString("12345678123456781234567812345678".AsSpan()),
                "tracking.process", ActivityKind.Internal, tags);
            var api = new M17BoundedSampler().ShouldSample(in apiParameters).Decision;
            var worker = new M17WorkerBoundedSampler().ShouldSample(in workerParameters).Decision;
            Assert.Equal(api, worker);
            if (api == SamplingDecision.RecordAndSample) found++;
        }
        Assert.InRange(found, 50, 150);
    }

    [Fact]
    public void WorkerTraceSanitizerRejectsUntrustedDetailsAndNonTagFields()
    {
        using var activity = new Activity("private-worker-span");
        activity.Start();
        activity.TraceStateString = "customer=secret";
        activity.SetStatus(ActivityStatusCode.Error, "private database details");
        activity.SetTag("db.statement", "select * from private");
        activity.SetTag("harborops.operation", "tracking.process");
        activity.SetTag("harborops.link_digest", "forged");
        new M17WorkerTraceSanitizer().OnEnd(activity);
        Assert.Null(activity.TraceStateString);
        Assert.Null(activity.StatusDescription);
        Assert.Null(activity.GetTagItem("db.statement"));
        Assert.Null(activity.GetTagItem("harborops.link_digest"));
        Assert.Equal("tracking.process", activity.GetTagItem("harborops.operation"));
    }
}
