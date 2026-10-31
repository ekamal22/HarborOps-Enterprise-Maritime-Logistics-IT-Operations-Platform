using System.Text.Json;
using HarborOps.Domain.Maritime;
using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HarborOps.Infrastructure.Maritime;

public sealed record MaritimeResult<T>(T? Value, OrganizationAccessError Error);
public sealed record MaritimePage<T>(IReadOnlyList<T> Items, Guid? NextAfter);

public sealed class MaritimeInput
{
    private readonly Dictionary<string, string?> fields = new(StringComparer.Ordinal);
    public long? ExpectedVersion { get; private set; }
    public bool Has(string key) => fields.ContainsKey(key);
    public string? Get(string key) => fields.GetValueOrDefault(key);
    public int Changed => fields.Count;
    public static MaritimeInput Parse(JsonElement root, bool voyage, string action)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new ArgumentException("Object required");
        var fields = voyage
            ? new[] { "referenceCode", "vesselId", "originPortId", "destinationPortId",
                "plannedDepartureAtUtc", "plannedArrivalAtUtc", "actualDepartureAtUtc",
                "actualArrivalAtUtc", "voyageStatus", "recordStatus" }
            : new[] { "referenceCode", "identifierKind", "containerIdentifier",
                "equipmentType", "displayName", "recordStatus" };
        var allowed = action == "delete" ? Array.Empty<string>() : fields;
        var result = new MaritimeInput();
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name == "expectedVersion")
            {
                if (action == "create" || result.ExpectedVersion.HasValue ||
                    property.Value.ValueKind != JsonValueKind.Number ||
                    !property.Value.TryGetInt64(out var version) || version < 1)
                    throw new ArgumentException("Invalid version");
                result.ExpectedVersion = version;
                continue;
            }
            if (!allowed.Contains(property.Name, StringComparer.Ordinal) ||
                result.fields.ContainsKey(property.Name) ||
                action != "create" && property.Name is "referenceCode" or "vesselId" or
                    "originPortId" or "destinationPortId" or "identifierKind" or
                    "containerIdentifier" or "equipmentType" ||
                action == "create" && property.Name is "recordStatus" or "voyageStatus" or
                    "actualDepartureAtUtc" or "actualArrivalAtUtc" ||
                action == "delete" ||
                property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                throw new ArgumentException("Unexpected field");
            var value = property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetString();
            if (value is { Length: > 2000 }) throw new ArgumentException("Field too long");
            result.fields.Add(property.Name, value);
        }
        if (action == "create" && (voyage
            ? new[] { "referenceCode", "vesselId", "originPortId", "destinationPortId" }
            : new[] { "referenceCode", "identifierKind", "containerIdentifier", "equipmentType" })
            .Any(field => !result.fields.ContainsKey(field)))
            throw new ArgumentException("Required fields missing");
        if (action != "create" && !result.ExpectedVersion.HasValue ||
            action == "update" && result.Changed == 0 ||
            action == "delete" && result.Changed != 0)
            throw new ArgumentException("Invalid operation");
        return result;
    }
}

public sealed class MaritimeStore(HarborOpsDbContext db, OrganizationStore organizations, TimeProvider clock)
{
    private static Guid Id(string? value)
    {
        if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
            throw new ArgumentException("Invalid identifier");
        return id;
    }
    private static bool IsConflict(DbUpdateException error) =>
        error.InnerException is PostgresException { SqlState: "23505" or "23514" or "23503" };

