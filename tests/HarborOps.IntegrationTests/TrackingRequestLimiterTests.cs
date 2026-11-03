using HarborOps.Api.Integrations;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class TrackingRequestLimiterTests
{
    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } =
            new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    [Fact]
    public void WindowThresholdAndExpiryUseProvidedClock()
    {
        var clock = new ManualClock();
        var limiter = new IntegrationRequestLimiter(clock);
        Assert.True(limiter.Allow("peer-a", 2));
        Assert.True(limiter.Allow("peer-a", 2));
        Assert.False(limiter.Allow("peer-a", 2));
        clock.UtcNow = clock.UtcNow.AddSeconds(61);
        Assert.True(limiter.Allow("peer-a", 2));
        Assert.True(limiter.Allow("peer-b", 2));
    }

    [Fact]
    public void HighCardinalityPartitionsAreBoundedAndStaleEntriesReclaimed()
    {
        var clock = new ManualClock();
        var limiter = new IntegrationRequestLimiter(clock);
        for (var i = 0; i < 4096; i++)
            Assert.True(limiter.Allow("remote-" + i, 1));
        Assert.False(limiter.Allow("remote-overflow", 1));
        Assert.False(limiter.Allow("remote-0", 1));
        clock.UtcNow = clock.UtcNow.AddMinutes(3);
        Assert.True(limiter.Allow("remote-overflow", 1));
    }

    [Fact]
    public async Task ConcurrentCallsNeverExceedConfiguredWindow()
    {
        var limiter = new IntegrationRequestLimiter(new ManualClock());
        var allowed = await Task.WhenAll(Enumerable.Range(0, 500)
            .Select(_ => Task.Run(() => limiter.Allow("same-source", 12))));
        Assert.Equal(12, allowed.Count(x => x));
    }
}
