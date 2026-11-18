using HarborOps.Domain.Incidents;
using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure.Identity;

namespace HarborOps.Infrastructure.Incidents;

public sealed record IncidentRecoveryInput(long ExpectedVersion, string RootCauseCode);
public sealed record IncidentSnapshotInput(long ExpectedVersion);

public sealed partial class IncidentStore
{
    public Task<IncidentChangeResult> RecordRecoveryAsync(CurrentSession actor,Guid org,
        Guid id,IncidentRecoveryInput input,Guid operationKey,CancellationToken ct)
    {
        if(id==Guid.Empty||input.ExpectedVersion<1)throw Invalid();
        var cause=IncidentPolicy.Reason(input.RootCauseCode);
        var hash=Digest($"{id:D}|{input.ExpectedVersion}|{cause}");
        return Scoped(actor,org,OrganizationPermission.ManageIncidentLifecycle,
            async(conn,tx,role,token)=>
            {
                var existing=await Reserve(conn,tx,actor,org,operationKey,"Recovery",hash,token);
                if(existing.HasValue)
                    return new IncidentChangeResult(await Read(conn,tx,org,existing.Value,false,token),true);
                var current=await Read(conn,tx,org,id,true,token);
                if(current.Version!=input.ExpectedVersion||current.State!="Monitoring"||
                    current.ServiceCode is not ("api" or "database"))throw Conflict();

                // These are verifiable conditions, not flags supplied by the browser.
                long count, first, last;
                await using(var check=Command(conn,tx,"""
                    SELECT count(*),coalesce(min(sequence),0),coalesce(max(sequence),0)
                    FROM it_incident_events WHERE organization_id=@org AND incident_id=@id
                    """,("org",org),("id",id)))
                await using(var reader=await check.ExecuteReaderAsync(token))
                {
                    if(!await reader.ReadAsync(token))throw Conflict();
                    count=reader.GetInt64(0);first=reader.GetInt64(1);last=reader.GetInt64(2);
                }
                if(count!=current.Version||first!=1||last!=current.Version)throw Conflict();
                await using(var probe=Command(conn,tx,"SELECT 1"))
                    if(Convert.ToInt32(await probe.ExecuteScalarAsync(token))!=1)throw Conflict();

                bool actions;
                await using(var pending=Command(conn,tx,"""
                    SELECT EXISTS(SELECT 1 FROM it_incident_actions
                    WHERE organization_id=@org AND incident_id=@id
                      AND state NOT IN('Verified','Cancelled'))
                    """,("org",org),("id",id)))
                    actions=!Convert.ToBoolean(await pending.ExecuteScalarAsync(token));

                Guid? firstEvidence=null;
                foreach(var code in new[]{"database_readiness_healthy","incident_history_integrity_verified"})
                {
                    var eid=Guid.NewGuid();firstEvidence ??= eid;
                    await using var evidence=Command(conn,tx,"""
                        INSERT INTO it_incident_evidence(id,organization_id,incident_id,kind,
                            service_code,observation_code,source_status,observed_at_utc,actor_user_id)
                        VALUES(@eid,@org,@id,'RecoveryProbe',@service,@code,'healthy',now(),@actor)
                        """,("eid",eid),("org",org),("id",id),("service",current.ServiceCode),
                        ("code",code),("actor",actor.UserId));
                    if(await evidence.ExecuteNonQueryAsync(token)!=1)throw Conflict();
                }

                var version=current.Version+1;
                await using(var update=Command(conn,tx,"""
                    UPDATE it_incidents SET root_cause_code=@cause,recovery_verified=true,
                        integrity_verified=true,actions_dispositioned=@actions,
                        version=@new,updated_at_utc=now()
                    WHERE organization_id=@org AND id=@id AND version=@old
                    """,("cause",cause),("actions",actions),("new",version),
                    ("org",org),("id",id),("old",current.Version)))
                    if(await update.ExecuteNonQueryAsync(token)!=1)throw Conflict();
                await Event(conn,tx,org,id,actor.UserId,version,"RecoveryVerified",
                    current.State,current.State,"verified_recovery",token,firstEvidence);
                await FinalizeOperation(conn,tx,actor,org,operationKey,id,version,token);
                return new IncidentChangeResult(await Read(conn,tx,org,id,false,token),false);
            },ct);
    }

    public Task<IncidentChangeResult> CaptureSafeSnapshotAsync(CurrentSession actor,Guid org,
        Guid id,IncidentSnapshotInput input,Guid operationKey,
        string trustedStatus,DateTimeOffset observed,CancellationToken ct)
    {
        if(input.ExpectedVersion<1 || trustedStatus is not ("healthy" or "degraded" or "unknown") ||
            observed>DateTimeOffset.UtcNow.AddSeconds(30))
            throw Invalid();
        var hash=Digest($"{id:D}|{input.ExpectedVersion}|{trustedStatus}|{observed:O}");
        return Scoped(actor,org,OrganizationPermission.AttachIncidentEvidence,
            async(conn,tx,role,token)=>
            {
                var existing=await Reserve(conn,tx,actor,org,operationKey,"M17Snapshot",hash,token);
                if(existing.HasValue)
                    return new IncidentChangeResult(await Read(conn,tx,org,existing.Value,false,token),true);
                var current=await Read(conn,tx,org,id,true,token);
                if(current.Version!=input.ExpectedVersion||
                    current.ServiceCode is not ("api" or "database"))throw Conflict();
                var evidenceId=Guid.NewGuid();
                await using(var insert=Command(conn,tx,"""
                    INSERT INTO it_incident_evidence(id,organization_id,incident_id,kind,
                        service_code,observation_code,source_status,observed_at_utc,actor_user_id)
                    VALUES(@eid,@org,@id,'M17Snapshot',@service,
                        'database_readiness',@status,@at,@actor)
                    """,("eid",evidenceId),("org",org),("id",id),("service",current.ServiceCode),
                    ("status",trustedStatus),("at",observed),("actor",actor.UserId)))
                    if(await insert.ExecuteNonQueryAsync(token)!=1)throw Conflict();
                var version=current.Version+1;
                await using(var update=Command(conn,tx,"""
                    UPDATE it_incidents SET version=@new,updated_at_utc=now()
                    WHERE organization_id=@org AND id=@id AND version=@old
                    """,("new",version),("org",org),("id",id),("old",current.Version)))
                    if(await update.ExecuteNonQueryAsync(token)!=1)throw Conflict();
                await Event(conn,tx,org,id,actor.UserId,version,"M17SnapshotAdded",
                    current.State,current.State,"safe_service_snapshot",token,evidenceId);
                await FinalizeOperation(conn,tx,actor,org,operationKey,id,version,token);
                return new IncidentChangeResult(await Read(conn,tx,org,id,false,token),false);
            },ct);
    }
}
