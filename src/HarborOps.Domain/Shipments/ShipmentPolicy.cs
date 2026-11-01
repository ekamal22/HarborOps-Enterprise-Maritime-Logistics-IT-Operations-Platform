using System.Text.RegularExpressions;
using HarborOps.Domain.Maritime;
using HarborOps.Domain.References;

namespace HarborOps.Domain.Shipments;

public static class ShipmentPolicy
{
    private static readonly Regex ExternalPattern = new("^[A-Z0-9._/-]{1,80}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Code(string? value) => ReferencePolicy.Code(value);
    public static string? ExternalReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().ToUpperInvariant();
        if (!ExternalPattern.IsMatch(normalized))
            throw new ArgumentException("Invalid external shipment reference");
        return normalized;
    }
    public static string? Name(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : ReferencePolicy.Name(value);

    public static string Workflow(string? value) => value is
        "Draft" or "Booked" or "InTransit" or "Arrived" or "Completed" or "Cancelled"
        ? value : throw new ArgumentException("Invalid shipment workflow status");

    public static string RecordStatus(string? value) =>
        value is "Active" or "Archived" ? value :
        throw new ArgumentException("Invalid shipment record status");

    public static DateTimeOffset? Utc(string? value) => MaritimePolicy.Utc(value);

    public static void ValidateTimes(DateTimeOffset? start, DateTimeOffset? end)
    {
        if (start.HasValue && end.HasValue && start >= end)
            throw new ArgumentException("Planned departure must precede arrival");
    }

    public static bool CanTransition(string from, string to) =>
        from == to ||
        (from == "Draft" && (to is "Booked" or "Cancelled")) ||
        (from == "Booked" && (to is "InTransit" or "Cancelled")) ||
        (from == "InTransit" && (to is "Arrived" or "Cancelled")) ||
        (from == "Arrived" && to == "Completed");
}
