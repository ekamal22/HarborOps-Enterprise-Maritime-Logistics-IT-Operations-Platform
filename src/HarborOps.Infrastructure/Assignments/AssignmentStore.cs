using System.Text.Json;
using HarborOps.Domain.Assignments;
using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Maritime;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.Shipments;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HarborOps.Infrastructure.Assignments;

public sealed record AssignmentResult<T>(T? Value, OrganizationAccessError Error);
public sealed record AssignmentPage<T>(IReadOnlyList<T> Assignments, Guid? NextAfter);

public sealed record AssignmentInput(
    Guid? TargetId, DateTimeOffset? StartsAtUtc, DateTimeOffset? EndsAtUtc, long? ExpectedVersion)
{
    public static AssignmentInput Parse(JsonElement root, bool voyage, bool close)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new ArgumentException("Object required");
        var target = voyage ? "voyageId" : "containerId";
        Guid? id = null;
        DateTimeOffset? start = null, end = null;
        long? version = null;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in root.EnumerateObject())
        {
            if (!names.Add(field.Name)) throw new ArgumentException("Duplicate field");
            if (field.Name == target && !close && field.Value.ValueKind == JsonValueKind.String &&
                Guid.TryParse(field.Value.GetString(), out var guid) && guid != Guid.Empty)
                id = guid;
            else if (field.Name == "startsAtUtc" && !close &&
                field.Value.ValueKind == JsonValueKind.String)
                start = AssignmentPolicy.Utc(field.Value.GetString());
            else if (field.Name == "endsAtUtc" &&
                field.Value.ValueKind is (JsonValueKind.String or JsonValueKind.Null))
                end = field.Value.ValueKind == JsonValueKind.Null ? null :
                    AssignmentPolicy.Utc(field.Value.GetString());
            else if (field.Name == "expectedVersion" && close &&
                field.Value.ValueKind == JsonValueKind.Number &&
                field.Value.TryGetInt64(out var number) && number > 0)
                version = number;
            else throw new ArgumentException("Invalid assignment request");
        }
        if (close)
        {
            if (version is null || !names.Contains("endsAtUtc") || end is null || names.Count != 2)
                throw new ArgumentException("Version and closure time required");
        }
        else if (id is null || start is null || names.Count is < 2 or > 3)
            throw new ArgumentException("Target and start required");
        if (start is not null) AssignmentPolicy.Window(start.Value, end);
        return new(id, start, end, version);
    }
}

public sealed class AssignmentStore(HarborOpsDbContext db, OrganizationStore organizations, TimeProvider clock)
{
    private static bool IsConstraint(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
            if (e is PostgresException p && p.SqlState is
                ("23P01" or "23505" or "23514" or "23503" or "P0001"))
                return true;
        return false;
    }

    private async Task<OrganizationAccessError> ScopeAsync(CurrentSession session, Guid org,
        OrganizationPermission permission, bool write, CancellationToken ct)
    {
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
        if (user is null || user.Status != "Active" || user.SecurityVersion != current.SecurityVersion)
            return OrganizationAccessError.Revoked;
        var resolved = await organizations.ResolveAsync(session.UserId, session.SessionId,
            org, permission, ct);
        return resolved.Value is null ? resolved.Error : OrganizationAccessError.None;
    }

