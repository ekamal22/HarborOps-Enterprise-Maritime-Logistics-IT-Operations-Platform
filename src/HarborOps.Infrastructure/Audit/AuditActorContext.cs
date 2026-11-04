using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HarborOps.Infrastructure.Audit;

// Transaction-local context is supplied by the server after authentication.
// It cannot leak across pooled PostgreSQL connections at transaction end.
public static class AuditActorContext
{
    public static async Task SetHumanAsync(HarborOpsDbContext db, Guid userId,
        Guid? sessionId, string? correlationId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Audit actor context requires a transaction");
        var correlation = string.IsNullOrWhiteSpace(correlationId) ? "database" : correlationId;
        if (correlation.Length > 64) correlation = correlation[..64];
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT set_config('harborops.audit_actor_kind', {"Human"}, true),
                set_config('harborops.audit_actor_user_id', {userId.ToString("D")}, true),
                set_config('harborops.audit_actor_session_id', {sessionId?.ToString("D") ?? ""}, true),
                set_config('harborops.audit_correlation_id', {correlation}, true)
            """, ct);
    }

    public static async Task SetOperatorAsync(HarborOpsDbContext db,
        string? correlationId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Audit actor context requires a transaction");
        var correlation = string.IsNullOrWhiteSpace(correlationId) ? "local-cli" : correlationId;
        if (correlation.Length > 64) correlation = correlation[..64];
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT set_config('harborops.audit_actor_kind', {"OperatorCli"}, true),
                set_config('harborops.audit_actor_user_id', {""}, true),
                set_config('harborops.audit_actor_session_id', {""}, true),
                set_config('harborops.audit_correlation_id', {correlation}, true)
            """, ct);
    }
}
