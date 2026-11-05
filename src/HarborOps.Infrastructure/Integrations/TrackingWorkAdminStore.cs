using System.Data;
using System.Data.Common;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

namespace HarborOps.Infrastructure.Integrations;

public sealed record TrackingWorkQueueEntry(Guid ReceiptId, Guid SourceId, string State,
    int AttemptCount, int RedriveGeneration, DateTimeOffset AvailableAtUtc,
    DateTimeOffset UpdatedAtUtc, string? LastErrorCode);

public enum TrackingRedriveOutcome { Redriven, NotFound, AccessDenied, InvalidState, GenerationLimit, CapacityExceeded }

public sealed class TrackingWorkAdminStore(HarborOpsDbContext db, IConfiguration config)
{
    public async Task<List<TrackingWorkQueueEntry>> ListAsync(
        OrganizationScope scope, Guid? after, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        // Use PostgreSQL UUID ordering with parameters. Guid.CompareTo in LINQ is not
        // consistently translated by relational providers.
        IQueryable<TrackingInboxWorkItem> scoped = after.HasValue
            ? db.TrackingInboxWorkItems.FromSqlInterpolated($"""
                SELECT * FROM tracking_inbox_work_items
                WHERE organization_id={scope.OrganizationId} AND receipt_id>{after.Value}
                """)
            : db.TrackingInboxWorkItems.Where(x => x.OrganizationId == scope.OrganizationId);
        var query = scoped.AsNoTracking().Where(x =>
            x.OrganizationId == scope.OrganizationId &&
            db.OrganizationMemberships.Any(m => m.OrganizationId == scope.OrganizationId &&
                m.UserId == scope.UserId && m.Status == "Active" &&
                (m.Role == "Manager" || m.Role == "Analyst" || m.Role == "Auditor")) &&
            db.Organizations.Any(o => o.Id == scope.OrganizationId && o.Status == "Active"));
        return await query.OrderBy(x => x.ReceiptId).Take(limit)
            .Select(x => new TrackingWorkQueueEntry(x.ReceiptId, x.SourceId, x.State,
                x.AttemptCount, x.RedriveGeneration, x.AvailableAtUtc, x.UpdatedAtUtc,
                x.LastErrorCode))
            .ToListAsync(ct);
    }

    public async Task<TrackingWorkQueueEntry?> GetAsync(OrganizationScope scope,
        Guid receiptId, CancellationToken ct)
    {
        return await db.TrackingInboxWorkItems.AsNoTracking()
            .Where(x => x.OrganizationId == scope.OrganizationId && x.ReceiptId == receiptId &&
                db.OrganizationMemberships.Any(m => m.OrganizationId == scope.OrganizationId &&
                    m.UserId == scope.UserId && m.Status == "Active" &&
                    (m.Role == "Manager" || m.Role == "Analyst" || m.Role == "Auditor")) &&
                db.Organizations.Any(o => o.Id == scope.OrganizationId && o.Status == "Active"))
            .Select(x => new TrackingWorkQueueEntry(x.ReceiptId, x.SourceId, x.State,
                x.AttemptCount, x.RedriveGeneration, x.AvailableAtUtc, x.UpdatedAtUtc,
                x.LastErrorCode))
            .SingleOrDefaultAsync(ct);
    }

