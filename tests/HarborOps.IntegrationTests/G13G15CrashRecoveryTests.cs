using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Notifications;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class G13G15CrashFactAttribute : FactAttribute
{
    public G13G15CrashFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("HARBOROPS_G13_G15_CRASH_PROBE") != "1" ||
            Environment.GetEnvironmentVariable("HARBOROPS_G13_EXCLUSIVE_POSTGRES") != "1")
            Skip = "Crash/restart probe requires an exclusive disposable PostgreSQL runner";
    }
}

public sealed class G13G15CrashRecoveryTests
{
    private sealed record Marker(string Phase, Guid Organization, Guid Target,
        string Email);

    private static string MarkerFile()
    {
        var path = Environment.GetEnvironmentVariable("HARBOROPS_CRASH_MARKER")
            ?? throw new InvalidOperationException("Missing crash probe path");
        var dir = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Missing crash probe directory");
        Directory.CreateDirectory(dir);
        return path;
    }

    private static async Task MarkAndHold(string phase, Guid org, Guid target, string email)
    {
        await File.WriteAllTextAsync(MarkerFile(),
            JsonSerializer.Serialize(new Marker(phase, org, target, email)));
        await Console.Out.FlushAsync();
        await Task.Delay(Timeout.InfiniteTimeSpan);
    }

    private static async Task<Marker> ReadMarker()
    {
        var value = await File.ReadAllTextAsync(MarkerFile());
        return JsonSerializer.Deserialize<Marker>(value)
            ?? throw new InvalidOperationException("Crash marker invalid");
    }

