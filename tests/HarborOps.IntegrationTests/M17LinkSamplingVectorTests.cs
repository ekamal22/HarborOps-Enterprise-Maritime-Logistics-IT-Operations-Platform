using HarborOps.Infrastructure.Observability;
using Xunit;

namespace HarborOps.IntegrationTests;

// The verification script scripts/g17-link-sampling.mjs predicts which receipts the API and the worker will trace.
// The same boundary table (SAMPLING_VECTORS in that file) is asserted here against the real implementation, so the
// two cannot drift apart.
public sealed class M17LinkSamplingVectorTests
{
    public static IEnumerable<object[]> Vectors() =>
    [
        [new string('0', 16) + new string('a', 48), true],
        ["0ccccccccccccccb" + new string('b', 48), true],
        ["0ccccccccccccccc" + new string('b', 48), false],
        ["0ccccccccccccccd" + new string('b', 48), false],
        ["7fffffffffffffff" + new string('c', 48), false],
        ["8000000000000000" + new string('d', 48), true],
        ["8ccccccccccccccb" + new string('e', 48), true],
        ["8ccccccccccccccc" + new string('e', 48), false],
        ["ffffffffffffffff" + new string('f', 48), false],
        ["1000000000000000" + new string('0', 48), false],
    ];

    [Theory]
    [MemberData(nameof(Vectors))]
    public void BoundaryVectorsMatchTheVerificationScript(string digest, bool expected) =>
        Assert.Equal(expected, M17CorrelationDigest.SampleLinkedJourney(digest));
}
