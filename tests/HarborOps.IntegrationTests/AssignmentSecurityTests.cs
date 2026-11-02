using System.Net;
using System.Net.Http.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class AssignmentSecurityTests
{
    [Fact]
    public async Task AuditorCanReadAssignmentsButCannotMutateOrReadMaritimeCatalog()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager; using var analyst = f.Analyst; using var admin = f.Admin;
        var seed = await AssignmentHttpTests.Seed(f);
        var route = seed.Root + "/" + seed.Shipment + "/container-assignments";
        using (var created = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post, route,
            new { containerId = seed.Container, startsAtUtc = "2026-11-10T10:00:00Z" }))
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        const string password = "Synthetic-reference-integration-passphrase-123";
        var email = "g6-auditor-" + Guid.NewGuid().ToString("N") + "@example.test";
        Guid auditorId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
            await identity.ProvisionAsync(email, "Synthetic G6 Auditor", password, CancellationToken.None);
            auditorId = await db.AuthUsers.AsNoTracking().Where(x => x.NormalizedEmail ==
                IdentityStore.NormalizeEmail(email)).Select(x => x.Id).SingleAsync();
        }
        using (var membership = await ReferenceDataHttpTests.Send(admin, HttpMethod.Put,
            "/api/v1/organizations/" + f.A + "/memberships/" + auditorId,
            new { role = "Auditor", status = "Active", expectedVersion = (long?)null }))
            Assert.Equal(HttpStatusCode.OK, membership.StatusCode);

        using var auditor = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            { HandleCookies = true });
        await ReferenceDataHttpTests.Login(auditor, email);
        await ReferenceDataHttpTests.Select(auditor, f.A);
        using (var read = await auditor.GetAsync(route))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            var detail = await read.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var assignments = detail.GetProperty("assignments");
            Assert.Single(assignments.EnumerateArray());
            var label = assignments[0].GetProperty("targetLabel").GetString();
            Assert.Contains("G6-BOX-", label);
        }
        using (var catalog = await auditor.GetAsync("/api/v1/organizations/" + f.A + "/containers"))
            Assert.Equal(HttpStatusCode.Forbidden, catalog.StatusCode);
        using (var denied = await ReferenceDataHttpTests.Send(auditor, HttpMethod.Post, route,
            new { containerId = seed.Container, startsAtUtc = "2026-11-10T11:00:00Z" }))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var administrator = await admin.GetAsync(route))
            Assert.Equal(HttpStatusCode.Forbidden, administrator.StatusCode);
    }

    [Fact]
    public async Task CompetingSameContainerWritesCommitAtMostOneAssignment()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager; using var analyst = f.Analyst; using var admin = f.Admin;
        var seed = await AssignmentHttpTests.Seed(f);
        var first = seed.Root + "/" + seed.Shipment + "/container-assignments";
        var second = seed.Root + "/" + seed.OtherShipment + "/container-assignments";
        var taskA = ReferenceDataHttpTests.Send(manager, HttpMethod.Post, first,
            new { containerId = seed.Container, startsAtUtc = "2026-11-10T10:00:00Z" });
        var taskB = ReferenceDataHttpTests.Send(analyst, HttpMethod.Post, second,
            new { containerId = seed.Container, startsAtUtc = "2026-11-10T10:30:00Z" });
        using var a = await taskA;
        using var b = await taskB;
        var statuses = new[] { a.StatusCode, b.StatusCode }.Order().ToArray();
        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }.Order().ToArray(), statuses);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(1, await db.ShipmentContainerAssignments.AsNoTracking()
            .CountAsync(x => x.OrganizationId == f.A && x.ContainerId == seed.Container));
    }

    [Fact]
    public async Task CompetingVoyagesForOneShipmentCommitAtMostOneAssignment()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager; using var analyst = f.Analyst; using var admin = f.Admin;
        var seed = await AssignmentHttpTests.Seed(f);
        var prefix = "/api/v1/organizations/" + f.A;
        using var first = await manager.GetAsync(prefix + "/voyages/" + seed.Voyage);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var row = await first.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        using var other = await ReferenceDataHttpTests.Send(manager,HttpMethod.Post,
            prefix + "/voyages",new {
                referenceCode="G6-RACE-"+Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                vesselId=row.GetProperty("vesselId").GetGuid(),
                originPortId=row.GetProperty("originPortId").GetGuid(),
                destinationPortId=row.GetProperty("destinationPortId").GetGuid()
            });
        Assert.Equal(HttpStatusCode.Created,other.StatusCode);
        var otherVoyage=(await other.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>())
            .GetProperty("id").GetGuid();
        var url=seed.Root+"/"+seed.Shipment+"/voyage-assignments";
        var firstWrite=ReferenceDataHttpTests.Send(manager,HttpMethod.Post,url,
            new{voyageId=seed.Voyage,startsAtUtc="2040-10-01T10:00:00Z"});
        var secondWrite=ReferenceDataHttpTests.Send(analyst,HttpMethod.Post,url,
            new{voyageId=otherVoyage,startsAtUtc="2040-10-01T10:00:00Z"});
        using var a=await firstWrite;using var b=await secondWrite;
        Assert.Equal(new[]{HttpStatusCode.Created,HttpStatusCode.Conflict}.Order(),
            new[]{a.StatusCode,b.StatusCode}.Order());
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(1,await db.ShipmentVoyageAssignments.AsNoTracking()
            .CountAsync(x=>x.OrganizationId==f.A && x.ShipmentId==seed.Shipment));
    }

    [Fact]
    public async Task LiveMembershipDowngradeImmediatelyRevokesAssignmentAccess()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager; using var analyst = f.Analyst; using var admin = f.Admin;
        var seed = await AssignmentHttpTests.Seed(f);
        var route = seed.Root + "/" + seed.Shipment + "/voyage-assignments";
        using (var read = await analyst.GetAsync(route))
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        long version;
        Guid analystId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            analystId = await db.AuthUsers.AsNoTracking().Where(x =>
                x.NormalizedEmail == IdentityStore.NormalizeEmail(f.AnalystEmail))
                .Select(x => x.Id).SingleAsync();
            version = await db.OrganizationMemberships.AsNoTracking()
                .Where(x => x.OrganizationId == f.A && x.UserId == analystId)
                .Select(x => x.Version).SingleAsync();
        }
        using (var downgrade = await ReferenceDataHttpTests.Send(admin, HttpMethod.Put,
            "/api/v1/organizations/" + f.A + "/memberships/" + analystId,
            new { role = "Support", status = "Active", expectedVersion = version }))
            Assert.Equal(HttpStatusCode.OK, downgrade.StatusCode);
        using (var after = await analyst.GetAsync(route))
            Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
        using (var denied = await ReferenceDataHttpTests.Send(analyst, HttpMethod.Post, route,
            new { voyageId = seed.Voyage, startsAtUtc = "2026-11-10T10:00:00Z" }))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
