using HarborOps.Domain.Incidents;
using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure.Identity;

namespace HarborOps.Infrastructure.Incidents;

public sealed record IncidentImpactUpdate(long ExpectedVersion, string ImpactCode, string ReasonCode);
public sealed record IncidentSeverityUpdate(long ExpectedVersion, string Severity, string ReasonCode);

public sealed partial class IncidentStore
{
    public Task<IncidentChangeResult> ReportImpactAsync(CurrentSession actor,Guid org,
        Guid id,IncidentImpactUpdate input,Guid operationKey,CancellationToken ct)
    {
        if(id==Guid.Empty || input.ExpectedVersion<1)throw Invalid();
        var code=IncidentPolicy.Reason(input.ImpactCode);
        var reason=IncidentPolicy.Reason(input.ReasonCode);
        var digest=Digest($"{id:D}|{input.ExpectedVersion}|{code}|{reason}");
        return Scoped(actor,org,OrganizationPermission.ReportIncidentImpact,
            async(conn,tx,role,token)=>
            {
                var already=await Reserve(conn,tx,actor,org,operationKey,"Impact",digest,token);
                if(already.HasValue)
                {
                    var seen=await Read(conn,tx,org,already.Value,false,token);
                    return new IncidentChangeResult(role==OrganizationRole.Manager
                        ? seen with { AssignedTo=null } : seen,true);
                }
                var old=await Read(conn,tx,org,id,true,token);
                if(old.Version!=input.ExpectedVersion || old.State=="Closed")throw Conflict();
                var version=old.Version+1;
                await using(var cmd=Command(conn,tx,"""
                    UPDATE it_incidents SET impact_code=@impact,version=@next,updated_at_utc=now()
                    WHERE organization_id=@org AND id=@id AND version=@previous
                    """,("impact",code),("next",version),("org",org),("id",id),
                    ("previous",old.Version)))
                    if(await cmd.ExecuteNonQueryAsync(token)!=1)throw Conflict();
                await Event(conn,tx,org,id,actor.UserId,version,"ImpactUpdated",
                    old.State,old.State,reason,token);
                await FinalizeOperation(conn,tx,actor,org,operationKey,id,version,token);
                var updated=await Read(conn,tx,org,id,false,token);
                return new IncidentChangeResult(role==OrganizationRole.Manager
                    ? updated with { AssignedTo=null } : updated,false);
            },ct);
    }

    public Task<IncidentChangeResult> ReclassifySeverityAsync(CurrentSession actor,Guid org,
        Guid id,IncidentSeverityUpdate input,Guid operationKey,CancellationToken ct)
    {
        if(id==Guid.Empty || input.ExpectedVersion<1 ||
            !IncidentPolicy.ParseSeverity(input.Severity,out _))throw Invalid();
        var reason=IncidentPolicy.Reason(input.ReasonCode);
        var digest=Digest($"{id:D}|{input.ExpectedVersion}|{input.Severity}|{reason}");
        return Scoped(actor,org,OrganizationPermission.ManageIncidentLifecycle,
            async(conn,tx,role,token)=>
            {
                var already=await Reserve(conn,tx,actor,org,operationKey,
                    "Severity",digest,token);
                if(already.HasValue)
                    return new IncidentChangeResult(await Read(conn,tx,org,already.Value,false,token),true);
                var old=await Read(conn,tx,org,id,true,token);
                if(old.Version!=input.ExpectedVersion || old.State=="Closed" ||
                    old.Severity==input.Severity)throw Conflict();
                await using(var evidence=Command(conn,tx,"""
                    SELECT EXISTS(SELECT 1 FROM it_incident_evidence
                        WHERE organization_id=@org AND incident_id=@id)
                    """,("org",org),("id",id)))
                    if(!Convert.ToBoolean(await evidence.ExecuteScalarAsync(token)))throw Conflict();
                var version=old.Version+1;
                await using(var cmd=Command(conn,tx,"""
                    UPDATE it_incidents SET severity=@severity,version=@next,updated_at_utc=now()
                    WHERE organization_id=@org AND id=@id AND version=@previous
                    """,("severity",input.Severity),("next",version),
                    ("org",org),("id",id),("previous",old.Version)))
                    if(await cmd.ExecuteNonQueryAsync(token)!=1)throw Conflict();
                await Event(conn,tx,org,id,actor.UserId,version,"SeverityChanged",
                    old.Severity,input.Severity,reason,token);
                await FinalizeOperation(conn,tx,actor,org,operationKey,id,version,token);
                return new IncidentChangeResult(await Read(conn,tx,org,id,false,token),false);
            },ct);
    }
}
