using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HarborOps.ContractTests;

// Real API pipeline with M17 opted in. No live collector or database is needed.
public sealed class M17HttpContractTests : IDisposable
{
    private readonly string? oldEventLimit;
    private readonly string? oldLinkLimit;

    public M17HttpContractTests()
    {
        oldEventLimit = Environment.GetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT");
        oldLinkLimit = Environment.GetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT");
        Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT","0");
        Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT","0");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT",oldEventLimit);
        Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT",oldLinkLimit);
    }

    private static readonly string LinkKey = Convert.ToBase64String(
        Enumerable.Range(1,32).Select(i => (byte)i).ToArray());

    private static WebApplicationFactory<Program> Enabled(CapturingLoggerProvider? log = null) =>
        new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("M17:Enabled","true");
            builder.UseSetting("M17:OtlpEndpoint","http://127.0.0.1:4317");
            builder.UseSetting("M17:LinkKey",LinkKey);
            if (log is not null)
                builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(log));
        });

    [Fact]
    public async Task EnabledTelemetryDoesNotChangeExistingHealthAndDatabaseErrorContracts()
    {
        using var factory = Enabled();
        using var client = factory.CreateClient();
        using var live = await client.GetAsync("/health/live");
        var readinessTimer = Stopwatch.StartNew();
        using var ready = await client.GetAsync("/health/ready");
        readinessTimer.Stop();
        Assert.InRange(readinessTimer.Elapsed.TotalSeconds,0,8);
        using var failed = await client.GetAsync("/api/v1/system/baseline");
        Assert.Equal(HttpStatusCode.OK,live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,ready.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,failed.StatusCode);
        Assert.Equal("healthy",JsonDocument.Parse(await live.Content.ReadAsStringAsync())
            .RootElement.GetProperty("status").GetString());
        Assert.Equal("unhealthy",JsonDocument.Parse(await ready.Content.ReadAsStringAsync())
            .RootElement.GetProperty("status").GetString());
        using var failureBody = JsonDocument.Parse(await failed.Content.ReadAsStringAsync());
        Assert.Equal("dependency_unavailable",failureBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(Assert.Single(failed.Headers.GetValues("X-Correlation-ID")),
            failureBody.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task SpoofedCorrelationIsReplacedAndWarningKeepsServerGeneratedScope()
    {
        var logs = new CapturingLoggerProvider();
        using var factory = Enabled(logs);
        using var client = factory.CreateClient();
        const string spoof = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        using var request = new HttpRequestMessage(HttpMethod.Get,"/api/v1/system/baseline");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID",spoof);
        request.Headers.TryAddWithoutValidation("baggage","private_key=never-export-this");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        var issued = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        Assert.Matches("^[0-9a-f]{32}$",issued);
        Assert.NotEqual(spoof,issued);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(issued,body.RootElement.GetProperty("correlationId").GetString());
        var warning = Assert.Single(logs.Entries,entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("dependency is unavailable",StringComparison.Ordinal));
        Assert.Contains(warning.Scopes,scope => scope is IEnumerable<KeyValuePair<string,object>> values &&
            values.Any(kv => kv.Key == "CorrelationId" && issued.Equals(kv.Value)));
        Assert.All(logs.Entries,entry =>
        {
            Assert.DoesNotContain(spoof,entry.Message,StringComparison.Ordinal);
            Assert.DoesNotContain("never-export-this",entry.Message,StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("invalid", "test=value")]
    [InlineData("00-00000000000000000000000000000000-0000000000000000-01", "test=value")]
    [InlineData("00-0123456789abcdef0123456789abcdef-0000000000000000-01", "test=value")]
    [InlineData("00-0123456789abcdef0123456789abcdef-0123456789abcdef-xx", "private=never-export")]
    public async Task MalformedTraceContextDoesNotTurnDependencyFailureIntoUnhandled500(
        string traceParent, string traceState)
    {
        using var factory = Enabled();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get,"/api/v1/system/baseline");
        request.Headers.TryAddWithoutValidation("traceparent",traceParent);
        request.Headers.TryAddWithoutValidation("tracestate",traceState);
        request.Headers.TryAddWithoutValidation("baggage",new string('b',2048));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(traceParent,body,StringComparison.Ordinal);
        Assert.DoesNotContain(traceState,body,StringComparison.Ordinal);
        Assert.DoesNotContain(new string('b',64),body,StringComparison.Ordinal);
        Assert.Matches("^[0-9a-f]{32}$",
            Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
    }

    [Fact]
    public async Task EnabledTelemetryRecordsExactlyOneRequestDurationAndFailureObservation()
    {
        var durations = new List<double>();
        var failures = new List<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument,observer) =>
        {
            if (instrument.Meter.Name == "HarborOps.Observability")
                observer.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument,value,_,_) =>
        {
            if (instrument.Name == "harborops.request.duration") durations.Add(value);
        });
        listener.SetMeasurementEventCallback<long>((instrument,value,_,_) =>
        {
            if (instrument.Name == "harborops.request.failures") failures.Add(value);
        });
        listener.Start();
        using var factory = Enabled();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/system/baseline");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        Assert.Single(durations);
        Assert.True(durations[0]>0);
        // M17 publishes a zero-valued business/5xx baseline on startup
        // so the first real outage is observable by Prometheus rate().
        // A seed does not represent a failing request. Require exactly one
        // positive increment, no duplicate counting, and the zero baseline.
        Assert.Contains(0L,failures);
        Assert.All(failures,value=>Assert.InRange(value,0L,1L));
        var positiveFailures=failures.Where(value=>value>0).ToArray();
        Assert.Single(positiveFailures);
        Assert.Equal(1L,positiveFailures[0]);
        Assert.Equal(1L,failures.Sum());
    }

    [Fact]
    public async Task ValidW3CTraceparentConnectsApiRequestAndBoundedDatabaseSpan()
    {
        const string traceId="00112233445566778899aabbccddeeff";
        const string parentSpan="0123456789abcdef";
        var completed = new System.Collections.Concurrent.ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "HarborOps.Observability" ||
                source.Name.StartsWith("Microsoft.AspNetCore",StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => completed.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(listener);
        using var factory = Enabled();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get,"/api/v1/system/baseline");
        request.Headers.TryAddWithoutValidation("traceparent",
            "00-"+traceId+"-"+parentSpan+"-01");
        request.Headers.TryAddWithoutValidation("tracestate","vendor=value");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        var serverCorrelation = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        Assert.Matches("^[0-9a-f]{32}$",serverCorrelation);
        Assert.NotEqual(traceId,serverCorrelation);
        Assert.Contains(completed,span =>
            span.Kind == ActivityKind.Server &&
            span.TraceId.ToHexString() == traceId &&
            span.ParentSpanId.ToHexString() == parentSpan);
        Assert.Contains(completed,span =>
            span.Kind == ActivityKind.Client &&
            span.Source.Name == "HarborOps.Observability" &&
            span.TraceId.ToHexString() == traceId &&
            Equals(span.GetTagItem("harborops.operation"),"baseline_read"));
        Assert.DoesNotContain(completed,span =>
            span.TagObjects.Any(tag=>tag.Key=="db.statement" || tag.Key=="db.query.text"));

        // Accepting W3C context must not change an authentication or authorization decision:
        // an anonymous caller stays refused with or without propagated context, baggage that
        // claims a role, or a forged correlation value.
        const string protectedPath = "/api/v1/organizations/11111111-1111-1111-1111-111111111111/ports";
        using var plain = await client.GetAsync(protectedPath);
        using var contextual = new HttpRequestMessage(HttpMethod.Get,protectedPath);
        contextual.Headers.TryAddWithoutValidation("traceparent","00-"+traceId+"-"+parentSpan+"-01");
        contextual.Headers.TryAddWithoutValidation("tracestate","vendor=value");
        contextual.Headers.TryAddWithoutValidation("baggage","role=Admin,user=administrator");
        contextual.Headers.TryAddWithoutValidation("X-Correlation-ID","administrator");
        using var withContext = await client.SendAsync(contextual);
        Assert.Equal(plain.StatusCode,withContext.StatusCode);
        Assert.True((int)withContext.StatusCode is >= 300 and < 500,
            "Anonymous access to a protected route must stay refused");
        Assert.NotEqual(HttpStatusCode.OK,withContext.StatusCode);
        Assert.DoesNotContain("administrator",await withContext.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }
}
