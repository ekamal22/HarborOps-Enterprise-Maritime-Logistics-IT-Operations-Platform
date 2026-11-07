using System.Security.Cryptography;
using System.Text.Json;
using HarborOps.Api.Authentication;
using HarborOps.Domain.Organizations;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Shipments;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;

namespace HarborOps.Api.Shipments;

public static class ShipmentTimelineEndpoints
{
    private sealed record Snapshot(Guid OrganizationId, Guid ShipmentId, string Digest);
    private sealed record Cursor(Guid OrganizationId, Guid ShipmentId, string Digest,
        ShipmentTimelineKey Last);

    private static bool Id(string? raw, out Guid id) =>
        Guid.TryParse(raw, out id) && id != Guid.Empty;
    private static CurrentSession Current(HttpContext ctx) =>
        (CurrentSession)ctx.Items["IdentitySession"]!;
    private static IResult Fail(HttpContext ctx, int status, string code) =>
        Results.Json(new { code, message = "Request could not be completed",
            correlationId = ctx.Items["CorrelationId"] }, statusCode: status);
    private static IResult Denied(HttpContext ctx, OrganizationAccessError error) => error switch
    {
        OrganizationAccessError.SelectionRequired => Fail(ctx, 409, "organization_selection_required"),
        OrganizationAccessError.Revoked => Fail(ctx, 403, "organization_access_revoked"),
        OrganizationAccessError.Forbidden => Fail(ctx, 403, "access_denied"),
        OrganizationAccessError.NotFound => Fail(ctx, 404, "resource_not_found"),
        _ => Fail(ctx, 400, "invalid_request")
    };
    private static async Task<IResult?> Guard(HttpContext ctx, OrganizationStore organizations,
        Guid org, OrganizationPermission permission, CancellationToken ct,
        bool managerOnly = false)
    {
        var session = Current(ctx);
        var access = await organizations.ResolveAsync(session.UserId, session.SessionId,
            org, permission, ct);
        if (access.Value is null) return Denied(ctx, access.Error);
        return managerOnly && access.Value.Role != OrganizationRole.Manager
            ? Denied(ctx, OrganizationAccessError.Forbidden) : null;
    }
    private static async Task<bool> Mutation(HttpContext ctx, WebApplication app,
        IAntiforgery antiforgery) =>
        AuthenticationSetup.IsAllowedTransport(ctx, app.Configuration) &&
        await AuthenticationSetup.ValidMutationAsync(ctx, antiforgery);

    private static bool TryKey(HttpContext ctx, out Guid key)
    {
        var raw = ctx.Request.Headers["Idempotency-Key"];
        key = Guid.Empty;
        return raw.Count == 1 && Id(raw.ToString(), out key);
    }

    private static ITimeLimitedDataProtector Protector(IDataProtectionProvider provider) =>
        provider.CreateProtector("HarborOps.M10.SnapshotCursor.v2").ToTimeLimitedDataProtector();

    private static string Protect<T>(ITimeLimitedDataProtector protector, T payload) =>
        protector.Protect(JsonSerializer.Serialize(payload), TimeSpan.FromMinutes(20));

