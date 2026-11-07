using System.Security.Cryptography;
using System.Text;
using HarborOps.Infrastructure.Shipments;

namespace HarborOps.Api.Shipments;

// The signing secret is supplied only to the authenticated HTTP runtime and
// the migration-time verifier provisioner. A database-only credential is not
// sufficient to construct an authorized correction request.
public static class M10CorrectionProofSigner
{
    public static TrackingCorrectionProof? Sign(string? keyBase64,
        Guid org, Guid shipment, Guid actor, Guid session,
        Guid idempotencyKey, TrackingCorrection correction)
    {
        if (string.IsNullOrWhiteSpace(keyBase64)) return null;
        byte[] key;
        try { key = Convert.FromBase64String(keyBase64); }
        catch (FormatException) { return null; }
        if (key.Length != 32)
        {
            CryptographicOperations.ZeroMemory(key);
            return null;
        }

        try
        {
            var nonce = Guid.NewGuid();
            var expires = DateTimeOffset.UtcNow.AddSeconds(45).ToUnixTimeSeconds();
            var requestDigest = ShipmentTimelineStore.RequestDigest(
                org, shipment, actor, correction);
            var message = string.Join("|", "m10-correction-v1",
                nonce.ToString("N"), expires.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                idempotencyKey.ToString("N"), session.ToString("N"), requestDigest);
            var signature = Convert.ToHexString(HMACSHA256.HashData(key,
                Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
            return new TrackingCorrectionProof(nonce, expires, signature);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
