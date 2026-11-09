using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class G15AuditAcceptanceTests
{
    [Fact]
    public async Task MarkReadIsLowNoiseWhileAcknowledgmentAuditIsAtomicAndReplayStable()
    {
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var seedAdmin = s.Fixture.Admin;
        using var seedManager = s.Fixture.Manager;
        using var seedAnalyst = s.Fixture.Analyst;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        await M15NotificationRuntimeTests.EnsureShipmentDelivered(s, shipment);
        var root = $"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var list = await manager.GetAsync(root);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var id = Assert.Single((await list.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid();
        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();

        async Task<long> ReceiptAuditCount()
        {
            await using var count = new NpgsqlCommand("""
                SELECT COUNT(*) FROM audit_entries
                WHERE organization_id=@org AND resource_id=@id
                  AND event_type LIKE 'notification.%'
                """, db);
            count.Parameters.AddWithValue("org", s.Fixture.A);
            count.Parameters.AddWithValue("id", id);
            return Convert.ToInt64(await count.ExecuteScalarAsync());
        }

        Assert.Equal(0L, await ReceiptAuditCount());
        var readKey = Guid.NewGuid();
        using (var read = await M11ExceptionRuntimeTests.SendAction(
            manager, root + "/" + id,
            new { action = "MarkRead", expectedVersion = 1L }, readKey))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.Equal(2L, (await read.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("version").GetInt64());
        }
        Assert.Equal(0L, await ReceiptAuditCount());

        var acknowledgeKey = Guid.NewGuid();
        using (var ack = await M11ExceptionRuntimeTests.SendAction(
            manager, root + "/" + id,
            new { action = "Acknowledge", expectedVersion = 2L }, acknowledgeKey))
        {
            Assert.Equal(HttpStatusCode.OK, ack.StatusCode);
            Assert.Equal(3L, (await ack.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("version").GetInt64());
        }
        Assert.Equal(1L, await ReceiptAuditCount());
        using (var retry = await M11ExceptionRuntimeTests.SendAction(
            manager, root + "/" + id,
            new { action = "Acknowledge", expectedVersion = 2L }, acknowledgeKey))
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(1L, await ReceiptAuditCount());
        await using var receipt = new NpgsqlCommand("""
            SELECT version,read_at_utc IS NOT NULL,acknowledged_at_utc IS NOT NULL
            FROM m15_user_notifications WHERE id=@id AND organization_id=@org
            """, db);
        receipt.Parameters.AddWithValue("id", id);
        receipt.Parameters.AddWithValue("org", s.Fixture.A);
        await using var row = await receipt.ExecuteReaderAsync();
        Assert.True(await row.ReadAsync());
        Assert.Equal(3L, row.GetInt64(0));
        Assert.True(row.GetBoolean(1));
        Assert.True(row.GetBoolean(2));
    }
}
