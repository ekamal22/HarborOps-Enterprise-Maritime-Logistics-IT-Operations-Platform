using System.Text.Json;
using HarborOps.Api.Authentication;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Shipments;
using Microsoft.AspNetCore.Antiforgery;

namespace HarborOps.Api.Shipments;

public static class ShipmentEndpoints
{
    private static CurrentSession Session(HttpContext ctx) =>
        (CurrentSession)ctx.Items["IdentitySession"]!;

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
    private static bool Id(string value, out Guid id) =>
        Guid.TryParse(value, out id) && id != Guid.Empty;

    private static bool ListFilters(HttpContext ctx, out string q, out string status,
        out string? workflow, out Guid? after, out int limit)
    {
        var args = ctx.Request.Query;
        var allowed = new[] { "q", "recordStatus", "workflowStatus", "after", "limit" };
        q = args["q"].ToString().Trim();
        status = args["recordStatus"].ToString();
        workflow = args["workflowStatus"].ToString();
        if (workflow == "") workflow = null;
        after = null; limit = 25;
        if (args.Count > allowed.Length || args.Keys.Any(k => !allowed.Contains(k, StringComparer.Ordinal)) ||
            args.Any(k => k.Value.Count != 1) || q.Length > 80 || q.Any(char.IsControl))
            return false;
        if (status == "") status = "Active";
        if (status is not ("Active" or "Archived" or "All")) return false;
        if (workflow is not null && workflow is not
            ("Draft" or "Booked" or "InTransit" or "Arrived" or "Completed" or "Cancelled"))
            return false;
        if (args.ContainsKey("limit") && (!int.TryParse(args["limit"], out limit) ||
            limit is < 1 or > 100)) return false;
        if (args.ContainsKey("after"))
        {
            if (!Id(args["after"].ToString(), out var cursor)) return false;
            after = cursor;
        }
        return true;
    }

    private static async Task<bool> ValidMutation(HttpContext ctx, IAntiforgery anti) =>
        AuthenticationSetup.IsAllowedTransport(ctx,
            ctx.RequestServices.GetRequiredService<IConfiguration>()) &&
        await AuthenticationSetup.ValidMutationAsync(ctx, anti);

    private static async Task<ShipmentInput?> Body(HttpContext ctx, string action)
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
            if (length is 0 or > maximum) return null;
            using var json = JsonDocument.Parse(data.AsMemory(0, length),
                new JsonDocumentOptions { MaxDepth = 16, AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow });
            return ShipmentInput.Parse(json.RootElement, action);
        }
        catch (JsonException) { return null; }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    public static void MapShipmentEndpoints(this WebApplication app)
    {
        var root = app.MapGroup("/api/v1/organizations/{organizationId}").RequireAuthorization();
        root.MapGet("/shipments", async (string organizationId, HttpContext ctx,
            ShipmentStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!Id(organizationId, out var org) ||
                !ListFilters(ctx, out var q, out var status, out var workflow, out var after, out var limit))
                return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.ListAsync(Session(ctx), org, q, status, workflow, after, limit, ct);
            return result.Value is null ? Error(ctx, result.Error) :
                Results.Ok(new { shipments = result.Value.Shipments, nextAfter = result.Value.NextAfter });
        });
        root.MapGet("/shipments/{shipmentId}", async (string organizationId, string shipmentId,
            HttpContext ctx, ShipmentStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!Id(organizationId, out var org) || !Id(shipmentId, out var id))
                return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.GetAsync(Session(ctx), org, id, ct);
            return result.Value is null ? Error(ctx, result.Error) : Results.Ok(result.Value);
        });
        root.MapGet("/shipments/{shipmentId}/status-changes",
            async (string organizationId, string shipmentId, HttpContext ctx,
                ShipmentStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!Id(organizationId, out var org) || !Id(shipmentId, out var id))
                return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.HistoryAsync(Session(ctx), org, id, ct);
            return result.Value is null ? Error(ctx, result.Error) :
                Results.Ok(new { changes = result.Value });
        });
        root.MapPost("/shipments", async (string organizationId, HttpContext ctx,
            IAntiforgery anti, ShipmentStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti))
                return Fail(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!Id(organizationId, out var org))
                return Error(ctx, OrganizationAccessError.Invalid);
            var input = await Body(ctx, "create");
            if (input is null) return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.WriteAsync(Session(ctx), org, null, "create", input, ct);
            return result.Value is null ? Error(ctx, result.Error) :
                Results.Created($"{ctx.Request.Path}/{result.Value.Id}", result.Value);
        });
        root.MapPatch("/shipments/{shipmentId}", async (string organizationId, string shipmentId,
            HttpContext ctx, IAntiforgery anti, ShipmentStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti))
                return Fail(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!Id(organizationId, out var org) || !Id(shipmentId, out var id))
                return Error(ctx, OrganizationAccessError.Invalid);
            var input = await Body(ctx, "update");
            if (input is null) return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.WriteAsync(Session(ctx), org, id, "update", input, ct);
            return result.Value is null ? Error(ctx, result.Error) : Results.Ok(result.Value);
        });
        root.MapDelete("/shipments/{shipmentId}", async (string organizationId, string shipmentId,
            HttpContext ctx, IAntiforgery anti, ShipmentStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await ValidMutation(ctx, anti))
                return Fail(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!Id(organizationId, out var org) || !Id(shipmentId, out var id))
                return Error(ctx, OrganizationAccessError.Invalid);
            var input = await Body(ctx, "delete");
            if (input is null) return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.WriteAsync(Session(ctx), org, id, "delete", input, ct);
            return result.Value is null ? Error(ctx, result.Error) : Results.NoContent();
        });
    }
}
