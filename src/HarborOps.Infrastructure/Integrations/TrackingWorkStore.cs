using System.Data;
using System.Data.Common;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HarborOps.Infrastructure.Integrations;

public sealed record TrackingWorkClaim(Guid ReceiptId, Guid OrganizationId, Guid SourceId,
    Guid LeaseToken, long LeaseEpoch, int Attempt, int Generation);

public enum TrackingWorkDisposition { Processed, NeedsReconciliation, Stale }

public sealed class TrackingWorkStore(HarborOpsDbContext db, TrackingWorkerSettings settings)
{
    private static DbCommand Command(DbConnection connection, DbTransaction transaction, string sql,
        params (string Name, object? Value)[] values)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.CommandTimeout = 30;
        foreach (var (name, value) in values)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    private static async Task<int> ExecAsync(DbConnection connection, DbTransaction transaction,
        string sql, CancellationToken ct, params (string Name, object? Value)[] values)
    {
        await using var cmd = Command(connection, transaction, sql, values);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> LockActiveOrganizationAsync(DbConnection connection, DbTransaction tx,
        Guid organization, CancellationToken ct)
    {
        await using var cmd = Command(connection, tx,
            "SELECT m9_lock_active_organization(@organization)",
            ("organization", organization));
        return await cmd.ExecuteScalarAsync(ct) is bool active && active;
    }

    public async Task<List<Guid>> CandidateOrganizationsAsync(Guid? after, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandTimeout = 15;
            cmd.CommandText = """
                SELECT DISTINCT o.id FROM organizations o
                JOIN tracking_inbox_work_items w ON w.organization_id=o.id
                WHERE o.status='Active' AND (CAST(@after AS uuid) IS NULL OR o.id > @after)
                  AND ((w.state IN ('Pending','RetryWaiting') AND
                        w.available_at_utc <= clock_timestamp()) OR
                       (w.state='Leased' AND w.lease_expires_at_utc <= clock_timestamp()))
                ORDER BY o.id LIMIT 100
                """;
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = "after";
            parameter.Value = after.HasValue ? after.Value : DBNull.Value;
            cmd.Parameters.Add(parameter);
            var result = new List<Guid>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) result.Add(reader.GetGuid(0));
            return result;
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    public async Task<TrackingWorkClaim?> ClaimOneAsync(Guid organizationId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var connection = db.Database.GetDbConnection();
        var tx = transaction.GetDbTransaction();
        if (!await LockActiveOrganizationAsync(connection, tx, organizationId, ct))
            return null;

        Guid receipt = default, source = default;
        string state = "";
        var attempt = 0;
        var generation = 0;
        long epoch = 0;
        await using (var cmd = Command(connection, tx, """
            SELECT receipt_id,source_id,state,attempt_count,redrive_generation,lease_epoch
            FROM tracking_inbox_work_items
            WHERE organization_id=@organization
              AND ((state IN ('Pending','RetryWaiting') AND available_at_utc <= clock_timestamp())
                OR (state='Leased' AND lease_expires_at_utc <= clock_timestamp()))
            ORDER BY created_at_utc,receipt_id
            LIMIT 1 FOR UPDATE SKIP LOCKED
            """, ("organization", organizationId)))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return null;
            receipt = reader.GetGuid(0);
            source = reader.GetGuid(1);
            state = reader.GetString(2);
            attempt = reader.GetInt32(3);
            generation = reader.GetInt32(4);
            epoch = reader.GetInt64(5);
        }

        if (attempt >= settings.MaxAttempts)
        {
            // No sixth claim: the expired lease is closed under the organization lock.
            if (state != "Leased")
                throw new InvalidOperationException("Unexpected exhausted work state");
            var changed = await ExecAsync(connection, tx, """
                UPDATE tracking_inbox_work_items SET state='DeadLetter',lease_token=NULL,
                    lease_expires_at_utc=NULL,last_error_code='lease_attempt_limit',
                    completed_at_utc=clock_timestamp(),updated_at_utc=clock_timestamp()
                WHERE organization_id=@organization AND receipt_id=@receipt
                  AND state='Leased' AND lease_expires_at_utc<=clock_timestamp()
                """, ct, ("organization", organizationId), ("receipt", receipt));
            await transaction.CommitAsync(ct);
            return null;
        }

        var token = Guid.NewGuid();
        var nextEpoch = checked(epoch + 1);
        var nextAttempt = checked(attempt + 1);
        var updated = await ExecAsync(connection, tx, """
            UPDATE tracking_inbox_work_items
            SET state='Leased', attempt_count=@attempt, lease_epoch=@epoch,
                lease_token=@token,lease_expires_at_utc=clock_timestamp()+(@seconds * interval '1 second'),
                updated_at_utc=clock_timestamp(),last_error_code=NULL,
                completed_at_utc=NULL,resolution_kind=NULL
            WHERE receipt_id=@receipt AND organization_id=@organization
            """, ct, ("receipt", receipt), ("organization", organizationId), ("attempt", nextAttempt),
            ("epoch", nextEpoch), ("token", token), ("seconds", settings.LeaseSeconds));
        if (updated != 1) throw new InvalidOperationException("Work claim was lost");
        var claim = new TrackingWorkClaim(receipt, organizationId, source, token, nextEpoch,
            nextAttempt, generation);
        await transaction.CommitAsync(ct);
        return claim;
    }

