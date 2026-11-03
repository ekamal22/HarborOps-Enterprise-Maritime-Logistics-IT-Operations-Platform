using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class OrganizationHttpTests
{
    private const string Password = "Synthetic-organization-http-passphrase-123";

    private sealed class OrganizationApiFactory(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            LocalPeerTestServer.Configure(builder);
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:HarborOps", connection);
            builder.UseSetting("Identity:AllowInsecureLoopbackHttp", "true");
            builder.UseSetting("Identity:KeyRingPath",
                Path.Combine(Directory.GetCurrentDirectory(), ".identity-keys"));
        }
    }

    [Fact]
    public async Task HttpPipelineEnforcesSelectedOrganizationAndMemberRole()
    {
        using var factory = new OrganizationApiFactory(TestDatabase.ConnectionString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var organizations = scope.ServiceProvider.GetRequiredService<OrganizationStore>();

        var id = Guid.NewGuid().ToString("N");
        var aEmail = "admin-a-" + id + "@example.test";
        var bEmail = "admin-b-" + id + "@example.test";
        var workerEmail = "worker-" + id + "@example.test";
        await identity.ProvisionAsync(aEmail, "Admin A", Password, CancellationToken.None);
        await identity.ProvisionAsync(bEmail, "Admin B", Password, CancellationToken.None);
        await identity.ProvisionAsync(workerEmail, "Worker", Password, CancellationToken.None);
        var a = await organizations.BootstrapAsync("a-" + id[..12], "Org A", aEmail, CancellationToken.None);
        var b = await organizations.BootstrapAsync("b-" + id[..12], "Org B", bEmail, CancellationToken.None);
        using var admin = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var worker = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await worker.GetAsync("/api/v1/me/organizations")).StatusCode);

        await LoginAsync(admin, aEmail);
        await LoginAsync(worker, workerEmail);

        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.GetAsync("/api/v1/organizations/" + a.Id)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound,
            (await SendAsync(admin, HttpMethod.Put, "/api/v1/auth/session/organization",
                new { organizationId = b.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await SendAsync(admin, HttpMethod.Put, "/api/v1/auth/session/organization",
                new { organizationId = a.Id })).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync("/api/v1/organizations/" + b.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync("/api/v1/organizations/" + b.Id + "/memberships")).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await admin.GetAsync("/api/v1/organizations/" + a.Id + "/memberships")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(admin, HttpMethod.Delete, "/api/v1/organizations/" + a.Id +
                "/memberships/" + (await identity.LoginAsync(aEmail, Password, "test", CancellationToken.None))!.UserId,
                new { expectedVersion = 1L })).StatusCode);

        var workerId = (await identity.LoginAsync(workerEmail, Password, "test", CancellationToken.None))!.UserId;
        var created = await SendAsync(admin, HttpMethod.Put,
            "/api/v1/organizations/" + a.Id + "/memberships/" + workerId,
            new { role = "Auditor", status = "Active", expectedVersion = (long?)null });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await SendAsync(worker, HttpMethod.Put, "/api/v1/auth/session/organization",
                new { organizationId = a.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await worker.GetAsync("/api/v1/organizations/" + a.Id + "/memberships")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await worker.GetAsync("/api/v1/organizations/" + a.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await worker.GetAsync("/api/v1/organizations/" + b.Id)).StatusCode);

        var blockedOrigin = new HttpRequestMessage(HttpMethod.Put, "/api/v1/auth/session/organization");
        blockedOrigin.Headers.TryAddWithoutValidation("Origin", "https://foreign.example.test");
        blockedOrigin.Content = JsonContent.Create(new { organizationId = a.Id });
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.SendAsync(blockedOrigin)).StatusCode);

        var removed = await SendAsync(admin, HttpMethod.Delete,
            "/api/v1/organizations/" + a.Id + "/memberships/" + workerId,
            new { expectedVersion = 1L });
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await worker.GetAsync("/api/v1/organizations/" + a.Id)).StatusCode);
        var mine = await worker.GetFromJsonAsync<JsonElement>("/api/v1/me/organizations");
        Assert.Empty(mine.GetProperty("organizations").EnumerateArray());
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        using var result = await SendAsync(client, HttpMethod.Post, "/api/v1/auth/login",
            new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method,
        string path, object payload)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        using var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
        request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF", token.GetProperty("token").GetString());
        request.Content = JsonContent.Create(payload);
        return await client.SendAsync(request);
    }
}
