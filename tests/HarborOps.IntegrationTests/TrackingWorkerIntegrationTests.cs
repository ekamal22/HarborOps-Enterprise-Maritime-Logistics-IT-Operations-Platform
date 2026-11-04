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
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class TrackingWorkerIntegrationTests
{
    private static async Task<Guid> Ingest(
        ReferenceDataHttpTests.ApiFactory factory,
        Guid organization, string key, string reference = "SYN-BOX-UNKNOWN")
    {
        ProvisionedIntegrationSource source;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
            source = await service.CreateAsync(organization,
                "m9_" + Guid.NewGuid().ToString("N")[..12], "M9 Source", CancellationToken.None);
        }
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/integrations/v1/tracking-events");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", source.Bearer);
        request.Headers.Add("X-HarborOps-Tracking-Profile", TrackingEventPolicy.Profile);
        request.Content = new StringContent($$"""
            {"sourceEventId":"{{key}}","eventFamily":"Equipment","eventCode":"GTIN",
             "classifier":"ACT","occurredAtUtc":"2026-11-03T10:00:00Z",
             "equipmentReference":"{{reference}}","externalSchemaVersion":"2.2.0"}
            """, Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("receiptId").GetGuid();
    }

    [Fact]
    public async Task UnknownReferenceIsParkedAndCanBeRedrivenOnlyByManager()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var id = await Ingest(factory, fixture.A, "m9-unknown-" + Guid.NewGuid().ToString("N")[..10]);

        TrackingWorkClaim? claimed;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var worker = new TrackingWorkStore(db, new TrackingWorkerSettings());
            claimed = await worker.ClaimOneAsync(fixture.A, CancellationToken.None);
            Assert.NotNull(claimed);
            Assert.Equal(id, claimed!.ReceiptId);
            Assert.Equal(1, claimed.Attempt);
            Assert.False(await worker.HeartbeatAsync(claimed with { LeaseEpoch = claimed.LeaseEpoch - 1 },
                CancellationToken.None));
            Assert.True(await worker.HeartbeatAsync(claimed, CancellationToken.None));
            Assert.Equal(TrackingWorkDisposition.NeedsReconciliation,
                await worker.FinalizeAsync(claimed, CancellationToken.None));
            Assert.Empty(await db.TrackingProcessingEffects.Where(x => x.ReceiptId == id).ToListAsync());
            Assert.Equal(1, await db.TrackingWorkAttemptEvents
                .CountAsync(x => x.ReceiptId == id && x.EventKind == "ReconciliationRequired"));
        }

        var url = "/api/v1/organizations/" + fixture.A + "/tracking-work";
        using var denied = await admin.GetAsync(url);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var visible = await analyst.GetAsync(url + "/" + id);
        Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
        var item = await visible.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("NeedsReconciliation", item.GetProperty("state").GetString());
        Assert.False(item.TryGetProperty("rawPayloadUtf8", out _));
        using var foreign = await manager.GetAsync(
            "/api/v1/organizations/" + fixture.B + "/tracking-work/" + id);
        Assert.NotEqual(HttpStatusCode.OK, foreign.StatusCode);
        using var forbidden = await ReferenceDataHttpTests.Send(analyst, HttpMethod.Post,
            url + "/" + id + "/redrive", new { reason = "investigate assignment" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var invalidCsrf = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post,
            url + "/" + id + "/redrive", new { reason = "investigate assignment" }, csrf: false);
        Assert.Equal(HttpStatusCode.Forbidden, invalidCsrf.StatusCode);
        using var accepted = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post,
            url + "/" + id + "/redrive", new { reason = "investigate assignment" });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var status = await manager.GetAsync(url + "/" + id);
        Assert.Equal("Pending",
            (await status.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("state").GetString());

        await using var verify = factory.Services.CreateAsyncScope();
        var readDb = verify.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var pending = await readDb.TrackingInboxWorkItems.SingleAsync(x => x.ReceiptId == id);
        Assert.Equal(1, pending.RedriveGeneration);
        Assert.Equal(0, pending.AttemptCount);
        var audited = await readDb.TrackingWorkAttemptEvents.AsNoTracking()
            .SingleAsync(x => x.ReceiptId == id && x.EventKind == "Redriven");
        Assert.NotNull(audited.ActorUserId);
        Assert.Equal("investigate assignment", audited.RedriveReason);
        Assert.Equal(1, await readDb.TrackingInboxReceipts.CountAsync(x => x.Id == id));
        Assert.Equal(1, await readDb.TrackingAcceptedEvents.CountAsync(x => x.ReceiptId == id));
    }

    [Fact]
    public async Task RealConcurrentClaimsNeverShareOneLease()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var id = await Ingest(factory, fixture.A, "m9-race-" + Guid.NewGuid().ToString("N")[..10]);

        async Task<TrackingWorkClaim?> Claim()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            return await new TrackingWorkStore(db, new TrackingWorkerSettings())
                .ClaimOneAsync(fixture.A, CancellationToken.None);
        }

        var attempts = await Task.WhenAll(Claim(), Claim());
        Assert.Single(attempts.Where(x => x is not null));
        var winner = attempts.Single(x => x is not null)!;
        Assert.Equal(id, winner.ReceiptId);
        Assert.Equal(1L, winner.LeaseEpoch);
        await using var read = factory.Services.CreateAsyncScope();
        var db = read.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(1, await db.TrackingWorkAttemptEvents
            .CountAsync(x => x.ReceiptId == id && x.EventKind == "Claimed"));
        var worker = new TrackingWorkStore(db, new TrackingWorkerSettings());
        Assert.Equal(TrackingWorkDisposition.Stale,
            await worker.FinalizeAsync(winner with { LeaseToken = Guid.NewGuid() },
                CancellationToken.None));
        Assert.Equal(TrackingWorkDisposition.NeedsReconciliation,
            await worker.FinalizeAsync(winner, CancellationToken.None));
        Assert.Equal(TrackingWorkDisposition.Stale,
            await worker.FinalizeAsync(winner, CancellationToken.None));
    }

    [Fact]
    public async Task LeasedAndReconciliationStatesStillConsumeAdmissionQuota()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString(), pendingLimit: 1);
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var id = await Ingest(factory, fixture.A, "m9-cap-a-" + Guid.NewGuid().ToString("N")[..9]);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var worker = new TrackingWorkStore(db, new TrackingWorkerSettings());
        var claim = await worker.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.NotNull(claim);
        // Admission with a claimed item still counts the same unresolved slot.
        var existingSource = await db.TrackingInboxReceipts.SingleAsync(x => x.Id == id);
        var source = await db.IntegrationSources.SingleAsync(x => x.Id == existingSource.SourceId);
        Assert.Equal("Leased", (await db.TrackingInboxWorkItems.SingleAsync(x => x.ReceiptId == id)).State);
        Assert.Equal(1, await db.TrackingInboxWorkItems.CountAsync(x =>
            x.OrganizationId == fixture.A && TrackingWorkPolicy.IsUnresolved(x.State)));
        Assert.Equal(TrackingWorkDisposition.NeedsReconciliation,
            await worker.FinalizeAsync(claim!, CancellationToken.None));
        Assert.Equal(1, await db.TrackingInboxWorkItems.CountAsync(x =>
            x.OrganizationId == fixture.A && x.State == "NeedsReconciliation"));
    }

    [Fact]
    public async Task InvalidDirectSqlCannotSkipStateOrRewriteEvidence()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var id = await Ingest(factory, fixture.A, "m9-sql-" + Guid.NewGuid().ToString("N")[..10]);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var skip = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE tracking_inbox_work_items
                   SET state = {"Processed"} WHERE receipt_id = {id}
                """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, skip.SqlState);
        var delete = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM tracking_inbox_receipts WHERE id = {id}
                """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, delete.SqlState);
        Assert.Equal("Pending", (await db.TrackingInboxWorkItems.AsNoTracking()
            .SingleAsync(x => x.ReceiptId == id)).State);
    }
}
