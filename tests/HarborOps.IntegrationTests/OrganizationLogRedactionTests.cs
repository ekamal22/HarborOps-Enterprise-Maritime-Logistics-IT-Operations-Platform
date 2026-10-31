using System.Collections.Concurrent;
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
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

/// <summary>
/// G2-S23: failing, forbidden, malformed and dependency-unavailable organization requests must
/// never place credentials, session or antiforgery material, account identifiers, or provider
/// details in responses or service logs.
/// </summary>
public sealed class OrganizationLogRedactionTests
{
    private const string Password = "Synthetic-G2-redaction-passphrase-123";
    private const string ConnectionSecret = "synthetic-connection-secret-7f3a91";

    private sealed class CapturingProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Capture(this, categoryName);
        public void Dispose() { }

        private sealed class Capture(CapturingProvider owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner.Entries.Enqueue(category + "|" + logLevel + "|" +
                    formatter(state, exception) + "|" + exception);
        }
    }

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

    private static async Task<HttpResponseMessage> Send(HttpClient client, ICollection<string> secrets,
        HttpMethod method, string path, object? body, bool csrf = true,
        string origin = "http://localhost", string? rawBody = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Origin", origin);
        if (csrf)
        {
            using var tokenResponse = await client.GetAsync("/api/v1/auth/csrf");
            tokenResponse.EnsureSuccessStatusCode();
            var token = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("token").GetString()!;
            secrets.Add(token);
            request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF", token);
        }
        if (rawBody is not null) request.Content = new StringContent(rawBody, Encoding.UTF8, "application/json");
        else if (body is not null) request.Content = JsonContent.Create(body);
        var response = await client.SendAsync(request);
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            foreach (var cookie in cookies)
            {
                var pair = cookie.Split(';', 2)[0];
                var separator = pair.IndexOf('=');
                if (separator > 0 && pair.Length - separator > 16) secrets.Add(pair[(separator + 1)..]);
            }
        return response;
    }

    [Fact]
    public async Task RejectedRequestsAndDependencyFailuresNeverLeakSecretsToResponsesOrLogs()
    {
        var logs = new CapturingProvider();
        var secrets = new List<string> { Password };
        var responses = new List<string>();
        var nonce = Guid.NewGuid().ToString("N");
        var adminEmail = "g2-redact-admin-" + nonce + "@example.test";
        var otherEmail = "g2-redact-other-" + nonce + "@example.test";
        var observedValues = new List<string> { adminEmail, otherEmail, "Redaction B Private" };
        string sessionCookie;
        Guid organizationId;

        using (var factory = new ApiFactory(TestDatabase.ConnectionString())
            .WithWebHostBuilder(builder => builder.ConfigureServices(
                services => services.AddSingleton<ILoggerProvider>(logs))))
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>().Database.MigrateAsync();
            var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
            var store = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
            await identity.ProvisionAsync(adminEmail, "Redaction Admin", Password, CancellationToken.None);
            await identity.ProvisionAsync(otherEmail, "Redaction Other", Password, CancellationToken.None);
            var a = await store.BootstrapAsync("g2-redact-a-" + nonce[..10], "Redaction A", adminEmail,
                CancellationToken.None);
            var b = await store.BootstrapAsync("g2-redact-b-" + nonce[..10], "Redaction B Private",
                otherEmail, CancellationToken.None);
            organizationId = a.Id;

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
            async Task Record(HttpResponseMessage response, params HttpStatusCode[] allowed)
            {
                using (response)
                {
                    var text = await response.Content.ReadAsStringAsync();
                    responses.Add(text);
                    Assert.Contains(response.StatusCode, allowed);
                }
            }

            // Failed credentials, rejected transport input and a truncated login body that
            // contains the password.
            await Record(await Send(client, secrets, HttpMethod.Post, "/api/v1/auth/login",
                new { email = adminEmail, password = Password + "-wrong" }), HttpStatusCode.Unauthorized);
            await Record(await Send(client, secrets, HttpMethod.Post, "/api/v1/auth/login",
                null, rawBody: "{\"email\":\"" + adminEmail + "\",\"password\":\"" + Password + "\""),
                HttpStatusCode.BadRequest);
            await Record(await Send(client, secrets, HttpMethod.Post, "/api/v1/auth/login",
                new { email = adminEmail, password = Password }, csrf: false), HttpStatusCode.Forbidden);
            using (var login = await Send(client, secrets, HttpMethod.Post, "/api/v1/auth/login",
                new { email = adminEmail, password = Password }))
            {
                Assert.Equal(HttpStatusCode.OK, login.StatusCode);
                sessionCookie = login.Headers.GetValues("Set-Cookie")
                    .Select(x => x.Split(';', 2)[0])
                    .Single(x => x.StartsWith("HarborOps.Session=", StringComparison.Ordinal));
            }
            secrets.Add(sessionCookie["HarborOps.Session=".Length..]);

            await Record(await Send(client, secrets, HttpMethod.Put, "/api/v1/auth/session/organization",
                new { organizationId = a.Id }), HttpStatusCode.OK);
            await Record(await client.GetAsync("/api/v1/organizations/" + b.Id), HttpStatusCode.NotFound);
            await Record(await client.GetAsync("/api/v1/organizations/" + b.Id + "/memberships"),
                HttpStatusCode.NotFound);
            await Record(await Send(client, secrets, HttpMethod.Patch, "/api/v1/organizations/" + a.Id,
                new { displayName = "Redaction A", expectedVersion = 1L }, origin: "https://foreign.example.test"),
                HttpStatusCode.Forbidden);
            await Record(await Send(client, secrets, HttpMethod.Patch, "/api/v1/organizations/" + a.Id,
                new { displayName = "Redaction A", expectedVersion = 1L }, csrf: false), HttpStatusCode.Forbidden);
            await Record(await Send(client, secrets, HttpMethod.Patch, "/api/v1/organizations/" + a.Id,
                null, rawBody: "{\"displayName\":\"" + Password + "\""), HttpStatusCode.BadRequest);
            await Record(await Send(client, secrets, HttpMethod.Patch, "/api/v1/organizations/" + a.Id,
                new { displayName = "Redaction A", expectedVersion = 99L }), HttpStatusCode.Conflict);
            await Record(await Send(client, secrets, HttpMethod.Put,
                "/api/v1/organizations/" + a.Id + "/memberships/" + Guid.NewGuid(),
                new { role = "Analyst", status = "Active", expectedVersion = (long?)null }), HttpStatusCode.NotFound);
            await Record(await Send(client, secrets, HttpMethod.Delete,
                "/api/v1/organizations/" + b.Id + "/memberships/" + Guid.NewGuid(),
                new { expectedVersion = 1L }), HttpStatusCode.NotFound);
            await Record(await Send(client, secrets, HttpMethod.Put, "/api/v1/auth/session/organization",
                new { organizationId = b.Id }), HttpStatusCode.NotFound);
        }

        // A separate application instance with an unreachable database accepts the same valid
        // session cookie (shared key ring) and must fail closed without provider details.
        var unreachable = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1", Port = 1, Database = "harborops_integration",
            Username = "harborops", Password = ConnectionSecret, Timeout = 2, Pooling = false
        }.ConnectionString;
        using (var degraded = new ApiFactory(unreachable)
            .WithWebHostBuilder(builder => builder.ConfigureServices(
                services => services.AddSingleton<ILoggerProvider>(logs))))
        using (var client = degraded.CreateClient())
        {
            secrets.Add(ConnectionSecret);
            foreach (var path in new[] {
                "/api/v1/me/organizations", "/api/v1/me/organization-context",
                "/api/v1/organizations/" + organizationId,
                "/api/v1/organizations/" + organizationId + "/memberships" })
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, path);
                request.Headers.TryAddWithoutValidation("Cookie", sessionCookie);
                using var response = await client.SendAsync(request);
                var text = await response.Content.ReadAsStringAsync();
                responses.Add(text);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
                Assert.Contains("dependency_unavailable", text, StringComparison.Ordinal);
                Assert.True(text.Length < 512, "Error envelope must be bounded");
                foreach (var forbidden in new[] { "Npgsql", "127.0.0.1", "Exception", "SELECT", "   at " })
                    Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
            }
        }

        var entries = logs.Entries.ToArray();
        Assert.NotEmpty(entries);
        // The dependency-failure exception is logged, so the secret checks below inspect it.
        Assert.Contains(entries, x => x.Contains("IdentityDependencyException", StringComparison.Ordinal));
        foreach (var secret in secrets.Distinct())
        {
            Assert.DoesNotContain(entries, x => x.Contains(secret, StringComparison.Ordinal));
            Assert.DoesNotContain(responses, x => x.Contains(secret, StringComparison.Ordinal));
        }
        foreach (var value in observedValues)
        {
            Assert.DoesNotContain(entries, x => x.Contains(value, StringComparison.Ordinal));
            Assert.DoesNotContain(responses, x => x.Contains(value, StringComparison.Ordinal));
        }
    }
}
