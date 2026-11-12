using System.Diagnostics;
using OpenTelemetry;

namespace HarborOps.Worker.Observability;

public sealed class M17WorkerTraceSanitizer : BaseProcessor<Activity>
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "harborops.operation", "harborops.outcome"
    };
    public override void OnEnd(Activity activity)
    {
        foreach (var tag in activity.TagObjects.ToArray())
            if (!Allowed.Contains(tag.Key) ||
                tag.Value is string text && text.Length > 80)
                activity.SetTag(tag.Key, (object?)null);
        activity.DisplayName = "tracking.process";
    }
}
