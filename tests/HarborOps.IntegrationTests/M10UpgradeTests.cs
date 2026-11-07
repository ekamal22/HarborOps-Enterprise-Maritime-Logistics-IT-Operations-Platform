using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

/// <summary>
/// M10 migration behavior against populated M8/M9/M16 history in isolated schemas: the
/// historical backfill, preservation of accepted evidence and the protected downgrade.
/// </summary>
public sealed class M10UpgradeTests
{
    private const string M10 = "20261105180000_ShipmentTrackingProjections";
    private const string M9 = "20261104190000_TrackingWorker";

    // Mirrors the additive M10 objects only. It exists solely to recreate the "populated M9/M16
    // database before M10" state from a populated head, so the real Up backfill can be run
    // against real history. It never touches M8, M9 or M16 tables or rows.
    private const string RemoveM10Objects = """
        DROP TRIGGER tr_m10_effect_input_revision ON tracking_processing_effects;
        DROP TRIGGER tr_m10_correction_input_revision ON shipment_tracking_corrections;
        DROP FUNCTION m10_track_input_change();
        DROP FUNCTION m10_lock_projector_scope(uuid,uuid);
        DROP TABLE shipment_tracking_operation_replays;
        DROP TABLE shipment_tracking_timeline_entries;
        DROP TABLE shipment_projection_applications;
        DROP TABLE shipment_tracking_corrections;
        DROP TABLE shipment_tracking_correction_heads;
        DROP TABLE shipment_tracking_projections;
        DROP TABLE shipment_tracking_input_versions;
        DROP FUNCTION m10_validate_timeline_entry();
        DROP FUNCTION m10_validate_correction();
        DROP FUNCTION m10_append_only();
        DROP INDEX uq_m10_effect_scope;
        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId"='20261105180000_ShipmentTrackingProjections';
        """;

    private static string Scoped(string schema) => new NpgsqlConnectionStringBuilder(
        TestDatabase.ConnectionString()) { SearchPath = schema + ",public" }.ConnectionString;

    private static async Task<object?> Scalar(string connection, string sql)
    {
        await using var direct = new NpgsqlConnection(connection);
        await direct.OpenAsync();
        await using var command = new NpgsqlCommand(sql, direct);
        return await command.ExecuteScalarAsync();
    }

