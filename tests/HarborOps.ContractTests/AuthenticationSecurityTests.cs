using System.Net;
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
