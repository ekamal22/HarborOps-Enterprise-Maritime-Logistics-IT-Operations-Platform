namespace HarborOps.Infrastructure.Persistence;

public sealed class BootstrapRecord
{
    public string Key { get; set; } = string.Empty;
    public string DatasetVersion { get; set; } = string.Empty;
    public DateTimeOffset SeededAtUtc { get; set; }
}