    private static T? Unprotect<T>(ITimeLimitedDataProtector protector, string? token)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(protector.Unprotect(token));
        }
        catch (Exception error) when (error is CryptographicException or
            FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    private static async Task<TrackingCorrection?> ReadCorrection(HttpContext ctx)
    {
        var contentType = ctx.Request.ContentType ?? "";
        if (!contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase) &&
            !contentType.StartsWith("application/json;", StringComparison.OrdinalIgnoreCase))
            return null;
        if (ctx.Request.ContentLength > 4096) return null;
        var bytes = new byte[4097];
        var length = 0;
        try
        {
            int received;
            while (length < bytes.Length &&
                (received = await ctx.Request.Body.ReadAsync(
                    bytes.AsMemory(length), ctx.RequestAborted)) > 0)
                length += received;
            if (length is 0 or > 4096) return null;
            using var document = JsonDocument.Parse(bytes.AsMemory(0, length),
                new JsonDocumentOptions { MaxDepth = 6, AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var allowed = new[] { "targetReceiptId", "replacementReceiptId",
                "expectedGeneration", "reason" };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
                if (!allowed.Contains(property.Name, StringComparer.Ordinal) ||
                    !seen.Add(property.Name)) return null;
            if (!root.TryGetProperty("targetReceiptId", out var t) ||
                t.ValueKind != JsonValueKind.String || !Id(t.GetString(), out var target) ||
                !root.TryGetProperty("expectedGeneration", out var g) ||
                g.ValueKind != JsonValueKind.Number ||
                !g.TryGetInt64(out var generation) || generation < 0 ||
                !root.TryGetProperty("reason", out var re) ||
                re.ValueKind != JsonValueKind.String) return null;
            var reason = re.GetString() ?? "";
            if (reason.Length is < 8 or > 240 || reason != reason.Trim() ||
                reason.Any(char.IsControl)) return null;
            Guid? replacement = null;
            if (root.TryGetProperty("replacementReceiptId", out var p) &&
                p.ValueKind != JsonValueKind.Null)
            {
                if (p.ValueKind != JsonValueKind.String ||
                    !Id(p.GetString(), out var id) || id == target) return null;
                replacement = id;
            }
            return new TrackingCorrection(target, replacement, generation, reason);
        }
        catch (JsonException) { return null; }
    }

    public static void MapShipmentTimelineEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/v1/organizations/{organizationId}/shipments/{shipmentId}")
            .RequireAuthorization();

        routes.MapGet("/events", async (string organizationId, string shipmentId,
            HttpContext ctx, OrganizationStore orgs, ShipmentTimelineStore store,
            IDataProtectionProvider protection, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!Id(organizationId, out var org) || !Id(shipmentId, out var shipment) ||
                ctx.Request.Query.Count > 3 ||
                ctx.Request.Query.Keys.Any(k => k is not ("cursor" or "limit" or "snapshot")) ||
                ctx.Request.Query.Any(k => k.Value.Count != 1))
                return Fail(ctx, 400, "invalid_request");
            var limit = 25;
            if (ctx.Request.Query.ContainsKey("limit") &&
                (!int.TryParse(ctx.Request.Query["limit"], out limit) || limit is < 1 or > 100))
                return Fail(ctx, 400, "invalid_page_size");
            var denied = await Guard(ctx, orgs, org, OrganizationPermission.ViewShipmentData, ct);
            if (denied is not null) return denied;

            var protector = Protector(protection);
            Snapshot? snapshot = null;
            Cursor? cursor = null;
            if (ctx.Request.Query.TryGetValue("snapshot", out var snapshotValue))
            {
                snapshot = Unprotect<Snapshot>(protector, snapshotValue.ToString());
                if (snapshot is null || snapshot.OrganizationId != org ||
                    snapshot.ShipmentId != shipment)
                    return Fail(ctx, 409, "stale_or_invalid_cursor");
            }
            if (ctx.Request.Query.TryGetValue("cursor", out var cursorValue))
            {
                cursor = Unprotect<Cursor>(protector, cursorValue.ToString());
                if (cursor is null || cursor.OrganizationId != org ||
                    cursor.ShipmentId != shipment || cursor.Last is null)
                    return Fail(ctx, 409, "stale_or_invalid_cursor");
            }

            var view = await store.ReadBoundedAsync(org, shipment, ct, limit, cursor?.Last);
            if (view is null) return Fail(ctx, 404, "resource_not_found");
            if (snapshot is not null && snapshot.Digest != view.State.InputDigest ||
                cursor is not null && cursor.Digest != view.State.InputDigest)
                return Fail(ctx, 409, "stale_or_invalid_cursor");

            var next = view.HasMore && view.Timeline.Count > 0
                ? Protect(protector, new Cursor(org, shipment, view.State.InputDigest,
                    ShipmentTimelineKey.Of(view.Timeline[^1]))) : null;
            var page = view.Timeline.Select(e => new
            {
                eventId = e.EventId, receiptId = e.ReceiptId,
                containerId = e.ContainerId, sourceId = e.SourceId,
                sourceEventId = e.SourceEventId, eventCode = e.EventCode,
                occurredAtUtc = e.OccurredAtUtc, receivedAtUtc = e.ReceivedAtUtc,
                superseded = e.Superseded, replacedByReceiptId = e.ReplacedByReceiptId
            }).ToArray();
            return Results.Ok(new
            {
                items = page, eventCount = view.State.EventCount, nextCursor = next,
                policyVersion = view.State.PolicyVersion,
                materializationRevision = view.MaterializationRevision,
                correctionGeneration = view.CorrectionGeneration,
                materializationStale = view.MaterializationStale
            });
        });

        routes.MapGet("/tracking-projection", async (string organizationId, string shipmentId,
            HttpContext ctx, OrganizationStore orgs, ShipmentTimelineStore store,
            IDataProtectionProvider protection, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!Id(organizationId, out var org) || !Id(shipmentId, out var shipment) ||
                ctx.Request.Query.Count != 0) return Fail(ctx, 400, "invalid_request");
            var denied = await Guard(ctx, orgs, org, OrganizationPermission.ViewShipmentData, ct);
            if (denied is not null) return denied;
            var view = await store.ReadBoundedAsync(org, shipment, ct, pageSize: 0);
            if (view is null) return Fail(ctx, 404, "resource_not_found");
            return Results.Ok(new
            {
                observedTrackingState = view.State.ObservedTrackingState,
                containerObservations = view.State.Containers?.Take(50).ToArray(),
                containerCount = view.State.Containers?.Count ?? 0,
                containerObservationsTruncated = (view.State.Containers?.Count ?? 0) > 50,
                etaUtc = (DateTimeOffset?)null, lastEventId = view.State.LastEventId,
                lastOccurredAtUtc = view.State.LastOccurredAtUtc,
                snapshotToken = Protect(Protector(protection),
                    new Snapshot(org, shipment, view.State.InputDigest)),
                policyVersion = view.State.PolicyVersion,
                eventCount = view.State.EventCount,
                materializationRevision = view.MaterializationRevision,
                correctionGeneration = view.CorrectionGeneration,
                materializationStale = view.MaterializationStale,
                materializedAtUtc = view.MaterializedAtUtc
            });
        });

        routes.MapPost("/tracking-projection/rebuild", async (string organizationId,
            string shipmentId, HttpContext ctx, IAntiforgery anti, OrganizationStore orgs,
            ShipmentTimelineStore store, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await Mutation(ctx, app, anti)) return Fail(ctx, 403, "csrf_invalid");
            if (!Id(organizationId, out var org) || !Id(shipmentId, out var shipment) ||
                !TryKey(ctx, out var key) ||
                ctx.Request.Query.Count != 0 || ctx.Request.ContentLength is > 0)
                return Fail(ctx, 400, "invalid_request");
            var denied = await Guard(ctx, orgs, org, OrganizationPermission.ManageShipmentData,
                ct, managerOnly: true);
            if (denied is not null) return denied;
            var (result, revision) = await store.RebuildForSessionAsync(
                org, shipment, Current(ctx).UserId, Current(ctx).SessionId, key, ct);
            return result switch
            {
                TrackingMutationResult.Applied or TrackingMutationResult.Replayed =>
                    Results.Ok(new { materializationRevision = revision,
                        replayed = result == TrackingMutationResult.Replayed,
                        correlationId = ctx.Items["CorrelationId"] }),
                TrackingMutationResult.NotFound => Fail(ctx, 404, "resource_not_found"),
                TrackingMutationResult.Forbidden => Fail(ctx, 403, "access_denied"),
                _ => Fail(ctx, 409, "rebuild_conflict")
            };
        }).RequireRateLimiting("m10-mutations");

        routes.MapPost("/tracking-corrections", async (string organizationId,
            string shipmentId, HttpContext ctx, IAntiforgery anti, OrganizationStore orgs,
            ShipmentTimelineStore store, IConfiguration configuration, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (!await Mutation(ctx, app, anti)) return Fail(ctx, 403, "csrf_invalid");
            if (!Id(organizationId, out var org) || !Id(shipmentId, out var shipment) ||
                !TryKey(ctx, out var key) || ctx.Request.Query.Count != 0)
                return Fail(ctx, 400, "invalid_request");
            var denied = await Guard(ctx, orgs, org, OrganizationPermission.ManageShipmentData,
                ct, managerOnly: true);
            if (denied is not null) return denied;
            var input = await ReadCorrection(ctx);
            if (input is null) return Fail(ctx, 400, "invalid_request");
            var signed = M10CorrectionProofSigner.Sign(
                configuration["M10CorrectionProof:Key"],
                org, shipment, Current(ctx).UserId, Current(ctx).SessionId, key, input);
            if (signed is null)
                return Fail(ctx, 503, "correction_authority_unavailable");
            var (result, revision) = await store.CorrectForSessionAsync(org, shipment,
                input, Current(ctx).UserId, Current(ctx).SessionId,
                ctx.Items["CorrelationId"]?.ToString() ?? "", key, signed, ct);
            return result switch
            {
                TrackingMutationResult.Applied or TrackingMutationResult.Replayed =>
                    Results.Ok(new
                    {
                        correctionGeneration = input.ExpectedGeneration + 1,
                        materializationRevision = revision,
                        replayed = result == TrackingMutationResult.Replayed,
                        correlationId = ctx.Items["CorrelationId"]
                    }),
                TrackingMutationResult.NotFound => Fail(ctx, 404, "resource_not_found"),
                TrackingMutationResult.Invalid => Fail(ctx, 422, "invalid_correction"),
                TrackingMutationResult.AuditUnavailable => Fail(ctx, 503, "dependency_unavailable"),
                TrackingMutationResult.DependencyUnavailable => Fail(ctx, 503, "dependency_unavailable"),
                TrackingMutationResult.Forbidden => Fail(ctx, 403, "access_denied"),
                _ => Fail(ctx, 409, "correction_conflict")
            };
        }).RequireRateLimiting("m10-mutations");
    }
}
