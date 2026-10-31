using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class MaritimeBoundaryTests
{
    private static readonly string[] Collections = ["voyages", "containers"];
    private static string Url(Guid org, string kind, Guid? id = null) =>
        "/api/v1/organizations/" + org + "/" + kind + (id.HasValue ? "/" + id : "");
    private static object CreateBody(string kind) => kind == "voyages"
        ? new { referenceCode = "BOUND-VOYAGE", vesselId = Guid.NewGuid(),
            originPortId = Guid.NewGuid(), destinationPortId = Guid.NewGuid() }
        : new { referenceCode = "BOUND-BOX", identifierKind = "Synthetic",
            containerIdentifier = "SYN-BOX-BOUNDARY", equipmentType = "Dry20" };
    private static object EditBody(string kind) => kind == "voyages"
        ? new { plannedDepartureAtUtc = "2026-11-01T12:00:00Z", expectedVersion = 1L }
        : new { displayName = "Changed", expectedVersion = 1L };
    private static async Task<HttpResponseMessage> Send(HttpClient c, HttpMethod method,
        string path, object? body = null, bool csrf = true) =>
        await ReferenceDataHttpTests.Send(c, method, path, body, csrf);

    [Fact]
    public async Task EveryMaritimeVerbRequiresSelectedOrganization()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        using var client = factory.CreateClient();
        await ReferenceDataHttpTests.Login(client, f.ManagerEmail);
        foreach (var kind in Collections)
        {
            var id = Guid.NewGuid();
            var baseUrl = Url(f.A, kind);
            var cases = new[] {
                (HttpMethod.Get, baseUrl, (object?)null),
                (HttpMethod.Get, baseUrl + "/" + id, null),
                (HttpMethod.Post, baseUrl, CreateBody(kind)),
                (HttpMethod.Patch, baseUrl + "/" + id, EditBody(kind)),
                (HttpMethod.Delete, baseUrl + "/" + id, new { expectedVersion = 1L })
            };
            foreach (var (method, route, body) in cases)
            {
                using var response = await Send(client, method, route, body);
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                Assert.Contains("organization_selection_required",
                    await response.Content.ReadAsStringAsync());
            }
        }
    }

    [Fact]
    public async Task InvalidFiltersAndMalformedPayloadsFailSafely()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager;
        using var admin = f.Admin;
        using var analyst = f.Analyst;
        foreach (var kind in Collections)
        {
            var path = Url(f.A, kind);
            foreach (var query in new[] { "?limit=101", "?limit=0", "?recordStatus=Deleted",
                "?after=abc", "?unexpected=1", "?q=x&q=y" })
            {
                using var bad = await manager.GetAsync(path + query);
                Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
            }
            using var unknown = await Send(manager, HttpMethod.Post, path,
                new { referenceCode = "TEST-UNKNOWN", wrongField = 1 });
            Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
            using var huge = await Send(manager, HttpMethod.Post, path,
                new { referenceCode = "TEST-HUGE", displayName = new string('X', 9000) });
            Assert.Equal(HttpStatusCode.BadRequest, huge.StatusCode);
            using var forbidden = await Send(manager, HttpMethod.Post, path, CreateBody(kind), false);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
    }

    [Fact]
    public async Task NonBusinessRolesReceiveNoImplicitM4Access()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        foreach (var kind in Collections)
        {
            using var restricted = await admin.GetAsync(Url(f.A, kind));
            Assert.Equal(HttpStatusCode.Forbidden, restricted.StatusCode);
            using var own = await analyst.GetAsync(Url(f.A, kind));
            Assert.Equal(HttpStatusCode.OK, own.StatusCode);
            using var writeDenied = await Send(analyst, HttpMethod.Post, Url(f.A, kind), CreateBody(kind));
            Assert.Equal(HttpStatusCode.Forbidden, writeDenied.StatusCode);
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var member = await db.OrganizationMemberships.SingleAsync(m =>
            m.OrganizationId == f.A && m.Role == "Manager");
        foreach (var role in new[] { "Support", "Auditor" })
        {
            using (var downgraded = await Send(admin, HttpMethod.Put,
                "/api/v1/organizations/" + f.A + "/memberships/" + member.UserId,
                new { role, status = "Active", expectedVersion = member.Version }))
                Assert.Equal(HttpStatusCode.OK, downgraded.StatusCode);
            member.Version++;
            foreach (var kind in Collections)
            {
                using var noRead = await manager.GetAsync(Url(f.A, kind));
                Assert.Equal(HttpStatusCode.Forbidden, noRead.StatusCode);
                using var noWrite = await Send(manager, HttpMethod.Post, Url(f.A, kind), CreateBody(kind));
                Assert.Equal(HttpStatusCode.Forbidden, noWrite.StatusCode);
            }
        }
    }

    [Fact]
    public async Task CompetingContainerUpdatesAreVersionGuarded()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager;
        using var admin = f.Admin;
        using var analyst = f.Analyst;
        var root = Url(f.A, "containers");
        using var created = await Send(manager, HttpMethod.Post, root, new {
            referenceCode = "CONCURRENT-BOX", identifierKind = "Synthetic",
            containerIdentifier = "SYN-BOX-CONCURRENT", equipmentType = "Dry20"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var first = Send(manager, HttpMethod.Patch, root + "/" + id,
            new { displayName = "First", expectedVersion = 1L });
        var second = Send(manager, HttpMethod.Patch, root + "/" + id,
            new { displayName = "Second", expectedVersion = 1L });
        using var a = await first;
        using var b = await second;
        Assert.Equal(1, new[] { a.StatusCode, b.StatusCode }.Count(x => x == HttpStatusCode.OK));
        Assert.Equal(1, new[] { a.StatusCode, b.StatusCode }.Count(x => x == HttpStatusCode.Conflict));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var row = await db.Containers.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(2L, row.Version);
        Assert.Contains(row.DisplayName, new[] { "First", "Second" });
    }
}
