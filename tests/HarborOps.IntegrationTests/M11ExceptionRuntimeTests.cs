using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Api.Audit;
using HarborOps.Infrastructure.Exceptions;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Shipments;
using Microsoft.Extensions.DependencyInjection;
using HarborOps.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
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
    public async Task EquipmentConflictCarriesExactAcceptedEventAndContainerWitness()
    {
        var s = await Prepare();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var administrator = s.Fixture.Admin;
        using var manager = s.Fixture.Manager;
        using var analyst = s.Fixture.Analyst;
        using var restrictedManager = s.Manager;
        using var restrictedAnalyst = s.Analyst;
        using var restrictedAdministrator = s.Administrator;

        var target = await G9Support.SeedResolvable(factory, s.Fixture);
        var source = await G9Support.NewSource(factory,s.Fixture.A,"m11_conflict");
        var suffix = Guid.NewGuid().ToString("N")[..12];
        const string occurred = "2026-11-03T10:00:00Z";
        var receiptIn = await G9Support.Accept(factory,source,
            G9Support.EquipmentPayload("m11-in-"+suffix,target.Reference,
                occurredAt:occurred,code:"GTIN"));
        var receiptOut = await G9Support.Accept(factory,source,
            G9Support.EquipmentPayload("m11-out-"+suffix,target.Reference,
                occurredAt:occurred,code:"GTOT"));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var worker = new TrackingWorkStore(db,new TrackingWorkerSettings());
        for (var i=0; i<2; i++)
        {
            var claim = await worker.ClaimOneAsync(s.Fixture.A,CancellationToken.None);
            Assert.NotNull(claim);
            Assert.Equal(TrackingWorkDisposition.Processed,
                await worker.FinalizeAsync(claim!,CancellationToken.None));
        }
        var acceptedIn = await db.TrackingAcceptedEvents.AsNoTracking()
            .SingleAsync(e=>e.ReceiptId==receiptIn);
        var acceptedOut = await db.TrackingAcceptedEvents.AsNoTracking()
            .SingleAsync(e=>e.ReceiptId==receiptOut);
        var timeline = new ShipmentTimelineStore(db);
        var rebuilt = await timeline.RebuildAsync(s.Fixture.A,target.Shipment,
            CancellationToken.None);
        Assert.Equal(TrackingMutationResult.Applied,rebuilt.Result);
        var projection = await timeline.ReadBoundedAsync(s.Fixture.A,target.Shipment,
            CancellationToken.None);
        Assert.NotNull(projection);
        Assert.Equal("Conflict",projection!.State.ObservedTrackingState);

        var shipmentUrl = $"/api/v1/organizations/{s.Fixture.A}/shipments/{target.Shipment}";
        using (var booked = await ReferenceDataHttpTests.Send(s.Fixture.Manager,
            HttpMethod.Patch,shipmentUrl,new { expectedVersion=1L, workflowStatus="Booked" }))
            Assert.Equal(HttpStatusCode.OK,booked.StatusCode);

        using (var evaluated = await ReferenceDataHttpTests.Send(restrictedManager,
            HttpMethod.Post,shipmentUrl+"/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK,evaluated.StatusCode);

        using var response = await restrictedManager.GetAsync(
            $"/api/v1/organizations/{s.Fixture.A}/shipments/{target.Shipment}/exceptions");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        var row = Assert.Single(data.GetProperty("items").EnumerateArray()
            .Where(e=>e.GetProperty("ruleCode").GetString()=="EquipmentConflict"));
        Assert.Equal("Active",row.GetProperty("condition").GetString());
        var evidence = row.GetProperty("evidence");
        Assert.Equal(1,evidence.GetProperty("conflictContainerCount").GetInt32());
        Assert.False(evidence.GetProperty("conflictWitnessTruncated").GetBoolean());
        var witness = evidence.GetProperty("conflictWitness");
        Assert.Equal(target.Container,witness.GetProperty("containerId").GetGuid());
        var incoming = witness.GetProperty("in");
        var outgoing = witness.GetProperty("out");
        Assert.Equal(receiptIn,incoming.GetProperty("receiptId").GetGuid());
        Assert.Equal(receiptOut,outgoing.GetProperty("receiptId").GetGuid());
        Assert.Equal(acceptedIn.Id,incoming.GetProperty("eventId").GetGuid());
        Assert.Equal(acceptedOut.Id,outgoing.GetProperty("eventId").GetGuid());
        Assert.Equal(source.Id,incoming.GetProperty("sourceId").GetGuid());
        Assert.Equal(source.Id,outgoing.GetProperty("sourceId").GetGuid());
        Assert.Equal("m11-in-"+suffix,incoming.GetProperty("sourceEventId").GetString());
        Assert.Equal("m11-out-"+suffix,outgoing.GetProperty("sourceEventId").GetString());
        Assert.Equal(JsonValueKind.Null,
            incoming.GetProperty("replacementCorrectionId").ValueKind);
        Assert.Equal(JsonValueKind.Null,
            outgoing.GetProperty("replacementCorrectionId").ValueKind);
    }

    [Fact]
    public async Task DirectApiSqlCannotReadExceptionsOrHistoryWithoutSignedSession()
    {
        var s = await Prepare();
        using var fixture = s.Factory; using var runtime = s.Runtime;
        using var administrator = s.Fixture.Admin;
        using var manager = s.Fixture.Manager;
        using var analyst = s.Fixture.Analyst;
        using var authorizedManager = s.Manager;
        using var authorizedAnalyst = s.Analyst;
        using var authorizedAdministrator = s.Administrator;
        var exception = await EvaluateAndFind(s, await ScheduledShipment(s));

        await using var privileged = new NpgsqlConnection(s.Roles.Admin);
        await privileged.OpenAsync();
        await using (var actual = new NpgsqlCommand(
            "SELECT count(*) FROM shipment_exceptions WHERE id=@id",privileged))
        {
            actual.Parameters.AddWithValue("id",exception);
            Assert.Equal(1L,(long)(await actual.ExecuteScalarAsync())!);
        }

        await using var restricted = new NpgsqlConnection(s.Roles.Api);
        await restricted.OpenAsync();
        foreach (var table in new[]
            { "shipment_exceptions", "shipment_exception_history",
              "shipment_exception_evaluations" })
        {
            await using var query = new NpgsqlCommand(
                "SELECT count(*) FROM " + table,restricted);
            Assert.Equal(0L,(long)(await query.ExecuteScalarAsync())!);
        }

        await using var tx = await restricted.BeginTransactionAsync();
        await using (var forged = new NpgsqlCommand(
            "SELECT set_config('harborops.m11_read_scope',@scope,true)",restricted,tx))
        {
            forged.Parameters.AddWithValue("scope",
                $"{s.Fixture.A:D}|{Guid.NewGuid():D}|{Guid.NewGuid():D}|"+
                $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()+20}|"+
                new string('0',64));
            await forged.ExecuteNonQueryAsync();
        }
        await using (var forgedRead = new NpgsqlCommand(
            "SELECT count(*) FROM shipment_exceptions",restricted,tx))
            Assert.Equal(0L,(long)(await forgedRead.ExecuteScalarAsync())!);
        await using (var forgedDue = new NpgsqlCommand(
            "SELECT set_config('harborops.m11_due_scope',@scope,true)",restricted,tx))
        {
            forgedDue.Parameters.AddWithValue("scope",
                $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()+20}|"+
                new string('0',64));
            await forgedDue.ExecuteNonQueryAsync();
        }
        await using (var forgedDueRead = new NpgsqlCommand(
            "SELECT count(*) FROM shipment_exception_evaluations",restricted,tx))
            Assert.Equal(0L,(long)(await forgedDueRead.ExecuteScalarAsync())!);
        await tx.CommitAsync();
    }

    [Fact]
    public async Task MigrationIsAdditiveAndRuntimeSqlCannotModifyExceptionTables()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        await using var db = new HarborOpsDbContext(
            new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(roles.Admin).Options);
        Assert.Equal(15, (await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.False(db.Database.HasPendingModelChanges());

        await using var restricted = new NpgsqlConnection(roles.Api);
        await restricted.OpenAsync();
        foreach (var statement in new[]
        {
            "INSERT INTO shipment_exceptions DEFAULT VALUES",
            "UPDATE shipment_exceptions SET lifecycle_state='Resolved'",
            "DELETE FROM shipment_exception_history",
            "DELETE FROM shipment_exception_replays",
            "SELECT * FROM shipment_exception_replays LIMIT 1",
            "SELECT * FROM shipment_exception_proof_nonces LIMIT 1",
            "SELECT * FROM m10_correction_proof_keys LIMIT 1"
        })
        {
            await using var command = new NpgsqlCommand(statement, restricted);
            var error = await Assert.ThrowsAsync<PostgresException>(
                () => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }

        // Direct restricted-login calls must not convert SQL NULL values
        // into an absent HMAC and then skip signature verification.
        foreach (var unsigned in new[]
        {
            """
            SELECT m11_apply_signed_evaluation(
                '11111111-1111-1111-1111-111111111111'::uuid,
                '22222222-2222-2222-2222-222222222222'::uuid,
                'ArrivalScheduleOverdue'::varchar,'Inactive'::varchar,
                NULL::varchar,'ScheduleOnly'::varchar,
                'scheduled_arrival_not_yet_overdue'::varchar,
                repeat('0',64)::varchar,
                NULL::timestamptz,NULL::timestamptz,NULL::timestamptz,
                1::bigint,0::bigint,0::bigint,
                '33333333-3333-3333-3333-333333333333'::uuid,
                (extract(epoch from clock_timestamp())::bigint+30),
                NULL::varchar)
            """,
            """
            SELECT * FROM m11_apply_signed_action(
                '11111111-1111-1111-1111-111111111111'::uuid,
                '22222222-2222-2222-2222-222222222222'::uuid,
                '33333333-3333-3333-3333-333333333333'::uuid,
                '44444444-4444-4444-4444-444444444444'::uuid,
                NULL::varchar,1::bigint,'Reviewed synthetic exception'::varchar,
                '55555555-5555-5555-5555-555555555555'::uuid,
                '66666666-6666-6666-6666-666666666666'::uuid,
                (extract(epoch from clock_timestamp())::bigint+30),
                NULL::varchar)
            """
        })
        {
            await using var command = new NpgsqlCommand(unsigned, restricted);
            var denied = await Assert.ThrowsAsync<PostgresException>(
                () => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
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
    public async Task ConcurrentFirstEvaluationsDoNotDuplicateOccurrenceOrHistory()
    {
        var setup = await Prepare();
        using var factory = setup.Factory;
        using var runtime = setup.Runtime;
        using var setupAdmin = setup.Fixture.Admin;
        using var setupManager = setup.Fixture.Manager;
        using var setupAnalyst = setup.Fixture.Analyst;
        using var manager = setup.Manager;
        using var analyst = setup.Analyst;
        using var administrator = setup.Administrator;
        var shipment = await ScheduledShipment(setup);
        var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
            .UseNpgsql(setup.Roles.Api).Options;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["M10CorrectionProof:Key"] = ReferenceDataHttpTests.CorrectionProofKey
            }).Build();
        await using var firstDb = new HarborOpsDbContext(options);
        await using var secondDb = new HarborOpsDbContext(options);
        var firstStore = new ShipmentExceptionStore(firstDb, configuration);
        var secondStore = new ShipmentExceptionStore(secondDb, configuration);
        var results = await Task.WhenAll(
            firstStore.EvaluateAsync(setup.Fixture.A, shipment, CancellationToken.None),
            secondStore.EvaluateAsync(setup.Fixture.A, shipment, CancellationToken.None));
        Assert.All(results, result => Assert.Contains(result.Result,
            new[] { M11StoreResult.Applied, M11StoreResult.Conflict }));
        Assert.Contains(results, result => result.Result == M11StoreResult.Applied);
        if (results[0].Result == M11StoreResult.Conflict)
            Assert.Equal(M11StoreResult.Applied, (await firstStore.EvaluateAsync(
                setup.Fixture.A, shipment, CancellationToken.None)).Result);
        if (results[1].Result == M11StoreResult.Conflict)
            Assert.Equal(M11StoreResult.Applied, (await secondStore.EvaluateAsync(
                setup.Fixture.A, shipment, CancellationToken.None)).Result);
        await using var inspect = new HarborOpsDbContext(
            new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(setup.Roles.Admin).Options);
        Assert.Equal(1L, await inspect.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM shipment_exceptions " +
            "WHERE organization_id={0} AND shipment_id={1} AND rule_code='ArrivalScheduleOverdue'",
            setup.Fixture.A, shipment).SingleAsync());
        Assert.Equal(1L, await inspect.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM shipment_exception_history h " +
            "JOIN shipment_exceptions e ON e.id=h.exception_id " +
            "WHERE h.organization_id={0} AND e.shipment_id={1} AND h.action='Opened'",
            setup.Fixture.A, shipment).SingleAsync());
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
        using (var analystDetail = await analyst.GetAsync(baseUrl))
        {
            var response = await analystDetail.Content.ReadFromJsonAsync<JsonElement>();
            var history = response.GetProperty("history");
            Assert.All(history.EnumerateArray(), entry =>
            {
                Assert.Equal(JsonValueKind.Null, entry.GetProperty("reason").ValueKind);
                Assert.Equal(JsonValueKind.Null, entry.GetProperty("actorUserId").ValueKind);
            });
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
    public async Task SameFingerprintWithStaleSeverityWritesAttributedReevaluation()
    {
        var s = await Prepare();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var admin = s.Fixture.Admin; using var managerFixture = s.Fixture.Manager;
        using var analystFixture = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        var shipment = await ScheduledShipment(s);
        var occurrence = await EvaluateAndFind(s, shipment);
        await using var db = new HarborOpsDbContext(
            new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(s.Roles.Admin).Options);
        // A migration-role fixture simulates a pre-gate severity recalibration.
        // Production API credentials do not have this direct UPDATE permission.
        Assert.Equal(1, await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE shipment_exceptions SET severity='Info' WHERE id={occurrence}"));
        using (var evaluate = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post,
            $"/api/v1/organizations/{s.Fixture.A}/shipments/{shipment}/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK, evaluate.StatusCode);
        using (var detail = await manager.GetAsync(Collection(s.Fixture.A) + "/" + occurrence))
        {
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            var data = await detail.Content.ReadFromJsonAsync<JsonElement>();
            var row = data.GetProperty("exception");
            Assert.Equal("Warning", row.GetProperty("severity").GetString());
            Assert.Equal(2L, row.GetProperty("version").GetInt64());
            Assert.Equal(2, data.GetProperty("history").GetArrayLength());
        }
        using (var again = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post,
            $"/api/v1/organizations/{s.Fixture.A}/shipments/{shipment}/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(2L, await db.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM shipment_exception_history WHERE exception_id={0}",
            occurrence).SingleAsync());
    }

    [Fact]
    public async Task UnchangedReevaluationAdvancesCheckpointWithoutDuplicateHistory()
    {
        var s = await Prepare();
        using var adminFactory = s.Factory; using var runtime = s.Runtime;
        using var setupAdmin = s.Fixture.Admin;
        using var setupManager = s.Fixture.Manager;
        using var setupAnalyst = s.Fixture.Analyst;
        using var manager = s.Manager;
        using var analyst = s.Analyst;
        using var administrator = s.Administrator;

        var shipment = await ScheduledShipment(s);
        var exception = await EvaluateAndFind(s, shipment);
        await using var db = new HarborOpsDbContext(
            new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(s.Roles.Admin).Options);
        var query = "SELECT evaluated_at_utc AS \"Value\" FROM shipment_exception_evaluations " +
            "WHERE organization_id={0} AND shipment_id={1} AND rule_code='ArrivalScheduleOverdue'";
        var before = await db.Database.SqlQueryRaw<DateTimeOffset>(query,
            s.Fixture.A, shipment).SingleAsync();
        await Task.Delay(30);
        using (var evaluate = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post,
            $"/api/v1/organizations/{s.Fixture.A}/shipments/{shipment}/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK, evaluate.StatusCode);
        var after = await db.Database.SqlQueryRaw<DateTimeOffset>(query,
            s.Fixture.A, shipment).SingleAsync();
        Assert.True(after > before, "Repeated evaluation must advance its due checkpoint.");
        Assert.Equal(1L, await db.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM shipment_exception_history WHERE exception_id={0}",
            exception).SingleAsync());
    }

    [Fact]
    public async Task RecurrenceAfterAcknowledgedClearanceCreatesNewOccurrence()
    {
        var s = await Prepare();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var a = s.Fixture.Admin; using var m = s.Fixture.Manager;
        using var n = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        var shipment = await ScheduledShipment(s);
        var original = await EvaluateAndFind(s, shipment);
        var originalPath = Collection(s.Fixture.A) + "/" + original;
        var shipmentPath = $"/api/v1/organizations/{s.Fixture.A}/shipments/{shipment}";
        using (var ack = await SendAction(manager, originalPath,
            new { action = "Acknowledge", expectedVersion = 1L,
                reason = "Reviewing the original overdue arrival" }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.OK, ack.StatusCode);

        using (var update = await ReferenceDataHttpTests.Send(s.Fixture.Manager,
            HttpMethod.Patch, shipmentPath, new { expectedVersion = 3L,
                plannedArrivalAtUtc = Utc(DateTimeOffset.UtcNow.AddHours(4)) }))
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using (var evaluation = await ReferenceDataHttpTests.Send(manager,
            HttpMethod.Post, shipmentPath + "/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK, evaluation.StatusCode);
        using (var detail = await manager.GetAsync(originalPath))
        {
            var row = (await detail.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("exception");
            Assert.Equal("Acknowledged", row.GetProperty("state").GetString());
            Assert.Equal("Inactive", row.GetProperty("condition").GetString());
        }

        using (var update = await ReferenceDataHttpTests.Send(s.Fixture.Manager,
            HttpMethod.Patch, shipmentPath, new { expectedVersion = 4L,
                plannedArrivalAtUtc = Utc(DateTimeOffset.UtcNow.AddHours(-2)) }))
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        for (var i = 0; i < 2; i++)
        {
            using var evaluation = await ReferenceDataHttpTests.Send(manager,
                HttpMethod.Post, shipmentPath + "/exceptions/evaluate");
            Assert.Equal(HttpStatusCode.OK, evaluation.StatusCode);
        }
        using (var list = await manager.GetAsync(shipmentPath + "/exceptions"))
        {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var data = await list.Content.ReadFromJsonAsync<JsonElement>();
            var rows = data.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(2, rows.Length);
            var first = rows.Single(item => item.GetProperty("id").GetGuid() == original);
            Assert.Equal(1, first.GetProperty("occurrence").GetInt32());
            Assert.Equal("Resolved", first.GetProperty("state").GetString());
            var current = rows.Single(item => item.GetProperty("id").GetGuid() != original);
            Assert.Equal(2, current.GetProperty("occurrence").GetInt32());
            Assert.Equal("Open", current.GetProperty("state").GetString());
            Assert.Equal("Active", current.GetProperty("condition").GetString());
        }

        await using var db = new HarborOpsDbContext(
            new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(s.Roles.Admin).Options);
        Assert.Equal(1L, await db.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM shipment_exception_history " +
            "WHERE exception_id={0} AND action='AutoResolved'", original).SingleAsync());
    }

    [Fact]
    public async Task VerifiedClearancePersistsAcrossUnknownBeforeRecurrence()
    {
        var s = await Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var admin = s.Fixture.Admin; using var fixtureManager = s.Fixture.Manager;
        using var analyst = s.Fixture.Analyst;
        using var manager = s.Manager; using var reader = s.Analyst;
        using var administrator = s.Administrator;
        var shipment = await ScheduledShipment(s);
        var original = await EvaluateAndFind(s, shipment);
        var detailPath = Collection(s.Fixture.A) + "/" + original;
        var route = $"/api/v1/organizations/{s.Fixture.A}/shipments/{shipment}";
        using (var ack = await SendAction(manager, detailPath,
            new { action = "Acknowledge", expectedVersion = 1L,
                reason = "Acknowledged for clearance recurrence review" }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.OK, ack.StatusCode);

        using (var cleared = await ReferenceDataHttpTests.Send(s.Fixture.Manager,
            HttpMethod.Patch, route,
            new { expectedVersion = 3L,
                plannedArrivalAtUtc = Utc(DateTimeOffset.UtcNow.AddHours(4)) }))
            Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        using (var evaluated = await ReferenceDataHttpTests.Send(manager,
            HttpMethod.Post, route + "/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode);

        using (var unknown = await ReferenceDataHttpTests.Send(s.Fixture.Manager,
            HttpMethod.Patch, route,
            new { expectedVersion = 4L, plannedArrivalAtUtc = (string?)null }))
            Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        using (var evaluated = await ReferenceDataHttpTests.Send(manager,
            HttpMethod.Post, route + "/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode);
        using (var detail = await manager.GetAsync(detailPath))
        {
            var item = (await detail.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("exception");
            Assert.Equal("Acknowledged", item.GetProperty("state").GetString());
            Assert.Equal("Unknown", item.GetProperty("condition").GetString());
        }

        using (var recurred = await ReferenceDataHttpTests.Send(s.Fixture.Manager,
            HttpMethod.Patch, route,
            new { expectedVersion = 5L,
                plannedArrivalAtUtc = Utc(DateTimeOffset.UtcNow.AddHours(-3)) }))
            Assert.Equal(HttpStatusCode.OK, recurred.StatusCode);
        using (var evaluated = await ReferenceDataHttpTests.Send(manager,
            HttpMethod.Post, route + "/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode);
        using var listing = await manager.GetAsync(route + "/exceptions");
        var rows = (await listing.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Equal("Resolved", rows.Single(x =>
            x.GetProperty("id").GetGuid() == original).GetProperty("state").GetString());
        var latest = rows.Single(x => x.GetProperty("id").GetGuid() != original);
        Assert.Equal("Open", latest.GetProperty("state").GetString());
        Assert.Equal(2, latest.GetProperty("occurrence").GetInt32());
    }

    [Fact]
    public async Task RecordedArrivalLateMayBeClosedWithoutErasingHistoricalFact()
    {
        var s = await Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var admin = s.Fixture.Admin; using var fixtureManager = s.Fixture.Manager;
        using var analyst = s.Fixture.Analyst;
        using var manager = s.Manager; using var reader = s.Analyst;
        using var administrator = s.Administrator;
        var shipment = await ScheduledShipment(s);
        var route = $"/api/v1/organizations/{s.Fixture.A}/shipments/{shipment}";
        using (var departure = await ReferenceDataHttpTests.Send(s.Fixture.Manager,
            HttpMethod.Patch, route,
            new { expectedVersion = 3L, workflowStatus = "InTransit" }))
            Assert.Equal(HttpStatusCode.OK, departure.StatusCode);
        using (var arrival = await ReferenceDataHttpTests.Send(s.Fixture.Manager,
            HttpMethod.Patch, route,
            new { expectedVersion = 4L, workflowStatus = "Arrived" }))
            Assert.Equal(HttpStatusCode.OK, arrival.StatusCode);
        using (var evaluated = await ReferenceDataHttpTests.Send(manager,
            HttpMethod.Post, route + "/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode);

        Guid lateId;
        using (var listing = await manager.GetAsync(route + "/exceptions"))
        {
            var rows = (await listing.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("items").EnumerateArray().ToArray();
            var late = rows.Single(x =>
                x.GetProperty("ruleCode").GetString() == "RecordedArrivalLate");
            lateId = late.GetProperty("id").GetGuid();
            Assert.Equal("Active", late.GetProperty("condition").GetString());
        }
        var detailPath = Collection(s.Fixture.A) + "/" + lateId;
        using (var ack = await SendAction(manager, detailPath,
            new { action = "Acknowledge", expectedVersion = 1L,
                reason = "Confirmed historical recorded arrival lateness" }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.OK, ack.StatusCode);
        using (var close = await SendAction(manager, detailPath,
            new { action = "Resolve", expectedVersion = 2L,
                reason = "Completed review of recorded late arrival fact" }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.OK, close.StatusCode);

        using (var changed = await ReferenceDataHttpTests.Send(s.Fixture.Manager,
            HttpMethod.Patch, route,
            new { expectedVersion = 5L,
                displayName = "Reviewed recorded arrival history" }))
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        for (var i = 0; i < 2; i++)
        {
            using var reevaluation = await ReferenceDataHttpTests.Send(manager,
                HttpMethod.Post, route + "/exceptions/evaluate");
            Assert.Equal(HttpStatusCode.OK, reevaluation.StatusCode);
        }
        using (var listing = await manager.GetAsync(route + "/exceptions"))
        {
            var rows = (await listing.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("items").EnumerateArray().ToArray()
                .Where(x => x.GetProperty("ruleCode").GetString() == "RecordedArrivalLate")
                .ToArray();
            var late = Assert.Single(rows);
            Assert.Equal(lateId, late.GetProperty("id").GetGuid());
            Assert.Equal("Resolved", late.GetProperty("state").GetString());
            Assert.Equal("Active", late.GetProperty("condition").GetString());
        }
        await using var db = new HarborOpsDbContext(
            new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(s.Roles.Admin).Options);
        Assert.Equal(1L, await db.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM shipment_exception_history " +
            "WHERE exception_id={0} AND action='Resolved' AND actor_kind='Human'",
            lateId).SingleAsync());
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
        // Recorded lateness outranks an earlier acknowledged schedule warning,
        // including when the result set is split across protected cursor pages.
        string cursor;
        using (var first = await manager.GetAsync(url + "/exceptions?limit=1"))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var json = await first.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("RecordedArrivalLate", Assert.Single(
                json.GetProperty("items").EnumerateArray())
                .GetProperty("ruleCode").GetString());
            cursor = json.GetProperty("nextCursor").GetString()!;
            Assert.False(string.IsNullOrEmpty(cursor));
        }
        using (var next = await manager.GetAsync(url + "/exceptions?limit=1&cursor=" +
            Uri.EscapeDataString(cursor)))
        {
            Assert.Equal(HttpStatusCode.OK, next.StatusCode);
            var json = await next.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("ArrivalScheduleOverdue", Assert.Single(
                json.GetProperty("items").EnumerateArray())
                .GetProperty("ruleCode").GetString());
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
    public async Task ExpiredRuleCheckpointBlocksResolveUntilFreshReevaluation()
    {
        var s = await Prepare();
        using var adminFactory = s.Factory; using var runtime = s.Runtime;
        using var setupAdmin = s.Fixture.Admin;
        using var setupManager = s.Fixture.Manager;
        using var setupAnalyst = s.Fixture.Analyst;
        using var manager = s.Manager;
        using var analyst = s.Analyst;
        using var administrator = s.Administrator;

        var shipment = await ScheduledShipment(s);
        var exception = await EvaluateAndFind(s, shipment);
        var path = Collection(s.Fixture.A) + "/" + exception;
        using (var ack = await SendAction(manager, path,
            new { action = "Acknowledge", expectedVersion = 1L,
                reason = "Reviewing the synthetic exception" }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.OK, ack.StatusCode);
        var shipmentPath = $"/api/v1/organizations/{s.Fixture.A}/shipments/{shipment}";
        using (var transit = await ReferenceDataHttpTests.Send(s.Fixture.Manager,
            HttpMethod.Patch, shipmentPath,
            new { expectedVersion = 3L, workflowStatus = "InTransit" }))
            Assert.Equal(HttpStatusCode.OK, transit.StatusCode);
        using (var arrival = await ReferenceDataHttpTests.Send(s.Fixture.Manager,
            HttpMethod.Patch, shipmentPath,
            new { expectedVersion = 4L, workflowStatus = "Arrived" }))
            Assert.Equal(HttpStatusCode.OK, arrival.StatusCode);
        using (var review = await ReferenceDataHttpTests.Send(manager,
            HttpMethod.Post, shipmentPath + "/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        await using (var connection = new NpgsqlConnection(s.Roles.Admin))
        {
            await connection.OpenAsync();
            await using var expire = new NpgsqlCommand("""
                UPDATE shipment_exception_evaluations
                SET next_evaluation_at_utc=clock_timestamp()-interval '2 seconds'
                WHERE organization_id=@org AND shipment_id=@shipment
                    AND rule_code='ArrivalScheduleOverdue'
                """, connection);
            expire.Parameters.AddWithValue("org", s.Fixture.A);
            expire.Parameters.AddWithValue("shipment", shipment);
            Assert.Equal(1, await expire.ExecuteNonQueryAsync());
        }
        using (var blocked = await SendAction(manager, path,
            new { action = "Resolve", expectedVersion = 3L,
                reason = "Reviewed cleared condition after expiry" }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        using (var reevaluate = await ReferenceDataHttpTests.Send(manager,
            HttpMethod.Post, shipmentPath + "/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK, reevaluate.StatusCode);
        using (var allowed = await SendAction(manager, path,
            new { action = "Resolve", expectedVersion = 3L,
                reason = "Reviewed cleared condition after refresh" }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
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
    public async Task InvalidMutationHeadersAndBodiesDoNotWriteHistory()
    {
        var s = await Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var a = s.Fixture.Admin; using var m = s.Fixture.Manager;
        using var n = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        var exception = await EvaluateAndFind(s, await ScheduledShipment(s));
        var path = Collection(s.Fixture.A) + "/" + exception;
        using (var noKey = await ReferenceDataHttpTests.Send(manager, HttpMethod.Patch,
            path, new { action = "Acknowledge", expectedVersion = 1L,
                reason = "Reviewed but missing idempotency key" }))
            Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        using (var unknown = await SendAction(manager, path,
            new { action = "Acknowledge", expectedVersion = 1L,
                reason = "Reviewed with an extra property", unexpected = true },
            Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        using (var shortReason = await SendAction(manager, path,
            new { action = "Acknowledge", expectedVersion = 1L,
                reason = "short" }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.BadRequest, shortReason.StatusCode);
        await using var db = new HarborOpsDbContext(
            new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(s.Roles.Admin).Options);
        Assert.Equal(1L, await db.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM shipment_exception_history WHERE exception_id={0}",
            exception).SingleAsync());
    }

    [Fact]
    public async Task LongImmutableHistoryCanBePagedWithRoleRedaction()
    {
        var s = await Prepare();
        using var adminFactory = s.Factory; using var runtime = s.Runtime;
        using var setupAdmin = s.Fixture.Admin;
        using var setupManager = s.Fixture.Manager;
        using var setupAnalyst = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        var exception = await EvaluateAndFind(s, await ScheduledShipment(s));
        var path = Collection(s.Fixture.A) + "/" + exception;
        await using var adminDb = new HarborOpsDbContext(
            new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(s.Roles.Admin).Options);
        // Use only synthetic, append-only rows. Production evidence is never
        // changed by this disposable-database regression.
        Assert.Equal(505, await adminDb.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO shipment_exception_history
                (organization_id,exception_id,action,old_state,new_state,
                 actor_kind,reason,source_fingerprint)
            SELECT e.organization_id,e.id,'Reevaluated','Open','Open',
                'System','Synthetic history pagination review',e.source_fingerprint
            FROM shipment_exceptions e CROSS JOIN generate_series(1,505)
            WHERE e.organization_id={s.Fixture.A} AND e.id={exception}
            """));
        string cursor;
        var seen = new HashSet<Guid>();
        using (var detail = await manager.GetAsync(path))
        {
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            var data = await detail.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(data.GetProperty("historyTruncated").GetBoolean());
            var events = data.GetProperty("history");
            Assert.Equal(500, events.GetArrayLength());
            foreach (var item in events.EnumerateArray())
                Assert.True(seen.Add(item.GetProperty("id").GetGuid()));
            cursor = data.GetProperty("nextHistoryCursor").GetString()!;
            Assert.False(string.IsNullOrEmpty(cursor));
        }
        using (var analystPage = await analyst.GetAsync(
            path + "/history?limit=1&cursor=" + Uri.EscapeDataString(cursor)))
        {
            Assert.Equal(HttpStatusCode.OK, analystPage.StatusCode);
            var events = (await analystPage.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("items");
            Assert.All(events.EnumerateArray(), item =>
            {
                Assert.Equal(JsonValueKind.Null, item.GetProperty("reason").ValueKind);
                Assert.Equal(JsonValueKind.Null, item.GetProperty("actorUserId").ValueKind);
            });
        }
        using (var wrongOrg = await manager.GetAsync(
            Collection(s.Fixture.B) + "/" + exception +
            "/history?cursor=" + Uri.EscapeDataString(cursor)))
            Assert.Equal(HttpStatusCode.NotFound, wrongOrg.StatusCode);
        for (var n = 0; n < 10 && cursor is not null; n++)
        {
            using var response = await manager.GetAsync(path +
                "/history?limit=100&cursor=" + Uri.EscapeDataString(cursor));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = await response.Content.ReadFromJsonAsync<JsonElement>();
            foreach (var item in page.GetProperty("items").EnumerateArray())
                Assert.True(seen.Add(item.GetProperty("id").GetGuid()));
            cursor = page.GetProperty("nextCursor").GetString()!;
        }
        Assert.Null(cursor);
        Assert.Equal(506, seen.Count);
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
    public async Task CombinedSearchAndExceptionRoutesPreserveM5ShipmentDetail()
    {
        var s = await Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var admin = s.Fixture.Admin; using var fixtureManager = s.Fixture.Manager;
        using var fixtureAnalyst = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        var shipment = await ScheduledShipment(s);
        var exception = await EvaluateAndFind(s, shipment);
        var route = $"/api/v1/organizations/{s.Fixture.A}/shipments/{shipment}";
        using var detailResponse = await manager.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        var code = detail.GetProperty("referenceCode").GetString();
        Assert.False(string.IsNullOrEmpty(code));

        using var searchResponse = await analyst.GetAsync(
            $"/api/v1/organizations/{s.Fixture.A}/shipments/search?q=" +
            Uri.EscapeDataString(code!));
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        var search = await searchResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(search.GetProperty("shipments").EnumerateArray(),
            row => row.GetProperty("id").GetGuid() == shipment);

        using var exceptionsResponse = await analyst.GetAsync(route + "/exceptions");
        Assert.Equal(HttpStatusCode.OK, exceptionsResponse.StatusCode);
        var exceptions = await exceptionsResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(exceptions.GetProperty("items").EnumerateArray(),
            row => row.GetProperty("id").GetGuid() == exception);
        using var preservedDetail = await analyst.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, preservedDetail.StatusCode);
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
        var firstException = await EvaluateAndFind(s, await ScheduledShipment(s));
        var secondException = await EvaluateAndFind(s, await ScheduledShipment(s));
        using var page = await manager.GetAsync(Collection(s.Fixture.A) + "?limit=1");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var body = await page.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        // Pages use a database-clock creation cutoff, but lifecycle fields
        // are intentionally live. A disposition between pages must be visible.
        var firstPageId = Assert.Single(body.GetProperty("items").EnumerateArray())
            .GetProperty("id").GetGuid();
        var remaining = firstPageId == firstException ? secondException : firstException;
        using (var acknowledge = await SendAction(manager,
            Collection(s.Fixture.A) + "/" + remaining,
            new { action = "Acknowledge", expectedVersion = 1L,
                reason = "Reviewing this exception during live pagination" },
            Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.OK, acknowledge.StatusCode);
        // A new occurrence opened after the first page must not enter its
        // database-clock creation-cutoff window.
        var later = await EvaluateAndFind(s, await ScheduledShipment(s));
        var seen = new HashSet<Guid>();
        seen.Add(Assert.Single(body.GetProperty("items").EnumerateArray())
            .GetProperty("id").GetGuid());
        var cursor = token;
        for (var pageNumber = 0; pageNumber < 8 && cursor is not null; pageNumber++)
        {
            using var next = await manager.GetAsync(Collection(s.Fixture.A) +
                "?limit=1&cursor=" + Uri.EscapeDataString(cursor));
            Assert.Equal(HttpStatusCode.OK, next.StatusCode);
            var json = await next.Content.ReadFromJsonAsync<JsonElement>();
            foreach (var item in json.GetProperty("items").EnumerateArray())
            {
                Assert.True(seen.Add(item.GetProperty("id").GetGuid()));
                if (item.GetProperty("id").GetGuid() == remaining)
                    Assert.Equal("Acknowledged", item.GetProperty("state").GetString());
            }
            cursor = json.GetProperty("nextCursor").GetString();
        }
        Assert.Equal(2, seen.Count);
        Assert.DoesNotContain(later, seen);
        using (var swapped = await manager.GetAsync(Collection(s.Fixture.A) +
            "?limit=1&state=Open&cursor=" + Uri.EscapeDataString(token!)))
            Assert.Equal(HttpStatusCode.Conflict, swapped.StatusCode);
        using (var foreign = await manager.GetAsync(Collection(s.Fixture.B) +
            "?limit=1&cursor=" + Uri.EscapeDataString(token!)))
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}
