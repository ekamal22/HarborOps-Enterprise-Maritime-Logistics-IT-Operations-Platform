using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HarborOps.Infrastructure.Shipments;

public sealed record ShipmentTrackingView(
    ShipmentTrackingState State, long MaterializationRevision, long CorrectionGeneration,
    bool MaterializationStale, DateTimeOffset? MaterializedAtUtc,
    IReadOnlyList<ShipmentTimelineEvent> Timeline, bool HasMore = false);

public enum TrackingMutationResult { Applied, Replayed, NotFound, Conflict, Invalid, Forbidden }

public sealed record TrackingCorrection(Guid TargetReceiptId, Guid? ReplacementReceiptId,
    long ExpectedGeneration, string Reason);

public sealed class ShipmentTimelineStore(HarborOpsDbContext db, bool restrictedProjector = false)
{
    private const string OrderedSourceEvents = """
        SELECT e.event_id,e.receipt_id,e.source_id,e.source_event_id,e.event_code,
               e.occurred_at_utc,e.received_at_utc,c.id IS NOT NULL,
               c.replacement_receipt_id,e.container_id,c.id
        FROM shipment_tracking_timeline_entries e
        LEFT JOIN shipment_tracking_corrections c
          ON c.organization_id=e.organization_id
          AND c.shipment_id=e.shipment_id
          AND c.target_receipt_id=e.receipt_id
        WHERE e.organization_id=@org AND e.shipment_id=@shipment
        ORDER BY e.occurred_at_utc,e.received_at_utc,e.source_id,
                 e.source_event_id COLLATE "C",e.receipt_id,e.event_id
        """;

    private static NpgsqlCommand Command(NpgsqlConnection con, NpgsqlTransaction? tx,
        string sql, params (string Name, object? Value)[] args)
    {
        var cmd = new NpgsqlCommand(sql, con, tx) { CommandTimeout = 60 };
        foreach (var (name, value) in args)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }
    private NpgsqlConnection Connection => (NpgsqlConnection)db.Database.GetDbConnection();

    private static async Task<bool> ExistsAsync(NpgsqlConnection con, NpgsqlTransaction tx,
        Guid org, Guid shipment, CancellationToken ct)
    {
        await using var cmd = Command(con, tx, """
            SELECT EXISTS(SELECT 1 FROM shipments s JOIN organizations o
                ON o.id=s.organization_id
                WHERE s.organization_id=@org AND s.id=@shipment
                  AND s.record_status='Active' AND o.status='Active')
            """, ("org", org), ("shipment", shipment));
        return await cmd.ExecuteScalarAsync(ct) is true;
    }

