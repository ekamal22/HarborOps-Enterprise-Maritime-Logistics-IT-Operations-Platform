using System.Text.Json;
using HarborOps.Api.Authentication;
using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure.Exceptions;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;

namespace HarborOps.Api.Exceptions;

public static class ShipmentExceptionEndpoints
{
    private static CurrentSession Session(HttpContext ctx) =>
        (CurrentSession)ctx.Items["IdentitySession"]!;

    private static bool Id(string? value, out Guid id) =>
        Guid.TryParse(value, out id) && id != Guid.Empty;

    private static IResult Fail(HttpContext ctx, int status, string code) =>
        Results.Json(new { code, message = "Request could not be completed",
            correlationId = ctx.Items["CorrelationId"] }, statusCode: status);

    private static IResult Access(HttpContext ctx, OrganizationAccessError error) => error switch
    {
        OrganizationAccessError.SelectionRequired => Fail(ctx, 409, "organization_selection_required"),
        OrganizationAccessError.Revoked => Fail(ctx, 403, "organization_access_revoked"),
        OrganizationAccessError.NotFound => Fail(ctx, 404, "resource_not_found"),
        OrganizationAccessError.Forbidden => Fail(ctx, 403, "access_denied"),
        _ => Fail(ctx, 400, "invalid_request")
    };

    private static IResult StoreError(HttpContext ctx, M11StoreResult result) => result switch
    {
        M11StoreResult.NotFound => Fail(ctx, 404, "resource_not_found"),
        M11StoreResult.Forbidden => Fail(ctx, 403, "access_denied"),
        M11StoreResult.Invalid => Fail(ctx, 422, "invalid_exception_transition"),
        M11StoreResult.Conflict => Fail(ctx, 409, "exception_conflict"),
        M11StoreResult.AuditUnavailable => Fail(ctx, 503, "audit_unavailable"),
        M11StoreResult.DependencyUnavailable => Fail(ctx, 503, "exception_authority_unavailable"),
        _ => Fail(ctx, 409, "exception_conflict")
    };

    private static async Task<IResult?> Guard(HttpContext ctx, OrganizationStore organizations,
        Guid org, bool managerOnly, CancellationToken ct)
    {
        var session = Session(ctx);
        var result = await organizations.ResolveAsync(session.UserId, session.SessionId,
            org, OrganizationPermission.ViewShipmentData, ct);
        if (result.Value is null) return Access(ctx, result.Error);
        if (managerOnly && result.Value.Role != OrganizationRole.Manager)
            return Fail(ctx, 403, "access_denied");
        return null;
    }

    private static async Task<bool> Mutation(HttpContext ctx, WebApplication app,
        IAntiforgery antiforgery) =>
        AuthenticationSetup.IsAllowedTransport(ctx, app.Configuration) &&
        await AuthenticationSetup.ValidMutationAsync(ctx, antiforgery);

    private static bool SingleQuery(HttpContext ctx, params string[] names) =>
        ctx.Request.Query.All(entry =>
            names.Contains(entry.Key, StringComparer.Ordinal) && entry.Value.Count == 1);

