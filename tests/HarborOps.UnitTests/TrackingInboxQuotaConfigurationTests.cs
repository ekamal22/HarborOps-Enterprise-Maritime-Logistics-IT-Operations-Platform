using HarborOps.Infrastructure.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class TrackingInboxQuotaConfigurationTests
{
    // The unit test project references only the configuration abstractions, so a minimal
    // single-value configuration is used instead of the in-memory provider.
    private sealed class SingleValueConfiguration(string? configured) : IConfiguration
    {
        public string? this[string key]
        {
            get => key == "M8:PendingLimit" ? configured : null;
            set => throw new NotSupportedException();
        }

        public IEnumerable<IConfigurationSection> GetChildren() => [];

        public IChangeToken GetReloadToken() => new CancellationChangeToken(CancellationToken.None);

        public IConfigurationSection GetSection(string key) => throw new NotSupportedException();
    }

    private static IConfiguration Config(string? text) => new SingleValueConfiguration(text);

    [Theory]
    [InlineData("0")]
    [InlineData("10001")]
    [InlineData("-1")]
    [InlineData("garbage")]
    [InlineData("1.0")]
    [InlineData(" 2 ")]
    public void InvalidPendingAdmissionLimitRejectsStartup(string invalid) =>
        Assert.Throws<InvalidOperationException>(() =>
            TrackingInboxStore.ReadPendingLimit(Config(invalid)));

    [Theory]
    [InlineData(null, 10000)]
    [InlineData("1", 1)]
    [InlineData("9999", 9999)]
    [InlineData("10000", 10000)]
    public void ValidPendingAdmissionLimitIsDeterministic(string? value, int expected) =>
        Assert.Equal(expected, TrackingInboxStore.ReadPendingLimit(Config(value)));
}
