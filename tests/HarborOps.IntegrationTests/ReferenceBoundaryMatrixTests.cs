using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.References;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class ReferenceBoundaryMatrixTests
{
    private const string Password = "Synthetic-reference-integration-passphrase-123";

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method,
        string path, object? body = null, bool csrf = true) =>
        ReferenceDataHttpTests.Send(client, method, path, body, csrf);

    [Fact]
    public async Task EveryReferenceRouteRejectsAnonymousAndWrongRoles()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var root = "/api/v1/organizations/" + fixture.A;
        var port = root + "/ports";
        var vessels = root + "/vessels";
        var id = Guid.NewGuid();
        var routes = new (HttpMethod Method, string Route, object? Body)[]
        {
            (HttpMethod.Get, port, null),
            (HttpMethod.Get, port + "/" + id, null),
            (HttpMethod.Post, port, new { referenceCode = "TEST-PORT", name = "Synthetic", countryCode = "US" }),
            (HttpMethod.Patch, port + "/" + id, new { name = "Changed", expectedVersion = 1L }),
            (HttpMethod.Delete, port + "/" + id, new { expectedVersion = 1L }),
            (HttpMethod.Get, vessels, null),
            (HttpMethod.Get, vessels + "/" + id, null),
            (HttpMethod.Post, vessels, new { referenceCode = "TEST-SHIP", name = "Synthetic" }),
            (HttpMethod.Patch, vessels + "/" + id, new { name = "Changed", expectedVersion = 1L }),
            (HttpMethod.Delete, vessels + "/" + id, new { expectedVersion = 1L })
        };
        using var anonymous = factory.CreateClient();
        foreach (var route in routes)
        {
            using var noLogin = await Send(anonymous, route.Method, route.Route, route.Body);
            Assert.Equal(HttpStatusCode.Unauthorized, noLogin.StatusCode);
            using var adminResponse = await Send(admin, route.Method, route.Route, route.Body);
            Assert.Equal(HttpStatusCode.Forbidden, adminResponse.StatusCode);
        }
        foreach (var route in routes.Where(x => x.Method != HttpMethod.Get))
        {
            using var readonlyResponse = await Send(analyst, route.Method, route.Route, route.Body);
            Assert.Equal(HttpStatusCode.Forbidden, readonlyResponse.StatusCode);
        }
        foreach (var route in routes.Where(x => x.Method == HttpMethod.Get &&
            !x.Route.EndsWith(id.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            using var viewerResponse = await Send(analyst, route.Method, route.Route);
            Assert.Equal(HttpStatusCode.OK, viewerResponse.StatusCode);
        }
        var bPath = "/api/v1/organizations/" + fixture.B;
        foreach (var route in routes)
        {
            var foreignPath = route.Route.Replace(root, bPath, StringComparison.Ordinal);
            using var foreign = await Send(manager, route.Method, foreignPath, route.Body);
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
            var json = await foreign.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Reference Test B", json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task MissingSelectionAndTrueForeignRecordsCannotBeEnumerated()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        using var noSelection = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(noSelection, f.ManagerEmail);
        var root = "/api/v1/organizations/" + f.A;
        using var missing = await noSelection.GetAsync(root + "/ports");
        Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        Assert.Contains("organization_selection_required",
            await missing.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var foreignId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.Ports.Add(new PortReference
        {
            Id = foreignId, OrganizationId = f.B, ReferenceCode = "FOREIGN-PORT",
            Name = "Foreign private port", CountryCode = "US",
            Status = "Active", SourceKind = "Synthetic",
            Version = 1, CreatedAtUtc = now, UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();
        var genuineForeign = root + "/ports/" + foreignId;
        using var foreign = await manager.GetAsync(genuineForeign);
        using var nonexistent = await manager.GetAsync(root + "/ports/" + Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, nonexistent.StatusCode);
        Assert.Equal(await nonexistent.Content.ReadAsStringAsync(),
            await foreign.Content.ReadAsStringAsync());
        using var list = await manager.GetAsync(root + "/ports?q=Foreign");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var json = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(json.GetProperty("ports").EnumerateArray());
    }

    [Fact]
    public async Task QueryBoundsAndLiteralWildcardsProtectScopedPagination()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var root = "/api/v1/organizations/" + f.A + "/ports";
        foreach (var suffix in new[] { "ONE", "TWO", "THREE" })
        {
            using var created = await Send(manager, HttpMethod.Post, root,
                new { referenceCode = "PAGE-" + suffix, name = "Synthetic Page " + suffix,
                    countryCode = "US" });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }
        var first = await manager.GetFromJsonAsync<JsonElement>(root + "?limit=1");
        Assert.Single(first.GetProperty("ports").EnumerateArray());
        var cursor = first.GetProperty("nextAfter").GetGuid();
        var second = await manager.GetFromJsonAsync<JsonElement>(root + "?limit=1&after=" + cursor);
        Assert.Single(second.GetProperty("ports").EnumerateArray());
        Assert.NotEqual(first.GetProperty("ports")[0].GetProperty("id").GetGuid(),
            second.GetProperty("ports")[0].GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await manager.GetAsync(root + "?limit=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await manager.GetAsync(root + "?limit=-1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await manager.GetAsync(root + "?after=garbage")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await manager.GetAsync(root + "?status=Removed")).StatusCode);
        var wildcard = await manager.GetFromJsonAsync<JsonElement>(root + "?q=%25");
        Assert.Empty(wildcard.GetProperty("ports").EnumerateArray());
    }

    [Fact]
    public async Task SuspendedMembershipIsDeniedOnNextRequest()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var root = "/api/v1/organizations/" + f.A + "/ports";
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync(root)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var membership = await db.OrganizationMemberships.AsNoTracking()
            .SingleAsync(m => m.OrganizationId == f.A && m.Role == "Manager");
        using var suspended = await Send(admin, HttpMethod.Put,
            "/api/v1/organizations/" + f.A + "/memberships/" + membership.UserId,
            new { role = "Manager", status = "Suspended", expectedVersion = membership.Version });
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        using var denied = await manager.GetAsync(root);
        Assert.NotEqual(HttpStatusCode.OK, denied.StatusCode);
        var body = await denied.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Synthetic Page", body, StringComparison.Ordinal);
    }
}
