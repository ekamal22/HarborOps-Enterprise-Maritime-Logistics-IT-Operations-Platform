using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Api.Audit;
using HarborOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// Runtime tests use the real migration-created SQL functions and restricted API identity.
// They are never a substitute for the separate source-locked G11 evidence campaign.
public sealed class M11ExceptionRuntimeTests
{
    private static string Utc(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static async Task<HttpResponseMessage> SendAction(HttpClient client,
        string path, object body, Guid key)
    {
        using var tokenResponse = await client.GetAsync("/api/v1/auth/csrf");
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        using var request = new HttpRequestMessage(HttpMethod.Patch, path);
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
        request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",
            token.GetProperty("token").GetString());
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key.ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private sealed record Setup(
        M10RuntimeIdentityTests.Roles Roles,
        ReferenceDataHttpTests.ApiFactory Factory,
        ReferenceDataHttpTests.Fixture Fixture,
        ReferenceDataHttpTests.ApiFactory Runtime,
        HttpClient Manager, HttpClient Analyst, HttpClient Administrator);

    private static async Task<Setup> Prepare()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        var factory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        async Task<HttpClient> Login(string email)
        {
            var client = runtime.CreateClient(new WebApplicationFactoryClientOptions
                { HandleCookies = true });
            await ReferenceDataHttpTests.Login(client, email);
            await ReferenceDataHttpTests.Select(client, fixture.A);
            return client;
        }
        return new Setup(roles, factory, fixture, runtime,
            await Login(fixture.ManagerEmail),
            await Login(fixture.AnalystEmail),
            await Login(fixture.AdminEmail));
    }

    private static async Task<Guid> ScheduledShipment(Setup setup)
    {
        var seed = await AssignmentHttpTests.Seed(setup.Fixture);
        var url = $"/api/v1/organizations/{setup.Fixture.A}/shipments/{seed.Shipment}";
        var now = DateTimeOffset.UtcNow;
        using (var plan = await ReferenceDataHttpTests.Send(setup.Fixture.Manager,
            HttpMethod.Patch, url,
            new { expectedVersion = 1L,
                plannedDepartureAtUtc = Utc(now.AddDays(-1)),
                plannedArrivalAtUtc = Utc(now.AddHours(-2)) }))
            Assert.Equal(HttpStatusCode.OK, plan.StatusCode);
        using (var booked = await ReferenceDataHttpTests.Send(setup.Fixture.Manager,
            HttpMethod.Patch, url,
            new { expectedVersion = 2L, workflowStatus = "Booked" }))
            Assert.Equal(HttpStatusCode.OK, booked.StatusCode);
        return seed.Shipment;
    }

    private static string Collection(Guid org) =>
        $"/api/v1/organizations/{org}/exceptions";

