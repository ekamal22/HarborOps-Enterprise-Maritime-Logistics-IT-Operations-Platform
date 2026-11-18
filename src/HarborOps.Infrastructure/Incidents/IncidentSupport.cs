namespace HarborOps.Infrastructure.Incidents;

// Request-scoped correlation identifier supplied by the API pipeline so the M16 audit
// entries written inside an incident transaction carry the same value as the HTTP response.
public sealed class IncidentCorrelation
{
    public string? Value { get; set; }
}

// Server-issued, finite service observation that a human may choose to turn into an incident.
public sealed record IncidentPrefill(string ServiceCode, string Status, DateTimeOffset ObservedAtUtc);
