using System.Data;
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
            .OrderBy(e => e.ReceiptId)
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
        var pending = await store.ReadBoundedAsync(fixture.A, target.Shipment,
            CancellationToken.None, pageSize: 1);
        Assert.NotNull(pending);
        Assert.True(pending!.MaterializationStale);
        Assert.Equal("Unknown", pending.State.ObservedTrackingState);
        Assert.Single(pending.Timeline);
        Assert.True(pending.HasMore);
        Assert.Null(await store.ReadBoundedAsync(fixture.B, target.Shipment,
            CancellationToken.None));

        var firstBuild = await store.RebuildAsync(fixture.A, target.Shipment, CancellationToken.None);
        Assert.Equal(TrackingMutationResult.Applied, firstBuild.Result);
        Assert.Equal(1, firstBuild.Revision);
        var sameBuild = await store.RebuildAsync(fixture.A, target.Shipment, CancellationToken.None);
        Assert.Equal(1, sameBuild.Revision);
        var fresh = await store.ReadBoundedAsync(fixture.A, target.Shipment,
            CancellationToken.None, pageSize: 1);
        Assert.NotNull(fresh);
        Assert.False(fresh!.MaterializationStale);
        Assert.Equal("EquipmentOut", fresh.State.ObservedTrackingState);
        Assert.Single(fresh.Timeline);
        Assert.True(fresh.HasMore);


        string? earlierSnapshot;
        using (var response = await analyst.GetAsync(
            $"/api/v1/organizations/{fixture.A}/shipments/{target.Shipment}/tracking-projection"))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("EquipmentOut", json.GetProperty("observedTrackingState").GetString());
            Assert.True(json.GetProperty("etaUtc").ValueKind == JsonValueKind.Null);
            Assert.Equal(1, json.GetProperty("materializationRevision").GetInt64());
            earlierSnapshot = json.GetProperty("snapshotToken").GetString();
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
        using (var stalePage = await analyst.GetAsync(
            $"/api/v1/organizations/{fixture.A}/shipments/{target.Shipment}/events?limit=1&snapshot={Uri.EscapeDataString(earlierSnapshot!)}"))
            Assert.Equal(HttpStatusCode.Conflict, stalePage.StatusCode);

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
            .OrderBy(e => e.ReceiptId)
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
        var immutableReplay = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE shipment_tracking_operation_replays
                SET response_revision={999L}
                WHERE organization_id={fixture.A} AND shipment_id={target.Shipment}
                  AND idempotency_key={correctionIdempotency}
                """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, immutableReplay.SqlState);
        var immutableReplayDelete = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM shipment_tracking_operation_replays
                WHERE organization_id={fixture.A} AND shipment_id={target.Shipment}
                  AND idempotency_key={correctionIdempotency}
                """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, immutableReplayDelete.SqlState);
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
    public async Task ConcurrentDirectCorrectionWritersCannotCommitAnInvalidChain()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        using var admin = fixture.Admin;
        var target = await G9Support.SeedResolvable(factory, fixture);
        var source = await G9Support.NewSource(factory, fixture.A, "g10_concurrency");
        var prefix = Guid.NewGuid().ToString("N")[..12];
        var first = await G9Support.Accept(factory, source,
            G9Support.EquipmentPayload("race-first-" + prefix, target.Reference));
        var second = await G9Support.Accept(factory, source,
            G9Support.EquipmentPayload("race-second-" + prefix, target.Reference,
                occurredAt: "2026-11-03T11:00:00Z", code: "GTOT"));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var worker = new TrackingWorkStore(db, new TrackingWorkerSettings());
        for (var i = 0; i < 2; i++)
        {
            var claim = await worker.ClaimOneAsync(fixture.A, CancellationToken.None);
            Assert.NotNull(claim);
            Assert.Equal(TrackingWorkDisposition.Processed,
                await worker.FinalizeAsync(claim!, CancellationToken.None));
        }
        var actor = await db.AuthUsers.AsNoTracking().Select(x => x.Id).FirstAsync();
        await using var conA = new NpgsqlConnection(TestDatabase.ConnectionString());
        await using var conB = new NpgsqlConnection(TestDatabase.ConnectionString());
        await conA.OpenAsync();
        await conB.OpenAsync();
        await using var backend = new NpgsqlCommand("SELECT pg_backend_pid()", conB);
        var contenderPid = Convert.ToInt32(await backend.ExecuteScalarAsync());
        await using var txA = await conA.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using var txB = await conB.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        static NpgsqlCommand Insert(NpgsqlConnection con, NpgsqlTransaction tx,
            Guid org, Guid shipment, Guid targetReceipt, Guid replaceReceipt,
            Guid actorId, long generation)
        {
            var cmd = new NpgsqlCommand("""
                INSERT INTO shipment_tracking_corrections
                    (id,organization_id,shipment_id,target_receipt_id,replacement_receipt_id,
                     generation,actor_user_id,reason,correlation_id,correction_digest)
                VALUES (@id,@org,@shipment,@target,@replacement,
                        @generation,@actor,@reason,@correlation,@digest)
                """, con, tx) { CommandTimeout = 10 };
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("org", org);
            cmd.Parameters.AddWithValue("shipment", shipment);
            cmd.Parameters.AddWithValue("target", targetReceipt);
            cmd.Parameters.AddWithValue("replacement", replaceReceipt);
            cmd.Parameters.AddWithValue("generation", generation);
            cmd.Parameters.AddWithValue("actor", actorId);
            cmd.Parameters.AddWithValue("reason", "Validated synthetic concurrency correction");
            cmd.Parameters.AddWithValue("correlation", "g10-concurrency");
            cmd.Parameters.AddWithValue("digest", new string('a', 64));
            return cmd;
        }

        await using (var firstWrite = Insert(conA, txA, fixture.A, target.Shipment,
            first, second, actor, 1))
            await firstWrite.ExecuteNonQueryAsync();
        var otherWrite = Task.Run(async () =>
        {
            await using var cmd = Insert(conB, txB, fixture.A, target.Shipment,
                second, first, actor, 2);
            try
            {
                await cmd.ExecuteNonQueryAsync();
                await txB.CommitAsync();
                return false;
            }
            catch (PostgresException ex) when (ex.SqlState is
                PostgresErrorCodes.CheckViolation or PostgresErrorCodes.SerializationFailure
                or PostgresErrorCodes.UniqueViolation)
            {
                await txB.RollbackAsync();
                return true;
            }
        });
        // Observe the PostgreSQL lock wait, not a timing assumption.
        await using var watcher = new NpgsqlConnection(TestDatabase.ConnectionString());
        await watcher.OpenAsync();
        var observedLock = false;
        try
        {
            for (var poll = 0; poll < 150 && !observedLock; poll++)
            {
                await using var observation = new NpgsqlCommand("""
                    SELECT wait_event_type FROM pg_stat_activity WHERE pid=@pid
                    """, watcher);
                observation.Parameters.AddWithValue("pid", contenderPid);
                observedLock = (await observation.ExecuteScalarAsync()) is string name
                    && name == "Lock";
                if (!observedLock) await Task.Delay(50);
            }
        }
        finally
        {
            // Release the lock even if the assertion is about to fail.
            await txA.CommitAsync();
        }
        Assert.True(observedLock, "Second writer never reached a PostgreSQL lock wait");
        Assert.True(await otherWrite.WaitAsync(TimeSpan.FromSeconds(15)));
        await using var verify = new NpgsqlCommand("""
            SELECT count(*) FROM shipment_tracking_corrections
            WHERE organization_id=@org AND shipment_id=@shipment
            """, conA);
        verify.Parameters.AddWithValue("org", fixture.A);
        verify.Parameters.AddWithValue("shipment", target.Shipment);
        Assert.Equal(1L, Convert.ToInt64(await verify.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task DirtyDiscoveryAdvancesBeyondFirstShipmentWithoutWaitingForRebuild()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var a = await G9Support.SeedResolvable(factory, fixture);
        var b = await G9Support.SeedResolvable(factory, fixture);
        var source = await G9Support.NewSource(factory, fixture.A, "g10_fairness");
        var prefix = Guid.NewGuid().ToString("N")[..10];
        await G9Support.Accept(factory, source,
            G9Support.EquipmentPayload("fair-a-" + prefix, a.Reference));
        await G9Support.Accept(factory, source,
            G9Support.EquipmentPayload("fair-b-" + prefix, b.Reference));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var worker = new TrackingWorkStore(db, new TrackingWorkerSettings());
        for (var i = 0; i < 2; i++)
        {
            var claim = await worker.ClaimOneAsync(fixture.A, CancellationToken.None);
            Assert.NotNull(claim);
            Assert.Equal(TrackingWorkDisposition.Processed,
                await worker.FinalizeAsync(claim!, CancellationToken.None));
        }
        var store = new ShipmentTimelineStore(db);
        var all = await store.DirtyAsync(50, CancellationToken.None);
        Assert.Contains(all, item => item.Organization == fixture.A && item.Shipment == a.Shipment);
        Assert.Contains(all, item => item.Organization == fixture.A && item.Shipment == b.Shipment);
        var ordered = all.Where(item => item.Organization == fixture.A &&
            (item.Shipment == a.Shipment || item.Shipment == b.Shipment))
            .OrderBy(item => item.Shipment).ToArray();
        Assert.Equal(2, ordered.Length);
        var later = await store.DirtyAsync(50, CancellationToken.None,
            ordered[0].Organization, ordered[0].Shipment);
        Assert.Contains(later, item => item.Organization == ordered[1].Organization &&
            item.Shipment == ordered[1].Shipment);
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