    private static async Task<Guid> EvaluateAndFind(Setup setup, Guid shipment)
    {
        var route = $"/api/v1/organizations/{setup.Fixture.A}/shipments/{shipment}/exceptions";
        using (var evaluate = await ReferenceDataHttpTests.Send(setup.Manager,
            HttpMethod.Post, route + "/evaluate"))
            Assert.Equal(HttpStatusCode.OK, evaluate.StatusCode);
        using var response = await setup.Manager.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        var rows = data.GetProperty("items");
        var row = Assert.Single(rows.EnumerateArray());
        Assert.Equal("ArrivalScheduleOverdue", row.GetProperty("ruleCode").GetString());
        Assert.Equal("Active", row.GetProperty("condition").GetString());
        Assert.Equal("ScheduleOnly", row.GetProperty("confidence").GetString());
        var provenance = row.GetProperty("evidence");
        Assert.Equal("m11-rules-v1",
            provenance.GetProperty("rulePolicyVersion").GetString());
        Assert.True(provenance.GetProperty("shipmentVersion").GetInt64() > 0);
        return row.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task MigrationIsAdditiveAndRuntimeSqlCannotModifyExceptionTables()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        await using var db = new HarborOpsDbContext(
            new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(roles.Admin).Options);
        Assert.Equal(14, (await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.False(db.Database.HasPendingModelChanges());

        await using var restricted = new NpgsqlConnection(roles.Api);
        await restricted.OpenAsync();
        foreach (var statement in new[]
        {
            "INSERT INTO shipment_exceptions DEFAULT VALUES",
            "UPDATE shipment_exceptions SET lifecycle_state='Resolved'",
            "DELETE FROM shipment_exception_history",
            "DELETE FROM shipment_exception_replays",
            "SELECT * FROM shipment_exception_proof_nonces LIMIT 1",
            "SELECT * FROM m10_correction_proof_keys LIMIT 1"
        })
        {
            await using var command = new NpgsqlCommand(statement, restricted);
            var error = await Assert.ThrowsAsync<PostgresException>(
                () => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }

        await using var admin = new NpgsqlConnection(roles.Admin);
        await admin.OpenAsync();
        await using var inspect = new NpgsqlCommand("""
            SELECT EXISTS(SELECT 1 FROM pg_proc p
                WHERE p.oid='m11_apply_signed_action(uuid,uuid,uuid,uuid,varchar,bigint,varchar,uuid,uuid,bigint,varchar)'::regprocedure
                  AND p.prosecdef AND NOT EXISTS(
                    SELECT 1 FROM aclexplode(p.proacl) a WHERE a.grantee=0))
            """, admin);
        Assert.Equal(true, await inspect.ExecuteScalarAsync());
    }

    [Fact]
    public async Task ScheduleOverdueIsScopedAuditedAndIdempotentForManager()
    {
        var s = await Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var _ = s.Fixture.Admin; using var __ = s.Fixture.Manager;
        using var ___ = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;

        var shipment = await ScheduledShipment(s);
        var exception = await EvaluateAndFind(s, shipment);
        var baseUrl = Collection(s.Fixture.A) + "/" + exception;

        using (var allowed = await analyst.GetAsync(baseUrl))
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        using (var forbidden = await administrator.GetAsync(baseUrl))
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using (var hidden = await manager.GetAsync(Collection(s.Fixture.B) + "/" + exception))
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using (var wrongRole = await SendAction(analyst, baseUrl,
            new { action = "Acknowledge", expectedVersion = 1L,
                reason = "Read-only user attempted disposition" }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);

        var key = Guid.NewGuid();
        var body = new { action = "Acknowledge", expectedVersion = 1L,
            reason = "Reviewed the synthetic overdue schedule" };
        using (var accepted = await SendAction(manager, baseUrl, body, key))
        {
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var json = await accepted.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Applied", json.GetProperty("result").GetString());
            Assert.Equal(2L, json.GetProperty("version").GetInt64());
        }
        using (var replay = await SendAction(manager, baseUrl, body, key))
        {
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            var json = await replay.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Replayed", json.GetProperty("result").GetString());
        }
        using (var collision = await SendAction(manager, baseUrl,
            new { action = "Acknowledge", expectedVersion = 1L,
                reason = "Different reason with the original key" }, key))
            Assert.Equal(HttpStatusCode.Conflict, collision.StatusCode);
        using (var activeResolve = await SendAction(manager, baseUrl,
            new { action = "Resolve", expectedVersion = 2L,
                reason = "Attempted to resolve while still overdue" }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.UnprocessableEntity, activeResolve.StatusCode);

        await using var db = new HarborOpsDbContext(
            new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(s.Roles.Admin).Options);
        Assert.Equal(1L, await db.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM audit_entries WHERE resource_id={0} AND event_type='shipment.exception_acknowledged'",
            exception).SingleAsync());
        Assert.Equal(2L, await db.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM shipment_exception_history WHERE exception_id={0}",
            exception).SingleAsync());
        Assert.Equal(1L, await db.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM shipment_exception_replays WHERE exception_id={0}",
            exception).SingleAsync());

        // Repeated unchanged evaluation cannot reset a human acknowledgment.
        using (var repeated = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post,
            $"/api/v1/organizations/{s.Fixture.A}/shipments/{shipment}/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        using (var detail = await manager.GetAsync(baseUrl))
        {
            var json = await detail.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Acknowledged", json.GetProperty("exception")
                .GetProperty("state").GetString());
        }
    }

    [Fact]
    public async Task AcknowledgedExceptionCanBeResolvedAfterSourceClearance()
    {
        var s = await Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var a = s.Fixture.Admin; using var m = s.Fixture.Manager;
        using var n = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        var shipment = await ScheduledShipment(s);
        var id = await EvaluateAndFind(s, shipment);
        var root = Collection(s.Fixture.A) + "/" + id;
        using (var ack = await SendAction(manager, root,
            new { action = "Acknowledge", expectedVersion = 1L,
                reason = "Review recorded overdue arrival" }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.OK, ack.StatusCode);
        var url = $"/api/v1/organizations/{s.Fixture.A}/shipments/{shipment}";
        using (var transit = await ReferenceDataHttpTests.Send(s.Fixture.Manager,
            HttpMethod.Patch, url, new { expectedVersion = 3L, workflowStatus = "InTransit" }))
            Assert.Equal(HttpStatusCode.OK, transit.StatusCode);
        using (var arrival = await ReferenceDataHttpTests.Send(s.Fixture.Manager,
            HttpMethod.Patch, url, new { expectedVersion = 4L, workflowStatus = "Arrived" }))
            Assert.Equal(HttpStatusCode.OK, arrival.StatusCode);
        using (var scan = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post,
            url + "/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        using (var detail = await manager.GetAsync(root))
        {
            var record = (await detail.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("exception");
            Assert.Equal("Inactive", record.GetProperty("condition").GetString());
            Assert.Equal("Acknowledged", record.GetProperty("state").GetString());
            Assert.Equal(3L, record.GetProperty("version").GetInt64());
        }
        using (var resolved = await SendAction(manager, root,
            new { action = "Resolve", expectedVersion = 3L,
                reason = "Reviewed the recorded workflow clearance" }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        await using var db = new HarborOpsDbContext(
            new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(s.Roles.Admin).Options);
        Assert.Equal(1L, await db.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM audit_entries WHERE resource_id={0} AND event_type='shipment.exception_resolved'",
            id).SingleAsync());
    }

    [Fact]
    public async Task ReplayKeyCannotBeReusedForAnotherException()
    {
        var s = await Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var a = s.Fixture.Admin; using var m = s.Fixture.Manager;
        using var n = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        var one = await EvaluateAndFind(s, await ScheduledShipment(s));
        var two = await EvaluateAndFind(s, await ScheduledShipment(s));
        var key = Guid.NewGuid();
        using (var accepted = await SendAction(manager, Collection(s.Fixture.A) + "/" + one,
            new { action = "Acknowledge", expectedVersion = 1L,
                reason = "First synthetic exception reviewed" }, key))
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using (var collision = await SendAction(manager, Collection(s.Fixture.A) + "/" + two,
            new { action = "Acknowledge", expectedVersion = 1L,
                reason = "Second synthetic exception reviewed" }, key))
            Assert.Equal(HttpStatusCode.Conflict, collision.StatusCode);
    }

    [Fact]
    public async Task ArchivedAcknowledgedRiskIsAutomaticallyClosed()
    {
        var s = await Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var a = s.Fixture.Admin; using var m = s.Fixture.Manager;
        using var n = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        var shipment = await ScheduledShipment(s);
        var exception = await EvaluateAndFind(s, shipment);
        using (var acknowledged = await SendAction(manager,
            Collection(s.Fixture.A) + "/" + exception,
            new { action = "Acknowledge", expectedVersion = 1L,
                reason = "Reviewed the synthetic overdue schedule" }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode);
        var shipmentUrl = $"/api/v1/organizations/{s.Fixture.A}/shipments/{shipment}";
        using (var archived = await ReferenceDataHttpTests.Send(s.Fixture.Manager,
            HttpMethod.Delete, shipmentUrl, new { expectedVersion = 3L }))
            Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        using (var evaluated = await ReferenceDataHttpTests.Send(manager,
            HttpMethod.Post, shipmentUrl + "/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode);
        using var detail = await manager.GetAsync(Collection(s.Fixture.A) + "/" + exception);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var row = (await detail.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("exception");
        Assert.Equal("Resolved", row.GetProperty("state").GetString());
        Assert.Equal("Inactive", row.GetProperty("condition").GetString());
    }

    [Fact]
    public async Task NullSchedulesAreUnknownNotInventedDelays()
    {
        var s = await Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var _ = s.Fixture.Admin; using var __ = s.Fixture.Manager;
        using var ___ = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        var seed = await AssignmentHttpTests.Seed(s.Fixture);
        var route = $"/api/v1/organizations/{s.Fixture.A}/shipments/{seed.Shipment}";
        using (var booked = await ReferenceDataHttpTests.Send(s.Fixture.Manager,
            HttpMethod.Patch, route, new { expectedVersion = 1L, workflowStatus = "Booked" }))
            Assert.Equal(HttpStatusCode.OK, booked.StatusCode);

        using (var evaluated = await ReferenceDataHttpTests.Send(manager,
            HttpMethod.Post, route + "/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode);
        using var response = await analyst.GetAsync(route + "/exceptions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(data.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task CursorCannotBeReusedAgainstDifferentFiltersOrTenant()
    {
        var s = await Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var _ = s.Fixture.Admin; using var __ = s.Fixture.Manager;
        using var ___ = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        await EvaluateAndFind(s, await ScheduledShipment(s));
        await EvaluateAndFind(s, await ScheduledShipment(s));
        using var page = await manager.GetAsync(Collection(s.Fixture.A) + "?limit=1");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var body = await page.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        using (var next = await manager.GetAsync(Collection(s.Fixture.A) +
            "?limit=1&cursor=" + Uri.EscapeDataString(token!)))
            Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        using (var swapped = await manager.GetAsync(Collection(s.Fixture.A) +
            "?limit=1&state=Open&cursor=" + Uri.EscapeDataString(token!)))
            Assert.Equal(HttpStatusCode.Conflict, swapped.StatusCode);
        using (var foreign = await manager.GetAsync(Collection(s.Fixture.B) +
            "?limit=1&cursor=" + Uri.EscapeDataString(token!)))
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}
