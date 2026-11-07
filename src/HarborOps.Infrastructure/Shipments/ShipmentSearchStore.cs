using System.Data;
using System.Globalization;
using System.Text;
using HarborOps.Domain.Organizations;
using HarborOps.Domain.Shipments;
using HarborOps.Infrastructure.Audit;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HarborOps.Infrastructure.Shipments;

public sealed record ShipmentSearchPage(IReadOnlyList<ShipmentRecord> Shipments,
    Guid? NextAfter, DateTimeOffset AsOfUtc);
public sealed record ShipmentSearchResult<T>(T? Value, OrganizationAccessError Error,
    string? FailureCode = null);

public sealed class ShipmentSearchStore(HarborOpsDbContext db, OrganizationStore organizations,
    TimeProvider clock, IConfiguration configuration)
{
    private async Task<OrganizationAccessError> ScopeAsync(CurrentSession session, Guid org,
        OrganizationPermission permission, CancellationToken ct)
    {
        await db.Organizations.FromSqlInterpolated(
            $"SELECT * FROM organizations WHERE id = {org} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        var current = await db.AuthSessions.FromSqlInterpolated(
            $"SELECT * FROM auth_sessions WHERE id = {session.SessionId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (current is null || current.UserId != session.UserId ||
            current.RevokedAtUtc is not null ||
            current.IdleExpiresAtUtc <= clock.GetUtcNow() ||
            current.AbsoluteExpiresAtUtc <= clock.GetUtcNow())
            return OrganizationAccessError.Revoked;
        if (current.SelectedOrganizationId is null)
            return OrganizationAccessError.SelectionRequired;
        if (current.SelectedOrganizationId != org)
            return OrganizationAccessError.NotFound;
        var user = await db.AuthUsers.FromSqlInterpolated(
            $"SELECT * FROM users WHERE id = {session.UserId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (user is null || user.Status != "Active" ||
            user.SecurityVersion != current.SecurityVersion)
            return OrganizationAccessError.Revoked;
        // Lock live membership until the read or export transaction commits.
        var membership = await db.OrganizationMemberships.FromSqlInterpolated(
            $"SELECT * FROM organization_memberships WHERE organization_id = {org} AND user_id = {session.UserId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (membership is null || membership.Status != "Active" ||
            !OrganizationPolicy.ParseRole(membership.Role, out var role) ||
            !OrganizationPolicy.Allows(role, permission))
            return OrganizationAccessError.Forbidden;
        var result = await organizations.ResolveAsync(session.UserId, session.SessionId, org,
            permission, ct);
        return result.Value is null ? result.Error : OrganizationAccessError.None;
    }

    private IQueryable<ShipmentRecord> Filter(Guid org, ShipmentSearchFilters f,
        DateTimeOffset asOfUtc, Guid? after = null)
    {
        IQueryable<ShipmentRecord> query = after.HasValue
            ? db.Shipments.FromSqlInterpolated(
                $"SELECT * FROM shipments WHERE organization_id = {org} AND id > {after.Value}")
                .AsNoTracking()
            : db.Shipments.AsNoTracking();
        query = query.Where(s => s.OrganizationId == org);
        if (f.RecordStatus != "All")
            query = query.Where(s => s.RecordStatus == f.RecordStatus);
        if (f.WorkflowStatus is not null)
            query = query.Where(s => s.WorkflowStatus == f.WorkflowStatus);
        if (f.VoyageId.HasValue)
            query = query.Where(s => db.ShipmentVoyageAssignments.Any(a =>
                a.OrganizationId == org && a.ShipmentId == s.Id &&
                a.VoyageId == f.VoyageId.Value && a.StartsAtUtc <= asOfUtc &&
                (a.EndsAtUtc == null || a.EndsAtUtc > asOfUtc)));
        if (f.OriginPortId.HasValue)
            query = query.Where(s => s.OriginPortId == f.OriginPortId.Value);
        if (f.DestinationPortId.HasValue)
            query = query.Where(s => s.DestinationPortId == f.DestinationPortId.Value);
        if (f.PlannedDepartureFromUtc.HasValue)
            query = query.Where(s => s.PlannedDepartureAtUtc >= f.PlannedDepartureFromUtc.Value);
        if (f.PlannedDepartureBeforeUtc.HasValue)
            query = query.Where(s => s.PlannedDepartureAtUtc < f.PlannedDepartureBeforeUtc.Value);
        if (f.PlannedArrivalFromUtc.HasValue)
            query = query.Where(s => s.PlannedArrivalAtUtc >= f.PlannedArrivalFromUtc.Value);
        if (f.PlannedArrivalBeforeUtc.HasValue)
            query = query.Where(s => s.PlannedArrivalAtUtc < f.PlannedArrivalBeforeUtc.Value);
        if (f.Q.Length > 0)
        {
            var lower = f.Q.ToLowerInvariant();
            query = query.Where(s => s.ReferenceCode.ToLower().Contains(lower) ||
                (s.ExternalReference != null && s.ExternalReference.ToLower().Contains(lower)) ||
                (s.DisplayName != null && s.DisplayName.ToLower().Contains(lower)));
        }
        return query;
    }

    public async Task<ShipmentSearchResult<ShipmentSearchPage>> SearchAsync(
        CurrentSession session, Guid org, ShipmentSearchFilters filter,
        DateTimeOffset asOfUtc, Guid? after, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SET LOCAL statement_timeout = '15s'", ct);
        var access = await ScopeAsync(session, org, OrganizationPermission.ViewShipmentData, ct);
        if (access != OrganizationAccessError.None) return new(null, access);
        var query = Filter(org, filter, asOfUtc, after);
        var rows = await query.OrderBy(s => s.Id)
            .Take(filter.Limit + 1).ToListAsync(ct);
        var more = rows.Count > filter.Limit;
        if (more) rows.RemoveAt(filter.Limit);
        await tx.CommitAsync(ct);
        return new(new ShipmentSearchPage(rows, more ? rows[^1].Id : null, asOfUtc),
            OrganizationAccessError.None);
    }

    public async Task<ShipmentSearchResult<byte[]>> ExportAsync(
        CurrentSession session, Guid org, ShipmentSearchFilters filter,
        string correlationId, CancellationToken ct)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(25));
        var operationToken = budget.Token;
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, operationToken);
        await db.Database.ExecuteSqlRawAsync("SET LOCAL statement_timeout = '15s'",
            operationToken);
        var access = await ScopeAsync(session, org, OrganizationPermission.ExportShipmentData, operationToken);
        if (access != OrganizationAccessError.None) return new(null, access);
        var asOf = clock.GetUtcNow();
        var rows = await Filter(org, filter, asOf).OrderBy(s => s.Id)
            .Take(ShipmentCsv.MaximumRows + 1).ToListAsync(operationToken);
        if (rows.Count > ShipmentCsv.MaximumRows)
            return new(null, OrganizationAccessError.Conflict, "export_row_limit");

        var csv = new StringBuilder();
        var heading = string.Join(",", ShipmentCsv.Headers.Select(ShipmentCsv.Cell)) + "\r\n";
        csv.Append(heading);
        var totalBytes = Encoding.UTF8.GetByteCount(heading);
        foreach (var s in rows)
        {
            var fields = new[]
            {
                s.Id.ToString("D"), s.ReferenceCode, s.ExternalReference, s.DisplayName,
                s.WorkflowStatus, s.RecordStatus, s.OriginPortId.ToString("D"),
                s.DestinationPortId.ToString("D"),
                s.PlannedDepartureAtUtc?.ToString("O", CultureInfo.InvariantCulture),
                s.PlannedArrivalAtUtc?.ToString("O", CultureInfo.InvariantCulture),
                s.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                s.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture)
            };
            var line = string.Join(",", fields.Select(ShipmentCsv.Cell)) + "\r\n";
            totalBytes += Encoding.UTF8.GetByteCount(line);
            if (totalBytes > ShipmentCsv.MaximumBytes)
                return new(null, OrganizationAccessError.Conflict, "export_byte_limit");
            csv.Append(line);
        }
        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        if (bytes.Length > ShipmentCsv.MaximumBytes)
            return new(null, OrganizationAccessError.Conflict, "export_byte_limit");
        var digest = filter.Fingerprint();
        var correlation = string.IsNullOrWhiteSpace(correlationId)
            ? "database" : correlationId[..Math.Min(correlationId.Length, 64)];
        var proof = M12ExportProofSigner.Sign(configuration["M12ExportProof:Key"],
            org, session.UserId, session.SessionId, rows.Count, bytes.LongLength,
            digest, correlation, clock);
        if (proof is null)
            return new(null, OrganizationAccessError.Invalid, "export_authority_unavailable");
        await db.Database.ExecuteSqlAsync($"""
            SELECT public.m12_record_shipment_export({org}, {session.UserId},
                {session.SessionId}, {rows.Count}, {bytes.LongLength},
                CAST({digest} AS varchar), CAST({correlation} AS varchar),
                {proof.ExpiresEpoch}, CAST({proof.Signature} AS varchar))
            """, operationToken);
        await tx.CommitAsync(operationToken);
        return new(bytes, OrganizationAccessError.None);
    }
}