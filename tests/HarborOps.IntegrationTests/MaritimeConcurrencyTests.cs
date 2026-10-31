using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Maritime;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.References;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class MaritimeConcurrencyTests
{
    [Fact]
    public async Task DuplicateContainerCreateRacePersistsOneIdentifier()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager;
        using var admin = f.Admin;
        using var analyst = f.Analyst;
        var root = "/api/v1/organizations/" + f.A + "/containers";
        var body = new {
            referenceCode = "RACE-BOX", identifierKind = "Synthetic",
            containerIdentifier = "SYN-BOX-RACE01", equipmentType = "Dry20"
        };
        var first = ReferenceDataHttpTests.Send(manager, HttpMethod.Post, root, body);
        var second = ReferenceDataHttpTests.Send(manager, HttpMethod.Post, root, body);
        using var a = await first;
        using var b = await second;
        Assert.Equal(1, new[] { a.StatusCode, b.StatusCode }.Count(x => x == HttpStatusCode.Created));
        Assert.Equal(1, new[] { a.StatusCode, b.StatusCode }.Count(x => x == HttpStatusCode.Conflict));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(1, await db.Containers.AsNoTracking()
            .CountAsync(x => x.OrganizationId == f.A &&
                x.ContainerIdentifier == "SYN-BOX-RACE01"));
    }

    [Fact]
    public async Task ParentArchivalAndVoyageCreationSerialize()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager;
        using var admin = f.Admin;
        using var analyst = f.Analyst;
        var baseUrl = "/api/v1/organizations/" + f.A;
        async Task<Guid> Create(string path, object data)
        {
            using var response = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post, path, data);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }
        var vessel = await Create(baseUrl + "/vessels", new {
            referenceCode = "RACE-SHIP", name = "Synthetic Race Vessel" });
        var port1 = await Create(baseUrl + "/ports", new {
            referenceCode = "RACE-PORT1", name = "Race Departure", countryCode = "US" });
        var port2 = await Create(baseUrl + "/ports", new {
            referenceCode = "RACE-PORT2", name = "Race Arrival", countryCode = "US" });
        var archive = ReferenceDataHttpTests.Send(manager, HttpMethod.Delete,
            baseUrl + "/vessels/" + vessel, new { expectedVersion = 1L });
        var create = ReferenceDataHttpTests.Send(manager, HttpMethod.Post,
            baseUrl + "/voyages", new {
                referenceCode = "RACE-VOYAGE", vesselId = vessel,
                originPortId = port1, destinationPortId = port2
            });
        using var archived = await archive;
        using var created = await create;
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        Assert.Contains(created.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.NotFound });
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal("Archived", (await db.Vessels.AsNoTracking()
            .SingleAsync(x => x.Id == vessel)).Status);
        var voyages = await db.Voyages.AsNoTracking().Where(x => x.OrganizationId == f.A).ToListAsync();
        Assert.Equal(created.StatusCode == HttpStatusCode.Created ? 1 : 0, voyages.Count);
        if (voyages.Count == 1)
        {
            Assert.Equal(vessel, voyages[0].VesselId);
            Assert.Equal(port1, voyages[0].OriginPortId);
            Assert.Equal(port2, voyages[0].DestinationPortId);
        }
        using var after = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post,
            baseUrl + "/voyages", new {
                referenceCode = "RACE-AFTER", vesselId = vessel,
                originPortId = port1, destinationPortId = port2
            });
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
    }

    [Fact]
    public async Task ExistingForeignVoyageUuidCannotBeReadOrMutated()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager;
        using var admin = f.Admin;
        using var analyst = f.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var now = DateTimeOffset.UtcNow;
        var vessel = new VesselReference {
            Id = Guid.NewGuid(), OrganizationId = f.B, ReferenceCode = "FOREIGN-VESSEL",
            Name = "Foreign Vessel", SourceKind = "Synthetic", CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var departure = new PortReference {
            Id = Guid.NewGuid(), OrganizationId = f.B, ReferenceCode = "FOREIGN-START",
            Name = "Foreign Start", CountryCode = "US", SourceKind = "Synthetic",
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var arrival = new PortReference {
            Id = Guid.NewGuid(), OrganizationId = f.B, ReferenceCode = "FOREIGN-END",
            Name = "Foreign End", CountryCode = "US", SourceKind = "Synthetic",
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.Vessels.Add(vessel); db.Ports.AddRange(departure, arrival);
        var foreign = new VoyageRecord {
            Id = Guid.NewGuid(), OrganizationId = f.B, ReferenceCode = "FOREIGN-VOYAGE",
            VesselId = vessel.Id, OriginPortId = departure.Id, DestinationPortId = arrival.Id,
            SourceKind = "Synthetic", CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.Voyages.Add(foreign);
        await db.SaveChangesAsync();
        var path = "/api/v1/organizations/" + f.A + "/voyages/" + foreign.Id;
        using var get = await manager.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        using var patch = await ReferenceDataHttpTests.Send(manager, HttpMethod.Patch,
            path, new { plannedDepartureAtUtc = "2026-11-01T15:00:00Z", expectedVersion = 1L });
        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
        using var delete = await ReferenceDataHttpTests.Send(manager, HttpMethod.Delete,
            path, new { expectedVersion = 1L });
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(1L, (await db.Voyages.AsNoTracking()
            .SingleAsync(x => x.Id == foreign.Id)).Version);
    }
}
