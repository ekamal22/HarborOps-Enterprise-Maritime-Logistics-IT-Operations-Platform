using System.Diagnostics;

namespace HarborOps.Infrastructure.Observability;

// The pinned OTLP serializer writes Activity events and links independently
// of tag processors. Never start the exporter unless both serializer limits
// suppress those untrusted collections. A code-only tag scrub is insufficient.
public static class M17TracePrivacyGuard
{
    public static void RequireSafeExporterLimits() =>
        RequireSafeExporterLimits(
            Environment.GetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT"),
            Environment.GetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT"));

    // Pure overload for negative controls. The exporter uses the environment
    // overload, which must agree with the pinned OTLP SDK serialization limits.
    public static void RequireSafeExporterLimits(string? eventLimit, string? linkLimit)
    {
        if (eventLimit != "0" || linkLimit != "0")
            throw new InvalidOperationException(
                "M17 requires zero exported span events and links");
    }

    public static void SanitizeNonTagFields(Activity activity)
    {
        // Trace state may be caller-supplied; status descriptions can contain
        // exception messages, query details or customer identifiers.
        activity.TraceStateString = null;
        activity.SetStatus(activity.Status);
    }
}
