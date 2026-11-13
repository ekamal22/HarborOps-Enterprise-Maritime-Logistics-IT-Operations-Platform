using System.Diagnostics.Metrics;
using HarborOps.Api.Observability;
using HarborOps.Worker.Observability;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class M17MetricSchemaTests
{
    [Fact]
    public void EveryLocalInstrumentHasTheApprovedNameTypeAndUnit()
    {
        var instruments = new Dictionary<string, Instrument>(StringComparer.Ordinal);
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, observer) =>
        {
            if (instrument.Meter.Name is M17Telemetry.MeterName or M17WorkerTelemetry.MeterName)
            {
                var id = instrument.Meter.Name + ":" + instrument.Name;
                Assert.True(instruments.TryAdd(id, instrument), "Duplicate instrument: " + id);
                observer.EnableMeasurementEvents(instrument);
            }
        };
        listener.Start();
        _ = M17Telemetry.Meter;
        _ = M17WorkerTelemetry.Enabled;

        static void Expect<T>(IReadOnlyDictionary<string, Instrument> found,
            string meter, string name, string? unit = null) where T : Instrument
        {
            var key = meter + ":" + name;
            Assert.True(found.TryGetValue(key, out var instrument), "Missing instrument: " + key);
            Assert.IsType<T>(instrument);
            if (unit is not null) Assert.Equal(unit, instrument.Unit);
        }
        const string a = M17Telemetry.MeterName;
        const string w = M17WorkerTelemetry.MeterName;
        Expect<Counter<long>>(instruments,a,"harborops.request.failures");
        Expect<Histogram<double>>(instruments,a,"harborops.request.duration","s");
        Expect<Histogram<double>>(instruments,a,"harborops.db.operation.duration","s");
        Expect<Counter<long>>(instruments,a,"harborops.worker.attempts");
        Expect<Counter<long>>(instruments,a,"harborops.telemetry.dropped");
        Expect<Counter<long>>(instruments,a,"harborops.telemetry.heartbeat");
        Expect<Counter<long>>(instruments,a,"harborops.telemetry.transport.failures");
        Expect<ObservableGauge<int>>(instruments,a,"harborops.db.readiness");
        Expect<ObservableGauge<long>>(instruments,a,"harborops.db.readiness_sample_unix_seconds");
        Expect<ObservableGauge<int>>(instruments,a,"harborops.telemetry.collector_reachable");
        Expect<ObservableGauge<int>>(instruments,a,"harborops.service.running");
        Expect<Counter<long>>(instruments,w,"harborops.telemetry.dropped");
        Expect<Counter<long>>(instruments,w,"harborops.worker.heartbeat");
        Expect<Counter<long>>(instruments,w,"harborops.worker.attempts");
        Expect<Histogram<double>>(instruments,w,"harborops.worker.operation.duration","s");
        Expect<ObservableGauge<int>>(instruments,w,"harborops.service.running");
        Expect<ObservableGauge<double>>(instruments,w,"harborops.worker.backlog.age","s");
        Assert.Equal(17,instruments.Count);
    }

    [Fact]
    public void RequestCountIsDerivedOnlyFromDurationHistogramAndNotDuplicated()
    {
        var seen = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, observer) =>
        {
            if (instrument.Meter.Name == M17Telemetry.MeterName)
            {
                seen.Add(instrument.Name);
                observer.EnableMeasurementEvents(instrument);
            }
        };
        listener.Start();
        _ = M17Telemetry.Meter;
        Assert.Equal(1,seen.Count(name => name == "harborops.request.duration"));
        Assert.Equal(1,seen.Count(name => name == "harborops.request.failures"));
        Assert.DoesNotContain(seen,name => name is "harborops.request.count" or
            "harborops.request.total" or "harborops.requests.total");
    }

    [Fact]
    public void RealNonzeroRequestAndDatabaseDurationsReachMeterListeners()
    {
        var requests = new List<double>();
        var database = new List<double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, observer) =>
        {
            if (instrument.Meter.Name == M17Telemetry.MeterName)
                observer.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument,value,_,_) =>
        {
            if (instrument.Name == "harborops.request.duration")
                requests.Add(value);
            if (instrument.Name == "harborops.db.operation.duration")
                database.Add(value);
        });
        listener.Start();

        var context = new DefaultHttpContext();
        context.Response.StatusCode = 503;
        M17Telemetry.ObserveRequest(context,TimeSpan.FromMilliseconds(43));
        M17Telemetry.ObserveBaselineRead(false,TimeSpan.FromMilliseconds(21));
        Assert.Contains(requests,value => value > 0.04 && value < 0.05);
        Assert.Contains(database,value => value > 0.02 && value < 0.03);
    }

    [Fact]
    public void CustomMetricLabelsRejectUntrustedHostsPathsAndBusinessIdentifiers()
    {
        var observed = new List<KeyValuePair<string,object?>>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument,observer) =>
        {
            if (instrument.Meter.Name is M17Telemetry.MeterName or M17WorkerTelemetry.MeterName)
                observer.EnableMeasurementEvents(instrument);
        };
        void Capture(ReadOnlySpan<KeyValuePair<string,object?>> tags)
        {
            observed.AddRange(tags.ToArray());
        }
        listener.SetMeasurementEventCallback<long>((_,_,tags,_)=>Capture(tags));
        listener.SetMeasurementEventCallback<int>((_,_,tags,_)=>Capture(tags));
        listener.SetMeasurementEventCallback<double>((_,_,tags,_)=>Capture(tags));
        listener.Start();

        var request = new DefaultHttpContext();
        request.Request.Path = "/tenant-secret-identifier/private-shipment-123";
        request.Request.Host = new HostString("tenant-secret.example.test");
        request.Response.StatusCode = 503;
        M17Telemetry.ObserveRequest(request,TimeSpan.FromMilliseconds(5));
        M17Telemetry.ObserveBaselineRead(false,TimeSpan.FromMilliseconds(2));
        M17Telemetry.RecordExporterFailure(2);
        M17WorkerTelemetry.ObserveAttempt("tenant-secret-result",TimeSpan.FromMilliseconds(3));
        listener.RecordObservableInstruments();
        Assert.NotEmpty(observed);
        var keyAllowlist = new HashSet<string>(StringComparer.Ordinal)
        {
            "operation","outcome","status_class","worker","service","signal","reason"
        };
        foreach (var label in observed)
        {
            Assert.Contains(label.Key,keyAllowlist);
            var value = label.Value?.ToString() ?? "";
            Assert.DoesNotContain("tenant-secret",value,StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("private-shipment",value,StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("example.test",value,StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("identifier",value,StringComparison.OrdinalIgnoreCase);
            Assert.InRange(value.Length,0,40);
        }
        Assert.Contains(observed,tag=>tag.Key=="operation" && Equals(tag.Value,"other"));
        Assert.Contains(observed,tag=>tag.Key=="outcome" && Equals(tag.Value,"other"));
        Assert.Contains(observed,tag=>tag.Key=="status_class" && Equals(tag.Value,"5xx"));
    }
}
