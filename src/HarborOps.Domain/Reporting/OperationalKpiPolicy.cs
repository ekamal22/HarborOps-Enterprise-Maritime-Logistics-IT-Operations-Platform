using System.Globalization;
using System.Text.RegularExpressions;

namespace HarborOps.Domain.Reporting;

public sealed record OperationalKpiDefinition(
    string Code, string Label, string SourceClass, string? DenominatorCode,
    string Description);

public sealed record OperationalKpiFilters(
    DateTimeOffset FromUtc, DateTimeOffset BeforeUtc, string Bucket,
    Guid? OriginPortId, Guid? DestinationPortId, Guid? VoyageId, string? SourceKind);

public static partial class OperationalKpiPolicy
{
    public const string Version = "m13-kpi-v1";
    public const int MaximumDays = 366;
    public const int MaximumMemberPage = 100;
    public const int MaximumReportMembers = 500_000; // provisional fail-closed capacity ceiling
    public static readonly TimeSpan ProvisionalReportLifetime = TimeSpan.FromMinutes(10);

    public static readonly IReadOnlyList<OperationalKpiDefinition> Definitions =
    [
        new("K01", "Active shipments", "CURRENT_WORKFLOW", "D01", "Active records other than Completed or Cancelled"),
        new("K02", "Workflow distribution", "CURRENT_WORKFLOW", "D01", "One current unarchived shipment per workflow state"),
        new("K03", "Completed workflows", "RECORDED_WORKFLOW_EVENT", null, "First recorded Completed transition in the UTC window"),
        new("K04", "Cancelled workflows", "RECORDED_WORKFLOW_EVENT", null, "First recorded Cancelled transition in the UTC window"),
        new("K05", "Strict on-time recorded arrivals", "RECORDED_WORKFLOW_EVENT", "D05", "First recorded Arrived at or before the proven planned arrival"),
        new("K06", "Within 60-minute tolerance", "RECORDED_WORKFLOW_EVENT", "D05", "First recorded Arrived no later than proven plan plus 60 minutes"),
        new("K07", "Recorded late arrivals", "CURRENT_EXCEPTION", "D07", "Current confirmed RecordedArrivalLate condition"),
        new("K08", "Schedule overdue, unconfirmed", "CURRENT_EXCEPTION", "D08", "Current schedule-overdue condition, not a physical arrival claim"),
        new("K09", "Departure slippage risk", "CURRENT_EXCEPTION", "D09", "Current recorded-workflow departure risk, not a forecast ETA"),
        new("K10", "Equipment conflicts", "CURRENT_TRACKING_QUALITY", "D10", "Fresh M10 conflict corroborated by current M11 evaluation"),
        new("K11", "Open or acknowledged exceptions", "CURRENT_EXCEPTION", null, "Active current exception occurrences not marked Resolved"),
        new("K12", "Exception openings", "RECORDED_EXCEPTION_EVENT", null, "Distinct occurrence IDs first detected in UTC buckets"),
        new("K13", "Evaluator coverage", "CURRENT_EXCEPTION", "D13", "Eligible shipments with all four current rule evaluations"),
        new("K14", "Unassessed or stale", "CURRENT_EXCEPTION", "D13", "Eligible shipments missing current reliable evaluations")
    ];

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,6})?(?:Z|\+00:00)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex UtcInstantPattern();

    public static bool TryUtc(string? text, out DateTimeOffset value)
    {
        value = default;
        return text is not null && UtcInstantPattern().IsMatch(text) &&
            DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out value) && value.Offset == TimeSpan.Zero;
    }

    public static bool ValidWindow(DateTimeOffset from, DateTimeOffset before) =>
        from.Offset == TimeSpan.Zero && before.Offset == TimeSpan.Zero &&
        from < before && before - from <= TimeSpan.FromDays(MaximumDays);

    public static OperationalKpiFilters DefaultWindow(DateTimeOffset nowUtc) 
    {
        var end = new DateTimeOffset(nowUtc.UtcDateTime.Date, TimeSpan.Zero);
        return new OperationalKpiFilters(end.AddDays(-7), end, "day", null, null, null, null);
    }

    public static bool ValidMetric(string code) => Definitions.Any(x => x.Code == code);
    public static bool ValidDimension(string code, string? dimension) =>
        code == "K02"
            ? dimension is "Draft" or "Booked" or "InTransit" or "Arrived" or "Completed" or "Cancelled"
            : code == "K12"
                ? dimension is "EquipmentConflict" or "RecordedArrivalLate" or "ArrivalScheduleOverdue" or "DepartureSlippageRisk"
                : string.IsNullOrEmpty(dimension);
}