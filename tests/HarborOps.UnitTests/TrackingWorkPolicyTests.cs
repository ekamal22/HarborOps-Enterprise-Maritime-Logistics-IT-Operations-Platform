using HarborOps.Domain.Tracking;
using HarborOps.Domain.Organizations;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class TrackingWorkPolicyTests
{
    private static readonly Guid Receipt =
        Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void SafeDefaultsAreBoundedAndConfigured()
    {
        var settings = new TrackingWorkerSettings();
        settings.Validate();
        Assert.Equal(60, settings.LeaseSeconds);
        Assert.Equal(20, settings.HeartbeatSeconds);
        Assert.Equal(5, settings.MaxAttempts);
        Assert.Equal(3, settings.MaxRedrives);
    }

    [Theory]
    [InlineData(8,20,5,2,5,3,16,4)]
    [InlineData(60,40,5,2,5,3,16,4)]
    [InlineData(60,20,0,2,5,3,16,4)]
    [InlineData(60,20,5,2,0,3,16,4)]
    [InlineData(60,20,5,2,11,3,16,4)]
    [InlineData(60,20,5,2,5,3,17,4)]
    [InlineData(60,20,5,2,5,3,16,5)]
    public void UnsafeConfigurationsFailBeforeWorkStarts(
        int lease, int heartbeat, int poll, int unused, int attempts, int redrives,
        int batch, int concurrency)
    {
        _ = unused;
        Assert.Throws<InvalidOperationException>(() =>
            new TrackingWorkerSettings(lease, heartbeat, poll, attempts, redrives,
                batch, concurrency).Validate());
    }

    [Fact]
    public void RetryIsDeterministicFiniteAndBounded()
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var a = TrackingWorkPolicy.RetryDelay(Receipt, 0, attempt);
            var b = TrackingWorkPolicy.RetryDelay(Receipt, 0, attempt);
            Assert.Equal(a, b);
            var nominal = Math.Min(900, 5L << (attempt - 1));
            Assert.InRange(a.TotalSeconds, nominal * 0.8, nominal);
        }
        var capped = TrackingWorkPolicy.RetryDelay(Receipt, 3, 20);
        Assert.InRange(capped.TotalSeconds, 720, 900);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackingWorkPolicy.RetryDelay(Receipt, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TrackingWorkPolicy.RetryDelay(Guid.Empty, 0, 1));
    }

    [Theory]
    [InlineData("Pending", true)]
    [InlineData("Leased", true)]
    [InlineData("RetryWaiting", true)]
    [InlineData("NeedsReconciliation", true)]
    [InlineData("Processed", false)]
    [InlineData("DeadLetter", false)]
    public void UnresolvedBacklogCountsEveryActiveState(string state, bool expected) =>
        Assert.Equal(expected, TrackingWorkPolicy.IsUnresolved(state));

    [Fact]
    public void RedriveAndRolesFailClosed()
    {
        Assert.True(TrackingWorkPolicy.CanRedrive("NeedsReconciliation", 0, 3));
        Assert.True(TrackingWorkPolicy.CanRedrive("DeadLetter", 2, 3));
        Assert.False(TrackingWorkPolicy.CanRedrive("Processed", 0, 3));
        Assert.False(TrackingWorkPolicy.CanRedrive("DeadLetter", 3, 3));
        foreach (var role in Enum.GetValues<OrganizationRole>())
        {
            Assert.Equal(role is OrganizationRole.Manager or OrganizationRole.Analyst or
                OrganizationRole.Auditor,
                OrganizationPolicy.Allows(role, OrganizationPermission.TrackingWorkRead));
            Assert.Equal(role == OrganizationRole.Manager,
                OrganizationPolicy.Allows(role, OrganizationPermission.TrackingWorkRedrive));
        }
        Assert.False(TrackingWorkPolicy.SafeReason("secret=foo/bar"));
        Assert.True(TrackingWorkPolicy.SafeReason("investigate assignment"));
    }

    [Theory]
    [InlineData("40001", true)]
    [InlineData("40P01", true)]
    [InlineData("08006", true)]
    [InlineData("23503", false)]
    [InlineData("23514", false)]
    public void OnlyExplicitSqlStatesAreTransient(string sqlState, bool expected) =>
        Assert.Equal(expected, TrackingWorkPolicy.IsRetryableSqlState(sqlState));
}
