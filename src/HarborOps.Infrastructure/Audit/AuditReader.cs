using System.Globalization;
using System.Text;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HarborOps.Infrastructure.Audit;

public sealed record AuditPage(IReadOnlyList<AuditEntry> Entries, string? NextCursor);

public sealed class AuditReader(HarborOpsDbContext db, TimeProvider clock)
{
    private IQueryable<AuditEntry> Visible(Guid organizationId, Guid actorId, Guid sessionId,
        IQueryable<AuditEntry>? source = null)
    {
        var now = clock.GetUtcNow();
        return (source ?? db.AuditEntries).AsNoTracking()
            .Where(e => e.OrganizationId == organizationId)
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

    private static string EncodeCursor(AuditEntry entry, Guid organizationId)
    {
        var value = string.Join("|", organizationId.ToString("D"),
            entry.OccurredAtUtc.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture),
            entry.Id.ToString("D"));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static (DateTimeOffset Timestamp, Guid Id) DecodeCursor(
        string cursor, Guid organizationId)
    {
        if (cursor.Length is < 1 or > 160 ||
            cursor.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')))
            throw new ArgumentException("Invalid audit cursor", nameof(cursor));
        try
        {
            var encoded = cursor.Replace('-', '+').Replace('_', '/');
            var bytes = Convert.FromBase64String(encoded.PadRight((encoded.Length + 3) / 4 * 4, '='));
            var fields = Encoding.UTF8.GetString(bytes).Split('|');
            if (fields.Length != 3 || !Guid.TryParseExact(fields[0], "D", out var org) ||
                org != organizationId || !Guid.TryParseExact(fields[2], "D", out var id) ||
                id == Guid.Empty || !long.TryParse(fields[1], NumberStyles.None,
                    CultureInfo.InvariantCulture, out var ticks))
                throw new ArgumentException("Invalid audit cursor", nameof(cursor));
            return (new DateTimeOffset(ticks, TimeSpan.Zero), id);
        }
        catch (Exception error) when (error is FormatException or ArgumentException)
        {
            throw new ArgumentException("Invalid audit cursor", nameof(cursor));
        }
    }

    public async Task<AuditPage> ListAsync(Guid organizationId, Guid actorId, Guid sessionId,
        int page, int limit, CancellationToken ct, string? cursor = null)
    {
        // Offset pagination is unsafe for a collection that appends an audit
        // event on each read. Subsequent pages must use the returned cursor.
        if (page != 1 || limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(page));
        IQueryable<AuditEntry> source = db.AuditEntries;
        if (cursor is not null)
        {
            var (timestamp, id) = DecodeCursor(cursor, organizationId);
            source = db.AuditEntries.FromSqlInterpolated($"""
                SELECT * FROM audit_entries
                WHERE occurred_at_utc < {timestamp}
                   OR (occurred_at_utc = {timestamp} AND id < {id})
                """);
        }
        var rows = await Visible(organizationId, actorId, sessionId, source)
            .OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id)
            .Take(limit + 1).ToListAsync(ct);
        var next = rows.Count > limit ? EncodeCursor(rows[limit - 1], organizationId) : null;
        if (rows.Count > limit) rows.RemoveAt(limit);
        return new AuditPage(rows, next);
    }

    public Task<AuditEntry?> GetAsync(Guid organizationId, Guid actorId,
        Guid sessionId, Guid auditId, CancellationToken ct) =>
        Visible(organizationId, actorId, sessionId)
            .SingleOrDefaultAsync(e => e.Id == auditId, ct);
}
