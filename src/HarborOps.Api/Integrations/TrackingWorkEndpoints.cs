using HarborOps.Api.Authentication;
using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Organizations;
using Microsoft.AspNetCore.Antiforgery;

namespace HarborOps.Api.Integrations;

public sealed record RedriveTrackingWorkRequest(string? Reason);

public static class TrackingWorkEndpoints
{
    private static CurrentSession Current(HttpContext ctx) =>
        (CurrentSession)ctx.Items["IdentitySession"]!;

    private static IResult Failure(HttpContext ctx, int status, string code, string message) =>
        Results.Json(new { code, message, correlationId = ctx.Items["CorrelationId"] },
            statusCode: status);

    private static IResult Denied(HttpContext ctx, OrganizationAccessError error) => error switch
    {
        OrganizationAccessError.SelectionRequired => Failure(ctx, 409, "organization_selection_required",
            "Select an organization to continue"),
        OrganizationAccessError.Revoked => Failure(ctx, 403, "organization_access_revoked",
            "Organization access is no longer available"),
        OrganizationAccessError.NotFound => Failure(ctx, 404, "resource_not_found", "Resource not found"),
        OrganizationAccessError.Forbidden => Failure(ctx, 403, "access_denied", "Access denied"),
        _ => Failure(ctx, 400, "invalid_request", "Invalid request")
    };

    private static bool Id(string? text, out Guid id) =>
        Guid.TryParse(text, out id) && id != Guid.Empty;

    public static void MapTrackingWorkEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId}/tracking-work")
            .RequireAuthorization();

        group.MapGet("", async (string organizationId, HttpContext ctx,
            OrganizationStore organizations, TrackingWorkAdminStore work, CancellationToken ct) =>
        {
            if (!Id(organizationId, out var organization) || ctx.Request.Query.Count > 2 ||
                ctx.Request.Query.Keys.Any(k => k is not ("after" or "limit")) ||
                ctx.Request.Query.Any(k => k.Value.Count != 1))
                return Failure(ctx, 400, "invalid_request", "Invalid request");
            Guid? after = null;
            var requestedAfter = ctx.Request.Query["after"].ToString();
            if (requestedAfter.Length > 0)
            {
                if (!Id(requestedAfter, out var parsed))
                    return Failure(ctx, 400, "invalid_request", "Invalid pagination cursor");
                after = parsed;
            }
            var limit = 25;
            var requestedLimit = ctx.Request.Query["limit"].ToString();
            if (requestedLimit.Length > 0 &&
                (!int.TryParse(requestedLimit, out limit) || limit is < 1 or > 100))
                return Failure(ctx, 400, "invalid_request", "Invalid pagination size");
            var session = Current(ctx);
            var access = await organizations.ResolveAsync(session.UserId, session.SessionId,
                organization, OrganizationPermission.TrackingWorkRead, ct);
            if (access.Value is null) return Denied(ctx, access.Error);
            var items = await work.ListAsync(access.Value, after, limit, ct);
            return Results.Ok(new { items, nextAfter = items.Count == limit
                ? items[^1].ReceiptId : (Guid?)null });
        });

        group.MapGet("/{receiptId}", async (string organizationId, string receiptId,
            HttpContext ctx, OrganizationStore organizations, TrackingWorkAdminStore work,
            CancellationToken ct) =>
        {
            if (!Id(organizationId, out var organization) || !Id(receiptId, out var receipt))
                return Failure(ctx, 400, "invalid_request", "Invalid request");
            var session = Current(ctx);
            var access = await organizations.ResolveAsync(session.UserId, session.SessionId,
                organization, OrganizationPermission.TrackingWorkRead, ct);
            if (access.Value is null) return Denied(ctx, access.Error);
            var result = await work.GetAsync(access.Value, receipt, ct);
            return result is null ? Failure(ctx, 404, "resource_not_found", "Resource not found")
                : Results.Ok(result);
        });

        group.MapPost("/{receiptId}/redrive", async (string organizationId, string receiptId,
            RedriveTrackingWorkRequest input, HttpContext ctx, IAntiforgery anti,
            OrganizationStore organizations, TrackingWorkAdminStore work, CancellationToken ct) =>
        {
            if (!AuthenticationSetup.IsAllowedTransport(ctx, app.Configuration) ||
                !await AuthenticationSetup.ValidMutationAsync(ctx, anti))
                return Failure(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!Id(organizationId, out var organization) || !Id(receiptId, out var receipt) ||
                string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length is < 8 or > 240)
                return Failure(ctx, 400, "invalid_request", "Invalid redrive request");
            var session = Current(ctx);
            var access = await organizations.ResolveAsync(session.UserId, session.SessionId,
                organization, OrganizationPermission.TrackingWorkRedrive, ct);
            if (access.Value is null) return Denied(ctx, access.Error);
            TrackingRedriveOutcome result;
            try
            {
                result = await work.RedriveAsync(organization, session.UserId,
                    session.SessionId, receipt, input.Reason, ct);
            }
            catch (ArgumentException)
            {
                return Failure(ctx, 400, "invalid_request", "Invalid redrive reason");
            }
            return result switch
            {
                TrackingRedriveOutcome.Redriven => Results.Ok(new { receiptId = receipt,
                    status = "Pending", correlationId = ctx.Items["CorrelationId"] }),
                TrackingRedriveOutcome.NotFound => Failure(ctx, 404, "resource_not_found", "Resource not found"),
                TrackingRedriveOutcome.AccessDenied => Failure(ctx, 403, "access_denied", "Access denied"),
                TrackingRedriveOutcome.GenerationLimit => Failure(ctx, 409, "redrive_limit",
                    "Manual redrive limit reached"),
                TrackingRedriveOutcome.CapacityExceeded => Failure(ctx, 409, "work_capacity_exceeded",
                    "Tracking work remains at capacity"),
                _ => Failure(ctx, 409, "work_state_conflict", "Work cannot be redriven in this state")
            };
        });
    }
}
