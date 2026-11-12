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

    public static string? FromServerCorrelation(string? correlation, byte[] key)
    {
        if (correlation is null || correlation.Length != 32 ||
            !correlation.All(char.IsAsciiHexDigit) || key.Length != 32)
            return null;
        return Convert.ToHexString(
            HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(correlation))).ToLowerInvariant();
    }
}
