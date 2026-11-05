using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

// G16-S26: the audit list appends an audit.read row on every read, so offsets are
// unstable. These tests walk the keyset cursor while writes and reads continue.
public sealed class AuditCursorTests
{
    private const string Password = "Synthetic-audit-cursor-passphrase-123";

    private sealed class CursorFactory(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            LocalPeerTestServer.Configure(builder);
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:HarborOps", connection);
            builder.UseSetting("Identity:AllowInsecureLoopbackHttp", "true");
            builder.UseSetting("Identity:KeyRingPath",
                Path.Combine(Directory.GetCurrentDirectory(), ".identity-keys-cursor"));
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method,
        string url, object payload)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        using var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
        request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF", csrf.GetProperty("token").GetString());
        request.Content = JsonContent.Create(payload);
        return await client.SendAsync(request);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        using var response = await SendAsync(client, HttpMethod.Post, "/api/v1/auth/login",
            new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string Base64Url(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Fact]
    public async Task CursorWalkHasNoDuplicatesOrGapsAndRejectsTamperedOrCrossTenantCursors()
    {
        using var factory = new CursorFactory(TestDatabase.ConnectionString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identities = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var organizations = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var token = Guid.NewGuid().ToString("N");
        var adminEmail = "cursor-admin-" + token + "@example.test";
        var auditorEmail = "cursor-auditor-" + token + "@example.test";
        await identities.ProvisionAsync(adminEmail, "Cursor Administrator", Password, CancellationToken.None);
        await identities.ProvisionAsync(auditorEmail, "Cursor Auditor", Password, CancellationToken.None);
        var orgA = await organizations.BootstrapAsync("cursor-a-" + token[..12], "Cursor A", adminEmail, CancellationToken.None);
        var orgB = await organizations.BootstrapAsync("cursor-b-" + token[..12], "Cursor B", adminEmail, CancellationToken.None);
        var auditorId = await db.AuthUsers.AsNoTracking().Where(u => u.Email == auditorEmail)
            .Select(u => u.Id).SingleAsync();
        var session = (await identities.LoginAsync(adminEmail, Password, "cursor-setup", CancellationToken.None))!;
        foreach (var org in new[] { orgA.Id, orgB.Id })
        {
            Assert.NotNull((await organizations.SelectAsync(session.UserId, session.SessionId, org,
                CancellationToken.None)).Value);
            Assert.NotNull((await organizations.PutMemberAsync(session.UserId, session.SessionId, org,
                auditorId, "Auditor", "Active", null, "cursor-grant", CancellationToken.None)).Value);
        }

        // Build history in organization A so the walk crosses several pages.
        for (var i = 0; i < 60; i++)
        {
            var version = await db.Organizations.AsNoTracking().Where(o => o.Id == orgA.Id)
                .Select(o => o.Version).SingleAsync();
            Assert.Equal(1, await db.Organizations.Where(o => o.Id == orgA.Id)
                .ExecuteUpdateAsync(p => p.SetProperty(o => o.DisplayName, "Cursor A " + i)
                    .SetProperty(o => o.Version, version + 1)
                    .SetProperty(o => o.UpdatedAtUtc, DateTimeOffset.UtcNow)));
        }

        using var auditor = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await LoginAsync(auditor, auditorEmail);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(auditor, HttpMethod.Put,
            "/api/v1/auth/session/organization", new { organizationId = orgA.Id })).StatusCode);
        var route = "/api/v1/organizations/" + orgA.Id + "/audit-events";

        var snapshot = (await db.AuditEntries.AsNoTracking().Where(e => e.OrganizationId == orgA.Id)
            .Select(e => e.Id).ToListAsync()).ToHashSet();
        Assert.True(snapshot.Count >= 60);

        // Concurrent writers keep appending audit rows while the walk proceeds.
        using var stop = new CancellationTokenSource();
        var writer = Task.Run(async () =>
        {
            await using var writerScope = factory.Services.CreateAsyncScope();
            var writerDb = writerScope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            for (var i = 0; !stop.IsCancellationRequested; i++)
            {
                var version = await writerDb.Organizations.AsNoTracking().Where(o => o.Id == orgA.Id)
                    .Select(o => o.Version).SingleAsync();
                await writerDb.Organizations.Where(o => o.Id == orgA.Id)
                    .ExecuteUpdateAsync(p => p.SetProperty(o => o.DisplayName, "Concurrent " + i)
                        .SetProperty(o => o.Version, version + 1)
                        .SetProperty(o => o.UpdatedAtUtc, DateTimeOffset.UtcNow));
                await Task.Delay(15);
            }
        });

        var seen = new List<string>();
        var times = new List<(DateTimeOffset At, Guid Id)>();
        string? cursor = null;
        string? firstCursor = null;
        for (var guard = 0; guard < 12; guard++)
        {
            var url = route + "?limit=20" + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor));
            using var response = await auditor.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var entry in body.RootElement.GetProperty("entries").EnumerateArray())
            {
                Assert.Equal(orgA.Id.ToString("D"), entry.GetProperty("organizationId").GetString());
                seen.Add(entry.GetProperty("id").GetString()!);
                times.Add((entry.GetProperty("occurredAtUtc").GetDateTimeOffset(),
                    entry.GetProperty("id").GetGuid()));
            }
            var next = body.RootElement.GetProperty("nextCursor");
            if (next.ValueKind == JsonValueKind.Null) { cursor = null; break; }
            cursor = next.GetString();
            firstCursor ??= cursor;
        }
        stop.Cancel();
        await writer;
        Assert.Null(cursor);

        Assert.Equal(seen.Count, seen.Distinct().Count());
        var walked = seen.Select(Guid.Parse).ToHashSet();
        Assert.True(snapshot.IsSubsetOf(walked),
            "Every entry that existed before the walk must be returned exactly once");
        for (var i = 1; i < times.Count; i++)
            Assert.True(times[i - 1].At > times[i].At ||
                (times[i - 1].At == times[i].At && times[i - 1].Id.CompareTo(times[i].Id) > 0),
                "Entries must be strictly descending by time and id");

        // Tampered and cross-tenant cursors.
        Assert.NotNull(firstCursor);
        using (var one = await auditor.GetAsync(route + "?limit=1"))
        {
            using var body = JsonDocument.Parse(await one.Content.ReadAsStringAsync());
            var validCursor = body.RootElement.GetProperty("nextCursor").GetString()!;
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(
                validCursor.Replace('-', '+').Replace('_', '/').PadRight((validCursor.Length + 3) / 4 * 4, '=')))
                .Split('|');
            Assert.Equal(3, parts.Length);
            Assert.Equal(orgA.Id.ToString("D"), parts[0]);

            foreach (var bad in new[]
            {
                Base64Url(string.Join("|", orgB.Id.ToString("D"), parts[1], parts[2])),
                Base64Url(string.Join("|", parts[0], "not-a-number", parts[2])),
                Base64Url(string.Join("|", parts[0], "-5", parts[2])),
                Base64Url(string.Join("|", parts[0], parts[1], Guid.Empty.ToString("D"))),
                Base64Url(string.Join("|", parts[0], parts[1], parts[2], "extra")),
                Base64Url(string.Join("|", parts[0], parts[1])),
                "AAAA!!!!",
                new string('A', 161)
            })
            {
                using var rejected = await auditor.GetAsync(route + "?limit=5&cursor=" + Uri.EscapeDataString(bad));
                Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            }

            // A cursor issued for organization A is useless against organization B.
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(auditor, HttpMethod.Put,
                "/api/v1/auth/session/organization", new { organizationId = orgB.Id })).StatusCode);
            using var cross = await auditor.GetAsync("/api/v1/organizations/" + orgB.Id +
                "/audit-events?limit=5&cursor=" + Uri.EscapeDataString(validCursor));
            Assert.Equal(HttpStatusCode.BadRequest, cross.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(auditor, HttpMethod.Put,
                "/api/v1/auth/session/organization", new { organizationId = orgA.Id })).StatusCode);
            using var good = await auditor.GetAsync(route + "?limit=5&cursor=" + Uri.EscapeDataString(validCursor));
            Assert.Equal(HttpStatusCode.OK, good.StatusCode);
        }

        // Boundaries of the supported query contract.
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync(route + "?limit=1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync(route + "?limit=100")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await auditor.GetAsync(route + "?limit=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await auditor.GetAsync(route + "?limit=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await auditor.GetAsync(route + "?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await auditor.GetAsync(route + "?page=2")).StatusCode);
    }
}
