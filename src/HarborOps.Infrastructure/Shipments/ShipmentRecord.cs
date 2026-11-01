using System.Text.Json.Serialization;

namespace HarborOps.Infrastructure.Shipments;

public sealed class ShipmentRecord
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string ReferenceCode { get; set; } = "";
    public string? ExternalReference { get; set; }
    public string? DisplayName { get; set; }
    public Guid OriginPortId { get; set; }
    public Guid DestinationPortId { get; set; }
    public DateTimeOffset? PlannedDepartureAtUtc { get; set; }
    public DateTimeOffset? PlannedArrivalAtUtc { get; set; }
    public string WorkflowStatus { get; set; } = "Draft";
    public string RecordStatus { get; set; } = "Active";
    public string SourceKind { get; set; } = "UserProvided";
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? ArchivedAtUtc { get; set; }
    [JsonIgnore]
    public Guid ChangedByUserId { get; set; }
}

public sealed class ShipmentStatusChange
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ShipmentId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = "";
    public long Version { get; set; }
    public DateTimeOffset ChangedAtUtc { get; set; }
    public Guid ChangedByUserId { get; set; }
}
