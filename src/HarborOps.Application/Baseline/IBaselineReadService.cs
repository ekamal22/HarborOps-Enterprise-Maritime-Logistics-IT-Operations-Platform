namespace HarborOps.Application.Baseline;

public sealed record BaselineStatus(string Service, string DatasetVersion, DateTimeOffset SeededAtUtc, string Source);

public interface IBaselineReadService
{
    Task<BaselineStatus?> ReadAsync(CancellationToken cancellationToken);
}
