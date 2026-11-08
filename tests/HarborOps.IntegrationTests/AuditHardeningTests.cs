using System.Text;
using HarborOps.Api.Audit;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// G16-S01, S09 to S12, S28: guarantees that were previously proven only by manual probes.
public sealed class AuditHardeningTests
{
    private static readonly string[] SafeFields =
    {
        "status", "security_version", "display_name", "role", "name", "code", "departure_at_utc",
        "arrival_at_utc", "source_code", "revoked_at_utc"
    };

    [Fact]
    public async Task SourceBackedAuditNeverInheritsAStaleHumanOrCliContext()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var source = await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
            .CreateAsync(fixture.A, "g16_stale_ctx", "Stale Context Source", CancellationToken.None);

        var raw = Encoding.UTF8.GetBytes("""
            {"sourceEventId":"g16-stale-context","eventFamily":"Equipment",
             "eventCode":"GTIN","classifier":"ACT","occurredAtUtc":"2026-11-03T10:00:00Z",
             "equipmentReference":"SYN-BOX-ABCD","externalSchemaVersion":"2.2.0"}
            """);
        var value = TrackingEventPolicy.Parse(raw, TimeProvider.System.GetUtcNow()).Value!;
        var staleUser = Guid.NewGuid();
        var staleSession = Guid.NewGuid();
        // Session-level settings survive transactions on this physical connection, which is the
        // worst case for context leakage: an earlier human request left its attribution behind.
        await db.Database.OpenConnectionAsync();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT set_config('harborops.audit_actor_kind', 'Human', false),
                    set_config('harborops.audit_actor_user_id', {staleUser.ToString("D")}, false),
                    set_config('harborops.audit_actor_session_id', {staleSession.ToString("D")}, false),
                    set_config('harborops.audit_actor_role', 'Administrator', false)
                """);
            var received = await scope.ServiceProvider.GetRequiredService<TrackingInboxStore>()
                .ReceiveAsync(value.Bind(source.Id, fixture.A), source.Bearer, raw,
                    "g16-stale-context", CancellationToken.None);
            Assert.Equal(TrackingReceiptOutcome.Received, received.Outcome);
            await db.Database.ExecuteSqlRawAsync("""
                SELECT set_config('harborops.audit_actor_kind', '', false),
                    set_config('harborops.audit_actor_user_id', '', false),
                    set_config('harborops.audit_actor_session_id', '', false),
                    set_config('harborops.audit_actor_role', '', false)
                """);
        }
        finally { await db.Database.CloseConnectionAsync(); }

        var audit = await db.AuditEntries.AsNoTracking().SingleAsync(x =>
            x.OrganizationId == fixture.A && x.EventType == "integration.tracking_accepted" &&
            x.ActorSourceId == source.Id);
        Assert.Equal("IntegrationSource", audit.ActorKind);
        Assert.Null(audit.ActorUserId);
        Assert.Null(audit.ActorSessionId);
        Assert.Null(audit.ActorRole);
        Assert.NotEqual(staleUser, audit.ActorUserId);
    }

    [Fact]
    public async Task AuditMetadataKeysAndFieldNamesStayWithinTheFixedAllowlist()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString());
        await connection.OpenAsync();
        var keys = new List<(string EventType, string Key)>();
        await using (var command = new NpgsqlCommand("""
            SELECT DISTINCT a.event_type, k
            FROM audit_entries a, jsonb_object_keys(a.metadata) AS k
            ORDER BY a.event_type, k
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                keys.Add((reader.GetString(0), reader.GetString(1)));
        Assert.NotEmpty(keys);
        Assert.All(keys, item =>
        {
            var allowed = item.EventType == "shipment.export_prepared"
                ? new[] { "rowCount", "byteCount", "filterDigest", "exportSchema" }
                : new[] { "changedFields", "operation" };
            Assert.Contains(item.Key, allowed);
        });

