using System.Text.Json;
using HarborOps.Api.Authentication;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.References;
using Microsoft.AspNetCore.Antiforgery;

namespace HarborOps.Api.References;

public static class ReferenceEndpoints
{
    private static CurrentSession Current(HttpContext ctx) =>
        (CurrentSession)ctx.Items["IdentitySession"]!;

    private static IResult Failure(HttpContext ctx, int status, string code, string message) =>
        Results.Json(new { code, message, correlationId = ctx.Items["CorrelationId"] }, statusCode: status);

    private static IResult Error(HttpContext ctx, OrganizationAccessError error) =>
        error switch
        {
            OrganizationAccessError.SelectionRequired => Failure(ctx, 409, "organization_selection_required", "Select an organization to continue"),
            OrganizationAccessError.Revoked => Failure(ctx, 403, "organization_access_revoked", "Organization access is no longer available"),
            OrganizationAccessError.NotFound => Failure(ctx, 404, "resource_not_found", "Resource not found"),
            OrganizationAccessError.Forbidden => Failure(ctx, 403, "access_denied", "Access denied"),
            OrganizationAccessError.Invalid => Failure(ctx, 400, "invalid_request", "Invalid request"),
            OrganizationAccessError.Conflict => Failure(ctx, 409, "conflict", "The requested change conflicts with current state"),
            _ => Failure(ctx, 500, "internal_error", "Request could not be completed")
        };

    private static bool ParseId(string? text, out Guid id) =>
        Guid.TryParse(text, out id) && id != Guid.Empty;

    private static bool ParseFilters(HttpContext ctx, out string? q, out string status,
        out Guid? after, out int limit)
    {
        q = ctx.Request.Query["q"].ToString().Trim();
        status = ctx.Request.Query["status"].ToString();
        after = null; limit = 25;
        if (ctx.Request.Query.Count > 4 ||
            ctx.Request.Query.Keys.Any(k => k is not ("q" or "status" or "limit" or "after")))
            return false;
        if (q.Length > 80 || q.Any(char.IsControl)) return false;
        if (status.Length == 0) status = "Active";
        if (status is not ("Active" or "Archived" or "All")) return false;
        var lim = ctx.Request.Query["limit"].ToString();
        if (lim.Length != 0 && (!int.TryParse(lim, out limit) || limit is < 1 or > 100))
            return false;
        var cursor = ctx.Request.Query["after"].ToString();
        if (cursor.Length != 0)
        {
            if (!ParseId(cursor, out var parsed)) return false;
            after = parsed;
        }
        return true;
    }

    private static async Task<bool> ValidMutation(HttpContext ctx, IAntiforgery anti) =>
        AuthenticationSetup.IsAllowedTransport(ctx, ctx.RequestServices.GetRequiredService<IConfiguration>())
        && await AuthenticationSetup.ValidMutationAsync(ctx, anti);

