using HarborOps.Domain.Incidents;
using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure.Identity;
using Npgsql;

namespace HarborOps.Infrastructure.Incidents;

public sealed record IncidentEventView(Guid Id, long Sequence, string Kind, string? FromState,
    string? ToState, string ReasonCode, Guid ActorUserId, DateTimeOffset OccurredAtUtc, Guid? RelatedId);
public sealed record IncidentNoteView(Guid Id, string Body, Guid ActorUserId, DateTimeOffset OccurredAtUtc);
public sealed record IncidentActionView(Guid Id, string Title, string State, Guid OwnerUserId, long Version);
public sealed record IncidentEvidenceView(Guid Id, string Kind, string ObservationCode,
    string SourceStatus, DateTimeOffset ObservedAtUtc, DateTimeOffset CapturedAtUtc);
public sealed record IncidentHistory(IncidentView Incident, IReadOnlyList<IncidentEventView> Events,
    IReadOnlyList<IncidentNoteView> Notes, IReadOnlyList<IncidentActionView> Actions,
    IReadOnlyList<IncidentEvidenceView> Evidence);
public sealed record IncidentActionCreate(long ExpectedVersion, string Title, Guid OwnerUserId);
public sealed record IncidentActionUpdate(long ExpectedVersion, Guid ActionId, long ExpectedActionVersion,
    string ToState, string ReasonCode);
public sealed record IncidentObservation(long ExpectedVersion, string ObservationCode);
public sealed record IncidentReadGrantView(Guid UserId, string Status, DateTimeOffset ExpiresAtUtc, long Version);

public sealed partial class IncidentStore
{
    public Task<IncidentHistory> HistoryAsync(CurrentSession actor,Guid org,Guid id,CancellationToken ct) =>
        Scoped(actor,org,OrganizationPermission.ViewIncidentDetail,
            async(conn,tx,role,token)=>
            {
                var incident=await Read(conn,tx,org,id,false,token);
                var events=new List<IncidentEventView>();
                await using(var cmd=Command(conn,tx,"""
                    SELECT id,sequence,kind,from_state,to_state,reason_code,
                        actor_user_id,occurred_at_utc,related_id
                    FROM it_incident_events
                    WHERE organization_id=@org AND incident_id=@id
                    ORDER BY sequence
                    """,("org",org),("id",id)))
                await using(var reader=await cmd.ExecuteReaderAsync(token))
                    while(await reader.ReadAsync(token))
                        events.Add(new(reader.GetGuid(0),reader.GetInt64(1),reader.GetString(2),
                            reader.IsDBNull(3)?null:reader.GetString(3),
                            reader.IsDBNull(4)?null:reader.GetString(4),
                            reader.GetString(5),reader.GetGuid(6),
                            reader.GetFieldValue<DateTimeOffset>(7),
                            reader.IsDBNull(8)?null:reader.GetGuid(8)));
                var notes=new List<IncidentNoteView>();
                if(role!=OrganizationRole.Auditor)
                {
                await using(var cmd=Command(conn,tx,"""
                    SELECT id,body,actor_user_id,occurred_at_utc
                    FROM it_incident_notes
                    WHERE organization_id=@org AND incident_id=@id
                    ORDER BY occurred_at_utc,id LIMIT 100
                    """,("org",org),("id",id)))
                await using(var reader=await cmd.ExecuteReaderAsync(token))
                    while(await reader.ReadAsync(token))
                        notes.Add(new(reader.GetGuid(0),reader.GetString(1),reader.GetGuid(2),
                            reader.GetFieldValue<DateTimeOffset>(3)));
                }
                var actions=new List<IncidentActionView>();
                await using(var cmd=Command(conn,tx,"""
                    SELECT id,title,state,owner_user_id,version FROM it_incident_actions
                    WHERE organization_id=@org AND incident_id=@id
                    ORDER BY created_at_utc,id LIMIT 100
                    """,("org",org),("id",id)))
                await using(var reader=await cmd.ExecuteReaderAsync(token))
                    while(await reader.ReadAsync(token))
                        actions.Add(new(reader.GetGuid(0),reader.GetString(1),reader.GetString(2),
                            reader.GetGuid(3),reader.GetInt64(4)));
                var evidence=new List<IncidentEvidenceView>();
                await using(var cmd=Command(conn,tx,"""
                    SELECT id,kind,observation_code,source_status,observed_at_utc,captured_at_utc
                    FROM it_incident_evidence
                    WHERE organization_id=@org AND incident_id=@id
                    ORDER BY captured_at_utc,id LIMIT 100
                    """,("org",org),("id",id)))
                await using(var reader=await cmd.ExecuteReaderAsync(token))
                    while(await reader.ReadAsync(token))
                        evidence.Add(new(reader.GetGuid(0),reader.GetString(1),reader.GetString(2),
                            reader.GetString(3),reader.GetFieldValue<DateTimeOffset>(4),
                            reader.GetFieldValue<DateTimeOffset>(5)));
                return new IncidentHistory(incident,events,notes,actions,evidence);
            },ct);

