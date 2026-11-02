using System.Text.Json;
using HarborOps.Api.Authentication;
using HarborOps.Infrastructure.Assignments;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Routing;

namespace HarborOps.Api.Assignments;

public static class AssignmentEndpoints
{
    private static CurrentSession Session(HttpContext ctx) =>
        (CurrentSession)ctx.Items["IdentitySession"]!;

    private static IResult Fail(HttpContext ctx, int status, string code, string message) =>
        Results.Json(new { code, message, correlationId = ctx.Items["CorrelationId"] },
            statusCode: status);

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

    private static bool Id(string? value, out Guid id) =>
        Guid.TryParse(value, out id) && id != Guid.Empty;

    private static bool Page(HttpContext ctx, out Guid? after, out int limit)
    {
        after = null; limit = 25;
        var q = ctx.Request.Query;
        if (q.Count > 2 || q.Keys.Any(k => k is not ("limit" or "after")) ||
            q.Any(k => k.Value.Count != 1)) return false;
        if (q.ContainsKey("limit") &&
            (!int.TryParse(q["limit"], out limit) || limit is < 1 or > 100))
            return false;
        if (q.ContainsKey("after"))
        {
            if (!Id(q["after"], out var id)) return false;
            after = id;
        }
        return true;
    }

    private static async Task<bool> ValidMutation(HttpContext ctx, IAntiforgery antiforgery) =>
        AuthenticationSetup.IsAllowedTransport(ctx,
            ctx.RequestServices.GetRequiredService<IConfiguration>()) &&
        await AuthenticationSetup.ValidMutationAsync(ctx, antiforgery);

    private static async Task<AssignmentInput?> Body(HttpContext ctx, bool voyage, bool close)
    {
        var contentType = ctx.Request.ContentType ?? "";
        if (!contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase) &&
            !contentType.StartsWith("application/json;", StringComparison.OrdinalIgnoreCase))
            return null;
        const int max = 8192;
        if (ctx.Request.ContentLength > max) return null;
        var data = new byte[max + 1];
        var length = 0;
        try
        {
            int read;
            while (length < data.Length &&
                (read = await ctx.Request.Body.ReadAsync(data.AsMemory(length), ctx.RequestAborted)) != 0)
                length += read;
            if (length is 0 or > max) return null;
            using var json = JsonDocument.Parse(data.AsMemory(0, length),
                new JsonDocumentOptions { MaxDepth = 16, AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow });
            return AssignmentInput.Parse(json.RootElement, voyage, close);
        }
        catch (JsonException) { return null; }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    private static void MapOne<T>(RouteGroupBuilder group, string name, bool voyage)
        where T : AssignmentRecord, new()
    {
        group.MapGet("/" + name, async (string organizationId, string shipmentId,
            HttpContext ctx, AssignmentStore store, CancellationToken ct) =>
        {
            if (!Id(organizationId, out var org) || !Id(shipmentId, out var shipment) ||
                !Page(ctx, out var after, out var limit))
                return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.ListAsync<T>(Session(ctx), org, shipment, after, limit, ct);
            return result.Value is null ? Error(ctx, result.Error) :
                Results.Ok(new { assignments = result.Value.Assignments, nextAfter = result.Value.NextAfter });
        });
        group.MapGet("/" + name + "/{assignmentId}", async (string organizationId, string shipmentId,
            string assignmentId, HttpContext ctx, AssignmentStore store, CancellationToken ct) =>
        {
            if (!Id(organizationId, out var org) || !Id(shipmentId, out var shipment) ||
                !Id(assignmentId, out var assignment))
                return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.GetAsync<T>(Session(ctx), org, shipment, assignment, ct);
            return result.Value is null ? Error(ctx, result.Error) : Results.Ok(result.Value);
        });
        group.MapPost("/" + name, async (string organizationId, string shipmentId,
            HttpContext ctx, IAntiforgery antiforgery, AssignmentStore store, CancellationToken ct) =>
        {
            if (!await ValidMutation(ctx, antiforgery))
                return Fail(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!Id(organizationId, out var org) || !Id(shipmentId, out var shipment))
                return Error(ctx, OrganizationAccessError.Invalid);
            var input = await Body(ctx, voyage, false);
            if (input is null) return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.WriteAsync<T>(Session(ctx), org, shipment, null, input, ct);
            return result.Value is null ? Error(ctx, result.Error) :
                Results.Created(ctx.Request.Path + "/" + result.Value.Id, result.Value);
        });
        group.MapPatch("/" + name + "/{assignmentId}", async (string organizationId, string shipmentId,
            string assignmentId, HttpContext ctx, IAntiforgery antiforgery, AssignmentStore store,
            CancellationToken ct) =>
        {
            if (!await ValidMutation(ctx, antiforgery))
                return Fail(ctx, 403, "csrf_invalid", "Request validation failed");
            if (!Id(organizationId, out var org) || !Id(shipmentId, out var shipment) ||
                !Id(assignmentId, out var assignment))
                return Error(ctx, OrganizationAccessError.Invalid);
            var input = await Body(ctx, voyage, true);
            if (input is null) return Error(ctx, OrganizationAccessError.Invalid);
            var result = await store.WriteAsync<T>(Session(ctx), org, shipment, assignment, input, ct);
            return result.Value is null ? Error(ctx, result.Error) : Results.Ok(result.Value);
        });
    }

    public static void MapAssignmentEndpoints(this WebApplication app)
    {
        var root = app.MapGroup("/api/v1/organizations/{organizationId}/shipments/{shipmentId}")
            .RequireAuthorization();
        MapOne<ShipmentContainerAssignment>(root, "container-assignments", false);
        MapOne<ShipmentVoyageAssignment>(root, "voyage-assignments", true);
    }
}
