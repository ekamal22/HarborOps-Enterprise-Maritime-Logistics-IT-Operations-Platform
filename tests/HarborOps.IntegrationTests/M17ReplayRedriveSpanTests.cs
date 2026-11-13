using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Worker;
using HarborOps.Worker.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class M17ReplayRedriveSpanTests
{
    [Fact]
    public async Task RealReplayAndAuthorizedRedriveEmitOneSpanPerWorkerAttemptNotPerHttpReplay()
    {
        var spans=new ConcurrentQueue<Activity>();
        using var listener=new ActivityListener {
            ShouldListenTo=source=>source.Name==M17WorkerTelemetry.SourceName,
            Sample=(ref ActivityCreationOptions<ActivityContext> _)=>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped=activity=>spans.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(listener);
        var connection=TestDatabase.ConnectionString();
        using var factory=new ReferenceDataHttpTests.ApiFactory(connection);
        var fixture=await ReferenceDataHttpTests.Setup(factory);
        using var admin=fixture.Admin;
        using var manager=fixture.Manager;
        using var analyst=fixture.Analyst;
        var source=await G9Support.NewSource(factory,fixture.A,"g17retry");
        var body=G9Support.EquipmentPayload("g17-retry-"+
            Guid.NewGuid().ToString("N")[..12],"SYN-BOX-UNKNOWN");
        var receipt=await G9Support.Accept(factory,source,body);
        var settings=new TrackingWorkerSettings(LeaseSeconds:12,HeartbeatSeconds:2,
            PollMilliseconds:100,BatchSize:8,Concurrency:1);
        using var services=new ServiceCollection()
            .AddDbContext<HarborOpsDbContext>(o=>o.UseNpgsql(connection))
            .AddSingleton(settings)
            .AddScoped<TrackingWorkStore>().BuildServiceProvider();
        var worker=new WorkerShell(services.GetRequiredService<IServiceScopeFactory>(),
            settings,NullLogger<WorkerShell>.Instance);
        try
        {
            await worker.StartAsync(CancellationToken.None);
            async Task<bool> IsAttempt(int generation,int attempt)
            {
                await using var scope=factory.Services.CreateAsyncScope();
                var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
                return await db.TrackingInboxWorkItems.AsNoTracking().AnyAsync(x=>
                    x.ReceiptId==receipt&&x.RedriveGeneration==generation&&
                    x.AttemptCount==attempt&&x.State=="NeedsReconciliation");
            }
            Assert.True(await TrackingWorkerG9ProcessTests.WaitFor(
                ()=>IsAttempt(0,1),TimeSpan.FromSeconds(50)));
            using (var replay=await G9Support.Post(factory,source,body))
            {
                Assert.Equal(HttpStatusCode.Accepted,replay.StatusCode);
                var json=await replay.Content.ReadFromJsonAsync<JsonElement>();
                Assert.True(json.GetProperty("duplicate").GetBoolean());
                Assert.Equal(receipt,json.GetProperty("receiptId").GetGuid());
            }
            var redrivePath="/api/v1/organizations/"+fixture.A+
                "/tracking-work/"+receipt+"/redrive";
            using(var redrive=await ReferenceDataHttpTests.Send(manager,HttpMethod.Post,
                redrivePath,new{reason="Synthetic G17 retry witness"}))
                Assert.Equal(HttpStatusCode.OK,redrive.StatusCode);
            Assert.True(await TrackingWorkerG9ProcessTests.WaitFor(
                ()=>IsAttempt(1,1),TimeSpan.FromSeconds(50)));
        }
        finally {await worker.StopAsync(CancellationToken.None);worker.Dispose();}
        var attempts=spans.Where(x=>x.OperationName=="tracking.process").ToArray();
        Assert.Equal(2,attempts.Length);
        Assert.All(attempts,span=>{
            Assert.Equal("needs_reconciliation",span.GetTagItem("harborops.outcome"));
            Assert.Null(span.GetTagItem("receipt_id"));
            Assert.Null(span.GetTagItem("organization_id"));
        });
        await using var verifyScope=factory.Services.CreateAsyncScope();
        var verify=verifyScope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(1,await verify.TrackingInboxReceipts.CountAsync(x=>x.Id==receipt));
        Assert.Equal(0,await verify.TrackingProcessingEffects.CountAsync(x=>x.ReceiptId==receipt));
        Assert.Equal(2,await verify.TrackingWorkAttemptEvents.CountAsync(x=>
            x.ReceiptId==receipt&&x.EventKind=="ReconciliationRequired"));
        Assert.Equal(1,await verify.TrackingWorkAttemptEvents.CountAsync(x=>
            x.ReceiptId==receipt&&x.EventKind=="ReconciliationRequired"&&
            x.Generation==0&&x.AttemptCount==1));
        Assert.Equal(1,await verify.TrackingWorkAttemptEvents.CountAsync(x=>
            x.ReceiptId==receipt&&x.EventKind=="ReconciliationRequired"&&
            x.Generation==1&&x.AttemptCount==1));
        Assert.Equal(1,await verify.TrackingWorkAttemptEvents.CountAsync(x=>
            x.ReceiptId==receipt&&x.EventKind=="Redriven"));
    }
}
