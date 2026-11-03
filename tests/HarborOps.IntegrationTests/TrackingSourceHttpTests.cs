using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class TrackingSourceHttpTests
{
    private static string Event() =>
        "{\"sourceEventId\":\"test-1\",\"family\":\"Equipment\",\"code\":\"GTIN\"," +
        "\"classifier\":\"ACT\",\"occurredAtUtc\":\"2026-11-02T10:00:00Z\"," +
        "\"equipmentReference\":\"SYN-BOX-ABCD\",\"externalSchemaVersion\":\"2.2.0\"}";

    private static async Task<HttpResponseMessage> Send(HttpClient client, string? bearer, string json,
        string? profile = "tnt-2.2-harborops-v1")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/integrations/v1/tracking-events");
        if (bearer is not null) request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", bearer);
        if (profile is not null)
            request.Headers.Add("X-HarborOps-Tracking-Profile", profile);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task AuthenticatedSourceValidatesButNeverAcknowledgesEvent()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var client = factory.CreateClient();
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        using var admin = fixture.Admin;
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
        var token = await store.CreateAsync(fixture.A, "terminal_a", "Synthetic Terminal",
            CancellationToken.None);
        using (var valid = await Send(client, token.Bearer, Event()))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, valid.StatusCode);
            Assert.Equal("no-store", valid.Headers.CacheControl?.ToString());
            var body = await valid.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("intake_unavailable", body.GetProperty("code").GetString());
            Assert.True(body.TryGetProperty("correlationId", out _));
            Assert.True(valid.Headers.RetryAfter is not null);
        }
        using (var missing = await Send(client, null, Event()))
            Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        using (var invalid = await Send(client, token.Bearer + "a", Event()))
            Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        using (var unsupported = await Send(client, token.Bearer, Event(), "unknown-v9"))
            Assert.Equal(HttpStatusCode.UnprocessableEntity, unsupported.StatusCode);
        using (var spoof = await Send(client, token.Bearer,
            Event().Replace("\"externalSchemaVersion\":",
                "\"organizationId\":\"foreign\",\"externalSchemaVersion\":",
                StringComparison.Ordinal)))
            Assert.Equal(HttpStatusCode.UnprocessableEntity, spoof.StatusCode);
        using (var duplicate = await Send(client, token.Bearer,
            Event().Replace("\"sourceEventId\":\"test-1\"",
                "\"sourceEventId\":\"test-1\",\"sourceEventId\":\"test-2\"",
                StringComparison.Ordinal)))
            Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(1, await db.IntegrationCredentials.CountAsync(x => x.SourceId == token.Id));
        Assert.DoesNotContain(db.Model.GetEntityTypes(),
            t => t.GetTableName() is "tracking_events" or "event_inbox" or "outbox");

        await store.RevokeAsync(fixture.A, "terminal_a", token.CredentialId, CancellationToken.None);
        using (var revoked = await Send(client, token.Bearer, Event()))
            Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }

    [Fact]
    public async Task RotationAndDisabledSourceFailClosedAcrossTenants()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        using var admin = fixture.Admin;
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
        var a = await store.CreateAsync(fixture.A, "carrier_a", "Carrier A", CancellationToken.None);
        var b = await store.CreateAsync(fixture.B, "carrier_a", "Carrier B", CancellationToken.None);
        Assert.NotEqual(a.Id, b.Id);
        Assert.NotEqual(a.Bearer, b.Bearer);
        Assert.Equal(fixture.A, (await store.ValidateAsync(a.Bearer, CancellationToken.None))!.OrganizationId);
        Assert.Equal(fixture.B, (await store.ValidateAsync(b.Bearer, CancellationToken.None))!.OrganizationId);
        var next = await store.RotateAsync(fixture.A, "carrier_a", CancellationToken.None);
        Assert.NotEqual(a.CredentialId, next.CredentialId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.RotateAsync(fixture.A, "carrier_a", CancellationToken.None));
        await store.DisableAsync(fixture.A, "carrier_a", CancellationToken.None);
        Assert.Null(await store.ValidateAsync(next.Bearer, CancellationToken.None));
        Assert.Null(await store.ValidateAsync(a.Bearer, CancellationToken.None));
        Assert.NotNull(await store.ValidateAsync(b.Bearer, CancellationToken.None));
    }

    [Fact]
    public async Task DuplicateSourceCodeAndForeignCredentialRevocationAreRejected()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        using var admin = fixture.Admin;
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
        var a = await store.CreateAsync(fixture.A, "feeder_1", "Feeder", CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CreateAsync(fixture.A, "feeder_1", "Duplicate", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.RevokeAsync(fixture.B, "feeder_1", a.CredentialId, CancellationToken.None));
    }
}
