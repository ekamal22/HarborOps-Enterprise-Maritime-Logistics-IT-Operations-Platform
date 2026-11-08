using System.Text.Json;
using HarborOps.Api.Authentication;
using Microsoft.AspNetCore.DataProtection;
using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure.Exceptions;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;

namespace HarborOps.Api.Exceptions;

public static class ShipmentExceptionEndpoints
{
    private sealed record ExceptionCursor(Guid OrganizationId, Guid? ShipmentId,
        string? State, Guid Last, DateTimeOffset SnapshotAtUtc);
    private sealed record HistoryCursor(Guid OrganizationId, Guid ExceptionId,
        DateTimeOffset LastAtUtc, Guid LastId);

    private static ITimeLimitedDataProtector CursorProtector(IDataProtectionProvider provider) =>
        provider.CreateProtector("HarborOps.M11.ExceptionCursor.v2")
            .ToTimeLimitedDataProtector();

    private static ITimeLimitedDataProtector HistoryProtector(
        IDataProtectionProvider provider) =>
        provider.CreateProtector("HarborOps.M11.HistoryCursor.v1")
            .ToTimeLimitedDataProtector();

    private static bool TryHistoryCursor(HttpContext ctx,
        IDataProtectionProvider provider, Guid org, Guid exception,
        out DateTimeOffset? afterUtc, out Guid? afterId)
    {
        afterUtc = null;
        afterId = null;
        if (!ctx.Request.Query.TryGetValue("cursor", out var token)) return true;
        if (token.Count != 1 || token.ToString().Length is < 1 or > 2048) return false;
        try
        {
            var parsed = JsonSerializer.Deserialize<HistoryCursor>(
                HistoryProtector(provider).Unprotect(token.ToString()));
            if (parsed is null || parsed.OrganizationId != org ||
                parsed.ExceptionId != exception || parsed.LastId == Guid.Empty ||
                parsed.LastAtUtc == default ||
                parsed.LastAtUtc.Offset != TimeSpan.Zero) return false;
            afterUtc = parsed.LastAtUtc;
            afterId = parsed.LastId;
            return true;
        }
        catch (System.Security.Cryptography.CryptographicException) { return false; }
        catch (JsonException) { return false; }
        catch (FormatException) { return false; }
    }

    private static string? ProtectHistoryNext(IDataProtectionProvider provider,
        Guid org, Guid exception, DateTimeOffset? afterUtc, Guid? afterId) =>
        afterUtc.HasValue && afterId.HasValue
            ? HistoryProtector(provider).Protect(
                JsonSerializer.Serialize(new HistoryCursor(
                    org, exception, afterUtc.Value, afterId.Value)),
                TimeSpan.FromMinutes(20)) : null;

    private static bool TryCursor(HttpContext ctx, IDataProtectionProvider provider,
        Guid org, Guid? shipment, string? state, out Guid? after,
        out DateTimeOffset? snapshot)
    {
        after = null;
        snapshot = null;
        if (!ctx.Request.Query.TryGetValue("cursor", out var token)) return true;
        if (token.Count != 1 || token.ToString().Length is < 1 or > 2048) return false;
        try
        {
            var data = JsonSerializer.Deserialize<ExceptionCursor>(
                CursorProtector(provider).Unprotect(token.ToString()));
            if (data is null || data.Last == Guid.Empty || data.OrganizationId != org ||
                data.ShipmentId != shipment || data.State != state ||
                data.SnapshotAtUtc == default ||
                data.SnapshotAtUtc.Offset != TimeSpan.Zero) return false;
            after = data.Last;
            snapshot = data.SnapshotAtUtc;
            return true;
        }
        catch (System.Security.Cryptography.CryptographicException) { return false; }
        catch (JsonException) { return false; }
        catch (FormatException) { return false; }
    }

