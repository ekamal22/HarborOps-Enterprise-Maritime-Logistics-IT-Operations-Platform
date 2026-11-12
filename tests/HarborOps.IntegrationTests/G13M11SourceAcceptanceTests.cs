using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class G13M11SourceAcceptanceTests
{
    private static async Task<JsonElement> Overview(HttpClient client,Guid org)
    {
        using var response=await client.GetAsync(
            $"/api/v1/organizations/{org}/analytics/overview");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static long Count(JsonElement report,string code)=>
        report.GetProperty("counts").EnumerateArray()
            .Where(x=>x.GetProperty("metricCode").GetString()==code)
            .Sum(x=>x.GetProperty("count").GetInt64());

    private static async Task<HashSet<Guid>> Members(
        HttpClient client,Guid org,JsonElement report,string metric)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/organizations/{org}/analytics/reports/"+
            report.GetProperty("reportId").GetGuid()+
            "/members?metricCode="+metric+"&limit=100");
        request.Headers.TryAddWithoutValidation("X-HarborOps-Report",
            report.GetProperty("reportToken").GetString());
        using var response=await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var body=await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null,body.GetProperty("nextAfter").ValueKind);
        var keys=body.GetProperty("items").EnumerateArray()
            .Select(x=>x.GetProperty("memberKey").GetGuid()).ToArray();
        Assert.Equal(keys.Length,keys.Distinct().Count());
        return keys.ToHashSet();
    }

    [Fact]
    public async Task ConcurrentManagerDispositionAndReportDoNotDoubleCountOccurrence()
    {
        var s=await M11ExceptionRuntimeTests.Prepare();
        using var factory=s.Factory;using var runtime=s.Runtime;
        using var manager=s.Manager;using var analyst=s.Analyst;
        using var administrator=s.Administrator;
        using var originalAdmin=s.Fixture.Admin;
        using var originalManager=s.Fixture.Manager;
        using var originalAnalyst=s.Fixture.Analyst;
        var shipment=await M11ExceptionRuntimeTests.ScheduledShipment(s);
        var exception=await M11ExceptionRuntimeTests.EvaluateAndFind(s,shipment);
        var path=M11ExceptionRuntimeTests.Collection(s.Fixture.A)+"/"+exception;
        // Two actual HTTP requests use independent authorized principals.
        // The report's repeatable-read transaction must not double count an
        // occurrence whose lifecycle moves Open -> Acknowledged concurrently.
        var reporting=Overview(analyst,s.Fixture.A);
        var acknowledged=M11ExceptionRuntimeTests.SendAction(manager,path,
            new {action="Acknowledge",expectedVersion=1L,
                reason="Concurrent reporting occurrence validation"},
            Guid.NewGuid());
        await Task.WhenAll(reporting,acknowledged);
        using var action=await acknowledged;
        Assert.Equal(HttpStatusCode.OK,action.StatusCode);
        var frozen=await reporting;
        Assert.Equal(1L,Count(frozen,"K11"));
        Assert.Contains(exception,await Members(analyst,s.Fixture.A,frozen,"K11"));
        var latest=await Overview(manager,s.Fixture.A);
        Assert.Equal(1L,Count(latest,"K11"));
        Assert.Contains(exception,await Members(manager,s.Fixture.A,latest,"K11"));
        await using var db=new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        await using var query=new NpgsqlCommand("""
            SELECT COUNT(*) FROM shipment_exception_history
            WHERE organization_id=@org AND exception_id=@exception AND action='Opened'
            """,db);
        query.Parameters.AddWithValue("org",s.Fixture.A);
        query.Parameters.AddWithValue("exception",exception);
        Assert.Equal(1L,Convert.ToInt64(await query.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task SourceCorrectionReevaluationKeepsFrozenRiskAndRefreshesCurrentRisk()
    {
        var s=await M11ExceptionRuntimeTests.Prepare();
        using var factory=s.Factory;using var runtime=s.Runtime;
        using var manager=s.Manager;using var analyst=s.Analyst;
        using var administrator=s.Administrator;
        using var originalAdmin=s.Fixture.Admin;
        using var originalManager=s.Fixture.Manager;
        using var originalAnalyst=s.Fixture.Analyst;
        var shipment=await M11ExceptionRuntimeTests.ScheduledShipment(s);
        var exception=await M11ExceptionRuntimeTests.EvaluateAndFind(s,shipment);
        var before=await Overview(analyst,s.Fixture.A);
        Assert.Equal(1L,Count(before,"K08"));
        var frozen=await Members(analyst,s.Fixture.A,before,"K08");
        Assert.Contains(shipment,frozen);

        var path=$"/api/v1/organizations/{s.Fixture.A}/shipments/{shipment}";
        using(var correction=await ReferenceDataHttpTests.Send(
            s.Fixture.Manager,HttpMethod.Patch,path,new {
                expectedVersion=3L,
                plannedArrivalAtUtc=M11ExceptionRuntimeTests.Utc(
                    DateTimeOffset.UtcNow.AddHours(4))
            }))
            Assert.Equal(HttpStatusCode.OK,correction.StatusCode);
        using(var evaluation=await ReferenceDataHttpTests.Send(
            manager,HttpMethod.Post,path+"/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK,evaluation.StatusCode);

        var current=await Overview(manager,s.Fixture.A);
        Assert.Equal(0L,Count(current,"K08"));
        Assert.Empty(await Members(manager,s.Fixture.A,current,"K08"));
        Assert.Contains(shipment,await Members(analyst,s.Fixture.A,before,"K08"));
        // The repeated evaluation must not recreate a new opening or invent a
        // risk after the accepted source revision has cleared the condition.
        using(var repeat=await ReferenceDataHttpTests.Send(
            manager,HttpMethod.Post,path+"/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK,repeat.StatusCode);
        var stable=await Overview(manager,s.Fixture.A);
        Assert.Equal(0L,Count(stable,"K08"));

        await using var db=new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        await using var count=new NpgsqlCommand("""
            SELECT COUNT(*) FROM shipment_exception_history
            WHERE organization_id=@org AND exception_id=@exception AND action='Opened'
            """,db);
        count.Parameters.AddWithValue("org",s.Fixture.A);
        count.Parameters.AddWithValue("exception",exception);
        Assert.Equal(1L,Convert.ToInt64(await count.ExecuteScalarAsync()));
    }
}