    public Task<IncidentChangeResult> AddActionAsync(CurrentSession actor,Guid org,Guid id,
        IncidentActionCreate input,Guid operationKey,CancellationToken ct)
    {
        if(input.ExpectedVersion<1||input.OwnerUserId==Guid.Empty)throw Invalid();
        var title=IncidentPolicy.Title(input.Title);
        var hash=Digest($"{id:D}|{input.ExpectedVersion}|{input.OwnerUserId:D}|{title}");
        return Scoped(actor,org,OrganizationPermission.ManageIncidentActions,
            async(conn,tx,role,token)=>
            {
                var existing=await Reserve(conn,tx,actor,org,operationKey,"ActionCreate",hash,token);
                if(existing.HasValue)
                    return new IncidentChangeResult(await Read(conn,tx,org,existing.Value,false,token),true);
                var current=await Read(conn,tx,org,id,true,token);
                if(current.Version!=input.ExpectedVersion)throw Conflict();
                await using(var check=Command(conn,tx,"""
                    SELECT 1 FROM organization_memberships m JOIN users u ON m.user_id=u.id
                    WHERE m.organization_id=@org AND m.user_id=@owner
                      AND m.status='Active' AND u.status='Active'
                      AND m.role IN ('Support','Administrator')
                    FOR SHARE OF m,u
                    """,("org",org),("owner",input.OwnerUserId)))
                    if(await check.ExecuteScalarAsync(token) is null)throw Deny();
                var actionId=Guid.NewGuid();
                await using(var insert=Command(conn,tx,"""
                    INSERT INTO it_incident_actions(id,organization_id,incident_id,
                        title,owner_user_id) VALUES(@aid,@org,@id,@title,@owner)
                    """,("aid",actionId),("org",org),("id",id),("title",title),
                    ("owner",input.OwnerUserId)))
                    if(await insert.ExecuteNonQueryAsync(token)!=1)throw Conflict();
                var version=current.Version+1;
                await using(var update=Command(conn,tx,"""
                    UPDATE it_incidents SET version=@new,updated_at_utc=now()
                    WHERE organization_id=@org AND id=@id AND version=@old
                    """,("new",version),("org",org),("id",id),("old",current.Version)))
                    if(await update.ExecuteNonQueryAsync(token)!=1)throw Conflict();
                await Event(conn,tx,org,id,actor.UserId,version,"ActionCreated",
                    current.State,current.State,"action_created",token,actionId);
                await FinalizeOperation(conn,tx,actor,org,operationKey,id,version,token);
                return new IncidentChangeResult(await Read(conn,tx,org,id,false,token),false);
            },ct);
    }

