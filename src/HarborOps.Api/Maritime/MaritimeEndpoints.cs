using System.Text.Json;
using HarborOps.Api.Authentication;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Maritime;
using HarborOps.Infrastructure.Organizations;
using Microsoft.AspNetCore.Antiforgery;

namespace HarborOps.Api.Maritime;

public static class MaritimeEndpoints
{
    private static CurrentSession Session(HttpContext ctx) => (CurrentSession)ctx.Items["IdentitySession"]!;
    private static IResult Fail(HttpContext ctx, int status, string code, string message) =>
        Results.Json(new { code, message, correlationId = ctx.Items["CorrelationId"] }, statusCode: status);
    private static IResult Error(HttpContext ctx, OrganizationAccessError error) => error switch
    {
        OrganizationAccessError.SelectionRequired => Fail(ctx, 409, "organization_selection_required", "Select an organization to continue"),
        OrganizationAccessError.Revoked => Fail(ctx, 403, "organization_access_revoked", "Organization access is no longer available"),
        OrganizationAccessError.Forbidden => Fail(ctx, 403, "access_denied", "Access denied"),
        OrganizationAccessError.NotFound => Fail(ctx, 404, "resource_not_found", "Resource not found"),
        OrganizationAccessError.Invalid => Fail(ctx, 400, "invalid_request", "Invalid request"),
        OrganizationAccessError.Conflict => Fail(ctx, 409, "conflict", "The requested change conflicts with current state"),
        _ => Fail(ctx, 500, "internal_error", "Request could not be completed")
    };
    private static bool Id(string value, out Guid id) => Guid.TryParse(value, out id) && id != Guid.Empty;

    private static bool ListFilters(HttpContext ctx, bool voyage, out string q, out string status,
        out Guid? after, out int limit, out Guid? vessel, out Guid? origin, out Guid? destination,
        out string? progress, out string? kind, out string? equipment)
    {
        var args = ctx.Request.Query;
        var allowed = voyage
            ? new[] { "q", "recordStatus", "after", "limit", "vesselId", "originPortId", "destinationPortId", "voyageStatus" }
            : new[] { "q", "recordStatus", "after", "limit", "identifierKind", "equipmentType" };
        q = args["q"].ToString().Trim();
        status = args["recordStatus"].ToString();
        after = null; limit = 25; vessel = null; origin = null; destination = null;
        progress = null; kind = null; equipment = null;
        if (args.Count > allowed.Length || args.Keys.Any(x => !allowed.Contains(x, StringComparer.Ordinal)) ||
            args.Any(x => x.Value.Count != 1) || q.Length > 80 || q.Any(char.IsControl))
            return false;
        if (status == "") status = "Active";
        if (status is not ("Active" or "Archived" or "All")) return false;
        if (args.ContainsKey("limit") && (!int.TryParse(args["limit"], out limit) || limit is < 1 or > 100))
            return false;
        if (!ParseNullable(args["after"], out after) ||
            !ParseNullable(args["vesselId"], out vessel) ||
            !ParseNullable(args["originPortId"], out origin) ||
            !ParseNullable(args["destinationPortId"], out destination))
            return false;
        progress = args["voyageStatus"].ToString();
        if (progress == "") progress = null;
        if (progress is not null && progress is not ("Planned" or "Underway" or "Arrived" or "Cancelled"))
            return false;
        kind = args["identifierKind"].ToString();
        if (kind == "") kind = null;
        if (kind is not null && kind is not ("ISO6346" or "Synthetic")) return false;
        equipment = args["equipmentType"].ToString();
        if (equipment == "") equipment = null;
        if (equipment is not null && equipment is not
            ("Dry20" or "Dry40" or "HighCube40" or "Reefer20" or "Reefer40" or "Other"))
            return false;
        return true;
    }
    private static bool ParseNullable(string? input, out Guid? result)
    {
        result = null;
        if (string.IsNullOrEmpty(input)) return true;
        if (!Guid.TryParse(input, out var id) || id == Guid.Empty) return false;
        result = id;
        return true;
    }
    private static async Task<bool> ValidMutation(HttpContext ctx, IAntiforgery anti) =>
        AuthenticationSetup.IsAllowedTransport(ctx, ctx.RequestServices.GetRequiredService<IConfiguration>()) &&
        await AuthenticationSetup.ValidMutationAsync(ctx, anti);

