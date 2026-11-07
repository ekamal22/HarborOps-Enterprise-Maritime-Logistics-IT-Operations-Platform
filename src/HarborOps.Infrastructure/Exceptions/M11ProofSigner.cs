using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HarborOps.Domain.Exceptions;

namespace HarborOps.Infrastructure.Exceptions;

// M11 has distinct signed domains even though the migration-time verifier uses
// the accepted private M10 key. A database-only API credential cannot sign actions.
public static class M11ProofSigner
{
    public sealed record Proof(Guid Nonce, long ExpiresEpoch, string Signature);

    private static string? Sign(string? base64, string message)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;
        byte[] secret;
        try { secret = Convert.FromBase64String(base64); }
        catch (FormatException) { return null; }
        try
        {
            return secret.Length == 32
                ? Convert.ToHexString(HMACSHA256.HashData(secret,
                    Encoding.UTF8.GetBytes(message))).ToLowerInvariant()
                : null;
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    private static string Milliseconds(DateTimeOffset? date) =>
        date?.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture) ?? "";

    public static Proof? Evaluation(string? base64, ShipmentExceptionInput input,
        ShipmentExceptionSignal signal, DateTimeOffset databaseNow)
    {
        var nonce = Guid.NewGuid();
        var expires = databaseNow.AddSeconds(45).ToUnixTimeSeconds();
        var message = string.Join("|", "m11-eval-v1",
            nonce.ToString("N"), expires.ToString(CultureInfo.InvariantCulture),
            input.OrganizationId.ToString("N"), input.ShipmentId.ToString("N"),
            signal.RuleCode, signal.Condition, signal.Severity,
            signal.Confidence, signal.ReasonCode, signal.SourceFingerprint,
            input.ShipmentVersion.ToString(CultureInfo.InvariantCulture),
            input.TrackingInputRevision.ToString(CultureInfo.InvariantCulture),
            input.CorrectionGeneration.ToString(CultureInfo.InvariantCulture),
            Milliseconds(signal.ReferenceAtUtc), Milliseconds(signal.ThresholdAtUtc),
            Milliseconds(signal.NextEvaluationAtUtc));
        var signature = Sign(base64, message);
        return signature is null ? null : new Proof(nonce, expires, signature);
    }

    public static string ActionDigest(Guid org, Guid exception, Guid actor,
        Guid session, string action, long expectedVersion, string reason)
    {
        var message = string.Join("|", "M11Action", org.ToString("N"),
            exception.ToString("N"), actor.ToString("N"), session.ToString("N"),
            action, expectedVersion.ToString(CultureInfo.InvariantCulture), reason);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message)))
            .ToLowerInvariant();
    }

    public static Proof? Action(string? base64, Guid org, Guid exception,
        Guid actor, Guid session, string action, long expectedVersion,
        string reason, Guid idempotencyKey, DateTimeOffset databaseNow)
    {
        var nonce = Guid.NewGuid();
        var expires = databaseNow.AddSeconds(45).ToUnixTimeSeconds();
        var digest = ActionDigest(org, exception, actor, session, action, expectedVersion, reason);
        var message = string.Join("|", "m11-action-v1", nonce.ToString("N"),
            expires.ToString(CultureInfo.InvariantCulture), idempotencyKey.ToString("N"),
            digest);
        var signature = Sign(base64, message);
        return signature is null ? null : new Proof(nonce, expires, signature);
    }
}
