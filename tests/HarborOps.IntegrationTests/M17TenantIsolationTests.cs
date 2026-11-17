using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace HarborOps.IntegrationTests;

// Creates only synthetic tenant records in TestDatabase's approved isolated DB.
public sealed class M17TenantIsolationTests
{
    [Fact]
    public async Task OptedInTelemetryPreservesTenantDenialsAndAnalystWriteRevocation()
    {
        var priorEvents = Environment.GetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT");
        var priorLinks = Environment.GetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT");
        Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT","0");
        Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT","0");
        var allDimensions = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument,observer) =>
        {
            if (instrument.Meter.Name == "HarborOps.Observability")
                observer.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument,_,tags,_) =>
        {
            if (instrument.Name == "harborops.request.duration")
                foreach (var tag in tags)
                    allDimensions.Add(tag.Key+"="+tag.Value);
        });
        listener.Start();
        try
        {
            var settings = new Dictionary<string,string>
            {
                ["M17:Enabled"] = "true",
                ["M17:OtlpEndpoint"] = "http://127.0.0.1:4317",
                ["M17:LinkKey"] = Convert.ToBase64String(
                    Enumerable.Range(1,32).Select(n=>(byte)n).ToArray())
            };
            using var factory = new ReferenceDataHttpTests.ApiFactory(
                TestDatabase.ConnectionString(),settings:settings);
            var tenant = await ReferenceDataHttpTests.Setup(factory);
            using var manager = tenant.Manager;
            using var analyst = tenant.Analyst;
            using var admin = tenant.Admin;
            var ownPath = "/api/v1/organizations/" + tenant.A + "/ports";
            var foreignPath = "/api/v1/organizations/" + tenant.B + "/ports";
            using var own = await manager.GetAsync(ownPath);
            using var foreign = await manager.GetAsync(foreignPath);
            Assert.Equal(HttpStatusCode.OK,own.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,foreign.StatusCode);
            using var refused = await ReferenceDataHttpTests.Send(
                analyst,HttpMethod.Post,ownPath,new {
                    referenceCode = "M17-UNAUTHORIZED",name = "Must Not Persist",
                    countryCode = "US"
                });
            Assert.Equal(HttpStatusCode.Forbidden,refused.StatusCode);
            var after = await manager.GetFromJsonAsync<JsonElement>(ownPath);
            Assert.Empty(after.GetProperty("ports").EnumerateArray());
            Assert.NotEmpty(allDimensions);
            Assert.All(allDimensions,tag =>
            {
                Assert.DoesNotContain(tenant.A.ToString(),tag,StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(tenant.B.ToString(),tag,StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("M17-UNAUTHORIZED",tag,StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("organization_id",tag,StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("tenant_id",tag,StringComparison.OrdinalIgnoreCase);
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT",priorEvents);
            Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT",priorLinks);
        }
    }
}
