namespace HarborOps.Infrastructure.Maritime;

public abstract class MaritimeRecord
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string ReferenceCode { get; set; } = "";
    public string RecordStatus { get; set; } = "Active";
    public string SourceKind { get; set; } = "UserProvided";
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
public sealed class VoyageRecord : MaritimeRecord
{
    public Guid VesselId { get; set; }
    public Guid OriginPortId { get; set; }
    public Guid DestinationPortId { get; set; }
    public DateTimeOffset? PlannedDepartureAtUtc { get; set; }
    public DateTimeOffset? PlannedArrivalAtUtc { get; set; }
    public DateTimeOffset? ActualDepartureAtUtc { get; set; }
    public DateTimeOffset? ActualArrivalAtUtc { get; set; }
    public string VoyageStatus { get; set; } = "Planned";
}
public sealed class ContainerRecord : MaritimeRecord
{
    public string IdentifierKind { get; set; } = "";
    public string ContainerIdentifier { get; set; } = "";
    public string EquipmentType { get; set; } = "";
    public string? DisplayName { get; set; }
}
