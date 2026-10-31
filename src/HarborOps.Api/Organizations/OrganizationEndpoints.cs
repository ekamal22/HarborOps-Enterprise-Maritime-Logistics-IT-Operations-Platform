using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using Microsoft.AspNetCore.Antiforgery;

namespace HarborOps.Api.Organizations;

public sealed record SelectOrganizationRequest(string? OrganizationId);
public sealed record UpdateOrganizationRequest(string? DisplayName, long ExpectedVersion);
public sealed record PutMembershipRequest(string? Role, string? Status, long? ExpectedVersion);
public sealed record RemoveMembershipRequest(long ExpectedVersion);

public static class OrganizationEndpoints
{
    private static CurrentSession Current(HttpContext ctx) =>
        (CurrentSession)ctx.Items["IdentitySession"]!;

    private static IResult Error(HttpContext ctx, OrganizationAccessError error) =>
        error switch
        {
            OrganizationAccessError.SelectionRequired => Failure(ctx, 409, "organization_selection_required",
                "Select an organization to continue"),
            OrganizationAccessError.Revoked => Failure(ctx, 403, "organization_access_revoked",
                "Organization access is no longer available"),
            OrganizationAccessError.NotFound => Failure(ctx, 404, "resource_not_found",
                "Resource not found"),
            OrganizationAccessError.Forbidden => Failure(ctx, 403, "access_denied", "Access denied"),
            OrganizationAccessError.Invalid => Failure(ctx, 400, "invalid_request", "Invalid request"),
            OrganizationAccessError.Conflict => Failure(ctx, 409, "conflict",
                "The requested change conflicts with current state"),
            _ => Failure(ctx, 500, "internal_error", "Request could not be completed")
        };

    private static IResult Failure(HttpContext ctx, int status, string code, string message) =>
        Results.Json(new { code, message, correlationId = ctx.Items["CorrelationId"] }, statusCode: status);

    private static bool TryId(string value, out Guid id) =>
        Guid.TryParse(value, out id) && id != Guid.Empty;

    private static object Selection(OrganizationSelection value) => new
    {
        id = value.Id, slug = value.Slug, displayName = value.DisplayName,
        role = value.Role.ToString(), membershipVersion = value.MembershipVersion
    };

    private static async Task<bool> ValidMutation(HttpContext ctx, IAntiforgery anti) =>
        Authentication.AuthenticationSetup.IsAllowedTransport(
            ctx, ctx.RequestServices.GetRequiredService<IConfiguration>()) &&
        await Authentication.AuthenticationSetup.ValidMutationAsync(ctx, anti);

    private static async Task<(OrganizationScope?, IResult?)> Authorized(
        HttpContext ctx, OrganizationStore store, Guid organizationId,
        OrganizationPermission permission, CancellationToken ct)
    {
        var user = Current(ctx);
        var check = await store.ResolveAsync(user.UserId, user.SessionId, organizationId, permission, ct);
        return check.Value is null ? (null, Error(ctx, check.Error)) : (check.Value, null);
    }

