using System.Diagnostics;
using OpenTelemetry;

namespace HarborOps.Api.Observability;

// No raw exception text, credentials, identifiers, SQL or unvalidated
// external trace attributes leave the process.
public sealed class M17TraceSanitizer : BaseProcessor<Activity>
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "http.request.method", "http.response.status_code", "http.route",
        "url.scheme", "network.protocol.version", "error.type",
        "db.system.name", "harborops.operation",
        "harborops.outcome", "harborops.link_digest"
    };
    private static readonly HashSet<string> Methods = new(StringComparer.Ordinal)
    {
        "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"
    };
    private static readonly HashSet<string> Outcomes = new(StringComparer.Ordinal)
    {
        "success", "unavailable", "cancelled", "other", "processed",
        "needs_reconciliation", "stale", "retry", "failure", "reference_conflict"
    };

    public override void OnEnd(Activity activity)
    {
        foreach (var tag in activity.TagObjects.ToArray())
        {
            var raw = tag.Value?.ToString();
            var valid = Allowed.Contains(tag.Key) && raw is { Length: <= 120 };
            if (tag.Key == "http.request.method")
                valid &= raw is not null && Methods.Contains(raw);
            else if (tag.Key == "harborops.outcome")
                valid &= raw is not null && Outcomes.Contains(raw);
            else if (tag.Key == "harborops.link_digest")
                valid &= raw is { Length: 64 } &&
                    raw.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
            else if (tag.Key == "db.system.name")
                valid &= raw == "postgresql";
            if (!valid) activity.SetTag(tag.Key, (object?)null);
        }
        activity.DisplayName = activity.Kind == ActivityKind.Server ? "http.request"
            : activity.Source.Name == M17Telemetry.TraceSourceName
                ? "harborops.operation" : "internal.operation";
    }
}
