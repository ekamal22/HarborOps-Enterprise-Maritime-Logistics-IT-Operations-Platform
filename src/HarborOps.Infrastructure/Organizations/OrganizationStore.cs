using HarborOps.Infrastructure.Audit;
using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HarborOps.Infrastructure.Organizations;

public enum OrganizationAccessError { None, SelectionRequired, Revoked, NotFound, Forbidden, Invalid, Conflict }

public sealed class OrganizationScope
{
    internal OrganizationScope(Guid organizationId, Guid userId, Guid sessionId,
        OrganizationRole role, long membershipVersion, long organizationVersion,
        string slug, string displayName)
    {
        OrganizationId = organizationId; UserId = userId; SessionId = sessionId;
        Role = role; MembershipVersion = membershipVersion;
        OrganizationVersion = organizationVersion; Slug = slug; DisplayName = displayName;
    }

    public Guid OrganizationId { get; }
    public Guid UserId { get; }
    public Guid SessionId { get; }
    public OrganizationRole Role { get; }
    public long MembershipVersion { get; }
    public long OrganizationVersion { get; }
    public string Slug { get; }
    public string DisplayName { get; }
}
public sealed record OrganizationSelection(Guid Id, string Slug, string DisplayName,
    OrganizationRole Role, long MembershipVersion);
public sealed record OrganizationProfile(Guid Id, string Slug, string DisplayName, string Status, long Version);
public sealed record MembershipProfile(Guid UserId, string Email, string DisplayName, string Role,
    string Status, long Version);
public sealed record ScopeResult(OrganizationScope? Value, OrganizationAccessError Error);
public sealed record OperationResult<T>(T? Value, OrganizationAccessError Error);

public sealed class OrganizationStore(HarborOpsDbContext db, TimeProvider clock)
{
    public async Task<List<OrganizationSelection>> ListMineAsync(Guid userId, CancellationToken ct)
    {
        var rows = await (from member in db.OrganizationMemberships.AsNoTracking()
                          join org in db.Organizations.AsNoTracking() on member.OrganizationId equals org.Id
                          where member.UserId == userId && member.Status == "Active" && org.Status == "Active"
                          orderby org.DisplayName, org.Id
                          select new { org.Id, org.Slug, org.DisplayName, member.Role, member.Version })
            .ToListAsync(ct);
        return rows.Where(row => OrganizationPolicy.ParseRole(row.Role, out _))
            .Select(row => new OrganizationSelection(row.Id, row.Slug, row.DisplayName,
                Enum.Parse<OrganizationRole>(row.Role), row.Version)).ToList();
    }

    public async Task<ScopeResult> ResolveAsync(Guid userId, Guid sessionId,
        Guid? requestedOrganizationId, OrganizationPermission permission, CancellationToken ct)
    {
        var selected = await db.AuthSessions.AsNoTracking()
            .Where(s => s.Id == sessionId && s.UserId == userId && s.RevokedAtUtc == null)
            .Select(s => s.SelectedOrganizationId).SingleOrDefaultAsync(ct);
        if (selected is null) return new(null, OrganizationAccessError.SelectionRequired);
        if (requestedOrganizationId.HasValue && selected != requestedOrganizationId)
            return new(null, OrganizationAccessError.NotFound);

        var matching = await (from member in db.OrganizationMemberships.AsNoTracking()
                              join org in db.Organizations.AsNoTracking() on member.OrganizationId equals org.Id
                              where member.UserId == userId && member.OrganizationId == selected
                              select new { member, org }).SingleOrDefaultAsync(ct);
        if (matching is null || matching.member.Status != "Active" || matching.org.Status != "Active")
            return new(null, OrganizationAccessError.Revoked);
        if (!OrganizationPolicy.ParseRole(matching.member.Role, out var role))
            return new(null, OrganizationAccessError.Forbidden);
        if (!OrganizationPolicy.Allows(role, permission))
            return new(null, OrganizationAccessError.Forbidden);
        return new(new OrganizationScope(matching.org.Id, userId, sessionId, role,
            matching.member.Version, matching.org.Version, matching.org.Slug,
            matching.org.DisplayName), OrganizationAccessError.None);
    }

