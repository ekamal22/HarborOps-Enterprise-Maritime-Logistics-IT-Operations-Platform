using System.Diagnostics;
using OpenTelemetry;
using HarborOps.Infrastructure.Observability;

namespace HarborOps.Worker.Observability;

public sealed class M17WorkerTraceSanitizer : BaseProcessor<Activity>
{
    private static readonly HashSet<string> Outcomes = new(StringComparer.Ordinal)
    {
        "processed", "needs_reconciliation", "stale", "retry",
        "failure", "reference_conflict", "cancelled", "other"
    };
    public override void OnEnd(Activity activity)
    {
        M17TracePrivacyGuard.SanitizeNonTagFields(activity);
        foreach (var tag in activity.TagObjects.ToArray())
        {
            var value = tag.Value?.ToString();
            var valid = tag.Key switch
            {
                "harborops.operation" => value == "tracking.process",
                "harborops.outcome" => value is not null && Outcomes.Contains(value),
                "harborops.link_digest" => value is { Length: 64 } &&
                    value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'),
                _ => false
            };
            if (!valid) activity.SetTag(tag.Key, (object?)null);
        }
        activity.DisplayName = "tracking.process";
    }
}
