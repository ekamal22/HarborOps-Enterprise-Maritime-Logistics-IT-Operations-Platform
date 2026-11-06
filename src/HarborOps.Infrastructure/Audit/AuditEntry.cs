namespace HarborOps.Infrastructure.Audit;

// Metadata is a bounded, fixed-shape JSON document produced only by the
// database audit triggers. Never deserialize arbitrary client input into it.
public sealed class AuditEntry
{
    public Guid Id { get; set; }
    public Guid? OrganizationId { get; set; }
    public string EventType { get; set; } = "";
    public int EventSchemaVersion { get; set; } = 1;
    public string Outcome { get; set; } = "";
    public string ActorKind { get; set; } = "";
    public Guid? ActorUserId { get; set; }
    public Guid? ActorSessionId { get; set; }
    public Guid? ActorSourceId { get; set; }
    public string? ActorRole { get; set; }
    public string ResourceType { get; set; } = "";
    public Guid? ResourceId { get; set; }
    public string CorrelationId { get; set; } = "";
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string MetadataJson { get; set; } = "{}";
}
