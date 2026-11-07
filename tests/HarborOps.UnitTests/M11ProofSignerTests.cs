using HarborOps.Domain.Exceptions;
using HarborOps.Infrastructure.Exceptions;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class M11ProofSignerTests
{
    private static readonly DateTimeOffset DatabaseTime =
        new(2026, 11, 7, 16, 30, 0, TimeSpan.Zero);

    private static readonly string TestKey = Convert.ToBase64String(new byte[32]);

    [Fact]
    public void EvaluationProofUsesSuppliedDatabaseClock()
    {
        var input = new ShipmentExceptionInput(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Active", "Booked", 3,
            null, DatabaseTime.AddHours(-2),
            null, null, "Unknown", true,
            0, 0, "", "", null);
        var signal = ShipmentExceptionPolicy.Evaluate(input, DatabaseTime)
            .Single(x => x.RuleCode == ShipmentExceptionPolicy.ArrivalScheduleOverdue);

        var proof = M11ProofSigner.Evaluation(TestKey, input, signal, DatabaseTime);

        Assert.NotNull(proof);
        Assert.Equal(DatabaseTime.AddSeconds(45).ToUnixTimeSeconds(),
            proof.ExpiresEpoch);
        Assert.Equal(64, proof.Signature.Length);
        Assert.Null(M11ProofSigner.Evaluation(null, input, signal, DatabaseTime));
    }

    [Fact]
    public void ManagerProofUsesDatabaseClockAndBindsAction()
    {
        var org = Guid.NewGuid();
        var exception = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var session = Guid.NewGuid();
        var key = Guid.NewGuid();

        var proof = M11ProofSigner.Action(TestKey, org, exception, actor,
            session, "Acknowledge", 2,
            "Reviewed synthetic exception", key, DatabaseTime);

        Assert.NotNull(proof);
        Assert.Equal(DatabaseTime.AddSeconds(45).ToUnixTimeSeconds(),
            proof.ExpiresEpoch);
        Assert.Equal(64, proof.Signature.Length);

        var digest = M11ProofSigner.ActionDigest(org, exception, actor,
            session, "Acknowledge", 2, "Reviewed synthetic exception");
        Assert.NotEqual(digest, M11ProofSigner.ActionDigest(org, exception,
            actor, session, "Resolve", 2, "Reviewed synthetic exception"));
        Assert.NotEqual(digest, M11ProofSigner.ActionDigest(org, exception,
            actor, Guid.NewGuid(), "Acknowledge", 2, "Reviewed synthetic exception"));
    }
}
