namespace HarborOps.Infrastructure.Identity;

public sealed class AuthUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Status { get; set; } = "Active";
    public long SecurityVersion { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DisabledAtUtc { get; set; }
}
public sealed class UserCredential
{
    public Guid UserId { get; set; }
    public string PasswordHash { get; set; } = "";
    public string HashAlgorithm { get; set; } = "argon2id";
    public DateTimeOffset PasswordChangedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
public sealed class AuthSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? SelectedOrganizationId { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset LastSeenAtUtc { get; set; }
    public DateTimeOffset IdleExpiresAtUtc { get; set; }
    public DateTimeOffset AbsoluteExpiresAtUtc { get; set; }
    public long SecurityVersion { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? RevocationReason { get; set; }
}
public sealed class AuthEvent
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? SessionId { get; set; }
    public string EventType { get; set; } = "";
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string CorrelationId { get; set; } = "";
    public string ResultCode { get; set; } = "";
}
