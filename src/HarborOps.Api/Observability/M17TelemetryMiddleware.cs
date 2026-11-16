using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace HarborOps.Api.Observability;

// Sits outside the existing exception handler, so handled 500/503 responses
// are classified by the final HTTP outcome without changing error contracts.
public sealed class M17TelemetryMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        // The exception handler (inside this middleware) clears the endpoint it re-executes, which would turn every
        // dependency failure on an authenticated route into an "unmatched" request. Keep what the router chose.
        var routed = context.GetEndpoint();
        try
        {
            await next(context);
        }
        finally
        {
            M17Telemetry.ObserveRequest(context, Stopwatch.GetElapsedTime(started), routed);
            // Never copy caller headers, business record IDs or raw request paths into spans.
            if (Activity.Current is { } activity)
                activity.SetTag("harborops.operation", "http");
        }
    }
}
