using System.Text.Json;
using HarborOps.Api.Authentication;
using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Shipments;
using Microsoft.AspNetCore.Antiforgery;

namespace HarborOps.Api.Shipments;

public static class ShipmentTimelineEndpoints
{
    private static bool Id(string? text, out Guid id) =>
        Guid.TryParse(text, out id) && id != Guid.Empty;
    private static CurrentSession Current(HttpContext c) =>
        (CurrentSession)c.Items["IdentitySession"]!;
    private static IResult Fail(HttpContext c, int status, string code) =>
        Results.Json(new { code, message = "Request could not be completed",
            correlationId = c.Items["CorrelationId"] }, statusCode: status);
    private static IResult Denied(HttpContext c, OrganizationAccessError reason) => reason switch
    {
        OrganizationAccessError.SelectionRequired => Fail(c, 409, "organization_selection_required"),
        OrganizationAccessError.Revoked => Fail(c, 403, "organization_access_revoked"),
        OrganizationAccessError.Forbidden => Fail(c, 403, "access_denied"),
        OrganizationAccessError.NotFound => Fail(c, 404, "resource_not_found"),
        _ => Fail(c, 400, "invalid_request")
    };
    private static async Task<IResult?> Guard(HttpContext c, OrganizationStore organizations,
        Guid org, OrganizationPermission permission, CancellationToken ct)
    {
        var session = Current(c);
        var access = await organizations.ResolveAsync(session.UserId, session.SessionId,
            org, permission, ct);
        return access.Value is null ? Denied(c, access.Error) : null;
    }
    private static async Task<bool> Mutation(HttpContext c, WebApplication app,
        IAntiforgery anti) =>
        AuthenticationSetup.IsAllowedTransport(c, app.Configuration) &&
        await AuthenticationSetup.ValidMutationAsync(c, anti);

