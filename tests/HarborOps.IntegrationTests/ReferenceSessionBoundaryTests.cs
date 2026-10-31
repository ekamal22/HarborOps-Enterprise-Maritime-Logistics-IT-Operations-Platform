using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class ReferenceSessionBoundaryTests
{
    private static (HttpMethod method, string path, object? body)[] Routes(Guid org)
    {
        var prefix = $"/api/v1/organizations/{org}";
        return new[]
        {
            (HttpMethod.Get, prefix + "/ports", (object?)null),
            (HttpMethod.Get, prefix + "/ports/" + Guid.NewGuid(), (object?)null),
            (HttpMethod.Post, prefix + "/ports",
                (object?)new { referenceCode = "SYNTHETIC", name = "Synthetic", countryCode = "US" }),
            (HttpMethod.Patch, prefix + "/ports/" + Guid.NewGuid(),
                (object?)new { name = "Invalid", expectedVersion = 1 }),
            (HttpMethod.Delete, prefix + "/ports/" + Guid.NewGuid(),
                (object?)new { expectedVersion = 1 }),
            (HttpMethod.Get, prefix + "/vessels", (object?)null),
            (HttpMethod.Get, prefix + "/vessels/" + Guid.NewGuid(), (object?)null),
            (HttpMethod.Post, prefix + "/vessels",
                (object?)new { referenceCode = "SYNTHETIC", name = "Synthetic" }),
            (HttpMethod.Patch, prefix + "/vessels/" + Guid.NewGuid(),
                (object?)new { name = "Invalid", expectedVersion = 1 }),
            (HttpMethod.Delete, prefix + "/vessels/" + Guid.NewGuid(),
                (object?)new { expectedVersion = 1 })
        };
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("disabled")]
    [InlineData("expired")]
    public async Task EveryReferenceVerbRejectsInvalidatedSession(string invalidation)
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var normalized = IdentityStore.NormalizeEmail(f.ManagerEmail);
        var user = await db.AuthUsers.AsNoTracking()
            .SingleAsync(x => x.NormalizedEmail == normalized);
        var now = DateTimeOffset.UtcNow;
        switch (invalidation)
        {
            case "revoked":
                await db.AuthSessions.Where(s => s.UserId == user.Id).ExecuteUpdateAsync(set =>
                    set.SetProperty(s => s.RevokedAtUtc, now));
                break;
            case "disabled":
                await db.AuthUsers.Where(u => u.Id == user.Id).ExecuteUpdateAsync(set =>
                    set.SetProperty(u => u.Status, "Disabled")
                        .SetProperty(u => u.SecurityVersion, u => u.SecurityVersion + 1));
                break;
            default:
                await db.AuthSessions.Where(s => s.UserId == user.Id).ExecuteUpdateAsync(set =>
                    set.SetProperty(s => s.IdleExpiresAtUtc,
                        s => s.IssuedAtUtc.AddTicks(1)));
                break;
        }
        foreach (var (method, path, body) in Routes(f.A))
        {
            using var result = await ReferenceDataHttpTests.Send(manager, method, path, body);
            Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
            Assert.Contains("authentication_required",
                await result.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ForeignOriginRejectsMutationEvenWithRealCsrfToken()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        using var csrfResponse = await manager.GetAsync("/api/v1/auth/csrf");
        csrfResponse.EnsureSuccessStatusCode();
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/organizations/{f.A}/ports");
        request.Headers.TryAddWithoutValidation("Origin", "https://untrusted-origin.example");
        request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",
            csrf.GetProperty("token").GetString());
        request.Content = JsonContent.Create(new {
            referenceCode = "ORIGIN-FAILED", name = "Denied", countryCode = "US"
        });
        using var result = await manager.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        var body = await result.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("csrf_invalid", body.GetProperty("code").GetString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(0, await db.Ports.CountAsync(p => p.OrganizationId == f.A));
    }
}
