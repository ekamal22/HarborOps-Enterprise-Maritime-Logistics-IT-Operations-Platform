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

public sealed class TrackingInboxRealCommitDisruptionTests
{
    private static async Task<HttpResponseMessage> Post(
        HttpClient client, string bearer, string id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/integrations/v1/tracking-events");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        request.Headers.Add("X-HarborOps-Tracking-Profile", "tnt-2.2-harborops-v1");
        request.Content = new StringContent($$"""
            {"sourceEventId":"{{id}}","eventFamily":"Equipment","eventCode":"GTIN",
             "classifier":"ACT","occurredAtUtc":"2026-11-03T10:00:00Z",
             "equipmentReference":"SYN-BOX-ABCD","externalSchemaVersion":"2.2.0"}
            """, Encoding.UTF8, "application/json");
        return await client.SendAsync(request);
    }

    [Theory]
    [InlineData(TrackingCommitSocketProxy.CutPoint.BeforeCommitForward, false)]
    [InlineData(TrackingCommitSocketProxy.CutPoint.AfterCommitServerReady, true)]
    public async Task RealPostgresCommitTransportLossNeverAcknowledgesAndReplayResolves(
        TrackingCommitSocketProxy.CutPoint cutPoint, bool expectedCommit)
    {
        var direct = TestDatabase.ConnectionString();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(direct);
        var fixture = await ReferenceDataHttpTests.Setup(seedFactory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var scope = seedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var source = await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
            .CreateAsync(fixture.A, "m8_wire_" + cutPoint.ToString().ToLowerInvariant(),
                "Commit Wire Fault", CancellationToken.None);

        await using var proxy = new TrackingCommitSocketProxy(direct, cutPoint);
        using var affectedFactory = new ReferenceDataHttpTests.ApiFactory(
            proxy.ProxiedConnectionString(direct));
        using var affectedClient = affectedFactory.CreateClient();
        var eventId = "m8-wire-" + cutPoint.ToString().ToLowerInvariant();
        using var interrupted = await Post(affectedClient, source.Bearer, eventId);
        Assert.True(proxy.Triggered, "A real PostgreSQL COMMIT wire exchange must be interrupted");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, interrupted.StatusCode);
        Assert.NotNull(interrupted.Headers.RetryAfter);
        var error = await interrupted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("receipt_unavailable", error.GetProperty("code").GetString());

        // The independent direct connection is the outcome oracle, not the
        // client response. COMMIT cut before forwarding rolls back; cutting
        // its ReadyForQuery reply commits but produces no successful 202.
        var persisted = await db.TrackingInboxReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SourceId == source.Id && x.SourceEventId == eventId);
        Assert.Equal(expectedCommit, persisted is not null);
        Assert.Equal(expectedCommit ? 1 : 0,
            await db.TrackingAcceptedEvents.CountAsync(x => x.SourceId == source.Id));
        Assert.Equal(expectedCommit ? 1 : 0,
            await db.TrackingInboxWorkItems.CountAsync(x => x.SourceId == source.Id));

        using var cleanClient = seedFactory.CreateClient();
        using var retry = await Post(cleanClient, source.Bearer, eventId);
        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
        var receipt = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedCommit, receipt.GetProperty("duplicate").GetBoolean());
        if (expectedCommit)
            Assert.Equal(persisted!.Id, receipt.GetProperty("receiptId").GetGuid());
        Assert.Equal(1, await db.TrackingInboxReceipts
            .CountAsync(x => x.SourceId == source.Id));
        Assert.Equal(1, await db.TrackingAcceptedEvents
            .CountAsync(x => x.SourceId == source.Id));
        Assert.Equal(1, await db.TrackingInboxWorkItems
            .CountAsync(x => x.SourceId == source.Id));
    }
}
