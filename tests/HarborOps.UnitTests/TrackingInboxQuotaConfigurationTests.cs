using HarborOps.Infrastructure.Integrations;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class TrackingInboxQuotaConfigurationTests
{
    private static IConfiguration Config(string? text)
    {
        var values = new Dictionary<string, string?>();
        if (text is not null) values["M8:PendingLimit"] = text;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

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
