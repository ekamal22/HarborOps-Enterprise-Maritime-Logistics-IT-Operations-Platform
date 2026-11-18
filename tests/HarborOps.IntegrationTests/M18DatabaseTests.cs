using System.Net;
using HarborOps.Infrastructure.Exceptions;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// G18 database boundary witnesses: grants, forced row-level security, signed scope, immutable
// history, the transition guard, composite tenant keys and the exact M16 audit vocabulary.
[Collection("M12ServerRoleIsolation")]
public sealed class M18DatabaseTests
{
    internal static readonly string[] Tables =
    [
        "it_incidents", "it_incident_events", "it_incident_notes", "it_incident_actions",
        "it_incident_action_events", "it_incident_evidence", "it_incident_operation_ids",
        "it_incident_read_grants"
    ];
    private static readonly string[] History =
        ["it_incident_events", "it_incident_notes", "it_incident_action_events", "it_incident_evidence"];

    private static async Task<bool> Priv(M18World w, string sql, params (string, object)[] p) =>
        await w.Scalar<bool>(sql, p);

    [Fact]
    public async Task RuntimeRoleHoldsOnlyAppendAndColumnLimitedUpdateRights()
    {
        using var w = await M18World.CreateAsync();
        foreach (var table in Tables)
        {
            Assert.True(await Priv(w, "SELECT has_table_privilege('harborops_api',@t,'SELECT')", ("t", table)), table);
            Assert.True(await Priv(w, "SELECT has_table_privilege('harborops_api',@t,'INSERT')", ("t", table)), table);
            foreach (var denied in new[] { "DELETE", "TRUNCATE", "REFERENCES", "TRIGGER", "UPDATE" })
                Assert.False(await Priv(w, $"SELECT has_table_privilege('harborops_api',@t,'{denied}')", ("t", table)),
                    $"{table} {denied}");
            // PUBLIC and the other runtime identities have nothing at all
            foreach (var role in new[] { "harborops_worker", "harborops_projector", "public" })
            {
                Assert.False(await Priv(w, "SELECT has_table_privilege(@r,@t,'SELECT')", ("r", role), ("t", table)), $"{role} {table}");
                Assert.False(await Priv(w, "SELECT has_any_column_privilege(@r,@t,'UPDATE')", ("r", role), ("t", table)), $"{role} {table}");
            }
            Assert.Equal(1L, await w.Scalar<long>("""
                SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='public' AND c.relname=@t AND c.relrowsecurity AND c.relforcerowsecurity
                """, ("t", table)));
            Assert.Equal(1L, await w.Scalar<long>(
                "SELECT count(*) FROM pg_policies WHERE tablename=@t AND policyname='m18_tenant_scope' AND cmd='ALL'", ("t", table)));
            Assert.NotEqual("harborops_api", await w.Scalar<string>("SELECT tableowner FROM pg_tables WHERE tablename=@t", ("t", table)));
        }
        foreach (var table in History)
            Assert.False(await Priv(w, "SELECT has_any_column_privilege('harborops_api',@t,'UPDATE')", ("t", table)), table);
        foreach (var column in new[] { "id", "organization_id", "created_by", "created_at_utc", "detected_at_utc", "title", "service_code" })
            Assert.False(await Priv(w, "SELECT has_column_privilege('harborops_api','it_incidents',@c,'UPDATE')", ("c", column)), column);
        foreach (var column in new[] { "state", "version", "severity", "assigned_to", "resolved_by", "closed_by" })
            Assert.True(await Priv(w, "SELECT has_column_privilege('harborops_api','it_incidents',@c,'UPDATE')", ("c", column)), column);
        foreach (var column in new[] { "operation_key", "payload_digest", "operation_kind", "actor_user_id", "organization_id" })
            Assert.False(await Priv(w, "SELECT has_column_privilege('harborops_api','it_incident_operation_ids',@c,'UPDATE')", ("c", column)), column);
        Assert.False(await Priv(w, "SELECT has_column_privilege('harborops_api','it_incident_read_grants','user_id','UPDATE')"));
        Assert.False(await Priv(w, "SELECT has_column_privilege('harborops_api','it_incident_actions','owner_user_id','UPDATE')"));
        // the runtime role owns nothing and cannot alter triggers or the schema
        Assert.Equal(0L, await w.Scalar<long>("SELECT count(*) FROM pg_class WHERE relowner=(SELECT oid FROM pg_roles WHERE rolname='harborops_api')"));
        foreach (var sql in new[]
        {
            "ALTER TABLE it_incident_events DISABLE TRIGGER ALL",
            "DROP TRIGGER m18_events_immutable ON it_incident_events",
            "ALTER TABLE it_incidents DISABLE ROW LEVEL SECURITY",
            "DROP TABLE it_incident_events",
            "CREATE POLICY open ON it_incidents USING (true)"
        })
        {
            await using var connection = new NpgsqlConnection(w.Roles.Api);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, failure.SqlState);
        }
    }

    private static async Task<Guid> SessionOf(M18World w, string user, Guid org) =>
        await w.Scalar<Guid>("SELECT id FROM auth_sessions WHERE user_id=@u AND selected_organization_id=@o AND revoked_at_utc IS NULL ORDER BY issued_at_utc DESC LIMIT 1",
            ("u", w.Users[user].Id), ("o", org));

    // Runs SQL as the real harborops_api role inside a transaction carrying a genuine server-signed scope.
    private static async Task<T> WithScope<T>(M18World w, string user, Guid org,
        Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> body)
    {
        var session = await SessionOf(w, user, org);
        await using var scope = w.Runtime.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var signer = scope.ServiceProvider.GetRequiredService<ShipmentExceptionStore>();
        await using var tx = await db.Database.BeginTransactionAsync();
        var npgsqlTx = (NpgsqlTransaction)tx.GetDbTransaction();
        await signer.SetReadScopeAsync(npgsqlTx, org, w.Users[user].Id, session, CancellationToken.None);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        return await body(connection, npgsqlTx);
    }

    private static async Task<string?> SqlState(M18World w, string user, Guid org, string sql,
        params (string, object)[] p)
    {
        try
        {
            await WithScope<int>(w, user, org, async (c, t) =>
            {
                await using var command = new NpgsqlCommand(sql, c, t);
                foreach (var (n, v) in p) command.Parameters.AddWithValue(n, v);
                return await command.ExecuteNonQueryAsync();
            });
            return null;
        }
        catch (PostgresException ex) { return ex.SqlState; }
    }

    private static async Task<long> ScopedCount(M18World w, string user, Guid org, string table) =>
        await WithScope(w, user, org, async (c, t) =>
        {
            await using var command = new NpgsqlCommand("SELECT count(*) FROM " + table, c, t);
            return (long)(await command.ExecuteScalarAsync())!;
        });

    [Fact]
    public async Task DirectRuntimeSqlSeesAndWritesNothingWithoutAGenuineSignedScope()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var (id, _) = await w.CreateIncident(support);
        await using var restricted = new NpgsqlConnection(w.Roles.Api);
        await restricted.OpenAsync();
        foreach (var table in Tables)
        {
            await using var query = new NpgsqlCommand("SELECT count(*) FROM " + table, restricted);
            Assert.Equal(0L, (long)(await query.ExecuteScalarAsync())!);
        }
        // no ambient scope: insert is refused by the row-level check
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO it_incidents(organization_id,title,service_code,severity,impact_code,detected_at_utc,created_by)
            VALUES(@o,'direct','api','SEV-3','x',now(),@u)
            """, restricted))
        {
            insert.Parameters.AddWithValue("o", w.A);
            insert.Parameters.AddWithValue("u", w.Users["support"].Id);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, failure.SqlState);
        }
        // a caller-written scope value with a made-up signature is not authority
        await using var tx = await restricted.BeginTransactionAsync();
        var session = await SessionOf(w, "support", w.A);
        await using (var forged = new NpgsqlCommand("SELECT set_config('harborops.m11_read_scope',@s,true)", restricted, tx))
        {
            forged.Parameters.AddWithValue("s",
                $"{w.A:D}|{w.Users["support"].Id:D}|{session:D}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 20}|{new string('0', 64)}");
            await forged.ExecuteNonQueryAsync();
        }
        await using (var read = new NpgsqlCommand("SELECT count(*) FROM it_incidents", restricted, tx))
            Assert.Equal(0L, (long)(await read.ExecuteScalarAsync())!);
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM it_incidents WHERE id=@i", ("i", id)));
    }

    [Fact]
    public async Task ValidSignedScopeIsTenantBoundAndCannotRewriteHistoryOrBypassTheTransitionGuard()
    {
        using var w = await M18World.CreateAsync();
        using var alpha = await w.Client("support");
        using var beta = await w.Client("supportb", w.B);
        var (alphaId, _) = await w.CreateIncident(alpha);
        var (betaId, _) = await w.CreateIncident(beta, org: w.B);
        await w.Post(alpha, alphaId, "/notes", new { expectedVersion = 1L, body = "note one" });

        Assert.Equal(1L, await ScopedCount(w, "support", w.A, "it_incidents"));
        Assert.Equal(1L, await ScopedCount(w, "supportb", w.B, "it_incidents"));
        // a scope for Alpha cannot read Beta rows, even naming them explicitly
        Assert.Equal(0L, await WithScope(w, "support", w.A, async (c, t) =>
        {
            await using var q = new NpgsqlCommand("SELECT count(*) FROM it_incidents WHERE id=@i", c, t);
            q.Parameters.AddWithValue("i", betaId);
            return (long)(await q.ExecuteScalarAsync())!;
        }));
        foreach (var table in History)
        {
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await SqlState(w, "support", w.A, $"UPDATE {table} SET organization_id=organization_id"));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await SqlState(w, "support", w.A, $"DELETE FROM {table}"));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await SqlState(w, "support", w.A, $"TRUNCATE {table}"));
        }
        foreach (var table in Tables)
        {
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await SqlState(w, "support", w.A, $"DELETE FROM {table}"));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await SqlState(w, "support", w.A, $"TRUNCATE {table}"));
        }
        // moving or re-owning a row is not a granted column update
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await SqlState(w, "support", w.A, "UPDATE it_incidents SET organization_id=@b", ("b", w.B)));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await SqlState(w, "support", w.A, "UPDATE it_incidents SET created_by=@u", ("u", w.Users["support2"].Id)));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await SqlState(w, "support", w.A, "UPDATE it_incidents SET title='rewritten'"));
        // granted columns still obey the database transition guard
        Assert.Equal(PostgresErrorCodes.CheckViolation, await SqlState(w, "support", w.A, "UPDATE it_incidents SET state='Closed',version=version+1,closed_by=@u,closed_at_utc=now()", ("u", w.Users["support"].Id)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await SqlState(w, "support", w.A, "UPDATE it_incidents SET state='Triage',version=version+5"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await SqlState(w, "support", w.A, "UPDATE it_incidents SET state='Investigating',version=version+1"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await SqlState(w, "support", w.A, "UPDATE it_incidents SET severity='SEV-1'"));
        // tenant escape on insert
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await SqlState(w, "support", w.A, """
            INSERT INTO it_incidents(organization_id,title,service_code,severity,impact_code,detected_at_utc,created_by)
            VALUES(@b,'escape','api','SEV-3','x',now(),@u)
            """, ("b", w.B), ("u", w.Users["support"].Id)));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, await SqlState(w, "support", w.A,
            "INSERT INTO it_incident_notes(organization_id,incident_id,body,actor_user_id) VALUES(@a,@b,'cross',@u)",
            ("a", w.A), ("b", betaId), ("u", w.Users["support"].Id)));
        // nothing above changed anything
        Assert.Equal("New", await w.Scalar<string>("SELECT state FROM it_incidents WHERE id=@i", ("i", alphaId)));
        Assert.Equal(2L, await w.Scalar<long>("SELECT version FROM it_incidents WHERE id=@i", ("i", alphaId)));
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM it_incident_notes WHERE incident_id=@i", ("i", alphaId)));
    }

    [Fact]
    public async Task HistoryTablesRejectUpdateDeleteAndTruncateEvenForTheSchemaOwner()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var (id, v) = await w.CreateIncident(support);
        v = await w.Post(support, id, "/notes", new { expectedVersion = v, body = "history note" });
        v = await w.Post(support, id, "/evidence", new { expectedVersion = v, observationCode = "seen" });
        v = await w.Post(support, id, "/actions", new { expectedVersion = v, title = "act", ownerUserId = w.Users["support"].Id });
        foreach (var table in History)
            foreach (var sql in new[] { $"UPDATE {table} SET organization_id=organization_id", $"DELETE FROM {table}", $"TRUNCATE {table}" })
            {
                await using var connection = new NpgsqlConnection(w.Roles.Admin);
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(sql, connection);
                var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
                Assert.Contains("immutable", failure.MessageText);
            }
        Assert.Equal(v, await w.EventCount(id));
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM it_incident_notes WHERE incident_id=@i", ("i", id)));
    }

    [Fact]
    public async Task CompositeTenantKeysRejectCrossOrganizationChildrenForEveryTable()
    {
        using var w = await M18World.CreateAsync();
        using var alpha = await w.Client("support");
        using var beta = await w.Client("supportb", w.B);
        var (alphaId, v) = await w.CreateIncident(alpha);
        v = await w.Post(alpha, alphaId, "/actions", new { expectedVersion = v, title = "act", ownerUserId = w.Users["support"].Id });
        var actionId = await w.Scalar<Guid>("SELECT id FROM it_incident_actions WHERE incident_id=@i", ("i", alphaId));
        var user = w.Users["support"].Id;
        var statements = new[]
        {
            "INSERT INTO it_incident_events(organization_id,incident_id,sequence,kind,reason_code,actor_user_id,incident_version) VALUES(@b,@i,99,'NoteAdded','x',@u,99)",
            "INSERT INTO it_incident_notes(organization_id,incident_id,body,actor_user_id) VALUES(@b,@i,'x',@u)",
            "INSERT INTO it_incident_actions(organization_id,incident_id,title,owner_user_id) VALUES(@b,@i,'x',@u)",
            "INSERT INTO it_incident_evidence(organization_id,incident_id,kind,service_code,observation_code,source_status,observed_at_utc,actor_user_id) VALUES(@b,@i,'ManualObservation','api','x','unknown',now(),@u)",
            "INSERT INTO it_incident_action_events(organization_id,incident_id,action_id,action_version,new_state,reason_code,actor_user_id) VALUES(@b,@i,@a,9,'Proposed','x',@u)",
            "INSERT INTO it_incident_operation_ids(organization_id,actor_user_id,operation_key,operation_kind,payload_digest,incident_id) VALUES(@b,@u,gen_random_uuid(),'Note','x',@i)",
        };
        foreach (var sql in statements)
        {
            await using var connection = new NpgsqlConnection(w.Roles.Admin);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("b", w.B);
            command.Parameters.AddWithValue("i", alphaId);
            command.Parameters.AddWithValue("u", user);
            command.Parameters.AddWithValue("a", actionId);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, failure.SqlState);
        }
        // sequence must equal the incident version and the event kind is a closed vocabulary
        foreach (var sql in new[]
        {
            "INSERT INTO it_incident_events(organization_id,incident_id,sequence,kind,reason_code,actor_user_id,incident_version) VALUES(@a,@i,50,'NoteAdded','x',@u,51)",
            "INSERT INTO it_incident_events(organization_id,incident_id,sequence,kind,reason_code,actor_user_id,incident_version) VALUES(@a,@i,50,'Invented','x',@u,50)",
            "INSERT INTO it_incident_events(organization_id,incident_id,sequence,kind,reason_code,actor_user_id,incident_version) VALUES(@a,@i,1,'NoteAdded','x',@u,1)",
        })
        {
            await using var connection = new NpgsqlConnection(w.Roles.Admin);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("a", w.A);
            command.Parameters.AddWithValue("i", alphaId);
            command.Parameters.AddWithValue("u", user);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.True(failure.SqlState is PostgresErrorCodes.CheckViolation or PostgresErrorCodes.UniqueViolation, failure.SqlState);
        }
        Assert.Equal(v, await w.EventCount(alphaId));
    }

    private sealed record Delta(string Type, string Resource, string Fields);

    private static async Task<List<Delta>> AuditSince(M18World w, DateTime from, Guid org, string? correlation = null)
    {
        await using var connection = new NpgsqlConnection(w.Roles.Admin);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT event_type,resource_type,metadata->'changedFields'::text, metadata::text, actor_kind,
                   actor_role, correlation_id, outcome, event_schema_version
            FROM audit_entries
            WHERE organization_id=@o AND occurred_at_utc>=@f AND event_type LIKE 'incident.%'
            ORDER BY occurred_at_utc, event_type
            """, connection);
        command.Parameters.AddWithValue("o", org);
        command.Parameters.AddWithValue("f", from);
        var list = new List<Delta>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            Assert.Equal("Human", reader.GetString(4));
            Assert.Equal("Succeeded", reader.GetString(7));
            Assert.Equal(1, reader.GetInt32(8));
            Assert.Equal("Support", reader.GetString(5));
            if (correlation is not null) Assert.Equal(correlation, reader.GetString(6));
            using var metadata = System.Text.Json.JsonDocument.Parse(reader.GetString(3));
            Assert.Equal(["changedFields", "operation"],
                metadata.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
            list.Add(new Delta(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }
        return list;
    }

    [Fact]
    public async Task EachSensitiveMutationAppendsExactlyItsApprovedAuditEventsWithCorrelationAndNoContent()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var canary = "CANARY-note-body-7f3a";
        async Task<(HttpResponseMessage Response, List<Delta> Events)> Observe(Func<Task<HttpResponseMessage>> action)
        {
            var from = await w.Scalar<DateTime>("SELECT clock_timestamp()");
            var response = await action();
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            var correlation = response.Headers.GetValues("X-Correlation-ID").Single();
            return (response, await AuditSince(w, from, w.A, correlation));
        }
        var created = await Observe(() => M18World.Send(support, HttpMethod.Post, w.Route(), M18World.NewIncident(canary + " title")));
        Assert.Equal(["incident.created"], created.Events.Select(e => e.Type));
        var json = await M18World.Json(created.Response);
        var id = json.GetProperty("id").GetGuid();
        var v = json.GetProperty("version").GetInt64();
        async Task<long> Step(string name, string[] expected, Func<long, Task<HttpResponseMessage>> act)
        {
            var result = await Observe(() => act(v));
            Assert.True(expected.OrderBy(e => e, StringComparer.Ordinal).SequenceEqual(result.Events.Select(e => e.Type).OrderBy(e => e, StringComparer.Ordinal)),
                $"{name}: expected [{string.Join(",", expected)}] but saw [{string.Join(",", result.Events.Select(e => e.Type))}]");
            return (await M18World.Json(result.Response)).GetProperty("version").GetInt64();
        }
        string R(string s) => w.Route(null, "/" + id + s);
        v = await Step("triage", ["incident.state_changed"], x => M18World.Send(support, HttpMethod.Post, R("/transitions"), M18LifecycleTests.Move(x, "Triage")));
        v = await Step("assign", ["incident.assigned"], x => M18World.Send(support, HttpMethod.Patch, R("/assignment"), new { expectedVersion = x, assignedTo = w.Users["support"].Id, reasonCode = "owner" }));
        v = await Step("investigate", ["incident.state_changed"], x => M18World.Send(support, HttpMethod.Post, R("/transitions"), M18LifecycleTests.Move(x, "Investigating")));
        v = await Step("note", ["incident.note_added"], x => M18World.Send(support, HttpMethod.Post, R("/notes"), new { expectedVersion = x, body = canary }));
        v = await Step("action", ["incident.action_created"], x => M18World.Send(support, HttpMethod.Post, R("/actions"), new { expectedVersion = x, title = canary + " action", ownerUserId = w.Users["support"].Id }));
        v = await Step("impact", ["incident.impact_changed"], x => M18World.Send(support, HttpMethod.Patch, R("/impact"), new { expectedVersion = x, impactCode = "customer_visible", reasonCode = "wider" }));
        v = await Step("evidence", ["incident.evidence_attached"], x => M18World.Send(support, HttpMethod.Post, R("/evidence"), new { expectedVersion = x, observationCode = "observed_state" }));
        v = await Step("severity", ["incident.severity_changed"], x => M18World.Send(support, HttpMethod.Patch, R("/severity"), new { expectedVersion = x, severity = "SEV-1", reasonCode = "escalated" }));
        v = await Step("snapshot", ["incident.evidence_attached"], x => M18World.Send(support, HttpMethod.Post, R("/m17-snapshot"), new { expectedVersion = x }));
        v = await Step("mitigated", ["incident.state_changed"], x => M18World.Send(support, HttpMethod.Post, R("/transitions"), M18LifecycleTests.Move(x, "Mitigated")));
        v = await Step("observation after mitigation", ["incident.evidence_attached"], x => M18World.Send(support, HttpMethod.Post, R("/evidence"), new { expectedVersion = x, observationCode = "after_mitigation" }));
        v = await Step("monitoring", ["incident.state_changed"], x => M18World.Send(support, HttpMethod.Post, R("/transitions"), M18LifecycleTests.Move(x, "Monitoring")));
        var action = await w.Scalar<Guid>("SELECT id FROM it_incident_actions WHERE incident_id=@i", ("i", id));
        v = await Step("action in progress", ["incident.action_changed"], x => M18World.Send(support, HttpMethod.Patch, R("/actions/" + action), new { expectedVersion = x, actionId = action, expectedActionVersion = 1L, toState = "InProgress", reasonCode = "started" }));
        v = await Step("action verified", ["incident.action_changed"], x => M18World.Send(support, HttpMethod.Patch, R("/actions/" + action), new { expectedVersion = x, actionId = action, expectedActionVersion = 2L, toState = "Verified", reasonCode = "done" }));
        v = await Step("recovery", ["incident.recovery_verified", "incident.evidence_attached", "incident.evidence_attached"],
            x => M18World.Send(support, HttpMethod.Post, R("/recovery"), new { expectedVersion = x, rootCauseCode = "pool_exhaustion" }));
        v = await Step("resolve", ["incident.state_changed"], x => M18World.Send(support, HttpMethod.Post, R("/transitions"), M18LifecycleTests.Move(x, "Resolved")));
        using var other = await w.Client("support2");
        var from = await w.Scalar<DateTime>("SELECT clock_timestamp()");
        using (var close = await M18World.Send(other, HttpMethod.Post, R("/transitions"), M18LifecycleTests.Move(v, "Closed")))
        {
            Assert.Equal(HttpStatusCode.OK, close.StatusCode);
            v = (await M18World.Json(close)).GetProperty("version").GetInt64();
        }
        await using (var connection = new NpgsqlConnection(w.Roles.Admin))
        {
            await connection.OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT event_type,actor_role FROM audit_entries WHERE organization_id=@o AND occurred_at_utc>=@f AND event_type LIKE 'incident.%'", connection);
            cmd.Parameters.AddWithValue("o", w.A); cmd.Parameters.AddWithValue("f", from);
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("incident.state_changed", reader.GetString(0));
            Assert.False(await reader.ReadAsync());
        }
        v = await Step("reopen", ["incident.reopened"], x => M18World.Send(support, HttpMethod.Post, R("/reopen"), M18LifecycleTests.Move(x, "Investigating", "regression")));
        // the audit tables and metadata never contain titles, note bodies or evidence content
        Assert.Equal(0L, await w.Scalar<long>("SELECT count(*) FROM audit_entries WHERE organization_id=@o AND (metadata::text LIKE '%CANARY%' OR event_type LIKE '%CANARY%')", ("o", w.A)));
        Assert.Equal(0L, await w.Scalar<long>("SELECT count(*) FROM audit_entries WHERE organization_id=@o AND event_type LIKE 'it\\_incident%'", ("o", w.A)));
        Assert.Equal(v, await w.EventCount(id));
    }

    [Fact]
    public async Task ReadGrantChangesAreAuditedAsGrantedAndRevoked()
    {
        using var w = await M18World.CreateAsync();
        using var admin = await w.Client("admin");
        var grantUrl = w.Route(null, "/read-grants/" + w.Users["auditor"].Id);
        var from = await w.Scalar<DateTime>("SELECT clock_timestamp()");
        using (var granted = await M18World.Send(admin, HttpMethod.Put, grantUrl,
            new { expiresAtUtc = DateTimeOffset.UtcNow.AddHours(2).ToString("O") }, operationHeader: false))
            Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        var version = await w.Scalar<long>("SELECT version FROM it_incident_read_grants WHERE user_id=@u", ("u", w.Users["auditor"].Id));
        using (var revoked = await M18World.Send(admin, HttpMethod.Delete, grantUrl, new { expectedVersion = version }, operationHeader: false))
            Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        var types = new List<string>();
        await using var connection = new NpgsqlConnection(w.Roles.Admin);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT event_type FROM audit_entries WHERE organization_id=@o AND occurred_at_utc>=@f AND event_type LIKE 'incident.%' ORDER BY occurred_at_utc", connection);
        cmd.Parameters.AddWithValue("o", w.A); cmd.Parameters.AddWithValue("f", from);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) types.Add(reader.GetString(0));
        Assert.Equal(["incident.read_grant_granted", "incident.read_grant_revoked"], types);
    }
}