    private static async Task Run(string connection, string sql)
    {
        await using var direct = new NpgsqlConnection(connection);
        await direct.OpenAsync();
        await using var command = new NpgsqlCommand(sql, direct);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> Fingerprint(string connection) => (string)(await Scalar(connection, """
        SELECT concat_ws('/',
          (SELECT md5(COALESCE(string_agg(x::text,'|' ORDER BY x::text),'')) FROM tracking_inbox_receipts x),
          (SELECT md5(COALESCE(string_agg(x::text,'|' ORDER BY x::text),'')) FROM tracking_accepted_events x),
          (SELECT md5(COALESCE(string_agg(x::text,'|' ORDER BY x::text),'')) FROM tracking_inbox_work_items x),
          (SELECT md5(COALESCE(string_agg(x::text,'|' ORDER BY x::text),'')) FROM tracking_work_attempt_events x),
          (SELECT md5(COALESCE(string_agg(x::text,'|' ORDER BY x::text),'')) FROM tracking_processing_effects x),
          (SELECT md5(COALESCE(string_agg(x::text,'|' ORDER BY x::text),'')) FROM audit_entries x))
        """))!;

    [Fact]
    public async Task PopulatedHistoryIsBackfilledByM10WithoutAlteringAcceptedEvidenceAndDowngradeIsProtected()
    {
        var schema = "m10_upgrade_" + Guid.NewGuid().ToString("N");
        var empty = "m10_empty_" + Guid.NewGuid().ToString("N");
        var scoped = Scoped(schema);
        await using var admin = new NpgsqlConnection(TestDatabase.ConnectionString());
        await admin.OpenAsync();
        foreach (var name in new[] { schema, empty })
            await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{name}\"", admin))
                await create.ExecuteNonQueryAsync();
        try
        {
            using var factory = new ReferenceDataHttpTests.ApiFactory(scoped);
            var fixture = await ReferenceDataHttpTests.Setup(factory);
            using var a = fixture.Admin;
            using var m = fixture.Manager;
            using var n = fixture.Analyst;
            var first = await G9Support.SeedResolvable(factory, fixture);
            var second = await G9Support.SeedResolvable(factory, fixture);
            var source = await G9Support.NewSource(factory, fixture.A, "m10up");
            var prefix = Guid.NewGuid().ToString("N")[..8];
            foreach (var (key, target, at, code) in new[]
            {
                ("up-1", first, "2026-11-03T10:00:00Z", "GTIN"),
                ("up-2", first, "2026-11-03T11:00:00Z", "GTOT"),
                ("up-3", first, "2026-11-03T12:00:00Z", "GTIN"),
                ("up-4", second, "2026-11-03T10:30:00Z", "GTIN")
            })
                await G9Support.Accept(factory, source,
                    G9Support.EquipmentPayload(key + "-" + prefix, target.Reference, at, code));
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
                var worker = new TrackingWorkStore(db, new TrackingWorkerSettings());
                for (var i = 0; i < 4; i++)
                {
                    var claim = await worker.ClaimOneAsync(fixture.A, CancellationToken.None);
                    Assert.NotNull(claim);
                    Assert.Equal(TrackingWorkDisposition.Processed,
                        await worker.FinalizeAsync(claim!, CancellationToken.None));
                }
            }
            Assert.Equal(4L, await Scalar(scoped, "SELECT COUNT(*) FROM tracking_processing_effects"));
            Assert.Equal(4L, await Scalar(scoped, "SELECT COUNT(*) FROM shipment_tracking_timeline_entries"));
            var evidenceBefore = await Fingerprint(scoped);

            // Roll back only the new correction-authority layer, then recreate the
            // populated pre-M10 state without removing retained accepted evidence.
            await using (var prerequisite = new HarborOpsDbContext(
                new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(scoped).Options))
                await prerequisite.Database.MigrateAsync(M10);
            await Run(scoped, RemoveM10Objects);
            Assert.Equal(false, await Scalar(scoped,
                $"SELECT to_regclass('\"{schema}\".shipment_tracking_timeline_entries') IS NOT NULL"));
            Assert.Equal(evidenceBefore, await Fingerprint(scoped));

