namespace HarborOps.Infrastructure.Integrations;

public sealed class IntegrationSource
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string SourceCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Status { get; set; } = "Active";
    public string Capability { get; set; } = "tracking_event_validate";
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? DisabledAtUtc { get; set; }
}

public sealed class IntegrationCredential
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid SourceId { get; set; }
    public byte[] SecretDigest { get; set; } = [];
    public string KeyVersion { get; set; } = "v1";
    public DateTimeOffset NotBeforeUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? LastUsedAtUtc { get; set; }
}

public sealed record ValidatedIntegrationSource(Guid Id, Guid OrganizationId);
public sealed record ProvisionedIntegrationSource(Guid Id, string SourceCode, Guid CredentialId, string Bearer);
