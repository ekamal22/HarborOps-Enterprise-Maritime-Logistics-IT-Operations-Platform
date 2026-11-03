using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using HarborOps.Domain.Tracking;
using System.Text;
using System.Text.Json;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class TrackingInboxHttpTests
{
    private static string Payload(string id = "m8-accepted-1", string code = "GTIN",
        string timestamp = "2026-11-03T10:00:00Z") =>
        $$"""
        {"sourceEventId":"{{id}}","eventFamily":"Equipment","eventCode":"{{code}}",
         "classifier":"ACT","occurredAtUtc":"{{timestamp}}",
         "equipmentReference":"SYN-BOX-ABCD","externalSchemaVersion":"2.2.0"}
        """;

    private static async Task<HttpResponseMessage> Post(HttpClient client, string bearer, string body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/integrations/v1/tracking-events");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        request.Headers.Add("X-HarborOps-Tracking-Profile", "tnt-2.2-harborops-v1");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task NewReceiptAndCanonicalReplayCommitOneImmutableTriple()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        using var client = factory.CreateClient();
        ProvisionedIntegrationSource source;
        await using (var scope = factory.Services.CreateAsyncScope())
            source = await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
                .CreateAsync(fixture.A, "m8_replay", "Replay Test", CancellationToken.None);

        using var first = await Post(client, source.Bearer, Payload());
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Contains("no-store", first.Headers.CacheControl?.ToString());
        var result = await first.Content.ReadFromJsonAsync<JsonElement>();
        var receiptId = result.GetProperty("receiptId").GetGuid();
        Assert.Equal("received", result.GetProperty("status").GetString());
        Assert.False(result.GetProperty("duplicate").GetBoolean());
        Assert.NotEqual(Guid.Empty, receiptId);
        var recordedAt = result.GetProperty("receivedAtUtc").GetDateTimeOffset();

        using var replay = await Post(client, source.Bearer,
            Payload(timestamp: "2026-11-03T10:00:00+00:00"));
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        var repeat = await replay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(receiptId, repeat.GetProperty("receiptId").GetGuid());
        Assert.True(repeat.GetProperty("duplicate").GetBoolean());
        Assert.Equal(recordedAt, repeat.GetProperty("receivedAtUtc").GetDateTimeOffset());

        await using var verify = factory.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(1, await db.TrackingInboxReceipts.CountAsync(x => x.Id == receiptId));
        Assert.Equal(1, await db.TrackingAcceptedEvents.CountAsync(x => x.ReceiptId == receiptId));
        var pending = await db.TrackingInboxWorkItems.AsNoTracking()
            .SingleAsync(x => x.ReceiptId == receiptId);
        Assert.Equal("Pending", pending.State);
        Assert.Equal(0, pending.AttemptCount);
        var accepted = await db.TrackingAcceptedEvents.AsNoTracking()
            .SingleAsync(x => x.ReceiptId == receiptId);
        Assert.Contains("m8-accepted-1", Encoding.UTF8.GetString(accepted.RawPayloadUtf8));
        Assert.Contains("m8-canonical-v1", accepted.CanonicalPayloadJson);
    }

    [Fact]
    public async Task ChangedPayloadConflictsWithoutMutatingOriginalOrLeakingBody()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var source = await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
            .CreateAsync(fixture.A, "m8_collision", "Collision Test", CancellationToken.None);

        using var first = await Post(client, source.Bearer, Payload(id: "m8-collision"));
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var body = await first.Content.ReadFromJsonAsync<JsonElement>();
        var receiptId = body.GetProperty("receiptId").GetGuid();
        for (var i = 0; i < 3; i++)
        {
            using var conflicting = await Post(client, source.Bearer,
                Payload(id: "m8-collision", code: "GTOT"));
            Assert.Equal(HttpStatusCode.Conflict, conflicting.StatusCode);
            var error = await conflicting.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("source_event_collision", error.GetProperty("code").GetString());
            Assert.False(error.TryGetProperty("receiptId", out _));
        }
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(1, await db.TrackingAcceptedEvents.CountAsync(x => x.ReceiptId == receiptId));
        Assert.Equal(1, await db.TrackingReceiptConflicts.CountAsync(x => x.ReceiptId == receiptId));
        Assert.Equal("GTIN", (await db.TrackingAcceptedEvents.AsNoTracking()
            .SingleAsync(x => x.ReceiptId == receiptId)).EventCode);
    }

    [Fact]
    public async Task TenantAndSourceKeysAreIndependentAndRevocationStopsReplay()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var sourceStore = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
        var a = await sourceStore.CreateAsync(fixture.A, "m8_tenant_a", "Tenant A", CancellationToken.None);
        var b = await sourceStore.CreateAsync(fixture.B, "m8_tenant_b", "Tenant B", CancellationToken.None);
        using var first = await Post(client, a.Bearer, Payload(id: "shared-key"));
        using var second = await Post(client, b.Bearer, Payload(id: "shared-key"));
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        var r1 = await first.Content.ReadFromJsonAsync<JsonElement>();
        var r2 = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(r1.GetProperty("receiptId").GetGuid(), r2.GetProperty("receiptId").GetGuid());
        await sourceStore.RevokeAsync(fixture.A, "m8_tenant_a", a.CredentialId, CancellationToken.None);
        using var revoked = await Post(client, a.Bearer, Payload(id: "shared-key"));
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        using var allowed = await Post(client, b.Bearer, Payload(id: "shared-key"));
        Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
        var replay = await allowed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(replay.GetProperty("duplicate").GetBoolean());
    }

    [Fact]
    public async Task FinalTransactionAuthorizationRejectsWrongDigestAndForeignSource()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var sourceStore = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
        var a = await sourceStore.CreateAsync(fixture.A, "m8_final_a", "Final A",
            CancellationToken.None);
        var b = await sourceStore.CreateAsync(fixture.A, "m8_final_b", "Final B",
            CancellationToken.None);
        var bytes = Encoding.UTF8.GetBytes(Payload(id: "final-reauth"));
        var parsed = TrackingEventPolicy.Parse(bytes, TimeProvider.System.GetUtcNow());
        Assert.NotNull(parsed.Value);
        var bound = parsed.Value!.Bind(a.Id, fixture.A);
        var intake = scope.ServiceProvider.GetRequiredService<TrackingInboxStore>();
        var forged = IntegrationSourceStore.FormatBearer(a.CredentialId,
            RandomNumberGenerator.GetBytes(32));
        var wrongDigest = await intake.ReceiveAsync(bound, forged, bytes, "final-digest",
            CancellationToken.None);
        Assert.Equal(TrackingReceiptOutcome.AccessDenied, wrongDigest.Outcome);
        var foreignSource = await intake.ReceiveAsync(bound, b.Bearer, bytes, "final-source",
            CancellationToken.None);
        Assert.Equal(TrackingReceiptOutcome.AccessDenied, foreignSource.Outcome);
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(0, await db.TrackingInboxReceipts.CountAsync(x => x.SourceId == a.Id));
    }

    [Fact]
    public async Task PendingCapacityReturnsRetryable503ButReplaysOriginalReceipt()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString(),
            pendingLimit: 1);
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var source = await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
            .CreateAsync(fixture.A, "m8_capacity", "Capacity Test", CancellationToken.None);
        using var first = await Post(client, source.Bearer, Payload(id: "capacity-1"));
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        using var blocked = await Post(client, source.Bearer, Payload(id: "capacity-2"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, blocked.StatusCode);
        Assert.NotNull(blocked.Headers.RetryAfter);
        var error = await blocked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("intake_capacity_exceeded", error.GetProperty("code").GetString());
        using var replay = await Post(client, source.Bearer, Payload(id: "capacity-1"));
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        Assert.True((await replay.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("duplicate").GetBoolean());
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(1, await db.TrackingInboxReceipts.CountAsync(x => x.SourceId == source.Id));
    }

    [Fact]
    public async Task ConcurrentDuplicateIntakeCreatesExactlyOneReceipt()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var source = await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
            .CreateAsync(fixture.A, "m8_parallel", "Parallel Test", CancellationToken.None);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            using var client = factory.CreateClient();
            using var response = await Post(client, source.Bearer, Payload(id: "parallel-m8"));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return (response.StatusCode, Id: body.GetProperty("receiptId").GetGuid(),
                Duplicate: body.GetProperty("duplicate").GetBoolean());
        }));
        Assert.All(outcomes, o => Assert.Equal(HttpStatusCode.Accepted, o.StatusCode));
        Assert.Single(outcomes.Select(x => x.Id).Distinct());
        Assert.Single(outcomes, x => !x.Duplicate);
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(1, await db.TrackingInboxReceipts.CountAsync(x => x.SourceId == source.Id));
        Assert.Equal(1, await db.TrackingInboxWorkItems.CountAsync(x => x.SourceId == source.Id));
    }
}
