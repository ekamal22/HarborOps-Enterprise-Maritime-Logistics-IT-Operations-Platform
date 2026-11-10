using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Notifications;
using HarborOps.Infrastructure.Organizations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// Closure of the G15 S25 round 1 findings. Every test uses a disposable
// PostgreSQL server and the real restricted API credentials.
[Collection("M13ExclusiveServer")]
public sealed class G15RemediationAcceptanceTests
{
    private static async Task<long> Count(NpgsqlConnection db, string sql,
        params (string Name, object Value)[] values)
    {
        await using var cmd = new NpgsqlCommand(sql, db);
        foreach (var (name, value) in values) cmd.Parameters.AddWithValue(name, value);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private static async Task<(M11ExceptionRuntimeTests.Setup Setup, Guid Shipment, Guid Exception)>
        OpenException()
    {
        var setup = await M11ExceptionRuntimeTests.Prepare();
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(setup);
        var exceptionId = await M11ExceptionRuntimeTests.EvaluateAndFind(setup, shipment);
        return (setup, shipment, exceptionId);
    }

    private static async Task<string> ProvisionMemberAsync(
        ReferenceDataHttpTests.ApiFactory factory, string adminEmail, Guid org, string role)
    {
        const string password = "Synthetic-reference-integration-passphrase-123";
        await using var scope = factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var orgs = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var admin = await identity.LoginAsync(adminEmail, password, "g15-remediation",
            CancellationToken.None);
        Assert.NotNull(admin);
        var selected = await orgs.SelectAsync(admin!.UserId, admin.SessionId, org,
            CancellationToken.None);
        Assert.NotNull(selected.Value);
        var email = "g15-remediation-" + Guid.NewGuid().ToString("N") + "@example.test";
        await identity.ProvisionAsync(email, "Remediation Reader", password,
            CancellationToken.None);
        var login = await identity.LoginAsync(email, password, "g15-remediation",
            CancellationToken.None);
        Assert.NotNull(login);
        var granted = await orgs.PutMemberAsync(admin.UserId, admin.SessionId, org,
            login!.UserId, role, "Active", null, "g15-remediation", CancellationToken.None);
        Assert.NotNull(granted.Value);
        return email;
    }

    // G15-S12-A05 and the 1,000-recipient bound: above the cap an event is
    // retried, never partially delivered, and finally quarantined; at exactly the
    // cap it is delivered to every eligible member once.
    [ExclusiveM13PostgresFact]
    public async Task RecipientCapOfOneThousandQuarantinesWithoutPartialReceiptsAndDeliversAtExactlyTheCap()
    {
        var (s, shipment, _) = await OpenException();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var seedAdmin = s.Fixture.Admin; using var seedManager = s.Fixture.Manager;
        using var seedAnalyst = s.Fixture.Analyst;
        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        const string eligibleSql = """
            SELECT COUNT(*) FROM organization_memberships m
            JOIN users u ON u.id=m.user_id
            WHERE m.organization_id=@org AND m.status='Active' AND u.status='Active'
                AND m.role IN ('Analyst','Manager','Auditor')
            """;
        var eligible = await Count(db, eligibleSql, ("org", s.Fixture.A));
        var needed = 1001 - eligible;
        Assert.True(needed > 0);
        var prefix = "g15-cap-" + Guid.NewGuid().ToString("N")[..12] + "-";
        await using (var users = new NpgsqlCommand("""
            INSERT INTO users (id,email,normalized_email,display_name,status,
                security_version,created_at_utc)
            SELECT gen_random_uuid(),@prefix || g::text || '@example.test',
                @prefix || g::text || '@example.test','Cap fixture','Active',1,
                clock_timestamp()
            FROM generate_series(1,@take) AS g
            """, db))
        {
            users.Parameters.AddWithValue("prefix", prefix);
            users.Parameters.AddWithValue("take", (int)needed);
            Assert.Equal((int)needed, await users.ExecuteNonQueryAsync());
        }
        await using (var members = new NpgsqlCommand("""
            INSERT INTO organization_memberships (id,organization_id,user_id,role,status,
                version,created_at_utc,updated_at_utc)
            SELECT gen_random_uuid(),@org,u.id,'Analyst','Active',1,
                clock_timestamp(),clock_timestamp()
            FROM users u WHERE u.normalized_email LIKE @pattern
            """, db))
        {
            members.Parameters.AddWithValue("org", s.Fixture.A);
            members.Parameters.AddWithValue("pattern", prefix + "%");
            Assert.Equal((int)needed, await members.ExecuteNonQueryAsync());
        }
        Assert.Equal(1001L, await Count(db, eligibleSql, ("org", s.Fixture.A)));

        await using var scope = runtime.Services.CreateAsyncScope();
        var delivery = scope.ServiceProvider.GetRequiredService<NotificationStore>();
        const string receipts = """
            SELECT COUNT(*) FROM m15_user_notifications
            WHERE organization_id=@org AND shipment_id=@shipment
            """;
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await using (var due = new NpgsqlCommand("""
                UPDATE m15_notification_outbox SET next_attempt_at_utc=clock_timestamp()
                WHERE organization_id=@org AND shipment_id=@shipment
                  AND quarantined_at_utc IS NULL
                """, db))
            {
                due.Parameters.AddWithValue("org", s.Fixture.A);
                due.Parameters.AddWithValue("shipment", shipment);
                due.ExecuteNonQuery();
            }
            await delivery.DeliverPendingAsync(CancellationToken.None);
            Assert.Equal(0L, await Count(db, receipts, ("org", s.Fixture.A), ("shipment", shipment)));
            await using var status = new NpgsqlCommand("""
                SELECT attempt_count,last_error_code,quarantined_at_utc IS NOT NULL,
                       completed_at_utc IS NULL
                FROM m15_notification_outbox WHERE organization_id=@org AND shipment_id=@shipment
                """, db);
            status.Parameters.AddWithValue("org", s.Fixture.A);
            status.Parameters.AddWithValue("shipment", shipment);
            await using var reader = await status.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(attempt, reader.GetInt32(0));
            Assert.Equal("recipient_limit", reader.GetString(1));
            Assert.Equal(attempt == 5, reader.GetBoolean(2));
            Assert.True(reader.GetBoolean(3));
        }

        // Bring the audience to exactly 1,000 and retry the quarantined intent.
        await using (var suspend = new NpgsqlCommand("""
            UPDATE organization_memberships SET status='Suspended',version=version+1,
                updated_at_utc=clock_timestamp()
            WHERE id=(SELECT m.id FROM organization_memberships m
                      JOIN users u ON u.id=m.user_id
                      WHERE m.organization_id=@org AND u.normalized_email LIKE @pattern
                      ORDER BY m.id LIMIT 1)
            """, db))
        {
            suspend.Parameters.AddWithValue("org", s.Fixture.A);
            suspend.Parameters.AddWithValue("pattern", prefix + "%");
            Assert.Equal(1, await suspend.ExecuteNonQueryAsync());
        }
        Assert.Equal(1000L, await Count(db, eligibleSql, ("org", s.Fixture.A)));
        await using (var requeue = new NpgsqlCommand("""
            UPDATE m15_notification_outbox SET attempt_count=0,last_error_code=NULL,
                quarantined_at_utc=NULL,next_attempt_at_utc=clock_timestamp()
            WHERE organization_id=@org AND shipment_id=@shipment
            """, db))
        {
            requeue.Parameters.AddWithValue("org", s.Fixture.A);
            requeue.Parameters.AddWithValue("shipment", shipment);
            Assert.Equal(1, await requeue.ExecuteNonQueryAsync());
        }
        for (var i = 0; i < 20 && await Count(db, receipts,
            ("org", s.Fixture.A), ("shipment", shipment)) == 0; i++)
            await delivery.DeliverPendingAsync(CancellationToken.None);
        Assert.Equal(1000L, await Count(db, receipts, ("org", s.Fixture.A), ("shipment", shipment)));
        Assert.Equal(1000L, await Count(db, """
            SELECT COUNT(DISTINCT recipient_user_id) FROM m15_user_notifications
            WHERE organization_id=@org AND shipment_id=@shipment
            """, ("org", s.Fixture.A), ("shipment", shipment)));
        Assert.Equal(1L, await Count(db, """
            SELECT COUNT(*) FROM m15_notification_outbox
            WHERE organization_id=@org AND shipment_id=@shipment
              AND completed_at_utc IS NOT NULL AND quarantined_at_utc IS NULL
            """, ("org", s.Fixture.A), ("shipment", shipment)));
    }

