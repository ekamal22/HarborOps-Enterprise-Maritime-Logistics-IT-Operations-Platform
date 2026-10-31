namespace HarborOps.Infrastructure.References;

public abstract class ReferenceRecord
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string ReferenceCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = "Active";
    public string SourceKind { get; set; } = "UserProvided";
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class PortReference : ReferenceRecord
{
    public string CountryCode { get; set; } = "";
    public string? UnLocode { get; set; }
}

public sealed class VesselReference : ReferenceRecord
{
    public string? ImoNumber { get; set; }
    public string? Mmsi { get; set; }
    public string? FlagCountryCode { get; set; }
}