        var fields = new List<(string EventType, string Field)>();
        await using (var command = new NpgsqlCommand("""
            SELECT DISTINCT a.event_type, f
            FROM audit_entries a, jsonb_array_elements_text(a.metadata->'changedFields') AS f
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                fields.Add((reader.GetString(0), reader.GetString(1)));
        Assert.All(fields, item =>
        {
            // M11 signed dispositions add an explicit safe lifecycle field
            // without expanding the accepted M16 fields for other events.
            if (item.Field == "lifecycle_state" &&
                item.EventType is "shipment.exception_acknowledged" or
                    "shipment.exception_resolved")
                return;
            Assert.Contains(item.Field, SafeFields);
        });

        // Values of sensitive columns must never be present anywhere in stored metadata.
        await using var leak = new NpgsqlCommand("""
            SELECT count(*) FROM audit_entries
            WHERE metadata::text ~* '(password|secret|token|bearer|cookie|hash|@example|sourceEventId|canonical)'
            """, connection);
        Assert.Equal(0L, (long)(await leak.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task OversizedAuditMetadataIsRejectedByDatabaseConstraint()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString());
        await connection.OpenAsync();
        // Use nonrepeating content so PostgreSQL compression cannot make the
        // oversized JSON appear smaller than the constraint threshold.
        var payload = string.Concat(Enumerable.Range(0, 100)
            .Select(_ => Guid.NewGuid().ToString("N")));
        await using var measured = new NpgsqlCommand(
            "SELECT pg_column_size(jsonb_build_object('payload', @payload))", connection);
        measured.Parameters.AddWithValue("payload", payload);
        Assert.True(Convert.ToInt32(await measured.ExecuteScalarAsync()) > 1024);
        await using var attempt = new NpgsqlCommand("""
            INSERT INTO public.audit_entries
                (organization_id,event_type,outcome,actor_kind,resource_type,correlation_id,metadata)
            VALUES (@org,'audit.metadata_limit','Succeeded','System',
                    'audit_entries','g16-size-check',jsonb_build_object('payload',@payload))
            """, connection);
        attempt.Parameters.AddWithValue("org", fixture.A);
        attempt.Parameters.AddWithValue("payload", payload);
        var error = await Assert.ThrowsAsync<PostgresException>(() => attempt.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task ProvisionedRuntimeRolesCannotModifyAuditEvidenceOrRunDdl()
    {
        var admin = TestDatabase.ConnectionString();
        var options = new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(admin).Options;
        await using var db = new HarborOpsDbContext(options);
        await db.Database.MigrateAsync();
        var apiSecret = "api-" + Guid.NewGuid().ToString("N");
        var workerSecret = "worker-" + Guid.NewGuid().ToString("N");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AuditRoles:ApiPassword"] = apiSecret,
            ["AuditRoles:WorkerPassword"] = workerSecret
        }).Build();
        await AuditRoleProvisioner.ProvisionAsync(db, configuration, CancellationToken.None);

        string RoleConnection(string role, string password) => new NpgsqlConnectionStringBuilder(admin)
        { Username = role, Password = password, Pooling = false }.ConnectionString;

        async Task ExpectDenied(string role, string password, string statement)
        {
            await using var connection = new NpgsqlConnection(RoleConnection(role, password));
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(statement, connection);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.True(failure.SqlState == PostgresErrorCodes.InsufficientPrivilege,
                $"{role} expected 42501 for [{statement}] but received {failure.SqlState}: {failure.MessageText}");
        }

        var denied = new[]
        {
            "UPDATE public.audit_entries SET outcome = 'Denied'",
            "DELETE FROM public.audit_entries",
            "TRUNCATE TABLE public.audit_entries",
            "INSERT INTO public.audit_entries DEFAULT VALUES",
            "ALTER TABLE public.audit_entries DISABLE TRIGGER ALL",
            "DROP TABLE public.audit_entries",
            "DROP TRIGGER m16_audit_no_change ON public.audit_entries",
            "DROP TRIGGER m16_audit_no_truncate ON public.audit_entries",
            "CREATE TABLE public.g16_unauthorized (id int)",
            "CREATE OR REPLACE FUNCTION public.m16_audit_guard() RETURNS trigger LANGUAGE plpgsql AS $f$ BEGIN RETURN NEW; END $f$",
            "TRUNCATE TABLE public.organizations CASCADE",
            "ALTER TABLE public.organizations DISABLE TRIGGER ALL",
            "SET session_replication_role = replica",
            "CREATE ROLE g16_unauthorized_role"
        };
        foreach (var statement in denied)
        {
            await ExpectDenied("harborops_api", apiSecret, statement);
            await ExpectDenied("harborops_worker", workerSecret, statement);
        }
        await ExpectDenied("harborops_worker", workerSecret, "SELECT id FROM public.audit_entries LIMIT 1");
        await ExpectDenied("harborops_worker", workerSecret,
            "INSERT INTO public.tracking_work_attempt_events DEFAULT VALUES");
        await ExpectDenied("harborops_worker", workerSecret, "UPDATE public.organizations SET display_name = display_name");
        await ExpectDenied("harborops_api", apiSecret, "DELETE FROM public.users WHERE false");
        await ExpectDenied("harborops_api", apiSecret, "DELETE FROM public.auth_sessions WHERE false");
        await ExpectDenied("harborops_api", apiSecret, "TRUNCATE TABLE public.tracking_accepted_events");

        // Positive controls: the API can read audit evidence and mutate ordinary business tables.
        await using (var api = new NpgsqlConnection(RoleConnection("harborops_api", apiSecret)))
        {
            await api.OpenAsync();
            await using var redriveGrant = new NpgsqlCommand(
                "SELECT has_table_privilege(current_user, 'public.tracking_work_attempt_events', 'INSERT')", api);
            Assert.True((bool)(await redriveGrant.ExecuteScalarAsync())!);
            await using var read = new NpgsqlCommand("SELECT count(*) FROM public.audit_entries", api);
            Assert.NotNull(await read.ExecuteScalarAsync());
            await using var update = new NpgsqlCommand(
                "UPDATE public.organizations SET display_name = display_name WHERE false", api);
            Assert.Equal(0, await update.ExecuteNonQueryAsync());
        }

        // Role attributes and guard objects are intact after the matrix.
        await using var inspect = new NpgsqlConnection(admin);
        await inspect.OpenAsync();
        await using (var attributes = new NpgsqlCommand("""
            SELECT count(*) FROM pg_roles WHERE rolname IN ('harborops_api','harborops_worker')
              AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)
            """, inspect))
            Assert.Equal(0L, (long)(await attributes.ExecuteScalarAsync())!);
        await using (var owner = new NpgsqlCommand("""
            SELECT count(*) FROM pg_class c JOIN pg_roles r ON r.oid = c.relowner
            WHERE r.rolname IN ('harborops_api','harborops_worker')
            """, inspect))
            Assert.Equal(0L, (long)(await owner.ExecuteScalarAsync())!);
        await using (var guards = new NpgsqlCommand("""
            SELECT count(*) FROM pg_trigger
            WHERE tgrelid = 'public.audit_entries'::regclass AND NOT tgisinternal AND tgenabled = 'O'
            """, inspect))
            Assert.Equal(2L, (long)(await guards.ExecuteScalarAsync())!);
    }
}
