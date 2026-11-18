using HarborOps.Domain.Incidents;
using Xunit;
namespace HarborOps.UnitTests;

public sealed class M18IncidentPolicyTests
{
    [Theory]
    [InlineData("New", true)]
    [InlineData("Closed", true)]
    [InlineData("Reopened", false)]
    [InlineData("closed", false)]
    [InlineData("", false)]
    public void OnlySevenExactStates(string input, bool expected) =>
        Assert.Equal(expected, IncidentPolicy.ParseState(input, out _));

    [Theory]
    [InlineData("SEV-1", true)]
    [InlineData("SEV-4", true)]
    [InlineData("sev-1", false)]
    [InlineData("SEV1", false)]
    public void OnlyDefinedSeverity(string input, bool expected) =>
        Assert.Equal(expected, IncidentPolicy.ParseSeverity(input, out _));

    [Fact]
    public void ValidChainAndEvidenceGates()
    {
        IncidentState[] path = [IncidentState.New, IncidentState.Triage, IncidentState.Investigating,
            IncidentState.Mitigated, IncidentState.Monitoring, IncidentState.Resolved, IncidentState.Closed];
        for (var i = 0; i < path.Length - 1; i++)
            Assert.True(IncidentPolicy.CanTransition(path[i], path[i + 1], true, true, true, true));
        Assert.False(IncidentPolicy.CanTransition(IncidentState.Monitoring, IncidentState.Resolved, false, true, true, true));
        Assert.False(IncidentPolicy.CanTransition(IncidentState.Monitoring, IncidentState.Resolved, true, false, true, true));
        Assert.False(IncidentPolicy.CanTransition(IncidentState.Resolved, IncidentState.Closed, true, true, true, false));
    }

    [Fact]
    public void InvalidTransitionsNeverSucceed()
    {
        foreach (IncidentState state in Enum.GetValues<IncidentState>())
            Assert.False(IncidentPolicy.CanTransition(state, state, true, true, true, true));
        Assert.False(IncidentPolicy.CanTransition(IncidentState.New, IncidentState.Closed, true, true, true, true));
        Assert.False(IncidentPolicy.CanTransition(IncidentState.Closed, IncidentState.Investigating, true, true, true, true));
        Assert.True(IncidentPolicy.CanReopen(IncidentState.Closed));
        Assert.False(IncidentPolicy.CanReopen(IncidentState.Mitigated));
        Assert.True(IncidentPolicy.CanTransition(IncidentState.Monitoring, IncidentState.Investigating, false, false, false, false));
        Assert.True(IncidentPolicy.CanTransition(IncidentState.Mitigated, IncidentState.Investigating, false, false, false, false));
        Assert.False(IncidentPolicy.CanTransition(IncidentState.Triage, IncidentState.Mitigated, true, true, true, true));
        Assert.False(IncidentPolicy.CanTransition(IncidentState.Resolved, IncidentState.Investigating, true, true, true, true));
    }

    [Fact]
    public void ActionAndBounds()
    {
        Assert.True(IncidentPolicy.CanMoveAction(IncidentActionState.Proposed, IncidentActionState.InProgress));
        Assert.False(IncidentPolicy.CanMoveAction(IncidentActionState.Proposed, IncidentActionState.Verified));
        Assert.Throws<ArgumentException>(() => IncidentPolicy.Title(new string('x', 161)));
        Assert.Throws<ArgumentException>(() => IncidentPolicy.ServiceCode("../api"));
        Assert.Throws<ArgumentException>(() => IncidentPolicy.Note("a\0"));
        Assert.Throws<ArgumentException>(() =>
            IncidentPolicy.Note("authorization: BearerExampleSecretToken"));
        Assert.Throws<ArgumentException>(() =>
            IncidentPolicy.Note("api_key=redacted-looking-test-value"));
        Assert.Equal("api", IncidentPolicy.ServiceCode("api"));
    }

    [Theory]
    [InlineData("Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.x.y")]
    [InlineData("authorization=Basic dXNlcjpwYXNzd29yZA==")]
    [InlineData("client_secret = abcdefghijkl")]
    [InlineData("curl -H 'Bearer abcdefghijklmnopqrstuv'")]
    [InlineData("key AKIAABCDEFGHIJKLMNOP leaked")]
    [InlineData("token hop_v1_abcdefgh12345678 was pasted")]
    [InlineData("jwt eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2ln here")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----")]
    public void KnownSecretShapesAreRejectedFromNotes(string text) =>
        Assert.Throws<ArgumentException>(() => IncidentPolicy.Note(text));

    [Theory]
    [InlineData("Readiness probe fails intermittently after pool restart.")]
    [InlineData("Authorization header was missing on the health check")]
    [InlineData("Rotated the secret store last week; no value is recorded here.")]
    public void OrdinaryOperationalProseIsAccepted(string text) =>
        Assert.Equal(text, IncidentPolicy.Note(text));

    [Fact]
    public void FreshnessAndSeparation()
    {
        var at = DateTimeOffset.UtcNow;
        Assert.True(IncidentPolicy.IsFresh(at, at, TimeSpan.FromMinutes(5)));
        Assert.False(IncidentPolicy.IsFresh(at.AddMinutes(-7), at, TimeSpan.FromMinutes(5)));
        Assert.True(IncidentPolicy.IndependentClosureRequired(IncidentSeverity.SEV1));
        Assert.False(IncidentPolicy.IndependentClosureRequired(IncidentSeverity.SEV4));
    }
}
