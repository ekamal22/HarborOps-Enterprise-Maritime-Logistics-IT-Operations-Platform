using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class IdentitySecurityTests
{
    private const string Password = "Synthetic-integration-security-passphrase";
    private sealed class AdjustableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset current = start;
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan delta) => current = current.Add(delta);
    }

    private sealed class IdentityApiFactory(string connection) : WebApplicationFactory<Program>
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

    [Fact]
    public async Task IdleAndAbsoluteExpirationCannotBeRevived()
    {
        using var factory = new IdentityApiFactory(TestDatabase.ConnectionString());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var now = new AdjustableClock(DateTimeOffset.UtcNow);
        var store = new IdentityStore(db, scope.ServiceProvider.GetRequiredService<PasswordHasher>(), now);
        var email = "expiry-" + Guid.NewGuid().ToString("N") + "@example.test";
        await store.ProvisionAsync(email, "Expiry Operator", Password, CancellationToken.None);

        var idle = await store.LoginAsync(email, Password, "integration", CancellationToken.None);
        Assert.NotNull(idle);
        Assert.NotNull(await store.ValidateAsync(idle.UserId, idle.SessionId, idle.SecurityVersion,
            CancellationToken.None));
        now.Advance(TimeSpan.FromMinutes(31));
        Assert.Null(await store.ValidateAsync(idle.UserId, idle.SessionId, idle.SecurityVersion,
            CancellationToken.None));

        var absolute = await store.LoginAsync(email, Password, "integration", CancellationToken.None);
        Assert.NotNull(absolute);
        // Keep idle expiration extended to isolate the absolute timeout boundary.
        await db.AuthSessions.Where(x => x.Id == absolute.SessionId).ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.IdleExpiresAtUtc, absolute.AbsoluteExpiry));
        now.Advance(TimeSpan.FromHours(12));
        Assert.Null(await store.ValidateAsync(absolute.UserId, absolute.SessionId,
            absolute.SecurityVersion, CancellationToken.None));
    }

    [Fact]
    public async Task SelfDisableEndpointRevokesSessionAndRejectsFutureLogin()
    {
        using var factory = new IdentityApiFactory(TestDatabase.ConnectionString());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var store = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var email = "disable-" + Guid.NewGuid().ToString("N") + "@example.test";
        await store.ProvisionAsync(email, "Disable Operator", Password, CancellationToken.None);

        using var client = factory.CreateClient();
        var token = await GetCsrfAsync(client);
        using var login = await MutationAsync(client, "/api/v1/auth/login", token,
            new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/api/v1/auth/session/guard")).StatusCode);

        token = await GetCsrfAsync(client);
        using var wrong = await MutationAsync(client, "/api/v1/auth/account/disable-self",
            token, new { password = "incorrect-password" });
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/api/v1/auth/session/guard")).StatusCode);

        token = await GetCsrfAsync(client);
        using var disabled = await MutationAsync(client, "/api/v1/auth/account/disable-self",
            token, new { password = Password });
        Assert.Equal(HttpStatusCode.NoContent, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/auth/session/guard")).StatusCode);
        Assert.Null(await store.LoginAsync(email, Password, "integration", CancellationToken.None));
    }

    [Fact]
    public async Task M0SeedUpgradesToM1WithoutLosingBaseline()
    {
        var baseConnection = TestDatabase.ConnectionString();
        await using (var setup = new NpgsqlConnection(baseConnection))
        {
            await setup.OpenAsync();
            await using var create = new NpgsqlCommand(
                "CREATE SCHEMA IF NOT EXISTS harborops_m1_upgrade_test", setup);
            await create.ExecuteNonQueryAsync();
        }
        var builder = new NpgsqlConnectionStringBuilder(baseConnection);
        builder["Search Path"] = "harborops_m1_upgrade_test";
        var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
            .UseNpgsql(builder.ConnectionString).Options;
        await using var db = new HarborOpsDbContext(options);
        if (!(await db.Database.GetAppliedMigrationsAsync()).Any())
        {
            await db.Database.MigrateAsync("20261030170000_InitialBootstrap");
            await BootstrapSeeder.SeedAsync(db, CancellationToken.None);
            Assert.Equal(1, await db.BootstrapRecords.AsNoTracking().CountAsync());
        }
        await db.Database.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal("m0-synthetic-v1",
            (await db.BootstrapRecords.AsNoTracking().SingleAsync()).DatasetVersion);
        Assert.Empty(await db.AuthUsers.AsNoTracking().ToListAsync());
        await db.Database.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    private static async Task<string> GetCsrfAsync(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        return json.GetProperty("token").GetString()!;
    }

    private static async Task<HttpResponseMessage> MutationAsync(
        HttpClient client, string path, string csrf, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
        request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF", csrf);
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
