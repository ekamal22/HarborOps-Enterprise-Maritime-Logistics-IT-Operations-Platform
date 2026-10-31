using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Maritime;
using HarborOps.Infrastructure.References;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class MaritimeHttpTests
{
    private static string Root(Guid org) => "/api/v1/organizations/" + org;
    private static async Task<Guid> Create(HttpClient client, string route, object body)
    {
        using var result = await ReferenceDataHttpTests.Send(client, HttpMethod.Post, route, body);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        var row = await result.Content.ReadFromJsonAsync<JsonElement>();
        return row.GetProperty("id").GetGuid();
    }
    private static async Task<(Guid PortA, Guid PortB, Guid Vessel)> Parents(
        HttpClient client, Guid org)
    {
        var prefix = Root(org);
        var a = await Create(client, prefix + "/ports",
            new { referenceCode = "M4-NORTH", name = "Synthetic Northern Port", countryCode = "US" });
        var b = await Create(client, prefix + "/ports",
            new { referenceCode = "M4-SOUTH", name = "Synthetic Southern Port", countryCode = "US" });
        var v = await Create(client, prefix + "/vessels",
            new { referenceCode = "M4-VESSEL", name = "Synthetic Vessel" });
        return (a, b, v);
    }

    [Fact]
    public async Task ManagerCreatesBothKindsAndAnalystReadsWithoutWritePermission()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        using var admin = f.Admin;
        var parents = await Parents(manager, f.A);
        var root = Root(f.A);
        var voyage = await Create(manager, root + "/voyages", new {
            referenceCode = "VOYAGE-001", vesselId = parents.Vessel,
            originPortId = parents.PortA, destinationPortId = parents.PortB,
            plannedDepartureAtUtc = "2026-11-01T22:00:00Z",
            plannedArrivalAtUtc = "2026-11-02T06:00:00Z"
        });
        var container = await Create(manager, root + "/containers", new {
            referenceCode = "BOX-001", identifierKind = "Synthetic",
            containerIdentifier = "SYN-BOX-ALPHA", equipmentType = "Dry20"
        });
        var iso = await Create(manager, root + "/containers", new {
            referenceCode = "BOX-002", identifierKind = "ISO6346",
            containerIdentifier = "CSQU3054383", equipmentType = "HighCube40"
        });
        using var v = await analyst.GetAsync(root + "/voyages/" + voyage);
        using var c = await analyst.GetAsync(root + "/containers/" + container);
        using var i = await analyst.GetAsync(root + "/containers/" + iso);
        Assert.Equal(HttpStatusCode.OK, v.StatusCode);
        Assert.Equal(HttpStatusCode.OK, c.StatusCode);
        Assert.Equal(HttpStatusCode.OK, i.StatusCode);
        using var denied = await ReferenceDataHttpTests.Send(analyst, HttpMethod.Patch,
            root + "/containers/" + container,
            new { displayName = "Denied", expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var forbidden = await admin.GetAsync(root + "/voyages");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var row = await c.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Synthetic", row.GetProperty("identifierKind").GetString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(1L, (await db.Containers.AsNoTracking().SingleAsync(x => x.Id == container)).Version);
    }

    [Fact]
    public async Task VoyageProgressAndArchiveRestoreRequireExpectedVersion()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager;
        var parent = await Parents(manager, f.A);
        var root = Root(f.A) + "/voyages";
        var id = await Create(manager, root, new {
            referenceCode = "VOYAGE-STATE", vesselId = parent.Vessel,
            originPortId = parent.PortA, destinationPortId = parent.PortB
        });
        using (var invalid = await ReferenceDataHttpTests.Send(manager, HttpMethod.Patch, root + "/" + id,
            new { voyageStatus = "Arrived", expectedVersion = 1 }))
            Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
        using (var underway = await ReferenceDataHttpTests.Send(manager, HttpMethod.Patch, root + "/" + id,
            new { voyageStatus = "Underway", actualDepartureAtUtc = "2026-11-01T21:00:00Z",
                expectedVersion = 1 }))
            Assert.Equal(HttpStatusCode.OK, underway.StatusCode);
        using (var stale = await ReferenceDataHttpTests.Send(manager, HttpMethod.Patch, root + "/" + id,
            new { voyageStatus = "Arrived", actualArrivalAtUtc = "2026-11-02T03:00:00Z",
                expectedVersion = 1 }))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var arrived = await ReferenceDataHttpTests.Send(manager, HttpMethod.Patch, root + "/" + id,
            new { voyageStatus = "Arrived", actualArrivalAtUtc = "2026-11-02T03:00:00Z",
                expectedVersion = 2 }))
            Assert.Equal(HttpStatusCode.OK, arrived.StatusCode);
        using (var archived = await ReferenceDataHttpTests.Send(manager, HttpMethod.Delete, root + "/" + id,
            new { expectedVersion = 3 }))
            Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        using (var failedEdit = await ReferenceDataHttpTests.Send(manager, HttpMethod.Patch, root + "/" + id,
            new { voyageStatus = "Underway", expectedVersion = 4 }))
            Assert.Equal(HttpStatusCode.Conflict, failedEdit.StatusCode);
        using (var restored = await ReferenceDataHttpTests.Send(manager, HttpMethod.Patch, root + "/" + id,
            new { recordStatus = "Active", expectedVersion = 4 }))
            Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }

    [Fact]
    public async Task ForeignParentAndForeignRecordIdsDoNotLeak()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager;
        var parent = await Parents(manager, f.A);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var now = DateTimeOffset.UtcNow;
        var foreignPort = new PortReference {
            Id = Guid.NewGuid(), OrganizationId = f.B, ReferenceCode = "FOREIGN-PORT",
            Name = "Foreign Synthetic Port", CountryCode = "US", Status = "Active",
            SourceKind = "Synthetic", CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.Ports.Add(foreignPort);
        db.Containers.Add(new ContainerRecord {
            Id = Guid.NewGuid(), OrganizationId = f.B, ReferenceCode = "FOREIGN-BOX",
            IdentifierKind = "Synthetic", ContainerIdentifier = "SYN-BOX-FOREIGN",
            EquipmentType = "Dry20", SourceKind = "Synthetic",
            CreatedAtUtc = now, UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();
        using var invalid = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post,
            Root(f.A) + "/voyages", new {
                referenceCode = "BAD-FOREIGN", vesselId = parent.Vessel,
                originPortId = foreignPort.Id, destinationPortId = parent.PortB
            });
        Assert.Equal(HttpStatusCode.NotFound, invalid.StatusCode);
        var foreignId = await db.Containers.Where(x => x.OrganizationId == f.B)
            .Select(x => x.Id).SingleAsync();
        using var inaccessible = await manager.GetAsync(Root(f.A) + "/containers/" + foreignId);
        Assert.Equal(HttpStatusCode.NotFound, inaccessible.StatusCode);
        using var modify = await ReferenceDataHttpTests.Send(manager, HttpMethod.Patch,
            Root(f.A) + "/containers/" + foreignId,
            new { displayName = "Exploit", expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.NotFound, modify.StatusCode);
    }

    [Fact]
    public async Task InvalidChecksumAndDuplicateIdentifierRejectWithoutPartialWrites()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager;
        var root = Root(f.A) + "/containers";
        using (var invalid = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post, root, new {
            referenceCode = "BAD-ISO", identifierKind = "ISO6346",
            containerIdentifier = "CSQU3054382", equipmentType = "Dry20"
        })) Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await Create(manager, root, new {
            referenceCode = "BOX-RESERVED", identifierKind = "ISO6346",
            containerIdentifier = "CSQU3054383", equipmentType = "Dry20"
        });
        using var duplicate = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post, root, new {
            referenceCode = "BOX-OTHER", identifierKind = "ISO6346",
            containerIdentifier = "CSQU3054383", equipmentType = "Other"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Single(await db.Containers.AsNoTracking()
            .Where(x => x.OrganizationId == f.A && x.ContainerIdentifier == "CSQU3054383").ToListAsync());
    }
}