    private static DbCommand Command(DbConnection connection, DbTransaction tx, string sql,
        params (string Name, object Value)[] values)
    {
        var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        command.CommandTimeout = 30;
        foreach (var (name, value) in values)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    public async Task<TrackingRedriveOutcome> RedriveAsync(Guid organizationId,
        Guid userId, Guid sessionId, Guid receiptId, string reason, CancellationToken ct)
    {
        if (reason.Length is < 8 or > 240 || !TrackingWorkPolicy.SafeReason(reason))
            throw new ArgumentException("Invalid redrive reason");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var connection = db.Database.GetDbConnection();
        var tx = transaction.GetDbTransaction();

        await using (var cmd = Command(connection, tx,
            "SELECT status FROM organizations WHERE id=@organization FOR UPDATE",
            ("organization", organizationId)))
        {
            if (await cmd.ExecuteScalarAsync(ct) is not string state || state != "Active")
                return TrackingRedriveOutcome.AccessDenied;
        }

        // Revalidate the live session and the Manager grant after the organization lock.
        await using (var authorize = Command(connection, tx, """
            SELECT 1 FROM auth_sessions s
            JOIN users u ON u.id=s.user_id
            JOIN organization_memberships m
              ON m.organization_id=s.selected_organization_id AND m.user_id=s.user_id
            WHERE s.id=@session AND s.user_id=@actor AND s.revoked_at_utc IS NULL
              AND s.selected_organization_id=@organization
              AND s.idle_expires_at_utc>clock_timestamp()
              AND s.absolute_expires_at_utc>clock_timestamp()
              AND s.security_version=u.security_version
              AND u.status='Active' AND m.status='Active' AND m.role='Manager'
            FOR SHARE OF s,m,u
            """, ("session", sessionId), ("actor", userId), ("organization", organizationId)))
        {
            if (await authorize.ExecuteScalarAsync(ct) is null)
                return TrackingRedriveOutcome.AccessDenied;
        }

        Guid source;
        string stateName;
        int generation;
        long epoch;
        await using (var find = Command(connection, tx, """
            SELECT source_id,state,redrive_generation,lease_epoch
            FROM tracking_inbox_work_items
            WHERE organization_id=@organization AND receipt_id=@receipt FOR UPDATE
            """, ("organization", organizationId), ("receipt", receiptId)))
        await using (var reader = await find.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return TrackingRedriveOutcome.NotFound;
            source = reader.GetGuid(0);
            stateName = reader.GetString(1);
            generation = reader.GetInt32(2);
            epoch = reader.GetInt64(3);
        }
        if (stateName is not ("NeedsReconciliation" or "DeadLetter"))
            return TrackingRedriveOutcome.InvalidState;
        if (generation >= TrackingWorkPolicy.DefaultMaxRedrives)
            return TrackingRedriveOutcome.GenerationLimit;

        if (stateName == "DeadLetter")
        {
            await using var quota = Command(connection, tx, """
                SELECT count(*) FROM tracking_inbox_work_items
                WHERE organization_id=@organization
                  AND state IN ('Pending','Leased','RetryWaiting','NeedsReconciliation')
                """, ("organization", organizationId));
            var unresolved = Convert.ToInt64(await quota.ExecuteScalarAsync(ct));
            if (unresolved >= TrackingInboxStore.ReadPendingLimit(config))
                return TrackingRedriveOutcome.CapacityExceeded;
        }

        await using (var update = Command(connection, tx, """
            UPDATE tracking_inbox_work_items
            SET state='Pending',attempt_count=0,redrive_generation=redrive_generation+1,
                lease_epoch=lease_epoch+1,lease_token=NULL,lease_expires_at_utc=NULL,
                available_at_utc=clock_timestamp(),last_error_code=NULL,
                completed_at_utc=NULL,resolution_kind=NULL,updated_at_utc=clock_timestamp()
            WHERE organization_id=@organization AND receipt_id=@receipt
            """, ("organization", organizationId), ("receipt", receiptId)))
        {
            if (await update.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("Redrive state changed unexpectedly");
        }

        await using (var audit = Command(connection, tx, """
            INSERT INTO tracking_work_attempt_events
                (id,receipt_id,organization_id,source_id,generation,attempt_count,
                 lease_epoch,event_kind,reason_code,actor_user_id,redrive_reason,occurred_at_utc)
            VALUES (@id,@receipt,@organization,@source,@generation,0,@epoch,
                    'Redriven','manual_redrive',@actor,@reason,clock_timestamp())
            """, ("id", Guid.NewGuid()), ("receipt", receiptId),
            ("organization", organizationId), ("source", source),
            ("generation", generation + 1), ("epoch", epoch + 1),
            ("actor", userId), ("reason", reason)))
            await audit.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
        return TrackingRedriveOutcome.Redriven;
    }
}
