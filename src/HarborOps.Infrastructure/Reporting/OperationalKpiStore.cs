using System.Data;
using System.Security.Cryptography;
using System.Text;
using HarborOps.Domain.Organizations;
using HarborOps.Domain.Reporting;
using HarborOps.Infrastructure.Exceptions;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace HarborOps.Infrastructure.Reporting;

public sealed record KpiCount(string MetricCode, string Dimension,
    DateTimeOffset? BucketStartUtc, long Count);
public sealed record KpiReport(Guid Id, Guid OrganizationId, string FiltersSha256,
    DateTimeOffset FromUtc, DateTimeOffset BeforeUtc, DateTimeOffset EvaluatedAtUtc,
    DateTimeOffset ExpiresAtUtc, int MemberCount, IReadOnlyList<KpiCount> Counts);
public sealed record KpiMember(Guid MemberKey, Guid ShipmentId, Guid? ExceptionId,
    string ReferenceCode);
public sealed record KpiMemberPage(IReadOnlyList<KpiMember> Items, Guid? NextAfter);
public sealed record KpiGrant(Guid UserId, bool Active, long Version);
public sealed record KpiOperation<T>(T? Value, OrganizationAccessError Error,
    string? FailureCode = null);

public sealed class OperationalKpiStore(
    HarborOpsDbContext db, OrganizationStore organizations,
    ShipmentExceptionStore exceptions, TimeProvider clock)
{
    private NpgsqlConnection Connection => (NpgsqlConnection)db.Database.GetDbConnection();

    private static NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction tx,
        string sql, params (string Name, NpgsqlDbType Type, object? Value)[] inputs)
    {
        var command = new NpgsqlCommand(sql, connection, tx) { CommandTimeout = 15 };
        foreach (var (name, type, value) in inputs)
            command.Parameters.Add(name, type).Value = value ?? DBNull.Value;
        return command;
    }

    private static (string, NpgsqlDbType, object?) U(string key, Guid? value) =>
        (key, NpgsqlDbType.Uuid, value);
    private static (string, NpgsqlDbType, object?) T(string key, DateTimeOffset? value) =>
        (key, NpgsqlDbType.TimestampTz, value);
    private static (string, NpgsqlDbType, object?) S(string key, string? value) =>
        (key, NpgsqlDbType.Text, value);
    private static (string, NpgsqlDbType, object?) I(string key, int value) =>
        (key, NpgsqlDbType.Integer, value);

    private async Task<OrganizationAccessError> ScopeAsync(CurrentSession session,
        Guid org, bool manager, CancellationToken ct)
    {
        // Lock ordering mirrors M12 and remains valid until the enclosing
        // transaction commits. Cookie authentication alone is not a DB lease.
        var organization = await db.Organizations.FromSqlInterpolated(
            $"SELECT * FROM organizations WHERE id={org} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (organization is null || organization.Status != "Active")
            return OrganizationAccessError.NotFound;
        var current = await db.AuthSessions.FromSqlInterpolated(
            $"SELECT * FROM auth_sessions WHERE id={session.SessionId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (current is null || current.UserId != session.UserId ||
            current.RevokedAtUtc is not null ||
            current.IdleExpiresAtUtc <= clock.GetUtcNow() ||
            current.AbsoluteExpiresAtUtc <= clock.GetUtcNow())
            return OrganizationAccessError.Revoked;
        if (current.SelectedOrganizationId is null)
            return OrganizationAccessError.SelectionRequired;
        if (current.SelectedOrganizationId != org)
            return OrganizationAccessError.NotFound;
        var user = await db.AuthUsers.FromSqlInterpolated(
            $"SELECT * FROM users WHERE id={session.UserId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (user is null || user.Status != "Active" ||
            user.SecurityVersion != current.SecurityVersion)
            return OrganizationAccessError.Revoked;
        var membership = await db.OrganizationMemberships.FromSqlInterpolated(
            $"SELECT * FROM organization_memberships WHERE organization_id={org} AND user_id={session.UserId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (membership is null || membership.Status != "Active" ||
            !OrganizationPolicy.ParseRole(membership.Role, out var role))
            return OrganizationAccessError.Forbidden;
        if (manager && role != OrganizationRole.Administrator)
            return OrganizationAccessError.Forbidden;
        if (!manager && role is not (OrganizationRole.Analyst or OrganizationRole.Manager or OrganizationRole.Auditor))
            return OrganizationAccessError.Forbidden;
        var resolved = await organizations.ResolveAsync(session.UserId, session.SessionId,
            org, manager ? OrganizationPermission.ManageMemberships :
                OrganizationPermission.ViewShipmentData, ct);
        return resolved.Value is null ? resolved.Error : OrganizationAccessError.None;
    }

    // Hold the Auditor entitlement row for the duration of each report read.
    // A concurrent grant revocation must wait for the reporting transaction.
    private async Task<bool> LockAuditorGrantAsync(CurrentSession session,
        Guid org, NpgsqlTransaction tx, CancellationToken ct)
    {
        await using var roleCommand = Command(Connection, tx,
            "SELECT m13_scoped_role(@org)", U("org", org));
        var role = await roleCommand.ExecuteScalarAsync(ct) as string;
        if (role is "Analyst" or "Manager") return true;
        if (role != "Auditor") return false;
        await using var command = Command(Connection, tx, """
            SELECT m13_lock_auditor_grant(@org,@actor,@session)
            """, U("org", org), U("actor", session.UserId),
            U("session", session.SessionId));
        return await command.ExecuteScalarAsync(ct) is true;
    }

    public async Task<KpiOperation<bool?>> CanViewAsync(CurrentSession session,
        Guid org, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var error = await ScopeAsync(session, org, false, ct);
        if (error != OrganizationAccessError.None) return new(null, error);
        await exceptions.SetReadScopeAsync((NpgsqlTransaction)tx.GetDbTransaction(), org,
            session.UserId, session.SessionId, ct);
        if (!await LockAuditorGrantAsync(session, org,
            (NpgsqlTransaction)tx.GetDbTransaction(), ct))
            return new(null, OrganizationAccessError.Forbidden);
        await using var cmd = Command(Connection, (NpgsqlTransaction)tx.GetDbTransaction(),
            "SELECT m13_can_view(@org)", U("org", org));
        var allowed = await cmd.ExecuteScalarAsync(ct) is true;
        await tx.CommitAsync(ct);
        return allowed ? new(true, OrganizationAccessError.None) :
            new(null, OrganizationAccessError.Forbidden);
    }

    public async Task<KpiOperation<KpiReport>> BuildAsync(CurrentSession session,
        Guid org, OperationalKpiFilters filters, CancellationToken ct)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(25));
        var token = budget.Token;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try { return await BuildOnceAsync(session, org, filters, token); }
            catch (Exception e) when (attempt == 0 && IsSerialization(e)) { }
        }
        return new(null, OrganizationAccessError.Conflict, "report_retry_exhausted");
    }

    private static bool IsSerialization(Exception exception)
    {
        for (Exception? e = exception; e is not null; e = e.InnerException)
            if (e is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
                return true;
        return false;
    }

    private async Task<KpiOperation<KpiReport>> BuildOnceAsync(CurrentSession session,
        Guid org, OperationalKpiFilters f, CancellationToken ct)
    {
        // A dedicated connection holds the two admission locks before the
        // reporting transaction takes its REPEATABLE READ MVCC snapshot.
        // Per-actor acquisition is nonblocking; tenant admission is bounded
        // by the request cancellation and SQL command deadline.
        await using var admission = new NpgsqlConnection(db.Database.GetConnectionString());
        await admission.OpenAsync(ct);
        try
        {
            await using (var actor = new NpgsqlCommand(
                "SELECT pg_try_advisory_lock(hashtextextended(@scope,0))",
                admission) { CommandTimeout = 15 })
            {
                actor.Parameters.AddWithValue("scope",
                    org.ToString("N") + ":" + session.UserId.ToString("N"));
                if (await actor.ExecuteScalarAsync(ct) is not true)
                    return new(null, OrganizationAccessError.Conflict,
                        "report_in_progress");
            }
            await using (var tenant = new NpgsqlCommand(
                "SELECT pg_advisory_lock(hashtextextended(@scope,0))",
                admission) { CommandTimeout = 15 })
            {
                tenant.Parameters.AddWithValue("scope",
                    "m13-report-quota:" + org.ToString("N"));
                await tenant.ExecuteScalarAsync(ct);
            }
            return await BuildLockedOnceAsync(session, org, f, ct);
        }
        finally
        {
            if (admission.State == ConnectionState.Open)
            {
                // The connection belongs only to this admission attempt.
                // Explicitly release session locks even if the pool is reused.
                try
                {
                    await using var unlock = new NpgsqlCommand(
                        "SELECT pg_advisory_unlock_all()", admission)
                        { CommandTimeout = 5 };
                    await unlock.ExecuteScalarAsync(CancellationToken.None);
                }
                catch (NpgsqlException)
                {
                    // A broken connection cannot retain a usable server lock.
                }
            }
        }
    }

    private async Task<KpiOperation<KpiReport>> BuildLockedOnceAsync(
        CurrentSession session, Guid org, OperationalKpiFilters f,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await db.Database.ExecuteSqlRawAsync("SET LOCAL statement_timeout = '15s'", ct);
        var error = await ScopeAsync(session, org, false, ct);
        if (error != OrganizationAccessError.None) return new(null, error);
        var sqlTx = (NpgsqlTransaction)tx.GetDbTransaction();
        await exceptions.SetReadScopeAsync(sqlTx, org, session.UserId, session.SessionId, ct);
        if (!await LockAuditorGrantAsync(session, org, sqlTx, ct))
            return new(null, OrganizationAccessError.Forbidden);
        await using (var guard = Command(Connection, sqlTx, "SELECT m13_can_view(@org)", U("org", org)))
            if (await guard.ExecuteScalarAsync(ct) is not true)
                return new(null, OrganizationAccessError.Forbidden);

        // Session advisory locks were acquired before opening this transaction.
        // The entire materialization and tenant quota check share one stable
        // MVCC snapshot. All early returns roll back the report transaction.

        // Bound expired-report maintenance to this selected organization.
        // No cross-tenant sweep or unbounded cleanup is exposed to runtime.
        await using (var prune = Command(Connection, sqlTx,
            "SELECT m13_prune_expired(@org,@limit)", U("org", org), I("limit", 8)))
            await prune.ExecuteScalarAsync(ct);

        // Only this actor's existing report may be retired; retained member rows
        // are deleted via scoped FK cascade. Other sessions are not affected.
        await using (var cleanup = Command(Connection, sqlTx, """
            DELETE FROM operational_kpi_reports
            WHERE organization_id=@org AND actor_user_id=@actor
                AND actor_session_id=@session
            """, U("org", org), U("actor", session.UserId),
            U("session", session.SessionId)))
            await cleanup.ExecuteNonQueryAsync(ct);

        DateTimeOffset now;
        await using (var current = Command(Connection, sqlTx, "SELECT clock_timestamp()"))
        await using (var reader = await current.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct))
                throw new InvalidOperationException("Database clock unavailable");
            now = reader.GetFieldValue<DateTimeOffset>(0);
        }
        var expires = now + OperationalKpiPolicy.ProvisionalReportLifetime;
        var report = Guid.NewGuid();
        var canonical = string.Join("|", OperationalKpiPolicy.Version,
            f.FromUtc.ToString("O"), f.BeforeUtc.ToString("O"), f.Bucket,
            f.OriginPortId?.ToString("N") ?? "", f.DestinationPortId?.ToString("N") ?? "",
            f.VoyageId?.ToString("N") ?? "", f.SourceKind ?? "");
        var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
        await using (var create = Command(Connection, sqlTx, """
            INSERT INTO operational_kpi_reports
                (id,organization_id,actor_user_id,actor_session_id,
                 policy_version,filters_sha256,from_utc,before_utc,
                 evaluated_at_utc,expires_at_utc)
            VALUES (@id,@org,@actor,@session,'m13-kpi-v1',@sha,@from,@before,@now,@expires)
            """, U("id", report), U("org", org), U("actor", session.UserId),
            U("session", session.SessionId), S("sha", sha), T("from", f.FromUtc),
            T("before", f.BeforeUtc), T("now", now), T("expires", expires)))
            await create.ExecuteNonQueryAsync(ct);

        // This statement-local claim is not an authority on its own. SQL RLS
        // verifies the signed M11 session, selected tenant, report ownership,
        // active membership and expiry once, and checks the org/report
        // equality for every inserted member row.
        await using (var claim = Command(Connection, sqlTx, """
            SELECT set_config('harborops.m13_materialize_org',@org::text,true),
                   set_config('harborops.m13_materialize_report',@report::text,true)
            """, U("org", org), U("report", report)))
            await claim.ExecuteNonQueryAsync(ct);

        // The materialized intermediate results (heads, evaluated, events) have no
        // statistics, so the planner estimates them at one or a few hundred rows
        // and chooses nested loops over what are 100,000 row sets on a realistic
        // tenant. That made a 100,000-shipment build exceed ten minutes; with hash
        // and merge joins the same statement completes in seconds. The setting is
        // transaction-local and reverted straight after this one statement.
        await using (var planGuard = Command(Connection, sqlTx,
            "SET LOCAL enable_nestloop = off"))
            await planGuard.ExecuteNonQueryAsync(ct);
        int count;
        await using (var insert = Command(Connection, sqlTx, MemberSql,
            U("org", org), U("report", report), T("from", f.FromUtc),
            T("before", f.BeforeUtc), T("now", now), S("bucket", f.Bucket),
            U("origin", f.OriginPortId), U("destination", f.DestinationPortId),
            U("voyage", f.VoyageId), S("source", f.SourceKind),
            I("maxrows", OperationalKpiPolicy.MaximumReportMembers + 1)))
            count = await insert.ExecuteNonQueryAsync(ct);
        await using (var planRestore = Command(Connection, sqlTx,
            "SET LOCAL enable_nestloop = on"))
            await planRestore.ExecuteNonQueryAsync(ct);

        if (count > OperationalKpiPolicy.MaximumReportMembers)
            return new(null, OrganizationAccessError.Conflict, "report_capacity_exceeded");
        // Check all live snapshots for this tenant under the transaction-scoped
        // tenant admission lock. The migration-owned function counts other
        // sessions without exposing their member rows to this API principal.
        await using (var quota = Command(Connection, sqlTx,
            "SELECT m13_tenant_quota(@org)", U("org", org)))
        {
            if (await quota.ExecuteScalarAsync(ct) is not true)
                return new(null, OrganizationAccessError.Conflict,
                    "report_tenant_capacity_exceeded");
        }
        // Fairness inside the tenant limit: at most two live snapshots per user.
        await using (var fairness = Command(Connection, sqlTx,
            "SELECT m13_user_quota(@org)", U("org", org)))
        {
            if (await fairness.ExecuteScalarAsync(ct) is not true)
                return new(null, OrganizationAccessError.Conflict,
                    "report_user_capacity_exceeded");
        }
        var counts = await ReadCountsAsync(sqlTx, org, report, ct);
        await tx.CommitAsync(ct);
        return new(new KpiReport(report, org, sha, f.FromUtc, f.BeforeUtc,
            now, expires, count, counts), OrganizationAccessError.None);
    }

    private static readonly string MemberSql = """
        WITH scoped AS MATERIALIZED (
            SELECT s.id,s.organization_id,s.workflow_status,s.record_status,
                s.planned_arrival_at_utc,s.version
            FROM shipments s
            WHERE s.organization_id=@org
                AND (@origin::uuid IS NULL OR s.origin_port_id=@origin)
                AND (@destination::uuid IS NULL OR s.destination_port_id=@destination)
                AND (@source::text IS NULL OR s.source_kind=@source)
                AND (@voyage::uuid IS NULL OR EXISTS (
                    SELECT 1 FROM shipment_voyage_assignments a
                    WHERE a.organization_id=@org AND a.shipment_id=s.id
                      AND a.voyage_id=@voyage
                      AND a.starts_at_utc<=@now AND
                        (a.ends_at_utc IS NULL OR a.ends_at_utc>@now)))
        ), events AS MATERIALIZED (
            SELECT DISTINCT ON (h.shipment_id,h.to_status)
                h.shipment_id,h.to_status,h.changed_at_utc
            FROM shipment_status_changes h
            JOIN scoped s ON s.id=h.shipment_id AND s.organization_id=h.organization_id
            WHERE h.organization_id=@org
                AND h.to_status IN ('Arrived','Completed','Cancelled')
            ORDER BY h.shipment_id,h.to_status,h.changed_at_utc,h.id
        ), completed AS (
            SELECT s.id,s.planned_arrival_at_utc,a.changed_at_utc AS arrived_at
            FROM scoped s JOIN events c ON c.shipment_id=s.id AND c.to_status='Completed'
            LEFT JOIN events a ON a.shipment_id=s.id AND a.to_status='Arrived'
            WHERE c.changed_at_utc>=@from AND c.changed_at_utc<@before
        ), heads AS MATERIALIZED (
            SELECT s.id,s.workflow_status,s.record_status,s.version,
                COALESCE(iv.input_revision,0) AS input_revision,
                COALESCE(iv.event_count,0) AS event_count,
                COALESCE(ch.generation,0) AS generation,
                p.observed_state,p.policy_version AS projection_policy,
                p.input_revision AS projection_revision,
                p.correction_generation AS projection_generation,
                p.refreshed_at_utc AS projection_refreshed_at
            FROM scoped s
            LEFT JOIN shipment_tracking_input_versions iv
                ON iv.organization_id=@org AND iv.shipment_id=s.id
            LEFT JOIN shipment_tracking_correction_heads ch
                ON ch.organization_id=@org AND ch.shipment_id=s.id
            LEFT JOIN shipment_tracking_projections p
                ON p.organization_id=@org AND p.shipment_id=s.id
        ), evaluated AS MATERIALIZED (
            SELECT h.id,e.rule_code,e.condition_state,
                e.evaluated_at_utc
            FROM heads h
            JOIN shipment_exception_evaluations e
                ON e.organization_id=@org AND e.shipment_id=h.id
            WHERE e.rule_policy_version='m11-rules-v1'
              AND e.shipment_version=h.version
              AND e.tracking_input_revision=h.input_revision
              AND e.correction_generation=h.generation
              AND (e.next_evaluation_at_utc IS NULL OR e.next_evaluation_at_utc>@now)
        ), quality AS MATERIALIZED (
            SELECT h.id,
                count(e.rule_code) AS fresh_rules,
                count(e.rule_code) FILTER (WHERE e.condition_state='Unknown') AS unknown_rules,
                count(e.rule_code) FILTER (WHERE e.condition_state='Stale') AS stale_rules,
                EXISTS (SELECT 1 FROM shipment_exception_evaluations old
                    WHERE old.organization_id=@org AND old.shipment_id=h.id
                      AND (old.rule_policy_version<>'m11-rules-v1'
                      OR old.shipment_version<>h.version
                      OR old.tracking_input_revision<>h.input_revision
                      OR old.correction_generation<>h.generation)) AS version_drift,
                (h.event_count>0 AND (h.projection_policy IS DISTINCT FROM 'm10-equipment-v2'
                    OR h.projection_revision IS DISTINCT FROM h.input_revision
                    OR h.projection_generation IS DISTINCT FROM h.generation
                    OR EXISTS (SELECT 1 FROM evaluated equipment
                        WHERE equipment.id=h.id AND equipment.rule_code='EquipmentConflict'
                          AND equipment.evaluated_at_utc<h.projection_refreshed_at))) AS bad_projection
            FROM heads h LEFT JOIN evaluated e ON e.id=h.id
            WHERE h.record_status='Active' AND h.workflow_status NOT IN ('Draft','Cancelled')
            GROUP BY h.id,h.version,h.input_revision,h.generation,h.event_count,
                h.projection_policy,h.projection_revision,h.projection_generation,
                h.projection_refreshed_at
        ), prepared AS (
            SELECT 'D01'::varchar AS code,''::varchar AS dimension,s.id AS member_key,
                s.id AS shipment_id,NULL::uuid AS exception_id,NULL::timestamptz AS bucket
                FROM scoped s WHERE s.record_status='Active'
            UNION ALL
            SELECT 'K01','',s.id,s.id,NULL,NULL FROM scoped s
                WHERE s.record_status='Active' AND s.workflow_status NOT IN ('Completed','Cancelled')
            UNION ALL
            SELECT 'K02',s.workflow_status,s.id,s.id,NULL,NULL FROM scoped s
                WHERE s.record_status='Active'
            UNION ALL
            SELECT 'K03','',s.id,s.id,NULL,NULL FROM scoped s
                JOIN events e ON e.shipment_id=s.id AND e.to_status='Completed'
                WHERE e.changed_at_utc>=@from AND e.changed_at_utc<@before
            UNION ALL
            SELECT 'K04','',s.id,s.id,NULL,NULL FROM scoped s
                JOIN events e ON e.shipment_id=s.id AND e.to_status='Cancelled'
                WHERE e.changed_at_utc>=@from AND e.changed_at_utc<@before
            UNION ALL
            SELECT 'D05','',c.id,c.id,NULL,NULL FROM completed c
                WHERE c.arrived_at IS NOT NULL AND c.planned_arrival_at_utc IS NOT NULL
            UNION ALL
            SELECT 'K05','',c.id,c.id,NULL,NULL FROM completed c
                WHERE c.arrived_at <= c.planned_arrival_at_utc
            UNION ALL
            SELECT 'K06','',c.id,c.id,NULL,NULL FROM completed c
                WHERE c.arrived_at <= c.planned_arrival_at_utc + interval '60 minutes'
            UNION ALL
            SELECT CASE e.rule_code WHEN 'RecordedArrivalLate' THEN 'K07'
                WHEN 'ArrivalScheduleOverdue' THEN 'K08'
                WHEN 'DepartureSlippageRisk' THEN 'K09' END,
                '',h.id,h.id,NULL,NULL
                FROM heads h JOIN evaluated e ON e.id=h.id
                WHERE h.record_status='Active'
                    AND h.workflow_status NOT IN ('Draft','Cancelled')
                    AND e.rule_code IN ('RecordedArrivalLate','ArrivalScheduleOverdue','DepartureSlippageRisk')
                    AND e.condition_state='Active'
            UNION ALL
            SELECT 'K10','',h.id,h.id,NULL,NULL
                FROM heads h JOIN evaluated e ON e.id=h.id
                WHERE h.record_status='Active'
                  AND h.workflow_status NOT IN ('Draft','Cancelled')
                  AND e.rule_code='EquipmentConflict' AND e.condition_state='Active'
                  AND h.observed_state='Conflict'
                  AND h.projection_policy='m10-equipment-v2'
                  AND h.projection_revision=h.input_revision
                  AND h.projection_generation=h.generation
                  AND e.evaluated_at_utc>=h.projection_refreshed_at
            UNION ALL
            SELECT 'K11','',e.id,e.shipment_id,e.id,NULL
                FROM shipment_exceptions e JOIN scoped s
                    ON s.id=e.shipment_id AND s.organization_id=e.organization_id
                WHERE e.organization_id=@org
                  AND s.record_status='Active'
                  AND s.workflow_status NOT IN ('Draft','Cancelled')
                  AND e.lifecycle_state IN ('Open','Acknowledged')
            UNION ALL
            SELECT 'K12',e.rule_code,e.id,e.shipment_id,e.id,
                (date_trunc(@bucket, e.first_detected_at_utc AT TIME ZONE 'UTC')
                    AT TIME ZONE 'UTC')
                FROM shipment_exceptions e JOIN scoped s
                    ON s.id=e.shipment_id AND s.organization_id=e.organization_id
                WHERE e.organization_id=@org AND e.first_detected_at_utc>=@from
                  AND e.first_detected_at_utc<@before
            UNION ALL
            SELECT CASE e.rule_code WHEN 'RecordedArrivalLate' THEN 'D07'
                WHEN 'ArrivalScheduleOverdue' THEN 'D08'
                WHEN 'DepartureSlippageRisk' THEN 'D09' END,
                '',h.id,h.id,NULL,NULL FROM heads h JOIN evaluated e ON e.id=h.id
                WHERE h.record_status='Active' AND h.workflow_status NOT IN ('Draft','Cancelled')
                  AND e.rule_code IN ('RecordedArrivalLate','ArrivalScheduleOverdue','DepartureSlippageRisk')
                  AND e.condition_state IN ('Active','Inactive')
            UNION ALL
            SELECT 'D10','',h.id,h.id,NULL,NULL
                FROM heads h JOIN evaluated e ON e.id=h.id
                WHERE h.record_status='Active'
                  AND h.workflow_status NOT IN ('Draft','Cancelled')
                  AND e.rule_code='EquipmentConflict' AND e.condition_state IN ('Active','Inactive')
                  AND h.projection_policy='m10-equipment-v2'
                  AND h.projection_revision=h.input_revision
                  AND h.projection_generation=h.generation
            UNION ALL
            SELECT 'D13','',q.id,q.id,NULL,NULL FROM quality q
            UNION ALL
            SELECT 'K13','',q.id,q.id,NULL,NULL FROM quality q WHERE q.fresh_rules=4
            UNION ALL
            SELECT 'K14','',q.id,q.id,NULL,NULL FROM quality q
                WHERE q.fresh_rules<4 OR q.unknown_rules>0 OR q.stale_rules>0
                    OR q.version_drift OR q.bad_projection
            UNION ALL
            SELECT 'Q01','',q.id,q.id,NULL,NULL FROM quality q WHERE q.fresh_rules<4
            UNION ALL
            SELECT 'Q02','',q.id,q.id,NULL,NULL FROM quality q
                WHERE q.stale_rules>0 OR q.version_drift
            UNION ALL
            SELECT 'Q03','',q.id,q.id,NULL,NULL FROM quality q WHERE q.unknown_rules>0
            UNION ALL
            SELECT 'Q04','',q.id,q.id,NULL,NULL FROM quality q WHERE q.bad_projection
        )
        INSERT INTO operational_kpi_members
            (organization_id,report_id,metric_code,dimension,
             member_key,shipment_id,exception_id,bucket_start_utc)
        SELECT @org,@report,code,dimension,member_key,shipment_id,exception_id,bucket
        FROM prepared LIMIT @maxrows
        """;

    private async Task<IReadOnlyList<KpiCount>> ReadCountsAsync(NpgsqlTransaction tx,
        Guid org, Guid report, CancellationToken ct)
    {
        var rows = new List<KpiCount>();
        await using var cmd = Command(Connection, tx, """
            SELECT metric_code,dimension,bucket_start_utc,count(*)
            FROM operational_kpi_members WHERE organization_id=@org AND report_id=@report
            GROUP BY metric_code,dimension,bucket_start_utc
            ORDER BY metric_code,dimension,bucket_start_utc
            """, U("org", org), U("report", report));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(new KpiCount(reader.GetString(0), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
                reader.GetInt64(3)));
        return rows;
    }

    public async Task<KpiOperation<KpiMemberPage>> MembersAsync(
        CurrentSession session, Guid org, Guid report, string filtersSha256,
        string metric, string dimension, DateTimeOffset? bucket, Guid? after,
        int limit, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var error = await ScopeAsync(session, org, false, ct);
        if (error != OrganizationAccessError.None) return new(null, error);
        var sqlTx = (NpgsqlTransaction)tx.GetDbTransaction();
        await exceptions.SetReadScopeAsync(sqlTx, org, session.UserId, session.SessionId, ct);
        if (!await LockAuditorGrantAsync(session, org, sqlTx, ct))
            return new(null, OrganizationAccessError.Forbidden);
        await using (var guard = Command(Connection, sqlTx, "SELECT m13_can_view(@org)",
            U("org", org)))
            if (await guard.ExecuteScalarAsync(ct) is not true)
                return new(null, OrganizationAccessError.Forbidden);
        await using (var check = Command(Connection, sqlTx, """
            SELECT m13_lock_report(@org,@actor,@session,@report,@fingerprint)
            """, U("report", report), U("org", org), U("actor", session.UserId),
            U("session", session.SessionId), S("fingerprint", filtersSha256)))
            if (await check.ExecuteScalarAsync(ct) is not true)
                return new(null, OrganizationAccessError.Conflict, "report_expired");

        // Report key and selected tenant are transaction-local. The SELECT
        // policy evaluates report authority once per statement and still
        // compares the report and tenant for each returned member.
        await using (var claim = Command(Connection, sqlTx, """
            SELECT set_config('harborops.m13_materialize_org',@org::text,true),
                   set_config('harborops.m13_materialize_report',@report::text,true)
            """, U("org", org), U("report", report)))
            await claim.ExecuteNonQueryAsync(ct);

        var items = new List<KpiMember>();
        await using (var cmd = Command(Connection, sqlTx, """
            SELECT m.member_key,m.shipment_id,m.exception_id,s.reference_code
            FROM operational_kpi_members m JOIN shipments s
              ON s.organization_id=m.organization_id AND s.id=m.shipment_id
            WHERE m.organization_id=@org AND m.report_id=@report
              AND m.metric_code=@metric AND m.dimension=@dimension
              AND (@bucket::timestamptz IS NULL OR m.bucket_start_utc=@bucket)
              AND (@after::uuid IS NULL OR m.member_key>@after)
            ORDER BY m.member_key LIMIT @limit
            """, U("org", org), U("report", report), S("metric", metric),
            S("dimension", dimension), T("bucket", bucket), U("after", after),
            I("limit", limit + 1)))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                items.Add(new KpiMember(reader.GetGuid(0), reader.GetGuid(1),
                    reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetString(3)));
        await tx.CommitAsync(ct);
        var more = items.Count > limit;
        if (more) items.RemoveAt(limit);
        return new(new KpiMemberPage(items,
            more && items.Count > 0 ? items[^1].MemberKey : null), OrganizationAccessError.None);
    }

    public async Task<KpiOperation<IReadOnlyList<KpiGrant>>> GrantsAsync(
        CurrentSession session, Guid org, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var error = await ScopeAsync(session, org, true, ct);
        if (error != OrganizationAccessError.None) return new(null, error);
        var sqlTx = (NpgsqlTransaction)tx.GetDbTransaction();
        await exceptions.SetReadScopeAsync(sqlTx, org, session.UserId, session.SessionId, ct);
        var grants = new List<KpiGrant>();
        await using (var cmd = Command(Connection, sqlTx, """
            SELECT user_id,active,version FROM operational_kpi_auditor_grants
            WHERE organization_id=@org ORDER BY user_id LIMIT 1001
            """, U("org", org)))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                grants.Add(new KpiGrant(reader.GetGuid(0), reader.GetBoolean(1),
                    reader.GetInt64(2)));
        if (grants.Count > 1000)
            return new(null, OrganizationAccessError.Conflict, "grant_list_limit");
        await tx.CommitAsync(ct);
        return new(grants, OrganizationAccessError.None);
    }

    public async Task<KpiOperation<long?>> SetGrantAsync(CurrentSession session,
        Guid org, Guid target, bool enabled, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var error = await ScopeAsync(session, org, true, ct);
        if (error != OrganizationAccessError.None) return new(null, error);
        var sqlTx = (NpgsqlTransaction)tx.GetDbTransaction();
        await exceptions.SetReadScopeAsync(sqlTx, org, session.UserId, session.SessionId, ct);
        await using var cmd = Command(Connection, sqlTx, """
            SELECT m13_set_auditor_grant(@org,@actor,@session,@target,@enabled)
            """, U("org", org), U("actor", session.UserId), U("session", session.SessionId),
            U("target", target), ("enabled", NpgsqlDbType.Boolean, enabled));
        var version = (long)(await cmd.ExecuteScalarAsync(ct)
            ?? throw new InvalidOperationException("Grant authority unavailable"));
        await tx.CommitAsync(ct);
        return new(version, OrganizationAccessError.None);
    }
}