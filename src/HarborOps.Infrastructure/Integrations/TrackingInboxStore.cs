using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HarborOps.Infrastructure.Integrations;

public enum TrackingReceiptOutcome
{
    Received,
    Replayed,
    Collision,
    AccessDenied,
    CapacityExceeded
}

public sealed record TrackingReceiptResult(TrackingReceiptOutcome Outcome,
    Guid ReceiptId = default, DateTimeOffset ReceivedAtUtc = default);

/// <summary>
/// All write and replay decisions run under the organization lock, including the
/// final credential check. No 202 can be sent until CommitAsync succeeds.
/// </summary>
public sealed class TrackingInboxStore(HarborOpsDbContext db, TimeProvider clock,
    Microsoft.Extensions.Configuration.IConfiguration configuration,
    ITrackingReceiptCommitHook commitHook)
{
    public const int MaximumPendingPerOrganization = 10_000;

    public static int ReadPendingLimit(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        var configured = configuration["M8:PendingLimit"];
        if (string.IsNullOrEmpty(configured)) return MaximumPendingPerOrganization;
        if (!int.TryParse(configured, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var limit) ||
            limit < 1 || limit > MaximumPendingPerOrganization)
            throw new InvalidOperationException("M8 pending admission limit must be between 1 and 10000");
        return limit;
    }

    private int PendingLimit => ReadPendingLimit(configuration);

    private static DbCommand Command(DbConnection connection, DbTransaction tx, string sql,
        params (string Name, object? Value)[] values)
    {
        var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        foreach (var (name, value) in values)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    private static async Task<int> ExecuteAsync(DbConnection connection, DbTransaction transaction,
        string sql, CancellationToken ct, params (string, object?)[] values)
    {
        await using var command = Command(connection, transaction, sql, values);
        return await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<Guid> ReauthorizeAsync(DbConnection connection, DbTransaction tx,
        string bearer, TrackingEvent value, TimeProvider clock, CancellationToken ct)
    {
        var secret = IntegrationSourceStore.ParseSecret(bearer, out var credentialId);
        if (secret is null) return Guid.Empty;
        try
        {
            var digest = SHA256.HashData(secret);
            try
            {
                // The organization is already held FOR UPDATE by the calling transaction.
                // Lock source and credential in the order used by source CLI mutations.
                await using var command = Command(connection, tx, """
                    SELECT c.secret_digest, c.not_before_utc, c.expires_at_utc
                    FROM integration_sources s
                    JOIN integration_credentials c
                      ON c.source_id = s.id AND c.organization_id = s.organization_id
                    WHERE s.organization_id = @organization AND s.id = @source
                      AND c.id = @credential AND c.key_version = 'v1'
                      AND s.status = 'Active'
                      AND s.capability = 'tracking_event_validate'
                      AND c.revoked_at_utc IS NULL
                    FOR SHARE OF s, c
                    """,
                    ("organization", value.OrganizationId), ("source", value.SourceId),
                    ("credential", credentialId));
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct)) return Guid.Empty;
                // Capture the current time after the FOR SHARE lock has been acquired.
                // A credential must not become valid merely because it was valid
                // before a long source/credential lock wait.
                var checkedAt = clock.GetUtcNow();
                var notBefore = reader.GetFieldValue<DateTimeOffset>(1);
                var expires = reader.GetFieldValue<DateTimeOffset>(2);
                var expected = reader.GetFieldValue<byte[]>(0);
                return notBefore <= checkedAt && expires > checkedAt &&
                    expected.Length == digest.Length &&
                    CryptographicOperations.FixedTimeEquals(expected, digest)
                    ? credentialId : Guid.Empty;
            }
            finally { CryptographicOperations.ZeroMemory(digest); }
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    public async Task<TrackingReceiptResult> ReceiveAsync(TrackingEvent value,
        string bearer, ReadOnlyMemory<byte> originalUtf8, string correlationId, CancellationToken ct)
    {
        if (value.SourceId == Guid.Empty || value.OrganizationId == Guid.Empty ||
            originalUtf8.IsEmpty || originalUtf8.Length > TrackingEventPolicy.MaximumBodyBytes ||
            string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 64)
            throw new ArgumentException("Invalid validated intake context");

        var canonical = TrackingReceiptPolicy.Canonicalize(value);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);
        var connection = db.Database.GetDbConnection();
        var current = transaction.GetDbTransaction();

        await ExecuteAsync(connection, current,
            "SET LOCAL lock_timeout = '15s'; SET LOCAL statement_timeout = '30s'", ct);

        // Lock the organization first. This serializes capacity checks across
        // sources and prevents suspension from committing mid-receipt.
        await using (var lockCommand = Command(connection, current,
            "SELECT status FROM organizations WHERE id = @organization FOR UPDATE",
            ("organization", value.OrganizationId)))
        {
            var status = await lockCommand.ExecuteScalarAsync(ct);
            if (status is not string state || state != "Active")
                return new(TrackingReceiptOutcome.AccessDenied);
        }

        var credentialId = await ReauthorizeAsync(connection, current, bearer, value, clock, ct);
        if (credentialId == Guid.Empty)
            return new(TrackingReceiptOutcome.AccessDenied);
        // PostgreSQL timestamptz stores microseconds. Truncate here so the first response and every
        // replay of the stored receipt report an identical receivedAtUtc.
        var observed = clock.GetUtcNow();
        var now = new DateTimeOffset(observed.UtcTicks - observed.UtcTicks % 10, TimeSpan.Zero);

        Guid originalReceipt = Guid.Empty;
        DateTimeOffset originalTime = default;
        byte[]? originalCanonical = null;
        await using (var lookup = Command(connection, current, """
            SELECT id, canonical_payload_utf8, received_at_utc
            FROM tracking_inbox_receipts
            WHERE organization_id = @organization AND source_id = @source
              AND profile = @profile AND source_event_id = @event_id
            """, ("organization", value.OrganizationId), ("source", value.SourceId),
            ("profile", TrackingEventPolicy.Profile), ("event_id", value.SourceEventId)))
        await using (var reader = await lookup.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                originalReceipt = reader.GetGuid(0);
                originalCanonical = reader.GetFieldValue<byte[]>(1);
                originalTime = reader.GetFieldValue<DateTimeOffset>(2);
            }
        }

        if (originalCanonical is not null)
        {
            if (originalCanonical.AsSpan().SequenceEqual(canonical.PayloadUtf8))
            {
                await transaction.CommitAsync(ct);
                return new(TrackingReceiptOutcome.Replayed, originalReceipt, originalTime);
            }

            // Only the first distinct collision is retained. No conflicting raw
            // request body and no unbounded per-key diagnostic history.
            await ExecuteAsync(connection, current, """
                INSERT INTO tracking_receipt_conflicts
                    (id, receipt_id, organization_id, source_id,
                     attempted_digest, first_seen_at_utc, correlation_id)
                VALUES (@id, @receipt, @organization, @source, @digest, @at, @correlation)
                ON CONFLICT (receipt_id) DO NOTHING
                """, ct, ("id", Guid.NewGuid()), ("receipt", originalReceipt),
                ("organization", value.OrganizationId), ("source", value.SourceId),
                ("digest", canonical.Digest), ("at", now), ("correlation", correlationId));
            await transaction.CommitAsync(ct);
            return new(TrackingReceiptOutcome.Collision);
        }

        long pending;
        await using (var count = Command(connection, current, """
            SELECT count(*) FROM tracking_inbox_work_items
            WHERE organization_id = @organization AND state = 'Pending'
            """, ("organization", value.OrganizationId)))
            pending = Convert.ToInt64(await count.ExecuteScalarAsync(ct));
        if (pending >= PendingLimit)
            return new(TrackingReceiptOutcome.CapacityExceeded);

        var receipt = Guid.NewGuid();
        await ExecuteAsync(connection, current, """
            INSERT INTO tracking_inbox_receipts
                (id, organization_id, source_id, credential_id, profile, source_event_id,
                 canonical_digest, canonical_payload_utf8, received_at_utc, correlation_id)
            VALUES (@id, @organization, @source, @credential, @profile, @event_id,
                    @digest, @canonical, @at, @correlation)
            """, ct, ("id", receipt), ("organization", value.OrganizationId),
            ("source", value.SourceId), ("credential", credentialId),
            ("profile", TrackingEventPolicy.Profile), ("event_id", value.SourceEventId),
            ("digest", canonical.Digest), ("canonical", canonical.PayloadUtf8),
            ("at", now), ("correlation", correlationId));

        await ExecuteAsync(connection, current, """
            INSERT INTO tracking_accepted_events
                (id, receipt_id, organization_id, source_id, event_family, event_code,
                 classifier, external_schema_version, occurred_at_utc,
                 equipment_reference, transport_reference, raw_payload_utf8, canonical_payload)
            VALUES (@id, @receipt, @organization, @source, @family, @code,
                    @classifier, @version, @occurred, @equipment, @transport, @raw,
                    CAST(@canonical AS jsonb))
            """, ct, ("id", Guid.NewGuid()), ("receipt", receipt),
            ("organization", value.OrganizationId), ("source", value.SourceId),
            ("family", value.EventFamily), ("code", value.EventCode),
            ("classifier", value.Classifier), ("version", value.ExternalSchemaVersion),
            ("occurred", value.OccurredAtUtc), ("equipment", value.EquipmentReference),
            ("transport", value.TransportReference), ("raw", originalUtf8.ToArray()),
            ("canonical", Encoding.UTF8.GetString(canonical.PayloadUtf8)));

        await ExecuteAsync(connection, current, """
            INSERT INTO tracking_inbox_work_items
                (receipt_id, organization_id, source_id, state, attempt_count,
                 created_at_utc, updated_at_utc)
            VALUES (@receipt, @organization, @source, 'Pending', 0, @at, @at)
            """, ct, ("receipt", receipt), ("organization", value.OrganizationId),
            ("source", value.SourceId), ("at", now));

        // The deferred completeness trigger runs as part of this COMMIT. An
        // exception or ambiguous commit outcome must never yield HTTP 202.
        await commitHook.BeforeCommitAsync(receipt, ct);
        await transaction.CommitAsync(ct);
        // A lost acknowledgement after a successful commit is indistinguishable to
        // the publisher from an uncertain COMMIT. Stable-key replay resolves it.
        await commitHook.AfterCommitAsync(receipt, ct);
        return new(TrackingReceiptOutcome.Received, receipt, now);
    }
}
