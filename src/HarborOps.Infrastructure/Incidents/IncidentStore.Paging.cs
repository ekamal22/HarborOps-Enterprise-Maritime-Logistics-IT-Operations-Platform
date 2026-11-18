using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure.Identity;

namespace HarborOps.Infrastructure.Incidents;

public sealed record IncidentPage(IReadOnlyList<IncidentView> Incidents,
    DateTimeOffset? NextUpdatedAtUtc,Guid? NextId);

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
                await using var command=Command(conn,tx,"""
                    SELECT id,title,service_code,severity,state,impact_code,version,
                        updated_at_utc,assigned_to
                    FROM it_incidents
                    WHERE organization_id=@org AND updated_at_utc<=@asof
                      AND (@hasafter=false OR updated_at_utc<@after
                          OR(updated_at_utc=@after AND id<@afterid))
                    ORDER BY updated_at_utc DESC,id DESC LIMIT @limit
                    """,("org",org),("asof",asOf),("hasafter",afterAt.HasValue),
                    ("after",afterAt ?? DateTimeOffset.UnixEpoch),
                    ("afterid",afterId ?? Guid.Empty),("limit",limit+1));
                await using var reader=await command.ExecuteReaderAsync(token);
                while(await reader.ReadAsync(token))rows.Add(Map(reader));
                if(rows.Count<=limit)return new IncidentPage(rows,null,null);
                rows.RemoveAt(rows.Count-1);
                var last=rows[^1];
                return new IncidentPage(rows,last.UpdatedAtUtc,last.Id);
            },ct);
    }
}
