using System.Net;
using System.Net.Http.Json;
using System.Text;
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

public sealed class OrganizationHttpLifecycleTests
{
    private const string Password = "Synthetic-G2-HTTP-lifecycle-passphrase-123";

    private sealed class ApiFactory(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:HarborOps", connection);
            builder.UseSetting("Identity:AllowInsecureLoopbackHttp", "true");
            builder.UseSetting("Identity:KeyRingPath",
                Path.Combine(Directory.GetCurrentDirectory(), ".identity-keys"));
        }
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method,
        string path, object payload)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        using var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",
            csrf.GetProperty("token").GetString());
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
        request.Content = JsonContent.Create(payload);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task RevokedAndDisabledCookiesCannotReadOrganizationEndpoints()
    {
        using var factory = new ApiFactory(TestDatabase.ConnectionString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var organizations = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var adminEmail = "g2-cookie-owner-" + nonce + "@example.test";
        var memberEmail = "g2-cookie-member-" + nonce + "@example.test";
        await identity.ProvisionAsync(adminEmail, "Owner", Password, CancellationToken.None);
        await identity.ProvisionAsync(memberEmail, "Member", Password, CancellationToken.None);
        var org = await organizations.BootstrapAsync("g2-cookie-" + nonce[..10],
            "Cookie Authorization", adminEmail, CancellationToken.None);
        using var administrator = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var member = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using (var login = await Send(administrator, HttpMethod.Post, "/api/v1/auth/login",
            new { email = adminEmail, password = Password }))
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using (var login = await Send(member, HttpMethod.Post, "/api/v1/auth/login",
            new { email = memberEmail, password = Password }))
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var uid = await db.AuthUsers.AsNoTracking()
            .Where(x => x.NormalizedEmail == IdentityStore.NormalizeEmail(memberEmail))
            .Select(x => x.Id).SingleAsync();
        var adminSession = await db.AuthSessions.AsNoTracking()
            .Where(x => x.UserId == (db.AuthUsers.Where(u =>
                u.NormalizedEmail == IdentityStore.NormalizeEmail(adminEmail)).Select(u => u.Id).Single()))
            .OrderByDescending(x => x.IssuedAtUtc).Select(x => x.Id).FirstAsync();
        var administratorId = (await db.AuthUsers.AsNoTracking()
            .SingleAsync(x => x.NormalizedEmail == IdentityStore.NormalizeEmail(adminEmail))).Id;
        Assert.NotNull((await organizations.SelectAsync(administratorId, adminSession,
            org.Id, CancellationToken.None)).Value);
        var memberAdded = await organizations.PutMemberAsync(administratorId, adminSession,
            org.Id, uid, "Support", "Active", null, "g2-http", CancellationToken.None);
        Assert.NotNull(memberAdded.Value);
        using (var selected = await Send(member, HttpMethod.Put,
            "/api/v1/auth/session/organization", new { organizationId = org.Id }))
            Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await member.GetAsync("/api/v1/organizations/" + org.Id)).StatusCode);
        await identity.RevokeAllAsync(uid, "logout_all", "g2-http", CancellationToken.None);
        foreach (var path in new[] {
            "/api/v1/me/organizations", "/api/v1/me/organization-context",
            "/api/v1/organizations/" + org.Id,
            "/api/v1/organizations/" + org.Id + "/memberships"
        })
        {
            using var response = await member.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(memberEmail, body, StringComparison.OrdinalIgnoreCase);
        }
        using (var loginAgain = await Send(member, HttpMethod.Post,
            "/api/v1/auth/login", new { email = memberEmail, password = Password }))
            Assert.Equal(HttpStatusCode.OK, loginAgain.StatusCode);
        Assert.True(await identity.DisableSelfAsync(uid, Password, "g2-http", CancellationToken.None));
        using var disabledResult = await member.GetAsync("/api/v1/me/organizations");
        Assert.Equal(HttpStatusCode.Unauthorized, disabledResult.StatusCode);
        using var missing = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await missing.GetAsync("/api/v1/me/organizations")).StatusCode);
    }

    [Fact]
    public async Task MalformedRequestsAndForeignIdsNeverLeakOrganizationData()
    {
        using var factory = new ApiFactory(TestDatabase.ConnectionString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var organizations = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var email = "g2-errors-" + nonce + "@example.test";
        var foreignEmail = "g2-errors-foreign-" + nonce + "@example.test";
        await identity.ProvisionAsync(email, "Admin A", Password, CancellationToken.None);
        await identity.ProvisionAsync(foreignEmail, "Admin B", Password, CancellationToken.None);
        var a = await organizations.BootstrapAsync("g2-errors-a-" + nonce[..10],
            "A Private Name", email, CancellationToken.None);
        var b = await organizations.BootstrapAsync("g2-errors-b-" + nonce[..10],
            "B Private Name", foreignEmail, CancellationToken.None);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using (var login = await Send(client, HttpMethod.Post, "/api/v1/auth/login",
            new { email, password = Password }))
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using (var selected = await Send(client, HttpMethod.Put,
            "/api/v1/auth/session/organization", new { organizationId = a.Id }))
            Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        var foreignPaths = new[] {
            (HttpMethod.Get, "/api/v1/organizations/" + b.Id),
            (HttpMethod.Get, "/api/v1/organizations/" + b.Id + "/memberships"),
            (HttpMethod.Get, "/api/v1/organizations/" + b.Id + "/memberships/" + Guid.NewGuid())
        };
        foreach (var (method, path) in foreignPaths)
        {
            using var response = await client.SendAsync(new HttpRequestMessage(method, path));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("B Private Name", body);
            Assert.DoesNotContain("Npgsql", body);
            Assert.DoesNotContain("PostgresException", body);
            Assert.True(body.Length < 512, "Error envelope must be bounded");
        }
        using (var changed = await Send(client, HttpMethod.Patch,
            "/api/v1/organizations/" + b.Id,
            new { displayName = "Bad", expectedVersion = 1L }))
            Assert.Equal(HttpStatusCode.NotFound, changed.StatusCode);
        using (var changed = await Send(client, HttpMethod.Put,
            "/api/v1/organizations/" + b.Id + "/memberships/" + Guid.NewGuid(),
            new { role = "Analyst", status = "Active", expectedVersion = (long?)null }))
            Assert.Equal(HttpStatusCode.NotFound, changed.StatusCode);
        using (var changed = await Send(client, HttpMethod.Delete,
            "/api/v1/organizations/" + b.Id + "/memberships/" + Guid.NewGuid(),
            new { expectedVersion = 1L }))
            Assert.Equal(HttpStatusCode.NotFound, changed.StatusCode);
        foreach (var input in new object[] {
            new { displayName = "", expectedVersion = 1L },
            new { displayName = " ", expectedVersion = 1L },
            new { displayName = "Valid", expectedVersion = 0L }
        })
        {
            using var rejected = await Send(client, HttpMethod.Patch,
                "/api/v1/organizations/" + a.Id, input);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        using var malformed = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/organizations/" + a.Id);
        malformed.Headers.TryAddWithoutValidation("Origin", "http://localhost");
        malformed.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",
            csrf.GetProperty("token").GetString());
        malformed.Content = new StringContent("{", Encoding.UTF8, "application/json");
        using var invalidJson = await client.SendAsync(malformed);
        Assert.Equal(HttpStatusCode.BadRequest, invalidJson.StatusCode);
        Assert.Equal("A Private Name",
            (await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == a.Id)).DisplayName);
        Assert.Equal("B Private Name",
            (await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == b.Id)).DisplayName);
    }
}
