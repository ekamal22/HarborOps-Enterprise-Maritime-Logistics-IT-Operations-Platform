using System.Buffers.Binary;
using System.Security.Cryptography;

namespace HarborOps.Domain.Tracking;

public static class TrackingWorkPolicy
{
    public const int DefaultLeaseSeconds = 60;
    public const int DefaultHeartbeatSeconds = 20;
    public const int DefaultPollMilliseconds = 2_000;
    public const int DefaultMaxAttempts = 5;
    public const int DefaultMaxRedrives = 3;
    public const int DefaultBatchSize = 16;
    public const int DefaultConcurrency = 4;
    public const int DefaultRetryBaseSeconds = 5;
    public const int DefaultRetryMaxSeconds = 900;
    public const string ResolutionVersion = "m9-reference-v1";

    public static bool IsUnresolved(string state) =>
        state is "Pending" or "Leased" or "RetryWaiting" or "NeedsReconciliation";

    public static bool CanRedrive(string state, int generation, int ceiling) =>
        (state is "DeadLetter" or "NeedsReconciliation") &&
        generation >= 0 && generation < ceiling;

    public static bool IsRetryableSqlState(string? state) =>
        state is "40001" or "40P01" or "55P03" or "57014" or "53300" or "53400" or "08000" or "08001" or "08003" or "08006";

    public static TimeSpan RetryDelay(Guid receipt, int generation, int attempt,
        int baseSeconds = DefaultRetryBaseSeconds, int maximumSeconds = DefaultRetryMaxSeconds)
    {
        if (receipt == Guid.Empty || generation < 0 || attempt is < 1 or > 32 ||
            baseSeconds is < 1 or > 60 || maximumSeconds < baseSeconds || maximumSeconds > 3600)
            throw new ArgumentOutOfRangeException(nameof(attempt), "Invalid bounded retry input");
        var exponent = Math.Min(attempt - 1, 15);
        var bounded = Math.Min(maximumSeconds, (long)baseSeconds << exponent);
        Span<byte> payload = stackalloc byte[24];
        receipt.TryWriteBytes(payload);
        BinaryPrimitives.WriteInt32LittleEndian(payload[16..20], generation);
        BinaryPrimitives.WriteInt32LittleEndian(payload[20..24], attempt);
        var hash = SHA256.HashData(payload);
        var fraction = BinaryPrimitives.ReadUInt32LittleEndian(hash.AsSpan(0, 4)) / (double)uint.MaxValue;
        return TimeSpan.FromMilliseconds(bounded * (1.0 - 0.2 * fraction) * 1000.0);
    }

    public static bool SafeReason(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 240 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '_' or '-' or '.' or ':');
}

public sealed record TrackingWorkerSettings(
    int LeaseSeconds = TrackingWorkPolicy.DefaultLeaseSeconds,
    int HeartbeatSeconds = TrackingWorkPolicy.DefaultHeartbeatSeconds,
    int PollMilliseconds = TrackingWorkPolicy.DefaultPollMilliseconds,
    int MaxAttempts = TrackingWorkPolicy.DefaultMaxAttempts,
    int MaxRedrives = TrackingWorkPolicy.DefaultMaxRedrives,
    int BatchSize = TrackingWorkPolicy.DefaultBatchSize,
    int Concurrency = TrackingWorkPolicy.DefaultConcurrency)
{
    public void Validate()
    {
        if (LeaseSeconds is < 10 or > 540 ||
            HeartbeatSeconds is < 2 or > 200 || HeartbeatSeconds * 2 >= LeaseSeconds ||
            PollMilliseconds is < 100 or > 60000 ||
            MaxAttempts != TrackingWorkPolicy.DefaultMaxAttempts ||
            MaxRedrives != TrackingWorkPolicy.DefaultMaxRedrives ||
            BatchSize is < 1 or > 16 || Concurrency is < 1 or > 4)
            throw new InvalidOperationException("Tracking worker settings are outside the safe bounded range");
    }
}
