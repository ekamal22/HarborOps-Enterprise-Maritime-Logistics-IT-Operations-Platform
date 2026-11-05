namespace HarborOps.Infrastructure.Integrations;

// A receipt is immutable. Work state is represented separately for the future M9 worker.
public sealed class TrackingInboxReceipt
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid SourceId { get; set; }
    public Guid CredentialId { get; set; }
    public string Profile { get; set; } = "";
    public string SourceEventId { get; set; } = "";
    public byte[] CanonicalDigest { get; set; } = [];
    public byte[] CanonicalPayloadUtf8 { get; set; } = [];
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public string CorrelationId { get; set; } = "";
}

public sealed class TrackingAcceptedEvent
{
    public Guid Id { get; set; }
    public Guid ReceiptId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid SourceId { get; set; }
    public string EventFamily { get; set; } = "";
    public string EventCode { get; set; } = "";
    public string Classifier { get; set; } = "";
    public string ExternalSchemaVersion { get; set; } = "";
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string? EquipmentReference { get; set; }
    public string? TransportReference { get; set; }
    public byte[] RawPayloadUtf8 { get; set; } = [];
    public string CanonicalPayloadJson { get; set; } = "";
}

public sealed class TrackingInboxWorkItem
{
    public Guid ReceiptId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid SourceId { get; set; }
    public string State { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid? LeaseToken { get; set; }
    public long LeaseEpoch { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset AvailableAtUtc { get; set; }
    public int RedriveGeneration { get; set; }
    public string? LastErrorCode { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? ResolutionKind { get; set; }
}

public sealed class TrackingWorkAttemptEvent
{
    public Guid Id { get; set; }
    public Guid ReceiptId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid SourceId { get; set; }
    public int Generation { get; set; }
    public int AttemptCount { get; set; }
    public long LeaseEpoch { get; set; }
    public string EventKind { get; set; } = "";
    public string? ReasonCode { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? RedriveReason { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}

public sealed class TrackingProcessingEffect
{
    public Guid ReceiptId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid SourceId { get; set; }
    public string ResolutionKind { get; set; } = "ReferenceResolved";
    public string ResolutionPolicy { get; set; } = "m9-reference-v1";
    public Guid ContainerId { get; set; }
    public Guid ShipmentId { get; set; }
    public DateTimeOffset ResolvedAtUtc { get; set; }
}

public sealed class TrackingReceiptConflict
{
    public Guid Id { get; set; }
    public Guid ReceiptId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid SourceId { get; set; }
    public byte[] AttemptedDigest { get; set; } = [];
    public DateTimeOffset FirstSeenAtUtc { get; set; }
    public string CorrelationId { get; set; } = "";
}