            await using var db2 = new HarborOpsDbContext(
                new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(scoped).Options);
            Assert.Equal(11, (await db2.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Contains(M10, await db2.Database.GetPendingMigrationsAsync());
            await db2.Database.MigrateAsync();
            Assert.Equal(13, (await db2.Database.GetAppliedMigrationsAsync()).Count());
            Assert.False(db2.Database.HasPendingModelChanges());

            // Backfill: one immutable entry per historical effect, matching the M8 event exactly.
            Assert.Equal(4L, await Scalar(scoped, "SELECT COUNT(*) FROM shipment_tracking_timeline_entries"));
            Assert.Equal(0L, await Scalar(scoped, """
                SELECT COUNT(*) FROM tracking_processing_effects fx
                LEFT JOIN shipment_tracking_timeline_entries t ON t.receipt_id=fx.receipt_id
                  AND t.organization_id=fx.organization_id AND t.shipment_id=fx.shipment_id
                  AND t.container_id=fx.container_id
                WHERE t.receipt_id IS NULL
                """));
            Assert.Equal(0L, await Scalar(scoped, """
                SELECT COUNT(*) FROM shipment_tracking_timeline_entries t
                JOIN tracking_accepted_events e ON e.receipt_id=t.receipt_id
                JOIN tracking_inbox_receipts r ON r.id=t.receipt_id
                WHERE t.event_id<>e.id OR t.event_code<>e.event_code
                   OR t.occurred_at_utc<>e.occurred_at_utc OR t.received_at_utc<>r.received_at_utc
                   OR t.source_event_id<>r.source_event_id OR t.source_id<>e.source_id
                """));
            // Input versions equal the historical effect count per shipment.
            Assert.Equal(0L, await Scalar(scoped, """
                SELECT COUNT(*) FROM (
                    SELECT organization_id,shipment_id,COUNT(*) AS n
                    FROM tracking_processing_effects GROUP BY organization_id,shipment_id) h
                LEFT JOIN shipment_tracking_input_versions v
                  ON v.organization_id=h.organization_id AND v.shipment_id=h.shipment_id
                WHERE v.input_revision IS DISTINCT FROM h.n OR v.event_count IS DISTINCT FROM h.n
                """));
            Assert.Equal(2L, await Scalar(scoped, "SELECT COUNT(*) FROM shipment_tracking_input_versions"));
            Assert.Equal(evidenceBefore, await Fingerprint(scoped));

            // The backfilled history is immutable like newly written history.
            var update = await Assert.ThrowsAsync<PostgresException>(() => Run(scoped,
                "UPDATE shipment_tracking_timeline_entries SET event_code='GTOT'"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, update.SqlState);
            var delete = await Assert.ThrowsAsync<PostgresException>(() => Run(scoped,
                "DELETE FROM shipment_tracking_timeline_entries"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, delete.SqlState);

            // Retained history blocks the downgrade; nothing is lost or partially dropped.
            var blocked = await Assert.ThrowsAsync<PostgresException>(() =>
                db2.Database.MigrateAsync(M9));
            Assert.Equal(PostgresErrorCodes.CheckViolation, blocked.SqlState);
            Assert.Contains("M10 retained history prevents downgrade", blocked.MessageText);
            // EF reverts one migration per transaction: the thirteenth (correction authority, no
            // retained nonce evidence here) was reverted cleanly, the twelfth is blocked and kept.
            Assert.Equal(12, (await db2.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(4L, await Scalar(scoped, "SELECT COUNT(*) FROM shipment_tracking_timeline_entries"));
            Assert.Equal(evidenceBefore, await Fingerprint(scoped));

            // Control: with no M10 rows the guarded downgrade succeeds and re-upgrade is clean.
            var emptyScoped = Scoped(empty);
            await using var db3 = new HarborOpsDbContext(
                new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(emptyScoped).Options);
            await db3.Database.MigrateAsync();
            Assert.Equal(13, (await db3.Database.GetAppliedMigrationsAsync()).Count());
            await db3.Database.MigrateAsync(M9);
            Assert.Equal(11, (await db3.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(false, await Scalar(emptyScoped,
                $"SELECT to_regclass('\"{empty}\".shipment_tracking_timeline_entries') IS NOT NULL"));
            await db3.Database.MigrateAsync();
            Assert.Equal(13, (await db3.Database.GetAppliedMigrationsAsync()).Count());
            Assert.False(db3.Database.HasPendingModelChanges());

            // Retained correction-authority evidence blocks the downgrade of the thirteenth migration.
            await Run(emptyScoped, """
                INSERT INTO m10_correction_proof_nonces
                    (nonce,organization_id,shipment_id,actor_user_id,idempotency_key,
                     request_digest,expires_at_utc)
                VALUES (gen_random_uuid(),gen_random_uuid(),gen_random_uuid(),gen_random_uuid(),
                        gen_random_uuid(),repeat('a',64),clock_timestamp())
                """);
            var nonceGuard = await Assert.ThrowsAsync<PostgresException>(() =>
                db3.Database.MigrateAsync(M10));
            Assert.Equal(PostgresErrorCodes.CheckViolation, nonceGuard.SqlState);
            Assert.Contains("Retained correction authority evidence", nonceGuard.MessageText);
            Assert.Equal(13, (await db3.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Equal(1L, await Scalar(emptyScoped, "SELECT COUNT(*) FROM m10_correction_proof_nonces"));
        }
        finally
        {
            foreach (var name in new[] { schema, empty })
                await using (var drop = new NpgsqlCommand($"DROP SCHEMA \"{name}\" CASCADE", admin))
                    await drop.ExecuteNonQueryAsync();
        }
    }
}
