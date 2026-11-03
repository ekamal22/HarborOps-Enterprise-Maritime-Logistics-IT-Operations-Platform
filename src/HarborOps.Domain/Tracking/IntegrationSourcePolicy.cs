using System.Text.RegularExpressions;

namespace HarborOps.Domain.Tracking;

public static class IntegrationSourcePolicy
{
    public const string Capability = "tracking_event_validate";
    public static readonly TimeSpan MaximumLifetime = TimeSpan.FromDays(90);
    public static readonly TimeSpan MaximumOverlap = TimeSpan.FromHours(24);
    private static readonly Regex SourceCode = new(@"\A[a-z][a-z0-9_-]{2,79}\z", RegexOptions.CultureInvariant);

    public static string NormalizeCode(string value)
    {
        var code = value.Trim().ToLowerInvariant();
        if (!SourceCode.IsMatch(code)) throw new ArgumentException("Invalid source code");
        return code;
    }

    public static string ValidateName(string value)
    {
        var name = value.Trim();
        if (name.Length is < 1 or > 160 || name.Any(char.IsControl))
            throw new ArgumentException("Invalid source name");
        return name;
    }

    public static bool IsValidWindow(DateTimeOffset from, DateTimeOffset until, DateTimeOffset now) =>
        from <= now && until > now && until - from <= MaximumLifetime;
}
