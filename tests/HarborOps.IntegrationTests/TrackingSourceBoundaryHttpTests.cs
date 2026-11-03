using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class TrackingSourceBoundaryHttpTests
{
    private const string Path = "/integrations/v1/tracking-events";
    private const string Valid = """
        {"sourceEventId":"case-1","eventFamily":"Equipment","eventCode":"GTIN",
         "classifier":"ACT","occurredAtUtc":"2026-11-03T08:00:00Z",
         "equipmentReference":"SYN-BOX-ABCD","externalSchemaVersion":"2.2.0"}
        """;

    private static HttpRequestMessage Request(string? credential, string body = Valid,
        string? profile = "tnt-2.2-harborops-v1")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Path);
        if (credential is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        if (profile is not null) request.Headers.TryAddWithoutValidation(
            "X-HarborOps-Tracking-Profile", profile);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return request;
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, string? credential,
        string body = Valid, string? profile = "tnt-2.2-harborops-v1")
    {
        using var request = Request(credential, body, profile);
        return await client.SendAsync(request);
    }

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode expected, string code)
    {
        using (response)
        {
            Assert.Equal(expected, response.StatusCode);
            Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(code, json.GetProperty("code").GetString());
            Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("correlationId").GetString()));
            Assert.DoesNotContain("exception", await response.Content.ReadAsStringAsync(),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task MediaProfilesSizeAndMethodRejectSafely()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var token = (await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
            .CreateAsync(fixture.A, "http_media", "HTTP Media", CancellationToken.None)).Bearer;

        await AssertError(await Send(client, token, Valid, null),
            HttpStatusCode.UnprocessableEntity, "unsupported_tracking_profile");
        await AssertError(await Send(client, token, Valid, "v9"),
            HttpStatusCode.UnprocessableEntity, "unsupported_tracking_profile");
        using (var req = Request(token))
        {
            req.Content!.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json; charset=iso-8859-1");
            await AssertError(await client.SendAsync(req),
                HttpStatusCode.UnsupportedMediaType, "unsupported_media_type");
        }
        using (var req = Request(token))
        {
            req.Content!.Headers.ContentEncoding.Add("gzip");
            await AssertError(await client.SendAsync(req),
                HttpStatusCode.UnsupportedMediaType, "unsupported_media_type");
        }
        using (var req = Request(token))
        {
            req.Content = new StringContent(new string(' ', 16385), Encoding.UTF8, "application/json");
            await AssertError(await client.SendAsync(req),
                HttpStatusCode.RequestEntityTooLarge, "body_too_large");
        }
        using (var req = Request(token))
        {
            var bytes = Encoding.UTF8.GetBytes(new string(' ', 16385));
            req.Content = new StreamContent(new MemoryStream(bytes));
            req.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
            await AssertError(await client.SendAsync(req),
                HttpStatusCode.RequestEntityTooLarge, "body_too_large");
        }
        using var get = await client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, get.StatusCode);
        Assert.Contains("no-store", get.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task BrowserSessionIsNotMachineAuthenticationAndCrossOriginIsDenied()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var token = (await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
            .CreateAsync(fixture.A, "http_session", "HTTP Session", CancellationToken.None)).Bearer;
        await AssertError(await Send(manager, null),
            HttpStatusCode.Unauthorized, "invalid_source_credentials");
        using (var request = Request(token))
        {
            request.Headers.TryAddWithoutValidation("Origin", "https://untrusted.example.test");
            await AssertError(await manager.SendAsync(request), HttpStatusCode.Forbidden, "origin_denied");
        }
        using (var request = Request(token))
        {
            request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
            await AssertError(await manager.SendAsync(request),
                HttpStatusCode.ServiceUnavailable, "intake_unavailable");
        }
        using var denied = await factory.CreateClient().GetAsync(
            "/api/v1/organizations/" + fixture.A + "/ports");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task AuthenticationFailuresAreThrottledBeforeExcessiveDatabaseWork()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var issued = await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
            .CreateAsync(fixture.A, "http_limit", "HTTP Limiter", CancellationToken.None);
        var malformed = IntegrationSourceStore.FormatBearer(issued.CredentialId,
            RandomNumberGenerator.GetBytes(32));
        for (var i = 0; i < 10; i++)
            await AssertError(await Send(client, malformed), HttpStatusCode.Unauthorized,
                "invalid_source_credentials");
        await AssertError(await Send(client, malformed), HttpStatusCode.TooManyRequests, "rate_limited");
        await AssertError(await Send(client, issued.Bearer), HttpStatusCode.ServiceUnavailable,
            "intake_unavailable");
    }

    [Fact]
    public async Task ValidCredentialDeniedAfterSourceDisableOrOrganizationSuspension()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var disabled = await store.CreateAsync(fixture.A, "http_disabled", "Disabled", CancellationToken.None);
        await store.DisableAsync(fixture.A, "http_disabled", CancellationToken.None);
        await AssertError(await Send(client, disabled.Bearer), HttpStatusCode.Unauthorized,
            "invalid_source_credentials");

        var suspended = await store.CreateAsync(fixture.A, "http_suspended", "Suspended", CancellationToken.None);
        await db.Organizations.Where(x => x.Id == fixture.A)
            .ExecuteUpdateAsync(update => update.SetProperty(o => o.Status, "Suspended"));
        await AssertError(await Send(client, suspended.Bearer),
            HttpStatusCode.Forbidden, "source_access_denied");
    }

    [Fact]
    public async Task DuplicateAuthorizationHeaderAndMalformedSourceIdAreRejected()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var issued = await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
            .CreateAsync(fixture.A, "http_headers", "HTTP Headers", CancellationToken.None);
        using (var request = Request(null))
        {
            request.Headers.TryAddWithoutValidation("Authorization",
                ["Bearer " + issued.Bearer, "Bearer " + issued.Bearer]);
            await AssertError(await client.SendAsync(request), HttpStatusCode.Unauthorized,
                "invalid_source_credentials");
        }
        await AssertError(await Send(client, issued.Bearer,
            Valid.Replace("case-1", "case-1\\n", StringComparison.Ordinal)),
            HttpStatusCode.UnprocessableEntity, "invalid_source_event_id");
    }
}
