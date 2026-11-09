using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

/// <summary>
/// Live restricted-role notification and reporting authorization checks.
/// Every case provisions isolated records on disposable PostgreSQL.
/// </summary>
public sealed class G13G15RevocationAcceptanceTests
{
    private static readonly HttpStatusCode[] RevokedResponses =
    {
        HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden,
        HttpStatusCode.NotFound, HttpStatusCode.Conflict
    };

    private static async Task<Guid> LatestSession(
        NpgsqlConnection db, string email, Guid organization)
    {
        await using var command = new NpgsqlCommand("""
            SELECT s.id FROM auth_sessions s JOIN users u ON u.id=s.user_id
            WHERE u.normalized_email=@email AND s.selected_organization_id=@org
                AND s.revoked_at_utc IS NULL
            ORDER BY s.issued_at_utc DESC,s.id DESC LIMIT 1
            """, db);
        command.Parameters.AddWithValue("email", IdentityStore.NormalizeEmail(email));
        command.Parameters.AddWithValue("org", organization);
        return Assert.IsType<Guid>(await command.ExecuteScalarAsync());
    }

    private static async Task<Guid> User(NpgsqlConnection db, string email)
    {
        await using var command = new NpgsqlCommand(
            "SELECT id FROM users WHERE normalized_email=@email", db);
        command.Parameters.AddWithValue("email", IdentityStore.NormalizeEmail(email));
        return Assert.IsType<Guid>(await command.ExecuteScalarAsync());
    }

