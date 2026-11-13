using System.Security.Cryptography;
using System.Text;

namespace HarborOps.Infrastructure.Observability;

// The existing M8 receipt correlation is server-generated, but must never be
// exported as a raw telemetry attribute. Use a disposable 256-bit keyed digest
// to associate API and worker diagnostic roots without trusting trace headers.
public static class M17CorrelationDigest
{
    public static byte[] RequireKey(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
            throw new InvalidOperationException("M17 requires a private 256-bit local link key");
        byte[] key;
        try { key = Convert.FromBase64String(encoded); }
        catch (FormatException) { throw new InvalidOperationException("Invalid M17 link key format"); }
        if (key.Length != 32)
            throw new InvalidOperationException("M17 link key must contain 256 random bits");
        return key;
    }

    // API receipt and detached worker use the same keyed decision. Their
    // trace IDs remain independent; only a sanitized digest associates them.
    public static bool IsValidDigest(string? digest) =>
        digest is { Length: 64 } &&
        digest.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static bool SampleLinkedJourney(string? digest)
    {
        if (!IsValidDigest(digest)) return false;
        var leading = ulong.Parse(digest!.AsSpan(0, 16),
            System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture);
        return (leading & long.MaxValue) < ((ulong)long.MaxValue / 10);
    }

    public static string? FromServerCorrelation(string? correlation, byte[] key)
    {
        if (correlation is null || correlation.Length != 32 ||
            !correlation.All(char.IsAsciiHexDigit) || key.Length != 32)
            return null;
        return Convert.ToHexString(
            HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(correlation))).ToLowerInvariant();
    }
}
