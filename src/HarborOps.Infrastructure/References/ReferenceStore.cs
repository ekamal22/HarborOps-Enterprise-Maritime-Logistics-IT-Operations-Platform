using System.Text.Json;
using HarborOps.Domain.Organizations;
using HarborOps.Domain.References;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HarborOps.Infrastructure.References;

public sealed record ReferencePage<T>(IReadOnlyList<T> Items, Guid? NextAfter);
public sealed record ReferenceResult<T>(T? Value, OrganizationAccessError Error);

public sealed class ReferenceInput
{
    private readonly Dictionary<string, string?> strings = new(StringComparer.Ordinal);
    public long? ExpectedVersion { get; private set; }
    public bool Has(string name) => strings.ContainsKey(name);
    public string? Get(string name) => strings.GetValueOrDefault(name);

    public static ReferenceInput Parse(JsonElement json, bool port, string action)
    {
        if (json.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Invalid request body");
        var allow = action == "delete" ? new HashSet<string>(StringComparer.Ordinal) { "expectedVersion" }
            : port
                ? new HashSet<string>(StringComparer.Ordinal) { "referenceCode", "name", "countryCode", "unLocode", "status", "expectedVersion" }
                : new HashSet<string>(StringComparer.Ordinal) { "referenceCode", "name", "imoNumber", "mmsi", "flagCountryCode", "status", "expectedVersion" };
        var result = new ReferenceInput();
        foreach (var property in json.EnumerateObject())
        {
            if (!allow.Contains(property.Name) ||
                (action == "create" && property.Name is "status" or "expectedVersion") ||
                (action == "update" && property.Name == "referenceCode") ||
                (action == "delete" && property.Name != "expectedVersion") ||
                (property.Name != "expectedVersion" && result.strings.ContainsKey(property.Name)) ||
                (property.Name == "expectedVersion" && result.ExpectedVersion.HasValue))
                throw new ArgumentException("Unexpected request property");
            if (property.Name == "expectedVersion")
            {
                if (!property.Value.TryGetInt64(out var version) || version < 1)
                    throw new ArgumentException("Invalid version");
                result.ExpectedVersion = version;
            }
            else
            {
                if (property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                    throw new ArgumentException("Invalid field");
                var value = property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetString();
                if (value is { Length: > 10000 })
                    throw new ArgumentException("Invalid field length");
                result.strings.Add(property.Name, value);
            }
        }
        if (action == "create" &&
            (!result.Has("referenceCode") || !result.Has("name") ||
             (port && !result.Has("countryCode"))))
            throw new ArgumentException("Required fields missing");
        if (action is "update" or "delete" && !result.ExpectedVersion.HasValue)
            throw new ArgumentException("Expected version required");
        if (action == "update" && !result.strings.Keys.Any())
            throw new ArgumentException("No change supplied");
        return result;
    }
}

public sealed class ReferenceStore(HarborOpsDbContext db, OrganizationStore organizations, TimeProvider clock)
{
    private static bool CanRead(string role) => role is "Analyst" or "Manager";

    private IQueryable<TEntity> Visible<TEntity>(CurrentSession session, Guid orgId)
        where TEntity : ReferenceRecord =>
        db.Set<TEntity>().AsNoTracking().Where(x =>
            x.OrganizationId == orgId &&
            db.AuthSessions.Any(s => s.Id == session.SessionId && s.UserId == session.UserId &&
                s.SelectedOrganizationId == orgId && s.RevokedAtUtc == null &&
                s.IdleExpiresAtUtc > clock.GetUtcNow() &&
                s.AbsoluteExpiresAtUtc > clock.GetUtcNow()) &&
            db.AuthUsers.Any(u => u.Id == session.UserId && u.Status == "Active") &&
            db.Organizations.Any(o => o.Id == orgId && o.Status == "Active") &&
            db.OrganizationMemberships.Any(m => m.UserId == session.UserId &&
                m.OrganizationId == orgId && m.Status == "Active" &&
                (m.Role == "Analyst" || m.Role == "Manager")));

    public async Task<ReferenceResult<ReferencePage<PortReference>>> ListPortsAsync(
        CurrentSession session, Guid orgId, string? q, string status, Guid? after, int limit,
        CancellationToken ct)
    {
        var scope = await organizations.ResolveAsync(session.UserId, session.SessionId, orgId,
            OrganizationPermission.ViewReferenceData, ct);
        if (scope.Value is null) return new(null, scope.Error);
        var query = Visible<PortReference>(session, orgId);
        if (status != "All") query = query.Where(x => x.Status == status);
        if (after.HasValue) query = query.Where(x => x.Id.CompareTo(after.Value) > 0);
        if (!string.IsNullOrEmpty(q))
        {
            var term = q.ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(term) ||
                x.ReferenceCode.ToLower().Contains(term) ||
                (x.UnLocode != null && x.UnLocode.ToLower().Contains(term)));
        }
        var items = await query.OrderBy(x => x.Id).Take(limit + 1).ToListAsync(ct);
        var more = items.Count > limit;
        if (more) items.RemoveAt(limit);
        return new(new ReferencePage<PortReference>(items, more ? items[^1].Id : null),
            OrganizationAccessError.None);
    }

    public async Task<ReferenceResult<ReferencePage<VesselReference>>> ListVesselsAsync(
        CurrentSession session, Guid orgId, string? q, string status, Guid? after, int limit,
        CancellationToken ct)
    {
        var scope = await organizations.ResolveAsync(session.UserId, session.SessionId, orgId,
            OrganizationPermission.ViewReferenceData, ct);
        if (scope.Value is null) return new(null, scope.Error);
        var query = Visible<VesselReference>(session, orgId);
        if (status != "All") query = query.Where(x => x.Status == status);
        if (after.HasValue) query = query.Where(x => x.Id.CompareTo(after.Value) > 0);
        if (!string.IsNullOrEmpty(q))
        {
            var term = q.ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(term) ||
                x.ReferenceCode.ToLower().Contains(term) ||
                (x.ImoNumber != null && x.ImoNumber.Contains(term)) ||
                (x.Mmsi != null && x.Mmsi.Contains(term)));
        }
        var items = await query.OrderBy(x => x.Id).Take(limit + 1).ToListAsync(ct);
        var more = items.Count > limit;
        if (more) items.RemoveAt(limit);
        return new(new ReferencePage<VesselReference>(items, more ? items[^1].Id : null),
            OrganizationAccessError.None);
    }

