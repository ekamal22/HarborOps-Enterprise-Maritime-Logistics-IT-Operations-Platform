using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure.Identity;

namespace HarborOps.Infrastructure.Incidents;

// NextAfterUtc is the creation time of the last returned incident. Pages are ordered by
// creation, which never changes, so an incident that is edited while a client is paging can
// neither move to a different page nor drop out of the traversal.
public sealed record IncidentPage(IReadOnlyList<IncidentView> Incidents,
    DateTimeOffset? NextAfterUtc,Guid? NextId);

public sealed partial class IncidentStore
{
    public Task<IncidentPage> PageAsync(CurrentSession actor,Guid org,int limit,
        DateTimeOffset asOf,DateTimeOffset? afterAt,Guid? afterId,CancellationToken ct)
    {
        if(limit is <1 or >100||asOf>DateTimeOffset.UtcNow.AddMinutes(1)||
            afterAt.HasValue != afterId.HasValue)throw Invalid();
        return Scoped(actor,org,OrganizationPermission.ViewIncidentSummary,
            async(conn,tx,role,token)=>
            {
                var rows=new List<IncidentView>();
                var created=new List<DateTimeOffset>();
                await using var command=Command(conn,tx,"""
                    SELECT id,title,service_code,severity,state,impact_code,version,
                        updated_at_utc,assigned_to,created_at_utc
                    FROM it_incidents
                    WHERE organization_id=@org AND created_at_utc<=@asof
                      AND (@hasafter=false OR created_at_utc<@after
                          OR(created_at_utc=@after AND id<@afterid))
                    ORDER BY created_at_utc DESC,id DESC LIMIT @limit
                    """,("org",org),("asof",asOf),("hasafter",afterAt.HasValue),
                    ("after",afterAt ?? DateTimeOffset.UnixEpoch),
                    ("afterid",afterId ?? Guid.Empty),("limit",limit+1));
                await using var reader=await command.ExecuteReaderAsync(token);
                while(await reader.ReadAsync(token))
                {
                    rows.Add(Redact(role,Map(reader)));
                    created.Add(reader.GetFieldValue<DateTimeOffset>(9));
                }
                if(rows.Count<=limit)return new IncidentPage(rows,null,null);
                rows.RemoveAt(rows.Count-1);
                created.RemoveAt(created.Count-1);
                return new IncidentPage(rows,created[^1],rows[^1].Id);
            },ct);
    }
}
