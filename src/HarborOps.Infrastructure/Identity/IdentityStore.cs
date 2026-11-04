using HarborOps.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.Organizations;

namespace HarborOps.Infrastructure.Identity;

public sealed class LastActiveAdministratorException : Exception { }

public sealed record LoginOutcome(Guid UserId, Guid SessionId, long SecurityVersion,
    string Email, string DisplayName, DateTimeOffset AbsoluteExpiry);
public sealed record CurrentSession(Guid UserId, Guid SessionId, string Email, string DisplayName,
    DateTimeOffset AbsoluteExpiry);

public sealed class IdentityStore(HarborOpsDbContext db, PasswordHasher passwords, TimeProvider clock)
{
    private static readonly Lazy<Task<string>> DecoyHash = new(() =>
        new PasswordHasher().HashAsync("missing-account-decoy-password"));
    private static readonly TimeSpan Idle = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan Absolute = TimeSpan.FromHours(12);

    public static string NormalizeEmail(string email)
    {
        var trimmed = email.Trim();
        if (trimmed.Length is < 3 or > 320 || trimmed.Any(char.IsWhiteSpace) ||
            trimmed.Count(ch => ch == '@') != 1 || trimmed.StartsWith('@') || trimmed.EndsWith('@'))
            throw new ArgumentException("Invalid email address");
        return trimmed.ToUpperInvariant();
    }

    public async Task ProvisionAsync(string email, string displayName, string password, CancellationToken ct)
    {
        var normalized = NormalizeEmail(email);
        if (displayName.Trim().Length is < 1 or > 160) throw new ArgumentException("Invalid display name");
        var hash = await passwords.HashAsync(password, ct);
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await AuditActorContext.SetOperatorAsync(db, "local-cli", ct);
        var user = new AuthUser { Id = Guid.NewGuid(), Email = email.Trim(), NormalizedEmail = normalized,
            DisplayName = displayName.Trim(), CreatedAtUtc = now };
        db.AuthUsers.Add(user);
        db.UserCredentials.Add(new UserCredential { UserId = user.Id, PasswordHash = hash,
            CreatedAtUtc = now, PasswordChangedAtUtc = now });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<LoginOutcome?> LoginAsync(string email, string password, string correlationId, CancellationToken ct)
    {
        string? normalized = null;
        try { normalized = NormalizeEmail(email); } catch (ArgumentException) { }
        var user = normalized is null ? null :
            await db.AuthUsers.AsNoTracking().SingleOrDefaultAsync(x => x.NormalizedEmail == normalized, ct);
        var credential = user is null ? null :
            await db.UserCredentials.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user.Id, ct);
        // Unknown and disabled accounts still perform one expensive derivation.
        var hash = credential?.PasswordHash ?? await DecoyHash.Value.WaitAsync(ct);
        var verified = await passwords.VerifyAsync(password, hash, ct);
        if (!verified || user is null || user.Status != "Active" || credential?.HashAlgorithm != "argon2id")
        {
            AddEvent("login_failed", "invalid_credentials", correlationId, user?.Id, null);
            await db.SaveChangesAsync(ct);
            return null;
        }
        var now = clock.GetUtcNow();
        var session = new AuthSession { Id = Guid.NewGuid(), UserId = user.Id, IssuedAtUtc = now,
            LastSeenAtUtc = now, IdleExpiresAtUtc = now.Add(Idle),
            AbsoluteExpiresAtUtc = now.Add(Absolute), SecurityVersion = user.SecurityVersion };
        db.AuthSessions.Add(session);
        AddEvent("login_succeeded", "ok", correlationId, user.Id, session.Id);
        await db.SaveChangesAsync(ct);
        return new LoginOutcome(user.Id, session.Id, user.SecurityVersion,
            user.Email, user.DisplayName, session.AbsoluteExpiresAtUtc);
    }