    // Lock order matches M3: organization, session, account, then resource rows.
    private async Task<OrganizationAccessError> ScopeAsync(CurrentSession session, Guid orgId,
        OrganizationPermission permission, bool writing, CancellationToken ct)
    {
        // Static queries are deliberate: SQL identifiers or lock clauses never come from user input.
        if (writing)
            await db.Organizations.FromSqlInterpolated(
                $"SELECT * FROM organizations WHERE id = {orgId} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct);
        else
            await db.Organizations.FromSqlInterpolated(
                $"SELECT * FROM organizations WHERE id = {orgId} FOR SHARE").AsNoTracking().SingleOrDefaultAsync(ct);
        var active = writing
            ? await db.AuthSessions.FromSqlInterpolated(
                $"SELECT * FROM auth_sessions WHERE id = {session.SessionId} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct)
            : await db.AuthSessions.FromSqlInterpolated(
                $"SELECT * FROM auth_sessions WHERE id = {session.SessionId} FOR SHARE").AsNoTracking().SingleOrDefaultAsync(ct);
        if (active is null || active.UserId != session.UserId ||
            active.RevokedAtUtc != null || active.IdleExpiresAtUtc <= clock.GetUtcNow() ||
            active.AbsoluteExpiresAtUtc <= clock.GetUtcNow())
            return OrganizationAccessError.Revoked;
        if (active.SelectedOrganizationId is null) return OrganizationAccessError.SelectionRequired;
        if (active.SelectedOrganizationId != orgId) return OrganizationAccessError.NotFound;
        var user = await db.AuthUsers.FromSqlInterpolated(
            $"SELECT * FROM users WHERE id = {session.UserId} FOR SHARE").AsNoTracking().SingleOrDefaultAsync(ct);
        if (user is null || user.Status != "Active" || user.SecurityVersion != active.SecurityVersion)
            return OrganizationAccessError.Revoked;
        var scope = await organizations.ResolveAsync(session.UserId, session.SessionId, orgId, permission, ct);
        return scope.Value is null ? scope.Error : OrganizationAccessError.None;
    }
    private IQueryable<T> Own<T>(Guid org) where T : MaritimeRecord =>
        db.Set<T>().Where(x => x.OrganizationId == org);

    public async Task<MaritimeResult<MaritimePage<VoyageRecord>>> ListVoyagesAsync(
        CurrentSession session, Guid org, string q, string status, Guid? after, int limit,
        Guid? vessel, Guid? origin, Guid? destination, string? progress, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var access = await ScopeAsync(session, org, OrganizationPermission.ViewVoyageData, false, ct);
        if (access != OrganizationAccessError.None) return new(null, access);
        IQueryable<VoyageRecord> query = after.HasValue
            ? db.Voyages.FromSqlInterpolated(
                $"SELECT * FROM voyages WHERE organization_id = {org} AND id > {after.Value}")
            : Own<VoyageRecord>(org);
        query = query.AsNoTracking();
        if (status != "All") query = query.Where(x => x.RecordStatus == status);
        if (vessel.HasValue) query = query.Where(x => x.VesselId == vessel);
        if (origin.HasValue) query = query.Where(x => x.OriginPortId == origin);
        if (destination.HasValue) query = query.Where(x => x.DestinationPortId == destination);
        if (progress is not null) query = query.Where(x => x.VoyageStatus == progress);
        if (q.Length != 0) query = query.Where(x => x.ReferenceCode.ToLower().Contains(q.ToLower()));
        var records = await query.OrderBy(x => x.Id).Take(limit + 1).ToListAsync(ct);
        var more = records.Count > limit;
        if (more) records.RemoveAt(limit);
        await tx.CommitAsync(ct);
        return new(new MaritimePage<VoyageRecord>(records, more ? records[^1].Id : null), OrganizationAccessError.None);
    }
    public async Task<MaritimeResult<MaritimePage<ContainerRecord>>> ListContainersAsync(
        CurrentSession session, Guid org, string q, string status, Guid? after, int limit,
        string? kind, string? equipment, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var access = await ScopeAsync(session, org, OrganizationPermission.ViewContainerData, false, ct);
        if (access != OrganizationAccessError.None) return new(null, access);
        IQueryable<ContainerRecord> query = after.HasValue
            ? db.Containers.FromSqlInterpolated(
                $"SELECT * FROM containers WHERE organization_id = {org} AND id > {after.Value}")
            : Own<ContainerRecord>(org);
        query = query.AsNoTracking();
        if (status != "All") query = query.Where(x => x.RecordStatus == status);
        if (kind is not null) query = query.Where(x => x.IdentifierKind == kind);
        if (equipment is not null) query = query.Where(x => x.EquipmentType == equipment);
        if (q.Length != 0) query = query.Where(x =>
            x.ReferenceCode.ToLower().Contains(q.ToLower()) ||
            x.ContainerIdentifier.ToLower().Contains(q.ToLower()) ||
            x.DisplayName != null && x.DisplayName.ToLower().Contains(q.ToLower()));
        var records = await query.OrderBy(x => x.Id).Take(limit + 1).ToListAsync(ct);
        var more = records.Count > limit;
        if (more) records.RemoveAt(limit);
        await tx.CommitAsync(ct);
        return new(new MaritimePage<ContainerRecord>(records, more ? records[^1].Id : null), OrganizationAccessError.None);
    }
    public async Task<MaritimeResult<VoyageRecord>> GetVoyageAsync(
        CurrentSession session, Guid org, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var access = await ScopeAsync(session, org, OrganizationPermission.ViewVoyageData, false, ct);
        if (access != OrganizationAccessError.None) return new(null, access);
        var value = await Own<VoyageRecord>(org).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        await tx.CommitAsync(ct);
        return value is null ? new(null, OrganizationAccessError.NotFound) :
            new(value, OrganizationAccessError.None);
    }
    public async Task<MaritimeResult<ContainerRecord>> GetContainerAsync(
        CurrentSession session, Guid org, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var access = await ScopeAsync(session, org, OrganizationPermission.ViewContainerData, false, ct);
        if (access != OrganizationAccessError.None) return new(null, access);
        var value = await Own<ContainerRecord>(org).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        await tx.CommitAsync(ct);
        return value is null ? new(null, OrganizationAccessError.NotFound) :
            new(value, OrganizationAccessError.None);
    }

    public async Task<MaritimeResult<VoyageRecord>> WriteVoyageAsync(CurrentSession session,
        Guid org, Guid? id, string action, MaritimeInput input, CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var access = await ScopeAsync(session, org, OrganizationPermission.ManageVoyageData, true, ct);
            if (access != OrganizationAccessError.None) return new(null, access);
            VoyageRecord record;
            if (action == "create")
            {
                var vessel = Id(input.Get("vesselId"));
                var from = Id(input.Get("originPortId"));
                var to = Id(input.Get("destinationPortId"));
                if (from == to) throw new ArgumentException("Route endpoints match");
                // Parent row locks plus the organization write lock serialize against M3 archival.
                var validVessel = await db.Vessels.AsNoTracking().AnyAsync(x =>
                    x.OrganizationId == org && x.Id == vessel && x.Status == "Active", ct);
                var validPorts = await db.Ports.AsNoTracking().CountAsync(x =>
                    x.OrganizationId == org && x.Status == "Active" && (x.Id == from || x.Id == to), ct);
                if (!validVessel || validPorts != 2) return new(null, OrganizationAccessError.NotFound);
                record = new VoyageRecord {
                    Id = Guid.NewGuid(), OrganizationId = org,
                    ReferenceCode = MaritimePolicy.Code(input.Get("referenceCode")),
                    VesselId = vessel, OriginPortId = from, DestinationPortId = to,
                    PlannedDepartureAtUtc = MaritimePolicy.Utc(input.Get("plannedDepartureAtUtc")),
                    PlannedArrivalAtUtc = MaritimePolicy.Utc(input.Get("plannedArrivalAtUtc")),
                    CreatedAtUtc = clock.GetUtcNow(), UpdatedAtUtc = clock.GetUtcNow()
                };
                MaritimePolicy.ValidateTimes(record.PlannedDepartureAtUtc, record.PlannedArrivalAtUtc,
                    null, null, "Planned");
                db.Voyages.Add(record);
            }
            else
            {
                record = await Own<VoyageRecord>(org).SingleOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new MissingRecordException();
                if (record.Version != input.ExpectedVersion) return new(null, OrganizationAccessError.Conflict);
                if (record.RecordStatus == "Archived")
                {
                    if (action != "update" || input.Changed != 1 ||
                        input.Get("recordStatus") != "Active")
                        return new(null, OrganizationAccessError.Conflict);
                    record.RecordStatus = "Active";
                }
                else if (action == "delete") record.RecordStatus = "Archived";
                else
                {
                    if (input.Has("recordStatus")) return new(null, OrganizationAccessError.Conflict);
                    var next = input.Has("voyageStatus") ?
                        MaritimePolicy.Progress(input.Get("voyageStatus")) : record.VoyageStatus;
                    if (!MaritimePolicy.Transition(record.VoyageStatus, next))
                        return new(null, OrganizationAccessError.Conflict);
                    if (input.Has("plannedDepartureAtUtc") || input.Has("plannedArrivalAtUtc"))
                    {
                        if (record.VoyageStatus != "Planned")
                            return new(null, OrganizationAccessError.Conflict);
                        if (input.Has("plannedDepartureAtUtc"))
                            record.PlannedDepartureAtUtc = MaritimePolicy.Utc(input.Get("plannedDepartureAtUtc"));
                        if (input.Has("plannedArrivalAtUtc"))
                            record.PlannedArrivalAtUtc = MaritimePolicy.Utc(input.Get("plannedArrivalAtUtc"));
                    }
                    if (input.Has("actualDepartureAtUtc"))
                    {
                        if (record.ActualDepartureAtUtc != null) return new(null, OrganizationAccessError.Conflict);
                        record.ActualDepartureAtUtc = MaritimePolicy.Utc(input.Get("actualDepartureAtUtc"));
                    }
                    if (input.Has("actualArrivalAtUtc"))
                    {
                        if (record.ActualArrivalAtUtc != null) return new(null, OrganizationAccessError.Conflict);
                        record.ActualArrivalAtUtc = MaritimePolicy.Utc(input.Get("actualArrivalAtUtc"));
                    }
                    MaritimePolicy.ValidateTimes(record.PlannedDepartureAtUtc, record.PlannedArrivalAtUtc,
                        record.ActualDepartureAtUtc, record.ActualArrivalAtUtc, next);
                    record.VoyageStatus = next;
                }
                record.Version++;
                record.UpdatedAtUtc = clock.GetUtcNow();
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(record, OrganizationAccessError.None);
        }
        catch (MissingRecordException) { return new(null, OrganizationAccessError.NotFound); }
        catch (ArgumentException) { return new(null, OrganizationAccessError.Invalid); }
        catch (DbUpdateException e) when (IsConflict(e)) { return new(null, OrganizationAccessError.Conflict); }
    }

    public async Task<MaritimeResult<ContainerRecord>> WriteContainerAsync(CurrentSession session,
        Guid org, Guid? id, string action, MaritimeInput input, CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var access = await ScopeAsync(session, org, OrganizationPermission.ManageContainerData, true, ct);
            if (access != OrganizationAccessError.None) return new(null, access);
            ContainerRecord record;
            if (action == "create")
            {
                var kind = input.Get("identifierKind");
                if (kind is not ("Synthetic" or "ISO6346"))
                    throw new ArgumentException("Invalid kind");
                record = new ContainerRecord {
                    Id = Guid.NewGuid(), OrganizationId = org,
                    ReferenceCode = MaritimePolicy.Code(input.Get("referenceCode")),
                    IdentifierKind = kind, ContainerIdentifier = MaritimePolicy.Identifier(kind, input.Get("containerIdentifier")),
                    EquipmentType = MaritimePolicy.EquipmentType(input.Get("equipmentType")),
                    DisplayName = MaritimePolicy.Label(input.Get("displayName")),
                    SourceKind = kind == "Synthetic" ? "Synthetic" : "UserProvided",
                    CreatedAtUtc = clock.GetUtcNow(), UpdatedAtUtc = clock.GetUtcNow()
                };
                db.Containers.Add(record);
            }
            else
            {
                record = await Own<ContainerRecord>(org).SingleOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new MissingRecordException();
                if (record.Version != input.ExpectedVersion) return new(null, OrganizationAccessError.Conflict);
                if (record.RecordStatus == "Archived")
                {
                    if (action != "update" || input.Changed != 1 ||
                        input.Get("recordStatus") != "Active")
                        return new(null, OrganizationAccessError.Conflict);
                    record.RecordStatus = "Active";
                }
                else if (action == "delete") record.RecordStatus = "Archived";
                else
                {
                    if (input.Has("recordStatus")) return new(null, OrganizationAccessError.Conflict);
                    if (input.Has("displayName"))
                        record.DisplayName = MaritimePolicy.Label(input.Get("displayName"));
                }
                record.Version++;
                record.UpdatedAtUtc = clock.GetUtcNow();
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(record, OrganizationAccessError.None);
        }
        catch (MissingRecordException) { return new(null, OrganizationAccessError.NotFound); }
        catch (ArgumentException) { return new(null, OrganizationAccessError.Invalid); }
        catch (DbUpdateException e) when (IsConflict(e)) { return new(null, OrganizationAccessError.Conflict); }
    }
    private sealed class MissingRecordException : Exception { }
}