    public async Task<OperationResult<OrganizationSelection>> SelectAsync(
        Guid userId, Guid sessionId, Guid organizationId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var org = await db.Organizations
            .FromSqlInterpolated($"SELECT * FROM organizations WHERE id = {organizationId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (org is null || org.Status != "Active")
            return new(null, OrganizationAccessError.NotFound);
        var membership = await db.OrganizationMemberships.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.UserId == userId && m.Status == "Active")
            .SingleOrDefaultAsync(ct);
        if (membership is null || !OrganizationPolicy.ParseRole(membership.Role, out var role))
            return new(null, OrganizationAccessError.NotFound);
        var rows = await db.AuthSessions
            .Where(s => s.Id == sessionId && s.UserId == userId &&
                s.RevokedAtUtc == null && s.IdleExpiresAtUtc > clock.GetUtcNow() &&
                s.AbsoluteExpiresAtUtc > clock.GetUtcNow())
            .ExecuteUpdateAsync(p => p.SetProperty(s => s.SelectedOrganizationId, organizationId), ct);
        if (rows != 1) return new(null, OrganizationAccessError.Revoked);
        await tx.CommitAsync(ct);
        return new(new OrganizationSelection(org.Id, org.Slug, org.DisplayName,
            role, membership.Version), OrganizationAccessError.None);
    }

