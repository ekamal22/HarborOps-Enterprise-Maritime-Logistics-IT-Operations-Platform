using System.Globalization;
using System.Text.RegularExpressions;
using HarborOps.Domain.References;

namespace HarborOps.Domain.Maritime;

public static class MaritimePolicy
{
    private static readonly Regex Iso = new("^[A-Z]{3}U[0-9]{7}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex Synthetic = new("^SYN-BOX-[A-Z0-9]{4,16}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly string[] Types = ["Dry20", "Dry40", "HighCube40", "Reefer20", "Reefer40", "Other"];

    public static string Code(string? value) => ReferencePolicy.Code(value);
    public static string? Label(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : ReferencePolicy.Name(value);
    public static string EquipmentType(string? value) =>
        Types.Contains(value, StringComparer.Ordinal) ? value! : throw new ArgumentException("Invalid equipment type");
    public static string Identifier(string? kind, string? value)
    {
        var text = (value ?? "").Trim().ToUpperInvariant();
        if (kind == "Synthetic")
        {
            if (!Synthetic.IsMatch(text)) throw new ArgumentException("Invalid synthetic identifier");
            return text;
        }
        if (kind != "ISO6346") throw new ArgumentException("Invalid identifier kind");
        text = text.Replace(" ", "", StringComparison.Ordinal);
        if (!Iso.IsMatch(text)) throw new ArgumentException("Invalid ISO container identifier");
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            var c = text[i];
            var val = c is >= '0' and <= '9' ? c - '0' : (c - 'A' + 10) + (c - 'A' + 10) / 11 + 1;
            // ISO letter table skips 11, 22 and 33.
            if (c is >= 'A' and <= 'Z')
            {
                val = c - 'A' + 10;
                val += (val - 1) / 10;
            }
            sum += val * (1 << i);
        }
        if ((sum % 11) % 10 != text[10] - '0')
            throw new ArgumentException("Invalid container check digit");
        return text;
    }
    public static string Progress(string? value) =>
        value is "Planned" or "Underway" or "Arrived" or "Cancelled"
            ? value : throw new ArgumentException("Invalid voyage progress");
    public static string RecordStatus(string? value) =>
        value is "Active" or "Archived" ? value : throw new ArgumentException("Invalid record status");
    public static DateTimeOffset? Utc(string? value)
    {
        if (value is null) return null;
        if (!DateTimeOffset.TryParseExact(value, "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ||
            time.Offset != TimeSpan.Zero)
            throw new ArgumentException("Timestamp must be an explicit UTC instant");
        return time.ToUniversalTime();
    }
    public static void ValidateTimes(DateTimeOffset? plannedDeparture, DateTimeOffset? plannedArrival,
        DateTimeOffset? actualDeparture, DateTimeOffset? actualArrival, string progress)
    {
        if (plannedDeparture.HasValue && plannedArrival.HasValue && plannedDeparture >= plannedArrival)
            throw new ArgumentException("Invalid planned window");
        if (actualDeparture.HasValue && actualArrival.HasValue && actualDeparture > actualArrival)
            throw new ArgumentException("Invalid actual window");
        if (progress == "Planned" && (actualDeparture.HasValue || actualArrival.HasValue) ||
            progress == "Underway" && (!actualDeparture.HasValue || actualArrival.HasValue) ||
            progress == "Arrived" && (!actualDeparture.HasValue || !actualArrival.HasValue) ||
            progress == "Cancelled" && (actualDeparture.HasValue || actualArrival.HasValue))
            throw new ArgumentException("Voyage progress conflicts with actual times");
    }
    public static bool Transition(string before, string after) =>
        before == after || before == "Planned" && after is "Underway" or "Cancelled" ||
        before == "Underway" && after == "Arrived";
}
