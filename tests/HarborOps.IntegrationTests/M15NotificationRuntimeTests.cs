using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// Future G15 execution requires a disposable PostgreSQL server and final source lock.
// Tests are committed as regression source, not claimed to have been executed.
public sealed class M15NotificationRuntimeTests
{
    [Fact]
    public async Task M11HistoryProducesScopedDeduplicatedPersonalReceipts()
    {
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var adminFactory = s.Factory;
        using var apiFactory = s.Runtime;
        using var originalAdmin = s.Fixture.Admin;
        using var originalManager = s.Fixture.Manager;
        using var originalAnalyst = s.Fixture.Analyst;
        using var manager = s.Manager;
        using var analyst = s.Analyst;
        using var administrator = s.Administrator;

        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        var exceptionId = await M11ExceptionRuntimeTests.EvaluateAndFind(s,shipment);

        await using (var scope = apiFactory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<NotificationStore>();
            Assert.True(await store.DeliverPendingAsync(CancellationToken.None) >= 1);
            // Other isolated test fixtures can leave separate pending intents.
            // This event's unique recipient row, not a global empty queue,
            // is the source-specific effect-once invariant.
        }

        var url = $"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var managerResult = await manager.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK,managerResult.StatusCode);
        var managerBody = await managerResult.Content.ReadFromJsonAsync<JsonElement>();
        var managerItem = Assert.Single(managerBody.GetProperty("items").EnumerateArray());
        Assert.Equal(shipment,managerItem.GetProperty("shipmentId").GetGuid());
        Assert.Equal(exceptionId,managerItem.GetProperty("exceptionId").GetGuid());
        Assert.Equal("ExceptionOpened",managerItem.GetProperty("eventType").GetString());
        Assert.Contains("unconfirmed",managerItem.GetProperty("title").GetString());

        using var analystResult = await analyst.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK,analystResult.StatusCode);
        Assert.Single((await analystResult.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray());

        using var adminDenied = await administrator.GetAsync(url);
        Assert.Equal(HttpStatusCode.Forbidden,adminDenied.StatusCode);
        using var foreignDenied = await manager.GetAsync(
            $"/api/v1/organizations/{s.Fixture.B}/notifications");
        Assert.Equal(HttpStatusCode.NotFound,foreignDenied.StatusCode);

        var notificationId = managerItem.GetProperty("id").GetGuid();

        // A database-only caller can know the real recipient/session IDs,
        // but without the API's HMAC proof it still cannot read personal rows.
        await using (var administratorSql = new NpgsqlConnection(s.Roles.Admin))
        await using (var apiSql = new NpgsqlConnection(s.Roles.Api))
        {
            await administratorSql.OpenAsync();
            await apiSql.OpenAsync();
            Guid recipientId,sessionId;
            await using (var lookup = new NpgsqlCommand("""
                SELECT n.recipient_user_id,a.id
                FROM m15_user_notifications n
                JOIN auth_sessions a ON a.user_id=n.recipient_user_id
                    AND a.selected_organization_id=n.organization_id
                    AND a.revoked_at_utc IS NULL
                WHERE n.id=@notification
                LIMIT 1
                """,administratorSql))
            {
                lookup.Parameters.AddWithValue("notification",notificationId);
                await using var rows = await lookup.ExecuteReaderAsync();
                Assert.True(await rows.ReadAsync());
                recipientId = rows.GetGuid(0);
                sessionId = rows.GetGuid(1);
            }
            var expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 25;
            var forged = $"{s.Fixture.A:D}|{recipientId:D}|{sessionId:D}|{expires}|"+
                new string('0',64);
            await using (var config = new NpgsqlCommand(
                "SELECT set_config('harborops.m11_read_scope',@scope,false)",apiSql))
            {
                config.Parameters.AddWithValue("scope",forged);
                await config.ExecuteScalarAsync();
            }
            await using (var count = new NpgsqlCommand(
                "SELECT COUNT(*) FROM m15_user_notifications",apiSql))
                Assert.Equal(0L,Convert.ToInt64(await count.ExecuteScalarAsync()));
        }

        using var countBefore = await manager.GetAsync(url+"/unread-count");
        var beforeBody = await countBefore.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1,beforeBody.GetProperty("unreadCount").GetInt64());

        var itemUrl = url+"/"+notificationId;
        var replay = Guid.NewGuid();
        using var first = await M11ExceptionRuntimeTests.SendAction(manager,
            itemUrl,new { action="MarkRead",expectedVersion=1L },replay);
        Assert.Equal(HttpStatusCode.OK,first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2,firstBody.GetProperty("version").GetInt64());

