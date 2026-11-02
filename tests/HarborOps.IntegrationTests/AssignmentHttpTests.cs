using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class AssignmentHttpTests
{
    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method,
        string path, object body) => ReferenceDataHttpTests.Send(client, method, path, body);

    private static async Task<Guid> Create(HttpClient client, string path, object body)
    {
        using var result = await Send(client, HttpMethod.Post, path, body);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        return (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    internal static async Task<(Guid Shipment, Guid OtherShipment, Guid Container, Guid Voyage, string Root)>
        Seed(ReferenceDataHttpTests.Fixture f)
    {
        var nonce = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var root = "/api/v1/organizations/" + f.A;
        var a = await Create(f.Manager, root + "/ports",
            new { referenceCode = "G6-PA-" + nonce, name = "G6 Origin", countryCode = "US" });
        var b = await Create(f.Manager, root + "/ports",
            new { referenceCode = "G6-PB-" + nonce, name = "G6 Destination", countryCode = "US" });
        var vessel = await Create(f.Manager, root + "/vessels",
            new { referenceCode = "G6-VES-" + nonce, name = "G6 Vessel" });
        var container = await Create(f.Manager, root + "/containers",
            new { referenceCode = "G6-BOX-" + nonce, identifierKind = "Synthetic",
                containerIdentifier = "SYN-BOX-G6" + nonce, equipmentType = "Dry20" });
        var voyage = await Create(f.Manager, root + "/voyages",
            new { referenceCode = "G6-VOY-" + nonce, vesselId = vessel,
                originPortId = a, destinationPortId = b });
        var shipment = await Create(f.Analyst, root + "/shipments",
            new { referenceCode = "G6-S1-" + nonce, originPortId = a, destinationPortId = b });
        var other = await Create(f.Analyst, root + "/shipments",
            new { referenceCode = "G6-S2-" + nonce, originPortId = a, destinationPortId = b });
        return (shipment, other, container, voyage, root + "/shipments");
    }

    [Fact]
    public async Task ContainerAndVoyageEndToEndEnforceOverlapClosureAndParentGuards()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var analyst = f.Analyst; using var manager = f.Manager; using var admin = f.Admin;
        var seed = await Seed(f);
        var root = seed.Root + "/" + seed.Shipment;
        var start = "2026-11-10T10:00:00Z";
        var end = "2026-11-10T11:00:00Z";
        var containerRoute = root + "/container-assignments";
        var voyageRoute = root + "/voyage-assignments";

        var container = await Create(analyst, containerRoute,
            new { containerId = seed.Container, startsAtUtc = start });
        var voyage = await Create(manager, voyageRoute,
            new { voyageId = seed.Voyage, startsAtUtc = start });

        using (var list = await analyst.GetAsync(containerRoute))
        {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var data = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("assignments");
            Assert.Contains(data.EnumerateArray(), x => x.GetProperty("id").GetGuid() == container);
        }
        using (var list = await analyst.GetAsync(voyageRoute))
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using (var denied = await admin.GetAsync(containerRoute))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var wrongOrg = await manager.GetAsync(
            "/api/v1/organizations/" + f.B + "/shipments/" + seed.Shipment + "/container-assignments"))
            Assert.Equal(HttpStatusCode.NotFound, wrongOrg.StatusCode);

        var competing = seed.Root + "/" + seed.OtherShipment + "/container-assignments";
        using (var overlap = await Send(manager, HttpMethod.Post, competing,
            new { containerId = seed.Container, startsAtUtc = "2026-11-10T10:30:00Z" }))
            Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
        using (var duplicateVoyage = await Send(manager, HttpMethod.Post, voyageRoute,
            new { voyageId = seed.Voyage, startsAtUtc = start }))
            Assert.Equal(HttpStatusCode.Conflict, duplicateVoyage.StatusCode);
        using (var guarded = await Send(manager, HttpMethod.Delete, seed.Root + "/" + seed.Shipment,
            new { expectedVersion = 1L }))
            Assert.Equal(HttpStatusCode.Conflict, guarded.StatusCode);

        using (var closed = await Send(analyst, HttpMethod.Patch, containerRoute + "/" + container,
            new { expectedVersion = 1L, endsAtUtc = end }))
            Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        using (var stale = await Send(analyst, HttpMethod.Patch, containerRoute + "/" + container,
            new { expectedVersion = 1L, endsAtUtc = "2026-11-10T12:00:00Z" }))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var adjacent = await Send(manager, HttpMethod.Post, competing,
            new { containerId = seed.Container, startsAtUtc = end, endsAtUtc = "2026-11-10T12:00:00Z" }))
            Assert.Equal(HttpStatusCode.Created, adjacent.StatusCode);
        using (var closed = await Send(manager, HttpMethod.Patch, voyageRoute + "/" + voyage,
            new { expectedVersion = 1L, endsAtUtc = end }))
            Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        using (var archived = await Send(manager, HttpMethod.Delete, seed.Root + "/" + seed.Shipment,
            new { expectedVersion = 1L }))
            Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        using (var history = await analyst.GetAsync(containerRoute + "/" + container))
        {
            Assert.Equal(HttpStatusCode.OK, history.StatusCode);
            Assert.Equal(2L, (await history.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("version").GetInt64());
        }
    }

    [Fact]
    public async Task ForeignAssignmentIdsReturnNotFoundInsideSelectedTenant()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager; using var analyst = f.Analyst; using var admin = f.Admin;
        var seed = await Seed(f);
        var origin = seed.Root + "/" + seed.Shipment + "/container-assignments";
        var assignment = await Create(analyst, origin,
            new { containerId = seed.Container, startsAtUtc = "2035-02-01T10:00:00Z" });
        var otherShipment = seed.Root + "/" + seed.OtherShipment + "/container-assignments/" + assignment;
        using (var hidden = await analyst.GetAsync(otherShipment))
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using (var own = await analyst.GetAsync(origin + "/" + assignment))
            Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    }

    [Fact]
    public async Task MalformedAndCrossOriginWritesCannotChangeAssignments()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager; using var analyst = f.Analyst; using var admin = f.Admin;
        var seed = await Seed(f);
        var route = seed.Root + "/" + seed.Shipment + "/container-assignments";

        using (var bad = await Send(manager, HttpMethod.Post, route,
            new { containerId = seed.Container, startsAtUtc = "2026-11-10T10:00:00+03:00" }))
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        using (var bad = await Send(manager, HttpMethod.Post, route,
            new { containerId = seed.Container, startsAtUtc = "2026-11-10T10:00:00Z",
                organizationId = f.B }))
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        using (var noCsrf = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post,
            route, new { containerId = seed.Container, startsAtUtc = "2026-11-10T10:00:00Z" },
            csrf: false))
            Assert.Equal(HttpStatusCode.Forbidden, noCsrf.StatusCode);
        using (var denied = await Send(admin, HttpMethod.Post, route,
            new { containerId = seed.Container, startsAtUtc = "2026-11-10T10:00:00Z" }))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var foreign = await Send(analyst, HttpMethod.Post,
            seed.Root + "/" + seed.OtherShipment + "/container-assignments",
            new { containerId = Guid.NewGuid(), startsAtUtc = "2026-11-10T10:00:00Z" }))
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(0, await db.ShipmentContainerAssignments
            .CountAsync(x => x.OrganizationId == f.A && x.ShipmentId == seed.Shipment));
    }
}
