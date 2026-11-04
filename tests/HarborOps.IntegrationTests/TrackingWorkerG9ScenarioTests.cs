using System.Net;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

/// <summary>G9 scenarios that run against real PostgreSQL inside one test process.</summary>
public sealed class TrackingWorkerG9ScenarioTests
{
    private static string Key(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N")[..12];

    private static async Task<TrackingInboxWorkItem> Work(HarborOpsDbContext db, Guid receipt) =>
        await db.TrackingInboxWorkItems.AsNoTracking().SingleAsync(x => x.ReceiptId == receipt);

    private static async Task<int> Events(HarborOpsDbContext db, Guid receipt, string kind) =>
        await db.TrackingWorkAttemptEvents.CountAsync(x => x.ReceiptId == receipt && x.EventKind == kind);

    // G9-S08 and G9-S09: renewal boundaries, expiry, takeover and stale-worker fencing.
    [Fact]
    public async Task HeartbeatExtendsOnlyLiveLeaseAndExpiredLeaseIsFencedThenRecovered()
    {
        var connection = TestDatabase.ConnectionString();
        using var factory = new ReferenceDataHttpTests.ApiFactory(connection);
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var target = await G9Support.SeedResolvable(factory, fixture);
        var source = await G9Support.NewSource(factory, fixture.A, "g9_lease");
        var receipt = await G9Support.Accept(factory, source,
            G9Support.EquipmentPayload(Key("g9-lease"), target.Reference));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var store = new TrackingWorkStore(db, new TrackingWorkerSettings(LeaseSeconds: 10, HeartbeatSeconds: 2));

        var first = await store.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.NotNull(first);
        Assert.Equal(1, first!.Attempt);
        Assert.Equal(1L, first.LeaseEpoch);
        var before = (await Work(db, receipt)).LeaseExpiresAtUtc;
        Assert.NotNull(before);

        // Wrong token, epoch or generation never extends the lease.
        Assert.False(await store.HeartbeatAsync(first with { LeaseToken = Guid.NewGuid() }, CancellationToken.None));
        Assert.False(await store.HeartbeatAsync(first with { LeaseEpoch = first.LeaseEpoch + 1 }, CancellationToken.None));
        Assert.False(await store.HeartbeatAsync(first with { Generation = first.Generation + 1 }, CancellationToken.None));
        Assert.Equal(before, (await Work(db, receipt)).LeaseExpiresAtUtc);

        Assert.True(await store.HeartbeatAsync(first, CancellationToken.None));
        var renewed = (await Work(db, receipt)).LeaseExpiresAtUtc;
        Assert.True(renewed > before, "A live heartbeat must extend the database-issued expiry");

        await G9Support.WaitForLeaseExpiry(connection, receipt, TimeSpan.FromSeconds(30));

        // At or after expiry the old owner is fenced out of every write path.
        Assert.False(await store.HeartbeatAsync(first, CancellationToken.None));
        Assert.Equal(TrackingWorkDisposition.Stale, await store.FinalizeAsync(first, CancellationToken.None));
        Assert.False(await store.FailAsync(first, "transient_dependency", true, CancellationToken.None));
        Assert.Equal("Leased", (await Work(db, receipt)).State);
        Assert.Equal(0, await db.TrackingProcessingEffects.CountAsync(x => x.ReceiptId == receipt));

        var second = await store.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.NotNull(second);
        Assert.Equal(receipt, second!.ReceiptId);
        Assert.Equal(2L, second.LeaseEpoch);
        Assert.Equal(2, second.Attempt);
        Assert.NotEqual(first.LeaseToken, second.LeaseToken);

        // The stale owner still cannot act while a newer owner holds a live lease.
        Assert.False(await store.HeartbeatAsync(first, CancellationToken.None));
        Assert.Equal(TrackingWorkDisposition.Stale, await store.FinalizeAsync(first, CancellationToken.None));
        Assert.Equal(TrackingWorkDisposition.Processed, await store.FinalizeAsync(second, CancellationToken.None));
        Assert.Equal(TrackingWorkDisposition.Stale, await store.FinalizeAsync(second, CancellationToken.None));
        Assert.Equal(TrackingWorkDisposition.Stale, await store.FinalizeAsync(first, CancellationToken.None));

        Assert.Equal(1, await db.TrackingProcessingEffects.CountAsync(x => x.ReceiptId == receipt));
        Assert.Equal(1, await Events(db, receipt, "Claimed"));
        Assert.Equal(1, await Events(db, receipt, "LeaseRecovered"));
        Assert.Equal(1, await Events(db, receipt, "Processed"));
        var done = await Work(db, receipt);
        Assert.Equal("Processed", done.State);
        Assert.Equal(2, done.AttemptCount);
        Assert.Equal(2L, done.LeaseEpoch);
    }

    // G9-S16: deterministic finite retry schedule, no busy loop, exactly five claims, then DeadLetter.
    [Fact]
    public async Task TransientFailuresFollowBoundedScheduleAndStopAtFiveAttempts()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var source = await G9Support.NewSource(factory, fixture.A, "g9_retry");
        var receipt = await G9Support.Accept(factory, source,
            G9Support.EquipmentPayload(Key("g9-retry"), "SYN-BOX-UNKNOWN"));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var store = new TrackingWorkStore(db, new TrackingWorkerSettings());

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var claim = await G9Support.WaitForClaim(store, fixture.A, TimeSpan.FromSeconds(120));
            Assert.Equal(receipt, claim.ReceiptId);
            Assert.Equal(attempt, claim.Attempt);
            Assert.Equal((long)attempt, claim.LeaseEpoch);
            Assert.True(await store.FailAsync(claim, "transient_dependency", retryable: true, CancellationToken.None));
            var row = await Work(db, receipt);
            Assert.Equal(attempt, row.AttemptCount);
            if (attempt < 5)
            {
                Assert.Equal("RetryWaiting", row.State);
                var baseSeconds = Math.Min(900, 5 << (attempt - 1));
                var delay = (row.AvailableAtUtc - row.UpdatedAtUtc).TotalSeconds;
                Assert.InRange(delay, baseSeconds * 0.8 - 0.05, baseSeconds + 0.05);
                // The item is not yet due, so an immediate poll must not claim it.
                Assert.Null(await store.ClaimOneAsync(fixture.A, CancellationToken.None));
            }
            else
            {
                Assert.Equal("DeadLetter", row.State);
                Assert.NotNull(row.CompletedAtUtc);
            }
        }

