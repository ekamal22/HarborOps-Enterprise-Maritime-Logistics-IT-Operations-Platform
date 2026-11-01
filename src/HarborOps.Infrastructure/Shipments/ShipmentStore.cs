using System.Text.Json;
using HarborOps.Domain.Organizations;
using HarborOps.Domain.Shipments;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HarborOps.Infrastructure.Shipments;

public sealed record ShipmentResult<T>(T? Value, OrganizationAccessError Error);
public sealed record ShipmentPage(IReadOnlyList<ShipmentRecord> Shipments, Guid? NextAfter);

public sealed class ShipmentInput
{
    private readonly Dictionary<string, string?> values = new(StringComparer.Ordinal);
    public long? ExpectedVersion { get; private set; }
    public bool Has(string key) => values.ContainsKey(key);
    public string? Get(string key) => values.GetValueOrDefault(key);
    public int Changed => values.Count;

    public static ShipmentInput Parse(JsonElement root, string action)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Expected JSON object");
        string[] allowed = action switch
        {
            "create" => ["referenceCode", "externalReference", "displayName",
                "originPortId", "destinationPortId", "plannedDepartureAtUtc", "plannedArrivalAtUtc"],
            "update" => ["displayName", "plannedDepartureAtUtc", "plannedArrivalAtUtc",
                "workflowStatus", "recordStatus"],
            "delete" => [],
            _ => throw new ArgumentException("Invalid action")
        };
        var input = new ShipmentInput();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!seen.Add(property.Name)) throw new ArgumentException("Duplicate JSON field");
            if (property.Name == "expectedVersion")
            {
                if (action == "create" || property.Value.ValueKind != JsonValueKind.Number ||
                    !property.Value.TryGetInt64(out var version) || version <= 0)
                    throw new ArgumentException("Invalid version");
                input.ExpectedVersion = version;
                continue;
            }
            if (!allowed.Contains(property.Name, StringComparer.Ordinal) ||
                property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                throw new ArgumentException("Unexpected field");
            var value = property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetString();
            if (value is { Length: > 2000 }) throw new ArgumentException("Value too long");
            input.values.Add(property.Name, value);
        }
        if (action == "create" && new[] { "referenceCode", "originPortId", "destinationPortId" }
            .Any(name => !input.Has(name))) throw new ArgumentException("Missing required field");
        if (action != "create" && !input.ExpectedVersion.HasValue ||
            action == "update" && input.Changed == 0 ||
            action == "delete" && input.Changed != 0)
            throw new ArgumentException("Invalid operation");
        return input;
    }
}

public sealed class ShipmentStore(HarborOpsDbContext db, OrganizationStore organizations, TimeProvider clock)
{
    private static Guid Id(string? value)
    {
        if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
            throw new ArgumentException("Invalid identifier");
        return id;
    }
    private static bool Constraint(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: "23505" or "23514" or "23503" };

