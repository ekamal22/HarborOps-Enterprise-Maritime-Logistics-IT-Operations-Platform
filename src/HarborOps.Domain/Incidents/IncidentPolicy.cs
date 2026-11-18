using System.Text.RegularExpressions;

namespace HarborOps.Domain.Incidents;

public enum IncidentState { New, Triage, Investigating, Mitigated, Monitoring, Resolved, Closed }
public enum IncidentSeverity { SEV1, SEV2, SEV3, SEV4 }
public enum IncidentActionState { Proposed, InProgress, Verified, Cancelled }

public static class IncidentPolicy
{
    // Best-effort known secret markers. This does not claim comprehensive
    // detection of every credential or sensitive user-entered value.
    private static readonly Regex KnownSecrets = new(
        @"(?:-----BEGIN [A-Z ]*PRIVATE KEY-----" +
        @"|\b(?:password|passwd|secret|client[_-]?secret|api[_-]?key|access[_-]?token|auth[_-]?token|authorization)" +
        @"\s*[:=]\s*(?:(?:bearer|basic)\s+)?\S{8,}" +
        @"|\bbearer\s+[A-Za-z0-9._~+/=-]{16,}" +
        @"|\bAKIA[0-9A-Z]{16}\b" +
        @"|\bhop_v1_[A-Za-z0-9_-]{8,}" +
        @"|\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{2,}\.[A-Za-z0-9_-]{2,})",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));
    public static bool ParseState(string? value, out IncidentState state)
    {
        state = default;
        return value is not null && Enum.TryParse(value, false, out state)
            && Enum.IsDefined(state) && value == state.ToString();
    }

    public static bool ParseSeverity(string? value, out IncidentSeverity severity)
    {
        severity = value switch {
            "SEV-1" => IncidentSeverity.SEV1, "SEV-2" => IncidentSeverity.SEV2,
            "SEV-3" => IncidentSeverity.SEV3, "SEV-4" => IncidentSeverity.SEV4,
            _ => (IncidentSeverity)(-1)
        };
        return Enum.IsDefined(severity);
    }

    public static string SeverityCode(IncidentSeverity value) => value switch {
        IncidentSeverity.SEV1 => "SEV-1", IncidentSeverity.SEV2 => "SEV-2",
        IncidentSeverity.SEV3 => "SEV-3", IncidentSeverity.SEV4 => "SEV-4",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static bool CanTransition(IncidentState from, IncidentState to,
        bool recoveryVerified, bool integrityVerified, bool actionsDispositioned,
        bool independentlyConfirmed)
    {
        var valid = (from, to) switch {
            (IncidentState.New, IncidentState.Triage) => true,
            (IncidentState.Triage, IncidentState.Investigating) => true,
            (IncidentState.Investigating, IncidentState.Mitigated) => true,
            (IncidentState.Mitigated, IncidentState.Monitoring) => true,
            (IncidentState.Mitigated, IncidentState.Investigating) => true,
            (IncidentState.Monitoring, IncidentState.Investigating) => true,
            (IncidentState.Monitoring, IncidentState.Resolved) => true,
            (IncidentState.Resolved, IncidentState.Closed) => true,
            _ => false
        };
        if (!valid) return false;
        if (to == IncidentState.Resolved)
            return recoveryVerified && integrityVerified && actionsDispositioned;
        if (to == IncidentState.Closed)
            return independentlyConfirmed && actionsDispositioned;
        return true;
    }
    public static bool CanReopen(IncidentState from) =>
        from is IncidentState.Resolved or IncidentState.Closed;

    public static bool CanMoveAction(IncidentActionState from, IncidentActionState to) =>
        (from, to) switch {
            (IncidentActionState.Proposed, IncidentActionState.InProgress) => true,
            (IncidentActionState.Proposed, IncidentActionState.Cancelled) => true,
            (IncidentActionState.InProgress, IncidentActionState.Verified) => true,
            (IncidentActionState.InProgress, IncidentActionState.Cancelled) => true,
            _ => false
        };

    public static string Title(string? text)
    {
        var value = (text ?? "").Trim();
        if (value.Length is < 1 or > 160 || value.Any(char.IsControl))
            throw new ArgumentException("Invalid incident title");
        return value;
    }
    public static string ServiceCode(string? text)
    {
        var value = text ?? "";
        if (value is not ("api" or "database" or "notifications" or "tracking_held"))
            throw new ArgumentException("Invalid service code");
        return value;
    }
    public static string Reason(string? text)
    {
        var value = text ?? "";
        if (value.Length is < 1 or > 80 ||
            value.Any(c => !(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '-' or '_')))
            throw new ArgumentException("Invalid reason");
        return value;
    }
    public static string Note(string? text)
    {
        var value = (text ?? "").Trim();
        if (value.Length is < 1 or > 4096 ||
            value.Any(c => char.IsControl(c) && c is not '\n' and not '\t') ||
            KnownSecrets.IsMatch(value))
            throw new ArgumentException("Invalid note");
        return value;
    }
    public static bool IsFresh(DateTimeOffset observed, DateTimeOffset now, TimeSpan window) =>
        observed <= now.AddSeconds(30) && observed >= now.Subtract(window);
    public static bool IndependentClosureRequired(IncidentSeverity severity) =>
        severity is IncidentSeverity.SEV1 or IncidentSeverity.SEV2;
}
