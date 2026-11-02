namespace HarborOps.Infrastructure.Assignments;

public abstract class AssignmentRecord
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ShipmentId { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? ClosedByUserId { get; set; }
}

public sealed class ShipmentContainerAssignment : AssignmentRecord
{
    public Guid ContainerId { get; set; }
}

public sealed class ShipmentVoyageAssignment : AssignmentRecord
{
    public Guid VoyageId { get; set; }
}