    // G15-S21-A03: a statement timeout inside one event is a counted retry; it
    // never aborts the batch or blocks later events behind it.
    [ExclusiveM13PostgresFact]
    public async Task DispatcherStatementTimeoutIsACountedRetryAndNeverBlocksLaterEvents()
    {
        var (s, shipment, exceptionId) = await OpenException();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var seedAdmin = s.Fixture.Admin; using var seedManager = s.Fixture.Manager;
        using var seedAnalyst = s.Fixture.Analyst;
        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        Guid poison;
        await using (var first = new NpgsqlCommand("""
            SELECT source_history_id FROM m15_notification_outbox
            WHERE organization_id=@org AND shipment_id=@shipment
            ORDER BY created_at_utc LIMIT 1
            """, db))
        {
            first.Parameters.AddWithValue("org", s.Fixture.A);
            first.Parameters.AddWithValue("shipment", shipment);
            poison = Assert.IsType<Guid>(await first.ExecuteScalarAsync());
        }
        await using (var second = new NpgsqlCommand("""
            INSERT INTO shipment_exception_history (organization_id,exception_id,action,
                old_state,new_state,actor_kind,source_fingerprint)
            SELECT organization_id,id,'Opened',NULL,'Open','System',source_fingerprint
            FROM shipment_exceptions WHERE id=@exception
            """, db))
        {
            second.Parameters.AddWithValue("exception", exceptionId);
            Assert.Equal(1, await second.ExecuteNonQueryAsync());
        }
        Guid healthy;
        await using (var later = new NpgsqlCommand("""
            SELECT source_history_id FROM m15_notification_outbox
            WHERE organization_id=@org AND shipment_id=@shipment
              AND source_history_id<>@poison
            ORDER BY created_at_utc DESC LIMIT 1
            """, db))
        {
            later.Parameters.AddWithValue("org", s.Fixture.A);
            later.Parameters.AddWithValue("shipment", shipment);
            later.Parameters.AddWithValue("poison", poison);
            healthy = Assert.IsType<Guid>(await later.ExecuteScalarAsync());
        }
        // Defer every unrelated pending intent so the batch contains only ours.
        await using (var defer = new NpgsqlCommand("""
            UPDATE m15_notification_outbox SET next_attempt_at_utc=clock_timestamp()+interval '1 hour'
            WHERE completed_at_utc IS NULL AND quarantined_at_utc IS NULL
              AND source_history_id NOT IN (@poison,@healthy)
            """, db))
        {
            defer.Parameters.AddWithValue("poison", poison);
            defer.Parameters.AddWithValue("healthy", healthy);
            await defer.ExecuteNonQueryAsync();
        }
        try
        {
            await using (var slow = new NpgsqlCommand($$"""
                CREATE FUNCTION m15_test_slow_receipt() RETURNS trigger LANGUAGE plpgsql AS $f$
                BEGIN
                    IF NEW.source_history_id='{{poison}}'::uuid THEN PERFORM pg_sleep(8); END IF;
                    RETURN NEW;
                END $f$;
                CREATE TRIGGER m15_test_slow_receipt BEFORE INSERT ON m15_user_notifications
                    FOR EACH ROW EXECUTE FUNCTION m15_test_slow_receipt();
                ALTER ROLE harborops_api SET statement_timeout='2s';
                """, db))
                await slow.ExecuteNonQueryAsync();
            NpgsqlConnection.ClearAllPools();

            await using var scope = runtime.Services.CreateAsyncScope();
            var delivery = scope.ServiceProvider.GetRequiredService<NotificationStore>();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var delivered = await delivery.DeliverPendingAsync(CancellationToken.None);
            clock.Stop();
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20), $"Batch not bounded: {clock.Elapsed}");
            Assert.Equal(1, delivered);

