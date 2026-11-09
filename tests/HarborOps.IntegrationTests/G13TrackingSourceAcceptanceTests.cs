using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class G13TrackingSourceAcceptanceTests
{
    private static long Metric(JsonElement report, string code) =>
        report.GetProperty("counts").EnumerateArray()
            .Where(x => x.GetProperty("metricCode").GetString() == code)
            .Sum(x => x.GetProperty("count").GetInt64());

    private static async Task<JsonElement> Report(HttpClient client, Guid org)
    {
        using var response = await client.GetAsync(
            $"/api/v1/organizations/{org}/analytics/overview");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<HashSet<Guid>> ActiveMemberKeys(
        HttpClient client, Guid org, JsonElement report)
    {
        using var query = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/organizations/{org}/analytics/reports/" +
            report.GetProperty("reportId").GetGuid() +
            "/members?metricCode=K01&limit=100");
        query.Headers.TryAddWithoutValidation("X-HarborOps-Report",
            report.GetProperty("reportToken").GetString());
        using var response = await client.SendAsync(query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null,page.GetProperty("nextAfter").ValueKind);
        var list = page.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("memberKey").GetGuid()).ToArray();
        Assert.Equal(list.Length, list.Distinct().Count());
        return list.ToHashSet();
    }

    [Fact]
    public async Task ReplayedEquipmentReceiptsAndParkedTransportNeverInventKpiProgress()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var fixtureFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(fixtureFactory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var target = await G9Support.SeedResolvable(fixtureFactory,fixture);
        var source = await G9Support.NewSource(
            fixtureFactory,fixture.A,"g13_kpi_source");
        using var scopedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var scoped = scopedFactory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(scoped,fixture.AnalystEmail);
        await ReferenceDataHttpTests.Select(scoped,fixture.A);

        var baseline = await Report(scoped,fixture.A);
        var before = await ActiveMemberKeys(scoped,fixture.A,baseline);
        Assert.Equal(2,before.Count);
        Assert.Contains(target.Shipment,before);
        Assert.Equal(2L,Metric(baseline,"K01"));
        Assert.Equal(0L,Metric(baseline,"K03"));
        Assert.Equal(0L,Metric(baseline,"K04"));

        var body = G9Support.EquipmentPayload(
            "g13-" + Guid.NewGuid().ToString("N")[..12],target.Reference);
        var first = await G9Support.Submit(fixtureFactory,source,body);
        Assert.Equal(HttpStatusCode.Accepted,first.Status);
        Assert.False(first.Duplicate);
        for(var i=0;i<8;i++)
        {
            var replay=await G9Support.Submit(fixtureFactory,source,body);
            Assert.Equal(HttpStatusCode.Accepted,replay.Status);
            Assert.True(replay.Duplicate);
            Assert.Equal(first.Receipt,replay.Receipt);
        }
        var arr=await G9Support.Accept(fixtureFactory,source,
            G9Support.TransportPayload(
                "g13-arri-" + Guid.NewGuid().ToString("N")[..12],
                "SYN-VOY-ARRI"));
        var depa=await G9Support.Accept(fixtureFactory,source,
            G9Support.TransportPayload(
                "g13-depa-" + Guid.NewGuid().ToString("N")[..12],
                "SYN-VOY-DEPA").Replace(
                    "\"eventCode\":\"ARRI\"","\"eventCode\":\"DEPA\"",
                    StringComparison.Ordinal));
        Assert.NotEqual(arr,depa);
        // Received order is GTIN at 10:00, GTOT at 12:00, then a delayed
        // GTIN event for 11:00. Event receipt order cannot become M5
        // workflow arrival/completion or a predictive ETA.
        await G9Support.Accept(fixtureFactory,source,
            G9Support.EquipmentPayload(
                "g13-gtot-" + Guid.NewGuid().ToString("N")[..12],
                target.Reference,"2026-11-03T12:00:00Z","GTOT"));
        await G9Support.Accept(fixtureFactory,source,
            G9Support.EquipmentPayload(
                "g13-late-" + Guid.NewGuid().ToString("N")[..12],
                target.Reference,"2026-11-03T11:00:00Z","GTIN"));

        await using (var scope = fixtureFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var worker = new TrackingWorkStore(db,new TrackingWorkerSettings());
            var dispositions = new List<TrackingWorkDisposition>();
            for(var i=0;i<5;i++)
            {
                var claim = await worker.ClaimOneAsync(
                    fixture.A,CancellationToken.None);
                Assert.NotNull(claim);
                dispositions.Add(await worker.FinalizeAsync(
                    claim!,CancellationToken.None));
            }
            Assert.Equal(3,dispositions.Count(
                x => x == TrackingWorkDisposition.Processed));
            Assert.Equal(2,dispositions.Count(
                x => x == TrackingWorkDisposition.NeedsReconciliation));
            Assert.Null(await worker.ClaimOneAsync(
                fixture.A,CancellationToken.None));
        }

        Assert.Equal(1L,await G9Support.Count(roles.Admin,
            "SELECT COUNT(*) FROM tracking_inbox_receipts WHERE id=@receipt",
            ("receipt",first.Receipt!.Value)));
        Assert.Equal(1L,await G9Support.Count(roles.Admin,
            "SELECT COUNT(*) FROM tracking_processing_effects WHERE receipt_id=@receipt",
            ("receipt",first.Receipt!.Value)));
        Assert.Equal(2L,await G9Support.Count(roles.Admin,
            "SELECT COUNT(*) FROM tracking_inbox_work_items WHERE organization_id=@org AND state='NeedsReconciliation'",
            ("org",fixture.A)));

        var after=await Report(scoped,fixture.A);
        var latest=await ActiveMemberKeys(scoped,fixture.A,after);
        Assert.True(before.SetEquals(latest),
            "M8 replay and parked M9 events changed membership without M5 workflow facts");
        Assert.Equal(2L,Metric(after,"K01"));
        Assert.Equal(0L,Metric(after,"K03"));
        Assert.Equal(0L,Metric(after,"K04"));
        Assert.Equal(0L,Metric(after,"K05"));
        Assert.Equal(0L,Metric(after,"K06"));
        Console.WriteLine("G13_M8_M9_KPI duplicate_receipt_effect_once=true out_of_order_equipment=2 parked_transport=2 workflow_unchanged=true");
    }
}
