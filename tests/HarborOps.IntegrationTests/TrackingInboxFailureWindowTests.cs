using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class TrackingInboxFailureWindowTests
{
    private sealed class FailOnceAtCommit(string stage) : ITrackingReceiptCommitHook
    {
        private int invoked;

        public Task BeforeCommitAsync(Guid receiptId, CancellationToken ct)
        {
            if (stage == "before" && Interlocked.Exchange(ref invoked, 1) == 0)
                throw new TimeoutException("Controlled before-COMMIT write failure");
            return Task.CompletedTask;
        }

        public Task AfterCommitAsync(Guid receiptId, CancellationToken ct)
        {
            if (stage == "after" && Interlocked.Exchange(ref invoked, 1) == 0)
                throw new TimeoutException("Controlled lost acknowledgment after COMMIT");
            return Task.CompletedTask;
        }
    }

    private sealed class CancelBeforeCommit(CancellationTokenSource source) : ITrackingReceiptCommitHook
    {
        public Task BeforeCommitAsync(Guid receiptId, CancellationToken ct)
        {
            source.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task AfterCommitAsync(Guid receiptId, CancellationToken ct) => Task.CompletedTask;
    }

    private static string Payload(string id) => $$"""
        {"sourceEventId":"{{id}}","eventFamily":"Equipment","eventCode":"GTIN",
         "classifier":"ACT","occurredAtUtc":"2026-11-03T10:00:00Z",
         "equipmentReference":"SYN-BOX-ABCD","externalSchemaVersion":"2.2.0"}
        """;

    private static async Task<HttpResponseMessage> Post(HttpClient client, string bearer, string id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/integrations/v1/tracking-events");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        request.Headers.Add("X-HarborOps-Tracking-Profile", TrackingEventPolicy.Profile);
        request.Content = new StringContent(Payload(id), Encoding.UTF8, "application/json");
        return await client.SendAsync(request);
    }

    [Theory]
    [InlineData("before", false)]
    [InlineData("after", true)]
    public async Task CommitUncertaintyNeverProducesSuccessAndReplayResolvesOutcome(
        string stage, bool committed)
    {
        var hook = new FailOnceAtCommit(stage);
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString(),
            overrideServices: services =>
            {
                services.RemoveAll<ITrackingReceiptCommitHook>();
                services.AddSingleton<ITrackingReceiptCommitHook>(hook);
            });
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var source = await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
            .CreateAsync(fixture.A, "m8_fault_" + stage, "Fault Window", CancellationToken.None);
        using var client = factory.CreateClient();
        var eventId = "m8-fault-" + stage;

        using var uncertain = await Post(client, source.Bearer, eventId);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, uncertain.StatusCode);
        Assert.NotNull(uncertain.Headers.RetryAfter);
        Assert.Contains("no-store", uncertain.Headers.CacheControl?.ToString());
        var failed = await uncertain.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("receipt_unavailable", failed.GetProperty("code").GetString());
        Assert.NotEqual("", failed.GetProperty("correlationId").GetString());

        // The exception from the test hook is thrown after every SQL INSERT.
        // Before COMMIT all three rows must roll back; after COMMIT they survive.
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var first = await db.TrackingInboxReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SourceId == source.Id && x.SourceEventId == eventId);
        Assert.Equal(committed, first is not null);
        Assert.Equal(committed ? 1 : 0,
            await db.TrackingAcceptedEvents.CountAsync(x => x.SourceId == source.Id));
        Assert.Equal(committed ? 1 : 0,
            await db.TrackingInboxWorkItems.CountAsync(x => x.SourceId == source.Id));

        using var retry = await Post(client, source.Bearer, eventId);
        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
        var result = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(committed, result.GetProperty("duplicate").GetBoolean());
        Assert.NotEqual(Guid.Empty, result.GetProperty("receiptId").GetGuid());
        if (committed)
            Assert.Equal(first!.Id, result.GetProperty("receiptId").GetGuid());
        Assert.Equal(1, await db.TrackingInboxReceipts.CountAsync(x => x.SourceId == source.Id));
        Assert.Equal(1, await db.TrackingAcceptedEvents.CountAsync(x => x.SourceId == source.Id));
        Assert.Equal(1, await db.TrackingInboxWorkItems.CountAsync(x => x.SourceId == source.Id));
    }

    [Fact]
    public async Task MidTransactionCancellationRollsBackCompleteReceiptTriple()
    {
        using var cancellation = new CancellationTokenSource();
        var hook = new CancelBeforeCommit(cancellation);
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString(),
            overrideServices: services =>
            {
                services.RemoveAll<ITrackingReceiptCommitHook>();
                services.AddSingleton<ITrackingReceiptCommitHook>(hook);
            });
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var source = await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
            .CreateAsync(fixture.A, "m8_cancel_during", "Cancel During Write", CancellationToken.None);
        var bytes = Encoding.UTF8.GetBytes(Payload("m8-mid-cancel"));
        var value = TrackingEventPolicy.Parse(bytes, TimeProvider.System.GetUtcNow()).Value!
            .Bind(source.Id, fixture.A);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scope.ServiceProvider.GetRequiredService<TrackingInboxStore>().ReceiveAsync(
                value, source.Bearer, bytes, "cancel-during", cancellation.Token));
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Empty(await db.TrackingInboxReceipts.Where(x => x.SourceId == source.Id).ToListAsync());
        Assert.Empty(await db.TrackingAcceptedEvents.Where(x => x.SourceId == source.Id).ToListAsync());
        Assert.Empty(await db.TrackingInboxWorkItems.Where(x => x.SourceId == source.Id).ToListAsync());
    }

    [Fact]
    public async Task InitialAuthenticationDatabaseOutageGivesRetryable503()
    {
        using var goodFactory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(goodFactory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var scope = goodFactory.Services.CreateAsyncScope();
        var source = await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
            .CreateAsync(fixture.A, "m8_preauth_fail", "Preauth Dependency", CancellationToken.None);

        // The connection refuses before initial M7 AuthenticateAsync completes.
        // This tests the global handler, not the receipt-store-specific catch.
        using var unavailableFactory = new ReferenceDataHttpTests.ApiFactory(
            "Host=127.0.0.1;Port=1;Database=harborops_unavailable;Username=harborops;Timeout=1");
        using var client = unavailableFactory.CreateClient();
        using var response = await Post(client, source.Bearer, "m8-preauth-outage");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("dependency_unavailable", body.GetProperty("code").GetString());
        Assert.NotEqual("", body.GetProperty("correlationId").GetString());
    }
}
