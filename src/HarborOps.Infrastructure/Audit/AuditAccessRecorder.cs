using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HarborOps.Infrastructure.Audit;

public sealed class AuditAccessRecorder(HarborOpsDbContext db)
{
    public async Task RecordAsync(Guid organizationId, Guid actorId, Guid sessionId,
        Guid? auditId, string? correlationId, bool authorized, CancellationToken ct)
    {
        if (!authorized)
        {
            // Record an attempted foreign read against the actor's selected
            // organization, never write foreign actor metadata into the target tenant.
            var selected = await db.AuthSessions.AsNoTracking()
                .Where(s => s.Id == sessionId && s.UserId == actorId)
                .Select(s => s.SelectedOrganizationId).SingleOrDefaultAsync(ct);
            if (!selected.HasValue) return;
            organizationId = selected.Value;
        }
        var code = authorized ? "audit.read" : "audit.read_denied";
        var correlation = string.IsNullOrWhiteSpace(correlationId)
            ? "database" : correlationId.Length > 64 ? correlationId[..64] : correlationId;
        // This is a restricted, security-definer database function that only
        // accepts audit-read outcomes. The runtime role has no audit UPDATE/DELETE.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT m16_record_audit_access({organizationId},
                CAST({code} AS varchar), {actorId}, {sessionId},
                {auditId}, CAST({correlation} AS varchar))
            """, ct);
    }
}
