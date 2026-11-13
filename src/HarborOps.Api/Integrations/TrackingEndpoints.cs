using System.Data.Common;
using System.Diagnostics;
using HarborOps.Api.Observability;
using HarborOps.Infrastructure.Observability;
using HarborOps.Api.Authentication;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Integrations;

namespace HarborOps.Api.Integrations;

public static class TrackingEndpoints
{
    public static void MapTrackingEndpoints(this WebApplication app)
    {
        app.MapPost("/integrations/v1/tracking-events", async (
            HttpContext context, IntegrationSourceStore store, TrackingInboxStore inbox,
            IntegrationRequestLimiter limiter, TimeProvider clock, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!AuthenticationSetup.IsAllowedTransport(context, app.Configuration))
                return Fail(context, 400, "invalid_transport", "Secure transport required");

            var peer = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            // Reject excessive traffic before any database query or credential lookup.
            if (!limiter.Allow("preauth:" + peer, 120)) return Throttled(context);
            var origin = context.Request.Headers.Origin;
            if (origin.Count > 0 && !AuthenticationSetup.IsSameOrigin(context))
                return Fail(context, 403, "origin_denied", "Origin not permitted");
            var auth = context.Request.Headers.Authorization;
            if (auth.Count != 1 || auth[0] is not { } raw || raw.Length > 160 ||
                !raw.StartsWith("Bearer ", StringComparison.Ordinal) ||
                raw.Length == 7 || raw[7..].Contains(' '))
            {
                if (!limiter.Allow("peer:" + peer, 10)) return Throttled(context);
                return Fail(context, 401, "invalid_source_credentials", "Source credentials required");
            }
            IntegrationAuthentication authentication;
            try
            {
                authentication = await store.AuthenticateAsync(raw[7..], ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return Results.StatusCode(499);
            }
            if (authentication.Forbidden)
                return Fail(context, 403, "source_access_denied", "Source access denied");
            if (authentication.Source is not { } source)
            {
                if (!limiter.Allow("peer:" + peer, 10)) return Throttled(context);
                return Fail(context, 401, "invalid_source_credentials", "Source credentials required");
            }
            if (!limiter.Allow("source:" + source.Id.ToString("N"), 60)) return Throttled(context);

            if (context.Request.Headers["Content-Encoding"].Count > 0 ||
                !IsJsonContentType(context.Request.ContentType))
                return Fail(context, 415, "unsupported_media_type", "JSON UTF-8 required");
            var profile = context.Request.Headers["X-HarborOps-Tracking-Profile"];
            if (profile.Count != 1 || profile[0] != TrackingEventPolicy.Profile)
                return Fail(context, 422, "unsupported_tracking_profile", "Unsupported tracking profile");
            if (context.Request.ContentLength > TrackingEventPolicy.MaximumBodyBytes)
                return Fail(context, 413, "body_too_large", "Request body exceeds the limit");

            var bytes = new byte[TrackingEventPolicy.MaximumBodyBytes + 1];
            var count = 0;
            try
            {
                while (count < bytes.Length)
                {
                    var read = await context.Request.Body.ReadAsync(bytes.AsMemory(count), ct);
                    if (read == 0) break;
                    count += read;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return Results.StatusCode(499);
            }

            if (count > TrackingEventPolicy.MaximumBodyBytes)
                return Fail(context, 413, "body_too_large", "Request body exceeds the limit");
            var validation = TrackingEventPolicy.Parse(bytes.AsMemory(0, count), clock.GetUtcNow());
            if (validation.Value is null)
                return Fail(context, validation.Status, validation.Code, "Invalid or unsupported tracking event");

            // Bind identity from the validated credential, never the request body.
            var normalized = validation.Value.Bind(source.Id, source.OrganizationId);
            if (normalized.SourceId != source.Id || normalized.OrganizationId != source.OrganizationId)
                return Fail(context, 403, "source_access_denied", "Source access denied");
            // Only a completed receipt transaction can produce HTTP 202.
            // The original bearer is checked again under the write transaction locks.
            try
            {
                var result = await inbox.ReceiveAsync(normalized, raw[7..],
                    bytes.AsMemory(0, count), context.Items["CorrelationId"]?.ToString() ?? "", ct);
                // Link only a newly durable receipt to the server correlation.
                // Replay has a different request correlation and must not fabricate
                // a link to the earlier retained receipt.
                if (result.Outcome == TrackingReceiptOutcome.Received &&
                    context.RequestServices.GetService<M17TelemetryOptions>() is { Enabled: true } options)
                {
                    var digest = M17CorrelationDigest.FromServerCorrelation(
                        context.Items["CorrelationId"]?.ToString(), options.LinkKey);
                    if (digest is not null)
                    {
                        // A short receipt marker is sampled from the durable keyed
                        // correlation, even when the HTTP root was not recorded.
                        // It is not a fabricated worker parent span.
                        var tags = new KeyValuePair<string, object?>[]
                        {
                            new("harborops.link_digest", digest),
                            new("harborops.operation", "tracking.receipt")
                        };
                        using var receiptSpan = M17Telemetry.Traces.StartActivity(
                            "tracking.receipt", ActivityKind.Internal, parentContext: default, tags: tags);
                    }
                }
                switch (result.Outcome)
                {
                    case TrackingReceiptOutcome.Received:
                    case TrackingReceiptOutcome.Replayed:
                        return Results.Json(new
                        {
                            receiptId = result.ReceiptId,
                            status = "received",
                            duplicate = result.Outcome == TrackingReceiptOutcome.Replayed,
                            receivedAtUtc = result.ReceivedAtUtc,
                            correlationId = context.Items["CorrelationId"]
                        }, statusCode: StatusCodes.Status202Accepted);
                    case TrackingReceiptOutcome.Collision:
                        return Fail(context, 409, "source_event_collision",
                            "Source event identifier conflicts with an earlier receipt");
                    case TrackingReceiptOutcome.AccessDenied:
                        return Fail(context, 403, "source_access_denied", "Source access denied");
                    case TrackingReceiptOutcome.CapacityExceeded:
                        context.Response.Headers.RetryAfter = "60";
                        return Fail(context, 503, "intake_capacity_exceeded",
                            "Durable intake is temporarily at capacity");
                    default:
                        throw new InvalidOperationException("Unknown tracking receipt outcome");
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return Results.StatusCode(499);
            }
            catch (DbException)
            {
                context.Response.Headers.RetryAfter = "60";
                return Fail(context, 503, "receipt_unavailable", "Durable receipt temporarily unavailable");
            }
            catch (TimeoutException)
            {
                context.Response.Headers.RetryAfter = "60";
                return Fail(context, 503, "receipt_unavailable", "Durable receipt temporarily unavailable");
            }
        }).AllowAnonymous();
    }

    private static bool IsJsonContentType(string? value) =>
        value is not null && (value.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("application/json; charset=utf-8", StringComparison.OrdinalIgnoreCase));

    private static IResult Throttled(HttpContext ctx)
    {
        ctx.Response.Headers.RetryAfter = "60";
        return Fail(ctx, 429, "rate_limited", "Request rate exceeded");
    }

    private static IResult Fail(HttpContext ctx, int status, string code, string message) =>
        Results.Json(new { code, message, correlationId = ctx.Items["CorrelationId"] }, statusCode: status);
}
