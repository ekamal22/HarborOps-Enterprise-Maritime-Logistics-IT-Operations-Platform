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

public sealed class OrganizationAdversarialHttpTests
{
    private const string Password = "Synthetic-g2-http-adversarial-passphrase-123";
    private const string Correlation = "g2-http";

    private sealed class ApiFactory(string connection) : WebApplicationFactory<Program>
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

    private static async Task<HttpResponseMessage> Mutate(HttpClient client, HttpMethod verb,
        string path, object? payload, bool csrf = true, string origin = "http://localhost")
    {
        using var request = new HttpRequestMessage(verb, path);
        request.Headers.TryAddWithoutValidation("Origin", origin);
        if (csrf)
        {
            using var tokenResponse = await client.GetAsync("/api/v1/auth/csrf");
            tokenResponse.EnsureSuccessStatusCode();
            var token = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
            request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",
                token.GetProperty("token").GetString());
        }
        if (payload is not null) request.Content = JsonContent.Create(payload);
        return await client.SendAsync(request);
    }

    private static async Task Login(HttpClient client, string email)
    {
        using var result = await Mutate(client, HttpMethod.Post, "/api/v1/auth/login",
            new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    private static async Task Select(HttpClient client, Guid id)
    {
        using var result = await Mutate(client, HttpMethod.Put,
            "/api/v1/auth/session/organization", new { organizationId = id });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    [Fact]
    public async Task AllNonAdministratorsAreDeniedAdministrationAndForeignIds()
    {
        using var factory = new ApiFactory(TestDatabase.ConnectionString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var store = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var adminEmail = "g2-admin-" + nonce + "@example.test";
        var foreignEmail = "g2-foreign-" + nonce + "@example.test";
        await identity.ProvisionAsync(adminEmail, "Admin", Password, CancellationToken.None);
        await identity.ProvisionAsync(foreignEmail, "Foreign Admin", Password, CancellationToken.None);
        var org = await store.BootstrapAsync("g2-http-a-" + nonce[..12],
            "Organization A", adminEmail, CancellationToken.None);
        var foreign = await store.BootstrapAsync("g2-http-b-" + nonce[..12],
            "Organization B", foreignEmail, CancellationToken.None);
        var foreignId = (await db.AuthUsers.AsNoTracking()
            .SingleAsync(x => x.NormalizedEmail == IdentityStore.NormalizeEmail(foreignEmail))).Id;
        using var admin = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await Login(admin, adminEmail);
        await Select(admin, org.Id);

        foreach (var role in new[] { "Analyst", "Manager", "Support", "Auditor" })
        {
            var email = "g2-" + role.ToLowerInvariant() + "-" + nonce + "@example.test";
            await identity.ProvisionAsync(email, role, Password, CancellationToken.None);
            var user = await db.AuthUsers.AsNoTracking()
                .SingleAsync(x => x.NormalizedEmail == IdentityStore.NormalizeEmail(email));
            using (var grant = await Mutate(admin, HttpMethod.Put,
                "/api/v1/organizations/" + org.Id + "/memberships/" + user.Id,
                new { role, status = "Active", expectedVersion = (long?)null }))
                Assert.Equal(HttpStatusCode.OK, grant.StatusCode);

            using var member = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
            await Login(member, email);
            await Select(member, org.Id);
            Assert.Equal(HttpStatusCode.OK,
                (await member.GetAsync("/api/v1/organizations/" + org.Id)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await member.GetAsync("/api/v1/organizations/" + org.Id + "/memberships")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await member.GetAsync("/api/v1/organizations/" + org.Id + "/memberships/" + user.Id)).StatusCode);
            using var profileChange = await Mutate(member, HttpMethod.Patch,
                "/api/v1/organizations/" + org.Id,
                new { displayName = "Unauthorized", expectedVersion = 1L });
            Assert.Equal(HttpStatusCode.Forbidden, profileChange.StatusCode);
            using var roleEscalation = await Mutate(member, HttpMethod.Put,
                "/api/v1/organizations/" + org.Id + "/memberships/" + user.Id,
                new { role = "Administrator", status = "Active", expectedVersion = 1L });
            Assert.Equal(HttpStatusCode.Forbidden, roleEscalation.StatusCode);
            using var removal = await Mutate(member, HttpMethod.Delete,
                "/api/v1/organizations/" + org.Id + "/memberships/" + user.Id,
                new { expectedVersion = 1L });
            Assert.Equal(HttpStatusCode.Forbidden, removal.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await member.GetAsync("/api/v1/organizations/" + foreign.Id)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync("/api/v1/organizations/" + org.Id +
                "/memberships/" + foreignId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync("/api/v1/organizations/" + foreign.Id +
                "/memberships/" + foreignId)).StatusCode);
        using (var foreignChange = await Mutate(admin, HttpMethod.Put,
            "/api/v1/organizations/" + foreign.Id + "/memberships/" + foreignId,
            new { role = "Analyst", status = "Active", expectedVersion = 1L }))
            Assert.Equal(HttpStatusCode.NotFound, foreignChange.StatusCode);
        using (var foreignRemoval = await Mutate(admin, HttpMethod.Delete,
            "/api/v1/organizations/" + foreign.Id + "/memberships/" + foreignId,
            new { expectedVersion = 1L }))
            Assert.Equal(HttpStatusCode.NotFound, foreignRemoval.StatusCode);
        using (var invalidRole = await Mutate(admin, HttpMethod.Put,
            "/api/v1/organizations/" + org.Id + "/memberships/" + foreignId,
            new { role = "Administrator ", status = "Active", expectedVersion = (long?)null }))
            Assert.Equal(HttpStatusCode.BadRequest, invalidRole.StatusCode);
        using (var noCsrf = await Mutate(admin, HttpMethod.Patch,
            "/api/v1/organizations/" + org.Id,
            new { displayName = "Bad", expectedVersion = 1L }, csrf: false))
            Assert.Equal(HttpStatusCode.Forbidden, noCsrf.StatusCode);
        using (var foreignOrigin = await Mutate(admin, HttpMethod.Patch,
            "/api/v1/organizations/" + org.Id,
            new { displayName = "Bad", expectedVersion = 1L }, origin: "https://foreign.example.test"))
            Assert.Equal(HttpStatusCode.Forbidden, foreignOrigin.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync("/api/v1/organizations/" + org.Id +
                "/memberships/" + Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task SameAccountSessionsSelectIndependentlyAndSuspensionFailsClosed()
    {
        using var factory = new ApiFactory(TestDatabase.ConnectionString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var store = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var adminAEmail = "g2-owner-a-" + nonce + "@example.test";
        var adminBEmail = "g2-owner-b-" + nonce + "@example.test";
        var memberEmail = "g2-multisession-" + nonce + "@example.test";
        foreach (var email in new[] { adminAEmail, adminBEmail, memberEmail })
            await identity.ProvisionAsync(email, "Synthetic", Password, CancellationToken.None);
        var a = await store.BootstrapAsync("g2-session-a-" + nonce[..10], "Site A",
            adminAEmail, CancellationToken.None);
        var b = await store.BootstrapAsync("g2-session-b-" + nonce[..10], "Site B",
            adminBEmail, CancellationToken.None);
        var memberId = (await db.AuthUsers.AsNoTracking()
            .SingleAsync(x => x.NormalizedEmail == IdentityStore.NormalizeEmail(memberEmail))).Id;
        var adminA = (await identity.LoginAsync(adminAEmail, Password,
            Correlation, CancellationToken.None))!;
        var adminB = (await identity.LoginAsync(adminBEmail, Password,
            Correlation, CancellationToken.None))!;
        Assert.NotNull((await store.SelectAsync(adminA.UserId, adminA.SessionId,
            a.Id, CancellationToken.None)).Value);
        Assert.NotNull((await store.SelectAsync(adminB.UserId, adminB.SessionId,
            b.Id, CancellationToken.None)).Value);
        Assert.NotNull((await store.PutMemberAsync(adminA.UserId, adminA.SessionId,
            a.Id, memberId, "Analyst", "Active", null, Correlation, CancellationToken.None)).Value);
        Assert.NotNull((await store.PutMemberAsync(adminB.UserId, adminB.SessionId,
            b.Id, memberId, "Manager", "Active", null, Correlation, CancellationToken.None)).Value);

        using var first = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var second = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await Login(first, memberEmail);
        await Login(second, memberEmail);
        await Select(first, a.Id);
        await Select(second, b.Id);
        var aContext = await first.GetFromJsonAsync<JsonElement>("/api/v1/me/organization-context");
        var bContext = await second.GetFromJsonAsync<JsonElement>("/api/v1/me/organization-context");
        Assert.Equal(a.Id, aContext.GetProperty("selected").GetProperty("id").GetGuid());
        Assert.Equal(b.Id, bContext.GetProperty("selected").GetProperty("id").GetGuid());
        Assert.Equal("Analyst", aContext.GetProperty("selected").GetProperty("role").GetString());
        Assert.Equal("Manager", bContext.GetProperty("selected").GetProperty("role").GetString());

        await db.Organizations.Where(x => x.Id == b.Id).ExecuteUpdateAsync(
            s => s.SetProperty(o => o.Status, "Suspended"));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await second.GetAsync("/api/v1/me/organization-context")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await second.GetAsync("/api/v1/organizations/" + b.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await first.GetAsync("/api/v1/organizations/" + a.Id)).StatusCode);
        var active = await second.GetFromJsonAsync<JsonElement>("/api/v1/me/organizations");
        Assert.DoesNotContain(active.GetProperty("organizations").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == b.Id);
        using var reselect = await Mutate(second, HttpMethod.Put,
            "/api/v1/auth/session/organization", new { organizationId = b.Id });
        Assert.Equal(HttpStatusCode.NotFound, reselect.StatusCode);
    }
}
