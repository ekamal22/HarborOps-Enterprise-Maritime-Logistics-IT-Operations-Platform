using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

[Collection("M13ExclusiveServer")]
public sealed class G13FilterAcceptanceTests
{
    private static string Utc(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss'Z'",
            System.Globalization.CultureInfo.InvariantCulture);

    private static long Count(JsonElement report, string code) =>
        report.GetProperty("counts").EnumerateArray()
            .Where(x => x.GetProperty("metricCode").GetString() == code)
            .Sum(x => x.GetProperty("count").GetInt64());

    private static async Task<JsonElement> Build(
        HttpClient client, Guid organization, string query)
    {
        using var response = await client.GetAsync(
            $"/api/v1/organizations/{organization}/analytics/overview" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<HashSet<Guid>> Members(
        HttpClient client, Guid organization, JsonElement report)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/organizations/{organization}/analytics/reports/" +
            report.GetProperty("reportId").GetGuid() + "/members?metricCode=K01&limit=100");
        req.Headers.TryAddWithoutValidation("X-HarborOps-Report",
            report.GetProperty("reportToken").GetString());
        using var response = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, result.GetProperty("nextAfter").ValueKind);
        var keys = result.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("memberKey").GetGuid()).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        return keys.ToHashSet();
    }

    [ExclusiveM13PostgresFact]
    public async Task OriginDestinationSourceAndVoyageFiltersAgreeWithMaterializedMembers()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var setupFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(setupFactory);
        using var admin = fixture.Admin; using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var seed = await AssignmentHttpTests.Seed(fixture);
        var url = seed.Root + "/" + seed.Shipment;
        using var detail = await manager.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var data = await detail.Content.ReadFromJsonAsync<JsonElement>();
        var origin = data.GetProperty("originPortId").GetGuid();
        var destination = data.GetProperty("destinationPortId").GetGuid();
        var otherOrigin = Guid.NewGuid();
        using (var port = await ReferenceDataHttpTests.Send(
            manager, HttpMethod.Post,
            $"/api/v1/organizations/{fixture.A}/ports",
            new { referenceCode = "G13-FILTER-" + Guid.NewGuid().ToString("N")[..12],
                name = "Reporting alternate origin", countryCode = "US" }))
        {
            Assert.Equal(HttpStatusCode.Created, port.StatusCode);
            otherOrigin = (await port.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("id").GetGuid();
        }
        Guid alternate;
        using (var created = await ReferenceDataHttpTests.Send(
            analyst, HttpMethod.Post, seed.Root,
            new { referenceCode = "G13-FILTER-S-" + Guid.NewGuid().ToString("N")[..12],
                originPortId = otherOrigin, destinationPortId = destination }))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            alternate = (await created.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("id").GetGuid();
        }
        var now = DateTimeOffset.UtcNow;
        using (var active = await ReferenceDataHttpTests.Send(
            manager, HttpMethod.Post, seed.Root + "/" + seed.Shipment +
            "/voyage-assignments",
            new { voyageId = seed.Voyage, startsAtUtc = Utc(now.AddHours(-1)) }))
            Assert.Equal(HttpStatusCode.Created, active.StatusCode);
        using (var future = await ReferenceDataHttpTests.Send(
            manager, HttpMethod.Post, seed.Root + "/" + seed.OtherShipment +
            "/voyage-assignments",
            new { voyageId = seed.Voyage, startsAtUtc = Utc(now.AddDays(2)) }))
            Assert.Equal(HttpStatusCode.Created, future.StatusCode);

        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var scoped = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(scoped, fixture.AnalystEmail);
        await ReferenceDataHttpTests.Select(scoped, fixture.A);
        using var scopedManager = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(scopedManager, fixture.ManagerEmail);
        await ReferenceDataHttpTests.Select(scopedManager, fixture.A);
        var all = await Build(scoped, fixture.A, "");
        Assert.Equal(3L, Count(all, "K01"));
        Assert.True((await Members(scoped, fixture.A, all))
            .SetEquals(new[] { seed.Shipment, seed.OtherShipment, alternate }));

        // A closed assignment (ended in the past) must never put its shipment into a
        // voyage filtered report, even though the voyage matches.
        await using (var closedDb = new NpgsqlConnection(roles.Admin))
        {
            await closedDb.OpenAsync();
            await using var closed = new NpgsqlCommand("""
                INSERT INTO shipment_voyage_assignments
                    (id,organization_id,shipment_id,voyage_id,starts_at_utc,ends_at_utc,version,
                     created_at_utc,updated_at_utc,created_by_user_id,closed_by_user_id)
                SELECT gen_random_uuid(),s.organization_id,s.id,@voyage,
                    clock_timestamp()-interval '5 hours',clock_timestamp()-interval '4 hours',1,
                    clock_timestamp(),clock_timestamp(),s.changed_by_user_id,s.changed_by_user_id
                FROM shipments s WHERE s.id=@shipment
                """, closedDb);
            closed.Parameters.AddWithValue("voyage", seed.Voyage);
            closed.Parameters.AddWithValue("shipment", alternate);
            Assert.Equal(1, await closed.ExecuteNonQueryAsync());
        }

        var checkIndex = 0;
        async Task Check(string filter, params Guid[] expected)
        {
            // Ten builds in the fixture, at most five for either actor.
            // Do not bypass or weaken the six-build-per-minute rate limit.
            var reader = (++checkIndex % 2 == 1) ? scopedManager : scoped;
            var report = await Build(reader, fixture.A, filter);
            Assert.Equal((long)expected.Length, Count(report, "K01"));
            // The denominator, the total and the member pages all describe one set.
            Assert.Equal((long)expected.Length, Count(report, "D01"));
            Assert.True((await Members(reader, fixture.A, report)).SetEquals(expected),
                "Filtered member set differs from the exact expected shipment IDs");
        }
        await Check("?originPortId=" + origin,
            seed.Shipment, seed.OtherShipment);
        await Check("?originPortId=" + otherOrigin, alternate);
        await Check("?destinationPortId=" + destination,
            seed.Shipment, seed.OtherShipment, alternate);
        await Check("?originPortId=" + origin + "&voyageId=" + seed.Voyage,
            seed.Shipment);
        await Check("?voyageId=" + seed.Voyage, seed.Shipment);
        await Check("?originPortId=" + otherOrigin + "&voyageId=" + seed.Voyage);
        await Check("?originPortId=" + Guid.NewGuid());
        await Check("?recordSource=Synthetic");
        await Check("?recordSource=UserProvided",
            seed.Shipment, seed.OtherShipment, alternate);
        await using var db = new NpgsqlConnection(roles.Admin);
        await db.OpenAsync();
        await using var verify = new NpgsqlCommand("""
            SELECT count(*) FROM shipment_voyage_assignments
            WHERE organization_id=@org AND voyage_id=@voyage
              AND starts_at_utc>clock_timestamp()
            """, db);
        verify.Parameters.AddWithValue("org", fixture.A);
        verify.Parameters.AddWithValue("voyage", seed.Voyage);
        Assert.Equal(1L, Convert.ToInt64(await verify.ExecuteScalarAsync()));
    }

    [ExclusiveM13PostgresFact]
    public async Task MalformedUtcAndForeignFiltersDoNotCreateKpiReports()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(seedFactory);
        using var admin = fixture.Admin; using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var scoped = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(scoped, fixture.AnalystEmail);
        await ReferenceDataHttpTests.Select(scoped, fixture.A);
        using var scopedManager = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(scopedManager, fixture.ManagerEmail);
        await ReferenceDataHttpTests.Select(scopedManager, fixture.A);
        var baseUrl = $"/api/v1/organizations/{fixture.A}/analytics/overview";
        await using var db = new NpgsqlConnection(roles.Admin);
        await db.OpenAsync();
        async Task<long> Saved()
        {
            await using var command = new NpgsqlCommand(
                "SELECT count(*) FROM operational_kpi_reports WHERE organization_id=@org", db);
            command.Parameters.AddWithValue("org", fixture.A);
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        var before = await Saved();
        var attempted = 0;
        foreach (var suffix in new[]
        {
            "?fromUtc=2026-11-09T00:00:00Z",
            "?bucket=month",
            "?fromUtc=2026-11-09T00:00:00Z&beforeUtc=2026-11-09T00:00:00Z",
            "?fromUtc=2026-11-09T00:00:00%2B03%3A00&beforeUtc=2026-11-10T00:00:00Z",
            "?originPortId=not-a-guid", "?voyageId=not-a-guid",
            "?destinationPortId=not-a-guid", "?recordSource=Internal",
            "?originPortId=" + Guid.Empty,
            "?bucket=day&bucket=week",
            "?arbitraryColumn=role"
        })
        {
            var reader = (++attempted % 2 == 0) ? scopedManager : scoped;
            using var rejected = await reader.GetAsync(baseUrl + suffix);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
        Assert.Equal(before, await Saved());
        // Six requests per actor, retaining the real reporting limiter.
        using var foreign = await scopedManager.GetAsync(
            $"/api/v1/organizations/{fixture.B}/analytics/overview");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(before, await Saved());
    }
}