    private async Task<OrganizationAccessError> ScopeAsync(CurrentSession session, Guid org,
        OrganizationPermission permission, bool write, CancellationToken ct)
    {
        // Keep the same lock order as accepted M2-M4 mutations.
        if (write)
            await db.Organizations.FromSqlInterpolated(
                $"SELECT * FROM organizations WHERE id = {org} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(ct);
        else
            await db.Organizations.FromSqlInterpolated(
                $"SELECT * FROM organizations WHERE id = {org} FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(ct);

        var current = write
            ? await db.AuthSessions.FromSqlInterpolated(
                $"SELECT * FROM auth_sessions WHERE id = {session.SessionId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(ct)
            : await db.AuthSessions.FromSqlInterpolated(
                $"SELECT * FROM auth_sessions WHERE id = {session.SessionId} FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(ct);
        if (current is null || current.UserId != session.UserId ||
            current.RevokedAtUtc is not null ||
            current.IdleExpiresAtUtc <= clock.GetUtcNow() ||
            current.AbsoluteExpiresAtUtc <= clock.GetUtcNow())
            return OrganizationAccessError.Revoked;
        if (current.SelectedOrganizationId is null) return OrganizationAccessError.SelectionRequired;
        if (current.SelectedOrganizationId != org) return OrganizationAccessError.NotFound;
        var user = await db.AuthUsers.FromSqlInterpolated(
            $"SELECT * FROM users WHERE id = {session.UserId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (user is null || user.Status != "Active" ||
            user.SecurityVersion != current.SecurityVersion)
            return OrganizationAccessError.Revoked;
        var resolved = await organizations.ResolveAsync(session.UserId, session.SessionId,
            org, permission, ct);
        return resolved.Value is null ? resolved.Error : OrganizationAccessError.None;
    }

    private IQueryable<ShipmentRecord> Own(Guid org) =>
        db.Shipments.Where(s => s.OrganizationId == org);

    public async Task<ShipmentResult<ShipmentPage>> ListAsync(CurrentSession session, Guid org,
        string q, string status, string? workflow, Guid? after, int limit, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var access = await ScopeAsync(session, org, OrganizationPermission.ViewShipmentData, false, ct);
        if (access != OrganizationAccessError.None) return new(null, access);
        IQueryable<ShipmentRecord> query = after.HasValue
            ? db.Shipments.FromSqlInterpolated(
                $"SELECT * FROM shipments WHERE organization_id = {org} AND id > {after.Value}")
            : Own(org);
        query = query.AsNoTracking().Where(x => x.OrganizationId == org);
        if (status != "All") query = query.Where(x => x.RecordStatus == status);
        if (workflow is not null) query = query.Where(x => x.WorkflowStatus == workflow);
        if (q.Length > 0)
        {
            var lowered = q.ToLowerInvariant();
            query = query.Where(x => x.ReferenceCode.ToLower().Contains(lowered) ||
                (x.ExternalReference != null && x.ExternalReference.ToLower().Contains(lowered)) ||
                (x.DisplayName != null && x.DisplayName.ToLower().Contains(lowered)));
        }
        var rows = await query.OrderBy(x => x.Id).Take(limit + 1).ToListAsync(ct);
        var more = rows.Count > limit;
        if (more) rows.RemoveAt(limit);
        await tx.CommitAsync(ct);
        return new(new ShipmentPage(rows, more ? rows[^1].Id : null), OrganizationAccessError.None);
    }

    public async Task<ShipmentResult<ShipmentRecord>> GetAsync(
        CurrentSession session, Guid org, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var access = await ScopeAsync(session, org, OrganizationPermission.ViewShipmentData, false, ct);
        if (access != OrganizationAccessError.None) return new(null, access);
        var row = await Own(org).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        await tx.CommitAsync(ct);
        return row is null ? new(null, OrganizationAccessError.NotFound) :
            new(row, OrganizationAccessError.None);
    }

    public async Task<ShipmentResult<IReadOnlyList<ShipmentStatusChange>>> HistoryAsync(
        CurrentSession session, Guid org, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var access = await ScopeAsync(session, org, OrganizationPermission.ViewShipmentData, false, ct);
        if (access != OrganizationAccessError.None) return new(null, access);
        if (!await Own(org).AsNoTracking().AnyAsync(x => x.Id == id, ct))
            return new(null, OrganizationAccessError.NotFound);
        var rows = await db.ShipmentStatusChanges.AsNoTracking()
            .Where(x => x.OrganizationId == org && x.ShipmentId == id)
            .OrderBy(x => x.Version).Take(500).ToListAsync(ct);
        await tx.CommitAsync(ct);
        return new(rows, OrganizationAccessError.None);
    }

    public async Task<ShipmentResult<ShipmentRecord>> WriteAsync(CurrentSession session,
        Guid org, Guid? id, string action, ShipmentInput input, CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var access = await ScopeAsync(session, org,
                OrganizationPermission.ManageShipmentData, true, ct);
            if (access != OrganizationAccessError.None) return new(null, access);
            ShipmentRecord row;
            var now = clock.GetUtcNow();
            if (action == "create")
            {
                var from = Id(input.Get("originPortId"));
                var to = Id(input.Get("destinationPortId"));
                if (from == to) throw new ArgumentException("Same origin and destination");
                // The organization write lock serializes M3 parent archival with shipment creation.
                var ports = await db.Ports.AsNoTracking().CountAsync(p =>
                    p.OrganizationId == org && p.Status == "Active" &&
                    (p.Id == from || p.Id == to), ct);
                if (ports != 2) return new(null, OrganizationAccessError.NotFound);
                row = new ShipmentRecord
                {
                    Id = Guid.NewGuid(), OrganizationId = org,
                    ReferenceCode = ShipmentPolicy.Code(input.Get("referenceCode")),
                    ExternalReference = ShipmentPolicy.ExternalReference(input.Get("externalReference")),
                    DisplayName = ShipmentPolicy.Name(input.Get("displayName")),
                    OriginPortId = from, DestinationPortId = to,
                    PlannedDepartureAtUtc = ShipmentPolicy.Utc(input.Get("plannedDepartureAtUtc")),
                    PlannedArrivalAtUtc = ShipmentPolicy.Utc(input.Get("plannedArrivalAtUtc")),
                    CreatedAtUtc = now, UpdatedAtUtc = now, ChangedByUserId = session.UserId
                };
                ShipmentPolicy.ValidateTimes(row.PlannedDepartureAtUtc, row.PlannedArrivalAtUtc);
                db.Shipments.Add(row);
            }
            else
            {
                row = await Own(org).SingleOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new MissingShipmentException();
                if (row.Version != input.ExpectedVersion)
                    return new(null, OrganizationAccessError.Conflict);
                if (row.RecordStatus == "Archived")
                {
                    if (action != "update" || input.Changed != 1 ||
                        input.Get("recordStatus") != "Active")
                        return new(null, OrganizationAccessError.Conflict);
                    row.RecordStatus = "Active";
                    row.ArchivedAtUtc = null;
                }
                else if (action == "delete")
                {
                    row.RecordStatus = "Archived";
                    row.ArchivedAtUtc = now;
                }
                else
                {
                    if (input.Has("recordStatus"))
                        return new(null, OrganizationAccessError.Conflict);
                    if (input.Has("displayName"))
                    {
                        var next = ShipmentPolicy.Name(input.Get("displayName"));
                        if (next == row.DisplayName) return new(null, OrganizationAccessError.Invalid);
                        row.DisplayName = next;
                    }
                    if (input.Has("plannedDepartureAtUtc") || input.Has("plannedArrivalAtUtc"))
                    {
                        if (row.WorkflowStatus is not ("Draft" or "Booked"))
                            return new(null, OrganizationAccessError.Conflict);
                        if (input.Has("plannedDepartureAtUtc"))
                            row.PlannedDepartureAtUtc = ShipmentPolicy.Utc(input.Get("plannedDepartureAtUtc"));
                        if (input.Has("plannedArrivalAtUtc"))
                            row.PlannedArrivalAtUtc = ShipmentPolicy.Utc(input.Get("plannedArrivalAtUtc"));
                    }
                    var nextStatus = input.Has("workflowStatus")
                        ? ShipmentPolicy.Workflow(input.Get("workflowStatus")) : row.WorkflowStatus;
                    if (!ShipmentPolicy.CanTransition(row.WorkflowStatus, nextStatus))
                        return new(null, OrganizationAccessError.Conflict);
                    ShipmentPolicy.ValidateTimes(row.PlannedDepartureAtUtc, row.PlannedArrivalAtUtc);
                    if (nextStatus == row.WorkflowStatus &&
                        input.Changed == 1 && input.Has("workflowStatus"))
                        return new(null, OrganizationAccessError.Invalid);
                    row.WorkflowStatus = nextStatus;
                }
                row.Version++;
                row.ChangedByUserId = session.UserId;
                row.UpdatedAtUtc = now > row.UpdatedAtUtc ? now : row.UpdatedAtUtc;
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(row, OrganizationAccessError.None);
        }
        catch (MissingShipmentException) { return new(null, OrganizationAccessError.NotFound); }
        catch (ArgumentException) { return new(null, OrganizationAccessError.Invalid); }
        catch (DbUpdateException ex) when (Constraint(ex))
        {
            return new(null, OrganizationAccessError.Conflict);
        }
    }
    private sealed class MissingShipmentException : Exception { }
}