    private static async Task<MaritimeInput?> Body(HttpContext ctx, bool voyage, string action)
    {
        var type = ctx.Request.ContentType ?? "";
        if (!type.Equals("application/json", StringComparison.OrdinalIgnoreCase) &&
            !type.StartsWith("application/json;", StringComparison.OrdinalIgnoreCase)) return null;
        const int maximum = 8192;
        if (ctx.Request.ContentLength > maximum) return null;
        var data = new byte[maximum + 1];
        var length = 0;
        try
        {
            int read;
            while (length < data.Length &&
                (read = await ctx.Request.Body.ReadAsync(data.AsMemory(length), ctx.RequestAborted)) != 0)
                length += read;
            if (length == 0 || length > maximum) return null;
            using var json = JsonDocument.Parse(data.AsMemory(0, length), new JsonDocumentOptions
            {
                MaxDepth = 16, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow
            });
            return MaritimeInput.Parse(json.RootElement, voyage, action);
        }
        catch (JsonException) { return null; }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
    }
    public static void MapMaritimeEndpoints(this WebApplication app)
    {
        var root = app.MapGroup("/api/v1/organizations/{organizationId}").RequireAuthorization();
        root.MapGet("/voyages", async (string organizationId, HttpContext ctx, MaritimeStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!Id(organizationId, out var org) ||
                !ListFilters(ctx, true, out var q, out var status, out var after, out var limit,
                    out var vessel, out var origin, out var destination, out var progress, out _, out _))
                return Error(ctx, OrganizationAccessError.Invalid);
            var response = await store.ListVoyagesAsync(Session(ctx), org, q, status, after, limit,
                vessel, origin, destination, progress, ct);
            return response.Value is null ? Error(ctx, response.Error) :
                Results.Ok(new { voyages = response.Value.Items, nextAfter = response.Value.NextAfter });
        });
        root.MapGet("/voyages/{voyageId}", async (string organizationId, string voyageId,
            HttpContext ctx, MaritimeStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!Id(organizationId, out var org) || !Id(voyageId, out var id))
                return Error(ctx, OrganizationAccessError.Invalid);
            var response = await store.GetVoyageAsync(Session(ctx), org, id, ct);
            return response.Value is null ? Error(ctx, response.Error) : Results.Ok(response.Value);
        });
        root.MapPost("/voyages", async (string organizationId, HttpContext ctx, IAntiforgery anti,
            MaritimeStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti)) return Fail(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!Id(organizationId, out var org)) return Error(ctx, OrganizationAccessError.Invalid);
            var input = await Body(ctx, true, "create");
            if (input is null) return Error(ctx, OrganizationAccessError.Invalid);
            var response = await store.WriteVoyageAsync(Session(ctx), org, null, "create", input, ct);
            return response.Value is null ? Error(ctx, response.Error) :
                Results.Created($"{ctx.Request.Path}/{response.Value.Id}", response.Value);
        });
        root.MapPatch("/voyages/{voyageId}", async (string organizationId, string voyageId,
            HttpContext ctx, IAntiforgery anti, MaritimeStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti)) return Fail(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!Id(organizationId, out var org) || !Id(voyageId, out var id))
                return Error(ctx, OrganizationAccessError.Invalid);
            var input = await Body(ctx, true, "update");
            if (input is null) return Error(ctx, OrganizationAccessError.Invalid);
            var response = await store.WriteVoyageAsync(Session(ctx), org, id, "update", input, ct);
            return response.Value is null ? Error(ctx, response.Error) : Results.Ok(response.Value);
        });
        root.MapDelete("/voyages/{voyageId}", async (string organizationId, string voyageId,
            HttpContext ctx, IAntiforgery anti, MaritimeStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti)) return Fail(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!Id(organizationId, out var org) || !Id(voyageId, out var id))
                return Error(ctx, OrganizationAccessError.Invalid);
            var input = await Body(ctx, true, "delete");
            if (input is null) return Error(ctx, OrganizationAccessError.Invalid);
            var response = await store.WriteVoyageAsync(Session(ctx), org, id, "delete", input, ct);
            return response.Value is null ? Error(ctx, response.Error) : Results.NoContent();
        });
        root.MapGet("/containers", async (string organizationId, HttpContext ctx, MaritimeStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!Id(organizationId, out var org) ||
                !ListFilters(ctx, false, out var q, out var status, out var after, out var limit,
                    out _, out _, out _, out _, out var kind, out var equipment))
                return Error(ctx, OrganizationAccessError.Invalid);
            var response = await store.ListContainersAsync(Session(ctx), org, q, status, after, limit,
                kind, equipment, ct);
            return response.Value is null ? Error(ctx, response.Error) :
                Results.Ok(new { containers = response.Value.Items, nextAfter = response.Value.NextAfter });
        });
        root.MapGet("/containers/{containerId}", async (string organizationId, string containerId,
            HttpContext ctx, MaritimeStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!Id(organizationId, out var org) || !Id(containerId, out var id))
                return Error(ctx, OrganizationAccessError.Invalid);
            var response = await store.GetContainerAsync(Session(ctx), org, id, ct);
            return response.Value is null ? Error(ctx, response.Error) : Results.Ok(response.Value);
        });
        root.MapPost("/containers", async (string organizationId, HttpContext ctx, IAntiforgery anti,
            MaritimeStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti)) return Fail(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!Id(organizationId, out var org)) return Error(ctx, OrganizationAccessError.Invalid);
            var input = await Body(ctx, false, "create");
            if (input is null) return Error(ctx, OrganizationAccessError.Invalid);
            var response = await store.WriteContainerAsync(Session(ctx), org, null, "create", input, ct);
            return response.Value is null ? Error(ctx, response.Error) :
                Results.Created($"{ctx.Request.Path}/{response.Value.Id}", response.Value);
        });
        root.MapPatch("/containers/{containerId}", async (string organizationId, string containerId,
            HttpContext ctx, IAntiforgery anti, MaritimeStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti)) return Fail(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!Id(organizationId, out var org) || !Id(containerId, out var id))
                return Error(ctx, OrganizationAccessError.Invalid);
            var input = await Body(ctx, false, "update");
            if (input is null) return Error(ctx, OrganizationAccessError.Invalid);
            var response = await store.WriteContainerAsync(Session(ctx), org, id, "update", input, ct);
            return response.Value is null ? Error(ctx, response.Error) : Results.Ok(response.Value);
        });
        root.MapDelete("/containers/{containerId}", async (string organizationId, string containerId,
            HttpContext ctx, IAntiforgery anti, MaritimeStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti)) return Fail(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!Id(organizationId, out var org) || !Id(containerId, out var id))
                return Error(ctx, OrganizationAccessError.Invalid);
            var input = await Body(ctx, false, "delete");
            if (input is null) return Error(ctx, OrganizationAccessError.Invalid);
            var response = await store.WriteContainerAsync(Session(ctx), org, id, "delete", input, ct);
            return response.Value is null ? Error(ctx, response.Error) : Results.NoContent();
        });
    }
}
