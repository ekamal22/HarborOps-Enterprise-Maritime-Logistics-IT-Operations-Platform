using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HarborOps.Infrastructure.Audit;

public sealed record AuditPage(IReadOnlyList<AuditEntry> Entries, int? NextPage);

public sealed class AuditReader(HarborOpsDbContext db, TimeProvider clock)
{
    private IQueryable<AuditEntry> Visible(Guid organizationId, Guid actorId, Guid sessionId)
    {
        var now = clock.GetUtcNow();
        return db.AuditEntries.AsNoTracking().Where(e => e.OrganizationId == organizationId)
            .Where(e => db.AuthSessions.AsNoTracking().Any(s =>
                s.Id == sessionId && s.UserId == actorId &&
                s.SelectedOrganizationId == organizationId &&
                s.RevokedAtUtc == null && s.IdleExpiresAtUtc > now &&
                s.AbsoluteExpiresAtUtc > now))
            .Where(e => db.AuthUsers.AsNoTracking().Any(u =>
                u.Id == actorId && u.Status == "Active"))
            .Where(e => db.Organizations.AsNoTracking().Any(o =>
                o.Id == organizationId && o.Status == "Active"))
            .Where(e => db.OrganizationMemberships.AsNoTracking().Any(m =>
                m.OrganizationId == organizationId && m.UserId == actorId &&
                m.Status == "Active" && m.Role == "Auditor"));
    }

    public async Task<AuditPage> ListAsync(Guid organizationId, Guid actorId, Guid sessionId,
        int page, int limit, CancellationToken ct)
    {
        if (page is < 1 or > 1000 || limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(page));
        var rows = await Visible(organizationId, actorId, sessionId)
            .OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id)
            .Skip((page - 1) * limit).Take(limit + 1).ToListAsync(ct);
        var next = rows.Count > limit ? page + 1 : (int?)null;
        if (rows.Count > limit) rows.RemoveAt(limit);
        return new AuditPage(rows, next);
    }

    public Task<AuditEntry?> GetAsync(Guid organizationId, Guid actorId,
        Guid sessionId, Guid auditId, CancellationToken ct) =>
        Visible(organizationId, actorId, sessionId)
            .SingleOrDefaultAsync(e => e.Id == auditId, ct);
}
