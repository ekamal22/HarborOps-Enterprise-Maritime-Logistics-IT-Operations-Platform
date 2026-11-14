using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Api.Observability;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class M17SuccessfulDatabaseAndAuditTests
{
    private static Dictionary<string,string> Settings() => new()
    {
        ["M17:Enabled"]="true",
        ["M17:OtlpEndpoint"]="http://127.0.0.1:4317",
        ["M17:LinkKey"]=Convert.ToBase64String(
            Enumerable.Range(1,32).Select(n=>(byte)n).ToArray())
    };

    [Fact]
    public async Task HealthyDatabaseReadRecordsSuccessDurationAndLinkedSafeSpan()
    {
        var priorEvents=Environment.GetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT");
        var priorLinks=Environment.GetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT");
        Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT","0");
        Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT","0");
        try
        {
            var samples=new ConcurrentQueue<(string Operation,string Outcome,double Duration)>();
            var spans=new ConcurrentQueue<Activity>();
            using var meter=new MeterListener();
            meter.InstrumentPublished=(instrument,listener)=>
            {
                if(instrument.Meter.Name==M17Telemetry.MeterName)
                    listener.EnableMeasurementEvents(instrument);
            };
            meter.SetMeasurementEventCallback<double>((instrument,value,tags,_)=>
            {
                if(instrument.Name!="harborops.db.operation.duration")return;
                var values=tags.ToArray();
                var op=values.FirstOrDefault(t=>t.Key=="operation").Value?.ToString()??"";
                var status=values.FirstOrDefault(t=>t.Key=="outcome").Value?.ToString()??"";
                samples.Enqueue((op,status,value));
            });
            meter.Start();
            using var traces=new ActivityListener
            {
                ShouldListenTo=source=>source.Name==M17Telemetry.TraceSourceName,
                Sample=(ref ActivityCreationOptions<ActivityContext> _)=>
                    ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped=span=>spans.Enqueue(span)
            };
            ActivitySource.AddActivityListener(traces);
            using var factory=new ReferenceDataHttpTests.ApiFactory(
                TestDatabase.ConnectionString(),settings:Settings());
            var tenant=await ReferenceDataHttpTests.Setup(factory);
            // Seed the real database baseline required by this successful-read witness.
            await using (var seedScope=factory.Services.CreateAsyncScope())
                await BootstrapSeeder.SeedAsync(
                    seedScope.ServiceProvider.GetRequiredService<HarborOpsDbContext>(),
                    CancellationToken.None);
            using var admin=tenant.Admin;
            using var manager=tenant.Manager;
            using var analyst=tenant.Analyst;
            const string traceId="00112233445566778899aabbccddeeff";
            using var request=new HttpRequestMessage(HttpMethod.Get,"/api/v1/system/baseline");
            request.Headers.TryAddWithoutValidation("traceparent",
                "00-"+traceId+"-0123456789abcdef-01");
            using var response=await manager.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("HarborOps",doc.RootElement.GetProperty("service").GetString());
            Assert.Contains(samples,s=>s.Operation=="baseline_read"&&
                s.Outcome=="success"&&s.Duration>0&&s.Duration<15);
            Assert.Contains(spans,s=>s.OperationName=="baseline.read"&&
                s.Kind==ActivityKind.Client&&
                s.TraceId.ToHexString()==traceId&&
                Equals(s.GetTagItem("harborops.operation"),"baseline_read"));
            Assert.DoesNotContain(spans,s=>s.TagObjects.Any(tag=>
                tag.Key.Contains("sql",StringComparison.OrdinalIgnoreCase)||
                tag.Key.Contains("credential",StringComparison.OrdinalIgnoreCase)||
                tag.Key=="db.statement"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT",priorEvents);
            Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT",priorLinks);
        }
    }

    [Fact]
    public async Task OptedInTrackingAcceptancePreservesImmutableAuditCorrelation()
    {
        var priorEvents=Environment.GetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT");
        var priorLinks=Environment.GetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT");
        Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT","0");
        Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT","0");
        try
        {
            using var factory=new ReferenceDataHttpTests.ApiFactory(
                TestDatabase.ConnectionString(),settings:Settings());
            var tenant=await ReferenceDataHttpTests.Setup(factory);
            // Seed the real database baseline required by this successful-read witness.
            await using (var seedScope=factory.Services.CreateAsyncScope())
                await BootstrapSeeder.SeedAsync(
                    seedScope.ServiceProvider.GetRequiredService<HarborOpsDbContext>(),
                    CancellationToken.None);
            using var admin=tenant.Admin;
            using var manager=tenant.Manager;
            using var analyst=tenant.Analyst;
            var source=await G9Support.NewSource(factory,tenant.A,"g17audit");
            using var accepted=await G9Support.Post(factory,source,
                G9Support.EquipmentPayload("g17-audit-"+Guid.NewGuid().ToString("N")[..10],
                    "SYN-BOX-AUDIT"));
            Assert.Equal(HttpStatusCode.Accepted,accepted.StatusCode);
            var correlation=Assert.Single(accepted.Headers.GetValues("X-Correlation-ID"));
            var payload=await accepted.Content.ReadFromJsonAsync<JsonElement>();
            var receiptId=payload.GetProperty("receiptId").GetGuid();
            await using var scope=factory.Services.CreateAsyncScope();
            var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var eventRow=await db.TrackingAcceptedEvents.AsNoTracking()
                .SingleAsync(x=>x.ReceiptId==receiptId);
            var audit=await db.AuditEntries.AsNoTracking().SingleAsync(x=>
                x.OrganizationId==tenant.A&&
                x.ResourceId==eventRow.Id&&
                x.EventType=="integration.tracking_accepted");
            Assert.Equal(correlation,audit.CorrelationId);
            Assert.Equal(tenant.A,audit.OrganizationId);
            Assert.Equal("IntegrationSource",audit.ActorKind);
            Assert.DoesNotContain(receiptId.ToString(),audit.MetadataJson,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT",priorEvents);
            Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT",priorLinks);
        }
    }
}
