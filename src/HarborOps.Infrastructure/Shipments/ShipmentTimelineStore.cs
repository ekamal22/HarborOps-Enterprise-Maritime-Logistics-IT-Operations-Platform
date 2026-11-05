using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace HarborOps.Infrastructure.Shipments;

public sealed record ShipmentTrackingView(
    ShipmentTrackingState State, long MaterializationRevision, long CorrectionGeneration,
    bool MaterializationStale, DateTimeOffset? MaterializedAtUtc,
    IReadOnlyList<ShipmentTimelineEvent> Timeline);

public enum TrackingMutationResult { Applied, NotFound, Conflict, Invalid, Forbidden }

public sealed record TrackingCorrection(Guid TargetReceiptId, Guid? ReplacementReceiptId,
    long ExpectedGeneration, string Reason);

public sealed class TrackingTimelineLimitException : Exception
{
    public TrackingTimelineLimitException() : base("Tracking timeline exceeds the supported read limit") { }
}

public sealed class ShipmentTimelineStore(HarborOpsDbContext db)
{
    private const int MaximumEvents = 10_000;

    private static NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction? tx,
        string sql, params (string Name, object? Value)[] args)
    {
        var command = new NpgsqlCommand(sql, connection, tx) { CommandTimeout = 30 };
        foreach (var (name, value) in args)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    private NpgsqlConnection Connection => (NpgsqlConnection)db.Database.GetDbConnection();

    private static async Task<bool> ExistsAsync(NpgsqlConnection con, NpgsqlTransaction? tx,
        Guid org, Guid shipment, CancellationToken ct)
    {
        await using var cmd = Command(con, tx, """
            SELECT EXISTS (
              SELECT 1 FROM shipments s JOIN organizations o
                ON o.id=s.organization_id
              WHERE s.organization_id=@org AND s.id=@shipment
                AND s.record_status='Active' AND o.status='Active')
            """, ("org", org), ("shipment", shipment));
        return await cmd.ExecuteScalarAsync(ct) is bool exists && exists;
    }

    private static async Task<List<ShipmentTimelineEvent>> LoadAsync(
        NpgsqlConnection con, NpgsqlTransaction? tx, Guid org, Guid shipment, CancellationToken ct)
    {
        await using var cmd = Command(con, tx, """
            SELECT e.id,e.receipt_id,e.source_id,r.source_event_id,e.event_code,
                   e.occurred_at_utc,r.received_at_utc,c.id IS NOT NULL,c.replacement_receipt_id
            FROM tracking_processing_effects fx
            JOIN tracking_accepted_events e ON e.receipt_id=fx.receipt_id
              AND e.organization_id=fx.organization_id AND e.source_id=fx.source_id
            JOIN tracking_inbox_receipts r ON r.id=e.receipt_id
              AND r.organization_id=e.organization_id AND r.source_id=e.source_id
            LEFT JOIN shipment_tracking_corrections c
              ON c.organization_id=fx.organization_id
              AND c.shipment_id=fx.shipment_id
              AND c.target_receipt_id=fx.receipt_id
            WHERE fx.organization_id=@org AND fx.shipment_id=@shipment
            ORDER BY e.occurred_at_utc,r.received_at_utc,e.source_id,r.source_event_id,
                     e.receipt_id,e.id
            LIMIT 10001
            """, ("org", org), ("shipment", shipment));
        var rows = new List<ShipmentTimelineEvent>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new ShipmentTimelineEvent(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2),
                reader.GetString(3), reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.GetFieldValue<DateTimeOffset>(6), reader.GetBoolean(7),
                reader.IsDBNull(8) ? null : reader.GetGuid(8)));
            if (rows.Count > MaximumEvents) throw new TrackingTimelineLimitException();
        }
        return rows;
    }

    private static async Task<(long Revision, string? Digest, DateTimeOffset? At)> CachedAsync(
        NpgsqlConnection con, NpgsqlTransaction? tx, Guid org, Guid shipment, CancellationToken ct)
    {
        await using var cmd = Command(con, tx, """
            SELECT materialization_revision,input_digest,refreshed_at_utc
            FROM shipment_tracking_projections WHERE organization_id=@org AND shipment_id=@shipment
            """, ("org", org), ("shipment", shipment));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return (0, null, null);
        return (reader.GetInt64(0), reader.GetString(1).Trim(),
            reader.GetFieldValue<DateTimeOffset>(2));
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

    public async Task<ShipmentTrackingView?> ReadAsync(
        Guid org, Guid shipment, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            // One consistent snapshot; later commits change the cursor digest,
            // not the contents of an already-read page.
            await using var tx = (NpgsqlTransaction)await Connection.BeginTransactionAsync(
                IsolationLevel.RepeatableRead, ct);
            if (!await ExistsAsync(Connection, tx, org, shipment, ct)) return null;
            var rows = await LoadAsync(Connection, tx, org, shipment, ct);
            var state = TrackingTimelinePolicy.Reduce(org, shipment, rows);
            var cached = await CachedAsync(Connection, tx, org, shipment, ct);
            var generation = await GenerationAsync(Connection, tx, org, shipment, ct);
            await tx.CommitAsync(ct);
            return new ShipmentTrackingView(state, cached.Revision, generation,
                cached.Digest != state.InputDigest, cached.At, rows);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    public static string EncodeCursor(string digest, int position)
    {
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(digest + ":" +
            position.ToString(CultureInfo.InvariantCulture)));
        return payload.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool DecodeCursor(string? cursor, string digest, int total, out int position)
    {
        position = 0;
        if (string.IsNullOrWhiteSpace(cursor)) return true;
        if (cursor.Length > 160 || cursor.Any(x => !char.IsAsciiLetterOrDigit(x) && x is not ('-' or '_')))
            return false;
        try
        {
            var value = cursor.Replace('-', '+').Replace('_', '/');
            value = value.PadRight((value.Length + 3) / 4 * 4, '=');
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value));
            var colon = decoded.LastIndexOf(':');
            if (colon != 64 || !string.Equals(decoded[..colon], digest, StringComparison.Ordinal))
                return false;
            return int.TryParse(decoded[(colon + 1)..], NumberStyles.None,
                CultureInfo.InvariantCulture, out position) && position >= 0 &&
                position <= total;
        }
        catch (FormatException) { return false; }
    }

    private static async Task<bool> LockScopeAsync(
        NpgsqlConnection con, NpgsqlTransaction tx, Guid org, Guid shipment, CancellationToken ct)
    {
        await using (var o = Command(con, tx,
            "SELECT status FROM organizations WHERE id=@org FOR UPDATE", ("org", org)))
        {
            if (await o.ExecuteScalarAsync(ct) is not string status || status != "Active")
                return false;
        }
        await using var s = Command(con, tx, """
            SELECT record_status FROM shipments
            WHERE organization_id=@org AND id=@shipment FOR UPDATE
            """, ("org", org), ("shipment", shipment));
        return await s.ExecuteScalarAsync(ct) is string state && state == "Active";
    }

    private static async Task<bool> CurrentManagerAsync(NpgsqlConnection con,
        NpgsqlTransaction tx, Guid org, Guid actor, Guid session, CancellationToken ct)
    {
        await using var cmd = Command(con, tx, """
            SELECT EXISTS (
                SELECT 1 FROM auth_sessions sess
                JOIN users u ON u.id=sess.user_id
                JOIN organization_memberships member
                  ON member.user_id=sess.user_id
                  AND member.organization_id=sess.selected_organization_id
                WHERE sess.id=@session AND sess.user_id=@actor
                  AND sess.selected_organization_id=@org
                  AND sess.revoked_at_utc IS NULL
                  AND sess.idle_expires_at_utc > clock_timestamp()
                  AND sess.absolute_expires_at_utc > clock_timestamp()
                  AND u.status='Active'
                  AND u.security_version=sess.security_version
                  AND member.status='Active' AND member.role='Manager'
                FOR SHARE OF sess,u,member
            )
            """, ("session", session), ("actor", actor), ("org", org));
        return await cmd.ExecuteScalarAsync(ct) is bool allowed && allowed;
    }

    private static async Task<long> PersistAsync(NpgsqlConnection con, NpgsqlTransaction tx,
        Guid org, Guid shipment, CancellationToken ct)
    {
        var events = await LoadAsync(con, tx, org, shipment, ct);
        var reduced = TrackingTimelinePolicy.Reduce(org, shipment, events);
        var current = await CachedAsync(con, tx, org, shipment, ct);
        if (current.Digest != reduced.InputDigest)
        {
            await using var put = Command(con, tx, """
                INSERT INTO shipment_tracking_projections
                    (organization_id,shipment_id,observed_state,last_event_id,
                     last_occurred_at_utc,input_digest,policy_version,materialization_revision)
                VALUES (@org,@shipment,@state,@event,@occurred,@digest,@policy,1)
                ON CONFLICT (shipment_id) DO UPDATE
                SET observed_state=EXCLUDED.observed_state,
                    last_event_id=EXCLUDED.last_event_id,
                    last_occurred_at_utc=EXCLUDED.last_occurred_at_utc,
                    input_digest=EXCLUDED.input_digest,
                    policy_version=EXCLUDED.policy_version,
                    materialization_revision=shipment_tracking_projections.materialization_revision+1,
                    refreshed_at_utc=clock_timestamp()
                WHERE shipment_tracking_projections.organization_id=EXCLUDED.organization_id
                  AND (shipment_tracking_projections.input_digest IS DISTINCT FROM EXCLUDED.input_digest
                    OR shipment_tracking_projections.policy_version IS DISTINCT FROM EXCLUDED.policy_version)
                """, ("org", org), ("shipment", shipment),
                ("state", reduced.ObservedTrackingState),
                ("event", reduced.LastEventId), ("occurred", reduced.LastOccurredAtUtc),
                ("digest", reduced.InputDigest), ("policy", reduced.PolicyVersion));
            await put.ExecuteNonQueryAsync(ct);
        }

        // This table is bookkeeping, not a second M9 effect.
        await using (var apps = Command(con, tx, """
            INSERT INTO shipment_projection_applications
                (receipt_id,organization_id,shipment_id,policy_version)
            SELECT fx.receipt_id,fx.organization_id,fx.shipment_id,@policy
            FROM tracking_processing_effects fx
            WHERE fx.organization_id=@org AND fx.shipment_id=@shipment
            ON CONFLICT (receipt_id) DO NOTHING
            """, ("policy", TrackingTimelinePolicy.Version),
            ("org", org), ("shipment", shipment)))
            await apps.ExecuteNonQueryAsync(ct);

        var saved = await CachedAsync(con, tx, org, shipment, ct);
        if (saved.Digest != reduced.InputDigest)
            throw new InvalidOperationException("Projection digest mismatch");
        return saved.Revision;
    }

    private async Task<(TrackingMutationResult Result, long Revision)> ExecuteAsync(
        Guid org, Guid shipment, TrackingCorrection? correction,
        Guid actor, Guid session, string correlation, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await db.Database.OpenConnectionAsync(ct);
            try
            {
                await using var tx = (NpgsqlTransaction)await Connection.BeginTransactionAsync(
                    IsolationLevel.Serializable, ct);
                if (!await LockScopeAsync(Connection, tx, org, shipment, ct))
                    return (TrackingMutationResult.NotFound, 0);
                if (session != Guid.Empty &&
                    !await CurrentManagerAsync(Connection, tx, org, actor, session, ct))
                    return (TrackingMutationResult.Forbidden, 0);
                if (correction is not null)
                {
                    long current;
                    await using (var gen = Command(Connection, tx, """
                        SELECT COALESCE(MAX(generation),0)
                        FROM shipment_tracking_corrections
                        WHERE organization_id=@org AND shipment_id=@shipment
                        """, ("org", org), ("shipment", shipment)))
                        current = Convert.ToInt64(await gen.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
                    if (correction.ExpectedGeneration != current)
                        return (TrackingMutationResult.Conflict, 0);
                    await using var insert = Command(Connection, tx, """
                        INSERT INTO shipment_tracking_corrections
                            (id,organization_id,shipment_id,target_receipt_id,
                             replacement_receipt_id,generation,actor_user_id,reason,correlation_id)
                        VALUES (@id,@org,@shipment,@target,@replacement,@generation,
                                @actor,@reason,@correlation)
                        """, ("id", Guid.NewGuid()), ("org", org), ("shipment", shipment),
                        ("target", correction.TargetReceiptId),
                        ("replacement", correction.ReplacementReceiptId),
                        ("generation", current + 1), ("actor", actor),
                        ("reason", correction.Reason), ("correlation", correlation));
                    await insert.ExecuteNonQueryAsync(ct);
                }
                var revision = await PersistAsync(Connection, tx, org, shipment, ct);
                await tx.CommitAsync(ct);
                return (TrackingMutationResult.Applied, revision);
            }
            catch (PostgresException pg) when (pg.SqlState is "40001" or "40P01" && attempt < 2)
            {
                // Retry a complete serializable transaction, never a partial write.
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
        => ExecuteAsync(org, shipment, null, Guid.Empty, Guid.Empty, "", ct);

    public Task<(TrackingMutationResult Result, long Revision)> RebuildForSessionAsync(
        Guid org, Guid shipment, Guid actor, Guid session, CancellationToken ct)
        => ExecuteAsync(org, shipment, null, actor, session, "", ct);

    public Task<(TrackingMutationResult Result, long Revision)> CorrectAsync(
        Guid org, Guid shipment, TrackingCorrection correction,
        Guid actor, string correlation, CancellationToken ct)
        => ExecuteAsync(org, shipment, correction, actor, Guid.Empty, correlation, ct);

    public Task<(TrackingMutationResult Result, long Revision)> CorrectForSessionAsync(
        Guid org, Guid shipment, TrackingCorrection correction,
        Guid actor, Guid session, string correlation, CancellationToken ct)
        => ExecuteAsync(org, shipment, correction, actor, session, correlation, ct);

    public async Task<IReadOnlyList<(Guid Organization, Guid Shipment)>> DirtyAsync(
        int batchSize, CancellationToken ct)
    {
        if (batchSize is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(batchSize));
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = Command(Connection, null, """
                SELECT DISTINCT fx.organization_id,fx.shipment_id
                FROM tracking_processing_effects fx
                JOIN organizations o ON o.id=fx.organization_id AND o.status='Active'
                JOIN shipments s ON s.organization_id=fx.organization_id
                  AND s.id=fx.shipment_id AND s.record_status='Active'
                LEFT JOIN shipment_projection_applications a ON a.receipt_id=fx.receipt_id
                LEFT JOIN shipment_tracking_projections p ON p.shipment_id=fx.shipment_id
                  AND p.organization_id=fx.organization_id
                WHERE a.receipt_id IS NULL OR p.shipment_id IS NULL
                  OR p.policy_version<>@policy
                ORDER BY fx.organization_id,fx.shipment_id LIMIT @limit
                """, ("policy", TrackingTimelinePolicy.Version), ("limit", batchSize));
            var found = new List<(Guid Organization, Guid Shipment)>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                found.Add((reader.GetGuid(0), reader.GetGuid(1)));
            return found;
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