    public async Task<bool> HeartbeatAsync(TrackingWorkClaim claim, CancellationToken ct)
    {
        // Only the currently fenced lease can be extended.
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE tracking_inbox_work_items
            SET lease_expires_at_utc=clock_timestamp()+({settings.LeaseSeconds} * interval '1 second'),
                updated_at_utc=clock_timestamp()
            WHERE receipt_id={claim.ReceiptId} AND organization_id={claim.OrganizationId}
              AND source_id={claim.SourceId} AND state='Leased'
              AND lease_token={claim.LeaseToken} AND lease_epoch={claim.LeaseEpoch}
              AND redrive_generation={claim.Generation}
              AND lease_expires_at_utc>clock_timestamp()
              AND EXISTS (SELECT 1 FROM organizations o WHERE o.id={claim.OrganizationId}
                          AND o.status='Active')
            """, ct);
        return changed == 1;
    }

    private static async Task<(Guid Container, Guid Shipment)?> ResolveAsync(
        DbConnection connection, DbTransaction tx, TrackingWorkClaim claim, CancellationToken ct)
    {
        string? family = null, equipment = null;
        DateTimeOffset occurred = default;
        await using (var cmd = Command(connection, tx, """
            SELECT event_family,equipment_reference,occurred_at_utc
            FROM tracking_accepted_events
            WHERE receipt_id=@receipt AND organization_id=@organization AND source_id=@source
            """, ("receipt", claim.ReceiptId), ("organization", claim.OrganizationId),
            ("source", claim.SourceId)))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return null;
            family = reader.GetString(0);
            equipment = reader.IsDBNull(1) ? null : reader.GetString(1);
            occurred = reader.GetFieldValue<DateTimeOffset>(2);
        }

        // Transport references are opaque in M7, never equated with voyage reference codes.
        if (family != "Equipment" || string.IsNullOrWhiteSpace(equipment)) return null;
        var normalized = equipment.Trim().ToUpperInvariant();
        await using var resolve = Command(connection, tx, """
            SELECT c.id,a.shipment_id
            FROM containers c
            JOIN shipment_container_assignments a
              ON a.organization_id=c.organization_id AND a.container_id=c.id
            JOIN shipments s
              ON s.organization_id=a.organization_id AND s.id=a.shipment_id
            WHERE c.organization_id=@organization
              AND c.container_identifier=@equipment AND c.record_status='Active'
              AND s.record_status='Active'
              AND a.starts_at_utc<=@occurred AND
                  (a.ends_at_utc IS NULL OR @occurred<a.ends_at_utc)
            ORDER BY a.id LIMIT 2
            """, ("organization", claim.OrganizationId), ("equipment", normalized),
            ("occurred", occurred));
        await using var rows = await resolve.ExecuteReaderAsync(ct);
        if (!await rows.ReadAsync(ct)) return null;
        var containerId = rows.GetGuid(0);
        var shipmentId = rows.GetGuid(1);
        // A second match is ambiguous even when both share a container.
        if (await rows.ReadAsync(ct)) return null;
        return (containerId, shipmentId);
    }

    public async Task<TrackingWorkDisposition> FinalizeAsync(TrackingWorkClaim claim,
        CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var connection = db.Database.GetDbConnection();
        var tx = transaction.GetDbTransaction();
        if (!await LockActiveOrganizationAsync(connection, tx, claim.OrganizationId, ct))
            return TrackingWorkDisposition.Stale;
        await using (var cmd = Command(connection, tx, """
            SELECT receipt_id FROM tracking_inbox_work_items
            WHERE receipt_id=@receipt AND organization_id=@organization AND source_id=@source
              AND state='Leased' AND lease_token=@token AND lease_epoch=@epoch
              AND redrive_generation=@generation AND lease_expires_at_utc>clock_timestamp()
            FOR UPDATE
            """, ("receipt", claim.ReceiptId), ("organization", claim.OrganizationId),
            ("source", claim.SourceId), ("token", claim.LeaseToken),
            ("epoch", claim.LeaseEpoch), ("generation", claim.Generation)))
        {
            if (await cmd.ExecuteScalarAsync(ct) is not Guid)
                return TrackingWorkDisposition.Stale;
        }
        var resolved = await ResolveAsync(connection, tx, claim, ct);
        if (resolved is { } matched)
        {
            await ExecAsync(connection, tx, """
                INSERT INTO tracking_processing_effects
                    (receipt_id,organization_id,source_id,resolution_kind,resolution_policy,
                     container_id,shipment_id,resolved_at_utc)
                VALUES (@receipt,@organization,@source,'ReferenceResolved','m9-reference-v1',
                        @container,@shipment,clock_timestamp())
                """, ct, ("receipt", claim.ReceiptId), ("organization", claim.OrganizationId),
                ("source", claim.SourceId), ("container", matched.Container),
                ("shipment", matched.Shipment));
        }
        var target = resolved.HasValue ? "Processed" : "NeedsReconciliation";
        var code = resolved.HasValue ? null : "unresolved_reference";
        var updated = await ExecAsync(connection, tx, """
            UPDATE tracking_inbox_work_items
            SET state=@state,lease_token=NULL,lease_expires_at_utc=NULL,
                last_error_code=@error,resolution_kind=@kind,
                completed_at_utc=CASE WHEN @state='Processed' THEN clock_timestamp() ELSE NULL END,
                updated_at_utc=clock_timestamp()
            WHERE receipt_id=@receipt AND organization_id=@organization AND source_id=@source
              AND state='Leased' AND lease_token=@token AND lease_epoch=@epoch
              AND redrive_generation=@generation AND lease_expires_at_utc>clock_timestamp()
            """, ct, ("state", target), ("error", code),
            ("kind", resolved.HasValue ? "ReferenceResolved" : null),
            ("receipt", claim.ReceiptId), ("organization", claim.OrganizationId),
            ("source", claim.SourceId), ("token", claim.LeaseToken),
            ("epoch", claim.LeaseEpoch), ("generation", claim.Generation));
        if (updated != 1) return TrackingWorkDisposition.Stale;
        await transaction.CommitAsync(ct);
        return resolved.HasValue ? TrackingWorkDisposition.Processed :
            TrackingWorkDisposition.NeedsReconciliation;
    }

    public async Task<bool> FailAsync(TrackingWorkClaim claim, string sanitizedCode,
        bool retryable, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sanitizedCode) || sanitizedCode.Length > 40 ||
            !sanitizedCode.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            throw new ArgumentException("Invalid reason code");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var connection = db.Database.GetDbConnection();
        var tx = transaction.GetDbTransaction();
        if (!await LockActiveOrganizationAsync(connection, tx, claim.OrganizationId, ct)) return false;
        // An interrupted COMMIT may already have persisted the unique resolution effect.
        // Only a currently valid lease can be marked for retry or dead-letter.
        var exhausted = !retryable || claim.Attempt >= settings.MaxAttempts;
        var due = exhausted ? TimeSpan.Zero : TrackingWorkPolicy.RetryDelay(claim.ReceiptId, claim.Generation, claim.Attempt);
        var changed = await ExecAsync(connection, tx, """
            UPDATE tracking_inbox_work_items
            SET state=@state,lease_token=NULL,lease_expires_at_utc=NULL,
                last_error_code=@error,available_at_utc=clock_timestamp()+(@delay * interval '1 millisecond'),
                completed_at_utc=CASE WHEN @exhausted THEN clock_timestamp() ELSE NULL END,
                updated_at_utc=clock_timestamp()
            WHERE receipt_id=@receipt AND organization_id=@organization AND source_id=@source
              AND state='Leased' AND lease_token=@token AND lease_epoch=@epoch
              AND redrive_generation=@generation AND lease_expires_at_utc>clock_timestamp()
            """, ct, ("state", exhausted ? "DeadLetter" : "RetryWaiting"),
            ("error", sanitizedCode), ("delay", (int)due.TotalMilliseconds),
            ("exhausted", exhausted), ("receipt", claim.ReceiptId),
            ("organization", claim.OrganizationId), ("source", claim.SourceId),
            ("token", claim.LeaseToken), ("epoch", claim.LeaseEpoch),
            ("generation", claim.Generation));
        if (changed != 1) return false;
        await transaction.CommitAsync(ct);
        return true;
    }
}