    public async Task<ReferenceResult<PortReference>> GetPortAsync(
        CurrentSession session, Guid orgId, Guid id, CancellationToken ct)
    {
        var scope = await organizations.ResolveAsync(session.UserId, session.SessionId, orgId,
            OrganizationPermission.ViewReferenceData, ct);
        if (scope.Value is null) return new(null, scope.Error);
        var item = await Visible<PortReference>(session, orgId).SingleOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? new(null, OrganizationAccessError.NotFound) :
            new(item, OrganizationAccessError.None);
    }

    public async Task<ReferenceResult<VesselReference>> GetVesselAsync(
        CurrentSession session, Guid orgId, Guid id, CancellationToken ct)
    {
        var scope = await organizations.ResolveAsync(session.UserId, session.SessionId, orgId,
            OrganizationPermission.ViewReferenceData, ct);
        if (scope.Value is null) return new(null, scope.Error);
        var item = await Visible<VesselReference>(session, orgId).SingleOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? new(null, OrganizationAccessError.NotFound) :
            new(item, OrganizationAccessError.None);
    }

    // Organization locks serialize M3 writes with M2 role/membership changes.
    // Revalidate the session, role and selected organization after taking the lock.
    private async Task<OrganizationAccessError> LockManagerAsync(
        CurrentSession session, Guid orgId, CancellationToken ct)
    {
        var org = await db.Organizations
            .FromSqlInterpolated($"SELECT * FROM organizations WHERE id = {orgId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        var selected = await db.AuthSessions.AsNoTracking()
            .Where(s => s.Id == session.SessionId && s.UserId == session.UserId &&
                s.RevokedAtUtc == null && s.IdleExpiresAtUtc > clock.GetUtcNow() &&
                s.AbsoluteExpiresAtUtc > clock.GetUtcNow())
            .Select(s => s.SelectedOrganizationId).SingleOrDefaultAsync(ct);
        if (selected is null) return OrganizationAccessError.SelectionRequired;
        if (selected != orgId) return OrganizationAccessError.NotFound;
        if (org is null || org.Status != "Active") return OrganizationAccessError.Revoked;
        if (!await db.AuthUsers.AsNoTracking()
            .AnyAsync(u => u.Id == session.UserId && u.Status == "Active", ct))
            return OrganizationAccessError.Revoked;
        var role = await db.OrganizationMemberships.AsNoTracking()
            .Where(m => m.OrganizationId == orgId && m.UserId == session.UserId &&
                m.Status == "Active")
            .Select(m => m.Role).SingleOrDefaultAsync(ct);
        if (role is null) return OrganizationAccessError.Revoked;
        return role == "Manager" ? OrganizationAccessError.None : OrganizationAccessError.Forbidden;
    }

    private static bool Duplicate(DbUpdateException error) =>
        error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    public async Task<ReferenceResult<PortReference>> WritePortAsync(
        CurrentSession session, Guid orgId, Guid? id, string action, ReferenceInput input, CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var access = await LockManagerAsync(session, orgId, ct);
            if (access != OrganizationAccessError.None) return new(null, access);
            PortReference item;
            if (action == "create")
            {
                item = new PortReference
                {
                    Id = Guid.NewGuid(), OrganizationId = orgId,
                    ReferenceCode = ReferencePolicy.Code(input.Get("referenceCode")),
                    Name = ReferencePolicy.Name(input.Get("name")),
                    CountryCode = ReferencePolicy.Country(input.Get("countryCode")),
                    SourceKind = "UserProvided", Version = 1,
                    CreatedAtUtc = clock.GetUtcNow(), UpdatedAtUtc = clock.GetUtcNow()
                };
                item.UnLocode = ReferencePolicy.UnLocode(input.Get("unLocode"), item.CountryCode);
                db.Ports.Add(item);
            }
            else
            {
                item = await db.Ports.SingleOrDefaultAsync(x => x.OrganizationId == orgId && x.Id == id, ct)
                    ?? throw new ReferenceMissingException();
                if (item.Version != input.ExpectedVersion || (action == "delete" && item.Status == "Archived"))
                    return new(null, OrganizationAccessError.Conflict);
                if (action == "delete") item.Status = "Archived";
                else
                {
                    if (input.Has("name")) item.Name = ReferencePolicy.Name(input.Get("name"));
                    if (input.Has("countryCode")) item.CountryCode = ReferencePolicy.Country(input.Get("countryCode"));
                    if (input.Has("unLocode") || input.Has("countryCode"))
                        item.UnLocode = ReferencePolicy.UnLocode(
                            input.Has("unLocode") ? input.Get("unLocode") : item.UnLocode, item.CountryCode);
                    if (input.Has("status")) item.Status = ReferencePolicy.Status(input.Get("status"));
                }
                item.Version++;
                item.UpdatedAtUtc = clock.GetUtcNow();
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(item, OrganizationAccessError.None);
        }
        catch (ReferenceMissingException) { return new(null, OrganizationAccessError.NotFound); }
        catch (ArgumentException) { return new(null, OrganizationAccessError.Invalid); }
        catch (DbUpdateException ex) when (Duplicate(ex))
        {
            return new(null, OrganizationAccessError.Conflict);
        }
    }

    public async Task<ReferenceResult<VesselReference>> WriteVesselAsync(
        CurrentSession session, Guid orgId, Guid? id, string action, ReferenceInput input, CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var access = await LockManagerAsync(session, orgId, ct);
            if (access != OrganizationAccessError.None) return new(null, access);
            VesselReference item;
            if (action == "create")
            {
                item = new VesselReference
                {
                    Id = Guid.NewGuid(), OrganizationId = orgId,
                    ReferenceCode = ReferencePolicy.Code(input.Get("referenceCode")),
                    Name = ReferencePolicy.Name(input.Get("name")),
                    ImoNumber = ReferencePolicy.Imo(input.Get("imoNumber")),
                    Mmsi = ReferencePolicy.Mmsi(input.Get("mmsi")),
                    FlagCountryCode = ReferencePolicy.OptionalCountry(input.Get("flagCountryCode")),
                    SourceKind = "UserProvided", Version = 1,
                    CreatedAtUtc = clock.GetUtcNow(), UpdatedAtUtc = clock.GetUtcNow()
                };
                db.Vessels.Add(item);
            }
            else
            {
                item = await db.Vessels.SingleOrDefaultAsync(x => x.OrganizationId == orgId && x.Id == id, ct)
                    ?? throw new ReferenceMissingException();
                if (item.Version != input.ExpectedVersion || (action == "delete" && item.Status == "Archived"))
                    return new(null, OrganizationAccessError.Conflict);
                if (action == "delete") item.Status = "Archived";
                else
                {
                    if (input.Has("name")) item.Name = ReferencePolicy.Name(input.Get("name"));
                    if (input.Has("imoNumber")) item.ImoNumber = ReferencePolicy.Imo(input.Get("imoNumber"));
                    if (input.Has("mmsi")) item.Mmsi = ReferencePolicy.Mmsi(input.Get("mmsi"));
                    if (input.Has("flagCountryCode")) item.FlagCountryCode =
                        ReferencePolicy.OptionalCountry(input.Get("flagCountryCode"));
                    if (input.Has("status")) item.Status = ReferencePolicy.Status(input.Get("status"));
                }
                item.Version++;
                item.UpdatedAtUtc = clock.GetUtcNow();
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(item, OrganizationAccessError.None);
        }
        catch (ReferenceMissingException) { return new(null, OrganizationAccessError.NotFound); }
        catch (ArgumentException) { return new(null, OrganizationAccessError.Invalid); }
        catch (DbUpdateException ex) when (Duplicate(ex))
        {
            return new(null, OrganizationAccessError.Conflict);
        }
    }

    private sealed class ReferenceMissingException : Exception;
}