    public Task<IncidentChangeResult> UpdateActionAsync(CurrentSession actor,Guid org,Guid id,
        IncidentActionUpdate input,Guid operationKey,CancellationToken ct)
    {
        if(input.ExpectedVersion<1||input.ExpectedActionVersion<1||input.ActionId==Guid.Empty||
            !Enum.TryParse<IncidentActionState>(input.ToState,false,out var target)||
            !Enum.IsDefined(target)||target.ToString()!=input.ToState)throw Invalid();
        var reason=IncidentPolicy.Reason(input.ReasonCode);
        var hash=Digest($"{id:D}|{input.ExpectedVersion}|{input.ActionId:D}|{input.ExpectedActionVersion}|{target}|{reason}");
        return Scoped(actor,org,OrganizationPermission.ManageIncidentActions,
            async(conn,tx,role,token)=>
            {
                var existing=await Reserve(conn,tx,actor,org,operationKey,"ActionChange",hash,token);
                if(existing.HasValue)
                    return new IncidentChangeResult(await Read(conn,tx,org,existing.Value,false,token),true);
                var current=await Read(conn,tx,org,id,true,token);
                if(current.Version!=input.ExpectedVersion)throw Conflict();
                string? oldState;
                await using(var check=Command(conn,tx,"""
                    SELECT state FROM it_incident_actions
                    WHERE organization_id=@org AND incident_id=@id
                      AND id=@aid AND version=@version FOR UPDATE
                    """,("org",org),("id",id),("aid",input.ActionId),
                    ("version",input.ExpectedActionVersion)))
                    oldState=await check.ExecuteScalarAsync(token) as string;
                if(!Enum.TryParse<IncidentActionState>(oldState,false,out var prior)||
                    !IncidentPolicy.CanMoveAction(prior,target))throw Conflict();
                await using(var update=Command(conn,tx,"""
                    UPDATE it_incident_actions SET state=@state,version=version+1,updated_at_utc=now()
                    WHERE organization_id=@org AND incident_id=@id AND id=@aid AND version=@version
                    """,("state",target.ToString()),("org",org),("id",id),("aid",input.ActionId),
                    ("version",input.ExpectedActionVersion)))
                    if(await update.ExecuteNonQueryAsync(token)!=1)throw Conflict();
                var version=current.Version+1;
                await using(var update=Command(conn,tx,"""
                    UPDATE it_incidents SET version=@new,updated_at_utc=now()
                    WHERE organization_id=@org AND id=@id AND version=@old
                    """,("new",version),("org",org),("id",id),("old",current.Version)))
                    if(await update.ExecuteNonQueryAsync(token)!=1)throw Conflict();
                await Event(conn,tx,org,id,actor.UserId,version,"ActionChanged",
                    current.State,current.State,reason,token,input.ActionId);
                await FinalizeOperation(conn,tx,actor,org,operationKey,id,version,token);
                return new IncidentChangeResult(await Read(conn,tx,org,id,false,token),false);
            },ct);
    }

    public Task<IncidentChangeResult> AddObservationAsync(CurrentSession actor,Guid org,
        Guid id,IncidentObservation input,Guid operationKey,CancellationToken ct)
    {
        if(input.ExpectedVersion<1)throw Invalid();
        var code=IncidentPolicy.Reason(input.ObservationCode);
        var hash=Digest($"{id:D}|{input.ExpectedVersion}|{code}");
        return Scoped(actor,org,OrganizationPermission.AttachIncidentEvidence,
            async(conn,tx,role,token)=>
            {
                var existing=await Reserve(conn,tx,actor,org,operationKey,"ManualEvidence",hash,token);
                if(existing.HasValue)
                    return new IncidentChangeResult(await Read(conn,tx,org,existing.Value,false,token),true);
                var current=await Read(conn,tx,org,id,true,token);
                if(current.Version!=input.ExpectedVersion)throw Conflict();
                var evidenceId=Guid.NewGuid();
                await using(var insert=Command(conn,tx,"""
                    INSERT INTO it_incident_evidence(id,organization_id,incident_id,
                        kind,service_code,observation_code,source_status,observed_at_utc,actor_user_id)
                    VALUES(@eid,@org,@id,'ManualObservation',@service,@code,'unknown',now(),@actor)
                    """,("eid",evidenceId),("org",org),("id",id),("service",current.ServiceCode),
                    ("code",code),("actor",actor.UserId)))
                    if(await insert.ExecuteNonQueryAsync(token)!=1)throw Conflict();
                var version=current.Version+1;
                await using(var update=Command(conn,tx,"""
                    UPDATE it_incidents SET version=@new,updated_at_utc=now()
                    WHERE organization_id=@org AND id=@id AND version=@old
                    """,("new",version),("org",org),("id",id),("old",current.Version)))
                    if(await update.ExecuteNonQueryAsync(token)!=1)throw Conflict();
                await Event(conn,tx,org,id,actor.UserId,version,"EvidenceAttached",
                    current.State,current.State,"manual_observation",token,evidenceId);
                await FinalizeOperation(conn,tx,actor,org,operationKey,id,version,token);
                return new IncidentChangeResult(await Read(conn,tx,org,id,false,token),false);
            },ct);
    }