    public async Task<AssignmentResult<AssignmentPage<T>>> ListAsync<T>(CurrentSession session,
        Guid org, Guid shipment, Guid? after, int limit, CancellationToken ct)
        where T : AssignmentRecord
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var access = await ScopeAsync(session, org,
            OrganizationPermission.ViewShipmentAssignments, false, ct);
        if (access != OrganizationAccessError.None) return new(null, access);
        if (!await db.Shipments.AsNoTracking().AnyAsync(s =>
            s.OrganizationId == org && s.Id == shipment, ct))
            return new(null, OrganizationAccessError.NotFound);
        var query = db.Set<T>().AsNoTracking()
            .Where(x => x.OrganizationId == org && x.ShipmentId == shipment);
        if (after.HasValue) query = query.Where(x => x.Id.CompareTo(after.Value) > 0);
        var rows = await query.OrderBy(x => x.Id).Take(limit + 1).ToListAsync(ct);
        var more = rows.Count > limit;
        if (more) rows.RemoveAt(limit);
        await tx.CommitAsync(ct);
        return new(new AssignmentPage<T>(rows, more ? rows[^1].Id : null),
            OrganizationAccessError.None);
    }

    public async Task<AssignmentResult<T>> GetAsync<T>(CurrentSession session, Guid org,
        Guid shipment, Guid id, CancellationToken ct) where T : AssignmentRecord
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var access = await ScopeAsync(session, org,
            OrganizationPermission.ViewShipmentAssignments, false, ct);
        if (access != OrganizationAccessError.None) return new(null, access);
        var row = await db.Set<T>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.OrganizationId == org && x.ShipmentId == shipment && x.Id == id, ct);
        await tx.CommitAsync(ct);
        return row is null ? new(null, OrganizationAccessError.NotFound) :
            new(row, OrganizationAccessError.None);
    }

    public async Task<AssignmentResult<T>> WriteAsync<T>(CurrentSession session, Guid org,
        Guid shipmentId, Guid? assignmentId, AssignmentInput input, CancellationToken ct)
        where T : AssignmentRecord, new()
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var access = await ScopeAsync(session, org,
                OrganizationPermission.ManageShipmentAssignments, true, ct);
            if (access != OrganizationAccessError.None) return new(null, access);
            // Lock the shipment row so a separate direct-SQL archive must wait.
            var shipment = await db.Shipments.FromSqlInterpolated(
                $"SELECT * FROM shipments WHERE organization_id = {org} AND id = {shipmentId} FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(ct);
            if (shipment is null) return new(null, OrganizationAccessError.NotFound);
            T row;
            var now = clock.GetUtcNow();
            if (assignmentId is null)
            {
                if (shipment.RecordStatus != "Active" ||
                    shipment.WorkflowStatus is ("Cancelled" or "Completed"))
                    return new(null, OrganizationAccessError.Conflict);
                if (input.TargetId is null || input.StartsAtUtc is null)
                    return new(null, OrganizationAccessError.Invalid);
                row = new T
                {
                    Id = Guid.NewGuid(), OrganizationId = org, ShipmentId = shipmentId,
                    StartsAtUtc = input.StartsAtUtc.Value, EndsAtUtc = input.EndsAtUtc,
                    CreatedAtUtc = now, UpdatedAtUtc = now,
                    CreatedByUserId = session.UserId, Version = 1
                };
                if (row is ShipmentContainerAssignment container)
                {
                    container.ContainerId = input.TargetId.Value;
                    var target = await db.Containers.FromSqlInterpolated(
                        $"SELECT * FROM containers WHERE organization_id = {org} AND id = {container.ContainerId} FOR SHARE")
                        .AsNoTracking().SingleOrDefaultAsync(ct);
                    if (target is null) return new(null, OrganizationAccessError.NotFound);
                    if (target.RecordStatus != "Active") return new(null, OrganizationAccessError.Conflict);
                }
                else if (row is ShipmentVoyageAssignment voyage)
                {
                    voyage.VoyageId = input.TargetId.Value;
                    var target = await db.Voyages.FromSqlInterpolated(
                        $"SELECT * FROM voyages WHERE organization_id = {org} AND id = {voyage.VoyageId} FOR SHARE")
                        .AsNoTracking().SingleOrDefaultAsync(ct);
                    if (target is null) return new(null, OrganizationAccessError.NotFound);
                    if (target.RecordStatus != "Active" ||
                        target.VoyageStatus is not ("Planned" or "Underway") ||
                        !AssignmentPolicy.VoyageCompatible(shipment.OriginPortId,
                            shipment.DestinationPortId, shipment.PlannedDepartureAtUtc,
                            shipment.PlannedArrivalAtUtc, target.OriginPortId,
                            target.DestinationPortId, target.PlannedDepartureAtUtc,
                            target.PlannedArrivalAtUtc))
                        return new(null, OrganizationAccessError.Conflict);
                }
                else return new(null, OrganizationAccessError.Invalid);
                db.Set<T>().Add(row);
            }
            else
            {
                row = await db.Set<T>().SingleOrDefaultAsync(x =>
                    x.OrganizationId == org && x.ShipmentId == shipmentId &&
                    x.Id == assignmentId.Value, ct)
                    ?? throw new MissingAssignmentException();
                if (input.ExpectedVersion != row.Version || row.EndsAtUtc is not null ||
                    input.EndsAtUtc is null)
                    return new(null, OrganizationAccessError.Conflict);
                AssignmentPolicy.Window(row.StartsAtUtc, input.EndsAtUtc);
                row.EndsAtUtc = input.EndsAtUtc;
                row.ClosedByUserId = session.UserId;
                row.Version++;
                row.UpdatedAtUtc = now > row.UpdatedAtUtc ? now : row.UpdatedAtUtc;
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(row, OrganizationAccessError.None);
        }
        catch (MissingAssignmentException) { return new(null, OrganizationAccessError.NotFound); }
        catch (ArgumentException) { return new(null, OrganizationAccessError.Invalid); }
        catch (Exception ex) when (IsConstraint(ex))
        {
            return new(null, OrganizationAccessError.Conflict);
        }
    }

    private sealed class MissingAssignmentException : Exception { }
}
