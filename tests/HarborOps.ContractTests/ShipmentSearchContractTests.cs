using System.Net;
using HarborOps.Api.Search;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace HarborOps.ContractTests;

public sealed class ShipmentSearchContractTests
{
    [Fact]
    public void CursorRequiresMatchingOrganizationAndFilters()
    {
        var codec = new ShipmentCursorCodec(new EphemeralDataProtectionProvider());
        var org = Guid.NewGuid();
        var after = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 11, 7, 16, 0, 0, TimeSpan.Zero);
        var token = codec.Encode(org, "FINGERPRINT", after, now, now);

        Assert.True(codec.TryDecode(token, org, "FINGERPRINT", now.AddMinutes(1), out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(after, parsed.After);
        Assert.False(codec.TryDecode(token, Guid.NewGuid(), "FINGERPRINT", now, out _));
        Assert.False(codec.TryDecode(token, org, "OTHER", now, out _));
        Assert.False(codec.TryDecode(token, org, "FINGERPRINT", now.AddMinutes(16), out _));
        Assert.False(codec.TryDecode(token + "X", org, "FINGERPRINT", now, out _));
    }

    [Fact]
    public void CursorDoesNotCrossKeyRings()
    {
        var first = new ShipmentCursorCodec(new EphemeralDataProtectionProvider());
        var second = new ShipmentCursorCodec(new EphemeralDataProtectionProvider());
        var now = DateTimeOffset.UtcNow;
        var org = Guid.NewGuid();
        var token = first.Encode(org, "SCOPE", Guid.NewGuid(), now, now);
        Assert.False(second.TryDecode(token, org, "SCOPE", now, out _));
    }

    [Theory]
    [InlineData("/api/v1/organizations/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa/shipments/search")]
    [InlineData("/api/v1/organizations/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa/exports/shipments")]
    public async Task SearchAndExportRequireAuthenticatedSession(string path)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("no-store", string.Join(",", response.Headers.GetValues("Cache-Control")));
    }
}
