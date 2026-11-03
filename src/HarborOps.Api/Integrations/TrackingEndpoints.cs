using HarborOps.Api.Authentication;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Integrations;
using Microsoft.Extensions.Primitives;

namespace HarborOps.Api.Integrations;

public static class TrackingEndpoints
{
    public static void MapTrackingEndpoints(this WebApplication app)
    {
        app.MapPost("/integrations/v1/tracking-events", async (
            HttpContext context, IntegrationSourceStore store, IntegrationRequestLimiter limiter,
            TimeProvider clock, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!AuthenticationSetup.IsAllowedTransport(context, app.Configuration))
                return Fail(context, 400, "invalid_transport", "Secure transport required");

            var auth = context.Request.Headers.Authorization;
            var peer = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (auth.Count != 1 || auth[0] is not { } raw || raw.Length > 160 ||
                !raw.StartsWith("Bearer ", StringComparison.Ordinal) ||
                raw.Length == 7 || raw[7..].Contains(' '))
            {
                if (!limiter.Allow("peer:" + peer, 10)) return Throttled(context);
                return Fail(context, 401, "invalid_source_credentials", "Source credentials required");
            }
            ValidatedIntegrationSource? source;
            try
            {
                source = await store.ValidateAsync(raw[7..], ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return Results.StatusCode(499);
            }
            if (source is null)
            {
                if (!limiter.Allow("peer:" + peer, 10)) return Throttled(context);
                return Fail(context, 401, "invalid_source_credentials", "Source credentials required");
            }
            if (!limiter.Allow("source:" + source.Id.ToString("N"), 60)) return Throttled(context);

            if (context.Request.Headers.ContentEncoding.Count > 0 ||
                !IsJsonContentType(context.Request.ContentType))
                return Fail(context, 415, "unsupported_media_type", "JSON UTF-8 required");
            if (context.Request.Headers["X-HarborOps-Tracking-Profile"] is not StringValues profile ||
                profile.Count != 1 || profile[0] != TrackingEventPolicy.Profile)
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

            // No persistent receipt exists until M8. Validation must never be a success acknowledgment.
            context.Response.Headers.RetryAfter = "60";
            return Fail(context, 503, "intake_unavailable", "Durable event intake is not available");
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
