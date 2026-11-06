using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Api.Audit;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.Shipments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

/// <summary>
/// M10/G16 combined verification under the real PostgreSQL identities. Every connection below
/// logs in as harborops_api, harborops_worker or harborops_projector, never as the migrator,
/// so privilege gaps in the SECURITY DEFINER triggers and the M10 grants surface as errors.
/// </summary>
public sealed class M10RuntimeIdentityTests
{
    private sealed record Roles(string Admin, string Api, string Worker, string Projector);

    private static string RepositoryFile(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException(relative);
    }

    private static async Task<Roles> ProvisionAsync()
    {
        var admin = TestDatabase.ConnectionString();
        await using var db = Context(admin);
        await db.Database.MigrateAsync();
        var apiSecret = "api-" + Guid.NewGuid().ToString("N");
        var workerSecret = "worker-" + Guid.NewGuid().ToString("N");
        var projectorSecret = "projector-" + Guid.NewGuid().ToString("N");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AuditRoles:ApiPassword"] = apiSecret,
            ["AuditRoles:WorkerPassword"] = workerSecret
        }).Build();
        await AuditRoleProvisioner.ProvisionAsync(db, configuration, CancellationToken.None);

        // Run the committed projector provisioning script itself, with only the database name
        // retargeted, so this test verifies the shipped grants and not a private copy.
        var database = new NpgsqlConnectionStringBuilder(admin).Database!;
        var script = (await File.ReadAllTextAsync(
                RepositoryFile("scripts/configure-m10-projector-db.sql")))
            .Replace("ON DATABASE harborops TO", $"ON DATABASE \"{database}\" TO",
                StringComparison.Ordinal);
        Assert.Contains($"\"{database}\"", script);
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await using var apply = new NpgsqlCommand(script, connection);
            await apply.ExecuteNonQueryAsync();
            await using var quote = new NpgsqlCommand("SELECT quote_literal(@s)", connection);
            quote.Parameters.AddWithValue("s", projectorSecret);
            var literal = (string)(await quote.ExecuteScalarAsync())!;
            await using var password = new NpgsqlCommand(
                "ALTER ROLE harborops_projector WITH PASSWORD " + literal, connection);
            await password.ExecuteNonQueryAsync();
        }
        string For(string role, string secret) => new NpgsqlConnectionStringBuilder(admin)
        { Username = role, Password = secret, Pooling = false }.ConnectionString;
        return new Roles(admin, For("harborops_api", apiSecret),
            For("harborops_worker", workerSecret), For("harborops_projector", projectorSecret));
    }

    private static HarborOpsDbContext Context(string connection) => new(
        new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(connection).Options);

    private static async Task<object?> Scalar(string connection, string sql,
        params (string, object)[] values)
    {
        await using var direct = new NpgsqlConnection(connection);
        await direct.OpenAsync();
        await using var command = new NpgsqlCommand(sql, direct);
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        return await command.ExecuteScalarAsync();
    }

    private static async Task<long> Count(string connection, string sql,
        params (string, object)[] values) => Convert.ToInt64(await Scalar(connection, sql, values));

    private static async Task<PostgresException> Denied(string connection, string sql,
        params (string, object)[] values)
    {
        await using var direct = new NpgsqlConnection(connection);
        await direct.OpenAsync();
        await using var command = new NpgsqlCommand(sql, direct);
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        return await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
    }

    private static async Task AssertPermissionDenied(string connection, string label, string sql,
        params (string, object)[] values)
    {
        var failure = await Denied(connection, sql, values);
        Assert.True(failure.SqlState == PostgresErrorCodes.InsufficientPrivilege,
            $"{label}: expected 42501 but got {failure.SqlState} {failure.MessageText}");
    }

    private static async Task<HttpResponseMessage> SendM10(HttpClient client, string path,
        object? payload = null, Guid? idempotencyKey = null)
    {
        using var csrf = await client.GetAsync("/api/v1/auth/csrf");
        csrf.EnsureSuccessStatusCode();
        var token = await csrf.Content.ReadFromJsonAsync<JsonElement>();
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
        request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",
            token.GetProperty("token").GetString());
        request.Headers.TryAddWithoutValidation("Idempotency-Key",
            (idempotencyKey ?? Guid.NewGuid()).ToString());
        if (payload is not null) request.Content = JsonContent.Create(payload);
        return await client.SendAsync(request);
    }

    private sealed record Seeded(Roles Roles, ReferenceDataHttpTests.ApiFactory Admin,
        ReferenceDataHttpTests.Fixture Fixture, G9Support.Resolvable Target,
        ProvisionedIntegrationSource Source, Guid Actor);

    private static async Task<Seeded> SeedAsync(string prefix)
    {
        var roles = await ProvisionAsync();
        var factory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        var target = await G9Support.SeedResolvable(factory, fixture);
        var source = await G9Support.NewSource(factory, fixture.A, prefix);
        Guid actor;
        await using (var lookup = Context(roles.Admin))
            actor = (await lookup.AuthUsers.AsNoTracking().SingleAsync(x =>
                x.NormalizedEmail == HarborOps.Infrastructure.Identity.IdentityStore
                    .NormalizeEmail(fixture.ManagerEmail))).Id;
        return new Seeded(roles, factory, fixture, target, source, actor);
    }

    private static async Task<Guid> AcceptAsync(Seeded s, string key,
        string occurredAt = "2026-11-03T10:00:00Z", string code = "GTIN") =>
        await G9Support.Accept(s.Admin, s.Source,
            G9Support.EquipmentPayload(key + "-" + Guid.NewGuid().ToString("N")[..10],
                s.Target.Reference, occurredAt, code));

    private static async Task<TrackingWorkDisposition> WorkerFinalizeOneAsync(Roles roles, Guid org)
    {
        await using var workerDb = Context(roles.Worker);
        var worker = new TrackingWorkStore(workerDb, new TrackingWorkerSettings());
        var claim = await worker.ClaimOneAsync(org, CancellationToken.None);
        Assert.NotNull(claim);
        return await worker.FinalizeAsync(claim!, CancellationToken.None);
    }

    private static async Task<bool> WaitForLockWait(string admin, string role, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await Count(admin, """
                SELECT COUNT(*) FROM pg_stat_activity
                WHERE usename=@role AND wait_event_type='Lock'
                """, ("role", role)) > 0) return true;
            await Task.Delay(50);
        }
        return false;
    }

    [Fact]
    public async Task RealWorkerIdentityFeedsTheM10IndexAndAuditCaptureThroughDefinerTriggers()
    {
        var s = await SeedAsync("g10w");
        using var _ = s.Admin;
        using var __ = s.Fixture.Admin;
        using var ___ = s.Fixture.Manager;
        using var ____ = s.Fixture.Analyst;
        var first = await AcceptAsync(s, "w-in");
        var second = await AcceptAsync(s, "w-out", "2026-11-03T11:00:00Z", "GTOT");
        var auditBefore = await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM audit_entries WHERE organization_id=@o", ("o", s.Fixture.A));
        Assert.Equal(2, await Count(s.Roles.Admin, """
            SELECT COUNT(*) FROM audit_entries
            WHERE organization_id=@o AND event_type='integration.tracking_accepted'
            """, ("o", s.Fixture.A)));

        Assert.Equal(TrackingWorkDisposition.Processed,
            await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));
        Assert.Equal(TrackingWorkDisposition.Processed,
            await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));

        // The definer triggers ran although the worker holds no M10 table rights.
        Assert.Equal(2, await Count(s.Roles.Admin, """
            SELECT COUNT(*) FROM shipment_tracking_timeline_entries
            WHERE organization_id=@o AND shipment_id=@s
            """, ("o", s.Fixture.A), ("s", s.Target.Shipment)));
        Assert.Equal(2, await Count(s.Roles.Admin, """
            SELECT input_revision FROM shipment_tracking_input_versions
            WHERE organization_id=@o AND shipment_id=@s
            """, ("o", s.Fixture.A), ("s", s.Target.Shipment)));
        // Accepted M16 contract: M9 worker effects are M9 history and never duplicate audit
        // rows. The M10 definer triggers must not add, alter or remove any audit entry either.
        Assert.Equal(auditBefore, await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM audit_entries WHERE organization_id=@o", ("o", s.Fixture.A)));
        Assert.Equal(0, await Count(s.Roles.Admin, """
            SELECT COUNT(*) FROM audit_entries
            WHERE organization_id=@o AND resource_type IN
                ('tracking_processing_effects','shipment_tracking_timeline_entries',
                 'shipment_tracking_input_versions')
            """, ("o", s.Fixture.A)));

        // No direct path for the worker into any M10 table.
        foreach (var table in new[] { "shipment_tracking_timeline_entries",
                     "shipment_tracking_input_versions", "shipment_tracking_correction_heads",
                     "shipment_tracking_corrections", "shipment_tracking_projections",
                     "shipment_projection_applications", "shipment_tracking_operation_replays" })
        {
            await AssertPermissionDenied(s.Roles.Worker, "worker select " + table,
                $"SELECT 1 FROM {table} LIMIT 1");
            await AssertPermissionDenied(s.Roles.Worker, "worker delete " + table,
                $"DELETE FROM {table}");
        }
        await AssertPermissionDenied(s.Roles.Worker, "worker forge input revision", """
            UPDATE shipment_tracking_input_versions SET input_revision=input_revision+1
            """);
        await AssertPermissionDenied(s.Roles.Worker, "worker execute projector lock",
            "SELECT m10_lock_projector_scope(@o,@s)", ("o", s.Fixture.A), ("s", s.Target.Shipment));
        Assert.NotEqual(Guid.Empty, first);
        Assert.NotEqual(Guid.Empty, second);
    }

    [Fact]
    public async Task RealApiIdentityHasOnlyTheScopedM10WritesAndTheDefinerTriggerRuns()
    {
        var s = await SeedAsync("g10a");
        using var _ = s.Admin;
        using var __ = s.Fixture.Admin;
        using var ___ = s.Fixture.Manager;
        using var ____ = s.Fixture.Analyst;
        var first = await AcceptAsync(s, "a-in");
        var second = await AcceptAsync(s, "a-out", "2026-11-03T11:00:00Z", "GTOT");
        for (var n = 0; n < 2; n++)
            Assert.Equal(TrackingWorkDisposition.Processed,
                await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));
        var revisionBefore = await Count(s.Roles.Admin, """
            SELECT input_revision FROM shipment_tracking_input_versions
            WHERE organization_id=@o AND shipment_id=@s
            """, ("o", s.Fixture.A), ("s", s.Target.Shipment));

        // Evidence tables and the M9/M16 evidence the API must never alter.
        foreach (var table in new[] { "shipment_tracking_timeline_entries",
                     "shipment_tracking_input_versions", "shipment_tracking_correction_heads" })
        {
            await AssertPermissionDenied(s.Roles.Api, "api update " + table,
                $"UPDATE {table} SET organization_id=organization_id");
            await AssertPermissionDenied(s.Roles.Api, "api delete " + table, $"DELETE FROM {table}");
            await AssertPermissionDenied(s.Roles.Api, "api truncate " + table, $"TRUNCATE {table}");
        }
        await AssertPermissionDenied(s.Roles.Api, "api forge head", """
            INSERT INTO shipment_tracking_correction_heads(organization_id,shipment_id,generation)
            VALUES (@o,@s,41)
            """, ("o", s.Fixture.A), ("s", s.Target.Shipment));
        await AssertPermissionDenied(s.Roles.Api, "api forge timeline", """
            INSERT INTO shipment_tracking_timeline_entries
                (receipt_id,organization_id,shipment_id,event_id,source_id,source_event_id,
                 event_code,occurred_at_utc,received_at_utc,container_id)
            SELECT receipt_id,organization_id,shipment_id,receipt_id,source_id,'x','GTIN',
                   clock_timestamp(),clock_timestamp(),container_id
            FROM tracking_processing_effects WHERE receipt_id=@r
            """, ("r", first));
        await AssertPermissionDenied(s.Roles.Api, "api forge M9 effect", """
            INSERT INTO tracking_processing_effects
                (receipt_id,organization_id,source_id,resolution_kind,resolution_policy,
                 container_id,shipment_id,resolved_at_utc)
            SELECT gen_random_uuid(),organization_id,source_id,resolution_kind,resolution_policy,
                   container_id,shipment_id,clock_timestamp()
            FROM tracking_processing_effects WHERE receipt_id=@r
            """, ("r", first));
        await AssertPermissionDenied(s.Roles.Api, "api audit insert", """
            INSERT INTO audit_entries SELECT * FROM audit_entries LIMIT 1
            """);
        await AssertPermissionDenied(s.Roles.Api, "api execute projector lock",
            "SELECT m10_lock_projector_scope(@o,@s)", ("o", s.Fixture.A), ("s", s.Target.Shipment));

        // Cross-tenant: organization B cannot correct a shipment that belongs to organization A.
        var crossTenant = await Denied(s.Roles.Api, """
            INSERT INTO shipment_tracking_corrections
                (id,organization_id,shipment_id,target_receipt_id,replacement_receipt_id,
                 generation,actor_user_id,reason,correlation_id,correction_digest)
            VALUES (gen_random_uuid(),@b,@s,@t,NULL,1,@u,'Cross tenant forgery probe','probe',@d)
            """, ("b", s.Fixture.B), ("s", s.Target.Shipment), ("t", first),
            ("u", s.Actor), ("d", new string('b', 64)));
        Assert.Contains(crossTenant.SqlState, new[] { PostgresErrorCodes.CheckViolation,
            PostgresErrorCodes.ForeignKeyViolation });
        Assert.Equal(0, await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM shipment_tracking_corrections WHERE organization_id=@b",
            ("b", s.Fixture.B)));

        // Allowed: a valid scoped correction. The SECURITY DEFINER trigger advances the head
        // and the input revision although the API holds no rights on either table.
        await using (var direct = new NpgsqlConnection(s.Roles.Api))
        {
            await direct.OpenAsync();
            await using var insert = new NpgsqlCommand("""
                INSERT INTO shipment_tracking_corrections
                    (id,organization_id,shipment_id,target_receipt_id,replacement_receipt_id,
                     generation,actor_user_id,reason,correlation_id,correction_digest)
                VALUES (gen_random_uuid(),@o,@s,@t,NULL,1,@u,'Confirmed invalid observation','api-id',@d)
                """, direct);
            insert.Parameters.AddWithValue("o", s.Fixture.A);
            insert.Parameters.AddWithValue("s", s.Target.Shipment);
            insert.Parameters.AddWithValue("t", first);
            insert.Parameters.AddWithValue("u", s.Actor);
            insert.Parameters.AddWithValue("d", new string('c', 64));
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());
        }
        Assert.Equal(1, await Count(s.Roles.Admin, """
            SELECT generation FROM shipment_tracking_correction_heads
            WHERE organization_id=@o AND shipment_id=@s
            """, ("o", s.Fixture.A), ("s", s.Target.Shipment)));
        Assert.Equal(revisionBefore + 1, await Count(s.Roles.Admin, """
            SELECT input_revision FROM shipment_tracking_input_versions
            WHERE organization_id=@o AND shipment_id=@s
            """, ("o", s.Fixture.A), ("s", s.Target.Shipment)));

        // Retained corrections stay immutable for the API role at privilege level.
        await AssertPermissionDenied(s.Roles.Api, "api update correction",
            "UPDATE shipment_tracking_corrections SET reason='Tampered after the fact'");
        await AssertPermissionDenied(s.Roles.Api, "api delete correction",
            "DELETE FROM shipment_tracking_corrections");
        await AssertPermissionDenied(s.Roles.Api, "api update replay",
            "UPDATE shipment_tracking_operation_replays SET response_revision=0");
        await AssertPermissionDenied(s.Roles.Api, "api delete replay",
            "DELETE FROM shipment_tracking_operation_replays");
        Assert.NotEqual(Guid.Empty, second);
    }

    [Fact]
    public async Task RealProjectorIdentityCannotMutateM8M9M16EvidenceOrOtherScopes()
    {
        var s = await SeedAsync("g10p");
        using var _ = s.Admin;
        using var __ = s.Fixture.Admin;
        using var ___ = s.Fixture.Manager;
        using var ____ = s.Fixture.Analyst;
        var first = await AcceptAsync(s, "p-in");
        Assert.Equal(TrackingWorkDisposition.Processed,
            await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));

        Assert.Equal("projector", await Scalar(s.Roles.Projector,
            "SELECT 'projector' WHERE session_user='harborops_projector'") as string);
        Assert.Equal(true, await Scalar(s.Roles.Projector,
            "SELECT m10_lock_projector_scope(@o,@s)", ("o", s.Fixture.A), ("s", s.Target.Shipment)));
        // The definer function refuses any session that is not the projector.
        Assert.Equal(false, await Scalar(s.Roles.Admin,
            "SELECT m10_lock_projector_scope(@o,@s)", ("o", s.Fixture.A), ("s", s.Target.Shipment)));
        // Unknown scope is reported as not lockable, never as success.
        Assert.Equal(false, await Scalar(s.Roles.Projector,
            "SELECT m10_lock_projector_scope(@o,@s)", ("o", s.Fixture.A), ("s", Guid.NewGuid())));

        foreach (var table in new[] { "tracking_processing_effects", "tracking_accepted_events",
                     "tracking_inbox_receipts", "tracking_inbox_work_items",
                     "tracking_work_attempt_events", "shipment_tracking_timeline_entries",
                     "shipment_tracking_correction_heads", "shipment_tracking_input_versions",
                     "shipment_tracking_corrections", "organizations", "shipments" })
        {
            await AssertPermissionDenied(s.Roles.Projector, "projector delete " + table,
                $"DELETE FROM {table}");
            var column = table is "organizations" or "shipments" ? "id" : "organization_id";
            await AssertPermissionDenied(s.Roles.Projector, "projector update " + table,
                $"UPDATE {table} SET {column}={column}");
        }
        await AssertPermissionDenied(s.Roles.Projector, "projector insert effect", """
            INSERT INTO tracking_processing_effects
                (receipt_id,organization_id,source_id,resolution_kind,resolution_policy,
                 container_id,shipment_id,resolved_at_utc)
            SELECT gen_random_uuid(),organization_id,source_id,resolution_kind,resolution_policy,
                   container_id,shipment_id,clock_timestamp()
            FROM tracking_processing_effects LIMIT 1
            """);
        await AssertPermissionDenied(s.Roles.Projector, "projector reads audit", "SELECT 1 FROM audit_entries LIMIT 1");
        await AssertPermissionDenied(s.Roles.Projector, "projector reads work queue",
            "SELECT 1 FROM tracking_inbox_work_items LIMIT 1");
        await AssertPermissionDenied(s.Roles.Projector, "projector reads credentials",
            "SELECT 1 FROM integration_sources LIMIT 1");
        await AssertPermissionDenied(s.Roles.Projector, "projector delete projection",
            "DELETE FROM shipment_tracking_projections");
        await AssertPermissionDenied(s.Roles.Projector, "projector update application",
            "UPDATE shipment_projection_applications SET organization_id=organization_id");

        // Allowed: the materialized projection and the application record only.
        await using (var direct = new NpgsqlConnection(s.Roles.Projector))
        {
            await direct.OpenAsync();
            await using var insert = new NpgsqlCommand("""
                INSERT INTO shipment_tracking_projections
                    (shipment_id,organization_id,observed_state,input_revision,input_digest,policy_version)
                VALUES (@s,@o,'EquipmentIn',1,@d,'probe');
                INSERT INTO shipment_projection_applications
                    (receipt_id,organization_id,shipment_id,policy_version)
                VALUES (@r,@o,@s,'probe');
                UPDATE shipment_tracking_projections SET observed_state='Unknown' WHERE shipment_id=@s;
                """, direct);
            insert.Parameters.AddWithValue("s", s.Target.Shipment);
            insert.Parameters.AddWithValue("o", s.Fixture.A);
            insert.Parameters.AddWithValue("d", new string('d', 64));
            insert.Parameters.AddWithValue("r", first);
            await insert.ExecuteNonQueryAsync();
        }
        // Even through the projector, an application for a different shipment is refused.
        var mismatched = await Denied(s.Roles.Projector, """
            INSERT INTO shipment_projection_applications
                (receipt_id,organization_id,shipment_id,policy_version)
            VALUES (@r,@o,@wrong,'probe2')
            """, ("r", first), ("o", s.Fixture.A), ("wrong", Guid.NewGuid()));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, mismatched.SqlState);
    }

    [Fact]
    public async Task M10HttpRoutesRunUnderTheRestrictedApiRoleWithManagerAndTenantGuards()
    {
        var s = await SeedAsync("g10h");
        using var _ = s.Admin;
        using var __ = s.Fixture.Admin;
        using var ___ = s.Fixture.Manager;
        using var ____ = s.Fixture.Analyst;
        var first = await AcceptAsync(s, "h-in");
        var second = await AcceptAsync(s, "h-out", "2026-11-03T11:00:00Z", "GTOT");
        for (var n = 0; n < 2; n++)
            Assert.Equal(TrackingWorkDisposition.Processed,
                await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));

        using var apiFactory = new ReferenceDataHttpTests.ApiFactory(s.Roles.Api);
        using var manager = apiFactory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var analyst = apiFactory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(manager, s.Fixture.ManagerEmail);
        await ReferenceDataHttpTests.Login(analyst, s.Fixture.AnalystEmail);
        await ReferenceDataHttpTests.Select(manager, s.Fixture.A);
        await ReferenceDataHttpTests.Select(analyst, s.Fixture.A);
        var root = $"/api/v1/organizations/{s.Fixture.A}/shipments/{s.Target.Shipment}";

        using (var read = await analyst.GetAsync(root + "/tracking-projection"))
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using (var events = await analyst.GetAsync(root + "/events?limit=1"))
        {
            Assert.Equal(HttpStatusCode.OK, events.StatusCode);
            Assert.Equal(2, (await events.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("eventCount").GetInt32());
        }
        using (var forbidden = await SendM10(analyst, root + "/tracking-projection/rebuild"))
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        // The write path under the restricted identity: projection upsert, application rows,
        // replay record and locking must all succeed without migrator privileges.
        var rebuildKey = Guid.NewGuid();
        using (var rebuild = await SendM10(manager, root + "/tracking-projection/rebuild",
            idempotencyKey: rebuildKey))
            Assert.Equal(HttpStatusCode.OK, rebuild.StatusCode);
        using (var rebuildReplay = await SendM10(manager, root + "/tracking-projection/rebuild",
            idempotencyKey: rebuildKey))
        {
            Assert.Equal(HttpStatusCode.OK, rebuildReplay.StatusCode);
            Assert.True((await rebuildReplay.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("replayed").GetBoolean());
        }
        var correction = new
        {
            targetReceiptId = first, replacementReceiptId = (Guid?)null,
            expectedGeneration = 0, reason = "Confirmed invalid first observation"
        };
        var correctionKey = Guid.NewGuid();
        using (var applied = await SendM10(manager, root + "/tracking-corrections", correction, correctionKey))
            Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        using (var replayed = await SendM10(manager, root + "/tracking-corrections", correction, correctionKey))
            Assert.True((await replayed.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("replayed").GetBoolean());
        // Same key with a different payload is a collision, never a silent success.
        using (var collision = await SendM10(manager, root + "/tracking-corrections",
            new { targetReceiptId = second, replacementReceiptId = (Guid?)null,
                  expectedGeneration = 1, reason = "Different payload with reused key" },
            correctionKey))
            Assert.Equal(HttpStatusCode.Conflict, collision.StatusCode);
        // Tenant boundary: a manager of A cannot reach the same shipment through organization B.
        using (var foreign = await SendM10(manager,
            $"/api/v1/organizations/{s.Fixture.B}/shipments/{s.Target.Shipment}/tracking-projection/rebuild"))
            Assert.NotEqual(HttpStatusCode.OK, foreign.StatusCode);
        // Missing CSRF and a hostile Origin are refused before any database write.
        using (var noCsrf = new HttpRequestMessage(HttpMethod.Post, root + "/tracking-projection/rebuild"))
        {
            noCsrf.Headers.TryAddWithoutValidation("Origin", "http://localhost");
            noCsrf.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());
            using var response = await manager.SendAsync(noCsrf);
            Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden,
                "CSRF guard returned " + response.StatusCode);
        }
        using (var csrf = await manager.GetAsync("/api/v1/auth/csrf"))
        {
            var token = (await csrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
            using var hostile = new HttpRequestMessage(HttpMethod.Post, root + "/tracking-projection/rebuild");
            hostile.Headers.TryAddWithoutValidation("Origin", "http://evil.example");
            hostile.Headers.TryAddWithoutValidation("X-HarborOps-CSRF", token);
            hostile.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());
            using var response = await manager.SendAsync(hostile);
            Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden,
                "Origin guard returned " + response.StatusCode);
        }
        Assert.Equal(1, await Count(s.Roles.Admin, """
            SELECT generation FROM shipment_tracking_correction_heads
            WHERE organization_id=@o AND shipment_id=@s
            """, ("o", s.Fixture.A), ("s", s.Target.Shipment)));
    }

    [Fact]
    public async Task WorkerFinalizationBlocksBehindProjectorAndCorrectionLocksWithoutDeadlock()
    {
        var s = await SeedAsync("g10l");
        using var _ = s.Admin;
        using var __ = s.Fixture.Admin;
        using var ___ = s.Fixture.Manager;
        using var ____ = s.Fixture.Analyst;
        var first = await AcceptAsync(s, "l-1");
        var second = await AcceptAsync(s, "l-2", "2026-11-03T11:00:00Z", "GTOT");
        var third = await AcceptAsync(s, "l-3", "2026-11-03T12:00:00Z", "GTIN");
        // Intake shares the organization lock, so every event is accepted before a lock is held.
        var fourth = await AcceptAsync(s, "l-4", "2026-11-03T13:00:00Z", "GTOT");
        Assert.Equal(TrackingWorkDisposition.Processed,
            await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));

        // Round 1: the real projector identity holds the organization and shipment locks.
        TrackingWorkDisposition blockedByProjector;
        await using (var projector = new NpgsqlConnection(s.Roles.Projector))
        {
            await projector.OpenAsync();
            await using var transaction = await projector.BeginTransactionAsync();
            await using var lockCommand = new NpgsqlCommand(
                "SELECT m10_lock_projector_scope(@o,@s)", projector, transaction);
            lockCommand.Parameters.AddWithValue("o", s.Fixture.A);
            lockCommand.Parameters.AddWithValue("s", s.Target.Shipment);
            Assert.Equal(true, await lockCommand.ExecuteScalarAsync());
            var workerTask = Task.Run(() => WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));
            Assert.True(await WaitForLockWait(s.Roles.Admin, "harborops_worker", TimeSpan.FromSeconds(30)),
                "The M9 worker did not wait behind the projector lock");
            Assert.False(workerTask.IsCompleted);
            await transaction.CommitAsync();
            blockedByProjector = await workerTask.WaitAsync(TimeSpan.FromSeconds(60));
        }
        Assert.Equal(TrackingWorkDisposition.Processed, blockedByProjector);

        // Round 2: an uncommitted API correction blocks the worker; releasing it lets both commit.
        TrackingWorkDisposition blockedByCorrection;
        await using (var api = new NpgsqlConnection(s.Roles.Api))
        {
            await api.OpenAsync();
            await using var transaction = await api.BeginTransactionAsync();
            await using var insert = new NpgsqlCommand("""
                INSERT INTO shipment_tracking_corrections
                    (id,organization_id,shipment_id,target_receipt_id,replacement_receipt_id,
                     generation,actor_user_id,reason,correlation_id,correction_digest)
                VALUES (gen_random_uuid(),@o,@s,@t,NULL,1,@u,'Confirmed invalid observation','lock',@d)
                """, api, transaction);
            insert.Parameters.AddWithValue("o", s.Fixture.A);
            insert.Parameters.AddWithValue("s", s.Target.Shipment);
            insert.Parameters.AddWithValue("t", first);
            insert.Parameters.AddWithValue("u", s.Actor);
            insert.Parameters.AddWithValue("d", new string('e', 64));
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());
            var workerTask = Task.Run(() => WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));
            Assert.True(await WaitForLockWait(s.Roles.Admin, "harborops_worker", TimeSpan.FromSeconds(30)),
                "The M9 worker did not wait behind the uncommitted correction");
            Assert.False(workerTask.IsCompleted);
            await transaction.CommitAsync();
            blockedByCorrection = await workerTask.WaitAsync(TimeSpan.FromSeconds(60));
            Assert.NotEqual(Guid.Empty, fourth);
        }
        Assert.Equal(TrackingWorkDisposition.Processed, blockedByCorrection);

        // Final state is exactly the sum of the committed inputs: no lost or doubled revision.
        Assert.Equal(3, await Count(s.Roles.Admin, """
            SELECT COUNT(*) FROM shipment_tracking_timeline_entries
            WHERE organization_id=@o AND shipment_id=@s
            """, ("o", s.Fixture.A), ("s", s.Target.Shipment)));
        Assert.Equal(4, await Count(s.Roles.Admin, """
            SELECT input_revision FROM shipment_tracking_input_versions
            WHERE organization_id=@o AND shipment_id=@s
            """, ("o", s.Fixture.A), ("s", s.Target.Shipment)));
        Assert.Equal(3, await Count(s.Roles.Admin, """
            SELECT event_count FROM shipment_tracking_input_versions
            WHERE organization_id=@o AND shipment_id=@s
            """, ("o", s.Fixture.A), ("s", s.Target.Shipment)));
        Assert.NotEqual(Guid.Empty, second);
        Assert.NotEqual(Guid.Empty, third);
    }

    [Fact]
    public async Task RealProjectorIdentityDiscoversAndRebuildsADirtyScopeThroughTheStore()
    {
        var s = await SeedAsync("g10r");
        using var _ = s.Admin;
        using var __ = s.Fixture.Admin;
        using var ___ = s.Fixture.Manager;
        using var ____ = s.Fixture.Analyst;
        await AcceptAsync(s, "r-1");
        await AcceptAsync(s, "r-2", "2026-11-03T11:00:00Z", "GTOT");
        for (var n = 0; n < 2; n++)
            Assert.Equal(TrackingWorkDisposition.Processed,
                await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));

        await using var db = Context(s.Roles.Projector);
        var store = new ShipmentTimelineStore(db, restrictedProjector: true);
        var dirty = await store.DirtyAsync(50, CancellationToken.None);
        Assert.Contains(dirty, d => d.Organization == s.Fixture.A && d.Shipment == s.Target.Shipment);
        var rebuilt = await store.RebuildAsync(s.Fixture.A, s.Target.Shipment, CancellationToken.None);
        Assert.Equal(TrackingMutationResult.Applied, rebuilt.Result);
        Assert.Equal(1L, await Scalar(s.Roles.Admin, """
            SELECT COUNT(*) FROM shipment_tracking_projections
            WHERE shipment_id=@s AND observed_state='EquipmentOut' AND event_count=2
            """, ("s", s.Target.Shipment)));
        // Once materialized at the current revision the scope is no longer dirty.
        var after = await store.DirtyAsync(50, CancellationToken.None);
        Assert.DoesNotContain(after, d => d.Shipment == s.Target.Shipment);
    }

    [Fact]
    public async Task LargeHistoryUsesTheKeysetIndexPagesCompletelyAndRebuildsWithinBounds()
    {
        const int Bulk = 12_000;
        var s = await SeedAsync("g10x");
        using var _ = s.Admin;
        using var __ = s.Fixture.Admin;
        using var manager = s.Fixture.Manager;
        using var analyst = s.Fixture.Analyst;
        var template = await AcceptAsync(s, "x-template");
        Assert.Equal(TrackingWorkDisposition.Processed,
            await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));

        // Historical-style bulk load: triggers are bypassed for this controlled seed only, and
        // every row mirrors the real template chain (receipt, event, work item, effect, index).
        await using (var seed = new NpgsqlConnection(s.Roles.Admin))
        {
            await seed.OpenAsync();
            await using var transaction = await seed.BeginTransactionAsync();
            await using var command = new NpgsqlCommand($$"""
                SET LOCAL session_replication_role = replica;
                CREATE TEMP TABLE bulk_ids ON COMMIT DROP AS
                    SELECT g AS n, gen_random_uuid() AS rid, gen_random_uuid() AS eid
                    FROM generate_series(1,{{Bulk}}) g;
                INSERT INTO tracking_inbox_receipts
                    (id,organization_id,source_id,credential_id,profile,source_event_id,
                     canonical_digest,canonical_payload_utf8,received_at_utc,correlation_id)
                SELECT b.rid,r.organization_id,r.source_id,r.credential_id,r.profile,
                       'bulk-'||b.n,sha256(convert_to('bulk-'||b.n,'UTF8')),
                       r.canonical_payload_utf8,
                       timestamptz '2026-11-04 00:00:00+00'+(b.n||' seconds')::interval,'bulk'
                FROM bulk_ids b CROSS JOIN tracking_inbox_receipts r WHERE r.id=@t;
                INSERT INTO tracking_accepted_events
                    (id,receipt_id,organization_id,source_id,event_family,event_code,classifier,
                     external_schema_version,occurred_at_utc,equipment_reference,
                     transport_reference,raw_payload_utf8,canonical_payload)
                SELECT b.eid,b.rid,e.organization_id,e.source_id,e.event_family,
                       CASE WHEN b.n%2=0 THEN 'GTOT' ELSE 'GTIN' END,e.classifier,
                       e.external_schema_version,
                       timestamptz '2026-11-03 12:00:00+00'+((b.n/3)||' seconds')::interval,
                       e.equipment_reference,e.transport_reference,e.raw_payload_utf8,
                       e.canonical_payload
                FROM bulk_ids b CROSS JOIN tracking_accepted_events e WHERE e.receipt_id=@t;
                INSERT INTO tracking_inbox_work_items
                SELECT b.rid,w.organization_id,w.source_id,w.state,w.attempt_count,
                       w.created_at_utc,w.updated_at_utc,w.lease_token,w.lease_epoch,
                       w.lease_expires_at_utc,w.available_at_utc,w.redrive_generation,
                       w.last_error_code,w.completed_at_utc,w.resolution_kind
                FROM bulk_ids b CROSS JOIN tracking_inbox_work_items w WHERE w.receipt_id=@t;
                INSERT INTO tracking_processing_effects
                SELECT b.rid,fx.organization_id,fx.source_id,fx.resolution_kind,
                       fx.resolution_policy,fx.container_id,fx.shipment_id,fx.resolved_at_utc
                FROM bulk_ids b CROSS JOIN tracking_processing_effects fx WHERE fx.receipt_id=@t;
                INSERT INTO shipment_tracking_timeline_entries
                    (receipt_id,organization_id,shipment_id,event_id,source_id,source_event_id,
                     event_code,occurred_at_utc,received_at_utc,container_id)
                SELECT fx.receipt_id,fx.organization_id,fx.shipment_id,e.id,e.source_id,
                       r.source_event_id,e.event_code,e.occurred_at_utc,r.received_at_utc,
                       fx.container_id
                FROM tracking_processing_effects fx
                JOIN tracking_accepted_events e ON e.receipt_id=fx.receipt_id
                JOIN tracking_inbox_receipts r ON r.id=e.receipt_id
                WHERE fx.receipt_id IN (SELECT rid FROM bulk_ids);
                UPDATE shipment_tracking_input_versions
                   SET input_revision=input_revision+{{Bulk}},event_count=event_count+{{Bulk}}
                 WHERE organization_id=@o AND shipment_id=@s;
                """, seed, transaction) { CommandTimeout = 120 };
            command.Parameters.AddWithValue("t", template);
            command.Parameters.AddWithValue("o", s.Fixture.A);
            command.Parameters.AddWithValue("s", s.Target.Shipment);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        var total = Bulk + 1;
        Assert.Equal(total, await Count(s.Roles.Admin, """
            SELECT COUNT(*) FROM shipment_tracking_timeline_entries
            WHERE organization_id=@o AND shipment_id=@s
            """, ("o", s.Fixture.A), ("s", s.Target.Shipment)));
        await Scalar(s.Roles.Admin, "ANALYZE shipment_tracking_timeline_entries");

        // The bounded page must be an index range scan with the ordering supplied by the index.
        var anchor = await Scalar(s.Roles.Admin, $"""
            SELECT concat_ws('|',occurred_at_utc::text,received_at_utc::text,source_id::text,
                             source_event_id,receipt_id::text,event_id::text)
            FROM shipment_tracking_timeline_entries
            WHERE organization_id='{s.Fixture.A}' AND shipment_id='{s.Target.Shipment}'
            ORDER BY occurred_at_utc,received_at_utc,source_id,source_event_id COLLATE "C",
                     receipt_id,event_id OFFSET 6000 LIMIT 1
            """) as string;
        var p = anchor!.Split('|');
        var plan = new System.Text.StringBuilder();
        await using (var explain = new NpgsqlConnection(s.Roles.Admin))
        {
            await explain.OpenAsync();
            await using var command = new NpgsqlCommand($"""
                EXPLAIN (ANALYZE, BUFFERS)
                SELECT e.event_id,e.receipt_id,e.source_id,e.source_event_id,e.event_code,
                       e.occurred_at_utc,e.received_at_utc,c.id IS NOT NULL,
                       c.replacement_receipt_id,e.container_id,c.id
                FROM shipment_tracking_timeline_entries e
                LEFT JOIN shipment_tracking_corrections c
                  ON c.organization_id=e.organization_id AND c.shipment_id=e.shipment_id
                 AND c.target_receipt_id=e.receipt_id
                WHERE e.organization_id='{s.Fixture.A}' AND e.shipment_id='{s.Target.Shipment}'
                  AND (e.occurred_at_utc,e.received_at_utc,e.source_id,
                       e.source_event_id COLLATE "C",e.receipt_id,e.event_id)
                    > ('{p[0]}'::timestamptz,'{p[1]}'::timestamptz,'{p[2]}'::uuid,
                       '{p[3]}' COLLATE "C",'{p[4]}'::uuid,'{p[5]}'::uuid)
                ORDER BY e.occurred_at_utc,e.received_at_utc,e.source_id,
                         e.source_event_id COLLATE "C",e.receipt_id,e.event_id
                LIMIT 101
                """, explain);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) plan.AppendLine(reader.GetString(0));
        }
        var planText = plan.ToString();
        Assert.Contains("ix_m10_timeline_keyset", planText);
        Assert.DoesNotContain("Sort", planText);
        Assert.DoesNotContain("Seq Scan on shipment_tracking_timeline_entries", planText);

        // Walk the complete history over HTTP and compare with the SQL canonical order.
        var root = $"/api/v1/organizations/{s.Fixture.A}/shipments/{s.Target.Shipment}";
        var received = new List<Guid>();
        var latencies = new List<double>();
        string? cursor = null, firstCursor = null;
        var watch = new System.Diagnostics.Stopwatch();
        do
        {
            watch.Restart();
            using var response = await analyst.GetAsync(root + "/events?limit=100" +
                (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)));
            watch.Stop();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            latencies.Add(watch.Elapsed.TotalMilliseconds);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(total, body.GetProperty("eventCount").GetInt64());
            foreach (var item in body.GetProperty("items").EnumerateArray())
                received.Add(item.GetProperty("receiptId").GetGuid());
            cursor = body.GetProperty("nextCursor").ValueKind == JsonValueKind.Null
                ? null : body.GetProperty("nextCursor").GetString();
            firstCursor ??= cursor;
        } while (cursor is not null);
        Assert.Equal(total, received.Count);
        Assert.Equal(total, received.Distinct().Count());
        var expected = new List<Guid>();
        await using (var order = new NpgsqlConnection(s.Roles.Admin))
        {
            await order.OpenAsync();
            await using var command = new NpgsqlCommand("""
                SELECT receipt_id FROM shipment_tracking_timeline_entries
                WHERE organization_id=@o AND shipment_id=@s
                ORDER BY occurred_at_utc,received_at_utc,source_id,source_event_id COLLATE "C",
                         receipt_id,event_id
                """, order);
            command.Parameters.AddWithValue("o", s.Fixture.A);
            command.Parameters.AddWithValue("s", s.Target.Shipment);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) expected.Add(reader.GetGuid(0));
        }
        Assert.Equal(expected, received);
        latencies.Sort();
        var p95 = latencies[(int)(latencies.Count * 0.95)];
        Assert.True(latencies[^1] < 5000, $"slowest page {latencies[^1]:F0} ms");

        // Rebuild streams the complete retained history under a bounded time and memory budget.
        var process = System.Diagnostics.Process.GetCurrentProcess();
        process.Refresh();
        var memoryBefore = process.PrivateMemorySize64;
        watch.Restart();
        using (var rebuild = await SendM10(manager, root + "/tracking-projection/rebuild"))
        {
            watch.Stop();
            Assert.Equal(HttpStatusCode.OK, rebuild.StatusCode);
        }
        process.Refresh();
        var memoryAfter = process.PrivateMemorySize64;
        var rebuildMs = watch.Elapsed.TotalMilliseconds;
        Assert.True(rebuildMs < 60_000, $"rebuild took {rebuildMs:F0} ms");
        Assert.True(memoryAfter - memoryBefore < 512L * 1024 * 1024,
            $"rebuild grew private memory by {(memoryAfter - memoryBefore) / 1024 / 1024} MiB");
        using (var read = await analyst.GetAsync(root + "/tracking-projection"))
        {
            var body = await read.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(total, body.GetProperty("eventCount").GetInt64());
            Assert.False(body.GetProperty("materializationStale").GetBoolean());
            Assert.Equal("EquipmentOut", body.GetProperty("observedTrackingState").GetString());
        }

        // A new M9 effect invalidates cursors issued before it.
        await AcceptAsync(s, "x-late", "2026-11-03T09:30:00Z", "GTIN");
        Assert.Equal(TrackingWorkDisposition.Processed,
            await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));
        using (var stale = await analyst.GetAsync(root + "/events?limit=100&cursor=" +
            Uri.EscapeDataString(firstCursor!)))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var fresh = await analyst.GetAsync(root + "/events?limit=100"))
        {
            var body = await fresh.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(total + 1, body.GetProperty("eventCount").GetInt64());
            Assert.True(body.GetProperty("materializationStale").GetBoolean());
        }

        var evidence = Environment.GetEnvironmentVariable("HARBOROPS_G10_PERF_OUT");
        if (!string.IsNullOrWhiteSpace(evidence))
            await File.AppendAllTextAsync(evidence,
                $"large-history events={total} pages={latencies.Count} page_p95_ms={p95:F1} " +
                $"page_max_ms={latencies[^1]:F1} rebuild_ms={rebuildMs:F0} " +
                $"rebuild_private_mem_delta_mib={(memoryAfter - memoryBefore) / 1024 / 1024}{Environment.NewLine}" +
                "-- plan --" + Environment.NewLine + planText + Environment.NewLine);
    }

    [Fact]
    public async Task M10RoutesHonorLiveRoleDowngradeRateLimitAndSessionRevocation()
    {
        var s = await SeedAsync("g10v");
        using var _ = s.Admin;
        using var admin = s.Fixture.Admin;
        using var manager = s.Fixture.Manager;
        using var analyst = s.Fixture.Analyst;
        await AcceptAsync(s, "v-1");
        Assert.Equal(TrackingWorkDisposition.Processed,
            await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));
        var root = $"/api/v1/organizations/{s.Fixture.A}/shipments/{s.Target.Shipment}";

        using (var allowed = await SendM10(manager, root + "/tracking-projection/rebuild"))
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        // A live role downgrade takes effect on the very next Manager-only request.
        async Task SetRole(string role)
        {
            await using var db = Context(s.Roles.Admin);
            var membership = await db.OrganizationMemberships.AsNoTracking().SingleAsync(m =>
                m.OrganizationId == s.Fixture.A && m.UserId == s.Actor);
            using var put = await ReferenceDataHttpTests.Send(admin, HttpMethod.Put,
                $"/api/v1/organizations/{s.Fixture.A}/memberships/{s.Actor}",
                new { role, status = "Active", expectedVersion = membership.Version });
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        }
        await SetRole("Analyst");
        using (var denied = await SendM10(manager, root + "/tracking-projection/rebuild"))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var deniedCorrection = await SendM10(manager, root + "/tracking-corrections",
            new { targetReceiptId = Guid.NewGuid(), replacementReceiptId = (Guid?)null,
                  expectedGeneration = 0, reason = "Downgraded manager attempt" }))
            Assert.Equal(HttpStatusCode.Forbidden, deniedCorrection.StatusCode);
        using (var stillReads = await manager.GetAsync(root + "/events?limit=1"))
            Assert.Equal(HttpStatusCode.OK, stillReads.StatusCode);
        Assert.Equal(0, await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM shipment_tracking_corrections WHERE organization_id=@o",
            ("o", s.Fixture.A)));
        await SetRole("Manager");

        // The mutation limiter (10 per minute per user) answers 429 without blocking readers.
        var statuses = new List<HttpStatusCode>();
        for (var n = 0; n < 25; n++)
        {
            using var response = await SendM10(manager, root + "/tracking-projection/rebuild");
            statuses.Add(response.StatusCode);
        }
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
        Assert.Contains(HttpStatusCode.OK, statuses);
        using (var reader = await analyst.GetAsync(root + "/events?limit=1"))
            Assert.Equal(HttpStatusCode.OK, reader.StatusCode);
        // Reads of other families keep working for the limited user.
        using (var limitedRead = await manager.GetAsync(root + "/tracking-projection"))
            Assert.Equal(HttpStatusCode.OK, limitedRead.StatusCode);

        // Revoking every session ends access immediately for reads and mutations.
        using (var revoke = await ReferenceDataHttpTests.Send(analyst, HttpMethod.Post,
            "/api/v1/auth/logout-all"))
            Assert.True(revoke.IsSuccessStatusCode, "logout-all " + revoke.StatusCode);
        using (var after = await analyst.GetAsync(root + "/events?limit=1"))
            Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        using (var afterProjection = await analyst.GetAsync(root + "/tracking-projection"))
            Assert.Equal(HttpStatusCode.Unauthorized, afterProjection.StatusCode);
    }

    private static async Task InstallStall(string admin, Guid shipment, int seconds)
    {
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($$"""
            CREATE OR REPLACE FUNCTION g10_test_stall() RETURNS trigger LANGUAGE plpgsql AS $f$
            BEGIN
                IF NEW.shipment_id='{{shipment}}'::uuid THEN PERFORM pg_sleep({{seconds}}); END IF;
                RETURN NEW;
            END $f$;
            CREATE TRIGGER g10_test_stall BEFORE INSERT ON shipment_projection_applications
                FOR EACH ROW EXECUTE FUNCTION g10_test_stall();
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RemoveStall(string admin)
    {
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            DROP TRIGGER IF EXISTS g10_test_stall ON shipment_projection_applications;
            DROP FUNCTION IF EXISTS g10_test_stall();
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int?> WaitForSleepingBackend(string admin, string role, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var pid = await Scalar(admin, """
                SELECT pid FROM pg_stat_activity
                WHERE usename=@role AND wait_event='PgSleep' LIMIT 1
                """, ("role", role));
            if (pid is int found) return found;
            await Task.Delay(50);
        }
        return null;
    }

    [Fact]
    public async Task ProjectorKilledMidRebuildRollsBackCompletelyAndTheRetryIsIdempotent()
    {
        var s = await SeedAsync("g10k");
        using var _ = s.Admin;
        using var __ = s.Fixture.Admin;
        using var ___ = s.Fixture.Manager;
        using var ____ = s.Fixture.Analyst;
        await AcceptAsync(s, "k-1");
        await AcceptAsync(s, "k-2", "2026-11-03T11:00:00Z", "GTOT");
        for (var n = 0; n < 2; n++)
            Assert.Equal(TrackingWorkDisposition.Processed,
                await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));
        var revision = await Count(s.Roles.Admin, """
            SELECT input_revision FROM shipment_tracking_input_versions
            WHERE organization_id=@o AND shipment_id=@s
            """, ("o", s.Fixture.A), ("s", s.Target.Shipment));

        await InstallStall(s.Roles.Admin, s.Target.Shipment, 30);
        try
        {
            await using var projectorDb = Context(s.Roles.Projector);
            var store = new ShipmentTimelineStore(projectorDb, restrictedProjector: true);
            var doomed = Task.Run(() => store.RebuildAsync(
                s.Fixture.A, s.Target.Shipment, CancellationToken.None));
            var pid = await WaitForSleepingBackend(s.Roles.Admin, "harborops_projector",
                TimeSpan.FromSeconds(30));
            Assert.NotNull(pid);   // the transaction is open and has already written the projection row
            Assert.Equal(true, await Scalar(s.Roles.Admin, "SELECT pg_terminate_backend(@p)",
                ("p", pid!.Value)));
            await Assert.ThrowsAnyAsync<Exception>(() => doomed);
        }
        finally { await RemoveStall(s.Roles.Admin); }

        // Crash before COMMIT: nothing leaked, source input untouched.
        Assert.Equal(0, await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM shipment_tracking_projections WHERE shipment_id=@s",
            ("s", s.Target.Shipment)));
        Assert.Equal(0, await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM shipment_projection_applications WHERE shipment_id=@s",
            ("s", s.Target.Shipment)));
        Assert.Equal(revision, await Count(s.Roles.Admin, """
            SELECT input_revision FROM shipment_tracking_input_versions
            WHERE organization_id=@o AND shipment_id=@s
            """, ("o", s.Fixture.A), ("s", s.Target.Shipment)));
        Assert.Equal(0, await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM pg_stat_activity WHERE usename='harborops_projector' AND state='idle in transaction'"));

        // Retry on a new connection: one application per receipt, and an identical replay is a no-op.
        await using var retryDb = Context(s.Roles.Projector);
        var retry = new ShipmentTimelineStore(retryDb, restrictedProjector: true);
        var first = await retry.RebuildAsync(s.Fixture.A, s.Target.Shipment, CancellationToken.None);
        Assert.Equal(TrackingMutationResult.Applied, first.Result);
        Assert.Equal(2, await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM shipment_projection_applications WHERE shipment_id=@s",
            ("s", s.Target.Shipment)));
        var again = await retry.RebuildAsync(s.Fixture.A, s.Target.Shipment, CancellationToken.None);
        Assert.Equal(first.Revision, again.Revision);
        Assert.Equal(2, await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM shipment_projection_applications WHERE shipment_id=@s",
            ("s", s.Target.Shipment)));
        Assert.Equal(1, await Count(s.Roles.Admin, """
            SELECT COUNT(*) FROM shipment_tracking_projections
            WHERE shipment_id=@s AND observed_state='EquipmentOut' AND event_count=2
            """, ("s", s.Target.Shipment)));
    }

    [Fact]
    public async Task LateWorkerEffectDuringRebuildIsNeverLostAndMarksTheProjectionStale()
    {
        var s = await SeedAsync("g10c");
        using var _ = s.Admin;
        using var __ = s.Fixture.Admin;
        using var ___ = s.Fixture.Manager;
        using var ____ = s.Fixture.Analyst;
        await AcceptAsync(s, "c-1");
        await AcceptAsync(s, "c-2", "2026-11-03T11:00:00Z", "GTOT");
        await AcceptAsync(s, "c-3", "2026-11-03T12:00:00Z", "GTIN");
        for (var n = 0; n < 2; n++)
            Assert.Equal(TrackingWorkDisposition.Processed,
                await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));

        await InstallStall(s.Roles.Admin, s.Target.Shipment, 3);
        TrackingWorkDisposition late;
        (TrackingMutationResult Result, long Revision) rebuilt;
        try
        {
            await using var projectorDb = Context(s.Roles.Projector);
            var store = new ShipmentTimelineStore(projectorDb, restrictedProjector: true);
            var rebuild = Task.Run(() => store.RebuildAsync(
                s.Fixture.A, s.Target.Shipment, CancellationToken.None));
            Assert.NotNull(await WaitForSleepingBackend(s.Roles.Admin, "harborops_projector",
                TimeSpan.FromSeconds(30)));
            // The rebuild transaction is open and holds the scope locks. The late M9 effect must wait.
            var worker = Task.Run(() => WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));
            Assert.True(await WaitForLockWait(s.Roles.Admin, "harborops_worker", TimeSpan.FromSeconds(30)),
                "The late M9 effect did not wait behind the rebuild");
            Assert.False(worker.IsCompleted);
            rebuilt = await rebuild.WaitAsync(TimeSpan.FromSeconds(60));
            late = await worker.WaitAsync(TimeSpan.FromSeconds(60));
        }
        finally { await RemoveStall(s.Roles.Admin); }

        Assert.Equal(TrackingMutationResult.Applied, rebuilt.Result);
        Assert.Equal(TrackingWorkDisposition.Processed, late);
        // The materialization covers exactly the two events it saw; the late effect is retained.
        Assert.Equal(2, await Count(s.Roles.Admin, """
            SELECT input_revision FROM shipment_tracking_projections WHERE shipment_id=@s
            """, ("s", s.Target.Shipment)));
        Assert.Equal(3, await Count(s.Roles.Admin, """
            SELECT input_revision FROM shipment_tracking_input_versions
            WHERE organization_id=@o AND shipment_id=@s
            """, ("o", s.Fixture.A), ("s", s.Target.Shipment)));
        await using var readDb = Context(s.Roles.Admin);
        var stale = await new ShipmentTimelineStore(readDb).ReadBoundedAsync(
            s.Fixture.A, s.Target.Shipment, CancellationToken.None);
        Assert.NotNull(stale);
        Assert.True(stale!.MaterializationStale, "a materialization older than the input must report stale");
        await using var sweepDb = Context(s.Roles.Projector);
        var sweep = new ShipmentTimelineStore(sweepDb, restrictedProjector: true);
        Assert.Contains(await sweep.DirtyAsync(50, CancellationToken.None),
            d => d.Shipment == s.Target.Shipment);
        // Recovery: the next rebuild includes the late effect and the scope is clean again.
        Assert.Equal(TrackingMutationResult.Applied,
            (await sweep.RebuildAsync(s.Fixture.A, s.Target.Shipment, CancellationToken.None)).Result);
        Assert.Equal(1, await Count(s.Roles.Admin, """
            SELECT COUNT(*) FROM shipment_tracking_projections
            WHERE shipment_id=@s AND input_revision=3 AND event_count=3
            """, ("s", s.Target.Shipment)));
        Assert.DoesNotContain(await sweep.DirtyAsync(50, CancellationToken.None),
            d => d.Shipment == s.Target.Shipment);
    }

    private static async Task<HttpResponseMessage> SendRawM10(HttpClient client, string path,
        string body, string contentType = "application/json")
    {
        using var csrf = await client.GetAsync("/api/v1/auth/csrf");
        csrf.EnsureSuccessStatusCode();
        var token = await csrf.Content.ReadFromJsonAsync<JsonElement>();
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
        request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF", token.GetProperty("token").GetString());
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent(body, System.Text.Encoding.UTF8);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task CorrectionReasonSanitizationAndLinkRulesAreEnforcedOverHttp()
    {
        var s = await SeedAsync("g10q");
        using var _ = s.Admin;
        using var __ = s.Fixture.Admin;
        using var manager = s.Fixture.Manager;
        using var analyst = s.Fixture.Analyst;
        var first = await AcceptAsync(s, "q-1");
        var second = await AcceptAsync(s, "q-2", "2026-11-03T11:00:00Z", "GTOT");
        var third = await AcceptAsync(s, "q-3", "2026-11-03T12:00:00Z", "GTIN");
        var other = await G9Support.SeedResolvable(s.Admin, s.Fixture);
        var foreign = await G9Support.Accept(s.Admin, s.Source, G9Support.EquipmentPayload(
            "q-x-" + Guid.NewGuid().ToString("N")[..10], other.Reference));
        for (var n = 0; n < 4; n++)
            Assert.Equal(TrackingWorkDisposition.Processed,
                await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));
        var root = $"/api/v1/organizations/{s.Fixture.A}/shipments/{s.Target.Shipment}/tracking-corrections";
        async Task<long> Rows() => await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM shipment_tracking_corrections WHERE shipment_id=@s", ("s", s.Target.Shipment));

        string Body(Guid target, string reason, Guid? replacement = null, long generation = 0,
            string extra = "") => "{\"targetReceiptId\":\"" + target + "\",\"replacementReceiptId\":" +
            (replacement.HasValue ? "\"" + replacement + "\"" : "null") +
            ",\"expectedGeneration\":" + generation + ",\"reason\":" +
            JsonSerializer.Serialize(reason) + extra + "}";
        // The m10-mutations limiter allows 10 requests per user per minute. A 429 is the limiter
        // working, so wait for the next window and send the same request again.
        async Task<HttpResponseMessage> Post(HttpClient client, string body,
            string contentType = "application/json")
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var response = await SendRawM10(client, root, body, contentType);
                if (response.StatusCode != HttpStatusCode.TooManyRequests) return response;
                response.Dispose();
                await Task.Delay(TimeSpan.FromSeconds(15));
            }
            throw new InvalidOperationException("The mutation rate limit window never reopened");
        }
        async Task Rejected(string label, string body, HttpStatusCode expected,
            string contentType = "application/json")
        {
            var before = await Rows();
            using var response = await Post(manager, body, contentType);
            Assert.True(response.StatusCode == expected,
                $"{label}: expected {(int)expected} but got {(int)response.StatusCode}");
            Assert.Equal(before, await Rows());
        }

        // Input sanitization at the HTTP boundary: nothing reaches the database.
        await Rejected("reason too short", Body(first, "short"), HttpStatusCode.BadRequest);
        await Rejected("reason over 240", Body(first, new string('r', 241)), HttpStatusCode.BadRequest);
        await Rejected("reason leading space", Body(first, " leading space reason"), HttpStatusCode.BadRequest);
        await Rejected("reason trailing space", Body(first, "trailing space reason "), HttpStatusCode.BadRequest);
        await Rejected("reason control character", Body(first, "bad\u0007control reason"), HttpStatusCode.BadRequest);
        await Rejected("unknown field", Body(first, "Valid reason text", extra: ",\"extra\":1"), HttpStatusCode.BadRequest);
        await Rejected("duplicate field", Body(first, "Valid reason text", extra: ",\"expectedGeneration\":0"), HttpStatusCode.BadRequest);
        await Rejected("replacement equals target", Body(first, "Valid reason text", first), HttpStatusCode.BadRequest);
        await Rejected("negative generation", Body(first, "Valid reason text", generation: -1), HttpStatusCode.BadRequest);
        await Rejected("wrong content type", Body(first, "Valid reason text"), HttpStatusCode.BadRequest, "text/plain");
        await Rejected("oversized body", Body(first, "Valid reason text", extra: ",\"x\":\"" + new string('a', 5000) + "\""), HttpStatusCode.BadRequest);

        // Database-level link validity: another shipment's receipt is never a valid target or replacement.
        await Rejected("target on another shipment", Body(foreign, "Valid reason text"), HttpStatusCode.UnprocessableEntity);
        await Rejected("replacement on another shipment", Body(first, "Valid reason text", foreign), HttpStatusCode.UnprocessableEntity);
        await Rejected("unknown target", Body(Guid.NewGuid(), "Valid reason text"), HttpStatusCode.UnprocessableEntity);
        await Rejected("stale generation", Body(first, "Valid reason text", generation: 5), HttpStatusCode.Conflict);

        // A valid replacement link applies; chains, cycles and reuse are refused and leave one row.
        using (var linked = await Post(manager, Body(first, "Replaced by the later observation", second)))
            Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        Assert.Equal(1, await Rows());
        await Rejected("cycle back to the target", Body(second, "Valid reason text", first, generation: 1), HttpStatusCode.UnprocessableEntity);
        await Rejected("chain through a replacement", Body(second, "Valid reason text", third, generation: 1), HttpStatusCode.UnprocessableEntity);
        await Rejected("replacement reused", Body(third, "Valid reason text", second, generation: 1), HttpStatusCode.UnprocessableEntity);
        await Rejected("target already corrected", Body(first, "Valid reason text", third, generation: 1), HttpStatusCode.UnprocessableEntity);
        Assert.Equal(1, await Rows());
        // Analyst remains refused even for a well-formed correction.
        using (var denied = await Post(analyst, Body(third, "Valid reason text")))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(1, await Rows());
    }

    private static string TransportBody(string key, string code, string reference) =>
        "{\"sourceEventId\":\"" + key + "\",\"eventFamily\":\"Transport\",\"eventCode\":\"" + code +
        "\",\"classifier\":\"ACT\",\"occurredAtUtc\":\"2026-11-03T10:00:00Z\"," +
        "\"transportReference\":\"" + reference + "\",\"externalSchemaVersion\":\"2.2.0\"}";

    [Fact]
    public async Task DepaAndArriTransportEventsStayParkedAndNeverReachTheM10IndexOrProjection()
    {
        var s = await SeedAsync("g10d");
        using var _ = s.Admin;
        using var __ = s.Fixture.Admin;
        using var ___ = s.Fixture.Manager;
        using var ____ = s.Fixture.Analyst;
        var nonce = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var depa = await G9Support.Accept(s.Admin, s.Source, TransportBody("depa-" + nonce, "DEPA", "SYN-VOY-" + nonce));
        var arri = await G9Support.Accept(s.Admin, s.Source, TransportBody("arri-" + nonce, "ARRI", "SYN-VOY-" + nonce));
        var equipment = await AcceptAsync(s, "d-eq");
        var revisionBefore = await Count(s.Roles.Admin, """
            SELECT COUNT(*) FROM shipment_tracking_input_versions WHERE organization_id=@o
            """, ("o", s.Fixture.A));
        var dispositions = new List<TrackingWorkDisposition>();
        for (var n = 0; n < 3; n++) dispositions.Add(await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));
        Assert.Equal(2, dispositions.Count(d => d == TrackingWorkDisposition.NeedsReconciliation));
        Assert.Equal(1, dispositions.Count(d => d == TrackingWorkDisposition.Processed));

        foreach (var receipt in new[] { depa, arri })
        {
            Assert.Equal("NeedsReconciliation", await Scalar(s.Roles.Admin,
                "SELECT state FROM tracking_inbox_work_items WHERE receipt_id=@r", ("r", receipt)));
            Assert.Equal(0, await Count(s.Roles.Admin,
                "SELECT COUNT(*) FROM tracking_processing_effects WHERE receipt_id=@r", ("r", receipt)));
            Assert.Equal(0, await Count(s.Roles.Admin,
                "SELECT COUNT(*) FROM shipment_tracking_timeline_entries WHERE receipt_id=@r", ("r", receipt)));
            Assert.Equal(0, await Count(s.Roles.Admin,
                "SELECT COUNT(*) FROM shipment_projection_applications WHERE receipt_id=@r", ("r", receipt)));
        }
        // Only the resolved equipment event is indexed; transport events change no input revision.
        Assert.Equal(1, await Count(s.Roles.Admin, """
            SELECT COUNT(*) FROM shipment_tracking_timeline_entries WHERE organization_id=@o
            """, ("o", s.Fixture.A)));
        Assert.Equal(1, await Count(s.Roles.Admin, """
            SELECT event_count FROM shipment_tracking_input_versions
            WHERE organization_id=@o AND shipment_id=@s
            """, ("o", s.Fixture.A), ("s", s.Target.Shipment)));
        Assert.Equal(1, await Count(s.Roles.Admin, """
            SELECT COUNT(*) FROM shipment_tracking_timeline_entries WHERE receipt_id=@r
            """, ("r", equipment)));
        Assert.True(revisionBefore <= 1);
        // Rebuilding never resolves the parked events either.
        await using var projectorDb = Context(s.Roles.Projector);
        var rebuilt = await new ShipmentTimelineStore(projectorDb, restrictedProjector: true)
            .RebuildAsync(s.Fixture.A, s.Target.Shipment, CancellationToken.None);
        Assert.Equal(TrackingMutationResult.Applied, rebuilt.Result);
        Assert.Equal(1, await Count(s.Roles.Admin, """
            SELECT event_count FROM shipment_tracking_projections WHERE shipment_id=@s
            """, ("s", s.Target.Shipment)));
        Assert.Equal(2, await Count(s.Roles.Admin, """
            SELECT COUNT(*) FROM tracking_inbox_work_items
            WHERE organization_id=@o AND state='NeedsReconciliation'
            """, ("o", s.Fixture.A)));
    }

    [Fact]
    public async Task ArchivedShipmentIsInvisibleToM10RoutesKeepsHistoryAndRecoversOnRestore()
    {
        var s = await SeedAsync("g10z");
        using var _ = s.Admin;
        using var admin = s.Fixture.Admin;
        using var manager = s.Fixture.Manager;
        using var analyst = s.Fixture.Analyst;
        await AcceptAsync(s, "z-1");
        await AcceptAsync(s, "z-2", "2026-11-03T11:00:00Z", "GTOT");
        for (var n = 0; n < 2; n++)
            Assert.Equal(TrackingWorkDisposition.Processed,
                await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));
        var root = $"/api/v1/organizations/{s.Fixture.A}/shipments/{s.Target.Shipment}";
        using (var before = await analyst.GetAsync(root + "/events?limit=5"))
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        // The accepted M6 rule: a shipment with an open allocation cannot be archived. Close it first.
        long openVersion;
        await using (var read = Context(s.Roles.Admin))
            openVersion = (await read.Shipments.AsNoTracking().SingleAsync(x => x.Id == s.Target.Shipment)).Version;
        using (var guarded = await ReferenceDataHttpTests.Send(manager, HttpMethod.Delete,
            $"/api/v1/organizations/{s.Fixture.A}/shipments/{s.Target.Shipment}", new { expectedVersion = openVersion }))
            Assert.Equal(HttpStatusCode.Conflict, guarded.StatusCode);
        var assignmentId = (Guid)(await Scalar(s.Roles.Admin, """
            SELECT id FROM shipment_container_assignments
            WHERE organization_id=@o AND shipment_id=@s AND ends_at_utc IS NULL
            """, ("o", s.Fixture.A), ("s", s.Target.Shipment)))!;
        using (var close = await ReferenceDataHttpTests.Send(analyst, HttpMethod.Patch,
            root + "/container-assignments/" + assignmentId,
            new { expectedVersion = 1L, endsAtUtc = "2026-11-10T12:00:00Z" }))
            Assert.Equal(HttpStatusCode.OK, close.StatusCode);

        long version;
        await using (var read = Context(s.Roles.Admin))
            version = (await read.Shipments.AsNoTracking().SingleAsync(x => x.Id == s.Target.Shipment)).Version;
        using (var archive = await ReferenceDataHttpTests.Send(manager, HttpMethod.Delete,
            $"/api/v1/organizations/{s.Fixture.A}/shipments/{s.Target.Shipment}", new { expectedVersion = version }))
            Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        // Every M10 surface treats the archived shipment as not found and reveals nothing.
        foreach (var client in new[] { analyst, manager })
        {
            foreach (var path in new[] { "/events?limit=5", "/tracking-projection" })
            {
                using var response = await client.GetAsync(root + path);
                Assert.True(response.StatusCode == HttpStatusCode.NotFound,
                    $"GET {path} on an archived shipment returned {(int)response.StatusCode}");
                Assert.DoesNotContain("g10", await response.Content.ReadAsStringAsync());
            }
        }
        using (var rebuild = await SendM10(manager, root + "/tracking-projection/rebuild"))
            Assert.Equal(HttpStatusCode.NotFound, rebuild.StatusCode);
        using (var correction = await SendM10(manager, root + "/tracking-corrections", new
        {
            targetReceiptId = Guid.NewGuid(), replacementReceiptId = (Guid?)null,
            expectedGeneration = 0, reason = "Correction attempt on archived shipment"
        }))
            Assert.Equal(HttpStatusCode.NotFound, correction.StatusCode);
        Assert.Equal(0, await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM shipment_tracking_corrections WHERE shipment_id=@s", ("s", s.Target.Shipment)));
        Assert.Equal(0, await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM shipment_tracking_projections WHERE shipment_id=@s", ("s", s.Target.Shipment)));
        // The projector never discovers or rebuilds an archived scope, although it was never materialized.
        await using var projectorDb = Context(s.Roles.Projector);
        var projector = new ShipmentTimelineStore(projectorDb, restrictedProjector: true);
        Assert.DoesNotContain(await projector.DirtyAsync(50, CancellationToken.None),
            d => d.Shipment == s.Target.Shipment);
        Assert.Equal(TrackingMutationResult.NotFound,
            (await projector.RebuildAsync(s.Fixture.A, s.Target.Shipment, CancellationToken.None)).Result);
        // History is retained, immutable and merely hidden.
        Assert.Equal(2, await Count(s.Roles.Admin,
            "SELECT COUNT(*) FROM shipment_tracking_timeline_entries WHERE shipment_id=@s", ("s", s.Target.Shipment)));

        // Restore makes the retained history visible again, unchanged.
        long archivedVersion;
        await using (var read = Context(s.Roles.Admin))
            archivedVersion = (await read.Shipments.AsNoTracking().SingleAsync(x => x.Id == s.Target.Shipment)).Version;
        using (var restore = await ReferenceDataHttpTests.Send(analyst, HttpMethod.Patch, root,
            new { recordStatus = "Active", expectedVersion = archivedVersion }))
            Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        using (var after = await analyst.GetAsync(root + "/events?limit=5"))
        {
            Assert.Equal(HttpStatusCode.OK, after.StatusCode);
            Assert.Equal(2, (await after.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("eventCount").GetInt64());
        }
    }

    [Fact]
    public async Task SuspendedOrganizationIsDeniedAtOnceByEveryM10PathAndRecoversOnReactivation()
    {
        var s = await SeedAsync("g10u");
        using var _ = s.Admin;
        using var admin = s.Fixture.Admin;
        using var manager = s.Fixture.Manager;
        using var analyst = s.Fixture.Analyst;
        await AcceptAsync(s, "u-1");
        await AcceptAsync(s, "u-2", "2026-11-03T11:00:00Z", "GTOT");
        Assert.Equal(TrackingWorkDisposition.Processed,
            await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));
        var root = $"/api/v1/organizations/{s.Fixture.A}/shipments/{s.Target.Shipment}";
        using (var before = await analyst.GetAsync(root + "/events?limit=5"))
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        async Task SetStatus(string status)
        {
            await using var connection = new NpgsqlConnection(s.Roles.Admin);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "UPDATE organizations SET status=@s WHERE id=@o", connection);
            command.Parameters.AddWithValue("s", status);
            command.Parameters.AddWithValue("o", s.Fixture.A);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }
        await SetStatus("Suspended");
        try
        {
            // Already authenticated sessions are refused on the very next request, for reads and mutations.
            foreach (var path in new[] { "/events?limit=5", "/tracking-projection" })
            {
                using var response = await analyst.GetAsync(root + path);
                Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or
                    HttpStatusCode.Conflict or HttpStatusCode.Unauthorized,
                    $"GET {path} for a suspended organization returned {(int)response.StatusCode}");
            }
            using (var rebuild = await SendM10(manager, root + "/tracking-projection/rebuild"))
                Assert.True(rebuild.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or
                    HttpStatusCode.Conflict or HttpStatusCode.Unauthorized,
                    $"rebuild returned {(int)rebuild.StatusCode}");
            using (var correction = await SendM10(manager, root + "/tracking-corrections", new
            {
                targetReceiptId = Guid.NewGuid(), replacementReceiptId = (Guid?)null,
                expectedGeneration = 0, reason = "Correction during suspension"
            }))
                Assert.True(correction.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or
                    HttpStatusCode.Conflict or HttpStatusCode.Unauthorized,
                    $"correction returned {(int)correction.StatusCode}");
            Assert.Equal(0, await Count(s.Roles.Admin,
                "SELECT COUNT(*) FROM shipment_tracking_corrections WHERE organization_id=@o", ("o", s.Fixture.A)));
            Assert.Equal(0, await Count(s.Roles.Admin,
                "SELECT COUNT(*) FROM shipment_tracking_projections WHERE organization_id=@o", ("o", s.Fixture.A)));
            // The restricted projector cannot lock a suspended scope, the sweep skips it, and the worker holds back.
            Assert.Equal(false, await Scalar(s.Roles.Projector,
                "SELECT m10_lock_projector_scope(@o,@s)", ("o", s.Fixture.A), ("s", s.Target.Shipment)));
            await using (var projectorDb = Context(s.Roles.Projector))
            {
                var projector = new ShipmentTimelineStore(projectorDb, restrictedProjector: true);
                Assert.DoesNotContain(await projector.DirtyAsync(50, CancellationToken.None),
                    d => d.Organization == s.Fixture.A);
                Assert.Equal(TrackingMutationResult.NotFound,
                    (await projector.RebuildAsync(s.Fixture.A, s.Target.Shipment, CancellationToken.None)).Result);
            }
            await using (var workerDb = Context(s.Roles.Worker))
            {
                var worker = new TrackingWorkStore(workerDb, new TrackingWorkerSettings());
                var claim = await worker.ClaimOneAsync(s.Fixture.A, CancellationToken.None);
                Assert.True(claim is null, "the worker claimed work for a suspended organization");
            }
            Assert.Equal(1, await Count(s.Roles.Admin, """
                SELECT COUNT(*) FROM tracking_inbox_work_items
                WHERE organization_id=@o AND state='Pending'
                """, ("o", s.Fixture.A)));
        }
        finally { await SetStatus("Active"); }

        // Reactivation: retained history is intact and the deferred work completes.
        using (var after = await analyst.GetAsync(root + "/events?limit=5"))
        {
            Assert.Equal(HttpStatusCode.OK, after.StatusCode);
            Assert.Equal(1, (await after.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("eventCount").GetInt64());
        }
        Assert.Equal(TrackingWorkDisposition.Processed,
            await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));
        Assert.Equal(2, await Count(s.Roles.Admin, """
            SELECT COUNT(*) FROM shipment_tracking_timeline_entries WHERE shipment_id=@s
            """, ("s", s.Target.Shipment)));
    }

    private sealed class ProjectorFactory(string api, string projector)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            LocalPeerTestServer.Configure(builder);
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:HarborOps", api);
            builder.UseSetting("ConnectionStrings:HarborOpsProjector", projector);
            builder.UseSetting("M10:EnableProjector", "true");
            builder.UseSetting("Identity:AllowInsecureLoopbackHttp", "true");
            builder.UseSetting("Identity:KeyRingPath",
                Path.Combine(Directory.GetCurrentDirectory(), ".identity-keys"));
        }
    }

    [Fact]
    public async Task RealProjectorServiceSkipsAPoisonedShipmentThenRecoversUnderTheRestrictedRole()
    {
        var s = await SeedAsync("g10s");
        using var _ = s.Admin;
        using var __ = s.Fixture.Admin;
        using var ___ = s.Fixture.Manager;
        using var ____ = s.Fixture.Analyst;
        var other = await G9Support.SeedResolvable(s.Admin, s.Fixture);
        await G9Support.Accept(s.Admin, s.Source, G9Support.EquipmentPayload(
            "s-a-" + Guid.NewGuid().ToString("N")[..10], s.Target.Reference));
        await G9Support.Accept(s.Admin, s.Source, G9Support.EquipmentPayload(
            "s-b-" + Guid.NewGuid().ToString("N")[..10], other.Reference));
        for (var n = 0; n < 2; n++)
            Assert.Equal(TrackingWorkDisposition.Processed,
                await WorkerFinalizeOneAsync(s.Roles, s.Fixture.A));

        // The poisoned shipment sorts first, so a fixed-position retry would starve the healthy one.
        var poison = (Guid)(await Scalar(s.Roles.Admin, "SELECT LEAST(@a,@b)",
            ("a", s.Target.Shipment), ("b", other.Shipment)))!;
        var healthy = poison == s.Target.Shipment ? other.Shipment : s.Target.Shipment;
        var poisonLiteral = poison.ToString();
        await using (var setup = new NpgsqlConnection(s.Roles.Admin))
        {
            await setup.OpenAsync();
            await using var create = new NpgsqlCommand($$"""
                CREATE OR REPLACE FUNCTION g10_test_poison() RETURNS trigger LANGUAGE plpgsql AS $f$
                BEGIN
                    IF NEW.shipment_id='{{poisonLiteral}}'::uuid THEN
                        RAISE EXCEPTION 'synthetic persistent projection failure';
                    END IF;
                    RETURN NEW;
                END $f$;
                CREATE TRIGGER g10_test_poison BEFORE INSERT OR UPDATE
                    ON shipment_tracking_projections
                    FOR EACH ROW EXECUTE FUNCTION g10_test_poison();
                """, setup);
            await create.ExecuteNonQueryAsync();
        }
        var auditBefore = await Count(s.Roles.Admin, "SELECT COUNT(*) FROM audit_entries");
        try
        {
            using var projectorHost = new ProjectorFactory(s.Roles.Api, s.Roles.Projector);
            using var client = projectorHost.CreateClient();   // starts the hosted service

            async Task<bool> Materialized(Guid shipment) => await Count(s.Roles.Admin, """
                SELECT COUNT(*) FROM shipment_tracking_projections
                WHERE shipment_id=@s AND observed_state='EquipmentIn'
                """, ("s", shipment)) == 1;
            var deadline = DateTime.UtcNow.AddSeconds(120);
            while (DateTime.UtcNow < deadline && !await Materialized(healthy))
                await Task.Delay(500);
            Assert.True(await Materialized(healthy),
                "The healthy shipment was starved by the poisoned shipment");
            Assert.False(await Materialized(poison));
            Assert.Equal(0, await Count(s.Roles.Admin,
                "SELECT COUNT(*) FROM shipment_tracking_projections WHERE shipment_id=@s",
                ("s", poison)));

            // Recovery: once the fault is removed the next round materializes the poisoned item.
            await using (var heal = new NpgsqlConnection(s.Roles.Admin))
            {
                await heal.OpenAsync();
                await using var drop = new NpgsqlCommand(
                    "DROP TRIGGER g10_test_poison ON shipment_tracking_projections", heal);
                await drop.ExecuteNonQueryAsync();
            }
            deadline = DateTime.UtcNow.AddSeconds(120);
            while (DateTime.UtcNow < deadline && !await Materialized(poison))
                await Task.Delay(500);
            Assert.True(await Materialized(poison), "The poisoned shipment did not recover");
            Assert.Equal(1, await Count(s.Roles.Admin, """
                SELECT COUNT(*) FROM shipment_projection_applications WHERE shipment_id=@s
                """, ("s", poison)));
        }
        finally
        {
            await using var cleanup = new NpgsqlConnection(s.Roles.Admin);
            await cleanup.OpenAsync();
            await using var drop = new NpgsqlCommand("""
                DROP TRIGGER IF EXISTS g10_test_poison ON shipment_tracking_projections;
                DROP FUNCTION IF EXISTS g10_test_poison();
                """, cleanup);
            await drop.ExecuteNonQueryAsync();
        }
        // The projector wrote no audit evidence and altered no accepted M8/M9 row.
        Assert.Equal(auditBefore, await Count(s.Roles.Admin, "SELECT COUNT(*) FROM audit_entries"));
    }
}
