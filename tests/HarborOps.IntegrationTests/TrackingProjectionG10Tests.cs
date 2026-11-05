using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.Shipments;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class TrackingProjectionG10Tests
{
    private static async Task<HttpResponseMessage> SendM10(HttpClient client, string path,
        object? payload = null, Guid? idempotencyKey = null)
    {
        using var csrf = await client.GetAsync("/api/v1/auth/csrf");
        csrf.EnsureSuccessStatusCode();
        var token = await csrf.Content.ReadFromJsonAsync<JsonElement>();
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
        request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",
            token.GetProperty("token").GetString());
        request.Headers.TryAddWithoutValidation("Idempotency-Key",
            (idempotencyKey ?? Guid.NewGuid()).ToString());
        if (payload is not null) request.Content = JsonContent.Create(payload);
        return await client.SendAsync(request);
    }

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
        // Both referenced shipments exist, but only the correct shipment may
        // receive an application record for the immutable M9 effect.
        var anotherShipment = await G9Support.SeedResolvable(factory, fixture);
        var badApplication = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO shipment_projection_applications
                    (receipt_id,organization_id,shipment_id,policy_version)
                VALUES ({firstReceipt},{fixture.A},{anotherShipment.Shipment},
                    {TrackingTimelinePolicy.Version})
                """));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, badApplication.SqlState);

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
        var duplicateChain = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO shipment_tracking_corrections
                    (id,organization_id,shipment_id,target_receipt_id,
                     replacement_receipt_id,generation,actor_user_id,reason,
                     correlation_id,correction_digest)
                VALUES ({Guid.NewGuid()},{fixture.A},{target.Shipment},{firstReceipt},
                        {secondReceipt},{2L},{actor},{"Invalid correction chain attempt"},
                        {"guard-check"},{new string('a', 64)})
                """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, duplicateChain.SqlState);
        var appendOnly = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE shipment_tracking_corrections
                SET reason={"Attempted evidence mutation"}
                WHERE organization_id={fixture.A} AND shipment_id={target.Shipment}
                """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, appendOnly.SqlState);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE shipment_tracking_projections SET observed_state={"Unknown"},
                verified_at_utc={DateTimeOffset.UtcNow.AddMinutes(-10)}
            WHERE organization_id={fixture.A} AND shipment_id={target.Shipment}
            """);
        var dirty = await store.DirtyAsync(50, CancellationToken.None);
        Assert.Contains(dirty, item => item.Organization == fixture.A &&
            item.Shipment == target.Shipment);
        var repaired = await store.RebuildAsync(fixture.A, target.Shipment,
            CancellationToken.None);
        Assert.Equal(TrackingMutationResult.Applied, repaired.Result);
        Assert.Equal(3, repaired.Revision);
        var recovered = await store.ReadAsync(fixture.A, target.Shipment,
            CancellationToken.None);
        Assert.NotNull(recovered);
        Assert.Equal("EquipmentIn", recovered!.State.ObservedTrackingState);
        Assert.False(recovered.MaterializationStale);


        var invalid = await store.CorrectAsync(fixture.A, target.Shipment,
            new TrackingCorrection(firstReceipt, null, 0, "Stale correction generation"),
            actor, "stale-correction", CancellationToken.None);
        Assert.Equal(TrackingMutationResult.Conflict, invalid.Result);
        Assert.Equal(3, (await store.RebuildAsync(fixture.A, target.Shipment,
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
        using (var forbidden = await SendM10(
            analyst, root + "/tracking-projection/rebuild"))
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using (var approved = await SendM10(
            manager, root + "/tracking-projection/rebuild"))
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        using (var staleCorrection = await SendM10(
            manager, root + "/tracking-corrections", new
            {
                targetReceiptId = firstReceipt, replacementReceiptId = (Guid?)null,
                expectedGeneration = 0, reason = "Invalid stale correction attempt"
            }))
            Assert.Equal(HttpStatusCode.Conflict, staleCorrection.StatusCode);
        var correctionIdempotency = Guid.NewGuid();
        var correctionRequest = new
        {
            targetReceiptId = firstReceipt, replacementReceiptId = (Guid?)null,
            expectedGeneration = 1, reason = "Confirmed invalid first observation"
        };
        using (var approvedCorrection = await SendM10(
            manager, root + "/tracking-corrections", correctionRequest, correctionIdempotency))
            Assert.Equal(HttpStatusCode.OK, approvedCorrection.StatusCode);
        using (var replayedCorrection = await SendM10(
            manager, root + "/tracking-corrections", correctionRequest, correctionIdempotency))
        {
            Assert.Equal(HttpStatusCode.OK, replayedCorrection.StatusCode);
            var replayBody = await replayedCorrection.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(replayBody.GetProperty("replayed").GetBoolean());
        }
        var afterHttp = await store.ReadAsync(fixture.A, target.Shipment, CancellationToken.None);
        Assert.NotNull(afterHttp);
        Assert.Equal("Unknown", afterHttp!.State.ObservedTrackingState);
        Assert.Equal(2, afterHttp.CorrectionGeneration);
        using (var redacted = await analyst.GetAsync(root + "/tracking-projection"))
        {
            Assert.Equal(HttpStatusCode.OK, redacted.StatusCode);
            var data = await redacted.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(data.TryGetProperty("inputDigest", out _));
            Assert.True(data.TryGetProperty("snapshotToken", out var snapshot));
            Assert.False(string.IsNullOrWhiteSpace(snapshot.GetString()));
        }
        using (var invalidCursor = await analyst.GetAsync(root + "/events?cursor=forged"))
            Assert.Equal(HttpStatusCode.Conflict, invalidCursor.StatusCode);
        using (var invalidSnapshot = await analyst.GetAsync(root + "/events?snapshot=forged"))
            Assert.Equal(HttpStatusCode.Conflict, invalidSnapshot.StatusCode);

    }

    [Fact]
    public void CanonicalTimelineKeyOrderingIsOrdinalAndDeterministic()
    {
        var utc = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
        var a = new ShipmentTimelineKey(utc, utc, Guid.Empty, "A",
            Guid.Empty, Guid.Empty);
        var b = a with { SourceEventId = "a" };
        var c = a with { SourceEventId = "B" };
        Assert.True(TrackingTimelinePolicy.Compare(a, b) < 0);
        Assert.True(TrackingTimelinePolicy.Compare(b, c) > 0);
        Assert.Equal(0, TrackingTimelinePolicy.Compare(a, a));
    }
}
