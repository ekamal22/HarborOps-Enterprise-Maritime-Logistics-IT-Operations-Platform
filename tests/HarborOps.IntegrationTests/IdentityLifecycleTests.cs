using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class IdentityLifecycleTests
{
    private const string Password = "Synthetic-integration-passphrase-123";
    private sealed class IdentityApiFactory(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:HarborOps", connection);
            builder.UseSetting("Identity:AllowInsecureLoopbackHttp", "true");
            builder.UseSetting("Identity:KeyRingPath", Path.Combine(Directory.GetCurrentDirectory(), ".identity-keys"));
        }
    }

    [Fact]
    public async Task IdentitySchemaProvisionLoginRevocationAndDisablement()
    {
        var connection = TestDatabase.ConnectionString();
        using var factory = new IdentityApiFactory(connection);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var store = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var email = "m1-" + Guid.NewGuid().ToString("N") + "@example.test";
        await store.ProvisionAsync(email, "Integration Operator", Password, CancellationToken.None);

        var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();
        var hash = await db.UserCredentials.AsNoTracking()
            .Where(x => x.UserId == db.AuthUsers.Single(y => y.Email == email).Id)
            .Select(x => x.PasswordHash).SingleAsync();
        Assert.StartsWith("argon2id$v=19$m=65536,t=3,p=1$", hash);
        Assert.True(await hasher.VerifyAsync(Password, hash));
        Assert.False(await hasher.VerifyAsync("wrong-credential", hash));

        using var client = factory.CreateClient();
        var anonymous = await client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var token = await GetCsrfAsync(client);
        using var missingCsrf = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Forbidden, missingCsrf.StatusCode);
        using var wrong = await LoginAsync(client, token, email, "not-a-correct-password", "http://localhost");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        using var forged = await LoginAsync(client, token, email, Password, "https://foreign.invalid");
        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);
        using var login = await LoginAsync(client, token, email, Password, "http://localhost");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains("HttpOnly", login.Headers.GetValues("Set-Cookie").Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/session/guard")).StatusCode);
        var session = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/session");
        Assert.Equal(email, session.GetProperty("user").GetProperty("email").GetString());

        var current = await store.LoginAsync(email, Password, "integration", CancellationToken.None);
        Assert.NotNull(current);
        await store.RevokeAllAsync(current.UserId, "logout_all", "integration", CancellationToken.None);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/auth/session/guard")).StatusCode);
        Assert.Null(await store.ValidateAsync(current.UserId, current.SessionId,
            current.SecurityVersion, CancellationToken.None));

        var second = await store.LoginAsync(email, Password, "integration", CancellationToken.None);
        Assert.NotNull(second);
        Assert.True(await store.DisableSelfAsync(second.UserId, Password, "integration", CancellationToken.None));
        Assert.Null(await store.LoginAsync(email, Password, "integration", CancellationToken.None));
        Assert.Null(await store.ValidateAsync(second.UserId, second.SessionId,
            second.SecurityVersion, CancellationToken.None));
    }

    [Fact]
    public async Task PasswordResetRevokesOldSessions()
    {
        using var factory = new IdentityApiFactory(TestDatabase.ConnectionString());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var store = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var email = "reset-" + Guid.NewGuid().ToString("N") + "@example.test";
        await store.ProvisionAsync(email, "Reset Operator", Password, CancellationToken.None);
        var old = await store.LoginAsync(email, Password, "integration", CancellationToken.None);
        Assert.NotNull(old);
        var nextPassword = "Another-strong-synthetic-passphrase-123";
        await store.ResetPasswordAsync(email, nextPassword, CancellationToken.None);
        Assert.Null(await store.ValidateAsync(old.UserId, old.SessionId, old.SecurityVersion, CancellationToken.None));
        Assert.Null(await store.LoginAsync(email, Password, "integration", CancellationToken.None));
        Assert.NotNull(await store.LoginAsync(email, nextPassword, "integration", CancellationToken.None));
    }

    private static async Task<string> GetCsrfAsync(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        return json.GetProperty("token").GetString()!;
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string csrf,
        string email, string password, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login");
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF", csrf);
        request.Content = JsonContent.Create(new { email, password });
        return client.SendAsync(request);
    }
}
