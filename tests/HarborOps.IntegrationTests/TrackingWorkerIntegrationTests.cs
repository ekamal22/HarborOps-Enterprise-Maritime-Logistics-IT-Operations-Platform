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
    private static async Task<(HttpStatusCode Status, Guid? ReceiptId)> PostIngest(
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
        return response.StatusCode == HttpStatusCode.Accepted
            ? (response.StatusCode, (await response.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("receiptId").GetGuid())
            : (response.StatusCode, null);
    }

    private static async Task<Guid> Ingest(ReferenceDataHttpTests.ApiFactory factory,
        Guid organization, string key, string reference = "SYN-BOX-UNKNOWN")
    {
        var (status, receipt) = await PostIngest(factory, organization, key, reference);
        Assert.Equal(HttpStatusCode.Accepted, status);
        Assert.NotNull(receipt);
        return receipt.Value;
    }

    [Fact]
    public async Task ValidTimeScopedContainerAssignmentProducesExactlyOneImmutableEffect()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var maritime = await AssignmentHttpTests.Seed(fixture);
        using var allocation = await ReferenceDataHttpTests.Send(analyst, HttpMethod.Post,
            maritime.Root + "/" + maritime.Shipment + "/container-assignments",
            new { containerId = maritime.Container, startsAtUtc = "2026-11-03T09:00:00Z" });
        Assert.Equal(HttpStatusCode.Created, allocation.StatusCode);
        string reference;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            reference = (await db.Containers.AsNoTracking()
                .SingleAsync(c => c.Id == maritime.Container)).ContainerIdentifier;
        }
        var receipt = await Ingest(factory, fixture.A,
            "m9-resolved-" + Guid.NewGuid().ToString("N")[..12], reference);
        await using var verify = factory.Services.CreateAsyncScope();
        var database = verify.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var worker = new TrackingWorkStore(database, new TrackingWorkerSettings());
        var claim = await worker.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.NotNull(claim);
        Assert.Equal(receipt, claim!.ReceiptId);
        Assert.Equal(TrackingWorkDisposition.Processed,
            await worker.FinalizeAsync(claim, CancellationToken.None));
        Assert.Equal(TrackingWorkDisposition.Stale,
            await worker.FinalizeAsync(claim, CancellationToken.None));
        var effect = await database.TrackingProcessingEffects.AsNoTracking()
            .SingleAsync(e => e.ReceiptId == receipt);
        Assert.Equal(maritime.Container, effect.ContainerId);
        Assert.Equal(maritime.Shipment, effect.ShipmentId);
        Assert.Equal("m9-reference-v1", effect.ResolutionPolicy);
        var work = await database.TrackingInboxWorkItems.AsNoTracking()
            .SingleAsync(x => x.ReceiptId == receipt);
        Assert.Equal("Processed", work.State);
        Assert.Equal(1, work.AttemptCount);
        Assert.Equal(1, await database.TrackingProcessingEffects.CountAsync(x => x.ReceiptId == receipt));
        var protectedEffect = await Assert.ThrowsAsync<PostgresException>(() =>
            database.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE tracking_processing_effects SET resolution_kind = {"Other"}
                WHERE receipt_id = {receipt}
                """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, protectedEffect.SqlState);
        Assert.Equal(1, await database.TrackingAcceptedEvents.CountAsync(x => x.ReceiptId == receipt));
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
    public async Task DistinctConcurrentDbContextsCannotShareOneLease()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var id = await Ingest(factory, fixture.A, "m9-race-" + Guid.NewGuid().ToString("N")[..10]);

        using var ready = new CountdownEvent(2);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<TrackingWorkClaim?> Claim()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            ready.Signal();
            await release.Task;
            return await new TrackingWorkStore(db, new TrackingWorkerSettings())
                .ClaimOneAsync(fixture.A, CancellationToken.None);
        }

        var first = Claim();
        var second = Claim();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(5)));
        release.SetResult();
        var attempts = await Task.WhenAll(first, second);
        var winner = Assert.Single(attempts, x => x is not null)!;
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
        Assert.Equal(1, await db.TrackingInboxReceipts.CountAsync(x => x.Id == id));
        Assert.Equal("Leased", (await db.TrackingInboxWorkItems.SingleAsync(x => x.ReceiptId == id)).State);
        Assert.Equal(1, await db.TrackingInboxWorkItems.CountAsync(x =>
            x.OrganizationId == fixture.A && (x.State == "Pending" || x.State == "Leased" ||
                x.State == "RetryWaiting" || x.State == "NeedsReconciliation")));
        var refusedWhileLeased = await PostIngest(factory, fixture.A,
            "m9-cap-refused-lease-" + Guid.NewGuid().ToString("N")[..9]);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, refusedWhileLeased.Status);
        Assert.Null(refusedWhileLeased.ReceiptId);
        Assert.Equal(TrackingWorkDisposition.NeedsReconciliation,
            await worker.FinalizeAsync(claim!, CancellationToken.None));
        Assert.Equal(1, await db.TrackingInboxWorkItems.CountAsync(x =>
            x.OrganizationId == fixture.A && x.State == "NeedsReconciliation"));
        var refusedWhileParked = await PostIngest(factory, fixture.A,
            "m9-cap-refused-parked-" + Guid.NewGuid().ToString("N")[..9]);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, refusedWhileParked.Status);
        Assert.Null(refusedWhileParked.ReceiptId);
    }

    [Fact]
    public async Task PermanentProcessorFailureIsDeadLetteredWithImmutableEvidence()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var receipt = await Ingest(factory, fixture.A,
            "m9-permanent-" + Guid.NewGuid().ToString("N")[..9]);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var worker = new TrackingWorkStore(db, new TrackingWorkerSettings());
        var lease = await worker.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.NotNull(lease);
        Assert.True(await worker.FailAsync(lease!, "processor_error", false, CancellationToken.None));
        var work = await db.TrackingInboxWorkItems.AsNoTracking()
            .SingleAsync(x => x.ReceiptId == receipt);
        Assert.Equal("DeadLetter", work.State);
        Assert.Equal(1, work.AttemptCount);
        Assert.Equal("processor_error", work.LastErrorCode);
        Assert.Equal(1, await db.TrackingWorkAttemptEvents.CountAsync(x =>
            x.ReceiptId == receipt && x.EventKind == "DeadLettered" &&
            x.ReasonCode == "processor_error"));
        Assert.Null(await worker.ClaimOneAsync(fixture.A, CancellationToken.None));
        Assert.Equal(1, await db.TrackingInboxReceipts.CountAsync(x => x.Id == receipt));
    }

    [Fact]
    public async Task DirectSqlRedriveCannotBypassAttributedImmutableHistory()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var receipt = await Ingest(factory, fixture.A,
            "m9-audit-" + Guid.NewGuid().ToString("N")[..10]);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var work = new TrackingWorkStore(db, new TrackingWorkerSettings());
        var claim = await work.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.NotNull(claim);
        Assert.Equal(TrackingWorkDisposition.NeedsReconciliation,
            await work.FinalizeAsync(claim!, CancellationToken.None));

        // Even a syntactically legal state transition is rejected unless an
        // attributed, receipt-scoped Redriven event commits with it.
        var unaudited = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE tracking_inbox_work_items
                SET state={"Pending"},attempt_count={0},
                    redrive_generation=redrive_generation+1,lease_epoch=lease_epoch+1,
                    available_at_utc=clock_timestamp(),last_error_code=NULL,
                    completed_at_utc=NULL,resolution_kind=NULL,
                    updated_at_utc=clock_timestamp()
                WHERE receipt_id={receipt}
                """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, unaudited.SqlState);
        var preserved = await db.TrackingInboxWorkItems.AsNoTracking()
            .SingleAsync(x => x.ReceiptId == receipt);
        Assert.Equal("NeedsReconciliation", preserved.State);
        Assert.Equal(0, preserved.RedriveGeneration);

        var attemptedUpdate = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE tracking_work_attempt_events
                   SET event_kind={"Processed"} WHERE receipt_id={receipt}
                """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, attemptedUpdate.SqlState);
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
