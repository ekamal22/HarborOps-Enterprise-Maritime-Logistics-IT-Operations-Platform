using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using HarborOps.Api.Authentication;
using Xunit;

namespace HarborOps.ContractTests;

public sealed class AuthenticationSecurityTests
{
    private sealed class AdjustableClock : TimeProvider
    {
        private DateTimeOffset value = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => value;
        public void Advance(TimeSpan delta) => value = value.Add(delta);
    }

    [Theory]
    [InlineData("127.0.0.1", "localhost", true)]
    [InlineData("::1", "localhost", true)]
    [InlineData("172.30.254.10", "localhost", true)]
    [InlineData("172.30.254.1", "127.0.0.1", true)]
    [InlineData("198.51.100.5", "localhost", false)]
    [InlineData("198.51.100.5", "127.0.0.1", false)]
    [InlineData("172.30.254.99", "localhost", false)]
    [InlineData("172.30.254.10", "external.example", false)]
    public void InsecureLocalHttpRequiresAnActualTrustedNetworkPeer(
        string remote, string host, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        context.Request.Host = new HostString(host);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Identity:AllowInsecureLoopbackHttp"] = "true",
                ["Identity:TrustedLocalHttpPeers"] = "172.30.254.1,172.30.254.10"
            }).Build();

        Assert.Equal(expected, AuthenticationSetup.IsAllowedTransport(context, configuration));
        if (!expected)
        {
            context.Request.Scheme = "https";
            Assert.True(AuthenticationSetup.IsAllowedTransport(context, configuration));
        }
    }

    [Fact]
    public void LocalHttpIsDeniedWhenExceptionDisabled()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Host = new HostString("localhost");
        Assert.False(AuthenticationSetup.IsAllowedTransport(context,
            new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["Identity:AllowInsecureLoopbackHttp"] = "false" })
                .Build()));
    }

    [Fact]
    public void IdentityAndSourceLimitsBlockFloodsThenReset()
    {
        var clock = new AdjustableClock();
        var limiter = new IdentityAttemptLimiter(clock);
        for (var i = 0; i < 10; i++)
            Assert.True(limiter.Permit("alice@example.test", "client-a"));
        Assert.False(limiter.Permit("alice@example.test", "client-a"));
        for (var i = 0; i < 10; i++)
            Assert.True(limiter.Permit("bob@example.test", "client-a"));
        Assert.False(limiter.Permit("charlie@example.test", "client-a"));
        Assert.True(limiter.Permit("charlie@example.test", "client-b"));
        clock.Advance(TimeSpan.FromMinutes(6));
        Assert.True(limiter.Permit("alice@example.test", "client-a"));
    }

    [Fact]
    public async Task AnonymousProtectedRequestsUseJson401AndLoginRequiresCsrf()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var session = await client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
        Assert.Equal("application/json", session.Content.Headers.ContentType?.MediaType);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "unknown@example.test", password = "invalid-passphrase" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