        using var repeated = await M11ExceptionRuntimeTests.SendAction(manager,
            itemUrl,new { action="MarkRead",expectedVersion=1L },replay);
        Assert.Equal(HttpStatusCode.OK,repeated.StatusCode);
        Assert.Equal(2,(await repeated.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("version").GetInt64());

        using var changed = await M11ExceptionRuntimeTests.SendAction(manager,
            itemUrl,new { action="Acknowledge",expectedVersion=2L },replay);
        Assert.Equal(HttpStatusCode.Conflict,changed.StatusCode);

        using var acknowledgment = await M11ExceptionRuntimeTests.SendAction(manager,
            itemUrl,new { action="Acknowledge",expectedVersion=2L },Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK,acknowledgment.StatusCode);
        Assert.Equal(3,(await acknowledgment.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("version").GetInt64());

        // A retry of the first committed action returns its original receipt
        // snapshot even after a separate request has acknowledged the notice.
        using var lateReplay = await M11ExceptionRuntimeTests.SendAction(manager,
            itemUrl,new { action="MarkRead",expectedVersion=1L },replay);
        Assert.Equal(HttpStatusCode.OK,lateReplay.StatusCode);
        var lateBody = await lateReplay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2,lateBody.GetProperty("version").GetInt64());
        Assert.Equal(JsonValueKind.Null,lateBody.GetProperty("acknowledgedAtUtc").ValueKind);
        using var latest = await manager.GetAsync(itemUrl);
        Assert.Equal(3,(await latest.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("version").GetInt64());

        using var countAfter = await manager.GetAsync(url+"/unread-count");
        Assert.Equal(0,(await countAfter.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("unreadCount").GetInt64());

        await using var sql = new NpgsqlConnection(s.Roles.Admin);
        await sql.OpenAsync();
        await using var audit = new NpgsqlCommand("""
            SELECT COUNT(*) FROM audit_entries
            WHERE event_type='notification.receipt_acknowledged'
                AND resource_id=@id AND organization_id=@org
            """,sql);
        audit.Parameters.AddWithValue("id",notificationId);
        audit.Parameters.AddWithValue("org",s.Fixture.A);
        Assert.Equal(1L,Convert.ToInt64(await audit.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task RestrictedSqlLoginCannotEnumerateOutboxOrWriteAnotherRecipient()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        await using var connection = new NpgsqlConnection(roles.Api);
        await connection.OpenAsync();

        await using (var forbidden = new NpgsqlCommand(
            "SELECT COUNT(*) FROM m15_notification_outbox",connection))
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(
                () => forbidden.ExecuteScalarAsync());
            Assert.Equal("42501",ex.SqlState);
        }

        await using (var forbidden = new NpgsqlCommand(
            "UPDATE m15_user_notifications SET read_at_utc=clock_timestamp()",connection))
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(
                () => forbidden.ExecuteNonQueryAsync());
            Assert.Equal("42501",ex.SqlState);
        }

        await using (var forbidden = new NpgsqlCommand(
            "SELECT m15_deliver_pending(1)",connection))
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(
                () => forbidden.ExecuteScalarAsync());
            Assert.Equal("42501",ex.SqlState);
        }

        await using (var forbidden = new NpgsqlCommand(
            "SELECT pending FROM m15_delivery_status()",connection))
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(
                () => forbidden.ExecuteScalarAsync());
            Assert.Equal("42501",ex.SqlState);
        }

        await using (var unscoped = new NpgsqlCommand(
            "SELECT COUNT(*) FROM m15_user_notifications",connection))
        {
            Assert.Equal(0L,Convert.ToInt64(await unscoped.ExecuteScalarAsync()));
        }

        await using var worker = new NpgsqlConnection(roles.Worker);
        await worker.OpenAsync();
        foreach (var sql in new[] {
            "SELECT COUNT(*) FROM m15_notification_outbox",
            "SELECT COUNT(*) FROM m15_user_notifications",
            "SELECT pending FROM m15_delivery_status()",
            "SELECT m15_deliver_pending(1)"
        })
        {
            await using var command = new NpgsqlCommand(sql,worker);
            var denied = await Assert.ThrowsAsync<PostgresException>(
                () => command.ExecuteScalarAsync());
            Assert.Equal("42501",denied.SqlState);
        }
    }
}