    [G13G15CrashFact]
    public async Task G13BeforeReportCommit()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var factory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var shipment = await AssignmentHttpTests.Seed(fixture);
        var report = Guid.NewGuid();
        await using var db = new NpgsqlConnection(roles.Admin);
        await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();
        Guid user, session;
        await using (var authority = new NpgsqlCommand("""
            SELECT a.user_id,a.id FROM auth_sessions a
            JOIN organization_memberships m ON m.user_id=a.user_id
                AND m.organization_id=a.selected_organization_id
            WHERE a.selected_organization_id=@org AND m.role='Manager'
                AND m.status='Active' AND a.revoked_at_utc IS NULL
            ORDER BY a.issued_at_utc DESC LIMIT 1
            """, db, tx))
        {
            authority.Parameters.AddWithValue("org", fixture.A);
            await using var rows = await authority.ExecuteReaderAsync();
            Assert.True(await rows.ReadAsync());
            user = rows.GetGuid(0);
            session = rows.GetGuid(1);
        }
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO operational_kpi_reports (
                id,organization_id,actor_user_id,actor_session_id,policy_version,
                filters_sha256,from_utc,before_utc,evaluated_at_utc,expires_at_utc)
            VALUES (@report,@org,@user,@session,'m13-kpi-v1',repeat('a',64),
                clock_timestamp()-interval '7 days',clock_timestamp(),
                clock_timestamp(),clock_timestamp()+interval '10 minutes')
            """, db, tx))
        {
            insert.Parameters.AddWithValue("report", report);
            insert.Parameters.AddWithValue("org", fixture.A);
            insert.Parameters.AddWithValue("user", user);
            insert.Parameters.AddWithValue("session", session);
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());
        }
        await using (var member = new NpgsqlCommand("""
            INSERT INTO operational_kpi_members (
                organization_id,report_id,metric_code,dimension,
                member_key,shipment_id)
            VALUES (@org,@report,'K01','',@shipment,@shipment)
            """, db, tx))
        {
            member.Parameters.AddWithValue("org", fixture.A);
            member.Parameters.AddWithValue("report", report);
            member.Parameters.AddWithValue("shipment", shipment.Shipment);
            Assert.Equal(1, await member.ExecuteNonQueryAsync());
        }
        await MarkAndHold("G13BeforeReportCommit", fixture.A, report,
            fixture.ManagerEmail);
    }

    [G13G15CrashFact]
    public async Task G13AfterReportCommit()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var factory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var seedManager = fixture.Manager;
        using var seedAnalyst = fixture.Analyst;
        await AssignmentHttpTests.Seed(fixture);
        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var manager = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(manager, fixture.ManagerEmail);
        await ReferenceDataHttpTests.Select(manager, fixture.A);
        using var response = await manager.GetAsync(
            $"/api/v1/organizations/{fixture.A}/analytics/overview");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var value = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(value.GetProperty("memberCount").GetInt32() > 0);
        await MarkAndHold("G13AfterReportCommit", fixture.A,
            value.GetProperty("reportId").GetGuid(), fixture.ManagerEmail);
    }

    [G13G15CrashFact]
    public async Task G15BeforeHistoryCommit()
    {
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var factory = s.Factory;
        using var runtime = s.Runtime;
        using var administrator = s.Administrator;
        using var analyst = s.Analyst;
        using var manager = s.Manager;
        using var originalAdmin = s.Fixture.Admin;
        using var originalManager = s.Fixture.Manager;
        using var originalAnalyst = s.Fixture.Analyst;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        var exceptionId = await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        var historyId = Guid.NewGuid();
        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO shipment_exception_history (
                id,organization_id,exception_id,action,old_state,new_state,
                actor_kind,source_fingerprint)
            SELECT @history,e.organization_id,e.id,'Opened',NULL,'Open',
                'System',e.source_fingerprint
            FROM shipment_exceptions e WHERE e.id=@exception
            """, db, tx))
        {
            insert.Parameters.AddWithValue("history", historyId);
            insert.Parameters.AddWithValue("exception", exceptionId);
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());
        }
        await MarkAndHold("G15BeforeHistoryCommit", s.Fixture.A,
            historyId, s.Fixture.ManagerEmail);
    }

    [G13G15CrashFact]
    public async Task G15AfterReceiptCommit()
    {
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var factory = s.Factory;
        using var runtime = s.Runtime;
        using var administrator = s.Administrator;
        using var analyst = s.Analyst;
        using var manager = s.Manager;
        using var originalAdmin = s.Fixture.Admin;
        using var originalManager = s.Fixture.Manager;
        using var originalAnalyst = s.Fixture.Analyst;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        await M15NotificationRuntimeTests.EnsureShipmentDelivered(s, shipment);
        var route = $"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var listed = await manager.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var data = await listed.Content.ReadFromJsonAsync<JsonElement>();
        var id = Assert.Single(data.GetProperty("items").EnumerateArray())
            .GetProperty("id").GetGuid();
        using var applied = await M11ExceptionRuntimeTests.SendAction(
            manager, route + "/" + id,
            new { action = "Acknowledge", expectedVersion = 1L },
            Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        Assert.Equal(2L, (await applied.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("version").GetInt64());
        await MarkAndHold("G15AfterReceiptCommit", s.Fixture.A, id,
            s.Fixture.ManagerEmail);
    }

    // Dispatch is executed through the production SQL function using a real
    // signed dispatcher scope. An uncommitted fanout must disappear on SIGKILL,
    // while a committed fanout must survive and remain effect-once.
    private static async Task StageFanoutCrash(string phase, bool commit)
    {
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var factory = s.Factory;
        using var runtime = s.Runtime;
        using var administrator = s.Administrator;
        using var analyst = s.Analyst;
        using var manager = s.Manager;
        using var adminSeed = s.Fixture.Admin;
        using var managerSeed = s.Fixture.Manager;
        using var analystSeed = s.Fixture.Analyst;

        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        Guid history;
        await using (var adminDb = new NpgsqlConnection(s.Roles.Admin))
        {
            await adminDb.OpenAsync();
            await using var command = new NpgsqlCommand("""
                SELECT source_history_id FROM m15_notification_outbox
                WHERE organization_id=@org AND shipment_id=@shipment
                    AND completed_at_utc IS NULL
                ORDER BY created_at_utc DESC LIMIT 1
                """, adminDb);
            command.Parameters.AddWithValue("org", s.Fixture.A);
            command.Parameters.AddWithValue("shipment", shipment);
            history = Assert.IsType<Guid>(await command.ExecuteScalarAsync());
        }

        await using var db = new NpgsqlConnection(s.Roles.Api);
        await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();
        var expiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30;
        var key = Convert.FromBase64String(ReferenceDataHttpTests.CorrectionProofKey);
        var signature = Convert.ToHexString(
            System.Security.Cryptography.HMACSHA256.HashData(key,
                System.Text.Encoding.UTF8.GetBytes($"m15-dispatch-v1|{expiry}")))
            .ToLowerInvariant();
        await using (var scope = new NpgsqlCommand(
            "SELECT set_config('harborops.m15_dispatch_scope',@scope,true)", db, tx))
        {
            scope.Parameters.AddWithValue("scope", $"{expiry}|{signature}");
            await scope.ExecuteNonQueryAsync();
        }
        await using (var dispatch = new NpgsqlCommand(
            "SELECT m15_deliver_pending(20)", db, tx))
            Assert.True(Convert.ToInt32(await dispatch.ExecuteScalarAsync()) > 0);
        if (commit)
            await tx.CommitAsync();
        await MarkAndHold(phase, s.Fixture.A, history, s.Fixture.ManagerEmail);
    }

    [G13G15CrashFact]
    public Task G15BeforeFanoutCommit() =>
        StageFanoutCrash("G15BeforeFanoutCommit", false);

    [G13G15CrashFact]
    public Task G15AfterFanoutCommit() =>
        StageFanoutCrash("G15AfterFanoutCommit", true);

    [G13G15CrashFact]
    public async Task G15BeforeClaimCommit()
    {
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var factory = s.Factory;
        using var runtime = s.Runtime;
        using var administrator = s.Administrator;
        using var analyst = s.Analyst;
        using var manager = s.Manager;
        using var adminSeed = s.Fixture.Admin;
        using var managerSeed = s.Fixture.Manager;
        using var analystSeed = s.Fixture.Analyst;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();
        await using var claim = new NpgsqlCommand("""
            SELECT source_history_id FROM m15_notification_outbox
            WHERE organization_id=@org AND shipment_id=@shipment
                AND completed_at_utc IS NULL
            LIMIT 1 FOR UPDATE SKIP LOCKED
            """, db, tx);
        claim.Parameters.AddWithValue("org", s.Fixture.A);
        claim.Parameters.AddWithValue("shipment", shipment);
        var history = Assert.IsType<Guid>(await claim.ExecuteScalarAsync());
        await MarkAndHold("G15BeforeClaimCommit", s.Fixture.A,
            history, s.Fixture.ManagerEmail);
    }

    [G13G15CrashFact]
    public async Task G15FanoutRecoveredInNewApiProcess()
    {
        var marker = await ReadMarker();
        Assert.Contains(marker.Phase, new[]
        {
            "G15BeforeClaimCommit", "G15BeforeFanoutCommit", "G15AfterFanoutCommit"
        });
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        await using var db = new NpgsqlConnection(roles.Admin);
        await db.OpenAsync();
        async Task<long> Count(string table)
        {
            await using var sql = new NpgsqlCommand(
                "SELECT COUNT(*) FROM " + table +
                " WHERE source_history_id=@id AND organization_id=@org", db);
            sql.Parameters.AddWithValue("id", marker.Target);
            sql.Parameters.AddWithValue("org", marker.Organization);
            return Convert.ToInt64(await sql.ExecuteScalarAsync());
        }
        Assert.Equal(1, await Count("m15_notification_outbox"));
        var before = await Count("m15_user_notifications");
        if (marker.Phase == "G15AfterFanoutCommit")
            Assert.True(before > 0, "Committed fanout receipts disappeared");
        else
            Assert.Equal(0, before);

        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        await using (var scope = runtime.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<NotificationStore>();
            await store.DeliverPendingAsync(CancellationToken.None);
            await store.DeliverPendingAsync(CancellationToken.None);
        }
        var receipts = await Count("m15_user_notifications");
        Assert.True(receipts > 0, "Committed source did not recover delivery");
        Assert.Equal(receipts, await Count("m15_user_notifications"));
        await using (var duplicates = new NpgsqlCommand("""
            SELECT COUNT(*) FROM (
                SELECT recipient_user_id FROM m15_user_notifications
                WHERE source_history_id=@id GROUP BY recipient_user_id
                HAVING COUNT(*) > 1
            ) d
            """, db))
        {
            duplicates.Parameters.AddWithValue("id", marker.Target);
            Assert.Equal(0L, Convert.ToInt64(await duplicates.ExecuteScalarAsync()));
        }
        await using (var status = new NpgsqlCommand("""
            SELECT completed_at_utc IS NOT NULL
            FROM m15_notification_outbox WHERE source_history_id=@id
            """, db))
        {
            status.Parameters.AddWithValue("id", marker.Target);
            Assert.Equal(true, await status.ExecuteScalarAsync());
        }
        using var manager = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(manager, marker.Email);
        await ReferenceDataHttpTests.Select(manager, marker.Organization);
        using var response = await manager.GetAsync(
            $"/api/v1/organizations/{marker.Organization}/notifications");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Console.WriteLine($"G15_CRASH phase={marker.Phase} recovered=true recipients={receipts}");
    }

    [G13G15CrashFact]
    public async Task G13AfterReportCommitRecoveredInNewApiProcess()
    {
        var marker = await ReadMarker();
        Assert.Equal("G13AfterReportCommit", marker.Phase);
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        await using var db = new NpgsqlConnection(roles.Admin);
        await db.OpenAsync();
        await using (var reports = new NpgsqlCommand("""
            SELECT COUNT(*) FROM operational_kpi_reports
            WHERE id=@id AND organization_id=@org
            """, db))
        {
            reports.Parameters.AddWithValue("id", marker.Target);
            reports.Parameters.AddWithValue("org", marker.Organization);
            Assert.Equal(1L, Convert.ToInt64(await reports.ExecuteScalarAsync()));
        }
        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var manager = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(manager, marker.Email);
        await ReferenceDataHttpTests.Select(manager, marker.Organization);
        using var restored = await manager.GetAsync(
            $"/api/v1/organizations/{marker.Organization}/analytics/overview");
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        var fresh = await restored.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(marker.Target, fresh.GetProperty("reportId").GetGuid());
        Console.WriteLine("G13_CRASH committed_report_survived=true fresh_authorization=true");
    }

    [G13G15CrashFact]
    public async Task G15AfterReceiptCommitRecoveredInNewApiProcess()
    {
        var marker = await ReadMarker();
        Assert.Equal("G15AfterReceiptCommit", marker.Phase);
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var manager = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(manager, marker.Email);
        await ReferenceDataHttpTests.Select(manager, marker.Organization);
        using var response = await manager.GetAsync(
            $"/api/v1/organizations/{marker.Organization}/notifications/{marker.Target}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var receipt = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Acknowledged", receipt.GetProperty("state").GetString());
        Assert.Equal(2L, receipt.GetProperty("version").GetInt64());
        await using var sql = new NpgsqlConnection(roles.Admin);
        await sql.OpenAsync();
        await using var audit = new NpgsqlCommand("""
            SELECT COUNT(*) FROM audit_entries
            WHERE organization_id=@org AND resource_id=@id
                AND event_type='notification.receipt_acknowledged'
            """, sql);
        audit.Parameters.AddWithValue("org", marker.Organization);
        audit.Parameters.AddWithValue("id", marker.Target);
        Assert.Equal(1L, Convert.ToInt64(await audit.ExecuteScalarAsync()));
        Console.WriteLine("G15_CRASH receipt_committed=true audit_once=true restored_reader=true");
    }
}
