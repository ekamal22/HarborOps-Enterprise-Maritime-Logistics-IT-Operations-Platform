using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class ShipmentHttpTests
{
    private static string Root(Guid org) => $"/api/v1/organizations/{org}/shipments";
    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method,
        string url, object body) => ReferenceDataHttpTests.Send(client, method, url, body);
    private static async Task<Guid> Create(HttpClient client, string url, object body)
    {
        using var response = await Send(client, HttpMethod.Post, url, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetGuid();
    }
    private static async Task<(Guid A, Guid B)> Ports(HttpClient client, Guid org)
    {
        var root = $"/api/v1/organizations/{org}/ports";
        var a = await Create(client, root, new
            { referenceCode = "SHP-PORTA", name = "Synthetic Origin", countryCode = "US" });
        var b = await Create(client, root, new
            { referenceCode = "SHP-PORTB", name = "Synthetic Destination", countryCode = "US" });
        return (a, b);
    }

    [Fact]
    public async Task AnalystCreatesTransitionsArchivesRestoresAndReadsImmutableHistory()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var p = await Ports(manager, f.A);
        var route = Root(f.A);
        var id = await Create(analyst, route, new
        {
            referenceCode = "SHP-TEST-01", externalReference = " ext/one ",
            originPortId = p.A, destinationPortId = p.B,
            plannedDepartureAtUtc = "2026-11-01T10:00:00Z",
            plannedArrivalAtUtc = "2026-11-02T10:00:00Z"
        });
        using (var detail = await analyst.GetAsync($"{route}/{id}"))
        {
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            var json = await detail.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("EXT/ONE", json.GetProperty("externalReference").GetString());
            Assert.Equal("Draft", json.GetProperty("workflowStatus").GetString());
            Assert.Equal(1L, json.GetProperty("version").GetInt64());
        }
        using (var forbidden = await admin.GetAsync(route))
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using (var wrong = await analyst.GetAsync(Root(f.B) + "/" + id))
            Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        using (var stale = await Send(analyst, HttpMethod.Patch, $"{route}/{id}",
            new { workflowStatus = "Arrived", expectedVersion = 1 }))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var booked = await Send(analyst, HttpMethod.Patch, $"{route}/{id}",
            new { workflowStatus = "Booked", expectedVersion = 1 }))
            Assert.Equal(HttpStatusCode.OK, booked.StatusCode);
        using (var outdated = await Send(manager, HttpMethod.Patch, $"{route}/{id}",
            new { workflowStatus = "InTransit", expectedVersion = 1 }))
            Assert.Equal(HttpStatusCode.Conflict, outdated.StatusCode);
        using (var transit = await Send(manager, HttpMethod.Patch, $"{route}/{id}",
            new { workflowStatus = "InTransit", expectedVersion = 2 }))
            Assert.Equal(HttpStatusCode.OK, transit.StatusCode);
        using (var archive = await Send(manager, HttpMethod.Delete, $"{route}/{id}",
            new { expectedVersion = 3 }))
            Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        using (var editArchived = await Send(manager, HttpMethod.Patch, $"{route}/{id}",
            new { displayName = "Denied", expectedVersion = 4 }))
            Assert.Equal(HttpStatusCode.Conflict, editArchived.StatusCode);
        using (var restore = await Send(analyst, HttpMethod.Patch, $"{route}/{id}",
            new { recordStatus = "Active", expectedVersion = 4 }))
            Assert.Equal(HttpStatusCode.OK, restore.StatusCode);

        using var historyResponse = await analyst.GetAsync($"{route}/{id}/status-changes");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = (await historyResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("changes");
        Assert.Equal(3, history.GetArrayLength());
        Assert.Null(history[0].GetProperty("fromStatus").GetString());
        Assert.Equal("Draft", history[0].GetProperty("toStatus").GetString());
        Assert.Equal("Booked", history[1].GetProperty("toStatus").GetString());
        Assert.Equal("InTransit", history[2].GetProperty("toStatus").GetString());
        Assert.Equal(3L, history[2].GetProperty("version").GetInt64());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(5L, (await db.Shipments.AsNoTracking().SingleAsync(x => x.Id == id)).Version);
        Assert.Equal(3, await db.ShipmentStatusChanges.CountAsync(x => x.ShipmentId == id));
    }

    [Fact]
    public async Task DuplicateArchivedReferenceAndCrossTenantIdentityStayScoped()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var parents = await Ports(manager, f.A);
        var route = Root(f.A);
        var code = "SHP-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var id = await Create(manager, route, new {
            referenceCode = code, externalReference = "SYN-EXTERNAL-ONE",
            originPortId = parents.A, destinationPortId = parents.B
        });
        using (var dup = await Send(manager, HttpMethod.Post, route,
            new { referenceCode = code, originPortId = parents.A, destinationPortId = parents.B }))
            Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        using (var arch = await Send(manager, HttpMethod.Delete, $"{route}/{id}",
            new { expectedVersion = 1L }))
            Assert.Equal(HttpStatusCode.NoContent, arch.StatusCode);
        using (var retained = await Send(manager, HttpMethod.Post, route,
            new { referenceCode = code, originPortId = parents.A, destinationPortId = parents.B }))
            Assert.Equal(HttpStatusCode.Conflict, retained.StatusCode);
        using (var list = await analyst.GetAsync(route))
        {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var rows = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("shipments");
            Assert.DoesNotContain(rows.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        }
        using (var list = await analyst.GetAsync(route + "?recordStatus=Archived&q=" + code))
        {
            var rows = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("shipments");
            Assert.Single(rows.EnumerateArray());
        }
        using (var selected = await analyst.GetAsync(Root(f.B)))
            Assert.Equal(HttpStatusCode.NotFound, selected.StatusCode);
        using (var invalidForeign = await Send(manager, HttpMethod.Post, route,
            new { referenceCode = "SHP-FOREIGN", originPortId = Guid.NewGuid(),
                destinationPortId = parents.B }))
            Assert.Equal(HttpStatusCode.NotFound, invalidForeign.StatusCode);
    }

    [Fact]
    public async Task CompetingExpectedVersionUpdatesHaveOneWinnerAndOneHistoryRecord()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var parents = await Ports(manager, f.A);
        var route = Root(f.A);
        var id = await Create(manager, route, new { referenceCode = "SHP-CONCURRENT",
            originPortId = parents.A, destinationPortId = parents.B });
        var operations = await Task.WhenAll(
            Send(manager, HttpMethod.Patch, $"{route}/{id}",
                new { workflowStatus = "Booked", expectedVersion = 1 }),
            Send(analyst, HttpMethod.Patch, $"{route}/{id}",
                new { workflowStatus = "Cancelled", expectedVersion = 1 }));
        try
        {
            Assert.Single(operations, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(operations, x => x.StatusCode == HttpStatusCode.Conflict);
        }
        finally
        {
            foreach (var result in operations) result.Dispose();
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(2L, (await db.Shipments.AsNoTracking().SingleAsync(x => x.Id == id)).Version);
        Assert.Equal(2, await db.ShipmentStatusChanges.CountAsync(x => x.ShipmentId == id));
    }

    [Fact]
    public async Task DirectSqlCannotDeleteShipmentOrForgeStatusChange()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var ports = await Ports(manager, f.A);
        var id = await Create(manager, Root(f.A), new { referenceCode = "SHP-SQL-GUARD",
            originPortId = ports.A, destinationPortId = ports.B });
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM shipments WHERE id={id}"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE shipments SET workflow_status='Completed', version=version+1 WHERE id={id}"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM shipment_status_changes WHERE shipment_id={id}"));
        Assert.Equal("Draft", (await db.Shipments.AsNoTracking().SingleAsync(x => x.Id == id)).WorkflowStatus);
        Assert.Equal(1, await db.ShipmentStatusChanges.CountAsync(x => x.ShipmentId == id));
    }
}
