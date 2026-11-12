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
        try
        {
            await next(context);
        }
        finally
        {
            M17Telemetry.ObserveRequest(context, Stopwatch.GetElapsedTime(started));
            // Never copy caller headers, business record IDs or raw request paths into spans.
            if (Activity.Current is { } activity)
                activity.SetTag("harborops.operation", "http");
        }
    }
}