    public async Task ClearSelectionAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        await db.AuthSessions.Where(s => s.Id == sessionId && s.UserId == userId &&
            s.RevokedAtUtc == null).ExecuteUpdateAsync(
                p => p.SetProperty(s => s.SelectedOrganizationId, (Guid?)null), ct);
    }

    public static OrganizationProfile Profile(OrganizationScope context) =>
        new(context.OrganizationId, context.Slug, context.DisplayName, "Active",
            context.OrganizationVersion);

    public async Task<List<MembershipProfile>> ListMembersAsync(
        OrganizationScope scope, Guid? after, int limit, CancellationToken ct)
    {
        var source = after.HasValue
            ? db.OrganizationMemberships.FromSqlInterpolated(
                $"SELECT * FROM organization_memberships WHERE organization_id = {scope.OrganizationId} AND user_id > {after.Value}")
            : db.OrganizationMemberships.Where(x => x.OrganizationId == scope.OrganizationId);
        var query = from membership in source.AsNoTracking()
                    join user in db.AuthUsers.AsNoTracking() on membership.UserId equals user.Id
                    where membership.OrganizationId == scope.OrganizationId &&
                        db.Organizations.Any(o => o.Id == scope.OrganizationId && o.Status == "Active") &&
                        db.OrganizationMemberships.Any(m => m.OrganizationId == scope.OrganizationId &&
                            m.UserId == scope.UserId && m.Status == "Active" &&
                            m.Role == "Administrator")
                    select new { membership, user };
        return await query.OrderBy(x => x.membership.UserId).Take(Math.Clamp(limit, 1, 100))
            .Select(x => new MembershipProfile(x.user.Id, x.user.Email, x.user.DisplayName,
                x.membership.Role, x.membership.Status, x.membership.Version)).ToListAsync(ct);
    }

    public async Task<MembershipProfile?> GetMemberAsync(
        OrganizationScope scope, Guid targetId, CancellationToken ct) =>
        await (from member in db.OrganizationMemberships.AsNoTracking()
               join user in db.AuthUsers.AsNoTracking() on member.UserId equals user.Id
               where member.OrganizationId == scope.OrganizationId && member.UserId == targetId &&
                   db.Organizations.Any(o => o.Id == scope.OrganizationId && o.Status == "Active") &&
                   db.OrganizationMemberships.Any(m => m.OrganizationId == scope.OrganizationId &&
                       m.UserId == scope.UserId && m.Status == "Active" &&
                       m.Role == "Administrator")
               select new MembershipProfile(user.Id, user.Email, user.DisplayName,
                   member.Role, member.Status, member.Version)).SingleOrDefaultAsync(ct);

    // Every administrative write locks the organization first, then revalidates the
    // actor within the same transaction. The verified HTTP selection is not a grant.
    private async Task<OrganizationAccessError> LockAdminAsync(
        Guid actorId, Guid sessionId, Guid organizationId, CancellationToken ct)
    {
        var org = await db.Organizations
            .FromSqlInterpolated($"SELECT * FROM organizations WHERE id = {organizationId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (org is null) return OrganizationAccessError.NotFound;
        var session = await db.AuthSessions.AsNoTracking()
            .Where(s => s.Id == sessionId && s.UserId == actorId && s.RevokedAtUtc == null &&
                s.IdleExpiresAtUtc > clock.GetUtcNow() && s.AbsoluteExpiresAtUtc > clock.GetUtcNow())
            .Select(s => new { s.SelectedOrganizationId, s.SecurityVersion })
            .SingleOrDefaultAsync(ct);
        if (session is null) return OrganizationAccessError.Revoked;
        if (session.SelectedOrganizationId is null) return OrganizationAccessError.SelectionRequired;
        if (session.SelectedOrganizationId != organizationId) return OrganizationAccessError.NotFound;
        var actor = await (from member in db.OrganizationMemberships.AsNoTracking()
                           join user in db.AuthUsers.AsNoTracking() on member.UserId equals user.Id
                           where member.OrganizationId == organizationId && member.UserId == actorId
                           select new { member, user }).SingleOrDefaultAsync(ct);
        if (org.Status != "Active" || actor is null || actor.member.Status != "Active" ||
            actor.user.Status != "Active" || actor.user.SecurityVersion != session.SecurityVersion)
            return OrganizationAccessError.Revoked;
        if (actor.member.Role != "Administrator") return OrganizationAccessError.Forbidden;
        return OrganizationAccessError.None;
    }

    public async Task<OperationResult<OrganizationProfile>> UpdateProfileAsync(Guid actorId, Guid sessionId,
        Guid orgId, string name, long expectedVersion,
        string correlationId, CancellationToken ct)
    {
        string display;
        try { display = OrganizationPolicy.ValidateDisplayName(name); }
        catch (ArgumentException) { return new(null, OrganizationAccessError.Invalid); }
        if (expectedVersion < 1) return new(null, OrganizationAccessError.Invalid);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
            await AuditActorContext.SetHumanAsync(db, actorId, sessionId, correlationId, ct);
        var access = await LockAdminAsync(actorId, sessionId, orgId, ct);
        if (access != OrganizationAccessError.None) return new(null, access);
        var org = await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == orgId, ct);
        if (org.Version != expectedVersion) return new(null, OrganizationAccessError.Conflict);
        var changed = await db.Organizations.Where(x => x.Id == orgId && x.Version == expectedVersion)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.DisplayName, display)
                .SetProperty(x => x.Version, x => x.Version + 1)
                .SetProperty(x => x.UpdatedAtUtc, clock.GetUtcNow()), ct);
        if (changed != 1) return new(null, OrganizationAccessError.Conflict);
        RecordChange(actorId, sessionId, "organization_updated", correlationId);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(new OrganizationProfile(orgId, org.Slug, display, "Active",
            expectedVersion + 1), OrganizationAccessError.None);
    }

    public async Task<OperationResult<MembershipProfile>> PutMemberAsync(
        Guid actorId, Guid sessionId, Guid orgId, Guid targetId,
        string roleName, string status, long? expectedVersion,
        string correlationId, CancellationToken ct)
    {
        if (actorId == targetId) return new(null, OrganizationAccessError.Forbidden);
        if (!OrganizationPolicy.ParseRole(roleName, out _) ||
            !OrganizationPolicy.ValidStatus(status) || expectedVersion is < 1)
            return new(null, OrganizationAccessError.Invalid);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
            await AuditActorContext.SetHumanAsync(db, actorId, sessionId, correlationId, ct);
        var access = await LockAdminAsync(actorId, sessionId, orgId, ct);
        if (access != OrganizationAccessError.None) return new(null, access);

        var target = await db.AuthUsers.AsNoTracking()
            .Where(x => x.Id == targetId && x.Status == "Active")
            .Select(x => new { x.Email, x.DisplayName }).SingleOrDefaultAsync(ct);
        if (target is null) return new(null, OrganizationAccessError.NotFound);
        var member = await db.OrganizationMemberships.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrganizationId == orgId && x.UserId == targetId, ct);
        var now = clock.GetUtcNow();
        long version;
        if (member is null)
        {
            if (expectedVersion is not null)
                return new(null, OrganizationAccessError.Conflict);
            db.OrganizationMemberships.Add(new OrganizationMembership
            {
                Id = Guid.NewGuid(), OrganizationId = orgId, UserId = targetId,
                Role = roleName, Status = status, Version = 1,
                CreatedAtUtc = now, UpdatedAtUtc = now, GrantedByUserId = actorId
            });
            version = 1;
            // SaveChanges occurs only after all authorization and invariant checks.
            await db.SaveChangesAsync(ct);
        }
        else
        {
            if (expectedVersion != member.Version)
                return new(null, OrganizationAccessError.Conflict);
            if (member.Role == "Administrator" && member.Status == "Active" &&
                (roleName != "Administrator" || status != "Active"))
            {
                var adminCount = await ActiveAdministratorCountAsync(orgId, ct);
                if (adminCount <= 1) return new(null, OrganizationAccessError.Conflict);
            }
            var changed = await db.OrganizationMemberships
                .Where(x => x.Id == member.Id && x.Version == member.Version)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.Role, roleName)
                    .SetProperty(x => x.Status, status)
                    .SetProperty(x => x.Version, x => x.Version + 1)
                    .SetProperty(x => x.UpdatedAtUtc, now)
                    .SetProperty(x => x.GrantedByUserId, actorId), ct);
            if (changed != 1) return new(null, OrganizationAccessError.Conflict);
            version = member.Version + 1;
        }
        if (status != "Active")
            await InvalidateSelectionsAsync(orgId, targetId, ct);
        RecordChange(actorId, sessionId, "membership_changed", correlationId);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(new MembershipProfile(targetId, target.Email, target.DisplayName,
            roleName, status, version), OrganizationAccessError.None);
    }

    public async Task<OrganizationAccessError> RemoveMemberAsync(Guid actorId, Guid sessionId,
        Guid orgId, Guid targetId, long expectedVersion,
        string correlationId, CancellationToken ct)
    {
        if (expectedVersion < 1) return OrganizationAccessError.Invalid;
        if (actorId == targetId) return OrganizationAccessError.Forbidden;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
            await AuditActorContext.SetHumanAsync(db, actorId, sessionId, correlationId, ct);
        var access = await LockAdminAsync(actorId, sessionId, orgId, ct);
        if (access != OrganizationAccessError.None) return access;
        var member = await db.OrganizationMemberships.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrganizationId == orgId && x.UserId == targetId, ct);
        if (member is null) return OrganizationAccessError.NotFound;
        if (member.Version != expectedVersion || member.Status == "Removed")
            return OrganizationAccessError.Conflict;
        if (member.Role == "Administrator" && member.Status == "Active")
        {
            var count = await ActiveAdministratorCountAsync(orgId, ct);
            if (count <= 1) return OrganizationAccessError.Conflict;
        }
        var changed = await db.OrganizationMemberships
            .Where(x => x.Id == member.Id && x.Version == expectedVersion)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, "Removed")
                .SetProperty(x => x.Version, x => x.Version + 1)
                .SetProperty(x => x.UpdatedAtUtc, clock.GetUtcNow()), ct);
        if (changed != 1) return OrganizationAccessError.Conflict;
        await InvalidateSelectionsAsync(orgId, targetId, ct);
        RecordChange(actorId, sessionId, "membership_removed", correlationId);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return OrganizationAccessError.None;
    }

    private Task<int> ActiveAdministratorCountAsync(Guid organizationId, CancellationToken ct) =>
        (from membership in db.OrganizationMemberships.AsNoTracking()
         join user in db.AuthUsers.AsNoTracking() on membership.UserId equals user.Id
         where membership.OrganizationId == organizationId &&
             membership.Role == "Administrator" && membership.Status == "Active" &&
             user.Status == "Active"
         select membership.Id).CountAsync(ct);

    private Task<int> InvalidateSelectionsAsync(Guid orgId, Guid userId, CancellationToken ct) =>
        db.AuthSessions.Where(x => x.UserId == userId && x.SelectedOrganizationId == orgId)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.SelectedOrganizationId, (Guid?)null), ct);

    private void RecordChange(Guid actorId, Guid sessionId, string type, string correlationId) =>
        db.AuthEvents.Add(new AuthEvent { Id = Guid.NewGuid(), UserId = actorId, SessionId = sessionId,
            EventType = type, ResultCode = "ok",
            CorrelationId = correlationId[..Math.Min(correlationId.Length, 64)],
            OccurredAtUtc = clock.GetUtcNow() });

    public async Task<OrganizationProfile> BootstrapAsync(string slug, string displayName,
        string adminEmail, CancellationToken ct)
    {
        var normalized = OrganizationPolicy.NormalizeSlug(slug);
        var name = OrganizationPolicy.ValidateDisplayName(displayName);
        var email = IdentityStore.NormalizeEmail(adminEmail);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var admin = await db.AuthUsers.AsNoTracking()
            .SingleOrDefaultAsync(x => x.NormalizedEmail == email && x.Status == "Active", ct);
        if (admin is null) throw new ArgumentException("An existing active account is required");
        if (await db.Organizations.AnyAsync(x => x.Slug == normalized, ct))
            throw new InvalidOperationException("Organization slug is already in use");
        var now = clock.GetUtcNow();
        var org = new Organization { Id = Guid.NewGuid(), Slug = normalized, DisplayName = name,
            Status = "Active", Version = 1, CreatedAtUtc = now, UpdatedAtUtc = now };
        db.Organizations.Add(org);
        db.OrganizationMemberships.Add(new OrganizationMembership { Id = Guid.NewGuid(),
            OrganizationId = org.Id, UserId = admin.Id, Role = "Administrator", Status = "Active",
            Version = 1, CreatedAtUtc = now, UpdatedAtUtc = now });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new OrganizationProfile(org.Id, org.Slug, org.DisplayName, org.Status, org.Version);
    }
}
