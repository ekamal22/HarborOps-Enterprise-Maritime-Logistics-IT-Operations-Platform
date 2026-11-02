using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class AssignmentPermissionMatrixTests
{
    [Fact]
    public async Task EveryAssignmentEndpointHonorsFiveRoleMatrixForBothKinds()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        using var admin = f.Admin;
        var seed = await AssignmentHttpTests.Seed(f);
        var basePath = seed.Root + "/" + seed.Shipment;
        var containerPath = basePath + "/container-assignments";
        var voyagePath = basePath + "/voyage-assignments";
        static async Task<Guid> Create(HttpClient client, string url, object request)
        {
            using var response = await ReferenceDataHttpTests.Send(client, HttpMethod.Post, url, request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }
        var containerId = await Create(manager, containerPath,
            new { containerId = seed.Container, startsAtUtc = "2039-01-01T10:00:00Z" });
        var voyageId = await Create(manager, voyagePath,
            new { voyageId = seed.Voyage, startsAtUtc = "2039-01-01T10:00:00Z" });

        const string password = "Synthetic-reference-integration-passphrase-123";
        var suffix = Guid.NewGuid().ToString("N");
        var auditorEmail = "g6-matrix-auditor-" + suffix + "@example.test";
        var supportEmail = "g6-matrix-support-" + suffix + "@example.test";
        var ids = new Dictionary<string,Guid>();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
            foreach (var email in new[] {auditorEmail,supportEmail})
            {
                await identity.ProvisionAsync(email, "Synthetic G6 Matrix", password,
                    CancellationToken.None);
                ids[email] = await db.AuthUsers.AsNoTracking()
                    .Where(x => x.NormalizedEmail == IdentityStore.NormalizeEmail(email))
                    .Select(x => x.Id).SingleAsync();
            }
        }
        foreach (var (email,role) in new[] { (auditorEmail,"Auditor"),(supportEmail,"Support") })
        {
            using var assignment = await ReferenceDataHttpTests.Send(admin,HttpMethod.Put,
                "/api/v1/organizations/"+f.A+"/memberships/"+ids[email],
                new {role,status="Active",expectedVersion=(long?)null});
            Assert.Equal(HttpStatusCode.OK, assignment.StatusCode);
        }
        using var auditor = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {HandleCookies=true});
        using var support = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {HandleCookies=true});
        await ReferenceDataHttpTests.Login(auditor,auditorEmail);
        await ReferenceDataHttpTests.Login(support,supportEmail);
        await ReferenceDataHttpTests.Select(auditor,f.A);
        await ReferenceDataHttpTests.Select(support,f.A);

        var roles = new (string Name,HttpClient Client,bool View,bool Manage)[]
        {
            ("Manager",manager,true,true),
            ("Analyst",analyst,true,true),
            ("Auditor",auditor,true,false),
            ("Administrator",admin,false,false),
            ("Support",support,false,false)
        };
        var paths = new (string Url,Guid Id,string TargetName)[]
        {
            (containerPath,containerId,"containerId"),
            (voyagePath,voyageId,"voyageId")
        };
        foreach (var (role,client,view,manage) in roles)
        foreach (var (url,id,targetName) in paths)
        {
            using (var list = await client.GetAsync(url))
                Assert.Equal(view ? HttpStatusCode.OK : HttpStatusCode.Forbidden,list.StatusCode);
            using (var detail = await client.GetAsync(url+"/"+id))
                Assert.Equal(view ? HttpStatusCode.OK : HttpStatusCode.Forbidden,detail.StatusCode);
            object body = targetName == "containerId"
                ? new {containerId=Guid.NewGuid(),startsAtUtc="2041-01-01T10:00:00Z"}
                : new {voyageId=Guid.NewGuid(),startsAtUtc="2041-01-01T10:00:00Z"};
            using (var post = await ReferenceDataHttpTests.Send(client,HttpMethod.Post,url,body))
                Assert.Equal(manage ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden,
                    post.StatusCode);
            using (var close = await ReferenceDataHttpTests.Send(client,HttpMethod.Patch,url+"/"+id,
                new {expectedVersion=999L,endsAtUtc="2040-01-01T11:00:00Z"}))
                Assert.Equal(manage ? HttpStatusCode.Conflict : HttpStatusCode.Forbidden,
                    close.StatusCode);
        }
    }
}