    // Consume immutable history without retaining every event: memory is bounded
    // by the requested page and the number of distinct resolved containers. Materialization never depends on a 10,000-row truncation.
    private static async Task<(ShipmentTrackingState State,
        IReadOnlyList<ShipmentTimelineEvent> Page, bool HasMore)> StreamAsync(
        NpgsqlConnection con, NpgsqlTransaction tx, Guid org, Guid shipment,
        int pageSize, ShipmentTimelineKey? after, CancellationToken ct)
    {
        if (pageSize is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var page = new List<ShipmentTimelineEvent>(pageSize);
        var hasMore = false;
        using var accumulator = new ShipmentTrackingAccumulator(org, shipment);
        await using var cmd = Command(con, tx, OrderedSourceEvents,
            ("org", org), ("shipment", shipment));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var e = new ShipmentTimelineEvent(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2),
                reader.GetString(3), reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.GetFieldValue<DateTimeOffset>(6), reader.GetBoolean(7),
                reader.IsDBNull(8) ? null : reader.GetGuid(8), reader.GetGuid(9),
                reader.IsDBNull(10) ? null : reader.GetGuid(10));
            accumulator.Add(e);
            if (pageSize == 0 || after is not null &&
                TrackingTimelinePolicy.Compare(ShipmentTimelineKey.Of(e), after) <= 0)
                continue;
            if (page.Count < pageSize) page.Add(e);
            else hasMore = true;
        }
        return (accumulator.Complete(), page, hasMore);
    }

    private sealed record ProjectionCache(
        long Revision, string? Digest, DateTimeOffset? At, string? ObservedState,
        Guid? LastEvent, DateTimeOffset? LastOccurred, long CorrectionGeneration,
        string? PolicyVersion, long InputRevision, long EventCount,
        int ContainerCount, string? ContainerJson);

    private static async Task<ProjectionCache> CachedAsync(
        NpgsqlConnection con, NpgsqlTransaction tx,
        Guid org, Guid shipment, CancellationToken ct)
    {
        await using var cmd = Command(con, tx, """
            SELECT materialization_revision,input_digest,refreshed_at_utc,
                   observed_state,last_event_id,last_occurred_at_utc,
                   correction_generation,policy_version,input_revision,event_count,
                   container_count,container_observations_json::text
            FROM shipment_tracking_projections
            WHERE organization_id=@org AND shipment_id=@shipment
            """, ("org", org), ("shipment", shipment));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new ProjectionCache(0, null, null, null, null, null, 0, null, 0, 0, 0, null);
        return new ProjectionCache(
            reader.GetInt64(0), reader.GetString(1).Trim(),
            reader.GetFieldValue<DateTimeOffset>(2), reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
            reader.GetInt64(6), reader.GetString(7), reader.GetInt64(8),
            reader.GetInt64(9), reader.GetInt32(10), reader.GetString(11));
    }

    private sealed record InputVersion(long Revision, long EventCount);

    private static async Task<InputVersion> VersionAsync(NpgsqlConnection con,
        NpgsqlTransaction tx, Guid org, Guid shipment, CancellationToken ct)
    {
        await using var cmd = Command(con, tx, """
            SELECT input_revision,event_count FROM shipment_tracking_input_versions
            WHERE organization_id=@org AND shipment_id=@shipment
            """, ("org", org), ("shipment", shipment));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return new InputVersion(0, 0);
        return new InputVersion(reader.GetInt64(0), reader.GetInt64(1));
    }

    // Unlike StreamAsync, this query uses the database index and fetches at
    // most 101 rows. The complete history is processed only during rebuild.
    private static async Task<(IReadOnlyList<ShipmentTimelineEvent>, bool)> PageAsync(
        NpgsqlConnection con, NpgsqlTransaction tx, Guid org, Guid shipment,
        int pageSize, ShipmentTimelineKey? after, CancellationToken ct)
    {
        if (pageSize == 0) return (Array.Empty<ShipmentTimelineEvent>(), false);
        if (pageSize is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var sql = """
            SELECT e.event_id,e.receipt_id,e.source_id,e.source_event_id,e.event_code,
                   e.occurred_at_utc,e.received_at_utc,c.id IS NOT NULL,
                   c.replacement_receipt_id,e.container_id,c.id
            FROM shipment_tracking_timeline_entries e
            LEFT JOIN shipment_tracking_corrections c
                ON c.organization_id=e.organization_id
                AND c.shipment_id=e.shipment_id
                AND c.target_receipt_id=e.receipt_id
            WHERE e.organization_id=@org AND e.shipment_id=@shipment
            """;
        var arguments = new List<(string Name, object? Value)>
        {
            ("org", org), ("shipment", shipment), ("take", pageSize + 1)
        };
        if (after is not null)
        {
            sql += """
                
                AND (e.occurred_at_utc,e.received_at_utc,e.source_id,
                     e.source_event_id COLLATE "C",e.receipt_id,e.event_id)
                  > (@afterOccurred,@afterReceived,@afterSource,
                     @afterEvent COLLATE "C",@afterReceipt,@afterId)
                """;
            arguments.Add(("afterOccurred", after.OccurredAtUtc));
            arguments.Add(("afterReceived", after.ReceivedAtUtc));
            arguments.Add(("afterSource", after.SourceId));
            arguments.Add(("afterEvent", after.SourceEventId));
            arguments.Add(("afterReceipt", after.ReceiptId));
            arguments.Add(("afterId", after.EventId));
        }
        sql += """
            
            ORDER BY e.occurred_at_utc,e.received_at_utc,e.source_id,
                     e.source_event_id COLLATE "C",e.receipt_id,e.event_id
            LIMIT @take
            """;
        await using var cmd = Command(con, tx, sql, arguments.ToArray());
        var rows = new List<ShipmentTimelineEvent>(pageSize + 1);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(new ShipmentTimelineEvent(
                reader.GetGuid(0),reader.GetGuid(1),reader.GetGuid(2),
                reader.GetString(3),reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.GetFieldValue<DateTimeOffset>(6),reader.GetBoolean(7),
                reader.IsDBNull(8)?null:reader.GetGuid(8),reader.GetGuid(9),
                reader.IsDBNull(10)?null:reader.GetGuid(10)));
        var more = rows.Count > pageSize;
        if (more) rows.RemoveAt(rows.Count - 1);
        return (rows, more);
    }

    // HTTP read model: bounded database page plus a versioned materialization.
    // On stale source input, never claim that cached tracking state is current.
    public async Task<ShipmentTrackingView?> ReadBoundedAsync(
        Guid org, Guid shipment, CancellationToken ct, int pageSize = 0,
        ShipmentTimelineKey? after = null)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var tx = (NpgsqlTransaction)await Connection.BeginTransactionAsync(
                IsolationLevel.RepeatableRead, ct);
            if (!await ExistsAsync(Connection, tx, org, shipment, ct)) return null;
            var input = await VersionAsync(Connection, tx, org, shipment, ct);
            var cached = await CachedAsync(Connection, tx, org, shipment, ct);
            var generation = await GenerationAsync(Connection, tx, org, shipment, ct);
            var (page, more) = await PageAsync(Connection, tx, org, shipment, pageSize, after, ct);
            await tx.CommitAsync(ct);
            var stale = cached.Revision == 0 || cached.InputRevision != input.Revision ||
                cached.CorrectionGeneration != generation ||
                cached.PolicyVersion != TrackingTimelinePolicy.Version;
            var token = input.Revision.ToString("x16", CultureInfo.InvariantCulture)
                .PadLeft(64, '0');
            IReadOnlyList<ContainerTrackingObservation> containerSummary = stale
                ? Array.Empty<ContainerTrackingObservation>()
                : JsonSerializer.Deserialize<ContainerTrackingObservation[]>(
                    cached.ContainerJson ?? "[]") ?? Array.Empty<ContainerTrackingObservation>();
            var state = new ShipmentTrackingState(
                stale ? "Unknown" : cached.ObservedState ?? "Unknown",
                stale ? null : cached.LastEvent, stale ? null : cached.LastOccurred,
                token, TrackingTimelinePolicy.Version,
                (int)Math.Min(input.EventCount, int.MaxValue), containerSummary);
            return new ShipmentTrackingView(state, cached.Revision, generation,
                stale, cached.At, page, more);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    private static async Task<long> GenerationAsync(NpgsqlConnection con, NpgsqlTransaction tx,
        Guid org, Guid shipment, CancellationToken ct)
    {
        await using var cmd = Command(con, tx, """
            SELECT COALESCE(MAX(generation),0) FROM shipment_tracking_corrections
            WHERE organization_id=@org AND shipment_id=@shipment
            """, ("org", org), ("shipment", shipment));
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
    }

    public async Task<ShipmentTrackingView?> ReadAsync(Guid org, Guid shipment, CancellationToken ct,
        int pageSize = 100, ShipmentTimelineKey? after = null)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var tx = (NpgsqlTransaction)await Connection.BeginTransactionAsync(
                IsolationLevel.RepeatableRead, ct);
            if (!await ExistsAsync(Connection, tx, org, shipment, ct)) return null;
            var (state, page, hasMore) =
                await StreamAsync(Connection, tx, org, shipment, pageSize, after, ct);
            var cached = await CachedAsync(Connection, tx, org, shipment, ct);
            var generation = await GenerationAsync(Connection, tx, org, shipment, ct);
            await tx.CommitAsync(ct);
            var stale = cached.Digest != state.InputDigest ||
                cached.ObservedState != state.ObservedTrackingState ||
                cached.LastEvent != state.LastEventId ||
                cached.LastOccurred != state.LastOccurredAtUtc ||
                cached.CorrectionGeneration != generation ||
                cached.PolicyVersion != state.PolicyVersion;
            return new ShipmentTrackingView(state, cached.Revision, generation,
                stale, cached.At, page, hasMore);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    private static async Task<bool> LockScopeAsync(NpgsqlConnection con, NpgsqlTransaction tx,
        Guid org, Guid shipment, bool restrictedProjector, CancellationToken ct)
    {
        if (restrictedProjector)
        {
            await using var guarded = Command(con, tx,
                "SELECT m10_lock_projector_scope(@org,@shipment)",
                ("org", org), ("shipment", shipment));
            return await guarded.ExecuteScalarAsync(ct) is true;
        }
        await using (var cmd = Command(con, tx,
            "SELECT status FROM organizations WHERE id=@org FOR UPDATE", ("org", org)))
        {
            if (await cmd.ExecuteScalarAsync(ct) is not string status || status != "Active")
                return false;
        }
        await using var shipmentCommand = Command(con, tx, """
            SELECT record_status FROM shipments
            WHERE organization_id=@org AND id=@shipment FOR UPDATE
            """, ("org", org), ("shipment", shipment));
        return await shipmentCommand.ExecuteScalarAsync(ct) is "Active";
    }

    private static async Task<bool> CurrentManagerAsync(NpgsqlConnection con,
        NpgsqlTransaction tx, Guid org, Guid actor, Guid session, CancellationToken ct)
    {
        await using var cmd = Command(con, tx, """
            SELECT EXISTS (
                SELECT 1 FROM auth_sessions sess
                JOIN users u ON u.id=sess.user_id
                JOIN organization_memberships member ON
                     member.user_id=sess.user_id
                 AND member.organization_id=sess.selected_organization_id
                WHERE sess.id=@session AND sess.user_id=@actor
                  AND sess.selected_organization_id=@org
                  AND sess.revoked_at_utc IS NULL
                  AND sess.idle_expires_at_utc > clock_timestamp()
                  AND sess.absolute_expires_at_utc > clock_timestamp()
                  AND u.status='Active' AND u.security_version=sess.security_version
                  AND member.status='Active' AND member.role='Manager'
                FOR SHARE OF sess,u,member
            )
            """, ("session", session), ("actor", actor), ("org", org));
        return await cmd.ExecuteScalarAsync(ct) is true;
    }

    private static async Task<long> PersistAsync(NpgsqlConnection con, NpgsqlTransaction tx,
        Guid org, Guid shipment, CancellationToken ct)
    {
        var (reduced, _, _) = await StreamAsync(con, tx, org, shipment, 0, null, ct);
        var input = await VersionAsync(con, tx, org, shipment, ct);
        var generation = await GenerationAsync(con, tx, org, shipment, ct);
        if (input.EventCount != reduced.EventCount)
            throw new InvalidOperationException("Tracking input-version counter mismatch");
        var containers = reduced.Containers ?? Array.Empty<ContainerTrackingObservation>();
        var sample = JsonSerializer.Serialize(containers.Take(50).ToArray());
        await using (var put = Command(con, tx, """
            INSERT INTO shipment_tracking_projections
                (organization_id,shipment_id,observed_state,last_event_id,
                 last_occurred_at_utc,input_digest,policy_version,correction_generation,
                 input_revision,event_count,container_count,container_observations_json,
                 materialization_revision)
            VALUES (@org,@shipment,@state,@event,@occurred,@digest,@policy,@generation,
                    @inputRevision,@eventCount,@containerCount,CAST(@containers AS jsonb),1)
            ON CONFLICT (shipment_id) DO UPDATE
            SET observed_state=EXCLUDED.observed_state,
                last_event_id=EXCLUDED.last_event_id,
                last_occurred_at_utc=EXCLUDED.last_occurred_at_utc,
                input_digest=EXCLUDED.input_digest,
                policy_version=EXCLUDED.policy_version,
                correction_generation=EXCLUDED.correction_generation,
                input_revision=EXCLUDED.input_revision,
                event_count=EXCLUDED.event_count,
                container_count=EXCLUDED.container_count,
                container_observations_json=EXCLUDED.container_observations_json,
                materialization_revision=shipment_tracking_projections.materialization_revision+1,
                refreshed_at_utc=clock_timestamp(),verified_at_utc=clock_timestamp()
            WHERE shipment_tracking_projections.organization_id=EXCLUDED.organization_id
              AND (shipment_tracking_projections.input_digest IS DISTINCT FROM EXCLUDED.input_digest
                OR shipment_tracking_projections.input_revision IS DISTINCT FROM EXCLUDED.input_revision
                OR shipment_tracking_projections.policy_version IS DISTINCT FROM EXCLUDED.policy_version
                OR shipment_tracking_projections.observed_state IS DISTINCT FROM EXCLUDED.observed_state
                OR shipment_tracking_projections.last_event_id IS DISTINCT FROM EXCLUDED.last_event_id
                OR shipment_tracking_projections.last_occurred_at_utc IS DISTINCT FROM EXCLUDED.last_occurred_at_utc
                OR shipment_tracking_projections.correction_generation IS DISTINCT FROM EXCLUDED.correction_generation
                OR shipment_tracking_projections.event_count IS DISTINCT FROM EXCLUDED.event_count
                OR shipment_tracking_projections.container_count IS DISTINCT FROM EXCLUDED.container_count
                OR shipment_tracking_projections.container_observations_json IS DISTINCT FROM EXCLUDED.container_observations_json)
            """, ("org", org), ("shipment", shipment),
                ("state", reduced.ObservedTrackingState), ("event", reduced.LastEventId),
                ("occurred", reduced.LastOccurredAtUtc), ("digest", reduced.InputDigest),
                ("policy", reduced.PolicyVersion), ("generation", generation),
                ("inputRevision", input.Revision), ("eventCount", input.EventCount),
                ("containerCount", containers.Count), ("containers", sample)))
        {
            if (await put.ExecuteNonQueryAsync(ct) == 0)
            {
                await using var verified = Command(con, tx, """
                    UPDATE shipment_tracking_projections SET verified_at_utc=clock_timestamp()
                    WHERE organization_id=@org AND shipment_id=@shipment
                    """, ("org", org), ("shipment", shipment));
                await verified.ExecuteNonQueryAsync(ct);
            }
        }

        await using (var apps = Command(con, tx, """
            INSERT INTO shipment_projection_applications
                (receipt_id,organization_id,shipment_id,policy_version)
            SELECT fx.receipt_id,fx.organization_id,fx.shipment_id,@policy
            FROM tracking_processing_effects fx
            WHERE fx.organization_id=@org AND fx.shipment_id=@shipment
            ON CONFLICT (receipt_id,policy_version) DO NOTHING
            """, ("policy", TrackingTimelinePolicy.Version),
                ("org", org), ("shipment", shipment)))
            await apps.ExecuteNonQueryAsync(ct);
        var saved = await CachedAsync(con, tx, org, shipment, ct);
        if (saved.Digest != reduced.InputDigest || saved.InputRevision != input.Revision)
            throw new InvalidOperationException("Projection verification mismatch");
        return saved.Revision;
    }

    private static string RequestDigest(Guid org, Guid shipment, Guid actor,
        TrackingCorrection? correction)
    {
        var canonical = correction is null
            ? "Rebuild|" + org.ToString("N") + "|" + shipment.ToString("N") + "|" +
              actor.ToString("N")
            : "Correction|" + org.ToString("N") + "|" + shipment.ToString("N") + "|" +
              actor.ToString("N") + "|" + correction.TargetReceiptId.ToString("N") + "|" +
              (correction.ReplacementReceiptId?.ToString("N") ?? "") + "|" +
              correction.ExpectedGeneration.ToString(CultureInfo.InvariantCulture) + "|" +
              correction.Reason;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private async Task<(TrackingMutationResult Result, long Revision)> ExecuteAsync(
        Guid org, Guid shipment, TrackingCorrection? correction,
        Guid actor, Guid session, string correlation, Guid? idempotencyKey, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await db.Database.OpenConnectionAsync(ct);
            try
            {
                await using var tx = (NpgsqlTransaction)await Connection.BeginTransactionAsync(
                    IsolationLevel.Serializable, ct);
                if (!await LockScopeAsync(Connection, tx, org, shipment, restrictedProjector, ct))
                    return (TrackingMutationResult.NotFound, 0);
                if (session != Guid.Empty &&
                    !await CurrentManagerAsync(Connection, tx, org, actor, session, ct))
                    return (TrackingMutationResult.Forbidden, 0);

                var requestHash = RequestDigest(org, shipment, actor, correction);
                if (idempotencyKey.HasValue)
                {
                    await using var replay = Command(Connection, tx, """
                        SELECT request_digest,response_revision
                        FROM shipment_tracking_operation_replays
                        WHERE organization_id=@org AND shipment_id=@shipment
                          AND actor_user_id=@actor AND idempotency_key=@key
                        """, ("org", org), ("shipment", shipment),
                        ("actor", actor), ("key", idempotencyKey.Value));
                    await using var reader = await replay.ExecuteReaderAsync(ct);
                    if (await reader.ReadAsync(ct))
                    {
                        var sameRequest = reader.GetString(0).Trim() == requestHash;
                        var savedRevision = reader.GetInt64(1);
                        return sameRequest
                            ? (TrackingMutationResult.Replayed, savedRevision)
                            : (TrackingMutationResult.Conflict, 0);
                    }
                }

                long generation = await GenerationAsync(Connection, tx, org, shipment, ct);
                if (correction is not null)
                {
                    if (correction.ExpectedGeneration != generation)
                        return (TrackingMutationResult.Conflict, 0);
                    await using var insert = Command(Connection, tx, """
                        INSERT INTO shipment_tracking_corrections
                            (id,organization_id,shipment_id,target_receipt_id,
                             replacement_receipt_id,generation,actor_user_id,reason,
                             correlation_id,correction_digest)
                        VALUES (@id,@org,@shipment,@target,@replacement,@generation,
                                @actor,@reason,@correlation,@digest)
                        """, ("id", Guid.NewGuid()), ("org", org), ("shipment", shipment),
                        ("target", correction.TargetReceiptId),
                        ("replacement", correction.ReplacementReceiptId),
                        ("generation", generation + 1), ("actor", actor),
                        ("reason", correction.Reason), ("correlation", correlation),
                        ("digest", requestHash));
                    await insert.ExecuteNonQueryAsync(ct);
                    generation++;
                }
                var revision = await PersistAsync(Connection, tx, org, shipment, ct);
                if (idempotencyKey.HasValue)
                {
                    await using var replayWrite = Command(Connection, tx, """
                        INSERT INTO shipment_tracking_operation_replays
                            (organization_id,shipment_id,actor_user_id,idempotency_key,
                             operation_kind,request_digest,response_revision,response_generation)
                        VALUES (@org,@shipment,@actor,@key,@kind,@digest,@revision,@generation)
                        """, ("org", org), ("shipment", shipment), ("actor", actor),
                        ("key", idempotencyKey.Value),
                        ("kind", correction is null ? "Rebuild" : "Correction"),
                        ("digest", requestHash), ("revision", revision),
                        ("generation", generation));
                    await replayWrite.ExecuteNonQueryAsync(ct);
                }
                await tx.CommitAsync(ct);
                return (TrackingMutationResult.Applied, revision);
            }
            catch (PostgresException pg) when ((pg.SqlState is "40001" or "40P01") && attempt < 2)
            {
                // Entire failed serializable transaction is discarded.
            }
            catch (PostgresException pg) when (pg.SqlState is "23514" or "23505" or "23503")
            {
                return (TrackingMutationResult.Invalid, 0);
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
        return (TrackingMutationResult.Conflict, 0);
    }

    public Task<(TrackingMutationResult Result, long Revision)> RebuildAsync(
        Guid org, Guid shipment, CancellationToken ct)
        => ExecuteAsync(org, shipment, null, Guid.Empty, Guid.Empty, "", null, ct);

    public Task<(TrackingMutationResult Result, long Revision)> RebuildForSessionAsync(
        Guid org, Guid shipment, Guid actor, Guid session, Guid key, CancellationToken ct)
        => ExecuteAsync(org, shipment, null, actor, session, "", key, ct);

    public Task<(TrackingMutationResult Result, long Revision)> CorrectAsync(
        Guid org, Guid shipment, TrackingCorrection correction,
        Guid actor, string correlation, CancellationToken ct)
        => ExecuteAsync(org, shipment, correction, actor, Guid.Empty, correlation, null, ct);

    public Task<(TrackingMutationResult Result, long Revision)> CorrectForSessionAsync(
        Guid org, Guid shipment, TrackingCorrection correction, Guid actor,
        Guid session, string correlation, Guid key, CancellationToken ct)
        => ExecuteAsync(org, shipment, correction, actor, session, correlation, key, ct);

    // The cursor is exclusive. Advancing past a failed item prevents it from
    // starving healthy later shipments; wrap to the start after reaching the end.
    public async Task<IReadOnlyList<(Guid Organization, Guid Shipment)>> DirtyAsync(
        int batchSize, CancellationToken ct,
        Guid? afterOrganization = null, Guid? afterShipment = null)
    {
        if (batchSize is < 1 or > 50 ||
            afterOrganization.HasValue != afterShipment.HasValue)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var sql = """
                SELECT v.organization_id,v.shipment_id
                FROM shipment_tracking_input_versions v
                JOIN organizations o ON o.id=v.organization_id AND o.status='Active'
                JOIN shipments s ON s.organization_id=v.organization_id
                    AND s.id=v.shipment_id AND s.record_status='Active'
                LEFT JOIN shipment_tracking_projections p
                    ON p.organization_id=v.organization_id AND p.shipment_id=v.shipment_id
                LEFT JOIN shipment_tracking_correction_heads head
                    ON head.organization_id=v.organization_id AND head.shipment_id=v.shipment_id
                WHERE (p.shipment_id IS NULL OR p.input_revision<>v.input_revision
                       OR p.policy_version<>@policy
                       OR p.correction_generation<>COALESCE(head.generation,0)
                       OR p.verified_at_utc < clock_timestamp() - interval '5 minutes')
                """;
            var args = new List<(string Name, object? Value)>
            {
                ("policy", TrackingTimelinePolicy.Version), ("limit", batchSize)
            };
            if (afterOrganization.HasValue)
            {
                sql += """
                    
                    AND (v.organization_id,v.shipment_id) > (@afterOrg,@afterShipment)
                    """;
                args.Add(("afterOrg", afterOrganization.Value));
                args.Add(("afterShipment", afterShipment!.Value));
            }
            sql += """
                
                ORDER BY v.organization_id,v.shipment_id LIMIT @limit
                """;
            await using var cmd = Command(Connection, null, sql, args.ToArray());
            var found = new List<(Guid Organization, Guid Shipment)>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                found.Add((reader.GetGuid(0), reader.GetGuid(1)));
            return found;
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