            await using var state = new NpgsqlCommand("""
                SELECT source_history_id,attempt_count,last_error_code,
                       completed_at_utc IS NOT NULL,quarantined_at_utc IS NOT NULL
                FROM m15_notification_outbox WHERE source_history_id IN (@poison,@healthy)
                """, db);
            state.Parameters.AddWithValue("poison", poison);
            state.Parameters.AddWithValue("healthy", healthy);
            await using var reader = await state.ExecuteReaderAsync();
            var seen = new Dictionary<Guid, (int Attempts, string? Error, bool Done, bool Quarantined)>();
            while (await reader.ReadAsync())
                seen[reader.GetGuid(0)] = (reader.GetInt32(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.GetBoolean(3), reader.GetBoolean(4));
            Assert.Equal((1, "delivery_timeout", false, false), seen[poison]);
            Assert.True(seen[healthy].Done);
            Assert.Equal(0, seen[healthy].Attempts);
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand("""
                ALTER ROLE harborops_api RESET statement_timeout;
                DROP TRIGGER IF EXISTS m15_test_slow_receipt ON m15_user_notifications;
                DROP FUNCTION IF EXISTS m15_test_slow_receipt();
                """, db);
            await cleanup.ExecuteNonQueryAsync();
            NpgsqlConnection.ClearAllPools();
        }
        // The poisoned event never produced a partial receipt.
        Assert.Equal(0L, await Count(db,
            "SELECT COUNT(*) FROM m15_user_notifications WHERE source_history_id=@id",
            ("id", poison)));
        Assert.True(await Count(db,
            "SELECT COUNT(*) FROM m15_user_notifications WHERE source_history_id=@id",
            ("id", healthy)) > 0);
    }

    // G15-S18-A05 and G15-S05-A04: acknowledging a personal alert changes only
    // personal state, and alert visibility grants no business mutation or export.
    [ExclusiveM13PostgresFact]
    public async Task AcknowledgingAnAlertNeverChangesM11StateAndAlertVisibilityGrantsNoMutationOrExport()
    {
        var (s, shipment, exceptionId) = await OpenException();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var seedAdmin = s.Fixture.Admin; using var seedManager = s.Fixture.Manager;
        using var seedAnalyst = s.Fixture.Analyst;
        await M15NotificationRuntimeTests.EnsureShipmentDelivered(s, shipment);
        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        async Task<string> M11Snapshot()
        {
            await using var snap = new NpgsqlCommand("""
                SELECT concat_ws('|',e.version,e.lifecycle_state,e.condition_state,
                    e.last_evaluated_at_utc,e.resolved_at_utc,
                    (SELECT count(*) FROM shipment_exception_history h WHERE h.exception_id=e.id),
                    s.version,s.workflow_status,s.record_status)
                FROM shipment_exceptions e JOIN shipments s ON s.id=e.shipment_id
                    AND s.organization_id=e.organization_id
                WHERE e.id=@id
                """, db);
            snap.Parameters.AddWithValue("id", exceptionId);
            return Assert.IsType<string>(await snap.ExecuteScalarAsync());
        }
        var before = await M11Snapshot();
        var root = $"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var page = await manager.GetAsync(root);
        var id = Assert.Single((await page.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid();
        using (var ack = await M11ExceptionRuntimeTests.SendAction(manager, root + "/" + id,
            new { action = "Acknowledge", expectedVersion = 1L }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.OK, ack.StatusCode);
        Assert.Equal(before, await M11Snapshot());
        // A notification id is never an exception id.
        using (var wrong = await M11ExceptionRuntimeTests.SendAction(manager,
            M11ExceptionRuntimeTests.Collection(s.Fixture.A) + "/" + id,
            new { action = "Acknowledge", expectedVersion = 1L,
                reason = "Alert review attempt" }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        Assert.Equal(before, await M11Snapshot());

        // An Auditor receives the alert yet cannot mutate a shipment, an exception
        // or export shipment data.
        var auditorEmail = await ProvisionMemberAsync(s.Factory, s.Fixture.AdminEmail,
            s.Fixture.A, "Auditor");
        using var auditor = s.Runtime.CreateClient(new WebApplicationFactoryClientOptions
            { HandleCookies = true });
        await ReferenceDataHttpTests.Login(auditor, auditorEmail);
        await ReferenceDataHttpTests.Select(auditor, s.Fixture.A);
        for (var i = 0; i < 100 && await Count(db, """
            SELECT COUNT(*) FROM m15_user_notifications n
            JOIN users u ON u.id=n.recipient_user_id
            WHERE n.organization_id=@org AND u.normalized_email=lower(@email)
            """, ("org", s.Fixture.A), ("email", auditorEmail)) == 0; i++)
        {
            await using var requeue = new NpgsqlCommand("""
                UPDATE m15_notification_outbox SET completed_at_utc=NULL,quarantined_at_utc=NULL,
                    attempt_count=0,next_attempt_at_utc=clock_timestamp()
                WHERE organization_id=@org AND shipment_id=@shipment
                """, db);
            requeue.Parameters.AddWithValue("org", s.Fixture.A);
            requeue.Parameters.AddWithValue("shipment", shipment);
            await requeue.ExecuteNonQueryAsync();
            await M15NotificationRuntimeTests.EnsureShipmentDelivered(s, shipment);
        }
        using (var visible = await auditor.GetAsync(root))
        {
            Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
            Assert.Single((await visible.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("items").EnumerateArray());
        }
        var shipmentPath = $"/api/v1/organizations/{s.Fixture.A}/shipments/{shipment}";
        using (var mutate = await ReferenceDataHttpTests.Send(auditor, HttpMethod.Patch,
            shipmentPath, new { expectedVersion = 1L, workflowStatus = "Cancelled" }))
            Assert.Equal(HttpStatusCode.Forbidden, mutate.StatusCode);
        using (var action = await M11ExceptionRuntimeTests.SendAction(auditor,
            M11ExceptionRuntimeTests.Collection(s.Fixture.A) + "/" + exceptionId,
            new { action = "Acknowledge", expectedVersion = 1L,
                reason = "Alert review attempt" }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.Forbidden, action.StatusCode);
        using (var export = await auditor.GetAsync(
            $"/api/v1/organizations/{s.Fixture.A}/exports/shipments"))
            Assert.Equal(HttpStatusCode.Forbidden, export.StatusCode);
        Assert.Equal(before, await M11Snapshot());
    }

    // G15-S17-A01 and A03: default page size 25 and a 20 minute cursor lifetime.
    [ExclusiveM13PostgresFact]
    public async Task DefaultPageSizeIsTwentyFiveAndCursorsExpireAfterTwentyMinutes()
    {
        var (s, shipment, exceptionId) = await OpenException();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var seedAdmin = s.Fixture.Admin; using var seedManager = s.Fixture.Manager;
        using var seedAnalyst = s.Fixture.Analyst;
        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        await using (var more = new NpgsqlCommand("""
            INSERT INTO shipment_exception_history (organization_id,exception_id,action,
                old_state,new_state,actor_kind,source_fingerprint)
            SELECT e.organization_id,e.id,'Opened',NULL,'Open','System',e.source_fingerprint
            FROM shipment_exceptions e CROSS JOIN generate_series(1,40) AS g WHERE e.id=@id
            """, db))
        {
            more.Parameters.AddWithValue("id", exceptionId);
            Assert.Equal(40, await more.ExecuteNonQueryAsync());
        }
        await using var scope = runtime.Services.CreateAsyncScope();
        var delivery = scope.ServiceProvider.GetRequiredService<NotificationStore>();
        for (var i = 0; i < 100 && await Count(db, """
            SELECT COUNT(*) FROM m15_notification_outbox
            WHERE organization_id=@org AND shipment_id=@shipment AND completed_at_utc IS NULL
            """, ("org", s.Fixture.A), ("shipment", shipment)) > 0; i++)
            await delivery.DeliverPendingAsync(CancellationToken.None);
        var root = $"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var first = await manager.GetAsync(root);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var body = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(25, body.GetProperty("items").GetArrayLength());
        var cursor = body.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrEmpty(cursor));
        var protector = runtime.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("HarborOps.M15.PersonalNotificationCursor.v1")
            .ToTimeLimitedDataProtector();
        protector.Unprotect(cursor!, out var expiresAt);
        var remaining = expiresAt - DateTimeOffset.UtcNow;
        Assert.InRange(remaining, TimeSpan.FromMinutes(19), TimeSpan.FromMinutes(20.1));
        using var limit50 = await manager.GetAsync(root + "?limit=50");
        var all = await limit50.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(41, all.GetProperty("items").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, all.GetProperty("nextCursor").ValueKind);
        using var over = await manager.GetAsync(root + "?limit=51");
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        using var zero = await manager.GetAsync(root + "?limit=0");
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
    }

    // G15-S10: the personal event survives a target that becomes cancelled or
    // archived; the shipment itself is governed only by current M11/M5 permission.
    [ExclusiveM13PostgresFact]
    public async Task CancelledAndArchivedTargetsKeepThePersonalEventAndGrantNothingNew()
    {
        var (s, shipment, _) = await OpenException();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var seedAdmin = s.Fixture.Admin; using var seedManager = s.Fixture.Manager;
        using var seedAnalyst = s.Fixture.Analyst;
        await M15NotificationRuntimeTests.EnsureShipmentDelivered(s, shipment);
        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        var root = $"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var before = await manager.GetAsync(root);
        var item = Assert.Single((await before.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray());
        var id = item.GetProperty("id").GetGuid();
        var detailPath = $"/api/v1/organizations/{s.Fixture.A}/shipments/{shipment}";
        using (var live = await manager.GetAsync(detailPath))
            Assert.Equal(HttpStatusCode.OK, live.StatusCode);

        await using (var cancel = new NpgsqlCommand("""
            UPDATE shipments SET workflow_status='Cancelled',version=version+1,
                updated_at_utc=clock_timestamp() WHERE organization_id=@org AND id=@id
            """, db))
        {
            cancel.Parameters.AddWithValue("org", s.Fixture.A);
            cancel.Parameters.AddWithValue("id", shipment);
            Assert.Equal(1, await cancel.ExecuteNonQueryAsync());
        }
        using (var afterCancel = await manager.GetAsync(root + "/" + id))
        {
            Assert.Equal(HttpStatusCode.OK, afterCancel.StatusCode);
            Assert.Equal(shipment, (await afterCancel.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("shipmentId").GetGuid());
        }
        using (var cancelled = await manager.GetAsync(detailPath))
        {
            Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
            Assert.Equal("Cancelled", (await cancelled.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("workflowStatus").GetString());
        }
        await using (var archive = new NpgsqlCommand("""
            UPDATE shipments SET record_status='Archived',archived_at_utc=clock_timestamp(),
                updated_at_utc=clock_timestamp(),version=version+1
            WHERE organization_id=@org AND id=@id
            """, db))
        {
            archive.Parameters.AddWithValue("org", s.Fixture.A);
            archive.Parameters.AddWithValue("id", shipment);
            Assert.Equal(1, await archive.ExecuteNonQueryAsync());
        }
        using (var afterArchive = await manager.GetAsync(root + "/" + id))
            Assert.Equal(HttpStatusCode.OK, afterArchive.StatusCode);
        using (var archived = await manager.GetAsync(detailPath))
        {
            Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
            Assert.Equal("Archived", (await archived.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("recordStatus").GetString());
        }
        // No new authority: the archived shipment cannot be edited through it.
        using (var edit = await ReferenceDataHttpTests.Send(manager, HttpMethod.Patch, detailPath,
            new { expectedVersion = 99L, displayName = "Changed" }))
            Assert.NotEqual(HttpStatusCode.OK, edit.StatusCode);
        // A missing target and a foreign tenant stay indistinguishable and generic.
        using (var missing = await manager.GetAsync(
            $"/api/v1/organizations/{s.Fixture.A}/shipments/{Guid.NewGuid()}"))
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using (var foreign = await manager.GetAsync(
            $"/api/v1/organizations/{s.Fixture.B}/notifications/{id}"))
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    // G15-S18-A03: a malformed or version-less action body never changes a receipt.
    [ExclusiveM13PostgresFact]
    public async Task MalformedOrVersionlessActionBodiesNeverChangeAReceipt()
    {
        var (s, shipment, _) = await OpenException();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var seedAdmin = s.Fixture.Admin; using var seedManager = s.Fixture.Manager;
        using var seedAnalyst = s.Fixture.Analyst;
        await M15NotificationRuntimeTests.EnsureShipmentDelivered(s, shipment);
        var root = $"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var page = await manager.GetAsync(root);
        var id = Assert.Single((await page.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid();
        var bodies = new object[]
        {
            new { action = "Acknowledge" },
            new { action = "Acknowledge", expectedVersion = 0L },
            new { action = "Acknowledge", expectedVersion = -1L },
            new { action = "Acknowledge", expectedVersion = "1" },
            new { action = "Delete", expectedVersion = 1L },
            new { expectedVersion = 1L },
            new { action = "Acknowledge", expectedVersion = 1L, extra = "field" }
        };
        foreach (var body in bodies)
        {
            using var response = await M11ExceptionRuntimeTests.SendAction(manager,
                root + "/" + id, body, Guid.NewGuid());
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var unchanged = await manager.GetAsync(root + "/" + id);
        var state = await unchanged.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, state.GetProperty("version").GetInt64());
        Assert.Equal(JsonValueKind.Null, state.GetProperty("readAtUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, state.GetProperty("acknowledgedAtUtc").ValueKind);
    }

    // G15-S14-A02 and A03: when the outbox intent cannot be written, the real M11
    // evaluation action fails as a whole (no exception, no history, no intent) and
    // a later retry succeeds.
    [ExclusiveM13PostgresFact]
    public async Task FailedOutboxIntentDuringARealM11EvaluationRollsBackTheWholeActionAndRetrySucceeds()
    {
        var setup = await M11ExceptionRuntimeTests.Prepare();
        using var factory = setup.Factory; using var runtime = setup.Runtime;
        using var manager = setup.Manager; using var analyst = setup.Analyst;
        using var administrator = setup.Administrator;
        using var seedAdmin = setup.Fixture.Admin; using var seedManager = setup.Fixture.Manager;
        using var seedAnalyst = setup.Fixture.Analyst;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(setup);
        await using var db = new NpgsqlConnection(setup.Roles.Admin);
        await db.OpenAsync();
        var route = $"/api/v1/organizations/{setup.Fixture.A}/shipments/{shipment}/exceptions";
        async Task<long> Rows(string table) => await Count(db,
            $"SELECT COUNT(*) FROM {table} WHERE organization_id=@org",
            ("org", setup.Fixture.A));
        var exceptionsBefore = await Rows("shipment_exceptions");
        var historyBefore = await Rows("shipment_exception_history");
        var outboxBefore = await Rows("m15_notification_outbox");
        try
        {
            await using (var fail = new NpgsqlCommand("""
                ALTER TABLE m15_notification_outbox ADD CONSTRAINT m15_test_reject_intent
                    CHECK (false) NOT VALID
                """, db))
                await fail.ExecuteNonQueryAsync();
            using var evaluate = await ReferenceDataHttpTests.Send(setup.Manager,
                HttpMethod.Post, route + "/evaluate");
            Assert.True((int)evaluate.StatusCode >= 500,
                $"Expected a bounded server failure, got {(int)evaluate.StatusCode}");
            var body = await evaluate.Content.ReadAsStringAsync();
            Assert.DoesNotContain("m15_test_reject_intent", body);
            Assert.Equal(exceptionsBefore, await Rows("shipment_exceptions"));
            Assert.Equal(historyBefore, await Rows("shipment_exception_history"));
            Assert.Equal(outboxBefore, await Rows("m15_notification_outbox"));
        }
        finally
        {
            await using var drop = new NpgsqlCommand(
                "ALTER TABLE m15_notification_outbox DROP CONSTRAINT IF EXISTS m15_test_reject_intent", db);
            await drop.ExecuteNonQueryAsync();
        }
        using (var retry = await ReferenceDataHttpTests.Send(setup.Manager,
            HttpMethod.Post, route + "/evaluate"))
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(exceptionsBefore + 1, await Rows("shipment_exceptions"));
        Assert.Equal(historyBefore + 1, await Rows("shipment_exception_history"));
        Assert.Equal(outboxBefore + 1, await Rows("m15_notification_outbox"));
    }

    // G15-S12-A03 and A04: several intents, two competing dispatchers and
    // readers that run during the fanout. Every intent completes once, every
    // eligible recipient gets exactly one receipt per intent, readers never see
    // a duplicate or a partial receipt set.
    [ExclusiveM13PostgresFact]
    public async Task CompetingDispatchersDeliverSeveralIntentsExactlyOnceWhileReadersRun()
    {
        var (s, shipment, exceptionId) = await OpenException();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var seedAdmin = s.Fixture.Admin; using var seedManager = s.Fixture.Manager;
        using var seedAnalyst = s.Fixture.Analyst;
        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        const int extraIntents = 11;
        await using (var more = new NpgsqlCommand("""
            INSERT INTO shipment_exception_history (organization_id,exception_id,action,
                old_state,new_state,actor_kind,source_fingerprint)
            SELECT e.organization_id,e.id,'Opened',NULL,'Open','System',e.source_fingerprint
            FROM shipment_exceptions e CROSS JOIN generate_series(1,@n) AS g WHERE e.id=@id
            """, db))
        {
            more.Parameters.AddWithValue("n", extraIntents);
            more.Parameters.AddWithValue("id", exceptionId);
            Assert.Equal(extraIntents, await more.ExecuteNonQueryAsync());
        }
        var intents = 1 + extraIntents;
        var eligible = await Count(db, """
            SELECT COUNT(*) FROM organization_memberships m JOIN users u ON u.id=m.user_id
            WHERE m.organization_id=@org AND m.status='Active' AND u.status='Active'
                AND m.role IN ('Analyst','Manager','Auditor')
            """, ("org", s.Fixture.A));
        // Keep unrelated pending intents from other suites out of this exercise.
        await using (var defer = new NpgsqlCommand("""
            UPDATE m15_notification_outbox SET next_attempt_at_utc=clock_timestamp()+interval '1 hour'
            WHERE completed_at_utc IS NULL AND quarantined_at_utc IS NULL
              AND NOT (organization_id=@org AND shipment_id=@shipment)
            """, db))
        {
            defer.Parameters.AddWithValue("org", s.Fixture.A);
            defer.Parameters.AddWithValue("shipment", shipment);
            await defer.ExecuteNonQueryAsync();
        }
        Guid actor, session;
        await using (var active = new NpgsqlCommand("""
            SELECT a.user_id,a.id FROM auth_sessions a
            JOIN organization_memberships m ON m.user_id=a.user_id
                AND m.organization_id=a.selected_organization_id
            WHERE a.selected_organization_id=@org AND m.role='Manager' AND m.status='Active'
              AND a.revoked_at_utc IS NULL ORDER BY a.issued_at_utc DESC LIMIT 1
            """, db))
        {
            active.Parameters.AddWithValue("org", s.Fixture.A);
            await using var r = await active.ExecuteReaderAsync();
            Assert.True(await r.ReadAsync());
            actor = r.GetGuid(0); session = r.GetGuid(1);
        }
        var done = false;
        var observed = new List<int>();
        var readerTask = Task.Run(async () =>
        {
            while (!Volatile.Read(ref done))
            {
                await using var scope = runtime.Services.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<NotificationStore>();
                var page = await store.ListAsync(s.Fixture.A, actor, session, "All",
                    null, null, null, 50, CancellationToken.None);
                var ids = page.Items.Select(x => x.Id).ToArray();
                Assert.Equal(ids.Length, ids.Distinct().Count());
                lock (observed) observed.Add(ids.Length);
                await Task.Delay(20);
            }
        });
        var delivered = 0;
        await using (var oneScope = runtime.Services.CreateAsyncScope())
        await using (var twoScope = runtime.Services.CreateAsyncScope())
        {
            var one = oneScope.ServiceProvider.GetRequiredService<NotificationStore>();
            var two = twoScope.ServiceProvider.GetRequiredService<NotificationStore>();
            for (var round = 0; round < 40 && await Count(db, """
                SELECT COUNT(*) FROM m15_notification_outbox
                WHERE organization_id=@org AND shipment_id=@shipment
                  AND completed_at_utc IS NULL AND quarantined_at_utc IS NULL
                """, ("org", s.Fixture.A), ("shipment", shipment)) > 0; round++)
            {
                var results = await Task.WhenAll(one.DeliverPendingAsync(CancellationToken.None),
                    two.DeliverPendingAsync(CancellationToken.None));
                delivered += results.Sum();
            }
        }
        Volatile.Write(ref done, true);
        await readerTask;
        Assert.Equal(intents, delivered);
        Assert.Equal((long)intents, await Count(db, """
            SELECT COUNT(*) FROM m15_notification_outbox
            WHERE organization_id=@org AND shipment_id=@shipment
              AND completed_at_utc IS NOT NULL AND quarantined_at_utc IS NULL
            """, ("org", s.Fixture.A), ("shipment", shipment)));
        Assert.Equal(intents * eligible, await Count(db, """
            SELECT COUNT(*) FROM m15_user_notifications
            WHERE organization_id=@org AND shipment_id=@shipment
            """, ("org", s.Fixture.A), ("shipment", shipment)));
        Assert.Equal(intents * eligible, await Count(db, """
            SELECT COUNT(DISTINCT (source_history_id,recipient_user_id))
            FROM m15_user_notifications WHERE organization_id=@org AND shipment_id=@shipment
            """, ("org", s.Fixture.A), ("shipment", shipment)));
        // Readers only ever observed complete per-intent receipt sets.
        lock (observed)
        {
            Assert.NotEmpty(observed);
            Assert.All(observed, n => Assert.InRange(n, 0, intents));
        }
    }

    // The compare-and-set must reject a stale expected version even when the
    // requested transition itself would be valid.
    [ExclusiveM13PostgresFact]
    public async Task StaleExpectedVersionOnAValidTransitionIsRejectedAsAConflict()
    {
        var (s, shipment, _) = await OpenException();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var seedAdmin = s.Fixture.Admin; using var seedManager = s.Fixture.Manager;
        using var seedAnalyst = s.Fixture.Analyst;
        await M15NotificationRuntimeTests.EnsureShipmentDelivered(s, shipment);
        var root = $"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var page = await manager.GetAsync(root);
        var id = Assert.Single((await page.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid();
        using (var read = await M11ExceptionRuntimeTests.SendAction(manager, root + "/" + id,
            new { action = "MarkRead", expectedVersion = 1L }, Guid.NewGuid()))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.Equal(2, (await read.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("version").GetInt64());
        }
        // Version 1 is stale now; Acknowledge is otherwise a legal next step.
        using (var stale = await M11ExceptionRuntimeTests.SendAction(manager, root + "/" + id,
            new { action = "Acknowledge", expectedVersion = 1L }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var unchanged = await manager.GetAsync(root + "/" + id))
        {
            var state = await unchanged.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(2, state.GetProperty("version").GetInt64());
            Assert.Equal(JsonValueKind.Null, state.GetProperty("acknowledgedAtUtc").ValueKind);
        }
        using (var current = await M11ExceptionRuntimeTests.SendAction(manager, root + "/" + id,
            new { action = "Acknowledge", expectedVersion = 2L }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.OK, current.StatusCode);
    }
}
