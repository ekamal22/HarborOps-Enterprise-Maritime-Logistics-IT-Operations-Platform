using System.Security.Cryptography;
using System.Text;
using System.Data;
using System.Text.Json;
using HarborOps.Domain.Exceptions;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace HarborOps.Infrastructure.Exceptions;

public sealed record ShipmentExceptionRecord(
    Guid Id, Guid OrganizationId, Guid ShipmentId, string RuleCode, int Occurrence,
    string Condition, string State, string Severity, string Confidence, string ReasonCode,
    string SourceFingerprint, long ShipmentVersion, long TrackingInputRevision,
    long CorrectionGeneration, DateTimeOffset? ReferenceAtUtc,
    DateTimeOffset? ThresholdAtUtc, DateTimeOffset FirstDetectedAtUtc,
    DateTimeOffset LastEvaluatedAtUtc, DateTimeOffset? ResolvedAtUtc, long Version,
    JsonElement Evidence);
public sealed record ShipmentExceptionHistory(
    Guid Id, Guid ExceptionId, string Action, string? OldState, string NewState,
    string ActorKind, Guid? ActorUserId, DateTimeOffset OccurredAtUtc, string? Reason);
public sealed record ShipmentExceptionPage(
    IReadOnlyList<ShipmentExceptionRecord> Items, Guid? NextAfter,
    DateTimeOffset SnapshotAtUtc);
public sealed record ShipmentExceptionDueBatch(
    IReadOnlyList<(Guid Organization, Guid Shipment)> Items,
    Guid? LastOrganization, Guid? LastShipment, int Scanned);
public sealed record ShipmentExceptionDetail(
    ShipmentExceptionRecord Exception, IReadOnlyList<ShipmentExceptionHistory> History,
    bool HistoryTruncated);
public sealed record ShipmentExceptionHistoryPage(
    IReadOnlyList<ShipmentExceptionHistory> Items,
    DateTimeOffset? NextAfterAtUtc, Guid? NextAfterId);
public enum M11StoreResult { Applied, Replayed, NotFound, Conflict, Invalid, Forbidden, DependencyUnavailable, AuditUnavailable }

public sealed class ShipmentExceptionStore(HarborOpsDbContext database, IConfiguration configuration)
{
    private NpgsqlConnection Connection =>
        (NpgsqlConnection)database.Database.GetDbConnection();

    private static NpgsqlCommand Command(NpgsqlConnection connection,
        NpgsqlTransaction? transaction, string sql,
        params (string Name, object? Value)[] parameters)
    {
        var command = new NpgsqlCommand(sql, connection, transaction) { CommandTimeout = 45 };
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }


    private string SignReadScope(long expires, Guid? organization = null,
        Guid? actor = null, Guid? session = null)
    {
        var encoded = configuration["M10CorrectionProof:Key"]
            ?? throw new InvalidOperationException("M11 read authority is unavailable");
        var key = Convert.FromBase64String(encoded);
        if (key.Length != 32)
            throw new InvalidOperationException("M11 read authority key length is invalid");
        var message = organization.HasValue && actor.HasValue && session.HasValue
            ? $"m11-read-v1|{organization.Value:N}|{actor.Value:N}|{session.Value:N}|{expires}"
            : $"m11-due-v1|{expires}";
        return Convert.ToHexString(HMACSHA256.HashData(key,
            Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
    }

    public async Task SetReadScopeAsync(NpgsqlTransaction tx, Guid organization,
        Guid actor, Guid session, CancellationToken ct)
    {
        var expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30;
        var scope = $"{organization:D}|{actor:D}|{session:D}|{expires}|"+
            SignReadScope(expires, organization, actor, session);
        await using var command = Command(Connection,tx,
            "SELECT set_config('harborops.m11_read_scope',@scope,true)",
            ("scope",scope));
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task SetDueScopeAsync(NpgsqlTransaction tx, CancellationToken ct)
    {
        var expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30;
        var scope = $"{expires}|"+SignReadScope(expires);
        await using var command = Command(Connection,tx,
            "SELECT set_config('harborops.m11_due_scope',@scope,true)",
            ("scope",scope));
        await command.ExecuteNonQueryAsync(ct);
    }

    private static JsonElement ParseEvidence(NpgsqlDataReader reader)
    {
        using var document = JsonDocument.Parse(reader.GetString(20));
        return document.RootElement.Clone();
    }

    private static ShipmentExceptionRecord Record(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3),
        reader.GetInt32(4), reader.GetString(5), reader.GetString(6),
        reader.GetString(7), reader.GetString(8), reader.GetString(9),
        reader.GetString(10).Trim(), reader.GetInt64(11), reader.GetInt64(12),
        reader.GetInt64(13), reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14),
        reader.IsDBNull(15) ? null : reader.GetFieldValue<DateTimeOffset>(15),
        reader.GetFieldValue<DateTimeOffset>(16), reader.GetFieldValue<DateTimeOffset>(17),
        reader.IsDBNull(18) ? null : reader.GetFieldValue<DateTimeOffset>(18),
        reader.GetInt64(19),
        ParseEvidence(reader));

    private const string Columns = """
        id,organization_id,shipment_id,rule_code,occurrence,condition_state,
        lifecycle_state,severity,confidence,reason_code,source_fingerprint,
        shipment_version,tracking_input_revision,correction_generation,
        reference_at_utc,threshold_at_utc,first_detected_at_utc,
        last_evaluated_at_utc,resolved_at_utc,version,evidence::text
        """;

    public Task<bool> ShipmentExistsAsync(Guid org, Guid shipment, CancellationToken ct) =>
        database.Shipments.AsNoTracking().AnyAsync(x =>
            x.OrganizationId == org && x.Id == shipment, ct);

    public async Task<ShipmentExceptionPage> ListAsync(Guid org, Guid actor, Guid session,
        Guid? shipment,
        string? state, Guid? after, DateTimeOffset? snapshot, int limit,
        CancellationToken ct)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        await database.Database.OpenConnectionAsync(ct);
        try
        {
            // A database-time creation cutoff excludes exceptions opened after
            // page one. Existing lifecycle fields can still change between
            // stateless requests, so this is not an MVCC snapshot of updates.
            await using var tx = (NpgsqlTransaction)await Connection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);
            await SetReadScopeAsync(tx,org,actor,session,ct);
            var snapshotAtUtc = snapshot ?? await DatabaseNowAsync(Connection, tx, ct);
            // The rule-precedence contract is presentation-only. Retain all
            // occurrences while keeping list order and keyset cursor identical.
            // The protected cursor contains an ID whose rule rank is resolved
            // within the already organization-scoped query.
            await using var command = Command(Connection, tx,
                $"""
                WITH scoped AS (
                    SELECT *, CASE rule_code
                        WHEN 'EquipmentConflict' THEN 1
                        WHEN 'RecordedArrivalLate' THEN 2
                        WHEN 'ArrivalScheduleOverdue' THEN 3
                        WHEN 'DepartureSlippageRisk' THEN 4
                        ELSE 5 END AS rule_priority
                    FROM shipment_exceptions
                    WHERE organization_id=@org
                )
                SELECT {Columns} FROM scoped
                WHERE (@shipment::uuid IS NULL OR shipment_id=@shipment)
                  AND (@state::varchar IS NULL OR lifecycle_state=@state)
                  AND first_detected_at_utc<=@snapshot::timestamptz
                  AND (@after::uuid IS NULL OR
                      (rule_priority,id) > (
                          SELECT rule_priority,id FROM scoped WHERE id=@after))
                ORDER BY rule_priority,id LIMIT @limit
                """,
                ("org", org), ("shipment", shipment), ("state", state),
                ("after", after), ("snapshot", snapshotAtUtc),
                ("limit", limit + 1));
            var found = new List<ShipmentExceptionRecord>();
            await using (var reader = await command.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct)) found.Add(Record(reader));
            await tx.CommitAsync(ct);
            var more = found.Count > limit;
            if (more) found.RemoveAt(limit);
            return new ShipmentExceptionPage(found,
                more && found.Count > 0 ? found[^1].Id : null, snapshotAtUtc);
        }
        finally { await database.Database.CloseConnectionAsync(); }
    }

    public async Task<ShipmentExceptionDetail?> DetailAsync(Guid org, Guid actor,
        Guid session, Guid exception,
        CancellationToken ct)
    {
        await database.Database.OpenConnectionAsync(ct);
        try
        {
            await using var tx = (NpgsqlTransaction)await Connection.BeginTransactionAsync(
                IsolationLevel.RepeatableRead, ct);
            await SetReadScopeAsync(tx,org,actor,session,ct);
            ShipmentExceptionRecord? record;
            await using (var command = Command(Connection, tx,
                $"SELECT {Columns} FROM shipment_exceptions WHERE organization_id=@org AND id=@id",
                ("org", org), ("id", exception)))
            await using (var reader = await command.ExecuteReaderAsync(ct))
                record = await reader.ReadAsync(ct) ? Record(reader) : null;
            if (record is null) return null;
            var history = new List<ShipmentExceptionHistory>();
            await using (var command = Command(Connection, tx, """
                SELECT id,exception_id,action,old_state,new_state,
                    actor_kind,actor_user_id,occurred_at_utc,reason
                FROM shipment_exception_history WHERE organization_id=@org
                    AND exception_id=@id
                ORDER BY occurred_at_utc,id LIMIT 501
                """, ("org", org), ("id", exception)))
            await using (var reader = await command.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct))
                    history.Add(new ShipmentExceptionHistory(reader.GetGuid(0),
                        reader.GetGuid(1), reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetString(3),
                        reader.GetString(4), reader.GetString(5),
                        reader.IsDBNull(6) ? null : reader.GetGuid(6),
                        reader.GetFieldValue<DateTimeOffset>(7),
                        reader.IsDBNull(8) ? null : reader.GetString(8)));
            await tx.CommitAsync(ct);
            var truncated = history.Count > 500;
            if (truncated) history.RemoveAt(500);
            return new ShipmentExceptionDetail(record, history, truncated);
        }
        finally { await database.Database.CloseConnectionAsync(); }
    }

    // Immutable history can exceed the initial 500-event detail preview.
    // Use the same occurred-at plus UUID ordering as that preview. Every
    // request rechecks the exception's organization before accessing rows.
    public async Task<ShipmentExceptionHistoryPage?> HistoryPageAsync(
        Guid org, Guid actor, Guid session, Guid exception,
        DateTimeOffset? afterUtc, Guid? afterId,
        int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 100 || afterUtc.HasValue != afterId.HasValue)
            throw new ArgumentOutOfRangeException(nameof(limit));
        await database.Database.OpenConnectionAsync(ct);
        try
        {
            await using var tx = (NpgsqlTransaction)await Connection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);
            await SetReadScopeAsync(tx,org,actor,session,ct);
            await using (var exists = Command(Connection, tx,
                "SELECT 1 FROM shipment_exceptions WHERE organization_id=@org AND id=@id",
                ("org", org), ("id", exception)))
                if (await exists.ExecuteScalarAsync(ct) is null) return null;

            await using var query = Command(Connection, tx, """
                SELECT id,exception_id,action,old_state,new_state,
                    actor_kind,actor_user_id,occurred_at_utc,reason
                FROM shipment_exception_history
                WHERE organization_id=@org AND exception_id=@id
                  AND (@at::timestamptz IS NULL
                       OR (occurred_at_utc,id)>(@at::timestamptz,@afterId::uuid))
                ORDER BY occurred_at_utc,id LIMIT @limit
                """, ("org", org), ("id", exception), ("at", afterUtc),
                    ("afterId", afterId), ("limit", limit + 1));
            var found = new List<ShipmentExceptionHistory>();
            await using (var reader = await query.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct))
                    found.Add(new ShipmentExceptionHistory(
                    reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetString(4), reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetGuid(6),
                    reader.GetFieldValue<DateTimeOffset>(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8)));
            await tx.CommitAsync(ct);
            var more = found.Count > limit;
            if (more) found.RemoveAt(limit);
            return new ShipmentExceptionHistoryPage(found,
                more ? found[^1].OccurredAtUtc : null,
                more ? found[^1].Id : null);
        }
        finally { await database.Database.CloseConnectionAsync(); }
    }

    private static DateTimeOffset? Date(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);

    private static async Task<DateTimeOffset> DatabaseNowAsync(NpgsqlConnection connection,
        NpgsqlTransaction? transaction, CancellationToken ct)
    {
        await using var command = Command(connection, transaction, "SELECT clock_timestamp()");
        var value = await command.ExecuteScalarAsync(ct)
            ?? throw new InvalidOperationException("Database clock unavailable");
        return value switch
        {
            DateTimeOffset offset => offset.ToUniversalTime(),
            DateTime timestamp => new DateTimeOffset(
                DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)),
            _ => throw new InvalidOperationException("Database clock invalid")
        };
    }

    // This query takes one repeatable source snapshot. The signed SQL procedure
    // rechecks all source version fields before accepting any of its effects.
    private async Task<ShipmentExceptionInput?> InputsAsync(Guid org, Guid shipment,
        NpgsqlTransaction tx, CancellationToken ct)
    {
        // Match the signed operation's organization-then-shipment lock order.
        await using (var organization = Command(Connection, tx,
            "SELECT status FROM organizations WHERE id=@org FOR SHARE", ("org", org)))
        {
            if (await organization.ExecuteScalarAsync(ct) is not "Active") return null;
        }
        await using var command = Command(Connection, tx, """
            SELECT s.record_status,s.workflow_status,s.version,
                   s.planned_departure_at_utc,s.planned_arrival_at_utc,
                   (SELECT min(c.changed_at_utc) FROM shipment_status_changes c
                       WHERE c.organization_id=s.organization_id AND c.shipment_id=s.id
                         AND c.to_status='InTransit'),
                   (SELECT min(c.changed_at_utc) FROM shipment_status_changes c
                       WHERE c.organization_id=s.organization_id AND c.shipment_id=s.id
                         AND c.to_status='Arrived'),
                   COALESCE(p.observed_state,'Unknown'),
                   p.input_revision,p.correction_generation,p.policy_version,
                   p.input_digest,p.last_occurred_at_utc,
                   COALESCE(v.input_revision,0),COALESCE(h.generation,0)
            FROM shipments s
            JOIN organizations o ON o.id=s.organization_id AND o.status='Active'
            LEFT JOIN shipment_tracking_input_versions v
                ON v.organization_id=s.organization_id AND v.shipment_id=s.id
            LEFT JOIN shipment_tracking_correction_heads h
                ON h.organization_id=s.organization_id AND h.shipment_id=s.id
            LEFT JOIN shipment_tracking_projections p
                ON p.organization_id=s.organization_id AND p.shipment_id=s.id
            WHERE s.organization_id=@org AND s.id=@shipment
            FOR UPDATE OF s
            """, ("org", org), ("shipment", shipment));
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var inputRevision = reader.GetInt64(13);
        var generation = reader.GetInt64(14);
        var stale = reader.IsDBNull(8) || reader.GetInt64(8) != inputRevision ||
            reader.IsDBNull(9) || reader.GetInt64(9) != generation ||
            reader.IsDBNull(10) || reader.GetString(10) != TrackingTimelinePolicy.Version;
        return new ShipmentExceptionInput(org, shipment,
            reader.GetString(0), reader.GetString(1), reader.GetInt64(2),
            Date(reader, 3), Date(reader, 4), Date(reader, 5), Date(reader, 6),
            reader.GetString(7), stale, inputRevision, generation,
            reader.IsDBNull(10) ? "" : reader.GetString(10),
            reader.IsDBNull(11) ? "" : reader.GetString(11).Trim(),
            Date(reader, 12));
    }

    public async Task<(M11StoreResult Result, int Rules)> EvaluateAsync(
        Guid org, Guid shipment, CancellationToken ct)
    {
        var key = configuration["M10CorrectionProof:Key"];
        if (string.IsNullOrWhiteSpace(key))
            return (M11StoreResult.DependencyUnavailable, 0);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await database.Database.OpenConnectionAsync(ct);
            try
            {
                await using var tx = (NpgsqlTransaction)await Connection.BeginTransactionAsync(
                    IsolationLevel.Serializable, ct);
                var input = await InputsAsync(org, shipment, tx, ct);
                if (input is null) return (M11StoreResult.NotFound, 0);
                var now = await DatabaseNowAsync(Connection, tx, ct);
                var results = ShipmentExceptionPolicy.Evaluate(input, now);
                foreach (var signal in results)
                {
                    var proof = M11ProofSigner.Evaluation(key, input, signal, now);
                    if (proof is null) return (M11StoreResult.DependencyUnavailable, 0);
                    await using var command = Command(Connection, tx, """
                        SELECT m11_apply_signed_evaluation(
                            @org::uuid,@shipment::uuid,@rule::varchar,@condition::varchar,
                            @severity::varchar,@confidence::varchar,@reason::varchar,
                            @fingerprint::varchar,@reference::timestamptz,
                            @threshold::timestamptz,@next::timestamptz,
                            @shipVersion::bigint,@inputRevision::bigint,@generation::bigint,
                            @nonce::uuid,@expires::bigint,@signature::varchar)
                        """, ("org", org), ("shipment", shipment),
                        ("rule", signal.RuleCode), ("condition", signal.Condition),
                        ("severity", signal.Severity), ("confidence", signal.Confidence),
                        ("reason", signal.ReasonCode), ("fingerprint", signal.SourceFingerprint),
                        ("reference", signal.ReferenceAtUtc), ("threshold", signal.ThresholdAtUtc),
                        ("next", signal.NextEvaluationAtUtc),
                        ("shipVersion", input.ShipmentVersion),
                        ("inputRevision", input.TrackingInputRevision),
                        ("generation", input.CorrectionGeneration),
                        ("nonce", proof.Nonce), ("expires", proof.ExpiresEpoch),
                        ("signature", proof.Signature));
                    await command.ExecuteNonQueryAsync(ct);
                }
                await tx.CommitAsync(ct);
                return (M11StoreResult.Applied, results.Count);
            }
            catch (PostgresException ex) when (IsEvaluationRace(ex) && attempt < 2)
            {
                // A concurrent evaluation may race on a new occurrence or
                // evaluation row. Retry only known concurrency failures.
            }
            catch (PostgresException ex) when (IsEvaluationRace(ex))
            {
                return (M11StoreResult.Conflict, 0);
            }
            catch (PostgresException ex) when (ex.SqlState == "42501")
            {
                return (M11StoreResult.Forbidden, 0);
            }
            catch (PostgresException ex) when (ex.SqlState == "PZ002")
            {
                return (M11StoreResult.DependencyUnavailable, 0);
            }
            catch (PostgresException ex) when (ex.SqlState == "PZ003")
            {
                return (M11StoreResult.NotFound, 0);
            }
            finally { await database.Database.CloseConnectionAsync(); }
        }
        return (M11StoreResult.Conflict, 0);
    }

    public async Task<(M11StoreResult Result, long Version)> ActionAsync(Guid org,
        Guid exception, Guid actor, Guid session, string action, long expectedVersion,
        string reason, Guid idempotencyKey, CancellationToken ct)
    {
        await database.Database.OpenConnectionAsync(ct);
        try
        {
            var databaseNow = await DatabaseNowAsync(Connection, null, ct);
            var proof = M11ProofSigner.Action(configuration["M10CorrectionProof:Key"],
                org, exception, actor, session, action, expectedVersion, reason,
                idempotencyKey, databaseNow);
            if (proof is null) return (M11StoreResult.DependencyUnavailable, 0);
            await using var command = Command(Connection, null, """
                SELECT result_code,result_version FROM m11_apply_signed_action(
                    @org::uuid,@exception::uuid,@actor::uuid,@session::uuid,
                    @action::varchar,@expectedVersion::bigint,@reason::varchar,
                    @key::uuid,@nonce::uuid,@expires::bigint,@signature::varchar)
                """, ("org", org), ("exception", exception), ("actor", actor),
                ("session", session), ("action", action),
                ("expectedVersion", expectedVersion), ("reason", reason),
                ("key", idempotencyKey), ("nonce", proof.Nonce),
                ("expires", proof.ExpiresEpoch), ("signature", proof.Signature));
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return (M11StoreResult.Conflict, 0);
            return (reader.GetString(0) == "Replayed"
                ? M11StoreResult.Replayed : M11StoreResult.Applied, reader.GetInt64(1));
        }
        catch (PostgresException ex) when (ex.SqlState == "PZ001")
        {
            return (M11StoreResult.AuditUnavailable, 0);
        }
        catch (PostgresException ex) when (ex.SqlState == "PZ002")
        {
            return (M11StoreResult.DependencyUnavailable, 0);
        }
        catch (PostgresException ex) when (ex.SqlState == "PZ003")
        {
            return (M11StoreResult.NotFound, 0);
        }
        catch (PostgresException ex) when (IsActionRace(ex))
        {
            // Only the known replay uniqueness collision or transaction races
            // can be represented as a conflict. Other integrity failures must
            // remain visible as errors and must never be reported as success.
            return (M11StoreResult.Conflict, 0);
        }
        catch (PostgresException ex) when (ex.SqlState == "PZ005")
        {
            return (M11StoreResult.Invalid, 0);
        }
        catch (PostgresException ex) when (ex.SqlState == "42501")
        {
            return (M11StoreResult.Forbidden, 0);
        }
        finally { await database.Database.CloseConnectionAsync(); }
    }

    private static bool IsActionRace(PostgresException error) =>
        error.SqlState is "PZ004" or "40001" or "40P01" ||
        (error.SqlState == "23505" &&
         error.ConstraintName == "shipment_exception_replays_pkey");

    private static bool IsEvaluationRace(PostgresException error) =>
        error.SqlState is "40001" or "40P01" ||
        (error.SqlState == "23505" &&
         error.ConstraintName is (
             "uq_m11_exception_occurrence" or
             "uq_m11_active_occurrence" or
             "shipment_exception_evaluations_pkey"));

    // Inspect at most one indexed keyset page. Advance past not-due rows too,
    // so sparse due work cannot repeatedly scan the full shipment population.
    public async Task<ShipmentExceptionDueBatch> DueAsync(
        Guid? afterOrg, Guid? afterShip, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 50 || afterOrg.HasValue != afterShip.HasValue)
            throw new ArgumentOutOfRangeException(nameof(limit));
        await database.Database.OpenConnectionAsync(ct);
        try
        {
            await using var tx = (NpgsqlTransaction)await Connection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);
            await SetDueScopeAsync(tx,ct);
            await using var command = Command(Connection, tx, """
                WITH candidate AS MATERIALIZED (
                    SELECT s.organization_id,s.id,s.version
                    FROM shipments s
                    JOIN organizations o ON o.id=s.organization_id
                        AND o.status='Active'
                    WHERE (@afterOrg::uuid IS NULL OR
                        (s.organization_id,s.id)>(@afterOrg::uuid,@afterShip::uuid))
                    ORDER BY s.organization_id,s.id LIMIT @limit
                )
                SELECT c.organization_id,c.id,
                    (SELECT COUNT(*) FROM shipment_exception_evaluations e
                        WHERE e.organization_id=c.organization_id
                          AND e.shipment_id=c.id) < 4
                    OR EXISTS (
                        SELECT 1 FROM shipment_exception_evaluations e
                        LEFT JOIN shipment_tracking_input_versions v
                          ON v.organization_id=e.organization_id
                         AND v.shipment_id=e.shipment_id
                        LEFT JOIN shipment_tracking_correction_heads h
                          ON h.organization_id=e.organization_id
                         AND h.shipment_id=e.shipment_id
                        WHERE e.organization_id=c.organization_id
                          AND e.shipment_id=c.id
                          AND (e.shipment_version<>c.version
                            OR e.rule_policy_version<>@policy
                            OR e.tracking_input_revision<>COALESCE(v.input_revision,0)
                            OR e.correction_generation<>COALESCE(h.generation,0)
                            OR (e.next_evaluation_at_utc IS NOT NULL
                                AND e.next_evaluation_at_utc<=clock_timestamp())
                            OR EXISTS (
                                SELECT 1 FROM shipment_tracking_projections p
                                WHERE p.organization_id=e.organization_id
                                  AND p.shipment_id=e.shipment_id
                                  AND p.refreshed_at_utc>e.evaluated_at_utc)))
                FROM candidate c
                ORDER BY c.organization_id,c.id
                """, ("afterOrg", afterOrg), ("afterShip", afterShip),
                ("limit", limit), ("policy", ShipmentExceptionPolicy.Version));
            var due = new List<(Guid Organization, Guid Shipment)>();
            Guid? lastOrg = null, lastShipment = null;
            int scanned = 0;
            await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
            {
                lastOrg = reader.GetGuid(0);
                lastShipment = reader.GetGuid(1);
                scanned++;
                if (reader.GetBoolean(2))
                    due.Add((lastOrg.Value, lastShipment.Value));
            }
            await tx.CommitAsync(ct);
            return new ShipmentExceptionDueBatch(due,lastOrg,lastShipment,scanned);
        }
        finally { await database.Database.CloseConnectionAsync(); }
    }

}
