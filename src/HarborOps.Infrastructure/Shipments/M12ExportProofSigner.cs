using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HarborOps.Infrastructure.Shipments;

public sealed record ShipmentExportProof(long ExpiresEpoch, string Signature);

public static class M12ExportProofSigner
{
    // Only the HTTP runtime and privileged migration provisioner receive this key.
    // The database runtime role can verify a proof but cannot read the verifier key.
    public static ShipmentExportProof? Sign(string? encodedKey, Guid organizationId,
        Guid actorId, Guid sessionId, int rowCount, long byteCount,
        string filterDigest, string correlationId, TimeProvider clock)
    {
        if (string.IsNullOrWhiteSpace(encodedKey)) return null;
        byte[] key;
        try { key = Convert.FromBase64String(encodedKey); }
        catch (FormatException) { return null; }
        if (key.Length != 32)
        {
            CryptographicOperations.ZeroMemory(key);
            return null;
        }
        try
        {
            var expiresEpoch = clock.GetUtcNow().AddSeconds(45).ToUnixTimeSeconds();
            var message = string.Join("|",
                "m12-export-v1", organizationId.ToString("N"), actorId.ToString("N"),
                sessionId.ToString("N"), rowCount.ToString(CultureInfo.InvariantCulture),
                byteCount.ToString(CultureInfo.InvariantCulture), filterDigest, correlationId,
                expiresEpoch.ToString(CultureInfo.InvariantCulture));
            var signature = Convert.ToHexString(HMACSHA256.HashData(key,
                Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
            return new ShipmentExportProof(expiresEpoch, signature);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
