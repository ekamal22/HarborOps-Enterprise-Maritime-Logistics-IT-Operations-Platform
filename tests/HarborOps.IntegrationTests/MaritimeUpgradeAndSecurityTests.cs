using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class MaritimeUpgradeAndSecurityTests
{
    private static string Root(Guid org) => $"/api/v1/organizations/{org}";
    private static async Task<Guid> Create(HttpClient client, string url, object data)
    {
        using var response = await ReferenceDataHttpTests.Send(client, HttpMethod.Post, url, data);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task FreshIsolatedSchemaRunsEveryMigrationWithoutModelDrift()
    {
        var source = TestDatabase.ConnectionString();
        var schema = "g4fresh_" + Guid.NewGuid().ToString("N");
        var cs = new NpgsqlConnectionStringBuilder(source) { SearchPath = schema };
        await using var admin = new NpgsqlConnection(source);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(cs.ConnectionString).Options;
            await using var db = new HarborOpsDbContext(options);
            Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
            await db.Database.MigrateAsync();
            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            Assert.Equal(5, applied.Length);
            Assert.Equal("20261101010000_VoyageContainerReferences", applied[^1]);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            await using var conn = new NpgsqlConnection(cs.ConnectionString);
            await conn.OpenAsync();
            foreach (var table in new[] { "voyages", "containers" })
            {
                await using var cmd = new NpgsqlCommand("SELECT to_regclass(@name)::text", conn);
                cmd.Parameters.AddWithValue("name", table);
                Assert.Equal(table, await cmd.ExecuteScalarAsync());
            }
            await using var fn = new NpgsqlCommand(
                "SELECT harborops_iso6346_valid('CSQU3054383'), harborops_iso6346_valid('CSQU3054382')", conn);
            await using var read = await fn.ExecuteReaderAsync();
            Assert.True(await read.ReadAsync());
            Assert.True(read.GetBoolean(0));
            Assert.False(read.GetBoolean(1));
            await read.CloseAsync();
            await db.Database.MigrateAsync();
            Assert.Equal(5, (await db.Database.GetAppliedMigrationsAsync()).Count());
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task SelectedOrganizationChangeAndSuspensionFailClosedForBothResources()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager;
        using var admin = f.Admin;
        using var analyst = f.Analyst;
        var createdId = await Create(manager, Root(f.A) + "/containers", new {
            referenceCode = "SECURE-BOX", identifierKind = "Synthetic",
            containerIdentifier = "SYN-BOX-SECURE01", equipmentType = "Dry20"
        });
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var id = await db.AuthUsers.AsNoTracking()
            .Where(u => u.NormalizedEmail == IdentityStore.NormalizeEmail(f.ManagerEmail))
            .Select(u => u.Id).SingleAsync();
        await db.AuthSessions.Where(s => s.UserId == id).ExecuteUpdateAsync(upd =>
            upd.SetProperty(s => s.SelectedOrganizationId, f.B));
        foreach (var kind in new[] { "voyages", "containers" })
        {
            using var list = await manager.GetAsync(Root(f.A) + "/" + kind);
            Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
            using var post = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post,
                Root(f.A) + "/" + kind, kind == "voyages"
                    ? (object)new { referenceCode = "REJECTED", vesselId = Guid.NewGuid(),
                        originPortId = Guid.NewGuid(), destinationPortId = Guid.NewGuid() }
                    : new { referenceCode = "REJECTED", identifierKind = "Synthetic",
                        containerIdentifier = "SYN-BOX-REJECTED", equipmentType = "Dry20" });
            Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        }
        await db.AuthSessions.Where(s => s.UserId == id).ExecuteUpdateAsync(upd =>
            upd.SetProperty(s => s.SelectedOrganizationId, f.A));
        await db.Organizations.Where(o => o.Id == f.A)
            .ExecuteUpdateAsync(upd => upd.SetProperty(o => o.Status, "Suspended"));
        foreach (var kind in new[] { "voyages", "containers" })
        {
            using var list = await manager.GetAsync(Root(f.A) + "/" + kind);
            Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
            var listError = await list.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("organization_access_revoked", listError.GetProperty("code").GetString());
            Assert.False(string.IsNullOrWhiteSpace(listError.GetProperty("correlationId").GetString()));
            using var mutation = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post,
                Root(f.A) + "/" + kind, kind == "voyages"
                    ? (object)new { referenceCode = "SUSPENDED", vesselId = Guid.NewGuid(),
                        originPortId = Guid.NewGuid(), destinationPortId = Guid.NewGuid() }
                    : new { referenceCode = "SUSPENDED", identifierKind = "Synthetic",
                        containerIdentifier = "SYN-BOX-SUSPENDED", equipmentType = "Dry20" });
            Assert.Equal(HttpStatusCode.Forbidden, mutation.StatusCode);
            var mutationError = await mutation.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("organization_access_revoked", mutationError.GetProperty("code").GetString());
            Assert.False(string.IsNullOrWhiteSpace(mutationError.GetProperty("correlationId").GetString()));
        }
        Assert.Equal(1, await db.Containers.CountAsync(x => x.OrganizationId == f.A));
        Assert.Equal(0, await db.Voyages.CountAsync(x => x.OrganizationId == f.A));
        await db.Organizations.Where(o => o.Id == f.A)
            .ExecuteUpdateAsync(upd => upd.SetProperty(o => o.Status, "Active"));
        using var recovered = await manager.GetAsync(Root(f.A) + "/containers/" + createdId);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }

    [Fact]
    public async Task VoyageFilteringSchedulesArchiveAndImmutableRoutesAreScoped()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager;
        using var admin = f.Admin;
        using var analyst = f.Analyst;
        var root = Root(f.A);
        var portA = await Create(manager, root + "/ports",
            new { referenceCode = "G4-PORT-A", name = "First Port", countryCode = "US" });
        var portB = await Create(manager, root + "/ports",
            new { referenceCode = "G4-PORT-B", name = "Second Port", countryCode = "US" });
        var vessel = await Create(manager, root + "/vessels",
            new { referenceCode = "G4-VESSEL", name = "Test Vessel" });
        var voyage = await Create(manager, root + "/voyages", new {
            referenceCode = "G4-ROUTE-1", vesselId = vessel,
            originPortId = portA, destinationPortId = portB,
            plannedDepartureAtUtc = "2026-11-01T21:00:00Z",
            plannedArrivalAtUtc = "2026-11-02T05:00:00Z"
        });
        var url = root + "/voyages";
        var filtered = await manager.GetFromJsonAsync<JsonElement>(
            url + $"?recordStatus=Active&vesselId={vessel}&originPortId={portA}&destinationPortId={portB}&voyageStatus=Planned&q=ROUTE");
        Assert.Single(filtered.GetProperty("voyages").EnumerateArray());
        foreach (var mismatch in new[] {
            "vesselId=" + Guid.NewGuid(), "originPortId=" + portB,
            "destinationPortId=" + portA, "voyageStatus=Cancelled"
        })
        {
            var empty = await manager.GetFromJsonAsync<JsonElement>(url + "?" + mismatch);
            Assert.Empty(empty.GetProperty("voyages").EnumerateArray());
        }
        using var cannotChangeRoute = await ReferenceDataHttpTests.Send(
            manager, HttpMethod.Patch, url + "/" + voyage,
            new { vesselId = Guid.NewGuid(), expectedVersion = 1L });
        Assert.Equal(HttpStatusCode.BadRequest, cannotChangeRoute.StatusCode);
        using var cannotPrematurelyArrive = await ReferenceDataHttpTests.Send(
            manager, HttpMethod.Patch, url + "/" + voyage,
            new { voyageStatus = "Arrived", actualArrivalAtUtc = "2026-11-02T05:00:00Z",
                expectedVersion = 1L });
        Assert.Equal(HttpStatusCode.Conflict, cannotPrematurelyArrive.StatusCode);
        using var underway = await ReferenceDataHttpTests.Send(
            manager, HttpMethod.Patch, url + "/" + voyage,
            new { voyageStatus = "Underway", actualDepartureAtUtc = "2026-11-01T22:00:00Z",
                expectedVersion = 1L });
        Assert.Equal(HttpStatusCode.OK, underway.StatusCode);
        var underwayList = await manager.GetFromJsonAsync<JsonElement>(url + "?voyageStatus=Underway");
        Assert.Single(underwayList.GetProperty("voyages").EnumerateArray());
        using var freezeSchedule = await ReferenceDataHttpTests.Send(
            manager, HttpMethod.Patch, url + "/" + voyage,
            new { plannedArrivalAtUtc = "2026-11-02T07:00:00Z", expectedVersion = 2L });
        Assert.Equal(HttpStatusCode.Conflict, freezeSchedule.StatusCode);
        using var archived = await ReferenceDataHttpTests.Send(
            manager, HttpMethod.Delete, url + "/" + voyage, new { expectedVersion = 2L });
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        var active = await manager.GetFromJsonAsync<JsonElement>(url);
        Assert.Empty(active.GetProperty("voyages").EnumerateArray());
        var archive = await manager.GetFromJsonAsync<JsonElement>(url + "?recordStatus=Archived");
        Assert.Single(archive.GetProperty("voyages").EnumerateArray());
        using var restore = await ReferenceDataHttpTests.Send(
            manager, HttpMethod.Patch, url + "/" + voyage,
            new { recordStatus = "Active", expectedVersion = 3L });
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        var result = await manager.GetFromJsonAsync<JsonElement>(url + "/" + voyage);
        Assert.Equal(4L, result.GetProperty("version").GetInt64());
        Assert.Equal("Underway", result.GetProperty("voyageStatus").GetString());
    }
}
