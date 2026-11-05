using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.Shipments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class TrackingProjectionG10Tests
{
    [Fact]
    public async Task ResolvedEventReadRebuildAndVoidCorrectionPreserveM9History()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var target = await G9Support.SeedResolvable(factory, fixture);
        var source = await G9Support.NewSource(factory, fixture.A, "g10_track");
        var prefix = Guid.NewGuid().ToString("N")[..10];
        var firstReceipt = await G9Support.Accept(factory, source,
            G9Support.EquipmentPayload("g10-in-" + prefix, target.Reference));
        var secondReceipt = await G9Support.Accept(factory, source,
            G9Support.EquipmentPayload("g10-out-" + prefix, target.Reference,
                occurredAt: "2026-11-03T11:00:00Z", code: "GTOT"));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var worker = new TrackingWorkStore(db, new TrackingWorkerSettings());
        for (var n = 0; n < 2; n++)
        {
            var claim = await worker.ClaimOneAsync(fixture.A, CancellationToken.None);
            Assert.NotNull(claim);
            Assert.Equal(TrackingWorkDisposition.Processed,
                await worker.FinalizeAsync(claim!, CancellationToken.None));
        }

        var beforeEffects = await db.TrackingProcessingEffects.AsNoTracking()
            .Where(e => e.ReceiptId == firstReceipt || e.ReceiptId == secondReceipt)
            .Select(e => new { e.ReceiptId, e.ShipmentId, e.ContainerId }).ToListAsync();
        Assert.Equal(2, beforeEffects.Count);
        var manualBefore = (await db.Shipments.AsNoTracking()
            .SingleAsync(s => s.Id == target.Shipment)).WorkflowStatus;

        var store = new ShipmentTimelineStore(db);
        var read = await store.ReadAsync(fixture.A, target.Shipment, CancellationToken.None);
        Assert.NotNull(read);
        Assert.Equal("EquipmentOut", read!.State.ObservedTrackingState);
        Assert.Equal(2, read.State.EventCount);
        Assert.Equal(0, read.MaterializationRevision);
        Assert.True(read.MaterializationStale);
        Assert.Null(await store.ReadAsync(fixture.B, target.Shipment, CancellationToken.None));

        var firstBuild = await store.RebuildAsync(fixture.A, target.Shipment, CancellationToken.None);
        Assert.Equal(TrackingMutationResult.Applied, firstBuild.Result);
        Assert.Equal(1, firstBuild.Revision);
        var sameBuild = await store.RebuildAsync(fixture.A, target.Shipment, CancellationToken.None);
        Assert.Equal(1, sameBuild.Revision);

        using (var response = await analyst.GetAsync(
            $"/api/v1/organizations/{fixture.A}/shipments/{target.Shipment}/tracking-projection"))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("EquipmentOut", json.GetProperty("observedTrackingState").GetString());
            Assert.True(json.GetProperty("etaUtc").ValueKind == JsonValueKind.Null);
            Assert.Equal(1, json.GetProperty("materializationRevision").GetInt64());
        }

        var actor = await db.AuthUsers.AsNoTracking().Select(u => u.Id).FirstAsync();
        var changed = await store.CorrectAsync(fixture.A, target.Shipment,
            new TrackingCorrection(secondReceipt, null, 0, "Confirmed invalid observation"),
            actor, Guid.NewGuid().ToString("N"), CancellationToken.None);
        Assert.Equal(TrackingMutationResult.Applied, changed.Result);
        Assert.Equal(2, changed.Revision);
        var revised = await store.ReadAsync(fixture.A, target.Shipment, CancellationToken.None);
        Assert.NotNull(revised);
        Assert.Equal("EquipmentIn", revised!.State.ObservedTrackingState);
        Assert.Equal(1, revised.CorrectionGeneration);
        Assert.Single(revised.Timeline.Where(e => e.Superseded));
        Assert.Null(revised.Timeline.Single(e => e.ReceiptId == secondReceipt).ReplacedByReceiptId);
        Assert.False(revised.MaterializationStale);

        var invalid = await store.CorrectAsync(fixture.A, target.Shipment,
            new TrackingCorrection(firstReceipt, null, 0, "Stale correction generation"),
            actor, "stale-correction", CancellationToken.None);
        Assert.Equal(TrackingMutationResult.Conflict, invalid.Result);
        Assert.Equal(2, (await store.RebuildAsync(fixture.A, target.Shipment,
            CancellationToken.None)).Revision);

        var afterEffects = await db.TrackingProcessingEffects.AsNoTracking()
            .Where(e => e.ReceiptId == firstReceipt || e.ReceiptId == secondReceipt)
            .Select(e => new { e.ReceiptId, e.ShipmentId, e.ContainerId }).ToListAsync();
        Assert.Equal(beforeEffects, afterEffects);
        Assert.Equal(manualBefore, (await db.Shipments.AsNoTracking()
            .SingleAsync(s => s.Id == target.Shipment)).WorkflowStatus);

        using var timeline = await analyst.GetAsync(
            $"/api/v1/organizations/{fixture.A}/shipments/{target.Shipment}/events?limit=1");
        Assert.Equal(HttpStatusCode.OK, timeline.StatusCode);
        var page = await timeline.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, page.GetProperty("eventCount").GetInt32());
        var cursor = page.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(cursor));
        using var next = await analyst.GetAsync(
            $"/api/v1/organizations/{fixture.A}/shipments/{target.Shipment}/events?limit=1&cursor={Uri.EscapeDataString(cursor!)}");
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        using var foreign = await analyst.GetAsync(
            $"/api/v1/organizations/{fixture.B}/shipments/{target.Shipment}/events");
        Assert.NotEqual(HttpStatusCode.OK, foreign.StatusCode);
        var root = $"/api/v1/organizations/{fixture.A}/shipments/{target.Shipment}";
        using (var forbidden = await ReferenceDataHttpTests.Send(
            analyst, HttpMethod.Post, root + "/tracking-projection/rebuild"))
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using (var approved = await ReferenceDataHttpTests.Send(
            manager, HttpMethod.Post, root + "/tracking-projection/rebuild"))
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        using (var staleCorrection = await ReferenceDataHttpTests.Send(
            manager, HttpMethod.Post, root + "/tracking-corrections", new
            {
                targetReceiptId = firstReceipt, replacementReceiptId = (Guid?)null,
                expectedGeneration = 0, reason = "Invalid stale correction attempt"
            }))
            Assert.Equal(HttpStatusCode.Conflict, staleCorrection.StatusCode);
        using (var approvedCorrection = await ReferenceDataHttpTests.Send(
            manager, HttpMethod.Post, root + "/tracking-corrections", new
            {
                targetReceiptId = firstReceipt, replacementReceiptId = (Guid?)null,
                expectedGeneration = 1, reason = "Confirmed invalid first observation"
            }))
            Assert.Equal(HttpStatusCode.OK, approvedCorrection.StatusCode);
        var afterHttp = await store.ReadAsync(fixture.A, target.Shipment, CancellationToken.None);
        Assert.NotNull(afterHttp);
        Assert.Equal("Unknown", afterHttp!.State.ObservedTrackingState);
        Assert.Equal(2, afterHttp.CorrectionGeneration);
    }

    [Fact]
    public void OpaqueTimelineCursorIsBoundToExactDigestAndRange()
    {
        var digest = new string('a', 64);
        var valid = ShipmentTimelineStore.EncodeCursor(digest, 5);
        Assert.True(ShipmentTimelineStore.DecodeCursor(valid, digest, 10, out var index));
        Assert.Equal(5, index);
        Assert.False(ShipmentTimelineStore.DecodeCursor(valid, new string('b', 64), 10, out _));
        Assert.False(ShipmentTimelineStore.DecodeCursor(valid, digest, 4, out _));
        Assert.False(ShipmentTimelineStore.DecodeCursor("../bad", digest, 10, out _));
    }
}
