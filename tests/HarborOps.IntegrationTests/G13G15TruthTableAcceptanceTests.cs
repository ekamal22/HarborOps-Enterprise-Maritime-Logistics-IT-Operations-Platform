using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class G15EventCatalogAcceptanceTests
{
    [Fact]
    public async Task OnlyApprovedM11ActionsCreateDistinctCatalogedOutboxIntents()
    {
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var originalAdmin = s.Fixture.Admin;
        using var originalManager = s.Fixture.Manager;
        using var originalAnalyst = s.Fixture.Analyst;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        var exception = await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        var actions = new[] {
            (Action: "Resolved", Old: "Open", New: "Resolved"),
            (Action: "AutoResolved", Old: "Open", New: "Resolved"),
            (Action: "Reevaluated", Old: "Open", New: "Open"),
            (Action: "InputUnavailable", Old: "Open", New: "Open"),
            (Action: "Acknowledged", Old: "Open", New: "Acknowledged")
        };
        foreach (var action in actions)
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO shipment_exception_history
                    (organization_id,exception_id,action,old_state,new_state,
                     actor_kind,source_fingerprint)
                SELECT e.organization_id,e.id,@action,@old,@next,
                    'System',e.source_fingerprint
                FROM shipment_exceptions e
                WHERE e.id=@exception AND e.organization_id=@org
                """, db);
            insert.Parameters.AddWithValue("action", action.Action);
            insert.Parameters.AddWithValue("old", action.Old);
            insert.Parameters.AddWithValue("next", action.New);
            insert.Parameters.AddWithValue("exception", exception);
            insert.Parameters.AddWithValue("org", s.Fixture.A);
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());
        }

        await using var count = new NpgsqlCommand("""
            SELECT event_type,count(*) FROM m15_notification_outbox
            WHERE organization_id=@org AND exception_id=@exception
            GROUP BY event_type ORDER BY event_type
            """, db);
        count.Parameters.AddWithValue("org", s.Fixture.A);
        count.Parameters.AddWithValue("exception", exception);
        var seen = new Dictionary<string, long>();
        await using (var rows = await count.ExecuteReaderAsync())
            while (await rows.ReadAsync()) seen.Add(rows.GetString(0), rows.GetInt64(1));
        Assert.Equal(3, seen.Count);
        foreach (var approved in new[] {
            "ExceptionOpened", "ExceptionResolved", "ExceptionAutoResolved" })
            Assert.Equal(1L, seen[approved]);
        Assert.Equal(3L, seen.Values.Sum());

        await using var unique = new NpgsqlCommand("""
            SELECT COUNT(*) - COUNT(DISTINCT source_history_id)
            FROM m15_notification_outbox
            WHERE organization_id=@org AND exception_id=@exception
            """, db);
        unique.Parameters.AddWithValue("org", s.Fixture.A);
        unique.Parameters.AddWithValue("exception", exception);
        Assert.Equal(0L, Convert.ToInt64(await unique.ExecuteScalarAsync()));
    }
}

[Collection("M13ExclusiveServer")]
public sealed class G13EmptyDataAcceptanceTests
{
    private static long Count(JsonElement report, string code) =>
        report.GetProperty("counts").EnumerateArray()
            .Where(x => x.GetProperty("metricCode").GetString() == code)
            .Sum(x => x.GetProperty("count").GetInt64());

    [ExclusiveM13PostgresFact]
    public async Task EmptyTenantNeverInventsPercentagesTrendsOrMembers()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(seedFactory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var scoped = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(scoped, fixture.AnalystEmail);
        await ReferenceDataHttpTests.Select(scoped, fixture.A);
        var baseUrl = $"/api/v1/organizations/{fixture.A}/analytics/overview";
        using var response = await scoped.GetAsync(baseUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0L, report.GetProperty("memberCount").GetInt64());
        foreach (var metric in new[] {
            "K01", "K02", "K03", "K04", "K05", "K06", "K07", "K08",
            "K09", "K10", "K11", "K12", "K13", "K14", "D05", "D13" })
            Assert.Equal(0L, Count(report, metric));

        var token = report.GetProperty("reportToken").GetString();
        var id = report.GetProperty("reportId").GetGuid();
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/organizations/{fixture.A}/analytics/reports/{id}/members" +
            "?metricCode=K01&limit=100");
        request.Headers.TryAddWithoutValidation("X-HarborOps-Report", token);
        using var emptyPage = await scoped.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, emptyPage.StatusCode);
        var page = await emptyPage.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(page.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("nextAfter").ValueKind);
    }

    [ExclusiveM13PostgresFact]
    public async Task DraftOnlyTenantHasNoFalseCompletedArrivalDenominator()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(seedFactory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await AssignmentHttpTests.Seed(fixture);
        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var scoped = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(scoped, fixture.AnalystEmail);
        await ReferenceDataHttpTests.Select(scoped, fixture.A);
        using var response = await scoped.GetAsync(
            $"/api/v1/organizations/{fixture.A}/analytics/overview");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2L, Count(report, "K01"));
        Assert.Equal(0L, Count(report, "K03"));
        Assert.Equal(0L, Count(report, "D05"));
        Assert.Equal(0L, Count(report, "K05"));
        Assert.Equal(0L, Count(report, "K06"));
        Assert.Equal(0L, Count(report, "K12"));
    }
}
