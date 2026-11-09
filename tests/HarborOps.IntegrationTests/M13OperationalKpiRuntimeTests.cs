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
        using var response = await ReferenceDataHttpTests.Send(
            client, HttpMethod.Post, root, new {
                referenceCode = code, originPortId = origin,
                destinationPortId = destination,
                plannedDepartureAtUtc = Utc(plan.AddDays(-2)),
                plannedArrivalAtUtc = Utc(plan)
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
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
    public async Task RealM11OverdueEvaluationProducesDistinctDashboardRisk()
    {
        var setup = await M11ExceptionRuntimeTests.Prepare();
        using var migrator = setup.Factory;
        using var api = setup.Runtime;
        using var seedManager = setup.Fixture.Manager;
        using var seedAdmin = setup.Fixture.Admin;
        using var seedAnalyst = setup.Fixture.Analyst;
        using var manager = setup.Manager;
        using var analyst = setup.Analyst;
        using var admin = setup.Administrator;

        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(setup);
        var exceptionId = await M11ExceptionRuntimeTests.EvaluateAndFind(setup, shipment);
        var dashboard = Root(setup.Fixture.A);
        var result = await Json(await manager.GetAsync(dashboard + "/overview"));
        Assert.Equal(1, Count(result, "K08"));
        Assert.Equal(0, Count(result, "K07"));
        Assert.Equal(0, Count(result, "K09"));
        Assert.Equal(1, Count(result, "D08"));
        Assert.True(Count(result, "K11") >= 1);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await admin.GetAsync(dashboard + "/catalog")).StatusCode);

        using var request = MemberRequest(setup.Fixture.A.ToString(), result, "K08");
        var rows = await Json(await manager.SendAsync(request));
        Assert.Contains(rows.GetProperty("items").EnumerateArray(),
            x => x.GetProperty("shipmentId").GetGuid() == shipment);
        using var occurrences = MemberRequest(setup.Fixture.A.ToString(), result, "K11");
        var opened = await Json(await manager.SendAsync(occurrences));
        Assert.Contains(opened.GetProperty("items").EnumerateArray(),
            x => x.GetProperty("exceptionId").GetGuid() == exceptionId);
        Assert.NotEqual(result.GetProperty("reportId").GetGuid(), Guid.Empty);
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
        var tag = Guid.NewGuid().ToString("N");
        var email = "m13-auditor-" + tag + "@example.test";
        const string passphrase = "Synthetic-reference-integration-passphrase-123";
        Guid auditorId;
        await using (var scope = seedFactory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
            await identity.ProvisionAsync(email, "M13 Auditor", passphrase, CancellationToken.None);
            auditorId = (await identity.LoginAsync(email, passphrase, "m13",
                CancellationToken.None))!.UserId;
        }
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

        // A role change increments membership version and must invalidate the
        // previous grant even if the role later returns to Auditor.
        using (var changed = await ReferenceDataHttpTests.Send(seedAdmin,
            HttpMethod.Put, $"/api/v1/organizations/{f.A}/memberships/{auditorId}",
            new { role = "Support", status = "Active", expectedVersion = 1L }))
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using (var restored = await ReferenceDataHttpTests.Send(seedAdmin,
            HttpMethod.Put, $"/api/v1/organizations/{f.A}/memberships/{auditorId}",
            new { role = "Auditor", status = "Active", expectedVersion = 2L }))
            Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await auditor.GetAsync(url + "/catalog")).StatusCode);
        using (var renewed = await ReferenceDataHttpTests.Send(admin,
            HttpMethod.Put, url + "/grants/" + auditorId, new { enabled = true }))
            Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await auditor.GetAsync(url + "/catalog")).StatusCode);

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
    }

    private static async Task<Guid> G13Insert(
        NpgsqlConnection db, Guid org, Guid origin, Guid destination,
        Guid actor, DateTimeOffset planned, string prefix)
    {
        var id = Guid.NewGuid();
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO shipments (
              id,organization_id,reference_code,origin_port_id,destination_port_id,
              planned_departure_at_utc,planned_arrival_at_utc,workflow_status,
              record_status,source_kind,version,created_at_utc,updated_at_utc,
              changed_by_user_id)
            VALUES (@id,@org,@code,@origin,@destination,@departure,@arrival,
              'Draft','Active','Synthetic',1,clock_timestamp(),clock_timestamp(),@actor)
            """, db);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("org", org);
        cmd.Parameters.AddWithValue("code", prefix + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant());
        cmd.Parameters.AddWithValue("origin", origin);
        cmd.Parameters.AddWithValue("destination", destination);
        cmd.Parameters.AddWithValue("departure", planned.AddDays(-2));
        cmd.Parameters.AddWithValue("arrival", planned);
        cmd.Parameters.AddWithValue("actor", actor);
        Assert.Equal(1, await cmd.ExecuteNonQueryAsync());
        return id;
    }

    private static async Task G13Transition(
        NpgsqlConnection db, Guid shipment, Guid actor, string status)
    {
        await using var cmd = new NpgsqlCommand("""
            UPDATE shipments SET workflow_status=@status,version=version+1,
              updated_at_utc=clock_timestamp(),changed_by_user_id=@actor
            WHERE id=@shipment
            """, db);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("actor", actor);
        cmd.Parameters.AddWithValue("shipment", shipment);
        Assert.Equal(1, await cmd.ExecuteNonQueryAsync());
    }

    private static async Task G13Complete(
        NpgsqlConnection db, Guid shipment, Guid actor)
    {
        foreach (var status in new[] { "Booked", "InTransit", "Arrived", "Completed" })
            await G13Transition(db, shipment, actor, status);
    }

    private static async Task<HashSet<Guid>> G13MemberIds(
        HttpClient client, Guid org, JsonElement overview, string metric)
    {
        using var request = MemberRequest(org.ToString(), overview, metric);
        var response = await Json(await client.SendAsync(request));
        Assert.Equal(JsonValueKind.Null, response.GetProperty("nextAfter").ValueKind);
        return response.GetProperty("items").EnumerateArray()
            .Select(row => row.GetProperty("memberKey").GetGuid()).ToHashSet();
    }

    private static async Task<HashSet<Guid>> G13StoredIds(
        NpgsqlConnection db, Guid org, JsonElement report, string metric)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT member_key FROM operational_kpi_members
            WHERE organization_id=@org AND report_id=@report AND metric_code=@metric
            """, db);
        cmd.Parameters.AddWithValue("org", org);
        cmd.Parameters.AddWithValue("report", report.GetProperty("reportId").GetGuid());
        cmd.Parameters.AddWithValue("metric", metric);
        var items = new HashSet<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            Assert.True(items.Add(reader.GetGuid(0)), "Duplicate report member ID");
        return items;
    }

    [ExclusiveM13PostgresFact]
    public async Task TwelveShipmentsAndTwoForeignDecoysReconcileExactMembers()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var setupFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(setupFactory);
        using var seedAdmin = fixture.Admin;
        using var seedManager = fixture.Manager;
        using var seedAnalyst = fixture.Analyst;
        var seed = await AssignmentHttpTests.Seed(fixture);
        using var seedResponse = await seedManager.GetAsync(seed.Root + "/" + seed.Shipment);
        var rootShipment = await seedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var origin = rootShipment.GetProperty("originPortId").GetGuid();
        var destination = rootShipment.GetProperty("destinationPortId").GetGuid();

        await using var db = new NpgsqlConnection(roles.Admin);
        await db.OpenAsync();
        var actor = await new NpgsqlCommand(
            $"SELECT changed_by_user_id FROM shipments WHERE id='{seed.Shipment}'", db)
            .ExecuteScalarAsync();
        var actorId = Assert.IsType<Guid>(actor);
        // A fresh B tenant has its own ports, user and two Draft shipments.
        var bOrigin = Guid.NewGuid();
        var bDestination = Guid.NewGuid();
        Guid bActor;
        await using (var bUser = new NpgsqlCommand("""
            SELECT user_id FROM organization_memberships
            WHERE organization_id=@org AND role='Administrator' AND status='Active'
            """, db))
        {
            bUser.Parameters.AddWithValue("org", fixture.B);
            bActor = Assert.IsType<Guid>(await bUser.ExecuteScalarAsync());
        }
        foreach (var (id, code) in new[] {
            (bOrigin, "M13-B-PORT-A"), (bDestination, "M13-B-PORT-B") })
        {
            await using var port = new NpgsqlCommand("""
                INSERT INTO ports
                  (id,organization_id,reference_code,name,country_code,status,
                    source_kind,version,created_at_utc,updated_at_utc)
                VALUES (@id,@org,@code,'M13 decoy port','US','Active',
                    'Synthetic',1,clock_timestamp(),clock_timestamp())
                """, db);
            port.Parameters.AddWithValue("id", id);
            port.Parameters.AddWithValue("org", fixture.B);
            port.Parameters.AddWithValue("code", code);
            Assert.Equal(1, await port.ExecuteNonQueryAsync());
        }
        var now = DateTimeOffset.UtcNow;
        var foreign = new List<Guid>();
        for (var i = 0; i < 2; i++)
            foreign.Add(await G13Insert(db, fixture.B, bOrigin, bDestination,
                bActor, now.AddDays(3), "M13B-"));

        // The first completion predates the requested reporting window.
        var early = await G13Insert(db, fixture.A, origin, destination,
            actorId, now.AddHours(2), "M13A-");
        await G13Complete(db, early, actorId);
        DateTimeOffset earlyCompleted;
        await using (var firstEvent = new NpgsqlCommand("""
            SELECT changed_at_utc FROM shipment_status_changes
            WHERE organization_id=@org AND shipment_id=@shipment
              AND to_status='Completed'
            """, db))
        {
            firstEvent.Parameters.AddWithValue("org", fixture.A);
            firstEvent.Parameters.AddWithValue("shipment", early);
            earlyCompleted = new DateTimeOffset(
                Assert.IsType<DateTime>(await firstEvent.ExecuteScalarAsync()), TimeSpan.Zero);
        }
        await Task.Delay(1250);
        var from = earlyCompleted.AddSeconds(1);
        var first = await G13Insert(db, fixture.A, origin, destination,
            actorId, now.AddHours(2), "M13A-");
        var late = await G13Insert(db, fixture.A, origin, destination,
            actorId, now.AddMinutes(-85), "M13A-");
        var tolerated = await G13Insert(db, fixture.A, origin, destination,
            actorId, now.AddMinutes(-30), "M13A-");
        foreach (var completed in new[] { first, late, tolerated })
            await G13Complete(db, completed, actorId);

        await using (var archive = new NpgsqlCommand("""
            UPDATE shipments SET record_status='Archived', archived_at_utc=clock_timestamp(),
                version=version+1,updated_at_utc=clock_timestamp(),changed_by_user_id=@actor
            WHERE id=@id
            """, db))
        {
            archive.Parameters.AddWithValue("actor", actorId);
            archive.Parameters.AddWithValue("id", tolerated);
            Assert.Equal(1, await archive.ExecuteNonQueryAsync());
        }
        var cancelled = await G13Insert(db, fixture.A, origin, destination,
            actorId, now.AddHours(2), "M13A-");
        await G13Transition(db, cancelled, actorId, "Cancelled");
        var booked = new List<Guid> { seed.Shipment };
        await G13Transition(db, seed.Shipment, actorId, "Booked");
        for (var i = 0; i < 5; i++)
        {
            var id = await G13Insert(db, fixture.A, origin, destination,
                actorId, now.AddDays(3), "M13A-");
            await G13Transition(db, id, actorId, "Booked");
            booked.Add(id);
        }

        // Twelve A shipments (including one archived), two B decoys.
        await using (var count = new NpgsqlCommand("""
            SELECT COUNT(*) FROM shipments WHERE organization_id=@org
            """, db))
        {
            count.Parameters.AddWithValue("org", fixture.A);
            Assert.Equal(12L, Convert.ToInt64(await count.ExecuteScalarAsync()));
            count.Parameters["org"].Value = fixture.B;
            Assert.Equal(2L, Convert.ToInt64(await count.ExecuteScalarAsync()));
        }

        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var analyst = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(analyst, fixture.AnalystEmail);
        await ReferenceDataHttpTests.Select(analyst, fixture.A);
        var filter = "?fromUtc=" + Uri.EscapeDataString(Utc(from)) +
            "&beforeUtc=" + Uri.EscapeDataString(Utc(DateTimeOffset.UtcNow.AddHours(2))) +
            "&bucket=day";
        var report = await Json(await analyst.GetAsync(Root(fixture.A) + "/overview" + filter));
        Assert.Equal(11L, Count(report, "D01"));
        Assert.Equal(7L, Count(report, "K01"));
        Assert.Equal(6L, Count(report, "K02", "Booked"));
        Assert.Equal(1L, Count(report, "K02", "Draft"));
        Assert.Equal(3L, Count(report, "K02", "Completed"));
        Assert.Equal(1L, Count(report, "K02", "Cancelled"));
        Assert.Equal(3L, Count(report, "K03"));
        Assert.Equal(1L, Count(report, "K04"));
        Assert.Equal(3L, Count(report, "D05"));
        Assert.Equal(1L, Count(report, "K05"));
        Assert.Equal(2L, Count(report, "K06"));
        Assert.Equal(9L, Count(report, "D13"));
        Assert.Equal(9L, Count(report, "K14"));
        Assert.Equal(9L, Count(report, "Q01"));
        Assert.Equal(0L, Count(report, "K13"));

        Assert.True((await G13MemberIds(analyst, fixture.A, report, "K03"))
            .SetEquals(new[] { first, late, tolerated }));
        Assert.True((await G13MemberIds(analyst, fixture.A, report, "K04"))
            .SetEquals(new[] { cancelled }));
        Assert.True((await G13StoredIds(db, fixture.A, report, "D05"))
            .SetEquals(new[] { first, late, tolerated }));
        Assert.True((await G13MemberIds(analyst, fixture.A, report, "K05"))
            .SetEquals(new[] { first }));
        Assert.True((await G13MemberIds(analyst, fixture.A, report, "K06"))
            .SetEquals(new[] { first, tolerated }));
        Assert.True((await G13MemberIds(analyst, fixture.A, report, "K01"))
            .SetEquals(booked.Append(seed.OtherShipment)));
        Assert.True((await G13StoredIds(db, fixture.A, report, "D13"))
            .SetEquals(booked.Concat(new[] { early, first, late })));
        Assert.True((await G13StoredIds(db, fixture.A, report, "Q01"))
            .SetEquals(booked.Concat(new[] { early, first, late })));
        foreach (var metric in new[] { "K01", "K03", "K05", "K14" })
            Assert.DoesNotContain((await G13MemberIds(analyst, fixture.A, report, metric)),
                id => foreign.Contains(id));
        Assert.Equal(HttpStatusCode.NotFound,
            (await analyst.GetAsync(Root(fixture.B) + "/overview" + filter)).StatusCode);
    }


    [ExclusiveM13PostgresFact]
    public async Task ForgedTokensUnsafeQueriesAndDirectSqlCannotDiscloseReports()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var fixtureFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(fixtureFactory);
        using var seedAdmin = f.Admin;
        using var seedManager = f.Manager;
        using var seedAnalyst = f.Analyst;
        var seed = await AssignmentHttpTests.Seed(f);
        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var analyst = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var secondSession = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var administrator = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(analyst, f.AnalystEmail);
        await ReferenceDataHttpTests.Login(secondSession, f.AnalystEmail);
        await ReferenceDataHttpTests.Login(administrator, f.AdminEmail);
        foreach (var client in new[] { analyst, secondSession, administrator })
            await ReferenceDataHttpTests.Select(client, f.A);
        var endpoint = Root(f.A);
        var report = await Json(await analyst.GetAsync(endpoint + "/overview"));
        var token = report.GetProperty("reportToken").GetString();
        Assert.False(string.IsNullOrEmpty(token));
        Assert.NotEqual(Guid.Empty, report.GetProperty("reportId").GetGuid());

        using (var other = MemberRequest(f.A.ToString(), report, "K01"))
            Assert.Equal(HttpStatusCode.Conflict,
                (await secondSession.SendAsync(other)).StatusCode);
        using (var denied = MemberRequest(f.A.ToString(), report, "K01"))
            Assert.Equal(HttpStatusCode.Conflict,
                (await administrator.SendAsync(denied)).StatusCode);
        using (var forged = MemberRequest(f.A.ToString(), report, "K01"))
        {
            forged.Headers.Remove("X-HarborOps-Report");
            forged.Headers.TryAddWithoutValidation("X-HarborOps-Report", token![..^2] + "xx");
            Assert.Equal(HttpStatusCode.Conflict,
                (await analyst.SendAsync(forged)).StatusCode);
        }
        using (var wrong = MemberRequest(f.A.ToString(), report, "D05"))
            Assert.Equal(HttpStatusCode.BadRequest,
                (await analyst.SendAsync(wrong)).StatusCode);
        using (var invalid = MemberRequest(f.A.ToString(), report, "K99"))
            Assert.Equal(HttpStatusCode.BadRequest,
                (await analyst.SendAsync(invalid)).StatusCode);
        using (var wrongDim = new HttpRequestMessage(HttpMethod.Get,
            endpoint + "/reports/" + report.GetProperty("reportId").GetGuid() +
                "/members?metricCode=K01&dimension=Cancelled"))
        {
            wrongDim.Headers.TryAddWithoutValidation("X-HarborOps-Report", token);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await analyst.SendAsync(wrongDim)).StatusCode);
        }
        foreach (var suffix in new[] {
            "?metricCode=K01&limit=101",
            "?metricCode=K01&limit=0",
            "?metricCode=K01&after=not-a-uuid",
            "?metricCode=K01&metricCode=K02",
            "?metricCode=K01%27%3BDROP%20TABLE%20shipments%3B--",
            "?metricCode=K12&dimension=Injected"
        })
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                endpoint + "/reports/" + report.GetProperty("reportId").GetGuid() +
                "/members" + suffix);
            req.Headers.TryAddWithoutValidation("X-HarborOps-Report", token);
            using var response = await analyst.SendAsync(req);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound,
            (await analyst.GetAsync(Root(f.B) + "/overview")).StatusCode);

        // Real restricted database identity without M11 signed read scope
        // must not see the materialized report, even knowing its exact UUID.
        await using (var db = new NpgsqlConnection(roles.Api))
        {
            await db.OpenAsync();
            foreach (var table in new[] {
                "operational_kpi_reports", "operational_kpi_members" })
            {
                await using var count = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM " + table + " WHERE organization_id=@org", db);
                count.Parameters.AddWithValue("org", f.A);
                Assert.Equal(0L, Convert.ToInt64(await count.ExecuteScalarAsync()));
            }
            await using var directUpdate = new NpgsqlCommand(
                "UPDATE operational_kpi_reports SET policy_version='m13-kpi-v1'", db);
            var failure = await Assert.ThrowsAsync<PostgresException>(
                () => directUpdate.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, failure.SqlState);
        }

        // A committed report can persist as a row but must become unusable
        // immediately after its SQL-backed expiry boundary.
        await Task.Delay(1200);
        await using (var adminDb = new NpgsqlConnection(roles.Admin))
        {
            await adminDb.OpenAsync();
            await using var expire = new NpgsqlCommand("""
                UPDATE operational_kpi_reports
                SET expires_at_utc=created_at_utc + interval '1 microsecond'
                WHERE id=@report AND organization_id=@org
                """, adminDb);
            expire.Parameters.AddWithValue("report", report.GetProperty("reportId").GetGuid());
            expire.Parameters.AddWithValue("org", f.A);
            Assert.Equal(1, await expire.ExecuteNonQueryAsync());
        }
        using (var expired = MemberRequest(f.A.ToString(), report, "K01"))
            Assert.Equal(HttpStatusCode.Conflict,
                (await analyst.SendAsync(expired)).StatusCode);
        var fresh = await Json(await analyst.GetAsync(endpoint + "/overview"));
        Assert.NotEqual(report.GetProperty("reportId").GetGuid(),
            fresh.GetProperty("reportId").GetGuid());
    }


    [ExclusiveM13PostgresFact]
    public async Task CommittedReportMembersRemainStableAcrossLaterWorkflowChanges()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var source = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(source);
        using var admin = f.Admin;
        using var seedManager = f.Manager;
        using var seedAnalyst = f.Analyst;
        var seed = await AssignmentHttpTests.Seed(f);
        using var baseResponse = await seedManager.GetAsync(seed.Root + "/" + seed.Shipment);
        var original = await baseResponse.Content.ReadFromJsonAsync<JsonElement>();
        var origin = original.GetProperty("originPortId").GetGuid();
        var destination = original.GetProperty("destinationPortId").GetGuid();

        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var manager = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(manager, f.ManagerEmail);
        await ReferenceDataHttpTests.Select(manager, f.A);
        var newShipment = await CreateShipment(manager, seed.Root,
            origin, destination, DateTimeOffset.UtcNow.AddDays(2), 81);
        var period = Window(DateTimeOffset.UtcNow);
        var first = await Json(await manager.GetAsync(Root(f.A) + "/overview" + period));
        var initialMembers = await G13MemberIds(manager, f.A, first, "K01");
        Assert.Contains(newShipment, initialMembers);

        await AdvanceToCompleted(manager, seed.Root, newShipment);
        var frozenMembers = await G13MemberIds(manager, f.A, first, "K01");
        Assert.True(initialMembers.SetEquals(frozenMembers),
            "Committed member snapshots must not drift with later workflow state");
        var second = await Json(await manager.GetAsync(Root(f.A) + "/overview" + period));
        var laterMembers = await G13MemberIds(manager, f.A, second, "K01");
        Assert.DoesNotContain(newShipment, laterMembers);
        Assert.Equal(initialMembers.Count - 1, laterMembers.Count);
        Assert.NotEqual(first.GetProperty("reportId").GetGuid(),
            second.GetProperty("reportId").GetGuid());
        Console.WriteLine("G13_S15 snapshot_frozen=true later_report_current=true");
    }

    [ExclusiveM13PostgresFact]
    public async Task ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var factory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var seedAdmin = fixture.Admin;
        using var seedManager = fixture.Manager;
        using var seedAnalyst = fixture.Analyst;
        var seed = await AssignmentHttpTests.Seed(fixture);
        using var seedResponse = await seedManager.GetAsync(seed.Root + "/" + seed.Shipment);
        var shipmentJson = await seedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var origin = shipmentJson.GetProperty("originPortId").GetGuid();
        var destination = shipmentJson.GetProperty("destinationPortId").GetGuid();
        await using var db = new NpgsqlConnection(roles.Admin);
        await db.OpenAsync();
        await using var actorCmd = new NpgsqlCommand(
            "SELECT changed_by_user_id FROM shipments WHERE id=@id", db);
        actorCmd.Parameters.AddWithValue("id", seed.Shipment);
        var actor = Assert.IsType<Guid>(await actorCmd.ExecuteScalarAsync());

        var now = DateTimeOffset.UtcNow;
        var ids = new Dictionary<int, Guid>
        {
            [6] = seed.OtherShipment,
            [7] = seed.Shipment
        };
        foreach (var i in new[] { 1, 2, 3, 4, 5, 8, 9, 10, 11, 12 })
        {
            var planned = i switch
            {
                1 => now.AddHours(1),
                2 => now.AddMinutes(-85),
                3 => now.AddMinutes(-30),
                _ => now.AddDays(3)
            };
            ids[i] = await G13Insert(db, fixture.A, origin, destination,
                actor, planned, "G13-REFERENCE-");
        }

        // S04 has the first Completed fact before the reporting window.
        await G13Complete(db, ids[4], actor);
        DateTimeOffset beforeWindow;
        await using (var history = new NpgsqlCommand("""
            SELECT changed_at_utc FROM shipment_status_changes
            WHERE shipment_id=@id AND to_status='Completed'
            ORDER BY changed_at_utc LIMIT 1
            """, db))
        {
            history.Parameters.AddWithValue("id", ids[4]);
            beforeWindow = new DateTimeOffset(
                Assert.IsType<DateTime>(await history.ExecuteScalarAsync()), TimeSpan.Zero);
        }
        await Task.Delay(1250);
        var from = beforeWindow.AddSeconds(1);
        foreach (var i in new[] { 1, 2, 3 })
            await G13Complete(db, ids[i], actor);
        await G13Transition(db, ids[5], actor, "Cancelled");
        foreach (var i in new[] { 7, 11 })
            await G13Transition(db, ids[i], actor, "Booked");
        foreach (var i in new[] { 8, 9, 12 })
        {
            await G13Transition(db, ids[i], actor, "Booked");
            await G13Transition(db, ids[i], actor, "InTransit");
        }
        foreach (var status in new[] { "Booked", "InTransit", "Arrived" })
            await G13Transition(db, ids[10], actor, status);
        await using (var archive = new NpgsqlCommand("""
            UPDATE shipments SET record_status='Archived',archived_at_utc=clock_timestamp(),
                version=version+1,updated_at_utc=clock_timestamp(),changed_by_user_id=@actor
            WHERE id=@id
            """, db))
        {
            archive.Parameters.AddWithValue("actor", actor);
            archive.Parameters.AddWithValue("id", ids[3]);
            Assert.Equal(1, await archive.ExecuteNonQueryAsync());
        }

        // Inject accepted-version M11 evaluation facts. Each metric is compared
        // against its independently constructed member set, not its displayed total.
        await using (var projection = new NpgsqlCommand("""
            INSERT INTO shipment_tracking_projections (
                shipment_id,organization_id,observed_state,input_revision,
                correction_generation,input_digest,policy_version)
            VALUES (@shipment,@org,'Conflict',0,0,repeat('a',64),'m10-equipment-v2')
            """, db))
        {
            projection.Parameters.AddWithValue("shipment", ids[9]);
            projection.Parameters.AddWithValue("org", fixture.A);
            Assert.Equal(1, await projection.ExecuteNonQueryAsync());
        }
        var risk = new Dictionary<int, string>
        {
            [2] = "RecordedArrivalLate",
            [7] = "ArrivalScheduleOverdue",
            [8] = "DepartureSlippageRisk",
            [9] = "EquipmentConflict"
        };
        foreach (var i in new[] { 1, 2, 4, 7, 8, 9, 10, 11 })
        foreach (var rule in new[] {
            "RecordedArrivalLate", "ArrivalScheduleOverdue",
            "DepartureSlippageRisk", "EquipmentConflict" })
        {
            await using var eval = new NpgsqlCommand("""
                INSERT INTO shipment_exception_evaluations (
                    organization_id,shipment_id,rule_code,rule_policy_version,
                    condition_state,source_fingerprint,shipment_version,
                    tracking_input_revision,correction_generation)
                SELECT organization_id,id,@rule,'m11-rules-v1',@state,
                    repeat('a',64),version,0,0 FROM shipments WHERE id=@shipment
                """, db);
            eval.Parameters.AddWithValue("rule", rule);
            eval.Parameters.AddWithValue("state",
                risk.TryGetValue(i, out var active) && active == rule ? "Active" : "Inactive");
            eval.Parameters.AddWithValue("shipment", ids[i]);
            Assert.Equal(1, await eval.ExecuteNonQueryAsync());
        }

        var occurrences = new Dictionary<int, Guid>();
        foreach (var i in new[] { 2, 7, 8, 9 })
        {
            var id = Guid.NewGuid();
            var life = i == 2 ? "Resolved" : i == 7 ? "Acknowledged" : "Open";
            await using (var exception = new NpgsqlCommand("""
                INSERT INTO shipment_exceptions (
                    id,organization_id,shipment_id,rule_code,occurrence,
                    condition_state,lifecycle_state,severity,confidence,reason_code,
                    source_fingerprint,shipment_version,tracking_input_revision,
                    correction_generation,resolved_at_utc,evidence)
                SELECT @id,organization_id,id,@rule,1,'Active',@life,'Warning',
                    'ScheduleOnly','fixture_observation',repeat('a',64),
                    version,0,0,CASE WHEN @life='Resolved' THEN clock_timestamp()
                        ELSE NULL END,'{}'::jsonb
                FROM shipments WHERE id=@shipment
                """, db))
            {
                exception.Parameters.AddWithValue("id", id);
                exception.Parameters.AddWithValue("rule", risk[i]);
                exception.Parameters.AddWithValue("life", life);
                exception.Parameters.AddWithValue("shipment", ids[i]);
                Assert.Equal(1, await exception.ExecuteNonQueryAsync());
            }
            await using (var history = new NpgsqlCommand("""
                INSERT INTO shipment_exception_history (
                    organization_id,exception_id,action,old_state,new_state,
                    actor_kind,source_fingerprint)
                VALUES (@org,@id,'Opened',NULL,'Open','System',repeat('a',64))
                """, db))
            {
                history.Parameters.AddWithValue("org", fixture.A);
                history.Parameters.AddWithValue("id", id);
                Assert.Equal(1, await history.ExecuteNonQueryAsync());
            }
            occurrences[i] = id;
        }

        // The other tenant contains one in-window completion and one booked
        // decoy. Both must be absent from every tenant-A member set.
        var foreignOrigin = Guid.NewGuid();
        var foreignDestination = Guid.NewGuid();
        foreach (var (id, code) in new[] {
            (foreignOrigin, "G13-ORACLE-B-ORIGIN"),
            (foreignDestination, "G13-ORACLE-B-DEST") })
        {
            await using var port = new NpgsqlCommand("""
                INSERT INTO ports (id,organization_id,reference_code,name,country_code,
                    status,source_kind,version,created_at_utc,updated_at_utc)
                VALUES (@id,@org,@code,'Foreign oracle port','US',
                    'Active','Synthetic',1,clock_timestamp(),clock_timestamp())
                """, db);
            port.Parameters.AddWithValue("id", id);
            port.Parameters.AddWithValue("org", fixture.B);
            port.Parameters.AddWithValue("code", code);
            Assert.Equal(1, await port.ExecuteNonQueryAsync());
        }
        await using var foreignActorCmd = new NpgsqlCommand("""
            SELECT user_id FROM organization_memberships
            WHERE organization_id=@org AND role='Administrator' AND status='Active'
            LIMIT 1
            """, db);
        foreignActorCmd.Parameters.AddWithValue("org", fixture.B);
        var foreignActor = Assert.IsType<Guid>(await foreignActorCmd.ExecuteScalarAsync());
        var foreignCompleted = await G13Insert(db, fixture.B, foreignOrigin,
            foreignDestination, foreignActor, now.AddHours(1), "G13-B-");
        var foreignBooked = await G13Insert(db, fixture.B, foreignOrigin,
            foreignDestination, foreignActor, now.AddDays(-1), "G13-B-");
        await G13Complete(db, foreignCompleted, foreignActor);
        await G13Transition(db, foreignBooked, foreignActor, "Booked");

        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var analyst = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(analyst, fixture.AnalystEmail);
        await ReferenceDataHttpTests.Select(analyst, fixture.A);
        var window = "?fromUtc=" + Uri.EscapeDataString(Utc(from)) +
            "&beforeUtc=" + Uri.EscapeDataString(Utc(DateTimeOffset.UtcNow.AddHours(2))) +
            "&bucket=day";
        var report = await Json(await analyst.GetAsync(Root(fixture.A) + "/overview" + window));
        Assert.Equal(7L, Count(report, "K01"));
        Assert.Equal(11L, Count(report, "D01"));
        foreach (var (dimension, expected) in new[] {
            ("Draft", 1L), ("Booked", 2L), ("InTransit", 3L),
            ("Arrived", 1L), ("Completed", 3L), ("Cancelled", 1L) })
            Assert.Equal(expected, Count(report, "K02", dimension));
        foreach (var (metric, count) in new[] {
            ("K03", 3L), ("K04", 1L), ("D05", 3L), ("K05", 1L),
            ("K06", 2L), ("K07", 1L), ("K08", 1L), ("K09", 1L),
            ("K10", 1L), ("K11", 3L), ("K13", 8L),
            ("D13", 9L), ("K14", 1L), ("Q01", 1L) })
            Assert.Equal(count, Count(report, metric));

        var expectedMembers = new Dictionary<string, Guid[]>
        {
            ["K01"] = new[] { ids[6], ids[7], ids[8], ids[9], ids[10], ids[11], ids[12] },
            ["K03"] = new[] { ids[1], ids[2], ids[3] },
            ["K04"] = new[] { ids[5] },
            ["D05"] = new[] { ids[1], ids[2], ids[3] },
            ["K05"] = new[] { ids[1] },
            ["K06"] = new[] { ids[1], ids[3] },
            ["K07"] = new[] { ids[2] },
            ["K08"] = new[] { ids[7] },
            ["K09"] = new[] { ids[8] },
            ["K10"] = new[] { ids[9] },
            ["K11"] = new[] { occurrences[7], occurrences[8], occurrences[9] },
            ["K12"] = occurrences.Values.ToArray(),
            ["K13"] = new[] { ids[1], ids[2], ids[4], ids[7], ids[8], ids[9], ids[10], ids[11] },
            ["K14"] = new[] { ids[12] }
        };
        foreach (var (metric, expected) in expectedMembers)
        {
            var stored = await G13StoredIds(db, fixture.A, report, metric);
            Assert.True(stored.SetEquals(expected),
                $"G13 reference {metric}: actual={string.Join(",", stored)} expected={string.Join(",", expected)}");
            Assert.DoesNotContain(foreignCompleted, stored);
            Assert.DoesNotContain(foreignBooked, stored);
        }
        var allK02 = await G13StoredIds(db, fixture.A, report, "K02");
        Assert.True(allK02.SetEquals(Enumerable.Range(1, 12)
            .Where(i => i != 3).Select(i => ids[i])));
        Console.WriteLine("G13_REFERENCE_ORACLE all_14_metric_members=true tenant_a=12 tenant_b=2");
    }


    [ExclusiveM13PostgresFact]
    public async Task RepeatableReadReportIgnoresUncommittedConcurrentShipmentTransition()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var adminFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(adminFactory);
        using var admin = fixture.Admin;
        using var originalManager = fixture.Manager;
        using var originalAnalyst = fixture.Analyst;
        var seed = await AssignmentHttpTests.Seed(fixture);
        using var existing = await originalManager.GetAsync(seed.Root + "/" + seed.Shipment);
        var shipment = await existing.Content.ReadFromJsonAsync<JsonElement>();
        var origin = shipment.GetProperty("originPortId").GetGuid();
        var destination = shipment.GetProperty("destinationPortId").GetGuid();

        using var api = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var manager = api.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(manager, fixture.ManagerEmail);
        await ReferenceDataHttpTests.Select(manager, fixture.A);
        var id = await CreateShipment(manager, seed.Root, origin, destination,
            DateTimeOffset.UtcNow.AddDays(2), 911);
        var period = Window(DateTimeOffset.UtcNow);

        // Establish the legal Arrived state before testing an in-flight
        // Arrived-to-Completed transition. Status triggers enforce the M5 graph.
        var shipmentPath = seed.Root + "/" + id;
        foreach (var (version, status) in new[] {
            (1L, "Booked"), (2L, "InTransit"), (3L, "Arrived") })
        {
            using var changed = await ReferenceDataHttpTests.Send(manager,
                HttpMethod.Patch, shipmentPath,
                new { expectedVersion = version, workflowStatus = status });
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        }
        await using var writer = new NpgsqlConnection(roles.Admin);
        await writer.OpenAsync();
        await using (var tx = await writer.BeginTransactionAsync())
        {
            await using var change = new NpgsqlCommand("""
                UPDATE shipments SET workflow_status='Completed',version=version+1,
                    updated_at_utc=clock_timestamp()
                WHERE organization_id=@org AND id=@id
                """, writer, tx);
            change.Parameters.AddWithValue("org", fixture.A);
            change.Parameters.AddWithValue("id", id);
            Assert.Equal(1, await change.ExecuteNonQueryAsync());

            // An independent restricted API connection builds against the
            // last committed MVCC state while the writer holds its row lock.
            var first = await Json(await manager.GetAsync(Root(fixture.A) +
                "/overview" + period));
            var beforeCommit = await G13MemberIds(manager, fixture.A, first, "K01");
            Assert.Contains(id, beforeCommit);
            await tx.CommitAsync();
            var stillFrozen = await G13MemberIds(manager, fixture.A, first, "K01");
            Assert.True(stillFrozen.SetEquals(beforeCommit));
        }

        var refreshed = await Json(await manager.GetAsync(Root(fixture.A) +
            "/overview" + period));
        Assert.DoesNotContain(id,
            await G13MemberIds(manager, fixture.A, refreshed, "K01"));
        Console.WriteLine("G13_CONCURRENT_SNAPSHOT uncommitted_update_hidden=true committed_update_visible=true");
    }


    [ExclusiveM13PostgresFact]
    public async Task PostgreSqlMicrosecondGraceAndUtcWeekBoundariesAreExact()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        await using var db = new NpgsqlConnection(roles.Admin);
        await db.OpenAsync();
        var plan = new DateTimeOffset(2024, 2, 29, 12, 0, 0, TimeSpan.Zero);
        foreach (var (arrival, accepted) in new[] {
            (plan, true),
            (plan.AddMinutes(60), true),
            (plan.AddMinutes(60).AddTicks(10), false)
        })
        {
            await using var cmd = new NpgsqlCommand("""
                SELECT @arrival::timestamptz <=
                       @planned::timestamptz + interval '60 minutes'
                """, db);
            cmd.Parameters.AddWithValue("arrival", arrival);
            cmd.Parameters.AddWithValue("planned", plan);
            Assert.Equal(accepted, Assert.IsType<bool>(await cmd.ExecuteScalarAsync()));
        }
        await using (var bucket = new NpgsqlCommand("""
            SELECT date_trunc('week',
                '2024-02-29T23:59:59.999999Z'::timestamptz
                AT TIME ZONE 'UTC') AT TIME ZONE 'UTC'
            """, db))
        {
            var result = Assert.IsType<DateTime>(await bucket.ExecuteScalarAsync());
            Assert.Equal(new DateTime(2024, 2, 26), result.Date);
        }
        await using (var historic = new NpgsqlCommand("""
            SELECT '1969-12-31T23:59:59.999999Z'::timestamptz <
                   '1970-01-01T00:00:00Z'::timestamptz
            """, db))
            Assert.True(Assert.IsType<bool>(await historic.ExecuteScalarAsync()));
        Console.WriteLine("G13_UTC grace_plus_one_microsecond_fails=true " +
            "leap_week_monday=true pre1970_ordering=true");
    }


    [ExclusiveM13PostgresFact]
    public async Task FrozenKpiMembersPageExactlyOnceAcrossOneHundredRows()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var factory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var managerSeed = f.Manager;
        using var analystSeed = f.Analyst;
        var seed = await AssignmentHttpTests.Seed(f);
        using var originalResponse = await managerSeed.GetAsync(seed.Root + "/" + seed.Shipment);
        var original = await originalResponse.Content.ReadFromJsonAsync<JsonElement>();
        var origin = original.GetProperty("originPortId").GetGuid();
        var destination = original.GetProperty("destinationPortId").GetGuid();
        var ids = new HashSet<Guid> { seed.Shipment, seed.OtherShipment };
        await using var sql = new NpgsqlConnection(roles.Admin);
        await sql.OpenAsync();
        await using var actor = new NpgsqlCommand(
            "SELECT changed_by_user_id FROM shipments WHERE id=@id", sql);
        actor.Parameters.AddWithValue("id", seed.Shipment);
        var actorId = Assert.IsType<Guid>(await actor.ExecuteScalarAsync());
        for (var i = 0; i < 115; i++)
            Assert.True(ids.Add(await G13Insert(sql, f.A, origin, destination,
                actorId, DateTimeOffset.UtcNow.AddDays(1), "G13-PAGING-")));

        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var analyst = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(analyst, f.AnalystEmail);
        await ReferenceDataHttpTests.Select(analyst, f.A);
        var report = await Json(await analyst.GetAsync(Root(f.A) + "/overview"));
        Assert.Equal(117L, Count(report, "K01"));

        string? after = null;
        var observed = new HashSet<Guid>();
        var pages = 0;
        do
        {
            using var request = MemberRequest(f.A.ToString(), report, "K01", 50, after);
            var result = await Json(await analyst.SendAsync(request));
            var page = result.GetProperty("items").EnumerateArray().ToArray();
            Assert.InRange(page.Length, 1, 50);
            foreach (var item in page)
                Assert.True(observed.Add(item.GetProperty("memberKey").GetGuid()),
                    "Immutable KPI report returned a duplicate member");
            var next = result.GetProperty("nextAfter");
            after = next.ValueKind == JsonValueKind.Null ? null : next.GetGuid().ToString();
            pages++;
            Assert.InRange(pages, 1, 4);
        } while (after is not null);
        Assert.Equal(3, pages);
        Assert.True(observed.SetEquals(ids));
        Console.WriteLine("G13_DRILL pages=3 exact_distinct_members=117 stable_report=true");
    }

}