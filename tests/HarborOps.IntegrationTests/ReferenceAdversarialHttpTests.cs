using System.Net;
using System.Net.Http.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.References;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class ReferenceAdversarialHttpTests
{
    private const string Password = "Synthetic-reference-integration-passphrase-123";

    private static Task<HttpResponseMessage> Send(HttpClient c, HttpMethod method,
        string path, object? value = null) =>
        ReferenceDataHttpTests.Send(c, method, path, value);

    [Fact]
    public async Task SupportAndAuditorCannotUseAnyReferenceVerb()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var ids = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var organizations = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var adminActor = await ids.LoginAsync(f.AdminEmail, Password, "g3-role-matrix", CancellationToken.None);
        Assert.NotNull(adminActor);
        await organizations.SelectAsync(adminActor!.UserId, adminActor.SessionId, f.A, CancellationToken.None);
        foreach (var role in new[] { "Support", "Auditor" })
        {
            var email = $"g3-{role.ToLowerInvariant()}-{Guid.NewGuid():N}@example.test";
            await ids.ProvisionAsync(email, "Synthetic " + role, Password, CancellationToken.None);
            var actor = await ids.LoginAsync(email, Password, "g3-role-matrix", CancellationToken.None);
            Assert.NotNull(actor);
            var grant = await organizations.PutMemberAsync(adminActor.UserId,
                adminActor.SessionId, f.A, actor!.UserId, role, "Active", null,
                "g3-role-matrix", CancellationToken.None);
            Assert.NotNull(grant.Value);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
            await ReferenceDataHttpTests.Login(client, email);
            await ReferenceDataHttpTests.Select(client, f.A);
            foreach (var kind in new[] { "ports", "vessels" })
            {
                var root = $"/api/v1/organizations/{f.A}/{kind}";
                var id = Guid.NewGuid();
                var routes = new (HttpMethod method, string path, object? body)[]
                {
                    (HttpMethod.Get, root, null),
                    (HttpMethod.Get, root + "/" + id, null),
                    (HttpMethod.Post, root, kind == "ports"
                        ? new { referenceCode = "SUPPORT-X", name = "Synthetic", countryCode = "US" }
                        : (object)new { referenceCode = "AUDITOR-X", name = "Synthetic" }),
                    (HttpMethod.Patch, root + "/" + id, new { name = "Denied", expectedVersion = 1 }),
                    (HttpMethod.Delete, root + "/" + id, new { expectedVersion = 1 })
                };
                foreach (var route in routes)
                {
                    using var response = await Send(client, route.method, route.path, route.body);
                    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                }
            }
        }
    }

    [Fact]
    public async Task ForeignPortAndVesselRejectEveryRecordVerbWithoutMutation()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var portId = Guid.NewGuid();
        var vesselId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            db.Ports.Add(new PortReference
            {
                Id = portId, OrganizationId = f.B, ReferenceCode = "FOREIGN-PORT",
                Name = "Private Port B", CountryCode = "US", Status = "Active",
                SourceKind = "Synthetic", Version = 1,
                CreatedAtUtc = now, UpdatedAtUtc = now
            });
            db.Vessels.Add(new VesselReference
            {
                Id = vesselId, OrganizationId = f.B, ReferenceCode = "FOREIGN-VESSEL",
                Name = "Private Vessel B", Status = "Active", SourceKind = "Synthetic",
                Version = 1, CreatedAtUtc = now, UpdatedAtUtc = now
            });
            await db.SaveChangesAsync();
        }
        foreach (var (kind, id) in new[] { ("ports", portId), ("vessels", vesselId) })
        {
            var root = $"/api/v1/organizations/{f.A}/{kind}/{id}";
            var responses = new[]
            {
                await Send(manager, HttpMethod.Get, root),
                await Send(manager, HttpMethod.Patch, root,
                    new { name = "Leaked", expectedVersion = 1 }),
                await Send(manager, HttpMethod.Patch, root,
                    new { status = "Active", expectedVersion = 1 }),
                await Send(manager, HttpMethod.Delete, root, new { expectedVersion = 1 })
            };
            foreach (var response in responses)
            {
                using (response)
                {
                    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                    var body = await response.Content.ReadAsStringAsync();
                    Assert.DoesNotContain("Private", body, StringComparison.Ordinal);
                }
            }
        }
        await using var verification = factory.Services.CreateAsyncScope();
        var current = verification.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal("Private Port B",
            (await current.Ports.AsNoTracking().SingleAsync(x => x.Id == portId)).Name);
        Assert.Equal("Active",
            (await current.Vessels.AsNoTracking().SingleAsync(x => x.Id == vesselId)).Status);
    }

    [Fact]
    public async Task SecurityDowngradeAndSuspendedOrganizationDenyReferenceReads()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var root = $"/api/v1/organizations/{f.A}/ports";
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync(root)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var membership = await db.OrganizationMemberships.AsNoTracking()
            .SingleAsync(m => m.OrganizationId == f.A && m.Role == "Manager");
        using var changed = await Send(admin, HttpMethod.Put,
            $"/api/v1/organizations/{f.A}/memberships/{membership.UserId}",
            new { role = "Support", status = "Active", expectedVersion = membership.Version });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using var denied = await manager.GetAsync(root);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        await db.Organizations.Where(o => o.Id == f.A)
            .ExecuteUpdateAsync(set => set.SetProperty(o => o.Status, "Suspended"));
        using var unavailable = await analyst.GetAsync(root);
        Assert.NotEqual(HttpStatusCode.OK, unavailable.StatusCode);
    }
}
