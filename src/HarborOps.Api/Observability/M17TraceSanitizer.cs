using System.Diagnostics;
using OpenTelemetry;

namespace HarborOps.Api.Observability;

// Final processor before export. The allowlist intentionally strips SQL,
// URLs, query strings, cookies, claims, exception messages and tenant fields.
public sealed class M17TraceSanitizer : BaseProcessor<Activity>
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "http.request.method", "http.response.status_code", "http.route",
        "url.scheme", "network.protocol.version", "server.port",
        "error.type", "db.system.name", "harborops.operation",
        "harborops.outcome", "harborops.link_digest"
    };

    public override void OnEnd(Activity activity)
    {
        foreach (var tag in activity.TagObjects.ToArray())
        {
            if (!Allowed.Contains(tag.Key) || tag.Value is string text && text.Length > 120)
                activity.SetTag(tag.Key, (object?)null);
        }
        activity.DisplayName = activity.Kind == ActivityKind.Server ? "http.request" :
            activity.Source.Name == M17Telemetry.TraceSourceName
                ? "harborops.operation" : "internal.operation";
    }
}
