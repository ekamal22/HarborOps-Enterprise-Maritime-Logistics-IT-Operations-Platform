using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HarborOps.Domain.Shipments;

public sealed record ShipmentSearchFilters(
    string Q, string RecordStatus, string? WorkflowStatus,
    Guid? VoyageId, Guid? OriginPortId, Guid? DestinationPortId,
    DateTimeOffset? PlannedDepartureFromUtc, DateTimeOffset? PlannedDepartureBeforeUtc,
    DateTimeOffset? PlannedArrivalFromUtc, DateTimeOffset? PlannedArrivalBeforeUtc,
    int Limit = 25)
{
    public static ShipmentSearchFilters Create(string? q, string? recordStatus,
        string? workflowStatus, Guid? voyageId, Guid? originPortId, Guid? destinationPortId,
        DateTimeOffset? departureFrom, DateTimeOffset? departureBefore,
        DateTimeOffset? arrivalFrom, DateTimeOffset? arrivalBefore, int limit)
    {
        var search = (q ?? "").Trim();
        if (search.Length > 80 || search.Any(char.IsControl))
            throw new ArgumentException("Invalid search text");
        var status = string.IsNullOrEmpty(recordStatus) ? "Active" : recordStatus;
        if (status is not ("Active" or "Archived" or "All"))
            throw new ArgumentException("Invalid record status");
        if (!string.IsNullOrEmpty(workflowStatus))
            ShipmentPolicy.Workflow(workflowStatus);
        else
            workflowStatus = null;
        if (limit is < 1 or > 100 || new[] { voyageId, originPortId, destinationPortId }
            .Any(id => id == Guid.Empty))
            throw new ArgumentException("Invalid search limit or identifier");
        ValidateWindow(departureFrom, departureBefore);
        ValidateWindow(arrivalFrom, arrivalBefore);
        return new(search, status, workflowStatus, voyageId, originPortId, destinationPortId,
            departureFrom, departureBefore, arrivalFrom, arrivalBefore, limit);
    }

    private static void ValidateWindow(DateTimeOffset? from, DateTimeOffset? before)
    {
        if (from.HasValue != before.HasValue) throw new ArgumentException("Both UTC bounds required");
        if (from is null) return;
        if (from.Value.Offset != TimeSpan.Zero || before!.Value.Offset != TimeSpan.Zero ||
            before.Value <= from.Value ||
            before.Value - from.Value > TimeSpan.FromDays(366))
            throw new ArgumentException("Invalid UTC window");
    }

    public string Fingerprint() =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this))));
}

public static class ShipmentCsv
{
    public const int MaximumRows = 5000;
    public const int MaximumBytes = 10 * 1024 * 1024;
    public static readonly string[] Headers =
    [
        "Shipment ID", "Reference Code", "External Reference", "Display Name",
        "Workflow Status", "Record Status", "Origin Port ID", "Destination Port ID",
        "Planned Departure UTC", "Planned Arrival UTC", "Created UTC", "Updated UTC"
    ];

    public static string Cell(string? value)
    {
        var text = value ?? "";
        var first = text.TrimStart();
        if (first.Length > 0 && first[0] is ('=' or '+' or '-' or '@'))
            text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}