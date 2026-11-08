using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// All role-changing tests must run on an exclusive disposable PostgreSQL server.
// A dedicated GitHub Actions job sets HARBOROPS_G13_EXCLUSIVE_POSTGRES=1.
[CollectionDefinition("M13ExclusiveServer", DisableParallelization = true)]
public sealed class M13ExclusiveServerCollection { }

[AttributeUsage(AttributeTargets.Method)]
public sealed class ExclusiveM13PostgresFactAttribute : FactAttribute
{
    public ExclusiveM13PostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("HARBOROPS_G13_EXCLUSIVE_POSTGRES") != "1")
            Skip = "G13 requires an exclusive disposable PostgreSQL server";
    }
}

[Collection("M13ExclusiveServer")]
public sealed class M13OperationalKpiRuntimeTests
{
    private static string Utc(DateTimeOffset time) =>
        time.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss'Z'",
            System.Globalization.CultureInfo.InvariantCulture);
    private static string Root(Guid org) =>
        $"/api/v1/organizations/{org}/analytics";
    private static string Window(DateTimeOffset now) =>
        "?fromUtc=" + Uri.EscapeDataString(Utc(now.AddDays(-1))) +
        "&beforeUtc=" + Uri.EscapeDataString(Utc(now.AddDays(1))) + "&bucket=day";

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }
    }

    private static async Task<Guid> CreateShipment(
        HttpClient client, string root, Guid origin, Guid destination,
        DateTimeOffset plan, int index)
    {
        var code = "M13-" + Guid.NewGuid().ToString("N")[..14].ToUpperInvariant() +
            "-" + index;
        var created = await Json(await ReferenceDataHttpTests.Send(
            client, HttpMethod.Post, root, new {
                referenceCode = code, originPortId = origin,
                destinationPortId = destination,
                plannedDepartureAtUtc = Utc(plan.AddDays(-2)),
                plannedArrivalAtUtc = Utc(plan)
            }));
        return created.GetProperty("id").GetGuid();
    }

    private static async Task AdvanceToCompleted(
        HttpClient client, string root, Guid id)
    {
        var path = root + "/" + id;
        var transitions = new[] { "Booked", "InTransit", "Arrived", "Completed" };
        for (var i = 0; i < transitions.Length; i++)
        {
            var changed = await Json(await ReferenceDataHttpTests.Send(
                client, HttpMethod.Patch, path,
                new { expectedVersion = i + 1, workflowStatus = transitions[i] }));
            Assert.Equal(transitions[i], changed.GetProperty("workflowStatus").GetString());
        }
    }

    private static long Count(JsonElement overview, string code, string dimension = "") =>
        overview.GetProperty("counts").EnumerateArray()
            .Where(x => x.GetProperty("metricCode").GetString() == code &&
                x.GetProperty("dimension").GetString() == dimension)
            .Sum(x => x.GetProperty("count").GetInt64());

    private static HttpRequestMessage MemberRequest(string org, JsonElement report,
        string code, int limit = 50, string? after = null)
    {
        var suffix = "?metricCode=" + code + "&limit=" + limit;
        if (after is not null) suffix += "&after=" + Uri.EscapeDataString(after);
        var request = new HttpRequestMessage(HttpMethod.Get,
            Root(Guid.Parse(org)) + "/reports/" +
            report.GetProperty("reportId").GetGuid() + "/members" + suffix);
        request.Headers.TryAddWithoutValidation("X-HarborOps-Report",
            report.GetProperty("reportToken").GetString());
        return request;
    }

    [ExclusiveM13PostgresFact]
    public async Task RestrictedRoleReportReconcilesRecordedArrivalAndMemberSnapshots()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var source = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(source);
        using var admin = f.Admin; using var seedManager = f.Manager;
        using var seedAnalyst = f.Analyst;
        var seed = await AssignmentHttpTests.Seed(f);
        using var original = await seedManager.GetAsync(seed.Root + "/" + seed.Shipment);
        var shipment = await original.Content.ReadFromJsonAsync<JsonElement>();
        var origin = shipment.GetProperty("originPortId").GetGuid();
        var destination = shipment.GetProperty("destinationPortId").GetGuid();

        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var manager = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var analyst = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var administrator = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(manager, f.ManagerEmail);
        await ReferenceDataHttpTests.Login(analyst, f.AnalystEmail);
        await ReferenceDataHttpTests.Login(administrator, f.AdminEmail);
        await ReferenceDataHttpTests.Select(manager, f.A);
        await ReferenceDataHttpTests.Select(analyst, f.A);
        await ReferenceDataHttpTests.Select(administrator, f.A);

        var now = DateTimeOffset.UtcNow;
        var first = await CreateShipment(manager, seed.Root, origin, destination,
            now.AddHours(2), 1);
        var late = await CreateShipment(manager, seed.Root, origin, destination,
            now.AddMinutes(-85), 2);
        var tolerated = await CreateShipment(manager, seed.Root, origin, destination,
            now.AddMinutes(-30), 3);
        foreach (var id in new[] { first, late, tolerated })
            await AdvanceToCompleted(manager, seed.Root, id);
        using (var archived = await ReferenceDataHttpTests.Send(
            manager, HttpMethod.Delete, seed.Root + "/" + tolerated,
            new { expectedVersion = 5 }))
            Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);

        var url = Root(f.A);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await administrator.GetAsync(url + "/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await administrator.GetAsync(url + "/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await analyst.GetAsync(Root(f.B) + "/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await manager.GetAsync(url + "/catalog")).StatusCode);

        var report = await Json(await analyst.GetAsync(url + "/overview" + Window(now)));
        Assert.Equal("m13-kpi-v1", report.GetProperty("policyVersion").GetString());
        Assert.Equal(3L, Count(report, "K03"));
        Assert.Equal(0L, Count(report, "K04"));
        Assert.Equal(3L, Count(report, "D05"));
        Assert.Equal(1L, Count(report, "K05"));
        Assert.Equal(2L, Count(report, "K06"));
        Assert.Equal(2L, Count(report, "K01")); // Two original unbooked M6 seed shipments
        Assert.Equal(4L, Count(report, "D01")); // Two current Completed and two Draft
        Assert.Equal(2L, Count(report, "K02", "Completed"));
        Assert.Equal(2L, Count(report, "K02", "Draft"));
        Assert.Equal(2L, Count(report, "D13"));
        Assert.Equal(2L, Count(report, "K14")); // Evaluator has not run on either Completed
        Assert.Equal(2L, Count(report, "Q01"));

        using var pageOneRequest = MemberRequest(f.A.ToString(), report, "K03", 1);
        var pageOne = await Json(await analyst.SendAsync(pageOneRequest));
        Assert.Single(pageOne.GetProperty("items").EnumerateArray());
        var next = pageOne.GetProperty("nextAfter").GetString();
        Assert.False(string.IsNullOrEmpty(next));
        using var pageTwoRequest = MemberRequest(f.A.ToString(), report, "K03", 1, next);
        var pageTwo = await Json(await analyst.SendAsync(pageTwoRequest));
        Assert.Single(pageTwo.GetProperty("items").EnumerateArray());
        Assert.NotEqual(pageOne.GetProperty("items")[0].GetProperty("memberKey").GetGuid(),
            pageTwo.GetProperty("items")[0].GetProperty("memberKey").GetGuid());

        // Same report and token are not valid for another actor, session or org.
        using var swap = MemberRequest(f.A.ToString(), report, "K03");
        using var wrongActor = await manager.SendAsync(swap);
        Assert.Equal(HttpStatusCode.Conflict, wrongActor.StatusCode);
        using var changedToken = MemberRequest(f.A.ToString(), report, "K03");
        changedToken.Headers.Remove("X-HarborOps-Report");
        changedToken.Headers.TryAddWithoutValidation("X-HarborOps-Report", "forged");
        Assert.Equal(HttpStatusCode.Conflict,
            (await analyst.SendAsync(changedToken)).StatusCode);
        using var unrecognized = await analyst.GetAsync(url + "/overview?unexpected=1");
        Assert.Equal(HttpStatusCode.BadRequest, unrecognized.StatusCode);
        using var wrongUtc = await analyst.GetAsync(url +
            "/overview?fromUtc=2026-11-01T00:00:00%2B03:00&beforeUtc=2026-11-03T00:00:00Z");
        Assert.Equal(HttpStatusCode.BadRequest, wrongUtc.StatusCode);

        // A raw API database connection cannot obtain a signed M11 read scope.
        await using var apiDb = new NpgsqlConnection(roles.Api);
        await apiDb.OpenAsync();
        await using var raw = new NpgsqlCommand(
            "SELECT count(*) FROM operational_kpi_reports WHERE organization_id=@org", apiDb);
        raw.Parameters.AddWithValue("org", f.A);
        Assert.Equal(0L, Convert.ToInt64(await raw.ExecuteScalarAsync()));
        await using var unsafeGrant = new NpgsqlCommand(
            "UPDATE operational_kpi_auditor_grants SET active=true", apiDb);
        var denied = await Assert.ThrowsAsync<PostgresException>(
            () => unsafeGrant.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
    }

    [ExclusiveM13PostgresFact]
    public async Task AuditorGrantRevocationDeniesDashboardAndOldReportMembers()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(seedFactory);
        using var seedAdmin = f.Admin;
        using var seedManager = f.Manager;
        using var seedAnalyst = f.Analyst;
        var identity = seedFactory.Services.GetRequiredService<IdentityStore>();
        var tag = Guid.NewGuid().ToString("N");
        var email = "m13-auditor-" + tag + "@example.test";
        const string passphrase = "Synthetic-reference-integration-passphrase-123";
        await identity.ProvisionAsync(email, "M13 Auditor", passphrase, CancellationToken.None);
        var auditorId = (await identity.LoginAsync(email, passphrase, "m13", CancellationToken.None))!.UserId;
        using (var member = await ReferenceDataHttpTests.Send(seedAdmin,
            HttpMethod.Put, $"/api/v1/organizations/{f.A}/memberships/{auditorId}",
            new { role = "Auditor", status = "Active", expectedVersion = (long?)null }))
            Assert.Equal(HttpStatusCode.OK, member.StatusCode);

        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var admin = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var auditor = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var analyst = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(admin, f.AdminEmail);
        await ReferenceDataHttpTests.Login(auditor, email);
        await ReferenceDataHttpTests.Login(analyst, f.AnalystEmail);
        foreach (var client in new[] { admin, auditor, analyst })
            await ReferenceDataHttpTests.Select(client, f.A);
        var url = Root(f.A);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await auditor.GetAsync(url + "/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await auditor.GetAsync(url + "/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await analyst.GetAsync(url + "/grants")).StatusCode);

        using (var denied = await ReferenceDataHttpTests.Send(analyst,
            HttpMethod.Put, url + "/grants/" + auditorId, new { enabled = true }))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var noCsrf = await ReferenceDataHttpTests.Send(admin,
            HttpMethod.Put, url + "/grants/" + auditorId, new { enabled = true },
            csrf: false))
            Assert.Equal(HttpStatusCode.Forbidden, noCsrf.StatusCode);

        using (var granted = await ReferenceDataHttpTests.Send(admin,
            HttpMethod.Put, url + "/grants/" + auditorId, new { enabled = true }))
            Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await auditor.GetAsync(url + "/catalog")).StatusCode);
        var report = await Json(await auditor.GetAsync(url + "/overview"));
        using (var mine = MemberRequest(f.A.ToString(), report, "K01"))
        {
            using var response = await auditor.SendAsync(mine);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using (var revoked = await ReferenceDataHttpTests.Send(admin,
            HttpMethod.Put, url + "/grants/" + auditorId, new { enabled = false }))
            Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await auditor.GetAsync(url + "/catalog")).StatusCode);
        using var previous = MemberRequest(f.A.ToString(), report, "K01");
        using var forbidden = await auditor.SendAsync(previous);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await auditor.GetAsync(url + "/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await analyst.GetAsync(url + "/exports/shipments")).StatusCode == HttpStatusCode.Forbidden
                ? HttpStatusCode.Forbidden : HttpStatusCode.OK); // Analyst remains a separate export role
    }
}