        Assert.Null(await store.ClaimOneAsync(fixture.A, CancellationToken.None));
        Assert.Equal(5, await Events(db, receipt, "Claimed"));
        Assert.Equal(4, await Events(db, receipt, "Retried"));
        Assert.Equal(1, await Events(db, receipt, "DeadLettered"));
        Assert.Equal(1, await db.TrackingInboxReceipts.CountAsync(x => x.Id == receipt));
        Assert.Equal(1, await db.TrackingAcceptedEvents.CountAsync(x => x.ReceiptId == receipt));
        Assert.Equal(0, await db.TrackingProcessingEffects.CountAsync(x => x.ReceiptId == receipt));
    }

    // G9-S13: duplicate delivery of one original event never repeats the business resolution.
    [Fact]
    public async Task ReplayedDeliveryYieldsOneReceiptAndOneEffect()
    {
        var connection = TestDatabase.ConnectionString();
        using var factory = new ReferenceDataHttpTests.ApiFactory(connection);
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var target = await G9Support.SeedResolvable(factory, fixture);
        var source = await G9Support.NewSource(factory, fixture.A, "g9_replay");
        var body = G9Support.EquipmentPayload(Key("g9-replay"), target.Reference);

        var original = await G9Support.Submit(factory, source, body);
        Assert.Equal(HttpStatusCode.Accepted, original.Status);
        Assert.False(original.Duplicate);
        var receipt = original.Receipt!.Value;

        for (var i = 0; i < 12; i++)
        {
            var replay = await G9Support.Submit(factory, source, body);
            Assert.Equal(HttpStatusCode.Accepted, replay.Status);
            Assert.True(replay.Duplicate);
            Assert.Equal(receipt, replay.Receipt);
        }
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => G9Support.Submit(factory, source, body))));
        Assert.All(concurrent, x =>
        {
            Assert.Equal(HttpStatusCode.Accepted, x.Status);
            Assert.True(x.Duplicate);
            Assert.Equal(receipt, x.Receipt);
        });

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var store = new TrackingWorkStore(db, new TrackingWorkerSettings());
        var claim = await store.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.NotNull(claim);
        Assert.Equal(TrackingWorkDisposition.Processed, await store.FinalizeAsync(claim!, CancellationToken.None));

        // Replays after processing and a restarted claim loop cannot create a second effect.
        for (var i = 0; i < 10; i++)
        {
            var late = await G9Support.Submit(factory, source, body);
            Assert.True(late.Duplicate);
            Assert.Equal(receipt, late.Receipt);
        }
        Assert.Null(await store.ClaimOneAsync(fixture.A, CancellationToken.None));
        Assert.Equal(1, await db.TrackingInboxReceipts.CountAsync(x => x.SourceId == source.Id));
        Assert.Equal(1, await db.TrackingInboxWorkItems.CountAsync(x => x.SourceId == source.Id));
        Assert.Equal(1, await db.TrackingProcessingEffects.CountAsync(x => x.SourceId == source.Id));
        Assert.Equal(1, await Events(db, receipt, "Processed"));
    }

    // G9-S14, G9-S15 and G9-S07: tenant-safe, time-safe resolution and no guessed associations.
    [Fact]
    public async Task ReferenceResolutionIsTenantTimeSafeAndNeverGuessed()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var target = await G9Support.SeedResolvable(factory, fixture);
        var sourceA = await G9Support.NewSource(factory, fixture.A, "g9_ref_a");
        var sourceB = await G9Support.NewSource(factory, fixture.B, "g9_ref_b");

        string voyageCode;
        await using (var lookup = factory.Services.CreateAsyncScope())
        {
            var lookupDb = lookup.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            voyageCode = (await lookupDb.Voyages.AsNoTracking().SingleAsync(v => v.Id == target.Voyage)).ReferenceCode;
        }
        var sharedKey = Key("g9-shared-key");
        var resolved = await G9Support.Accept(factory, sourceA,
            G9Support.EquipmentPayload(sharedKey, target.Reference));
        var unknown = await G9Support.Accept(factory, sourceA,
            G9Support.EquipmentPayload(Key("g9-unknown"), "SYN-BOX-NOSUCH1"));
        var beforeWindow = await G9Support.Accept(factory, sourceA,
            G9Support.EquipmentPayload(Key("g9-early"), target.Reference, "2026-11-03T08:00:00Z"));
        // Same external key and the other tenant's real container identifier from a foreign source.
        var foreignSame = await G9Support.Accept(factory, sourceB,
            G9Support.EquipmentPayload(sharedKey, target.Reference));
        // An opaque transport token that equals an existing voyage reference must not be treated as one.
        var transport = await G9Support.Accept(factory, sourceA,
            G9Support.TransportPayload(Key("g9-transport"), voyageCode.ToUpperInvariant()));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var store = new TrackingWorkStore(db, new TrackingWorkerSettings());
        var outcomes = new Dictionary<Guid, TrackingWorkDisposition>();
        foreach (var organization in new[] { fixture.A, fixture.B })
        {
            while (await store.ClaimOneAsync(organization, CancellationToken.None) is { } claim)
            {
                Assert.Equal(organization, claim.OrganizationId);
                outcomes[claim.ReceiptId] = await store.FinalizeAsync(claim, CancellationToken.None);
            }
        }

        Assert.Equal(TrackingWorkDisposition.Processed, outcomes[resolved]);
        Assert.Equal(TrackingWorkDisposition.NeedsReconciliation, outcomes[unknown]);
        Assert.Equal(TrackingWorkDisposition.NeedsReconciliation, outcomes[beforeWindow]);
        Assert.Equal(TrackingWorkDisposition.NeedsReconciliation, outcomes[foreignSame]);
        Assert.Equal(TrackingWorkDisposition.NeedsReconciliation, outcomes[transport]);
        Assert.Equal(5, outcomes.Count);

        var effects = await db.TrackingProcessingEffects.AsNoTracking().ToListAsync();
        var effect = Assert.Single(effects, x => x.OrganizationId == fixture.A || x.OrganizationId == fixture.B);
        Assert.Equal(resolved, effect.ReceiptId);
        Assert.Equal(fixture.A, effect.OrganizationId);
        Assert.Equal(target.Container, effect.ContainerId);
        Assert.Equal(target.Shipment, effect.ShipmentId);
        foreach (var parked in new[] { unknown, beforeWindow, foreignSame, transport })
        {
            Assert.Equal(0, await db.TrackingProcessingEffects.CountAsync(x => x.ReceiptId == parked));
            Assert.Equal(1, await Events(db, parked, "ReconciliationRequired"));
            Assert.Equal("NeedsReconciliation", (await Work(db, parked)).State);
        }
        Assert.Equal(fixture.B, (await Work(db, foreignSame)).OrganizationId);
    }

    // G9-S21: a suspended organization is not processed, history is preserved, resumption proceeds once.
    [Fact]
    public async Task SuspendedOrganizationIsNotProcessedAndResumesExactlyOnce()
    {
        var connection = TestDatabase.ConnectionString();
        using var factory = new ReferenceDataHttpTests.ApiFactory(connection);
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var target = await G9Support.SeedResolvable(factory, fixture);
        var source = await G9Support.NewSource(factory, fixture.A, "g9_suspend");
        var held = await G9Support.Accept(factory, source,
            G9Support.EquipmentPayload(Key("g9-susp-held"), target.Reference));
        var waiting = await G9Support.Accept(factory, source,
            G9Support.EquipmentPayload(Key("g9-susp-wait"), target.Reference));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var store = new TrackingWorkStore(db, new TrackingWorkerSettings());
        var claim = await store.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.NotNull(claim);
        Assert.Equal(held, claim!.ReceiptId);

        async Task SetStatus(string status)
        {
            await using var direct = new NpgsqlConnection(connection);
            await direct.OpenAsync();
            await using var update = new NpgsqlCommand(
                "UPDATE organizations SET status=@status WHERE id=@id", direct);
            update.Parameters.AddWithValue("status", status);
            update.Parameters.AddWithValue("id", fixture.A);
            Assert.Equal(1, await update.ExecuteNonQueryAsync());
        }

        await SetStatus("Suspended");
        try
        {
            Assert.DoesNotContain(fixture.A, await store.CandidateOrganizationsAsync(null, CancellationToken.None));
            Assert.Null(await store.ClaimOneAsync(fixture.A, CancellationToken.None));
            Assert.False(await store.HeartbeatAsync(claim, CancellationToken.None));
            Assert.Equal(TrackingWorkDisposition.Stale, await store.FinalizeAsync(claim, CancellationToken.None));
            Assert.Equal(0, await db.TrackingProcessingEffects.CountAsync(x => x.OrganizationId == fixture.A));
            Assert.Equal("Pending", (await Work(db, waiting)).State);
            Assert.Equal(1, await db.TrackingInboxReceipts.CountAsync(x => x.Id == held));
            Assert.Equal(1, await db.TrackingAcceptedEvents.CountAsync(x => x.ReceiptId == waiting));
        }
        finally
        {
            await SetStatus("Active");
        }

        // The accepted receipts and the still-valid lease survive suspension without new credentials.
        Assert.Contains(fixture.A, await store.CandidateOrganizationsAsync(null, CancellationToken.None));
        Assert.Equal(TrackingWorkDisposition.Processed, await store.FinalizeAsync(claim, CancellationToken.None));
        var next = await store.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.NotNull(next);
        Assert.Equal(waiting, next!.ReceiptId);
        Assert.Equal(TrackingWorkDisposition.Processed, await store.FinalizeAsync(next, CancellationToken.None));
        Assert.Equal(2, await db.TrackingProcessingEffects.CountAsync(x => x.OrganizationId == fixture.A));
        Assert.Equal(1, await Events(db, held, "Processed"));
        Assert.Equal(1, await Events(db, waiting, "Processed"));
    }

    // G9-S03 and G9-S04: scoped SQL integrity, immutable evidence and one effect per receipt.
    [Fact]
    public async Task DirectSqlCannotForgeDuplicateOrTerminalWorkEvidence()
    {
        var connection = TestDatabase.ConnectionString();
        using var factory = new ReferenceDataHttpTests.ApiFactory(connection);
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var target = await G9Support.SeedResolvable(factory, fixture);
        var source = await G9Support.NewSource(factory, fixture.A, "g9_sql");
        var processed = await G9Support.Accept(factory, source,
            G9Support.EquipmentPayload(Key("g9-sql-done"), target.Reference));
        var claimedOnly = await G9Support.Accept(factory, source,
            G9Support.EquipmentPayload(Key("g9-sql-lease"), target.Reference));
        var dead = await G9Support.Accept(factory, source,
            G9Support.EquipmentPayload(Key("g9-sql-dead"), "SYN-BOX-UNKNOWN"));
        var pending = await G9Support.Accept(factory, source,
            G9Support.EquipmentPayload(Key("g9-sql-pending"), target.Reference));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var store = new TrackingWorkStore(db, new TrackingWorkerSettings());
        var first = await store.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.Equal(processed, first!.ReceiptId);
        Assert.Equal(TrackingWorkDisposition.Processed, await store.FinalizeAsync(first, CancellationToken.None));
        var second = await store.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.Equal(claimedOnly, second!.ReceiptId);
        var third = await store.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.Equal(dead, third!.ReceiptId);
        Assert.True(await store.FailAsync(third, "processor_error", false, CancellationToken.None));

        async Task<string> Sqlstate(FormattableString sql)
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(sql));
            return error.SqlState;
        }

        // Illegal transitions and deletes.
        Assert.Equal(PostgresErrorCodes.CheckViolation, await Sqlstate(
            $"UPDATE tracking_inbox_work_items SET state={"Leased"} WHERE receipt_id={processed}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await Sqlstate(
            $"UPDATE tracking_inbox_work_items SET state={"Processed"} WHERE receipt_id={dead}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await Sqlstate(
            $"UPDATE tracking_inbox_work_items SET state={"Processed"} WHERE receipt_id={pending}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await Sqlstate(
            $"DELETE FROM tracking_inbox_work_items WHERE receipt_id={processed}"));

        // A valid Leased to Processed transition cannot commit without its effect.
        Assert.Equal(PostgresErrorCodes.CheckViolation, await Sqlstate($"""
            UPDATE tracking_inbox_work_items
               SET state={"Processed"},lease_token=NULL,lease_expires_at_utc=NULL,
                   resolution_kind={"ReferenceResolved"},completed_at_utc=clock_timestamp(),
                   updated_at_utc=clock_timestamp()
             WHERE receipt_id={claimedOnly}
            """));
        Assert.Equal("Leased", (await Work(db, claimedOnly)).State);

        // A forged effect for a non-terminal receipt cannot commit.
        Assert.Equal(PostgresErrorCodes.CheckViolation, await Sqlstate($"""
            INSERT INTO tracking_processing_effects
                (receipt_id,organization_id,source_id,resolution_kind,resolution_policy,
                 container_id,shipment_id,resolved_at_utc)
            VALUES ({pending},{fixture.A},{source.Id},{"ReferenceResolved"},{"m9-reference-v1"},
                    {target.Container},{target.Shipment},clock_timestamp())
            """));
        // A second effect for a processed receipt is impossible.
        Assert.Equal(PostgresErrorCodes.UniqueViolation, await Sqlstate($"""
            INSERT INTO tracking_processing_effects
                (receipt_id,organization_id,source_id,resolution_kind,resolution_policy,
                 container_id,shipment_id,resolved_at_utc)
            VALUES ({processed},{fixture.A},{source.Id},{"ReferenceResolved"},{"m9-reference-v1"},
                    {target.Container},{target.Shipment},clock_timestamp())
            """));
        // A cross-tenant effect cannot reference the receipt through another organization. Either the
        // reference-validation guard (23514) or the scoped foreign key (23503) rejects it first.
        Assert.Contains(await Sqlstate($"""
            INSERT INTO tracking_processing_effects
                (receipt_id,organization_id,source_id,resolution_kind,resolution_policy,
                 container_id,shipment_id,resolved_at_utc)
            VALUES ({pending},{fixture.B},{source.Id},{"ReferenceResolved"},{"m9-reference-v1"},
                    {target.Container},{target.Shipment},clock_timestamp())
            """), new[] { PostgresErrorCodes.ForeignKeyViolation, PostgresErrorCodes.CheckViolation });

        // Evidence tables are append-only for every writer.
        Assert.Equal(PostgresErrorCodes.CheckViolation, await Sqlstate(
            $"DELETE FROM tracking_processing_effects WHERE receipt_id={processed}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await Sqlstate(
            $"DELETE FROM tracking_work_attempt_events WHERE receipt_id={processed}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, await Sqlstate(
            $"UPDATE tracking_processing_effects SET shipment_id={target.Shipment} WHERE receipt_id={processed}"));

        Assert.Equal(1, await db.TrackingProcessingEffects.CountAsync(x => x.OrganizationId == fixture.A));
        Assert.Equal("Pending", (await Work(db, pending)).State);
        Assert.Equal("DeadLetter", (await Work(db, dead)).State);
        Assert.Equal("Processed", (await Work(db, processed)).State);
    }

    // G9-S20: the unresolved ceiling counts every non-terminal state and holds under worker movement.
    [Fact]
    public async Task UnresolvedCeilingCountsLeasedRetryAndReconciliationAtTheExactLimit()
    {
        var connection = TestDatabase.ConnectionString();
        using var factory = new ReferenceDataHttpTests.ApiFactory(connection);
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var seeded = await G9Support.NewSource(factory, fixture.A, "g9_cap_seed");
        var live = await G9Support.NewSource(factory, fixture.A, "g9_cap_live");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();

        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = new NpgsqlCommand("""
                WITH seed AS MATERIALIZED (
                    SELECT gen_random_uuid() AS rid, 'bulk-g9-' || n::text AS external_id
                      FROM generate_series(1, 9997) AS n
                ),
                receipts AS (
                    INSERT INTO tracking_inbox_receipts
                        (id, organization_id, source_id, credential_id, profile,
                         source_event_id, canonical_digest, canonical_payload_utf8,
                         received_at_utc, correlation_id)
                    SELECT rid, @org, @source, @credential, 'tnt-2.2-harborops-v1',
                           external_id, decode(repeat('ab',32),'hex'),
                           convert_to('{"canonicalVersion":"m8-canonical-v1"}', 'UTF8'),
                           clock_timestamp(), 'bulk-g9'
                      FROM seed
                    RETURNING id, organization_id, source_id
                ),
                events AS (
                    INSERT INTO tracking_accepted_events
                        (id, receipt_id, organization_id, source_id, event_family, event_code,
                         classifier, external_schema_version, occurred_at_utc,
                         equipment_reference, raw_payload_utf8, canonical_payload)
                    SELECT gen_random_uuid(), r.id, r.organization_id, r.source_id,
                           'Equipment', 'GTIN', 'ACT', '2.2.0',
                           '2026-11-03T10:00:00Z'::timestamptz, 'SYN-BOX-ABCD',
                           convert_to('{"sourceEventId":"bulk"}','UTF8'),
                           '{"canonicalVersion":"m8-canonical-v1"}'::jsonb
                    FROM receipts r
                    RETURNING receipt_id
                )
                INSERT INTO tracking_inbox_work_items
                    (receipt_id, organization_id, source_id, state, attempt_count,
                     created_at_utc, updated_at_utc)
                SELECT r.id, r.organization_id, r.source_id, 'Pending', 0, now(), now()
                  FROM receipts r JOIN events e ON e.receipt_id = r.id
                """, (NpgsqlConnection)db.Database.GetDbConnection());
            command.CommandTimeout = 180;
            command.Parameters.AddWithValue("org", fixture.A);
            command.Parameters.AddWithValue("source", seeded.Id);
            command.Parameters.AddWithValue("credential", seeded.CredentialId);
            Assert.Equal(9997, await command.ExecuteNonQueryAsync());
        }
        finally { await db.Database.CloseConnectionAsync(); }

        Task<int> Unresolved() => db.TrackingInboxWorkItems.CountAsync(x => x.OrganizationId == fixture.A &&
            (x.State == "Pending" || x.State == "Leased" || x.State == "RetryWaiting" ||
             x.State == "NeedsReconciliation"));

        var keys = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var key = Key("g9-cap-live" + i);
            keys.Add(key);
            var accepted = await G9Support.Submit(factory, live, G9Support.EquipmentPayload(key, "SYN-BOX-ABCD"));
            Assert.Equal(HttpStatusCode.Accepted, accepted.Status);
        }
        Assert.Equal(10000, await Unresolved());
        async Task AssertRefused(string label) => Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await G9Support.Submit(factory, live, G9Support.EquipmentPayload(Key(label), "SYN-BOX-ABCD"))).Status);

        await AssertRefused("g9-cap-refused-pending");
        // An identical replay is canonical and bypasses capacity.
        var replay = await G9Support.Submit(factory, live, G9Support.EquipmentPayload(keys[0], "SYN-BOX-ABCD"));
        Assert.Equal(HttpStatusCode.Accepted, replay.Status);
        Assert.True(replay.Duplicate);

        var store = new TrackingWorkStore(db, new TrackingWorkerSettings());
        var leased = await store.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.NotNull(leased);
        Assert.Equal(10000, await Unresolved());
        await AssertRefused("g9-cap-refused-leased");

        var parked = await store.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.Equal(TrackingWorkDisposition.NeedsReconciliation,
            await store.FinalizeAsync(parked!, CancellationToken.None));
        Assert.Equal(10000, await Unresolved());
        await AssertRefused("g9-cap-refused-reconciliation");

        var retrying = await store.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.True(await store.FailAsync(retrying!, "transient_dependency", true, CancellationToken.None));
        Assert.Equal(10000, await Unresolved());
        await AssertRefused("g9-cap-refused-retry");

        // A terminal outcome frees exactly one slot while a competing intake races the worker.
        var ending = await store.ClaimOneAsync(fixture.A, CancellationToken.None);
        Assert.NotNull(ending);
        var contenderKey = Key("g9-cap-race");
        var contender = G9Support.Submit(factory, live, G9Support.EquipmentPayload(contenderKey, "SYN-BOX-ABCD"));
        var finisher = Task.Run(async () =>
        {
            await using var workerScope = factory.Services.CreateAsyncScope();
            var workerDb = workerScope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            return await new TrackingWorkStore(workerDb, new TrackingWorkerSettings())
                .FailAsync(ending!, "processor_error", false, CancellationToken.None);
        });
        var results = await Task.WhenAll(contender, finisher.ContinueWith(t => (HttpStatusCode.OK, (Guid?)null, t.Result)));
        Assert.True(await finisher);
        var contenderAccepted = results[0].Item1 == HttpStatusCode.Accepted;
        Assert.True(results[0].Item1 is HttpStatusCode.Accepted or HttpStatusCode.ServiceUnavailable);
        Assert.Equal(contenderAccepted ? 10000 : 9999, await Unresolved());
        Assert.Equal("DeadLetter", (await Work(db, ending!.ReceiptId)).State);
        if (!contenderAccepted)
        {
            // The freed slot is usable exactly once afterwards.
            var late = await G9Support.Submit(factory, live, G9Support.EquipmentPayload(contenderKey, "SYN-BOX-ABCD"));
            Assert.Equal(HttpStatusCode.Accepted, late.Status);
        }
        Assert.Equal(10000, await Unresolved());
        await AssertRefused("g9-cap-refused-final");
    }
}