    private static async Task<(string Action, long ExpectedVersion, string Reason)?>
        ReadActionAsync(HttpContext ctx)
    {
        if (ctx.Request.ContentLength is > 1024) return null;
        using var buffer = new MemoryStream();
        var bytes = new byte[512];
        int read;
        while ((read = await ctx.Request.Body.ReadAsync(bytes.AsMemory(),
            ctx.RequestAborted)) > 0)
        {
            if (buffer.Length + read > 1024) return null;
            buffer.Write(bytes, 0, read);
        }
        try
        {
            using var doc = JsonDocument.Parse(buffer.ToArray());
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in root.EnumerateObject())
                if (!seen.Add(field.Name) || field.Name is not
                    ("action" or "expectedVersion" or "reason")) return null;
            if (seen.Count != 3 ||
                root.GetProperty("action").ValueKind != JsonValueKind.String ||
                root.GetProperty("reason").ValueKind != JsonValueKind.String ||
                root.GetProperty("expectedVersion").ValueKind != JsonValueKind.Number ||
                !root.GetProperty("expectedVersion").TryGetInt64(out var version) ||
                version < 1) return null;
            var action = root.GetProperty("action").GetString() ?? "";
            var reason = root.GetProperty("reason").GetString() ?? "";
            if (action is not ("Acknowledge" or "Resolve") ||
                reason.Length is < 8 or > 240 ||
                reason != reason.Trim() || reason.Any(char.IsControl))
                return null;
            return (action, version, reason);
        }
        catch (JsonException) { return null; }
    }

    public static void MapShipmentExceptionEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/v1/organizations/{organizationId}")
            .RequireAuthorization();

        routes.MapGet("/shipments/{shipmentId}/exceptions", async
            (string organizationId, string shipmentId, HttpContext ctx,
            OrganizationStore organizations, ShipmentExceptionStore store,
            CancellationToken ct) =>
        {
            if (!Id(organizationId, out var org) ||
                !Id(shipmentId, out var shipment) ||
                !SingleQuery(ctx, "after", "limit") ||
                !TryPage(ctx, out var after, out var limit))
                return Fail(ctx, 400, "invalid_request");
            var denied = await Guard(ctx, organizations, org, false, ct);
            if (denied is not null) return denied;
            if (!await store.ShipmentExistsAsync(org, shipment, ct))
                return Fail(ctx, 404, "resource_not_found");
            return Results.Ok(await store.ListAsync(org, shipment, null, after, limit, ct));
        }).RequireRateLimiting("audit-read");

        routes.MapGet("/exceptions", async
            (string organizationId, HttpContext ctx,
            OrganizationStore organizations, ShipmentExceptionStore store,
            CancellationToken ct) =>
        {
            if (!Id(organizationId, out var org) ||
                !SingleQuery(ctx, "shipmentId", "state", "after", "limit") ||
                !TryPage(ctx, out var after, out var limit))
                return Fail(ctx, 400, "invalid_request");
            Guid? shipment = null;
            if (ctx.Request.Query.TryGetValue("shipmentId", out var shipmentValue))
            {
                if (!Id(shipmentValue.ToString(), out var ship))
                    return Fail(ctx, 400, "invalid_request");
                shipment = ship;
            }
            var state = ctx.Request.Query["state"].ToString();
            if (state.Length > 0 && state is not ("Open" or "Acknowledged" or "Resolved"))
                return Fail(ctx, 400, "invalid_request");
            var denied = await Guard(ctx, organizations, org, false, ct);
            if (denied is not null) return denied;
            return Results.Ok(await store.ListAsync(org, shipment,
                state.Length == 0 ? null : state, after, limit, ct));
        }).RequireRateLimiting("audit-read");

        routes.MapGet("/exceptions/{exceptionId}", async
            (string organizationId, string exceptionId, HttpContext ctx,
            OrganizationStore organizations, ShipmentExceptionStore store,
            CancellationToken ct) =>
        {
            if (!Id(organizationId, out var org) ||
                !Id(exceptionId, out var id) || ctx.Request.Query.Count != 0)
                return Fail(ctx, 400, "invalid_request");
            var denied = await Guard(ctx, organizations, org, false, ct);
            if (denied is not null) return denied;
            var result = await store.DetailAsync(org, id, ct);
            return result is null ? Fail(ctx, 404, "resource_not_found")
                : Results.Ok(result);
        }).RequireRateLimiting("audit-read");

        routes.MapPost("/shipments/{shipmentId}/exceptions/evaluate", async
            (string organizationId, string shipmentId, HttpContext ctx,
            OrganizationStore organizations, ShipmentExceptionStore store,
            IAntiforgery antiforgery, CancellationToken ct) =>
        {
            if (!Id(organizationId, out var org) ||
                !Id(shipmentId, out var shipment) ||
                ctx.Request.Query.Count != 0 || ctx.Request.ContentLength is > 0)
                return Fail(ctx, 400, "invalid_request");
            if (!await Mutation(ctx, app, antiforgery))
                return Fail(ctx, 403, "csrf_invalid");
            var denied = await Guard(ctx, organizations, org, true, ct);
            if (denied is not null) return denied;
            var result = await store.EvaluateAsync(org, shipment, ct);
            return result.Result != M11StoreResult.Applied
                ? StoreError(ctx, result.Result)
                : Results.Ok(new { rulesEvaluated = result.Rules,
                    rulePolicyVersion = HarborOps.Domain.Exceptions.ShipmentExceptionPolicy.Version });
        }).RequireRateLimiting("m10-mutations");

        routes.MapPatch("/exceptions/{exceptionId}", async
            (string organizationId, string exceptionId, HttpContext ctx,
            OrganizationStore organizations, ShipmentExceptionStore store,
            IAntiforgery antiforgery, CancellationToken ct) =>
        {
            if (!Id(organizationId, out var org) ||
                !Id(exceptionId, out var exception) || ctx.Request.Query.Count != 0 ||
                ctx.Request.Headers["Idempotency-Key"].Count != 1 ||
                !Id(ctx.Request.Headers["Idempotency-Key"].ToString(), out var key))
                return Fail(ctx, 400, "invalid_request");
            if (!await Mutation(ctx, app, antiforgery))
                return Fail(ctx, 403, "csrf_invalid");
            var denied = await Guard(ctx, organizations, org, true, ct);
            if (denied is not null) return denied;
            var input = await ReadActionAsync(ctx);
            if (input is null) return Fail(ctx, 400, "invalid_request");
            var session = Session(ctx);
            var result = await store.ActionAsync(org, exception,
                session.UserId, session.SessionId, input.Value.Action,
                input.Value.ExpectedVersion, input.Value.Reason, key, ct);
            return result.Result is M11StoreResult.Applied or M11StoreResult.Replayed
                ? Results.Ok(new { result = result.Result.ToString(),
                    version = result.Version })
                : StoreError(ctx, result.Result);
        }).RequireRateLimiting("m10-mutations");
    }

    private static bool TryPage(HttpContext ctx, out Guid? after, out int limit)
    {
        after = null;
        limit = 25;
        if (ctx.Request.Query.TryGetValue("after", out var cursor))
        {
            if (!Id(cursor.ToString(), out var parsed)) return false;
            after = parsed;
        }
        return !ctx.Request.Query.ContainsKey("limit") ||
            (int.TryParse(ctx.Request.Query["limit"], out limit) && limit is >= 1 and <= 100);
    }
}