    private static string? ProtectNext(IDataProtectionProvider provider,
        Guid org, Guid? shipment, string? state, Guid? after,
        DateTimeOffset snapshot) =>
        after.HasValue ? CursorProtector(provider).Protect(
            JsonSerializer.Serialize(new ExceptionCursor(
                org, shipment, state, after.Value, snapshot)),
            TimeSpan.FromMinutes(20)) : null;

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
        ctx.Items["M11AuthorizedRole"] = result.Value.Role;
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
            IDataProtectionProvider protection, CancellationToken ct) =>
        {
            if (!Id(organizationId, out var org) ||
                !Id(shipmentId, out var shipment) ||
                !SingleQuery(ctx, "cursor", "limit") ||
                !TryPage(ctx, out var limit))
                return Fail(ctx, 400, "invalid_request");
            var denied = await Guard(ctx, organizations, org, false, ct);
            if (denied is not null) return denied;
            if (!await store.ShipmentExistsAsync(org, shipment, ct))
                return Fail(ctx, 404, "resource_not_found");
            if (!TryCursor(ctx, protection, org, shipment, null,
                    out var after, out var snapshot))
                return Fail(ctx, 409, "stale_or_invalid_cursor");
            var authenticated = Session(ctx);
            var page = await store.ListAsync(org, authenticated.UserId,
                authenticated.SessionId, shipment, null, after, snapshot, limit, ct);
            return Results.Ok(new { items = page.Items,
                nextCursor = ProtectNext(protection, org, shipment, null,
                    page.NextAfter, page.SnapshotAtUtc) });
        }).RequireRateLimiting("audit-read");

        routes.MapGet("/exceptions", async
            (string organizationId, HttpContext ctx,
            OrganizationStore organizations, ShipmentExceptionStore store,
            IDataProtectionProvider protection, CancellationToken ct) =>
        {
            if (!Id(organizationId, out var org) ||
                !SingleQuery(ctx, "shipmentId", "state", "cursor", "limit") ||
                !TryPage(ctx, out var limit))
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
            var normalizedState = state.Length == 0 ? null : state;
            if (!TryCursor(ctx, protection, org, shipment, normalizedState,
                    out var after, out var snapshot))
                return Fail(ctx, 409, "stale_or_invalid_cursor");
            var authenticated = Session(ctx);
            var page = await store.ListAsync(org, authenticated.UserId,
                authenticated.SessionId, shipment, normalizedState,
                after, snapshot, limit, ct);
            return Results.Ok(new { items = page.Items,
                nextCursor = ProtectNext(protection, org, shipment,
                    normalizedState, page.NextAfter, page.SnapshotAtUtc) });
        }).RequireRateLimiting("audit-read");

        routes.MapGet("/exceptions/{exceptionId}", async
            (string organizationId, string exceptionId, HttpContext ctx,
            OrganizationStore organizations, ShipmentExceptionStore store,
            IDataProtectionProvider protection, CancellationToken ct) =>
        {
            if (!Id(organizationId, out var org) ||
                !Id(exceptionId, out var id) || ctx.Request.Query.Count != 0)
                return Fail(ctx, 400, "invalid_request");
            var denied = await Guard(ctx, organizations, org, false, ct);
            if (denied is not null) return denied;
            var authenticated = Session(ctx);
            var result = await store.DetailAsync(org, authenticated.UserId,
                authenticated.SessionId, id, ct);
            if (result is null) return Fail(ctx, 404, "resource_not_found");
            // Analysts can inspect the operational exception lifecycle without
            // receiving free-form Manager reasons or actor identifiers.
            if (ctx.Items["M11AuthorizedRole"] is OrganizationRole.Analyst)
                result = result with
                {
                    History = result.History.Select(item => item with
                    {
                        Reason = null,
                        ActorUserId = null
                    }).ToArray()
                };
            var last = result.HistoryTruncated ? result.History.LastOrDefault() : null;
            return Results.Ok(new
            {
                result.Exception, result.History, result.HistoryTruncated,
                nextHistoryCursor = ProtectHistoryNext(protection, org, id,
                    last?.OccurredAtUtc, last?.Id)
            });
        }).RequireRateLimiting("audit-read");

        routes.MapGet("/exceptions/{exceptionId}/history", async
            (string organizationId, string exceptionId, HttpContext ctx,
            OrganizationStore organizations, ShipmentExceptionStore store,
            IDataProtectionProvider protection, CancellationToken ct) =>
        {
            if (!Id(organizationId, out var org) ||
                !Id(exceptionId, out var exception) ||
                !SingleQuery(ctx, "cursor", "limit") ||
                !TryPage(ctx, out var limit))
                return Fail(ctx, 400, "invalid_request");
            var denied = await Guard(ctx, organizations, org, false, ct);
            if (denied is not null) return denied;
            if (!TryHistoryCursor(ctx, protection, org, exception,
                    out var afterUtc, out var afterId))
                return Fail(ctx, 409, "stale_or_invalid_cursor");
            var authenticated = Session(ctx);
            var page = await store.HistoryPageAsync(
                org, authenticated.UserId, authenticated.SessionId,
                exception, afterUtc, afterId, limit, ct);
            if (page is null) return Fail(ctx, 404, "resource_not_found");
            var visible = ctx.Items["M11AuthorizedRole"] is OrganizationRole.Analyst
                ? page.Items.Select(item => item with
                {
                    Reason = null,
                    ActorUserId = null
                }).ToArray()
                : page.Items;
            return Results.Ok(new
            {
                items = visible,
                nextCursor = ProtectHistoryNext(protection, org, exception,
                    page.NextAfterAtUtc, page.NextAfterId)
            });
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
            // A chunked body can have no Content-Length. The contract still
            // requires an empty request body, so inspect one bounded byte.
            var marker = new byte[1];
            if (await ctx.Request.Body.ReadAsync(marker.AsMemory(), ct) != 0)
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

    private static bool TryPage(HttpContext ctx, out int limit)
    {
        limit = 25;
        return !ctx.Request.Query.ContainsKey("limit") ||
            (int.TryParse(ctx.Request.Query["limit"], out limit) && limit is >= 1 and <= 100);
    }
}
