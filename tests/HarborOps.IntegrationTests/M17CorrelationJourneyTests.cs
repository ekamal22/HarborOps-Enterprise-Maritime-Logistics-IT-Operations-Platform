using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Observability;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class M17CorrelationJourneyTests
{
    [Fact]
    public async Task DurableReceiptLinksToReadOnlyWorkerCorrelationWithoutForgedParentOrReplayMarker()
    {
        var oldEvents = Environment.GetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT");
        var oldLinks = Environment.GetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT");
        Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT","0");
        Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT","0");
        var key = Enumerable.Range(1,32).Select(i=>(byte)i).ToArray();
        var completed = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source=>source.Name=="HarborOps.Observability",
            Sample=(ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity=>completed.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(listener);
        try
        {
            var settings = new Dictionary<string,string>
            {
                ["M17:Enabled"]="true",
                ["M17:OtlpEndpoint"]="http://127.0.0.1:4317",
                ["M17:LinkKey"]=Convert.ToBase64String(key)
            };
            using var factory = new ReferenceDataHttpTests.ApiFactory(
                TestDatabase.ConnectionString(),settings:settings);
            var tenant = await ReferenceDataHttpTests.Setup(factory);
            using var admin = tenant.Admin;
            using var manager = tenant.Manager;
            using var analyst = tenant.Analyst;
            var source = await G9Support.NewSource(factory,tenant.A,"g17bridge");
            var body = G9Support.EquipmentPayload(
                "g17-receipt-"+Guid.NewGuid().ToString("N")[..10],"SYN-BOX-UNKNOWN");
            using var first = await G9Support.Post(factory,source,body);
            Assert.Equal(HttpStatusCode.Accepted,first.StatusCode);
            var firstCorrelation = Assert.Single(first.Headers.GetValues("X-Correlation-ID"));
            var accepted = await first.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(accepted.GetProperty("duplicate").GetBoolean());
            var receiptId = accepted.GetProperty("receiptId").GetGuid();
            Assert.Equal(firstCorrelation,accepted.GetProperty("correlationId").GetString());

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var persisted = await db.TrackingInboxReceipts.AsNoTracking()
                .SingleAsync(r=>r.Id==receiptId);
            Assert.Equal(firstCorrelation,persisted.CorrelationId);
            var digest=M17CorrelationDigest.FromServerCorrelation(persisted.CorrelationId,key);
            Assert.NotNull(digest);
            Assert.Equal(64,digest.Length);
            var markers=completed.Where(a=>a.OperationName=="tracking.receipt").ToList();
            var marker = Assert.Single(markers);
            Assert.Equal(digest,marker.GetTagItem("harborops.link_digest"));
            Assert.NotEqual(firstCorrelation,marker.GetTagItem("harborops.link_digest"));
            Assert.Null(marker.GetTagItem("organization_id"));

            var work = new TrackingWorkStore(db,new TrackingWorkerSettings());
            var claim = await work.ClaimOneAsync(tenant.A,CancellationToken.None);
            Assert.NotNull(claim);
            Assert.Equal(receiptId,claim!.ReceiptId);
            Assert.Equal(firstCorrelation,
                await work.ReadReceiptCorrelationAsync(claim,CancellationToken.None));
            var foreignClaim = claim with {OrganizationId=tenant.B};
            Assert.Null(await work.ReadReceiptCorrelationAsync(foreignClaim,CancellationToken.None));

            using var replay = await G9Support.Post(factory,source,body);
            Assert.Equal(HttpStatusCode.Accepted,replay.StatusCode);
            var replayBody = await replay.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(replayBody.GetProperty("duplicate").GetBoolean());
            Assert.Equal(receiptId,replayBody.GetProperty("receiptId").GetGuid());
            Assert.NotEqual(firstCorrelation,Assert.Single(replay.Headers.GetValues("X-Correlation-ID")));
            Assert.Single(completed.Where(a=>a.OperationName=="tracking.receipt"));
            Assert.Equal(1,await db.TrackingInboxReceipts.CountAsync(r=>r.Id==receiptId));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT",oldEvents);
            Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT",oldLinks);
        }
    }
}
