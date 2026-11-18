using System.Data;
using System.Security.Cryptography;
using System.Text;
using HarborOps.Domain.Incidents;
using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure.Audit;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Exceptions;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace HarborOps.Infrastructure.Incidents;

public sealed class IncidentRequestException(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
public sealed record IncidentView(Guid Id, string Title, string ServiceCode, string Severity,
    string State, string ImpactCode, long Version, DateTimeOffset UpdatedAtUtc, Guid? AssignedTo);
public sealed record IncidentCreate(string Title, string ServiceCode, string Severity,
    string ImpactCode, DateTimeOffset DetectedAtUtc, string? PrefillToken = null);
public sealed record IncidentTransition(long ExpectedVersion, string ToState, string ReasonCode);
public sealed record IncidentNoteInput(long ExpectedVersion, string Body);
public sealed record IncidentAssignment(long ExpectedVersion, Guid AssignedTo, string ReasonCode);
public sealed record IncidentChangeResult(IncidentView Incident, bool Replayed);

public sealed partial class IncidentStore(HarborOpsDbContext db, ShipmentExceptionStore signedScope,
    IncidentCorrelation correlation, Microsoft.Extensions.Configuration.IConfiguration configuration)
{
    private static IncidentRequestException Deny() => new(403, "access_denied");
    private static IncidentRequestException Missing() => new(404, "resource_not_found");
    private static IncidentRequestException Invalid() => new(400, "invalid_request");
    private static IncidentRequestException Conflict() => new(409, "conflict");

    private static NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string sql, params (string, object?)[] parameters)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (key, value) in parameters)
            command.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return command;
    }

    private async Task<T> Scoped<T>(CurrentSession actor, Guid organizationId,
        OrganizationPermission permission, Func<NpgsqlConnection, NpgsqlTransaction,
        OrganizationRole, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var transaction = (NpgsqlTransaction)tx.GetDbTransaction();
        await signedScope.SetReadScopeAsync(transaction, organizationId,
            actor.UserId, actor.SessionId, ct);

        // Lock session, user, membership and selected organization to prevent
        // role or session revocation from committing ahead of this operation.
        await using var guard = Command(connection, transaction, """
            SELECT m.role FROM auth_sessions s
            JOIN users u ON u.id=s.user_id
            JOIN organization_memberships m
              ON m.user_id=s.user_id AND m.organization_id=s.selected_organization_id
            JOIN organizations o ON o.id=m.organization_id
            WHERE s.id=@sid AND s.user_id=@uid
              AND s.selected_organization_id=@org
              AND s.revoked_at_utc IS NULL
              AND s.absolute_expires_at_utc>now() AND s.idle_expires_at_utc>now()
              AND s.security_version=u.security_version AND u.status='Active'
              AND m.status='Active' AND o.status='Active'
            FOR SHARE OF s,u,m,o
            """, ("sid", actor.SessionId), ("uid", actor.UserId), ("org", organizationId));
        var roleText = await guard.ExecuteScalarAsync(ct) as string;
        if (!OrganizationPolicy.ParseRole(roleText, out var role))
            throw Deny();

        if (!OrganizationPolicy.Allows(role, permission))
        {
            if (role != OrganizationRole.Auditor ||
                permission is not (OrganizationPermission.ViewIncidentSummary or
                    OrganizationPermission.ViewIncidentDetail))
                throw Deny();
            await using var grant = Command(connection, transaction, """
                SELECT 1 FROM it_incident_read_grants
                WHERE organization_id=@org AND user_id=@actor AND status='Active'
                  AND expires_at_utc>now() FOR SHARE
                """, ("org",organizationId),("actor",actor.UserId));
            if (await grant.ExecuteScalarAsync(ct) is null) throw Deny();
        }

        await AuditActorContext.SetHumanAsync(db, actor.UserId, actor.SessionId, correlation.Value, ct);
        var result = await action(connection, transaction, role, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private static IncidentView Map(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
        reader.GetString(3), reader.GetString(4), reader.GetString(5),
        reader.GetInt64(6), reader.GetFieldValue<DateTimeOffset>(7),
        reader.IsDBNull(8) ? null : reader.GetGuid(8));

    private static async Task<IncidentView> Read(NpgsqlConnection connection,
        NpgsqlTransaction tx, Guid organizationId, Guid incidentId, bool lockRow,
        CancellationToken ct)
    {
        await using var command = Command(connection, tx, $"""
            SELECT id,title,service_code,severity,state,impact_code,version,updated_at_utc,assigned_to
            FROM it_incidents WHERE organization_id=@org AND id=@id
            {(lockRow ? "FOR UPDATE" : "")}
            """, ("org",organizationId),("id",incidentId));
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : throw Missing();
    }

    // Manager and Auditor see a summary tier: responder identity is never part of it.
    private static IncidentView Redact(OrganizationRole role, IncidentView view) =>
        role is OrganizationRole.Manager or OrganizationRole.Auditor
            ? view with { AssignedTo = null } : view;

    private static string Digest(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    // Operation reservation lives in the same transaction as mutation and M16 audit.
    // A competing identical request waits for this unique key's commit.
    private static async Task<Guid?> Reserve(NpgsqlConnection conn, NpgsqlTransaction tx,
        CurrentSession actor, Guid org, Guid key, string kind, string digest,
        CancellationToken ct)
    {
        if (key == Guid.Empty) throw Invalid();
        await using var insert = Command(conn, tx, """
            INSERT INTO it_incident_operation_ids
                (organization_id,actor_user_id,operation_key,operation_kind,payload_digest)
            VALUES (@org,@actor,@key,@kind,@hash)
            ON CONFLICT DO NOTHING
            RETURNING operation_key
            """, ("org",org),("actor",actor.UserId),("key",key),
            ("kind",kind),("hash",digest));
        if (await insert.ExecuteScalarAsync(ct) is not null) return null;
        await using var check = Command(conn, tx, """
            SELECT operation_kind,payload_digest,incident_id
            FROM it_incident_operation_ids
            WHERE organization_id=@org AND actor_user_id=@actor AND operation_key=@key
            """, ("org",org),("actor",actor.UserId),("key",key));
        await using var reader = await check.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw Conflict();
        if (reader.GetString(0)!=kind || reader.GetString(1)!=digest ||
            reader.IsDBNull(2))
            throw Conflict();
        return reader.GetGuid(2);
    }

    private static async Task FinalizeOperation(NpgsqlConnection conn, NpgsqlTransaction tx,
        CurrentSession actor, Guid org, Guid key, Guid id, long version, CancellationToken ct)
    {
        await using var command = Command(conn, tx, """
            UPDATE it_incident_operation_ids
               SET incident_id=@id,result_version=@version
             WHERE organization_id=@org AND actor_user_id=@actor AND operation_key=@key
            """,("org",org),("actor",actor.UserId),("key",key),("id",id),("version",version));
        if (await command.ExecuteNonQueryAsync(ct)!=1) throw Conflict();
    }

    private static async Task Event(NpgsqlConnection conn, NpgsqlTransaction tx,
        Guid org, Guid id, Guid actor, long version, string kind,
        string? before, string? after, string reason, CancellationToken ct, Guid? relatedId = null)
    {
        await using var command = Command(conn, tx, """
            INSERT INTO it_incident_events(organization_id,incident_id,sequence,kind,
                from_state,to_state,reason_code,actor_user_id,incident_version,related_id)
            VALUES(@org,@id,@seq,@kind,@before,@after,@reason,@actor,@version,@related)
            """,("org",org),("id",id),("seq",version),("kind",kind),
            ("before",before),("after",after),("reason",reason),
            ("actor",actor),("version",version),("related",relatedId));
        if (await command.ExecuteNonQueryAsync(ct)!=1) throw Conflict();
    }

    public Task<List<IncidentView>> ListAsync(CurrentSession actor, Guid org,
        int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 100) throw Invalid();
        return Scoped(actor,org,OrganizationPermission.ViewIncidentSummary,
            async (conn,tx,role,token) =>
            {
                var result = new List<IncidentView>();
                await using var command = Command(conn,tx,"""
                    SELECT id,title,service_code,severity,state,impact_code,version,updated_at_utc,assigned_to
                    FROM it_incidents WHERE organization_id=@org
                    ORDER BY updated_at_utc DESC,id DESC LIMIT @limit
                    """,("org",org),("limit",limit));
                await using var reader = await command.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                {
                    var item=Map(reader);
                    result.Add(Redact(role,item));
                }
                return result;
            },ct);
    }

    public Task<IncidentView> GetAsync(CurrentSession actor, Guid org, Guid id,
        bool detail, CancellationToken ct) =>
        Scoped(actor,org,detail?OrganizationPermission.ViewIncidentDetail:
            OrganizationPermission.ViewIncidentSummary,
            async (conn,tx,role,token) =>
            {
                var item=await Read(conn,tx,org,id,false,token);
                return Redact(role,item);
            },ct);

    // Recovery evidence must be recent when the incident is resolved. The default is five
    // minutes; the setting only exists so a disposable test can exercise expiry quickly.
    private int RecoveryFreshnessSeconds =>
        int.TryParse(configuration["M18:RecoveryFreshnessSeconds"], out var seconds)
            ? Math.Clamp(seconds, 1, 900) : 300;

    private static string ReadinessCode(string serviceCode) =>
        serviceCode == "api" ? "api_readiness_healthy" : "database_readiness_healthy";

    // A bounded digest that links one incident to one safe observation. It is a correlation
    // aid only; it does not claim to prove M17 trace continuity.
    private static string EvidenceDigest(Guid incident, string service, string status,
        DateTimeOffset observed) =>
        Digest($"{incident:D}|{service}|{status}|{Micro(observed).ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", System.Globalization.CultureInfo.InvariantCulture)}")[..32];

    // PostgreSQL keeps microseconds. Truncating first means the stored time and the digest input
    // are identical, so the digest can be recomputed later from the stored row alone.
    internal static DateTimeOffset Micro(DateTimeOffset value) =>
        new(value.UtcTicks - value.UtcTicks % 10, TimeSpan.Zero);

    public Task<IncidentChangeResult> CreateAsync(CurrentSession actor, Guid org,
        IncidentCreate input, Guid operationKey, CancellationToken ct) =>
        CreateAsync(actor, org, input, operationKey, null, ct);

    public Task<IncidentChangeResult> CreateAsync(CurrentSession actor, Guid org,
        IncidentCreate input, Guid operationKey, IncidentPrefill? prefill, CancellationToken ct)
    {
        var title=IncidentPolicy.Title(input.Title);
        var service=IncidentPolicy.ServiceCode(input.ServiceCode);
        var impact=IncidentPolicy.Reason(input.ImpactCode);
        if (!IncidentPolicy.ParseSeverity(input.Severity,out _) ||
            input.DetectedAtUtc > DateTimeOffset.UtcNow.AddMinutes(5) ||
            input.DetectedAtUtc < DateTimeOffset.UtcNow.AddYears(-2) ||
            (prefill is not null && (prefill.ServiceCode != service ||
                prefill.Status is not ("healthy" or "degraded" or "unknown"))))
            throw Invalid();
        var hash=Digest(string.Join("\n",title,service,input.Severity,impact,
            input.DetectedAtUtc.ToUniversalTime().ToString("O"),
            prefill is null ? "-" : $"{prefill.ServiceCode}|{prefill.Status}|{prefill.ObservedAtUtc.ToUniversalTime():O}"));
        return Scoped(actor,org,OrganizationPermission.CreateIncident,
            async (conn,tx,role,token)=>
            {
                var existing=await Reserve(conn,tx,actor,org,operationKey,"Create",hash,token);
                if (existing.HasValue)
                    return new IncidentChangeResult(
                        Redact(role,await Read(conn,tx,org,existing.Value,false,token)),true);
                var id=Guid.NewGuid();
                await using(var insert=Command(conn,tx,"""
                    INSERT INTO it_incidents(id,organization_id,title,service_code,severity,
                        impact_code,detected_at_utc,created_by)
                    VALUES(@id,@org,@title,@service,@severity,@impact,@detected,@actor)
                    """,("id",id),("org",org),("title",title),("service",service),
                    ("severity",input.Severity),("impact",impact),
                    ("detected",input.DetectedAtUtc),("actor",actor.UserId)))
                    if(await insert.ExecuteNonQueryAsync(token)!=1)throw Conflict();
                await Event(conn,tx,org,id,actor.UserId,1,"Created",null,"New","created",token);
                if (prefill is not null)
                {
                    // Only the server-issued, finite observation is stored; a Manager may confirm it
                    // but never receives the evidence tier back.
                    await using var snapshot=Command(conn,tx,"""
                        INSERT INTO it_incident_evidence(organization_id,incident_id,kind,service_code,
                            observation_code,source_status,observed_at_utc,correlation_digest,actor_user_id)
                        VALUES(@org,@id,'M17Snapshot',@service,'database_readiness',@status,@at,@digest,@actor)
                        """,("org",org),("id",id),("service",prefill.ServiceCode),
                        ("status",prefill.Status),("at",Micro(prefill.ObservedAtUtc)),
                        ("digest",EvidenceDigest(id,prefill.ServiceCode,prefill.Status,prefill.ObservedAtUtc)),
                        ("actor",actor.UserId));
                    if(await snapshot.ExecuteNonQueryAsync(token)!=1)throw Conflict();
                }
                await FinalizeOperation(conn,tx,actor,org,operationKey,id,1,token);
                var result=await Read(conn,tx,org,id,false,token);
                return new IncidentChangeResult(Redact(role,result),false);
            },ct);
    }

    public Task<IncidentChangeResult> TransitionAsync(CurrentSession actor, Guid org,
        Guid id, IncidentTransition input, Guid operationKey, bool reopen,
        CancellationToken ct)
    {
        if (id==Guid.Empty||input.ExpectedVersion<1||
            !IncidentPolicy.ParseState(input.ToState,out var next))throw Invalid();
        var reason=IncidentPolicy.Reason(input.ReasonCode);
        var hash=Digest($"{id:D}|{input.ExpectedVersion}|{input.ToState}|{reason}|{reopen}");
        return Scoped(actor,org,OrganizationPermission.ManageIncidentLifecycle,
            async(conn,tx,role,token)=>
            {
                var existing=await Reserve(conn,tx,actor,org,operationKey,
                    reopen?"Reopen":"Transition",hash,token);
                if(existing.HasValue)
                    return new IncidentChangeResult(await Read(conn,tx,org,existing.Value,false,token),true);
                var current=await Read(conn,tx,org,id,true,token);
                if(current.Version!=input.ExpectedVersion||
                    !IncidentPolicy.ParseState(current.State,out var from))throw Conflict();
                if(from==IncidentState.Triage && next==IncidentState.Investigating)
                {
                    if(!current.AssignedTo.HasValue)throw Conflict();
                    await using var assignee=Command(conn,tx,"""
                        SELECT 1 FROM organization_memberships m JOIN users u ON u.id=m.user_id
                        WHERE m.organization_id=@org AND m.user_id=@owner
                          AND m.status='Active' AND u.status='Active'
                          AND m.role IN ('Support','Administrator')
                        FOR SHARE OF m,u
                        """,("org",org),("owner",current.AssignedTo.Value));
                    if(await assignee.ExecuteScalarAsync(token) is null)throw Conflict();
                }
                // Gates look only at records written since the incident entered the current state, so
                // an earlier cycle's note or observation cannot satisfy a later cycle.
                if(from==IncidentState.Investigating && next==IncidentState.Mitigated)
                {
                    await using var mitigation=Command(conn,tx,"""
                        SELECT EXISTS(SELECT 1 FROM it_incident_notes
                            WHERE organization_id=@org AND incident_id=@id
                              AND occurred_at_utc>=COALESCE((SELECT max(occurred_at_utc) FROM it_incident_events
                                  WHERE organization_id=@org AND incident_id=@id AND to_state='Investigating'
                                    AND kind IN('Transitioned','Reopened')),'-infinity'))
                          AND EXISTS(SELECT 1 FROM it_incident_actions
                            WHERE organization_id=@org AND incident_id=@id)
                        """,("org",org),("id",id));
                    if(!Convert.ToBoolean(await mitigation.ExecuteScalarAsync(token)))
                        throw Conflict();
                }
                if(from==IncidentState.Mitigated && next==IncidentState.Monitoring)
                {
                    await using var observed=Command(conn,tx,"""
                        SELECT EXISTS(SELECT 1 FROM it_incident_evidence
                        WHERE organization_id=@org AND incident_id=@id
                          AND kind IN('ManualObservation','M17Snapshot')
                          AND captured_at_utc>=COALESCE((SELECT max(occurred_at_utc) FROM it_incident_events
                              WHERE organization_id=@org AND incident_id=@id AND to_state='Mitigated'
                                AND kind='Transitioned'),'-infinity'))
                        """,("org",org),("id",id));
                    if(!Convert.ToBoolean(await observed.ExecuteScalarAsync(token)))
                        throw Conflict();
                }
                if(from==IncidentState.Mitigated && next==IncidentState.Investigating)
                {
                    // A failed mitigation must say why: a note or an observation recorded after mitigation.
                    await using var failed=Command(conn,tx,"""
                        SELECT EXISTS(SELECT 1 FROM it_incident_notes
                            WHERE organization_id=@org AND incident_id=@id
                              AND occurred_at_utc>=COALESCE((SELECT max(occurred_at_utc) FROM it_incident_events
                                  WHERE organization_id=@org AND incident_id=@id AND to_state='Mitigated'
                                    AND kind='Transitioned'),'-infinity'))
                          OR EXISTS(SELECT 1 FROM it_incident_evidence
                            WHERE organization_id=@org AND incident_id=@id
                              AND captured_at_utc>=COALESCE((SELECT max(occurred_at_utc) FROM it_incident_events
                                  WHERE organization_id=@org AND incident_id=@id AND to_state='Mitigated'
                                    AND kind='Transitioned'),'-infinity'))
                        """,("org",org),("id",id));
                    if(!Convert.ToBoolean(await failed.ExecuteScalarAsync(token)))
                        throw Conflict();
                }
                bool recovery=false, integrity=false, actions=false, independentlyConfirmed=false;
                string? rootCause=null;
                Guid? resolvedBy=null;
                await using(var evidence=Command(conn,tx,"""
                    SELECT recovery_verified,integrity_verified,actions_dispositioned,
                        resolved_by,root_cause_code
                    FROM it_incidents WHERE organization_id=@org AND id=@id
                    """,("org",org),("id",id)))
                await using(var reader=await evidence.ExecuteReaderAsync(token))
                {
                    if(!await reader.ReadAsync(token))throw Missing();
                    recovery=reader.GetBoolean(0);
                    integrity=reader.GetBoolean(1);
                    actions=reader.GetBoolean(2);
                    resolvedBy=reader.IsDBNull(3)?null:reader.GetGuid(3);
                    rootCause=reader.IsDBNull(4)?null:reader.GetString(4);
                }
                // No caller-provided boolean may establish health or integrity.
                if(next==IncidentState.Resolved)
                {
                    if(string.IsNullOrEmpty(rootCause))throw Conflict();
                    await using var fresh=Command(conn,tx,"""
                        SELECT COUNT(DISTINCT observation_code)
                        FROM it_incident_evidence
                        WHERE organization_id=@org AND incident_id=@id
                          AND kind='RecoveryProbe' AND source_status='healthy'
                          AND observed_at_utc>=clock_timestamp()-make_interval(secs=>@fresh::double precision)
                          AND observation_code IN (@readiness,'incident_history_integrity_verified')
                        """,("org",org),("id",id),("fresh",(double)RecoveryFreshnessSeconds),
                        ("readiness",ReadinessCode(current.ServiceCode)));
                    if(Convert.ToInt64(await fresh.ExecuteScalarAsync(token))!=2)throw Conflict();
                }
                await using(var pending=Command(conn,tx,"""
                    SELECT EXISTS(SELECT 1 FROM it_incident_actions
                    WHERE organization_id=@org AND incident_id=@id
                      AND state NOT IN ('Verified','Cancelled'))
                    """,("org",org),("id",id)))
                    actions=!Convert.ToBoolean(await pending.ExecuteScalarAsync(token));
                if(next==IncidentState.Closed)
                {
                    if(!IncidentPolicy.ParseSeverity(current.Severity,out var severity))throw Conflict();
                    independentlyConfirmed= !IncidentPolicy.IndependentClosureRequired(severity) ||
                        (resolvedBy.HasValue && resolvedBy.Value!=actor.UserId);
                }
                if(reopen ? (!IncidentPolicy.CanReopen(from)||next!=IncidentState.Investigating) :
                    !IncidentPolicy.CanTransition(from,next,recovery,integrity,actions,independentlyConfirmed))
                    throw Conflict();
                var version=current.Version+1;
                var clear=reopen || (from==IncidentState.Monitoring && next==IncidentState.Investigating);
                await using(var update=Command(conn,tx,"""
                    UPDATE it_incidents
                       SET state=@state,version=@version,updated_at_utc=now(),
                           resolved_by=CASE WHEN @clear THEN NULL
                               WHEN @state='Resolved' THEN @actor ELSE resolved_by END,
                           closed_by=CASE WHEN @clear THEN NULL
                               WHEN @state='Closed' THEN @actor ELSE closed_by END,
                           resolved_at_utc=CASE WHEN @clear THEN NULL
                               WHEN @state='Resolved' THEN now() ELSE resolved_at_utc END,
                           closed_at_utc=CASE WHEN @clear THEN NULL
                               WHEN @state='Closed' THEN now() ELSE closed_at_utc END,
                           recovery_verified=CASE WHEN @clear THEN false ELSE recovery_verified END,
                           integrity_verified=CASE WHEN @clear THEN false ELSE integrity_verified END,
                           actions_dispositioned=@actions,
                           root_cause_code=CASE WHEN @clear THEN NULL ELSE root_cause_code END
                    WHERE organization_id=@org AND id=@id AND version=@previous
                    """,("state",next.ToString()),("version",version),("org",org),("id",id),
                    ("previous",current.Version),("actor",actor.UserId),
                    ("clear",clear),("actions",actions)))
                    if(await update.ExecuteNonQueryAsync(token)!=1)throw Conflict();
                await Event(conn,tx,org,id,actor.UserId,version,reopen?"Reopened":"Transitioned",
                    current.State,next.ToString(),reason,token);
                await FinalizeOperation(conn,tx,actor,org,operationKey,id,version,token);
                return new IncidentChangeResult(await Read(conn,tx,org,id,false,token),false);
            },ct);
    }

    public Task<IncidentChangeResult> AddNoteAsync(CurrentSession actor,Guid org,
        Guid id,IncidentNoteInput input,Guid operationKey,CancellationToken ct)
    {
        var body=IncidentPolicy.Note(input.Body);
        if(input.ExpectedVersion<1)throw Invalid();
        var hash=Digest($"{id:D}|{input.ExpectedVersion}|{body}");
        return Scoped(actor,org,OrganizationPermission.AppendIncidentNote,
            async(conn,tx,role,token)=>
            {
                var existing=await Reserve(conn,tx,actor,org,operationKey,"Note",hash,token);
                if(existing.HasValue)
                    return new IncidentChangeResult(await Read(conn,tx,org,existing.Value,false,token),true);
                var current=await Read(conn,tx,org,id,true,token);
                if(current.Version!=input.ExpectedVersion)throw Conflict();
                await using(var insert=Command(conn,tx,"""
                    INSERT INTO it_incident_notes(organization_id,incident_id,body,actor_user_id)
                    VALUES(@org,@id,@body,@actor)
                    """,("org",org),("id",id),("body",body),("actor",actor.UserId)))
                    if(await insert.ExecuteNonQueryAsync(token)!=1)throw Conflict();
                var version=current.Version+1;
                await using(var update=Command(conn,tx,"""
                    UPDATE it_incidents SET version=@version,updated_at_utc=now()
                    WHERE organization_id=@org AND id=@id AND version=@old
                    """,("version",version),("org",org),("id",id),("old",current.Version)))
                    if(await update.ExecuteNonQueryAsync(token)!=1)throw Conflict();
                await Event(conn,tx,org,id,actor.UserId,version,"NoteAdded",
                    current.State,current.State,"note_added",token);
                await FinalizeOperation(conn,tx,actor,org,operationKey,id,version,token);
                return new IncidentChangeResult(await Read(conn,tx,org,id,false,token),false);
            },ct);
    }

    public Task<IncidentChangeResult> AssignAsync(CurrentSession actor,Guid org, Guid id,
        IncidentAssignment input,Guid operationKey,CancellationToken ct)
    {
        if(input.AssignedTo==Guid.Empty||input.ExpectedVersion<1)throw Invalid();
        var reason=IncidentPolicy.Reason(input.ReasonCode);
        var hash=Digest($"{id:D}|{input.ExpectedVersion}|{input.AssignedTo:D}|{reason}");
        return Scoped(actor,org,OrganizationPermission.AssignIncident,
            async(conn,tx,role,token)=>
            {
                var existing=await Reserve(conn,tx,actor,org,operationKey,"Assign",hash,token);
                if(existing.HasValue)
                    return new IncidentChangeResult(await Read(conn,tx,org,existing.Value,false,token),true);
                var current=await Read(conn,tx,org,id,true,token);
                if(current.Version!=input.ExpectedVersion)throw Conflict();
                await using(var eligible=Command(conn,tx,"""
                    SELECT 1 FROM organization_memberships m
                    JOIN users u ON u.id=m.user_id
                    WHERE m.organization_id=@org AND m.user_id=@assignee
                      AND m.status='Active' AND u.status='Active'
                      AND m.role IN ('Support','Administrator')
                    FOR SHARE OF m,u
                    """,("org",org),("assignee",input.AssignedTo)))
                    if(await eligible.ExecuteScalarAsync(token) is null)throw Deny();
                var version=current.Version+1;
                await using(var update=Command(conn,tx,"""
                    UPDATE it_incidents SET assigned_to=@assignee,version=@version,updated_at_utc=now()
                    WHERE organization_id=@org AND id=@id AND version=@old
                    """,("assignee",input.AssignedTo),("version",version),
                    ("org",org),("id",id),("old",current.Version)))
                    if(await update.ExecuteNonQueryAsync(token)!=1)throw Conflict();
                await Event(conn,tx,org,id,actor.UserId,version,"Assigned",
                    current.State,current.State,reason,token);
                await FinalizeOperation(conn,tx,actor,org,operationKey,id,version,token);
                return new IncidentChangeResult(await Read(conn,tx,org,id,false,token),false);
            },ct);
    }
}
