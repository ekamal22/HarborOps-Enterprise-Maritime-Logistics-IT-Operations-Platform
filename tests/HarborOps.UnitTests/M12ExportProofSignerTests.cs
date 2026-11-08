using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HarborOps.Infrastructure.Shipments;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class M12ExportProofSignerTests
{
    private sealed class FrozenClock(DateTimeOffset utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utc;
    }

    [Fact]
    public void SignatureBindsEveryAuthorizedExportField()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var encoded = Convert.ToBase64String(key);
        var clock = new FrozenClock(new DateTimeOffset(2026, 11, 7, 20, 0, 0, TimeSpan.Zero));
        var org = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var session = Guid.NewGuid();
        var digest = new string('A', 64);
        var correlation = Guid.NewGuid().ToString("N");
        var proof = M12ExportProofSigner.Sign(encoded, org, actor, session,
            2, 200L, digest, correlation, clock);
        Assert.NotNull(proof);
        Assert.Equal(clock.GetUtcNow().AddSeconds(45).ToUnixTimeSeconds(),
            proof.ExpiresEpoch);
        // Independently derive the wire signature; SQL must use the exact format.
        var message = string.Join("|",
            "m12-export-v1", org.ToString("N"), actor.ToString("N"), session.ToString("N"),
            2.ToString(CultureInfo.InvariantCulture),
            200L.ToString(CultureInfo.InvariantCulture), digest, correlation,
            proof.ExpiresEpoch.ToString(CultureInfo.InvariantCulture));
        var expected = Convert.ToHexString(HMACSHA256.HashData(key,
            Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
        Assert.Equal(expected, proof.Signature);
        Assert.Matches("^[0-9a-f]{64}$", proof.Signature);

        Assert.NotEqual(proof.Signature,
            M12ExportProofSigner.Sign(encoded, org, Guid.NewGuid(), session,
                2, 200, digest, correlation, clock)!.Signature);
        Assert.NotEqual(proof.Signature,
            M12ExportProofSigner.Sign(encoded, org, actor, session,
                3, 200, digest, correlation, clock)!.Signature);
        Assert.NotEqual(proof.Signature,
            M12ExportProofSigner.Sign(encoded, org, actor, session,
                2, 201, digest, correlation, clock)!.Signature);
        Assert.NotEqual(proof.Signature,
            M12ExportProofSigner.Sign(encoded, org, actor, session,
                2, 200, digest, Guid.NewGuid().ToString("N"), clock)!.Signature);
        CryptographicOperations.ZeroMemory(key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64")]
    [InlineData("YQ==")]
    public void MissingOrInvalidKeyCannotSign(string? input)
    {
        var result = M12ExportProofSigner.Sign(input,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0, 128,
            new string('F', 64), Guid.NewGuid().ToString("N"), TimeProvider.System);
        Assert.Null(result);
    }
}
