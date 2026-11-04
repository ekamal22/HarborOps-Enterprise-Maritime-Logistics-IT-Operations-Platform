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

public sealed class AuditHttpTests
{
    private const string Password = "Synthetic-audit-http-passphrase-123";

    private sealed class AuditFactory(string connection) : WebApplicationFactory<Program>
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
    public async Task AuditorOnlyTenantScopedReadAndRevocationAreEnforced()
    {
        using var factory = new AuditFactory(TestDatabase.ConnectionString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identities = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var organizations = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var token = Guid.NewGuid().ToString("N");
        var adminEmail = "audit-admin-" + token + "@example.test";
        var auditorEmail = "audit-reader-" + token + "@example.test";
        await identities.ProvisionAsync(adminEmail, "Synthetic Administrator", Password, CancellationToken.None);
        await identities.ProvisionAsync(auditorEmail, "Synthetic Auditor", Password, CancellationToken.None);
        var orgA = await organizations.BootstrapAsync("audit-a-" + token[..12],
            "Audit A", adminEmail, CancellationToken.None);
        var orgB = await organizations.BootstrapAsync("audit-b-" + token[..12],
            "Audit B", adminEmail, CancellationToken.None);
        var auditorId = await db.AuthUsers.AsNoTracking().Where(u => u.Email == auditorEmail)
            .Select(u => u.Id).SingleAsync();
        var session = (await identities.LoginAsync(adminEmail, Password, "m16-setup", CancellationToken.None))!;
        Assert.NotNull((await organizations.SelectAsync(session.UserId, session.SessionId,
            orgA.Id, CancellationToken.None)).Value);
        var grant = await organizations.PutMemberAsync(session.UserId, session.SessionId,
            orgA.Id, auditorId, "Auditor", "Active", null, "m16-grant", CancellationToken.None);
        Assert.NotNull(grant.Value);

        using var administrator = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        using var auditor = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await LoginAsync(administrator, adminEmail);
        await LoginAsync(auditor, auditorEmail);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(administrator, HttpMethod.Put,
            "/api/v1/auth/session/organization", new { organizationId = orgA.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(auditor, HttpMethod.Put,
            "/api/v1/auth/session/organization", new { organizationId = orgA.Id })).StatusCode);

        var route = "/api/v1/organizations/" + orgA.Id + "/audit-events";
        Assert.Equal(HttpStatusCode.Forbidden, (await administrator.GetAsync(route)).StatusCode);
        using var allowed = await auditor.GetAsync(route + "?limit=25");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        using var body = JsonDocument.Parse(await allowed.Content.ReadAsStringAsync());
        var entries = body.RootElement.GetProperty("entries");
        Assert.True(entries.GetArrayLength() > 0);
        foreach (var entry in entries.EnumerateArray())
        {
            Assert.Equal(orgA.Id.ToString("D"),
                entry.GetProperty("organizationId").GetString());
            Assert.False(entry.TryGetProperty("actorSessionId", out _));
        }
        var firstId = entries[0].GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.OK,
            (await auditor.GetAsync(route + "/" + firstId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await auditor.GetAsync("/api/v1/organizations/" + orgB.Id + "/audit-events")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await auditor.GetAsync(route + "?limit=1001")).StatusCode);

        var revoke = await organizations.RemoveMemberAsync(session.UserId, session.SessionId,
            orgA.Id, auditorId, 1, "m16-revoke", CancellationToken.None);
        Assert.Equal(OrganizationAccessError.None, revoke);
        Assert.Equal(HttpStatusCode.Conflict, (await auditor.GetAsync(route)).StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        using var response = await SendAsync(client, HttpMethod.Post,
            "/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method,
        string url, object payload)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        using var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
        request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",
            csrf.GetProperty("token").GetString());
        request.Content = JsonContent.Create(payload);
        return await client.SendAsync(request);
    }
}
