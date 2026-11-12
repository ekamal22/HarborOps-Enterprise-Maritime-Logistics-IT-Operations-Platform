using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class G15HistoricalTargetAcceptanceTests
{
    [Fact]
    public async Task ArchivedShipmentKeepsHistoricalPersonalEventWithoutNewBusinessAuthority()
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
        using var before = await manager.GetAsync(root);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var first = Assert.Single((await before.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray());
        var id = first.GetProperty("id").GetGuid();
        var originalEvent = first.GetProperty("eventType").GetString();
        Assert.Equal("ExceptionOpened", originalEvent);

        await using var adminDb = new NpgsqlConnection(s.Roles.Admin);
        await adminDb.OpenAsync();
        await using (var archive = new NpgsqlCommand("""
            UPDATE shipments SET record_status='Archived',
                archived_at_utc=clock_timestamp(),
                updated_at_utc=clock_timestamp(),version=version+1
            WHERE organization_id=@org AND id=@shipment
            """, adminDb))
        {
            archive.Parameters.AddWithValue("org", s.Fixture.A);
            archive.Parameters.AddWithValue("shipment", shipment);
            Assert.Equal(1, await archive.ExecuteNonQueryAsync());
        }

        using var after = await manager.GetAsync(root);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        var historical = Assert.Single(
            (await after.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("items").EnumerateArray());
        Assert.Equal(id, historical.GetProperty("id").GetGuid());
        Assert.Equal(originalEvent, historical.GetProperty("eventType").GetString());
        Assert.Equal(shipment, historical.GetProperty("shipmentId").GetGuid());

        var targetPath =
            $"/api/v1/organizations/{s.Fixture.A}/shipments/{shipment}";
        using var detail = await manager.GetAsync(targetPath);
        Assert.Contains(detail.StatusCode,
            new[] { HttpStatusCode.OK, HttpStatusCode.NotFound });
        if (detail.StatusCode == HttpStatusCode.OK)
        {
            var body = await detail.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(shipment, body.GetProperty("id").GetGuid());
            Assert.Equal("Archived", body.GetProperty("recordStatus").GetString());
        }

        using var denied = await manager.GetAsync(
            $"/api/v1/organizations/{s.Fixture.B}/shipments/{shipment}");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var foreignNotice = await manager.GetAsync(
            $"/api/v1/organizations/{s.Fixture.B}/notifications/{id}");
        Assert.Equal(HttpStatusCode.NotFound, foreignNotice.StatusCode);
    }
}
