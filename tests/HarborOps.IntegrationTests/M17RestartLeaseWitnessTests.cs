using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

// A positive leased receipt is preserved across an actual API-host restart.
// This is deliberately separate from the worker's own lease-recovery test.
[Collection("M17WorkerShellIsolation")]
public sealed class M17RestartLeaseWitnessTests
{
    [Fact]
    public async Task ApiRestartPreservesActiveWorkerLeaseAndNoDuplicateEffects()
    {
        var priorEvents=Environment.GetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT");
        var priorLinks=Environment.GetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT");
        Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT","0");
        Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT","0");
        try
        {
        var connection=TestDatabase.ConnectionString();
        var nonce=Guid.NewGuid().ToString("N")[..12];
        var options=new Dictionary<string,string> {
            ["M17:Enabled"]="true",
            ["M17:OtlpEndpoint"]="http://127.0.0.1:4317",
            ["M17:LinkKey"]=Convert.ToBase64String(
                Enumerable.Range(1,32).Select(x=>(byte)x).ToArray())
        };
        Guid receipt,org;
        Guid? token;
        long epoch;
        int attempts;
        var payload=G9Support.EquipmentPayload("g17-lease-"+nonce,
            "SYN-BOX-UNKNOWN");
        using(var first=new ReferenceDataHttpTests.ApiFactory(connection,
            settings:options))
        {
            var tenant=await ReferenceDataHttpTests.Setup(first);
            using var admin=tenant.Admin;
            using var manager=tenant.Manager;
            using var analyst=tenant.Analyst;
            org=tenant.A;
            var source=await G9Support.NewSource(first,org,"g17lease");
            receipt=await G9Support.Accept(first,source,payload);
            await using var scope=first.Services.CreateAsyncScope();
            var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var store=new TrackingWorkStore(db,new TrackingWorkerSettings());
            var claim=await store.ClaimOneAsync(org,CancellationToken.None);
            Assert.NotNull(claim);
            Assert.Equal(receipt,claim!.ReceiptId);
            var leased=await db.TrackingInboxWorkItems.AsNoTracking()
                .SingleAsync(x=>x.ReceiptId==receipt);
            Assert.Equal("Leased",leased.State);
            Assert.NotNull(leased.LeaseToken);
            token=leased.LeaseToken;
            epoch=leased.LeaseEpoch;
            attempts=leased.AttemptCount;
            Assert.True(epoch>0);
            Assert.True(attempts>0);
            Assert.Equal(0,await db.TrackingProcessingEffects
                .CountAsync(x=>x.ReceiptId==receipt));
            using var http=first.CreateClient();
            using var live=await http.GetAsync("/health/live");
            Assert.Equal(HttpStatusCode.OK,live.StatusCode);
        }
        // Disposing the first host, then creating another from the same
        // PostgreSQL database is an actual API lifetime boundary. No worker
        // runs in this isolated test, so only API instrumentation can act.
        using(var restarted=new ReferenceDataHttpTests.ApiFactory(connection,
            settings:options))
        {
            using var client=restarted.CreateClient();
            using var live=await client.GetAsync("/health/live");
            Assert.Equal(HttpStatusCode.OK,live.StatusCode);
            using var baseline=await client.GetAsync("/api/v1/system/baseline");
            Assert.Equal(HttpStatusCode.OK,baseline.StatusCode);
            await using var scope=restarted.Services.CreateAsyncScope();
            var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var after=await db.TrackingInboxWorkItems.AsNoTracking()
                .SingleAsync(x=>x.ReceiptId==receipt);
            Assert.Equal("Leased",after.State);
            Assert.Equal(token,after.LeaseToken);
            Assert.Equal(epoch,after.LeaseEpoch);
            Assert.Equal(attempts,after.AttemptCount);
            Assert.Equal(1,await db.TrackingInboxReceipts
                .CountAsync(x=>x.Id==receipt));
            Assert.Equal(1,await db.TrackingAcceptedEvents
                .CountAsync(x=>x.ReceiptId==receipt));
            Assert.Equal(0,await db.TrackingProcessingEffects
                .CountAsync(x=>x.ReceiptId==receipt));
            Assert.Equal(1,await db.TrackingWorkAttemptEvents.CountAsync(x=>
                x.ReceiptId==receipt&&x.EventKind=="Claimed"));
        }
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT",priorEvents);
            Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT",priorLinks);
        }
    }
}