    public static void MapOrganizationEndpoints(this WebApplication app)
    {
        var me = app.MapGroup("/api/v1/me").RequireAuthorization();
        me.MapGet("/organizations", async (HttpContext ctx, OrganizationStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            var list = await store.ListMineAsync(Current(ctx).UserId, ct);
            return Results.Ok(new { organizations = list.Select(Selection).ToArray() });
        });

        me.MapGet("/organization-context", async (HttpContext ctx, OrganizationStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            var user = Current(ctx);
            var result = await store.ResolveAsync(user.UserId, user.SessionId, null,
                OrganizationPermission.ViewOrganization, ct);
            if (result.Error == OrganizationAccessError.SelectionRequired)
                return Results.Ok(new { selected = (object?)null });
            if (result.Value is null) return Error(ctx, result.Error);
            return Results.Ok(new { selected = Selection(new OrganizationSelection(
                result.Value.OrganizationId, result.Value.Slug, result.Value.DisplayName,
                result.Value.Role, result.Value.MembershipVersion)) });
        });

        var session = app.MapGroup("/api/v1/auth/session").RequireAuthorization();
        session.MapPut("/organization", async (SelectOrganizationRequest input,
            HttpContext ctx, IAntiforgery anti, OrganizationStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti))
                return Failure(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!TryId(input.OrganizationId ?? "", out var organizationId))
                return Error(ctx, OrganizationAccessError.Invalid);
            var current = Current(ctx);
            var result = await store.SelectAsync(current.UserId, current.SessionId, organizationId, ct);
            return result.Value is null ? Error(ctx, result.Error) :
                Results.Ok(new { selected = Selection(result.Value) });
        });
        session.MapDelete("/organization", async (HttpContext ctx, IAntiforgery anti,
            OrganizationStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti))
                return Failure(ctx, 403, "csrf_invalid", "Request validation failed");
            var current = Current(ctx);
            await store.ClearSelectionAsync(current.UserId, current.SessionId, ct);
            return Results.NoContent();
        });

        var organizations = app.MapGroup("/api/v1/organizations").RequireAuthorization();
        organizations.MapGet("/{organizationId}", async (string organizationId, HttpContext ctx,
            OrganizationStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!TryId(organizationId, out var id)) return Error(ctx, OrganizationAccessError.Invalid);
            var (scope, denied) = await Authorized(ctx, store, id,
                OrganizationPermission.ViewOrganization, ct);
            return denied ?? Results.Ok(OrganizationStore.Profile(scope!));
        });

        organizations.MapPatch("/{organizationId}", async (string organizationId,
            UpdateOrganizationRequest input, HttpContext ctx, IAntiforgery anti,
            OrganizationStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti))
                return Failure(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!TryId(organizationId, out var id) || input.ExpectedVersion < 1)
                return Error(ctx, OrganizationAccessError.Invalid);
            var (scope, denied) = await Authorized(ctx, store, id,
                OrganizationPermission.UpdateOrganization, ct);
            if (denied is not null) return denied;
            var current = Current(ctx);
            var changed = await store.UpdateProfileAsync(current.UserId, current.SessionId,
                scope!.OrganizationId, input.DisplayName ?? "", input.ExpectedVersion,
                ctx.Items["CorrelationId"]?.ToString() ?? "", ct);
            return changed.Value is null ? Error(ctx, changed.Error) : Results.Ok(changed.Value);
        });

        organizations.MapGet("/{organizationId}/memberships", async (string organizationId,
            HttpContext ctx, OrganizationStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!TryId(organizationId, out var id))
                return Error(ctx, OrganizationAccessError.Invalid);
            var afterText = ctx.Request.Query["after"].ToString();
            var limitText = ctx.Request.Query["limit"].ToString();
            Guid? after = null;
            if (afterText.Length > 0)
            {
                if (!TryId(afterText, out var parsed)) return Error(ctx, OrganizationAccessError.Invalid);
                after = parsed;
            }
            var limit = 25;
            if (limitText.Length > 0 &&
                (!int.TryParse(limitText, out limit) || limit is < 1 or > 100))
                return Error(ctx, OrganizationAccessError.Invalid);
            var (scope, denied) = await Authorized(ctx, store, id,
                OrganizationPermission.ViewMemberships, ct);
            if (denied is not null) return denied;
            var memberships = await store.ListMembersAsync(scope!, after, limit, ct);
            return Results.Ok(new { memberships, nextAfter = memberships.Count == limit
                ? memberships[^1].UserId : (Guid?)null });
        });

        organizations.MapGet("/{organizationId}/memberships/{userId}", async (
            string organizationId, string userId, HttpContext ctx,
            OrganizationStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!TryId(organizationId, out var id) || !TryId(userId, out var target))
                return Error(ctx, OrganizationAccessError.Invalid);
            var (scope, denied) = await Authorized(ctx, store, id,
                OrganizationPermission.ViewMemberships, ct);
            if (denied is not null) return denied;
            var member = await store.GetMemberAsync(scope!, target, ct);
            return member is null ? Error(ctx, OrganizationAccessError.NotFound) : Results.Ok(member);
        });

        organizations.MapPut("/{organizationId}/memberships/{userId}", async (
            string organizationId, string userId, PutMembershipRequest input,
            HttpContext ctx, IAntiforgery anti, OrganizationStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti))
                return Failure(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!TryId(organizationId, out var id) || !TryId(userId, out var target))
                return Error(ctx, OrganizationAccessError.Invalid);
            var (scope, denied) = await Authorized(ctx, store, id,
                OrganizationPermission.ManageMemberships, ct);
            if (denied is not null) return denied;
            var current = Current(ctx);
            var changed = await store.PutMemberAsync(current.UserId, current.SessionId,
                scope!.OrganizationId, target, input.Role ?? "", input.Status ?? "",
                input.ExpectedVersion, ctx.Items["CorrelationId"]?.ToString() ?? "", ct);
            return changed.Value is null ? Error(ctx, changed.Error) : Results.Ok(changed.Value);
        });

        organizations.MapDelete("/{organizationId}/memberships/{userId}", async (
            string organizationId, string userId, RemoveMembershipRequest input,
            HttpContext ctx, IAntiforgery anti, OrganizationStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti))
                return Failure(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!TryId(organizationId, out var id) || !TryId(userId, out var target) ||
                input.ExpectedVersion < 1)
                return Error(ctx, OrganizationAccessError.Invalid);
            var (scope, denied) = await Authorized(ctx, store, id,
                OrganizationPermission.ManageMemberships, ct);
            if (denied is not null) return denied;
            var current = Current(ctx);
            var error = await store.RemoveMemberAsync(current.UserId, current.SessionId,
                scope!.OrganizationId, target, input.ExpectedVersion,
                ctx.Items["CorrelationId"]?.ToString() ?? "", ct);
            return error == OrganizationAccessError.None ? Results.NoContent() : Error(ctx, error);
        });
    }
}
