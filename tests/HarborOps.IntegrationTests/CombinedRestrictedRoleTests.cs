using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Api.Audit;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

/// <summary>
/// Combined M9/M16 verification: the accepted M9 redrive and worker lifecycle executed under the
/// actual restricted harborops_api and harborops_worker database identities provisioned by the
/// M16 migration-time provisioner, with M16 audit capture active on the same database.
/// </summary>
public sealed class CombinedRestrictedRoleTests
{
    private sealed record Roles(string Admin, string Api, string Worker);

    private static async Task<Roles> ProvisionAsync()
    {
        var admin = TestDatabase.ConnectionString();
        await using var db = new HarborOpsDbContext(
            new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(admin).Options);
        await db.Database.MigrateAsync();
        var apiSecret = "api-" + Guid.NewGuid().ToString("N");
        var workerSecret = "worker-" + Guid.NewGuid().ToString("N");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AuditRoles:ApiPassword"] = apiSecret,
            ["AuditRoles:WorkerPassword"] = workerSecret
        }).Build();
        await AuditRoleProvisioner.ProvisionAsync(db, configuration, CancellationToken.None);
        // Provisioning twice must be safe; the second run is the idempotency control.
        await AuditRoleProvisioner.ProvisionAsync(db, configuration, CancellationToken.None);
        string For(string role, string secret) => new NpgsqlConnectionStringBuilder(admin)
        { Username = role, Password = secret }.ConnectionString;
        return new Roles(admin, For("harborops_api", apiSecret), For("harborops_worker", workerSecret));
    }

    private static HarborOpsDbContext Context(string connection) => new(
        new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(connection).Options);

    private static async Task<object?> Scalar(string connection, string sql, params (string, object)[] values)
    {
        await using var direct = new NpgsqlConnection(connection);
        await direct.OpenAsync();
        await using var command = new NpgsqlCommand(sql, direct);
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        return await command.ExecuteScalarAsync();
    }

    private static async Task<string> Kinds(string connection, Guid receipt) =>
        (string)(await Scalar(connection, """
            SELECT COALESCE(string_agg(event_kind, ',' ORDER BY occurred_at_utc, id), '')
            FROM tracking_work_attempt_events WHERE receipt_id=@r
            """, ("r", receipt)))!;

    private static async Task<PostgresException> Denied(string connection, string sql,
        params (string, object)[] values)
    {
        await using var direct = new NpgsqlConnection(connection);
        await direct.OpenAsync();
        await using var command = new NpgsqlCommand(sql, direct);
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        return await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task ManagerRedriveAndWorkerLifecycleRunUnderTheRealRestrictedRoles()
    {
        var roles = await ProvisionAsync();
        using var adminFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(adminFactory);
        using var adminClient = fixture.Admin;
        using var seededManager = fixture.Manager;
        using var seededAnalyst = fixture.Analyst;
        var resolvable = await G9Support.SeedResolvable(adminFactory, fixture);
        var source = await G9Support.NewSource(adminFactory, fixture.A, "cg16");

        // Everything below the HTTP boundary uses the restricted API identity.
        using var apiFactory = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var manager = apiFactory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var analyst = apiFactory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(manager, fixture.ManagerEmail);
        await ReferenceDataHttpTests.Login(analyst, fixture.AnalystEmail);
        await ReferenceDataHttpTests.Select(manager, fixture.A);
        await ReferenceDataHttpTests.Select(analyst, fixture.A);
        Guid managerId;
        await using (var lookup = Context(roles.Admin))
            managerId = (await lookup.AuthUsers.AsNoTracking().SingleAsync(x =>
                x.NormalizedEmail == IdentityStore.NormalizeEmail(fixture.ManagerEmail))).Id;

        // Intake through the restricted API role: receipt, accepted event, work item and the
        // SECURITY DEFINER M16 capture for the accepted event.
        var parked = await G9Support.Accept(apiFactory, source,
            G9Support.EquipmentPayload("cg16-" + Guid.NewGuid().ToString("N")[..10], "SYN-BOX-UNKNOWN"));

        // Tenant isolation of the M9 queue read under the restricted API identity: a foreign tenant's
        // work exists in the same table, yet it is neither listed nor readable through this tenant.
        var foreignSource = await G9Support.NewSource(adminFactory, fixture.B, "cg16b");
        var foreignReceipt = await G9Support.Accept(adminFactory, foreignSource,
            G9Support.EquipmentPayload("cg16b-" + Guid.NewGuid().ToString("N")[..10], "SYN-BOX-UNKNOWN"));
        var queue = "/api/v1/organizations/" + fixture.A + "/tracking-work";
        using (var listed = await manager.GetAsync(queue + "?limit=100"))
        {
            Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
            var ids = (await listed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items")
                .EnumerateArray().Select(x => x.GetProperty("receiptId").GetGuid()).ToList();
            Assert.Contains(parked, ids);
            Assert.DoesNotContain(foreignReceipt, ids);
        }
        using (var foreignDetail = await manager.GetAsync(queue + "/" + foreignReceipt))
            Assert.Equal(HttpStatusCode.NotFound, foreignDetail.StatusCode);

        await using var workerDb = Context(roles.Worker);
        var worker = new TrackingWorkStore(workerDb, new TrackingWorkerSettings());
        var claim = await worker.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.NotNull(claim);
        Assert.Equal(parked, claim!.ReceiptId);
        Assert.Equal(TrackingWorkDisposition.NeedsReconciliation,
            await worker.FinalizeAsync(claim, CancellationToken.None));
        Assert.Equal("Claimed,ReconciliationRequired", await Kinds(roles.Admin, parked));

        // The worker identity cannot perform the Manager-only redrive transition directly.
        var forged = await Denied(roles.Worker, """
            UPDATE tracking_inbox_work_items
               SET state='Pending', attempt_count=0, redrive_generation=redrive_generation+1,
                   lease_epoch=lease_epoch+1, last_error_code=NULL, updated_at_utc=clock_timestamp(),
                   available_at_utc=clock_timestamp()
             WHERE receipt_id=@r
            """, ("r", parked));
        Assert.Equal(PostgresErrorCodes.CheckViolation, forged.SqlState);
        Assert.Equal("NeedsReconciliation", await Scalar(roles.Admin,
            "SELECT state FROM tracking_inbox_work_items WHERE receipt_id=@r", ("r", parked)));

        var url = "/api/v1/organizations/" + fixture.A + "/tracking-work/" + parked + "/redrive";
        // An Analyst can read the queue but cannot redrive: no business effect, no attempt row.
        using (var forbidden = await ReferenceDataHttpTests.Send(analyst, HttpMethod.Post, url,
            new { reason = "Analyst attempting redrive" }))
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("Claimed,ReconciliationRequired", await Kinds(roles.Admin, parked));

        using (var redrive = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post, url,
            new { reason = "Reference data corrected by operator" }))
            Assert.Equal(HttpStatusCode.OK, redrive.StatusCode);
        Assert.Equal("Claimed,ReconciliationRequired,Redriven", await Kinds(roles.Admin, parked));
        Assert.Equal(managerId, (Guid)(await Scalar(roles.Admin, """
            SELECT actor_user_id FROM tracking_work_attempt_events
            WHERE receipt_id=@r AND event_kind='Redriven'
            """, ("r", parked)))!);
        Assert.Equal("Pending", await Scalar(roles.Admin,
            "SELECT state FROM tracking_inbox_work_items WHERE receipt_id=@r", ("r", parked)));
        Assert.Equal(1, await Scalar(roles.Admin,
            "SELECT redrive_generation FROM tracking_inbox_work_items WHERE receipt_id=@r", ("r", parked)));

        // A second redrive of work that is no longer parked is refused without a new attempt row.
        using (var again = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post, url,
            new { reason = "Duplicate redrive attempt" }))
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("Claimed,ReconciliationRequired,Redriven", await Kinds(roles.Admin, parked));

        // Worker processes the redriven item again (still unresolved) and then a resolvable item.
        var reclaim = await worker.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.NotNull(reclaim);
        Assert.Equal(parked, reclaim!.ReceiptId);
        Assert.Equal(TrackingWorkDisposition.NeedsReconciliation,
            await worker.FinalizeAsync(reclaim, CancellationToken.None));
        var good = await G9Support.Accept(apiFactory, source,
            G9Support.EquipmentPayload("cg16-" + Guid.NewGuid().ToString("N")[..10], resolvable.Reference));

        // A new Pending item at generation zero has never been redriven.
        // Supplying a real active Manager identity and matching row counters
        // must not be sufficient to fabricate immutable redrive evidence.
        Assert.Equal(0, await Scalar(roles.Admin,
            "SELECT redrive_generation FROM tracking_inbox_work_items WHERE receipt_id=@r",
            ("r", good)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, (await Denied(roles.Api, """
            INSERT INTO tracking_work_attempt_events
                (id,receipt_id,organization_id,source_id,generation,attempt_count,
                 lease_epoch,event_kind,reason_code,actor_user_id,redrive_reason,occurred_at_utc)
            SELECT gen_random_uuid(),receipt_id,organization_id,source_id,
                   redrive_generation,attempt_count,lease_epoch,
                   'Redriven','manual_redrive',@actor,
                   'Fabricated redrive without a parked state',clock_timestamp()
            FROM tracking_inbox_work_items WHERE receipt_id=@r
            """, ("r", good), ("actor", managerId))).SqlState);
        Assert.Equal("", await Kinds(roles.Admin, good));

        // The restricted API identity is confined to the Manager redrive transition: it cannot
        // claim a lease, forge worker attempt history, or redrive without attributed evidence.
        Assert.Equal(PostgresErrorCodes.CheckViolation, (await Denied(roles.Api, """
            UPDATE tracking_inbox_work_items
               SET state='Leased', attempt_count=1, lease_epoch=lease_epoch+1,
                   lease_token=gen_random_uuid(),
                   lease_expires_at_utc=clock_timestamp()+interval '5 minutes',
                   updated_at_utc=clock_timestamp()
             WHERE receipt_id=@r
            """, ("r", good))).SqlState);
        foreach (var kind in new[] { "Processed", "Claimed", "Redriven" })
            Assert.Equal(PostgresErrorCodes.CheckViolation, (await Denied(roles.Api, """
                INSERT INTO tracking_work_attempt_events
                    (id,receipt_id,organization_id,source_id,generation,attempt_count,lease_epoch,
                     event_kind,actor_user_id,redrive_reason,occurred_at_utc)
                SELECT gen_random_uuid(),receipt_id,organization_id,source_id,0,1,1,@kind,
                       CASE WHEN @kind='Redriven' THEN @actor END,
                       CASE WHEN @kind='Redriven' THEN 'forged redrive reason' END,
                       clock_timestamp()
                  FROM tracking_inbox_work_items WHERE receipt_id=@r
                """, ("r", good), ("kind", kind), ("actor", managerId))).SqlState);
        // Redrive of parked work without the attributed attempt row is refused at commit.
        Assert.Equal(PostgresErrorCodes.CheckViolation, (await Denied(roles.Api, """
            UPDATE tracking_inbox_work_items
               SET state='Pending', attempt_count=0, redrive_generation=redrive_generation+1,
                   lease_epoch=lease_epoch+1, last_error_code=NULL,
                   updated_at_utc=clock_timestamp(), available_at_utc=clock_timestamp()
             WHERE receipt_id=@r
            """, ("r", parked))).SqlState);
        Assert.Equal("Claimed,ReconciliationRequired,Redriven,Claimed,ReconciliationRequired",
            await Kinds(roles.Admin, parked));
        Assert.Equal("", await Kinds(roles.Admin, good));

        var goodClaim = await worker.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.NotNull(goodClaim);
        Assert.Equal(good, goodClaim!.ReceiptId);
        Assert.Equal(TrackingWorkDisposition.Processed,
            await worker.FinalizeAsync(goodClaim, CancellationToken.None));
        Assert.Equal("Claimed,Processed", await Kinds(roles.Admin, good));
        Assert.Equal(1L, await Scalar(roles.Admin,
            "SELECT count(*) FROM tracking_processing_effects WHERE receipt_id=@r", ("r", good)));

        // Immutable M9 history and effects stay immutable for both runtime identities.
        foreach (var (name, connection) in new[] { ("worker", roles.Worker), ("api", roles.Api) })
        {
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Denied(connection,
                "UPDATE tracking_work_attempt_events SET reason_code='x' WHERE receipt_id=@r", ("r", good))).SqlState);
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Denied(connection,
                "DELETE FROM tracking_work_attempt_events WHERE receipt_id=@r", ("r", good))).SqlState);
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Denied(connection,
                "UPDATE tracking_processing_effects SET container_id=container_id WHERE receipt_id=@r", ("r", good))).SqlState);
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Denied(connection,
                "DELETE FROM tracking_processing_effects WHERE receipt_id=@r", ("r", good))).SqlState);
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Denied(connection,
                "DELETE FROM tracking_inbox_work_items WHERE receipt_id=@r", ("r", good))).SqlState);
            _ = name;
        }
        // Only the worker may write effects; the API identity must not.
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Denied(roles.Api,
            "INSERT INTO tracking_processing_effects DEFAULT VALUES")).SqlState);
        // The worker must never write attempt history directly.
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Denied(roles.Worker,
            "INSERT INTO tracking_work_attempt_events DEFAULT VALUES")).SqlState);

        // Source attribution: accepted events are audited as the integration source, never as a human.
        Assert.Equal(2L, await Scalar(roles.Admin, """
            SELECT count(*) FROM audit_entries
            WHERE organization_id=@o AND event_type='integration.tracking_accepted'
              AND actor_kind='IntegrationSource' AND actor_source_id=@s
              AND actor_user_id IS NULL AND actor_session_id IS NULL AND actor_role IS NULL
            """, ("o", fixture.A), ("s", source.Id)));
        // The worker transitions and the manual redrive write M9 history, not duplicate M16 rows.
        Assert.Equal(0L, await Scalar(roles.Admin, """
            SELECT count(*) FROM audit_entries
            WHERE organization_id=@o AND resource_type IN
                ('tracking_work_attempt_events','tracking_inbox_work_items','tracking_processing_effects')
            """, ("o", fixture.A)));
    }

    [Fact]
    public async Task RuntimeRolesCarryExactlyTheCombinedGrantContract()
    {
        var roles = await ProvisionAsync();
        await using var connection = new NpgsqlConnection(roles.Admin);
        await connection.OpenAsync();
        async Task<bool> Has(string role, string table, string privilege)
        {
            await using var command = new NpgsqlCommand(
                "SELECT has_table_privilege(@role, @table, @privilege)", connection);
            command.Parameters.AddWithValue("role", role);
            command.Parameters.AddWithValue("table", "public." + table);
            command.Parameters.AddWithValue("privilege", privilege);
            return (bool)(await command.ExecuteScalarAsync())!;
        }
        // API: Manager redrive needs work-item UPDATE and attempt INSERT only.
        Assert.True(await Has("harborops_api", "tracking_inbox_work_items", "UPDATE"));
        Assert.True(await Has("harborops_api", "tracking_work_attempt_events", "INSERT"));
        foreach (var table in new[] { "tracking_work_attempt_events", "tracking_processing_effects", "audit_entries" })
            foreach (var privilege in new[] { "UPDATE", "DELETE", "TRUNCATE" })
                Assert.False(await Has("harborops_api", table, privilege), $"api {privilege} {table}");
        Assert.False(await Has("harborops_api", "tracking_processing_effects", "INSERT"));
        Assert.False(await Has("harborops_api", "audit_entries", "INSERT"));
        // M10 is an additive API capability. The accepted M16 audit and M9
        // worker grant matrix remains unchanged; only these scoped API writes expand.
        Assert.False(await Has("harborops_api", "shipment_tracking_corrections", "INSERT"));
        Assert.False(await Has("harborops_api", "m10_correction_proof_keys", "SELECT"));
        Assert.False(await Has("harborops_api", "m10_correction_proof_nonces", "SELECT"));
        foreach (var table in new[] { "shipment_tracking_operation_replays",
                     "shipment_tracking_projections", "shipment_projection_applications" })
            Assert.True(await Has("harborops_api", table, "INSERT"), $"M10 api INSERT {table}");
        Assert.True(await Has("harborops_api", "shipment_tracking_projections", "UPDATE"));
        foreach (var table in new[] { "shipment_tracking_timeline_entries",
                     "shipment_tracking_input_versions", "shipment_tracking_correction_heads" })
            foreach (var privilege in new[] { "INSERT", "UPDATE", "DELETE" })
                Assert.False(await Has("harborops_api", table, privilege), $"M10 evidence {table} {privilege}");
        foreach (var table in new[] { "shipment_tracking_corrections",
                     "shipment_tracking_operation_replays", "shipment_tracking_projections",
                     "shipment_projection_applications", "shipment_tracking_timeline_entries",
                     "shipment_tracking_input_versions", "shipment_tracking_correction_heads" })
            foreach (var privilege in new[] { "INSERT", "UPDATE", "DELETE" })
                Assert.False(await Has("harborops_worker", table, privilege), $"M10 worker {table} {privilege}");

        // Worker: effects INSERT and work-item column UPDATE; no attempt-history or audit writes.
        Assert.True(await Has("harborops_worker", "tracking_processing_effects", "INSERT"));
        foreach (var table in new[] { "tracking_work_attempt_events", "tracking_processing_effects",
                     "tracking_inbox_work_items", "audit_entries" })
            foreach (var privilege in new[] { "DELETE", "TRUNCATE" })
                Assert.False(await Has("harborops_worker", table, privilege), $"worker {privilege} {table}");
        Assert.False(await Has("harborops_worker", "tracking_work_attempt_events", "INSERT"));
        Assert.False(await Has("harborops_worker", "tracking_work_attempt_events", "UPDATE"));
        Assert.False(await Has("harborops_worker", "audit_entries", "SELECT"));
        Assert.False(await Has("harborops_worker", "tracking_inbox_work_items", "INSERT"));

        // The worker holds exactly the accepted M9 table privileges and nothing more, so a widened
        // grant (for example credential or source tables) fails here instead of passing silently.
        async Task<string> Granted(string role)
        {
            await using var command = new NpgsqlCommand("""
                SELECT COALESCE(string_agg(table_name || ':' || privilege_type, ',' ORDER BY table_name, privilege_type), '')
                FROM information_schema.role_table_grants
                WHERE grantee = @role AND table_schema = 'public'
                """, connection);
            command.Parameters.AddWithValue("role", role);
            return (string)(await command.ExecuteScalarAsync())!;
        }
        Assert.Equal(string.Join(",", new[]
        {
            "containers:SELECT", "organizations:SELECT", "shipment_container_assignments:SELECT",
            "shipments:SELECT", "tracking_accepted_events:SELECT", "tracking_inbox_receipts:SELECT",
            "tracking_inbox_work_items:SELECT", "tracking_processing_effects:INSERT",
            "tracking_processing_effects:SELECT", "tracking_work_attempt_events:SELECT"
        }), await Granted("harborops_worker"));
        var api = await Granted("harborops_api");
        foreach (var forbidden in new[]
        {
            "audit_entries:INSERT", "audit_entries:UPDATE", "audit_entries:DELETE", "audit_entries:TRUNCATE",
            "tracking_processing_effects:INSERT", "tracking_processing_effects:UPDATE",
            "shipment_tracking_corrections:INSERT", "m10_correction_proof_keys:SELECT",
            "m10_correction_proof_nonces:SELECT",
            "tracking_work_attempt_events:UPDATE", "tracking_work_attempt_events:DELETE",
            "tracking_inbox_receipts:UPDATE", "tracking_accepted_events:UPDATE",
            "users:DELETE", "auth_sessions:DELETE", "organizations:DELETE", "organizations:TRUNCATE"
        })
            Assert.DoesNotContain(forbidden, api.Split(','));
        foreach (var required in new[]
        {
            "audit_entries:SELECT", "tracking_inbox_work_items:UPDATE", "tracking_work_attempt_events:INSERT",
            "tracking_inbox_receipts:INSERT", "tracking_accepted_events:INSERT"
        })
            Assert.Contains(required, api.Split(','));
        var workerColumns = (string)(await new NpgsqlCommand("""
            SELECT COALESCE(string_agg(column_name, ',' ORDER BY column_name), '')
            FROM information_schema.role_column_grants
            WHERE grantee='harborops_worker' AND table_schema='public'
              AND table_name='tracking_inbox_work_items' AND privilege_type='UPDATE'
            """, connection).ExecuteScalarAsync())!;
        Assert.Equal("attempt_count,available_at_utc,completed_at_utc,last_error_code,lease_epoch," +
            "lease_expires_at_utc,lease_token,redrive_generation,resolution_kind,state,updated_at_utc",
            workerColumns);
    }
}
