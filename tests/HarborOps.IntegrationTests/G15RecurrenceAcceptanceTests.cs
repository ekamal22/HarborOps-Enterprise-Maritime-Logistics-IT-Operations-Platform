using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class G15RecurrenceAcceptanceTests
{
    [Fact]
    public async Task AutoResolvedOldOccurrenceAndNewOpeningKeepDistinctHistoryAndIntents()
    {
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var originalAdmin = s.Fixture.Admin;
        using var originalManager = s.Fixture.Manager;
        using var originalAnalyst = s.Fixture.Analyst;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        var original = await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        var path = $"/api/v1/organizations/{s.Fixture.A}/shipments/{shipment}";
        var originalException = M11ExceptionRuntimeTests.Collection(s.Fixture.A)
            + "/" + original;

        using (var acknowledged = await M11ExceptionRuntimeTests.SendAction(
            manager, originalException,
            new { action = "Acknowledge", expectedVersion = 1L,
                reason = "Reviewing recurring overdue schedule evidence" },
            Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode);

        using (var updated = await ReferenceDataHttpTests.Send(
            s.Fixture.Manager, HttpMethod.Patch, path,
            new { expectedVersion = 3L,
                plannedArrivalAtUtc = M11ExceptionRuntimeTests.Utc(
                    DateTimeOffset.UtcNow.AddHours(4)) }))
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using (var refreshed = await ReferenceDataHttpTests.Send(
            manager, HttpMethod.Post, path + "/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);

        using (var reverted = await ReferenceDataHttpTests.Send(
            s.Fixture.Manager, HttpMethod.Patch, path,
            new { expectedVersion = 4L,
                plannedArrivalAtUtc = M11ExceptionRuntimeTests.Utc(
                    DateTimeOffset.UtcNow.AddHours(-2)) }))
            Assert.Equal(HttpStatusCode.OK, reverted.StatusCode);
        for (var repeat = 0; repeat < 2; repeat++)
        {
            using var evaluated = await ReferenceDataHttpTests.Send(
                manager, HttpMethod.Post, path + "/exceptions/evaluate");
            Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode);
        }

        using var list = await manager.GetAsync(path + "/exceptions");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var occurrences = (await list.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, occurrences.Length);
        var first = occurrences.Single(x => x.GetProperty("id").GetGuid() == original);
        Assert.Equal(1, first.GetProperty("occurrence").GetInt32());
        Assert.Equal("Resolved", first.GetProperty("state").GetString());
        var reopened = Assert.Single(occurrences,
            x => x.GetProperty("id").GetGuid() != original);
        Assert.Equal(2, reopened.GetProperty("occurrence").GetInt32());
        Assert.Equal("Open", reopened.GetProperty("state").GetString());
        var newId = reopened.GetProperty("id").GetGuid();

        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        await using var sql = new NpgsqlCommand("""
            SELECT o.event_type,o.exception_id,o.source_history_id,
                   h.action,h.exception_id
            FROM m15_notification_outbox o
            JOIN shipment_exception_history h ON h.id=o.source_history_id
            WHERE o.organization_id=@org AND o.shipment_id=@shipment
            ORDER BY h.occurred_at_utc,h.id
            """, db);
        sql.Parameters.AddWithValue("org", s.Fixture.A);
        sql.Parameters.AddWithValue("shipment", shipment);
        var entries = new List<(string Event, Guid Exception, Guid History)>();
        await using (var rows = await sql.ExecuteReaderAsync())
            while (await rows.ReadAsync())
            {
                var ev = rows.GetString(0);
                var exception = rows.GetGuid(1);
                var history = rows.GetGuid(2);
                var action = rows.GetString(3);
                Assert.Equal(exception, rows.GetGuid(4));
                Assert.Equal(action switch
                {
                    "Opened" => "ExceptionOpened",
                    "AutoResolved" => "ExceptionAutoResolved",
                    "Resolved" => "ExceptionResolved",
                    _ => throw new InvalidOperationException(
                        "Non-catalog event generated an outbox intent")
                }, ev);
                entries.Add((ev, exception, history));
            }
        Assert.Equal(3, entries.Count);
        Assert.Equal(3, entries.Select(x => x.History).Distinct().Count());
        Assert.Equal(2, entries.Count(x => x.Event == "ExceptionOpened"));
        Assert.Single(entries,
            x => x.Event == "ExceptionOpened" && x.Exception == original);
        Assert.Single(entries,
            x => x.Event == "ExceptionAutoResolved" && x.Exception == original);
        Assert.Single(entries,
            x => x.Event == "ExceptionOpened" && x.Exception == newId);
        Assert.Equal(0, entries.Count(x => x.Event == "ExceptionResolved"));
    }
}