    private static async Task<ReferenceInput?> Input(HttpContext ctx, bool isPort, string action)
    {
        var contentType = ctx.Request.ContentType ?? "";
        if (!contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase) &&
            !contentType.StartsWith("application/json;", StringComparison.OrdinalIgnoreCase))
            return null;
        const int maxBytes = 8192;
        if (ctx.Request.ContentLength > maxBytes) return null;
        var buffer = new byte[maxBytes + 1];
        var size = 0;
        try
        {
            int read;
            while (size < buffer.Length &&
                (read = await ctx.Request.Body.ReadAsync(buffer.AsMemory(size), ctx.RequestAborted)) != 0)
                size += read;
            if (size > maxBytes || size == 0) return null;
            using var document = JsonDocument.Parse(buffer.AsMemory(0, size),
                new JsonDocumentOptions { MaxDepth = 16 });
            return ReferenceInput.Parse(document.RootElement, isPort, action);
        }
        catch (JsonException) { return null; }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    public static void MapReferenceEndpoints(this WebApplication app)
    {
        var root = app.MapGroup("/api/v1/organizations/{organizationId}").RequireAuthorization();
        root.MapGet("/ports", async (string organizationId, HttpContext ctx,
            ReferenceStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!ParseId(organizationId, out var org) ||
                !ParseFilters(ctx, out var q, out var status, out var after, out var limit))
                return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.ListPortsAsync(Current(ctx), org, q, status, after, limit, ct);
            return result.Value is null ? Error(ctx, result.Error) :
                Results.Ok(new { ports = result.Value.Items, nextAfter = result.Value.NextAfter });
        });
        root.MapGet("/ports/{portId}", async (string organizationId, string portId,
            HttpContext ctx, ReferenceStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!ParseId(organizationId, out var org) || !ParseId(portId, out var id))
                return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.GetPortAsync(Current(ctx), org, id, ct);
            return result.Value is null ? Error(ctx, result.Error) : Results.Ok(result.Value);
        });
        root.MapPost("/ports", async (string organizationId, HttpContext ctx,
            IAntiforgery anti, ReferenceStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti))
                return Failure(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!ParseId(organizationId, out var org))
                return Error(ctx, OrganizationAccessError.Invalid);
            var input = await Input(ctx, true, "create");
            if (input is null) return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.WritePortAsync(Current(ctx), org, null, "create", input, ct);
            return result.Value is null ? Error(ctx, result.Error) :
                Results.Created($"{ctx.Request.Path}/{result.Value.Id}", result.Value);
        });
        root.MapPatch("/ports/{portId}", async (string organizationId, string portId,
            HttpContext ctx, IAntiforgery anti,
            ReferenceStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti))
                return Failure(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!ParseId(organizationId, out var org) || !ParseId(portId, out var id))
                return Error(ctx, OrganizationAccessError.Invalid);
            var input = await Input(ctx, true, "update");
            if (input is null) return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.WritePortAsync(Current(ctx), org, id, "update", input, ct);
            return result.Value is null ? Error(ctx, result.Error) : Results.Ok(result.Value);
        });
        root.MapDelete("/ports/{portId}", async (string organizationId, string portId,
            HttpContext ctx, IAntiforgery anti,
            ReferenceStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti))
                return Failure(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!ParseId(organizationId, out var org) || !ParseId(portId, out var id))
                return Error(ctx, OrganizationAccessError.Invalid);
            var input = await Input(ctx, true, "delete");
            if (input is null) return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.WritePortAsync(Current(ctx), org, id, "delete", input, ct);
            return result.Value is null ? Error(ctx, result.Error) : Results.NoContent();
        });

        root.MapGet("/vessels", async (string organizationId, HttpContext ctx,
            ReferenceStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!ParseId(organizationId, out var org) ||
                !ParseFilters(ctx, out var q, out var status, out var after, out var limit))
                return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.ListVesselsAsync(Current(ctx), org, q, status, after, limit, ct);
            return result.Value is null ? Error(ctx, result.Error) :
                Results.Ok(new { vessels = result.Value.Items, nextAfter = result.Value.NextAfter });
        });
        root.MapGet("/vessels/{vesselId}", async (string organizationId, string vesselId,
            HttpContext ctx, ReferenceStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!ParseId(organizationId, out var org) || !ParseId(vesselId, out var id))
                return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.GetVesselAsync(Current(ctx), org, id, ct);
            return result.Value is null ? Error(ctx, result.Error) : Results.Ok(result.Value);
        });
        root.MapPost("/vessels", async (string organizationId, HttpContext ctx,
            IAntiforgery anti, ReferenceStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti))
                return Failure(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!ParseId(organizationId, out var org))
                return Error(ctx, OrganizationAccessError.Invalid);
            var input = await Input(ctx, false, "create");
            if (input is null) return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.WriteVesselAsync(Current(ctx), org, null, "create", input, ct);
            return result.Value is null ? Error(ctx, result.Error) :
                Results.Created($"{ctx.Request.Path}/{result.Value.Id}", result.Value);
        });
        root.MapPatch("/vessels/{vesselId}", async (string organizationId, string vesselId,
            HttpContext ctx, IAntiforgery anti,
            ReferenceStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti))
                return Failure(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!ParseId(organizationId, out var org) || !ParseId(vesselId, out var id))
                return Error(ctx, OrganizationAccessError.Invalid);
            var input = await Input(ctx, false, "update");
            if (input is null) return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.WriteVesselAsync(Current(ctx), org, id, "update", input, ct);
            return result.Value is null ? Error(ctx, result.Error) : Results.Ok(result.Value);
        });
        root.MapDelete("/vessels/{vesselId}", async (string organizationId, string vesselId,
            HttpContext ctx, IAntiforgery anti,
            ReferenceStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti))
                return Failure(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!ParseId(organizationId, out var org) || !ParseId(vesselId, out var id))
                return Error(ctx, OrganizationAccessError.Invalid);
            var input = await Input(ctx, false, "delete");
            if (input is null) return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.WriteVesselAsync(Current(ctx), org, id, "delete", input, ct);
            return result.Value is null ? Error(ctx, result.Error) : Results.NoContent();
        });
    }
}
