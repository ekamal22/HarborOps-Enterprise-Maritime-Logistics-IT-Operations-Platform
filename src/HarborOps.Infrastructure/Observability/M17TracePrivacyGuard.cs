using System.Diagnostics;

namespace HarborOps.Infrastructure.Observability;

// The pinned OTLP serializer writes Activity events and links independently
// of tag processors. Never start the exporter unless both serializer limits
// suppress those untrusted collections. A code-only tag scrub is insufficient.
public static class M17TracePrivacyGuard
{
    public static void RequireSafeExporterLimits()
    {
        if (Environment.GetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT") != "0" ||
            Environment.GetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT") != "0")
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