    private static async Task<TrackingCorrection?> ReadCorrection(HttpContext c)
    {
        var type = c.Request.ContentType ?? "";
        if (!type.Equals("application/json", StringComparison.OrdinalIgnoreCase) &&
            !type.StartsWith("application/json;", StringComparison.OrdinalIgnoreCase))
            return null;
        if (c.Request.ContentLength > 4096) return null;
        var data = new byte[4097]; var length = 0;
        try
        {
            int count;
            while (length < data.Length &&
                (count = await c.Request.Body.ReadAsync(data.AsMemory(length), c.RequestAborted)) > 0)
                length += count;
            if (length is 0 or > 4096) return null;
            using var json = JsonDocument.Parse(data.AsMemory(0, length),
                new JsonDocumentOptions { MaxDepth = 6, AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow });
            var v = json.RootElement;
            if (v.ValueKind != JsonValueKind.Object) return null;
            var allowed = new[] { "targetReceiptId", "replacementReceiptId", "expectedGeneration", "reason" };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in v.EnumerateObject())
                if (!allowed.Contains(property.Name, StringComparer.Ordinal) ||
                    !seen.Add(property.Name)) return null;
            if (!v.TryGetProperty("targetReceiptId", out var t) ||
                t.ValueKind != JsonValueKind.String || !Id(t.GetString(), out var target) ||
                !v.TryGetProperty("expectedGeneration", out var g) ||
                g.ValueKind != JsonValueKind.Number ||
                !g.TryGetInt64(out var generation) || generation < 0 ||
                !v.TryGetProperty("reason", out var re) ||
                re.ValueKind != JsonValueKind.String) return null;
            var reason = re.GetString() ?? "";
            if (reason.Length is < 8 or > 240 || reason != reason.Trim() ||
                reason.Any(char.IsControl)) return null;
            Guid? replacement = null;
            if (v.TryGetProperty("replacementReceiptId", out var p) &&
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
        var g = app.MapGroup("/api/v1/organizations/{organizationId}/shipments/{shipmentId}")
            .RequireAuthorization();
        g.MapGet("/events", async (string organizationId, string shipmentId,
            HttpContext c, OrganizationStore orgs, ShipmentTimelineStore store,
            CancellationToken ct) =>
        {
            c.Response.Headers.CacheControl = "no-store";
            if (!Id(organizationId, out var org) || !Id(shipmentId, out var shipment) ||
                c.Request.Query.Count > 2 ||
                c.Request.Query.Keys.Any(k => k is not ("cursor" or "limit")) ||
                c.Request.Query.Any(k => k.Value.Count != 1))
                return Fail(c, 400, "invalid_request");
            var limit = 25;
            if (c.Request.Query.ContainsKey("limit") &&
                (!int.TryParse(c.Request.Query["limit"], out limit) || limit is < 1 or > 100))
                return Fail(c, 400, "invalid_page_size");
            var denied = await Guard(c, orgs, org, OrganizationPermission.ViewShipmentData, ct);
            if (denied is not null) return denied;
            ShipmentTrackingView? view;
            try { view = await store.ReadAsync(org, shipment, ct); }
            catch (TrackingTimelineLimitException) { return Fail(c, 409, "timeline_limit_exceeded"); }
            if (view is null) return Fail(c, 404, "resource_not_found");
            if (!ShipmentTimelineStore.DecodeCursor(c.Request.Query["cursor"],
                view.State.InputDigest, view.Timeline.Count, out var index))
                return Fail(c, 409, "stale_or_invalid_cursor");
            var page = view.Timeline.Skip(index).Take(limit).Select(e => new {
                eventId = e.EventId, receiptId = e.ReceiptId, sourceId = e.SourceId,
                sourceEventId = e.SourceEventId, eventCode = e.EventCode,
                occurredAtUtc = e.OccurredAtUtc, receivedAtUtc = e.ReceivedAtUtc,
                superseded = e.Superseded,
                replacedByReceiptId = e.ReplacedByReceiptId
            }).ToArray();
            var next = index + page.Length;
            return Results.Ok(new { items = page, eventCount = view.State.EventCount,
                nextCursor = next < view.Timeline.Count
                    ? ShipmentTimelineStore.EncodeCursor(view.State.InputDigest, next) : null,
                policyVersion = view.State.PolicyVersion,
                materializationRevision = view.MaterializationRevision,
                correctionGeneration = view.CorrectionGeneration,
                materializationStale = view.MaterializationStale });
        });
        g.MapGet("/tracking-projection", async (string organizationId, string shipmentId,
            HttpContext c, OrganizationStore orgs, ShipmentTimelineStore store,
            CancellationToken ct) =>
        {
            c.Response.Headers.CacheControl = "no-store";
            if (!Id(organizationId, out var org) || !Id(shipmentId, out var shipment) ||
                c.Request.Query.Count != 0) return Fail(c, 400, "invalid_request");
            var denied = await Guard(c, orgs, org, OrganizationPermission.ViewShipmentData, ct);
            if (denied is not null) return denied;
            ShipmentTrackingView? view;
            try { view = await store.ReadAsync(org, shipment, ct); }
            catch (TrackingTimelineLimitException) { return Fail(c, 409, "timeline_limit_exceeded"); }
            if (view is null) return Fail(c, 404, "resource_not_found");
            return Results.Ok(new {
                observedTrackingState = view.State.ObservedTrackingState,
                etaUtc = (DateTimeOffset?)null, lastEventId = view.State.LastEventId,
                lastOccurredAtUtc = view.State.LastOccurredAtUtc,
                inputDigest = view.State.InputDigest, policyVersion = view.State.PolicyVersion,
                eventCount = view.State.EventCount,
                materializationRevision = view.MaterializationRevision,
                correctionGeneration = view.CorrectionGeneration,
                materializationStale = view.MaterializationStale,
                materializedAtUtc = view.MaterializedAtUtc
            });
        });
        g.MapPost("/tracking-projection/rebuild", async (string organizationId,
            string shipmentId, HttpContext c, IAntiforgery anti, OrganizationStore orgs,
            ShipmentTimelineStore store, CancellationToken ct) =>
        {
            c.Response.Headers.CacheControl = "no-store";
            if (!await Mutation(c, app, anti)) return Fail(c, 403, "csrf_invalid");
            if (!Id(organizationId, out var org) || !Id(shipmentId, out var shipment) ||
                c.Request.Query.Count != 0 || c.Request.ContentLength is > 0)
                return Fail(c, 400, "invalid_request");
            var denied = await Guard(c, orgs, org, OrganizationPermission.ManageShipmentData, ct);
            if (denied is not null) return denied;
            try
            {
                var (result, revision) = await store.RebuildAsync(org, shipment, ct);
                return result switch {
                    TrackingMutationResult.Applied =>
                        Results.Ok(new { materializationRevision = revision,
                            correlationId = c.Items["CorrelationId"] }),
                    TrackingMutationResult.NotFound => Fail(c, 404, "resource_not_found"),
                    _ => Fail(c, 409, "rebuild_conflict")
                };
            }
            catch (TrackingTimelineLimitException) { return Fail(c, 409, "timeline_limit_exceeded"); }
        });
        g.MapPost("/tracking-corrections", async (string organizationId,
            string shipmentId, HttpContext c, IAntiforgery anti, OrganizationStore orgs,
            ShipmentTimelineStore store, CancellationToken ct) =>
        {
            c.Response.Headers.CacheControl = "no-store";
            if (!await Mutation(c, app, anti)) return Fail(c, 403, "csrf_invalid");
            if (!Id(organizationId, out var org) || !Id(shipmentId, out var shipment) ||
                c.Request.Query.Count != 0) return Fail(c, 400, "invalid_request");
            var denied = await Guard(c, orgs, org, OrganizationPermission.ManageShipmentData, ct);
            if (denied is not null) return denied;
            var input = await ReadCorrection(c);
            if (input is null) return Fail(c, 400, "invalid_request");
            try
            {
                var (result, revision) = await store.CorrectAsync(org, shipment, input,
                    Current(c).UserId, c.Items["CorrelationId"]?.ToString() ?? "", ct);
                return result switch {
                    TrackingMutationResult.Applied =>
                        Results.Ok(new { correctionGeneration = input.ExpectedGeneration + 1,
                            materializationRevision = revision,
                            correlationId = c.Items["CorrelationId"] }),
                    TrackingMutationResult.NotFound => Fail(c, 404, "resource_not_found"),
                    TrackingMutationResult.Invalid => Fail(c, 422, "invalid_correction"),
                    _ => Fail(c, 409, "correction_conflict")
                };
            }
            catch (TrackingTimelineLimitException) { return Fail(c, 409, "timeline_limit_exceeded"); }
        });
    }
}
