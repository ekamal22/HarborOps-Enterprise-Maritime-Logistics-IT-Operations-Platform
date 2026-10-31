namespace HarborOps.Infrastructure.Organizations;

public sealed class Organization
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Status { get; set; } = "Active";
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class OrganizationMembership
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "";
    public string Status { get; set; } = "Active";
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid? GrantedByUserId { get; set; }
}