    public Task<IncidentReadGrantView> GrantReadAsync(CurrentSession actor,Guid org,
        Guid recipient,DateTimeOffset expiresAt,CancellationToken ct)
    {
        if(recipient==Guid.Empty||expiresAt<=DateTimeOffset.UtcNow||
            expiresAt>DateTimeOffset.UtcNow.AddDays(30))throw Invalid();
        return Scoped(actor,org,OrganizationPermission.ManageMemberships,
            async(conn,tx,role,token)=>
            {
                if(role!=OrganizationRole.Administrator)throw Deny();
                await using(var check=Command(conn,tx,"""
                    SELECT 1 FROM organization_memberships m JOIN users u ON u.id=m.user_id
                    WHERE m.organization_id=@org AND m.user_id=@recipient
                      AND m.status='Active' AND m.role='Auditor' AND u.status='Active'
                    FOR SHARE OF m,u
                    """,("org",org),("recipient",recipient)))
                    if(await check.ExecuteScalarAsync(token) is null)throw Deny();
                await using(var insert=Command(conn,tx,"""
                    INSERT INTO it_incident_read_grants(organization_id,user_id,granted_by,expires_at_utc)
                    VALUES(@org,@recipient,@actor,@expiry)
                    ON CONFLICT(organization_id,user_id)
                    DO UPDATE SET status='Active',expires_at_utc=excluded.expires_at_utc,
                        granted_by=excluded.granted_by,version=it_incident_read_grants.version+1,
                        revoked_at_utc=NULL
                    RETURNING status,expires_at_utc,version
                    """,("org",org),("recipient",recipient),("actor",actor.UserId),("expiry",expiresAt));
                await using var reader=await insert.ExecuteReaderAsync(token);
                if(!await reader.ReadAsync(token))throw Conflict();
                return new IncidentReadGrantView(recipient,reader.GetString(0),
                    reader.GetFieldValue<DateTimeOffset>(1),reader.GetInt64(2));
            },ct);
    }

    public Task<IncidentReadGrantView> RevokeReadAsync(CurrentSession actor,Guid org,
        Guid recipient,long expectedVersion,CancellationToken ct)
    {
        if(recipient==Guid.Empty||expectedVersion<1)throw Invalid();
        return Scoped(actor,org,OrganizationPermission.ManageMemberships,
            async(conn,tx,role,token)=>
            {
                if(role!=OrganizationRole.Administrator)throw Deny();
                await using var update=Command(conn,tx,"""
                    UPDATE it_incident_read_grants
                    SET status='Revoked',revoked_at_utc=now(),version=version+1
                    WHERE organization_id=@org AND user_id=@recipient
                      AND status='Active' AND version=@expected
                    RETURNING status,expires_at_utc,version
                    """,("org",org),("recipient",recipient),("expected",expectedVersion));
                await using var reader=await update.ExecuteReaderAsync(token);
                if(!await reader.ReadAsync(token))throw Conflict();
                return new IncidentReadGrantView(recipient,reader.GetString(0),
                    reader.GetFieldValue<DateTimeOffset>(1),reader.GetInt64(2));
            },ct);
    }
}
