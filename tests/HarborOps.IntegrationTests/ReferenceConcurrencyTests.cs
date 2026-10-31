using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class ReferenceConcurrencyTests
{
    private static Task<HttpResponseMessage> Send(HttpClient c, HttpMethod m, string path, object data) =>
        ReferenceDataHttpTests.Send(c, m, path, data);

    [Fact]
    public async Task ParallelUpdateAndArchiveProduceExactlyOneVersionTransition()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        using var second = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(second, f.ManagerEmail);
        await ReferenceDataHttpTests.Select(second, f.A);

        foreach (var kind in new[] { "ports", "vessels" })
        {
            var root = $"/api/v1/organizations/{f.A}/{kind}";
            using var create = await Send(manager, HttpMethod.Post, root,
                kind == "ports"
                    ? new { referenceCode = "RACE-PORT", name = "Synthetic Port", countryCode = "US" }
                    : (object)new { referenceCode = "RACE-SHIP", name = "Synthetic Vessel" });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            var row = await create.Content.ReadFromJsonAsync<JsonElement>();
            var path = root + "/" + row.GetProperty("id").GetGuid();

            var responses = await Task.WhenAll(
                Send(manager, HttpMethod.Patch, path,
                    new { name = "Race Updated", expectedVersion = 1 }),
                Send(second, HttpMethod.Delete, path, new { expectedVersion = 1 }));
            var status = responses.Select(x => x.StatusCode).ToArray();
            Assert.Contains(HttpStatusCode.Conflict, status);
            Assert.Equal(1, status.Count(x =>
                x is HttpStatusCode.OK or HttpStatusCode.NoContent));
            foreach (var response in responses) response.Dispose();
            var persisted = await manager.GetFromJsonAsync<JsonElement>(path);
            Assert.Equal(2L, persisted.GetProperty("version").GetInt64());
            Assert.True(persisted.GetProperty("name").GetString() == "Race Updated" ||
                persisted.GetProperty("status").GetString() == "Archived");
        }
    }

    [Fact]
    public async Task ParallelDuplicateCreatesReturnConflictWithoutDuplicateRows()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        using var second = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(second, f.ManagerEmail);
        await ReferenceDataHttpTests.Select(second, f.A);
        var root = $"/api/v1/organizations/{f.A}/vessels";
        var operations = await Task.WhenAll(
            Send(manager, HttpMethod.Post, root,
                new { referenceCode = "DUPLICATE-A", name = "Synthetic A", mmsi = "012345678" }),
            Send(second, HttpMethod.Post, root,
                new { referenceCode = "DUPLICATE-B", name = "Synthetic B", mmsi = "012345678" }));
        Assert.Equal(1, operations.Count(x => x.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, operations.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        foreach (var result in operations) result.Dispose();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(1, await db.Vessels.CountAsync(x =>
            x.OrganizationId == f.A && x.Mmsi == "012345678"));
    }

    [Fact]
    public async Task IdenticalExternalIdentifiersAreAllowedAcrossOrganizations()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var now = DateTimeOffset.UtcNow;
            db.Vessels.Add(new HarborOps.Infrastructure.References.VesselReference
            {
                Id = Guid.NewGuid(), OrganizationId = f.B,
                ReferenceCode = "SHARED-LOCAL", Name = "Tenant B Vessel",
                ImoNumber = "9074729", Status = "Active", SourceKind = "Synthetic",
                Version = 1, CreatedAtUtc = now, UpdatedAtUtc = now
            });
            await db.SaveChangesAsync();
        }
        using var create = await Send(manager, HttpMethod.Post,
            $"/api/v1/organizations/{f.A}/vessels",
            new { referenceCode = "SHARED-LOCAL", name = "Tenant A Vessel", imoNumber = "9074729" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var body = await create.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(f.A, body.GetProperty("organizationId").GetGuid());
    }
}