    private static async Task Execute(
        NpgsqlConnection db, string sql, params (string Name, object Value)[] args)
    {
        await using var command = new NpgsqlCommand(sql, db);
        foreach (var (name, value) in args)
            command.Parameters.AddWithValue(name, value);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private static async Task<string> Csrf(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetString()!;
    }

    private static async Task<HttpResponseMessage> PreparedAction(
        HttpClient client, string path, string csrf, Guid replay)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, path);
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
        request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF", csrf);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", replay.ToString());
        request.Content = JsonContent.Create(new { action = "Acknowledge", expectedVersion = 1L });
        return await client.SendAsync(request);
    }

    private static async Task AssertUnchangedReceipt(
        NpgsqlConnection db, Guid notification)
    {
        await using var command = new NpgsqlCommand("""
            SELECT version,read_at_utc,acknowledged_at_utc
            FROM m15_user_notifications WHERE id=@notification
            """, db);
        command.Parameters.AddWithValue("notification", notification);
        await using var row = await command.ExecuteReaderAsync();
        Assert.True(await row.ReadAsync());
        Assert.Equal(1L, row.GetInt64(0));
        Assert.True(row.IsDBNull(1));
        Assert.True(row.IsDBNull(2));
    }

    [Theory]
    [InlineData("session_revoked")]
    [InlineData("idle_expired")]
    [InlineData("absolute_expired")]
    [InlineData("security_version")]
    [InlineData("account_disabled")]
    [InlineData("organization_suspended")]
    [InlineData("membership_removed")]
    [InlineData("role_support")]
    [InlineData("organization_changed")]
    [InlineData("logout")]
    [InlineData("logout_all")]
    public async Task RevokedNotificationScopeCannotReadCountDetailOrAcknowledge(
        string revocation)
    {
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var factory = s.Factory;
        using var runtime = s.Runtime;
        using var manager = s.Manager;
        using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var originalAdmin = s.Fixture.Admin;
        using var originalManager = s.Fixture.Manager;
        using var originalAnalyst = s.Fixture.Analyst;

        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        await M15NotificationRuntimeTests.EnsureShipmentDelivered(s, shipment);
        var root = $"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var baseline = await manager.GetAsync(root);
        Assert.Equal(HttpStatusCode.OK, baseline.StatusCode);
        var first = await baseline.Content.ReadFromJsonAsync<JsonElement>();
        var id = Assert.Single(first.GetProperty("items").EnumerateArray())
            .GetProperty("id").GetGuid();
        var token = await Csrf(manager);

        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        var session = await LatestSession(db, s.Fixture.ManagerEmail, s.Fixture.A);
        var user = await User(db, s.Fixture.ManagerEmail);

        if (revocation is "logout" or "logout_all")
        {
            var path = revocation == "logout" ? "/api/v1/auth/logout" :
                "/api/v1/auth/logout-all";
            using var logout = await ReferenceDataHttpTests.Send(
                manager, HttpMethod.Post, path);
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }
        else
        {
            var sql = revocation switch
            {
                "session_revoked" => """
                    UPDATE auth_sessions SET revoked_at_utc=clock_timestamp()
                    WHERE id=@session
                    """,
                "idle_expired" => """
                    UPDATE auth_sessions
                    SET idle_expires_at_utc=issued_at_utc+interval '1 microsecond'
                    WHERE id=@session
                    """,
                "absolute_expired" => """
                    UPDATE auth_sessions
                    SET last_seen_at_utc=issued_at_utc,
                        idle_expires_at_utc=issued_at_utc+interval '1 microsecond',
                        absolute_expires_at_utc=issued_at_utc+interval '2 microseconds'
                    WHERE id=@session
                    """,
                "security_version" => """
                    UPDATE users SET security_version=security_version+1 WHERE id=@user
                    """,
                "account_disabled" => """
                    UPDATE users SET status='Disabled',disabled_at_utc=clock_timestamp()
                    WHERE id=@user
                    """,
                "organization_suspended" => """
                    UPDATE organizations SET status='Suspended' WHERE id=@org
                    """,
                "membership_removed" => """
                    UPDATE organization_memberships
                    SET status='Removed',version=version+1,updated_at_utc=clock_timestamp()
                    WHERE user_id=@user AND organization_id=@org
                    """,
                "role_support" => """
                    UPDATE organization_memberships
                    SET role='Support',version=version+1,updated_at_utc=clock_timestamp()
                    WHERE user_id=@user AND organization_id=@org
                    """,
                "organization_changed" => """
                    UPDATE auth_sessions SET selected_organization_id=@other
                    WHERE id=@session
                    """,
                _ => throw new InvalidOperationException("Unknown revocation case")
            };
            await Execute(db, sql, ("session", session), ("user", user),
                ("org", s.Fixture.A), ("other", s.Fixture.B));
        }

        using var listed = await manager.GetAsync(root);
        Assert.Contains(listed.StatusCode, RevokedResponses);
        using var counted = await manager.GetAsync(root + "/unread-count");
        Assert.Contains(counted.StatusCode, RevokedResponses);
        using var detailed = await manager.GetAsync(root + "/" + id);
        Assert.Contains(detailed.StatusCode, RevokedResponses);
        using var mutation = await PreparedAction(manager, root + "/" + id,
            token, Guid.NewGuid());
        Assert.Contains(mutation.StatusCode, RevokedResponses);

        foreach (var response in new[] { listed, counted, detailed, mutation })
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(id.ToString(), body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(shipment.ToString(), body, StringComparison.OrdinalIgnoreCase);
        }
        await AssertUnchangedReceipt(db, id);
    }

    [Fact]
    public async Task PersonalNotificationDetailHidesAnotherRecipientLikeAnUnknownId()
    {
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var originalAdmin = s.Fixture.Admin;
        using var originalManager = s.Fixture.Manager;
        using var originalAnalyst = s.Fixture.Analyst;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        await M15NotificationRuntimeTests.EnsureShipmentDelivered(s, shipment);
        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        await using var other = new NpgsqlCommand("""
            SELECT n.id FROM m15_user_notifications n
            JOIN users u ON u.id=n.recipient_user_id
            WHERE n.organization_id=@org AND n.shipment_id=@shipment
              AND u.normalized_email=@analyst
            """, db);
        other.Parameters.AddWithValue("org", s.Fixture.A);
        other.Parameters.AddWithValue("shipment", shipment);
        other.Parameters.AddWithValue("analyst",
            IdentityStore.NormalizeEmail(s.Fixture.AnalystEmail));
        var analystNotification = Assert.IsType<Guid>(await other.ExecuteScalarAsync());
        var root = $"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var unauthorized = await manager.GetAsync(root + "/" + analystNotification);
        using var unknown = await manager.GetAsync(root + "/" + Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, unauthorized.StatusCode);
        Assert.Equal(unknown.StatusCode, unauthorized.StatusCode);
        Assert.Equal(
            (await unknown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString(),
            (await unauthorized.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        using var otherOrg = await manager.GetAsync(
            $"/api/v1/organizations/{s.Fixture.B}/notifications/{analystNotification}");
        Assert.Equal(HttpStatusCode.NotFound, otherOrg.StatusCode);
    }

    [Fact]
    public async Task InvalidNotificationQueriesAndMutationsNeverChangeReceipt()
    {
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var originalAdmin = s.Fixture.Admin;
        using var originalManager = s.Fixture.Manager;
        using var originalAnalyst = s.Fixture.Analyst;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        await M15NotificationRuntimeTests.EnsureShipmentDelivered(s, shipment);
        var root = $"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var list = await manager.GetAsync(root);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var id = Assert.Single((await list.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid();

        foreach (var invalid in new[]
        {
            "?limit=0", "?limit=51", "?limit=-1", "?limit=invalid",
            "?limit=1&limit=2", "?state=Unknown", "?state=All&state=Unread",
            "?cursor=invalid", "?extra=1"
        })
        {
            using var response = await manager.GetAsync(root + invalid);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using (var invalidCount = await manager.GetAsync(root + "/unread-count?limit=1"))
            Assert.Equal(HttpStatusCode.BadRequest, invalidCount.StatusCode);
        using (var malformed = await manager.GetAsync(root + "/not-a-uuid"))
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        using (var malformedOrg = await manager.GetAsync(
            "/api/v1/organizations/not-a-uuid/notifications"))
            Assert.Equal(HttpStatusCode.BadRequest, malformedOrg.StatusCode);

        using (var noCsrf = await manager.PatchAsJsonAsync(root + "/" + id,
            new { action = "Acknowledge", expectedVersion = 1L }))
            Assert.Equal(HttpStatusCode.Forbidden, noCsrf.StatusCode);
        var token = await Csrf(manager);
        using (var missingKey = new HttpRequestMessage(HttpMethod.Patch, root + "/" + id))
        {
            missingKey.Headers.TryAddWithoutValidation("Origin", "http://localhost");
            missingKey.Headers.TryAddWithoutValidation("X-HarborOps-CSRF", token);
            missingKey.Content = JsonContent.Create(
                new { action = "Acknowledge", expectedVersion = 1L });
            using var missingResponse = await manager.SendAsync(missingKey);
            Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);
        }
        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        await AssertUnchangedReceipt(db, id);
    }

    [Theory]
    [InlineData("session_revoked")]
    [InlineData("security_version")]
    [InlineData("organization_suspended")]
    [InlineData("membership_removed")]
    public async Task ExistingKpiReportCannotBeReadAfterAuthorityRevocation(string change)
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(seedFactory);
        using var seedAdmin = fixture.Admin;
        using var seedManager = fixture.Manager;
        using var seedAnalyst = fixture.Analyst;
        await AssignmentHttpTests.Seed(fixture);
        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var manager = runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(manager, fixture.ManagerEmail);
        await ReferenceDataHttpTests.Select(manager, fixture.A);
        var root = $"/api/v1/organizations/{fixture.A}/analytics";
        using var generated = await manager.GetAsync(root + "/overview");
        Assert.Equal(HttpStatusCode.OK, generated.StatusCode);
        var report = await generated.Content.ReadFromJsonAsync<JsonElement>();
        var id = report.GetProperty("reportId").GetGuid();
        var token = report.GetProperty("reportToken").GetString();
        Assert.False(string.IsNullOrEmpty(token));

        await using var db = new NpgsqlConnection(roles.Admin);
        await db.OpenAsync();
        var user = await User(db, fixture.ManagerEmail);
        var session = await LatestSession(db, fixture.ManagerEmail, fixture.A);
        var sql = change switch
        {
            "session_revoked" => "UPDATE auth_sessions SET revoked_at_utc=clock_timestamp() WHERE id=@session",
            "security_version" => "UPDATE users SET security_version=security_version+1 WHERE id=@user",
            "organization_suspended" => "UPDATE organizations SET status='Suspended' WHERE id=@org",
            "membership_removed" => """
                UPDATE organization_memberships
                SET status='Removed',version=version+1,updated_at_utc=clock_timestamp()
                WHERE user_id=@user AND organization_id=@org
                """,
            _ => throw new InvalidOperationException()
        };
        await Execute(db, sql, ("user", user), ("session", session), ("org", fixture.A));
        using var drill = new HttpRequestMessage(HttpMethod.Get,
            root + "/reports/" + id + "/members?metricCode=K01");
        drill.Headers.TryAddWithoutValidation("X-HarborOps-Report", token);
        using var denied = await manager.SendAsync(drill);
        Assert.Contains(denied.StatusCode, RevokedResponses);
        using var overview = await manager.GetAsync(root + "/overview");
        Assert.Contains(overview.StatusCode, RevokedResponses);
        await using var stored = new NpgsqlCommand(
            "SELECT COUNT(*) FROM operational_kpi_reports WHERE id=@id", db);
        stored.Parameters.AddWithValue("id", id);
        Assert.Equal(1L, Convert.ToInt64(await stored.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task QueuedNotificationAcknowledgmentCannotCommitAfterRevocation()
    {
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var originalAdmin = s.Fixture.Admin;
        using var originalManager = s.Fixture.Manager;
        using var originalAnalyst = s.Fixture.Analyst;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        await M15NotificationRuntimeTests.EnsureShipmentDelivered(s, shipment);
        var root = $"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var listed = await manager.GetAsync(root);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var id = Assert.Single((await listed.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid();
        var token = await Csrf(manager);
        await using var inspect = new NpgsqlConnection(s.Roles.Admin);
        await inspect.OpenAsync();
        var user = await User(inspect, s.Fixture.ManagerEmail);
        await using var lockConnection = new NpgsqlConnection(s.Roles.Admin);
        await lockConnection.OpenAsync();
        await using var tx = await lockConnection.BeginTransactionAsync();
        await using (var hold = new NpgsqlCommand(
            "SELECT id FROM organizations WHERE id=@org FOR UPDATE",
            lockConnection, tx))
        {
            hold.Parameters.AddWithValue("org", s.Fixture.A);
            Assert.Equal(s.Fixture.A, await hold.ExecuteScalarAsync());
        }

        Task<HttpResponseMessage>? pending = null;
        try
        {
            pending = PreparedAction(manager, root + "/" + id, token, Guid.NewGuid());
            var waiting = false;
            for (var attempt = 0; attempt < 200; attempt++)
            {
                await Task.Delay(30);
                await using var probe = new NpgsqlCommand("""
                    SELECT count(*) FROM pg_stat_activity
                    WHERE datname=current_database() AND pid<>pg_backend_pid()
                      AND wait_event_type='Lock'
                      AND query ILIKE '%organizations%'
                    """, inspect);
                if (Convert.ToInt32(await probe.ExecuteScalarAsync()) > 0)
                {
                    waiting = true;
                    break;
                }
            }
            Assert.True(waiting, "Acknowledgement must queue on the organization lock");
            await Execute(inspect,
                "UPDATE users SET security_version=security_version+1 WHERE id=@user",
                ("user", user));
        }
        finally
        {
            await tx.CommitAsync();
        }
        Assert.NotNull(pending);
        using var result = await pending!;
        Assert.Contains(result.StatusCode,
            new[] { HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized });
        await AssertUnchangedReceipt(inspect, id);
        await using var audits = new NpgsqlCommand("""
            SELECT COUNT(*) FROM audit_entries
            WHERE organization_id=@org AND resource_id=@notification
              AND event_type='notification.receipt_acknowledged'
            """, inspect);
        audits.Parameters.AddWithValue("org", s.Fixture.A);
        audits.Parameters.AddWithValue("notification", id);
        Assert.Equal(0L, Convert.ToInt64(await audits.ExecuteScalarAsync()));
    }
}
