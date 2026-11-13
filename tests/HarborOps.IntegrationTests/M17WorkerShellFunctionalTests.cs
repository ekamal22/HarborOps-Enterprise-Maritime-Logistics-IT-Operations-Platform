using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using HarborOps.Api.Observability;
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

[Collection("M17WorkerShellIsolation")]
public sealed class M17WorkerShellFunctionalTests
{
    [Fact]
    public async Task ActualWorkerShellEmitsProcessedAndReconciliationDispositionsWithoutDuplicateEffects()
    {
        var connection=TestDatabase.ConnectionString();
        using var factory=new ReferenceDataHttpTests.ApiFactory(connection);
        var tenant=await ReferenceDataHttpTests.Setup(factory);
        using var admin=tenant.Admin;
        using var manager=tenant.Manager;
        using var analyst=tenant.Analyst;
        var target=await G9Support.SeedResolvable(factory,tenant);
        var source=await G9Support.NewSource(factory,tenant.A,"g17worker");
        var received=await G9Support.Accept(factory,source,G9Support.EquipmentPayload(
            "g17-success-"+Guid.NewGuid().ToString("N")[..10],target.Reference));
        var parked=await G9Support.Accept(factory,source,G9Support.EquipmentPayload(
            "g17-reconcile-"+Guid.NewGuid().ToString("N")[..10],"SYN-BOX-UNKNOWN"));

        var observed=new ConcurrentQueue<string>();
        using var meter=new MeterListener();
        meter.InstrumentPublished=(instrument,listener)=>{
            if(instrument.Meter.Name==M17WorkerTelemetry.MeterName)
                listener.EnableMeasurementEvents(instrument);
        };
        meter.SetMeasurementEventCallback<long>((instrument,value,tags,_)=>{
            if(instrument.Name!="harborops.worker.attempts")return;
            var values=tags.ToArray();
            var worker=values.FirstOrDefault(t=>t.Key=="worker").Value?.ToString();
            var outcome=values.FirstOrDefault(t=>t.Key=="outcome").Value?.ToString();
            Assert.Equal("tracking",worker);
            Assert.DoesNotContain(values,v=>v.Key.Contains("tenant",StringComparison.OrdinalIgnoreCase)||
                v.Key.Contains("receipt",StringComparison.OrdinalIgnoreCase));
            if(outcome is not null) observed.Enqueue(outcome);
        });
        meter.Start();

        var settings=new TrackingWorkerSettings(LeaseSeconds:12,HeartbeatSeconds:2,
            PollMilliseconds:100,BatchSize:8,Concurrency:2);
        using var services=new ServiceCollection()
            .AddDbContext<HarborOpsDbContext>(o=>o.UseNpgsql(connection))
            .AddSingleton(settings)
            .AddScoped<TrackingWorkStore>()
            .BuildServiceProvider();
        var shell=new WorkerShell(services.GetRequiredService<IServiceScopeFactory>(),
            settings,NullLogger<WorkerShell>.Instance);
        try
        {
            await shell.StartAsync(CancellationToken.None);
            var complete=await TrackingWorkerG9ProcessTests.WaitFor(async()=>{
                await using var scope=factory.Services.CreateAsyncScope();
                var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
                var states=await db.TrackingInboxWorkItems.AsNoTracking()
                    .Where(x=>x.ReceiptId==received||x.ReceiptId==parked)
                    .Select(x=>new{x.ReceiptId,x.State})
                    .ToListAsync();
                return states.Count==2&&
                    states.Any(x=>x.ReceiptId==received&&x.State=="Processed")&&
                    states.Any(x=>x.ReceiptId==parked&&x.State=="NeedsReconciliation");
            },TimeSpan.FromSeconds(45));
            Assert.True(complete,"Actual worker did not finish both synthetic disposition families");
        }
        finally {await shell.StopAsync(CancellationToken.None);shell.Dispose();}
        Assert.Contains("processed",observed);
        Assert.Contains("needs_reconciliation",observed);
        await using var check=factory.Services.CreateAsyncScope();
        var verify=check.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(1,await verify.TrackingProcessingEffects.CountAsync(x=>x.ReceiptId==received));
        Assert.Equal(0,await verify.TrackingProcessingEffects.CountAsync(x=>x.ReceiptId==parked));
        Assert.Equal(1,await verify.TrackingWorkAttemptEvents.CountAsync(x=>
            x.ReceiptId==received&&x.EventKind=="Processed"));
        Assert.Equal(1,await verify.TrackingWorkAttemptEvents.CountAsync(x=>
            x.ReceiptId==parked&&x.EventKind=="ReconciliationRequired"));
    }

    [Fact]
    public void DefaultApiHostDoesNotRegisterTheHeldTrackingWorker()
    {
        using var factory=new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        Assert.Null(factory.Services.GetService<WorkerShell>());
        Assert.Null(factory.Services.GetService<TrackingWorkStore>());
        Assert.False(M17WorkerTelemetry.Enabled);
    }
}