    public async Task<CurrentSession?> ValidateAsync(Guid userId, Guid sessionId, long securityVersion,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var state = await (from session in db.AuthSessions.AsNoTracking()
                           join user in db.AuthUsers.AsNoTracking() on session.UserId equals user.Id
                           where session.Id == sessionId && session.UserId == userId
                           select new { session, user }).SingleOrDefaultAsync(ct);
        if (state is null || state.user.Status != "Active" ||
            state.user.SecurityVersion != securityVersion ||
            state.session.SecurityVersion != securityVersion || state.session.RevokedAtUtc != null ||
            state.session.IdleExpiresAtUtc <= now || state.session.AbsoluteExpiresAtUtc <= now)
            return null;

        // Conditional update cannot revive an already revoked or expired session.
        var nextIdle = now.Add(Idle) < state.session.AbsoluteExpiresAtUtc
            ? now.Add(Idle) : state.session.AbsoluteExpiresAtUtc;
        var changed = await db.AuthSessions
            .Where(x => x.Id == sessionId && x.RevokedAtUtc == null &&
                x.IdleExpiresAtUtc > now && x.AbsoluteExpiresAtUtc > now &&
                x.SecurityVersion == securityVersion)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.LastSeenAtUtc, now)
                .SetProperty(x => x.IdleExpiresAtUtc, nextIdle), ct);
        if (changed != 1) return null;
        return new CurrentSession(userId, sessionId, state.user.Email,
            state.user.DisplayName, state.session.AbsoluteExpiresAtUtc);
    }

    public async Task RevokeCurrentAsync(Guid sessionId, Guid userId, string correlationId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await AuditActorContext.SetHumanAsync(db, userId, sessionId, correlationId, ct);
        await db.AuthSessions.Where(x => x.Id == sessionId && x.UserId == userId && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAtUtc, now)
                .SetProperty(x => x.RevocationReason, "logout"), ct);
        AddEvent("logout", "ok", correlationId, userId, sessionId);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task RevokeAllAsync(Guid userId, string reason, string correlationId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await AuditActorContext.SetHumanAsync(db, userId, null, correlationId, ct);
        await db.AuthSessions.Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAtUtc, now)
                .SetProperty(x => x.RevocationReason, reason), ct);
        AddEvent("logout_all", "ok", correlationId, userId, null);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<bool> DisableSelfAsync(Guid userId, string password, string correlationId, CancellationToken ct)
    {
        var credential = await db.UserCredentials.AsNoTracking().SingleAsync(x => x.UserId == userId, ct);
        if (!await passwords.VerifyAsync(password, credential.PasswordHash, ct)) return false;
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await AuditActorContext.SetHumanAsync(db, userId, null, correlationId, ct);
        // Administrator membership mutations lock the same organization rows first.
        // Holding these locks through account disablement prevents losing the last
        // active administrator through a concurrent role or membership change.
        var administeredOrganizations = await db.Organizations
            .FromSqlInterpolated($"""
                SELECT * FROM organizations
                WHERE id IN (
                    SELECT organization_id FROM organization_memberships
                    WHERE user_id = {userId} AND role = 'Administrator' AND status = 'Active'
                )
                ORDER BY id FOR UPDATE
                """)
            .AsNoTracking().ToListAsync(ct);
        foreach (var org in administeredOrganizations)
        {
            var activeAdministrators = await (
                from member in db.OrganizationMemberships.AsNoTracking()
                join account in db.AuthUsers.AsNoTracking() on member.UserId equals account.Id
                where member.OrganizationId == org.Id &&
                    member.Role == "Administrator" && member.Status == "Active" &&
                    account.Status == "Active"
                select member.Id).CountAsync(ct);
            if (activeAdministrators <= 1)
                throw new LastActiveAdministratorException();
        }
        var updated = await db.AuthUsers.Where(x => x.Id == userId && x.Status == "Active")
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Disabled")
                .SetProperty(x => x.DisabledAtUtc, now)
                .SetProperty(x => x.SecurityVersion, x => x.SecurityVersion + 1), ct);
        if (updated != 1) { await transaction.RollbackAsync(ct); return false; }
        await db.AuthSessions.Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAtUtc, now)
                .SetProperty(x => x.RevocationReason, "account_disabled"), ct);
        AddEvent("account_disabled", "ok", correlationId, userId, null);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task ResetPasswordAsync(string email, string password, CancellationToken ct)
    {
        var normalized = NormalizeEmail(email);
        var hash = await passwords.HashAsync(password, ct);
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await AuditActorContext.SetOperatorAsync(db, "local-cli", ct);
        var user = await db.AuthUsers.SingleAsync(x => x.NormalizedEmail == normalized, ct);
        await db.UserCredentials.Where(x => x.UserId == user.Id).ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.PasswordHash, hash)
                .SetProperty(x => x.PasswordChangedAtUtc, now), ct);
        await db.AuthUsers.Where(x => x.Id == user.Id).ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.SecurityVersion, x => x.SecurityVersion + 1), ct);
        await db.AuthSessions.Where(x => x.UserId == user.Id && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAtUtc, now)
                .SetProperty(x => x.RevocationReason, "password_reset"), ct);
        AddEvent("password_reset", "ok", "local-cli", user.Id, null);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private void AddEvent(string eventType, string result, string correlation, Guid? userId, Guid? sessionId) =>
        db.AuthEvents.Add(new AuthEvent { Id = Guid.NewGuid(), EventType = eventType,
            ResultCode = result, CorrelationId = correlation[..Math.Min(correlation.Length, 64)],
            UserId = userId, SessionId = sessionId, OccurredAtUtc = clock.GetUtcNow() });
}
