using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HarborOps.Infrastructure;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

/// <summary>
/// Boundary matrices over the real HTTP pipeline and PostgreSQL: anonymous callers, missing
/// and foreign selections on every supported endpoint and verb, malformed input, paging,
/// removal and reinstatement.
/// </summary>
public sealed class OrganizationBoundaryMatrixTests
{
    private const string Password = "Synthetic-G2-boundary-matrix-passphrase-123";
    private const string Correlation = "g2-boundary";

    private sealed class ApiFactory(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            LocalPeerTestServer.Configure(builder);
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:HarborOps", connection);
            builder.UseSetting("Identity:AllowInsecureLoopbackHttp", "true");
            builder.UseSetting("Identity:KeyRingPath",
                Path.Combine(Directory.GetCurrentDirectory(), ".identity-keys"));
        }
    }

    private sealed record Operation(string Name, HttpMethod Method, string Path, object? Body);

    private static HttpClient NewClient(ApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method,
        string path, object? body, bool csrf = true, string? rawBody = null,
        string? contentType = "application/json")
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
        if (csrf)
        {
            using var tokenResponse = await client.GetAsync("/api/v1/auth/csrf");
            tokenResponse.EnsureSuccessStatusCode();
            var token = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
            request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",
                token.GetProperty("token").GetString());
        }
        if (rawBody is not null)
            request.Content = contentType is null
                ? new StringContent(rawBody)
                : new StringContent(rawBody, Encoding.UTF8, contentType);
        else if (body is not null)
            request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task Login(HttpClient client, string email)
    {
        using var response = await Send(client, HttpMethod.Post, "/api/v1/auth/login",
            new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task Select(HttpClient client, Guid organizationId)
    {
        using var response = await Send(client, HttpMethod.Put, "/api/v1/auth/session/organization",
            new { organizationId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The six organization-scoped operations, each targeting the supplied organization
    // and member identifiers.
    private static Operation[] ScopedOperations(Guid organizationId, Guid memberId)
    {
        var org = "/api/v1/organizations/" + organizationId;
        return
        [
            new("read organization", HttpMethod.Get, org, null),
            new("update organization", HttpMethod.Patch, org,
                new { displayName = "Boundary Probe", expectedVersion = 1L }),
            new("list memberships", HttpMethod.Get, org + "/memberships", null),
            new("read membership", HttpMethod.Get, org + "/memberships/" + memberId, null),
            new("change membership", HttpMethod.Put, org + "/memberships/" + memberId,
                new { role = "Analyst", status = "Active", expectedVersion = (long?)null }),
            new("remove membership", HttpMethod.Delete, org + "/memberships/" + memberId,
                new { expectedVersion = 1L })
        ];
    }

    private static async Task<JsonElement> Body(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task ProvisionAll(IdentityStore identity, params string[] emails)
    {
        foreach (var email in emails)
            await identity.ProvisionAsync(email, "Synthetic " + email[..8], Password, CancellationToken.None);
    }

    private static async Task<Guid> UserId(HarborOpsDbContext db, string email) =>
        (await db.AuthUsers.AsNoTracking()
            .SingleAsync(x => x.NormalizedEmail == IdentityStore.NormalizeEmail(email))).Id;

    [Fact]
    public async Task AnonymousCallersAreRejectedOnEveryScopedEndpointAndVerb()
    {
        using var factory = new ApiFactory(TestDatabase.ConnectionString());
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>().Database.MigrateAsync();
        using var anonymous = NewClient(factory);
        var sessionOperations = new Operation[]
        {
            new("list own organizations", HttpMethod.Get, "/api/v1/me/organizations", null),
            new("read selection", HttpMethod.Get, "/api/v1/me/organization-context", null),
            new("select organization", HttpMethod.Put, "/api/v1/auth/session/organization",
                new { organizationId = Guid.NewGuid() }),
            new("clear selection", HttpMethod.Delete, "/api/v1/auth/session/organization", null)
        };
        var operations = ScopedOperations(Guid.NewGuid(), Guid.NewGuid())
            .Concat(sessionOperations).ToArray();
        foreach (var operation in operations)
            foreach (var withToken in new[] { true, false })
            {
                using var response = await Send(anonymous, operation.Method, operation.Path,
                    operation.Body, csrf: withToken);
                Assert.True(HttpStatusCode.Unauthorized == response.StatusCode,
                    operation.Name + " (csrf " + withToken + ") returned " + (int)response.StatusCode);
                var body = await response.Content.ReadAsStringAsync();
                Assert.Contains("authentication_required", body, StringComparison.Ordinal);
                Assert.True(body.Length < 512, "Error envelope must be bounded");
            }
    }

    [Fact]
    public async Task MissingAndForeignSelectionsDenyEveryVerbWithoutEnumeration()
    {
        using var factory = new ApiFactory(TestDatabase.ConnectionString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var store = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var dual = "g2-dual-" + nonce + "@example.test";
        var otherAdmin = "g2-bowner-" + nonce + "@example.test";
        var thirdAdmin = "g2-cowner-" + nonce + "@example.test";
        await ProvisionAll(identity, dual, otherAdmin, thirdAdmin);
        var a = await store.BootstrapAsync("g2-matrix-a-" + nonce[..10], "Matrix A Private", dual,
            CancellationToken.None);
        var b = await store.BootstrapAsync("g2-matrix-b-" + nonce[..10], "Matrix B Private", otherAdmin,
            CancellationToken.None);
        var c = await store.BootstrapAsync("g2-matrix-c-" + nonce[..10], "Matrix C Private", thirdAdmin,
            CancellationToken.None);
        var dualId = await UserId(db, dual);
        var ownerB = (await identity.LoginAsync(otherAdmin, Password, Correlation, CancellationToken.None))!;
        Assert.NotNull((await store.SelectAsync(ownerB.UserId, ownerB.SessionId, b.Id,
            CancellationToken.None)).Value);
        Assert.NotNull((await store.PutMemberAsync(ownerB.UserId, ownerB.SessionId, b.Id, dualId,
            "Analyst", "Active", null, Correlation, CancellationToken.None)).Value);

        using var client = NewClient(factory);
        await Login(client, dual);

        // G2-S07: membership listing works with no selection; every scoped operation returns 409.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me/organizations")).StatusCode);
        var none = await client.GetFromJsonAsync<JsonElement>("/api/v1/me/organization-context");
        Assert.Equal(JsonValueKind.Null, none.GetProperty("selected").ValueKind);
        foreach (var target in new[] { a.Id, b.Id, c.Id, Guid.NewGuid() })
            foreach (var operation in ScopedOperations(target, dualId))
            {
                using var response = await Send(client, operation.Method, operation.Path, operation.Body);
                Assert.True(HttpStatusCode.Conflict == response.StatusCode,
                    operation.Name + " without a selection returned " + (int)response.StatusCode);
                Assert.Equal("organization_selection_required",
                    (await Body(response)).GetProperty("code").GetString());
            }

        // G2-S10 and G2-S13: with A selected, B (where the same account is a member), C and a
        // random identifier are all indistinguishable and denied for every verb.
        await Select(client, a.Id);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/api/v1/organizations/" + a.Id)).StatusCode);
        foreach (var target in new[] { b.Id, c.Id, Guid.NewGuid() })
            foreach (var operation in ScopedOperations(target, ownerB.UserId))
            {
                using var response = await Send(client, operation.Method, operation.Path, operation.Body);
                Assert.True(HttpStatusCode.NotFound == response.StatusCode,
                    operation.Name + " for a foreign organization returned " + (int)response.StatusCode);
                var text = await response.Content.ReadAsStringAsync();
                Assert.DoesNotContain("Private", text, StringComparison.Ordinal);
                Assert.Contains("resource_not_found", text, StringComparison.Ordinal);
            }

        // The same account holds only Analyst in B: role follows the selected membership.
        await Select(client, b.Id);
        var inB = await client.GetFromJsonAsync<JsonElement>("/api/v1/me/organization-context");
        Assert.Equal("Analyst", inB.GetProperty("selected").GetProperty("role").GetString());
        foreach (var operation in ScopedOperations(a.Id, dualId))
        {
            using var response = await Send(client, operation.Method, operation.Path, operation.Body);
            Assert.True(HttpStatusCode.NotFound == response.StatusCode,
                operation.Name + " for A while B is selected returned " + (int)response.StatusCode);
        }
        foreach (var operation in ScopedOperations(b.Id, ownerB.UserId).Skip(1))
        {
            using var response = await Send(client, operation.Method, operation.Path, operation.Body);
            Assert.True(HttpStatusCode.Forbidden == response.StatusCode,
                operation.Name + " as Analyst returned " + (int)response.StatusCode);
        }

        // Selection input validation, and equal 404 envelopes for foreign and nonexistent IDs.
        foreach (var invalid in new object[] {
            new { organizationId = "not-a-guid" },
            new { organizationId = Guid.Empty },
            new { organizationId = (string?)null },
            new { } })
        {
            using var response = await Send(client, HttpMethod.Put, "/api/v1/auth/session/organization", invalid);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var foreignSelect = await Send(client, HttpMethod.Put,
            "/api/v1/auth/session/organization", new { organizationId = c.Id });
        using var missingSelect = await Send(client, HttpMethod.Put,
            "/api/v1/auth/session/organization", new { organizationId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, foreignSelect.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingSelect.StatusCode);
        var foreignBody = await Body(foreignSelect);
        var missingBody = await Body(missingSelect);
        Assert.Equal(foreignBody.GetProperty("code").GetString(), missingBody.GetProperty("code").GetString());
        Assert.Equal(foreignBody.GetProperty("message").GetString(), missingBody.GetProperty("message").GetString());
        var stillB = await client.GetFromJsonAsync<JsonElement>("/api/v1/me/organization-context");
        Assert.Equal(b.Id, stillB.GetProperty("selected").GetProperty("id").GetGuid());

        using (var clear = await Send(client, HttpMethod.Delete, "/api/v1/auth/session/organization", null))
            Assert.Equal(HttpStatusCode.NoContent, clear.StatusCode);
        var cleared = await client.GetFromJsonAsync<JsonElement>("/api/v1/me/organization-context");
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("selected").ValueKind);
        using var afterClear = await client.GetAsync("/api/v1/organizations/" + b.Id);
        Assert.Equal(HttpStatusCode.Conflict, afterClear.StatusCode);
    }

    [Fact]
    public async Task MalformedBodiesIdentifiersAndQueriesFailValidationWithoutWrites()
    {
        using var factory = new ApiFactory(TestDatabase.ConnectionString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var store = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var admin = "g2-valid-admin-" + nonce + "@example.test";
        var target = "g2-valid-target-" + nonce + "@example.test";
        await ProvisionAll(identity, admin, target);
        var org = await store.BootstrapAsync("g2-valid-" + nonce[..12], "Validation Org", admin,
            CancellationToken.None);
        var targetId = await UserId(db, target);
        using var client = NewClient(factory);
        await Login(client, admin);
        await Select(client, org.Id);
        var orgPath = "/api/v1/organizations/" + org.Id;
        var memberPath = orgPath + "/memberships/" + targetId;

        async Task Rejected(HttpMethod method, string path, string raw, string? contentType = "application/json")
        {
            using var response = await Send(client, method, path, null, rawBody: raw, contentType: contentType);
            var text = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnsupportedMediaType,
                method + " " + path + " with `" + raw + "` returned " + (int)response.StatusCode);
            Assert.True(text.Length < 512, "Error envelope must be bounded");
            foreach (var forbidden in new[] { "Exception", "   at ", "Npgsql", "System.", "HarborOps." })
                Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
        }

        foreach (var raw in new[] {
            "{", "[]", "null", "\"text\"",
            "{\"role\":1,\"status\":\"Active\"}",
            "{\"role\":\"Analyst\",\"status\":\"Active\",\"expectedVersion\":\"abc\"}",
            "{\"role\":\"Analyst\",\"status\":\"Active\",\"expectedVersion\":1.5}" })
            await Rejected(HttpMethod.Put, memberPath, raw);
        await Rejected(HttpMethod.Put, memberPath, "", contentType: null);
        foreach (var raw in new[] {
            "{}", "{\"role\":\"\",\"status\":\"Active\"}",
            "{\"role\":\"Analyst\",\"status\":\"Deleted\"}",
            "{\"role\":\"Analyst\",\"status\":\"Active\",\"expectedVersion\":0}",
            "{\"role\":\"Analyst\",\"status\":\"Active\",\"expectedVersion\":-3}",
            "{\"role\":\"analyst\",\"status\":\"Active\"}",
            "{\"role\":\"Analyst\",\"status\":\"active\"}" })
            await Rejected(HttpMethod.Put, memberPath, raw);

        foreach (var raw in new[] { "{", "{}", "{\"expectedVersion\":\"x\"}", "{\"expectedVersion\":0}" })
            await Rejected(HttpMethod.Delete, memberPath, raw);
        await Rejected(HttpMethod.Delete, memberPath, "", contentType: null);

        foreach (var raw in new[] {
            "{", "{}", "{\"displayName\":null,\"expectedVersion\":1}",
            "{\"displayName\":\"\",\"expectedVersion\":1}",
            "{\"displayName\":\"Bad\\u0007Name\",\"expectedVersion\":1}",
            "{\"displayName\":\"" + new string('x', 161) + "\",\"expectedVersion\":1}",
            "{\"displayName\":\"" + new string('x', 100_000) + "\",\"expectedVersion\":1}",
            "{\"displayName\":\"Valid\",\"expectedVersion\":-1}" })
            await Rejected(HttpMethod.Patch, orgPath, raw);

        await Rejected(HttpMethod.Put, "/api/v1/auth/session/organization", "{");

        foreach (var badId in new[] { "not-a-guid", Guid.Empty.ToString(), "1234" })
        {
            foreach (var operation in new[] {
                (HttpMethod.Get, "/api/v1/organizations/" + badId),
                (HttpMethod.Get, "/api/v1/organizations/" + badId + "/memberships"),
                (HttpMethod.Get, "/api/v1/organizations/" + badId + "/memberships/" + targetId),
                (HttpMethod.Get, orgPath + "/memberships/" + badId) })
            {
                using var response = await client.SendAsync(
                    new HttpRequestMessage(operation.Item1, operation.Item2));
                Assert.True(HttpStatusCode.BadRequest == response.StatusCode,
                    operation.Item2 + " returned " + (int)response.StatusCode);
            }
            foreach (var path in new[] {
                "/api/v1/organizations/" + badId + "/memberships/" + targetId,
                orgPath + "/memberships/" + badId })
            {
                using var put = await Send(client, HttpMethod.Put, path,
                    new { role = "Analyst", status = "Active", expectedVersion = (long?)null });
                Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
                using var delete = await Send(client, HttpMethod.Delete, path, new { expectedVersion = 1L });
                Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
            }
            using var patch = await Send(client, HttpMethod.Patch, "/api/v1/organizations/" + badId,
                new { displayName = "Valid", expectedVersion = 1L });
            Assert.Equal(HttpStatusCode.BadRequest, patch.StatusCode);
        }

        foreach (var query in new[] { "limit=0", "limit=101", "limit=abc", "limit=-1", "after=bad",
            "after=" + Guid.Empty })
        {
            using var response = await client.GetAsync(orgPath + "/memberships?" + query);
            Assert.True(HttpStatusCode.BadRequest == response.StatusCode,
                query + " returned " + (int)response.StatusCode);
        }

        // None of the rejected requests wrote anything.
        var after = await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == org.Id);
        Assert.Equal("Validation Org", after.DisplayName);
        Assert.Equal(1, after.Version);
        Assert.Equal(1, await db.OrganizationMemberships.CountAsync(x => x.OrganizationId == org.Id));
        Assert.Equal(0, await db.OrganizationMemberships.CountAsync(x =>
            x.OrganizationId == org.Id && x.UserId == targetId));
    }

    [Fact]
    public async Task MembershipPagingIsBoundedCompleteAndDuplicateFree()
    {
        using var factory = new ApiFactory(TestDatabase.ConnectionString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var store = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var admin = "g2-page-admin-" + nonce + "@example.test";
        await ProvisionAll(identity, admin);
        var org = await store.BootstrapAsync("g2-page-" + nonce[..12], "Paging Org", admin,
            CancellationToken.None);
        var session = (await identity.LoginAsync(admin, Password, Correlation, CancellationToken.None))!;
        Assert.NotNull((await store.SelectAsync(session.UserId, session.SessionId, org.Id,
            CancellationToken.None)).Value);
        var expected = new HashSet<Guid> { session.UserId };
        for (var i = 0; i < 6; i++)
        {
            var email = "g2-page-member-" + i + "-" + nonce + "@example.test";
            await ProvisionAll(identity, email);
            var id = await UserId(db, email);
            Assert.NotNull((await store.PutMemberAsync(session.UserId, session.SessionId, org.Id, id,
                i % 2 == 0 ? "Analyst" : "Auditor", "Active", null, Correlation,
                CancellationToken.None)).Value);
            expected.Add(id);
        }

        using var client = NewClient(factory);
        await Login(client, admin);
        await Select(client, org.Id);
        var seen = new List<Guid>();
        string? after = null;
        for (var page = 0; page < 10; page++)
        {
            var path = "/api/v1/organizations/" + org.Id + "/memberships?limit=3" +
                (after is null ? "" : "&after=" + after);
            var body = await client.GetFromJsonAsync<JsonElement>(path);
            var rows = body.GetProperty("memberships").EnumerateArray().ToList();
            Assert.True(rows.Count <= 3, "Page exceeded the requested limit");
            seen.AddRange(rows.Select(x => x.GetProperty("userId").GetGuid()));
            var next = body.GetProperty("nextAfter");
            if (next.ValueKind == JsonValueKind.Null) break;
            after = next.GetGuid().ToString();
        }
        Assert.Equal(expected.Count, seen.Count);
        Assert.Equal(expected, seen.ToHashSet());
        var firstPage = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/organizations/" + org.Id + "/memberships");
        Assert.Equal(expected.Count, firstPage.GetProperty("memberships").GetArrayLength());
    }

    [Fact]
    public async Task RemovedMemberIsDeniedImmediatelyAndReinstatementRequiresAnExplicitGrant()
    {
        using var factory = new ApiFactory(TestDatabase.ConnectionString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var store = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var admin = "g2-remove-admin-" + nonce + "@example.test";
        var member = "g2-remove-member-" + nonce + "@example.test";
        await ProvisionAll(identity, admin, member);
        var org = await store.BootstrapAsync("g2-remove-" + nonce[..12], "Removal Org", admin,
            CancellationToken.None);
        var memberId = await UserId(db, member);
        using var adminClient = NewClient(factory);
        using var memberClient = NewClient(factory);
        await Login(adminClient, admin);
        await Select(adminClient, org.Id);
        var memberPath = "/api/v1/organizations/" + org.Id + "/memberships/" + memberId;
        using (var grant = await Send(adminClient, HttpMethod.Put, memberPath,
            new { role = "Manager", status = "Active", expectedVersion = (long?)null }))
            Assert.Equal(HttpStatusCode.OK, grant.StatusCode);
        await Login(memberClient, member);
        await Select(memberClient, org.Id);
        Assert.Equal(HttpStatusCode.OK,
            (await memberClient.GetAsync("/api/v1/organizations/" + org.Id)).StatusCode);

        using (var removal = await Send(adminClient, HttpMethod.Delete, memberPath, new { expectedVersion = 1L }))
            Assert.Equal(HttpStatusCode.NoContent, removal.StatusCode);
        using (var denied = await memberClient.GetAsync("/api/v1/organizations/" + org.Id))
            Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        var context = await memberClient.GetFromJsonAsync<JsonElement>("/api/v1/me/organization-context");
        Assert.Equal(JsonValueKind.Null, context.GetProperty("selected").ValueKind);
        var mine = await memberClient.GetFromJsonAsync<JsonElement>("/api/v1/me/organizations");
        Assert.Equal(0, mine.GetProperty("organizations").GetArrayLength());
        using (var reselect = await Send(memberClient, HttpMethod.Put, "/api/v1/auth/session/organization",
            new { organizationId = org.Id }))
            Assert.Equal(HttpStatusCode.NotFound, reselect.StatusCode);
        using (var repeatedRemoval = await Send(adminClient, HttpMethod.Delete, memberPath,
            new { expectedVersion = 2L }))
            Assert.Equal(HttpStatusCode.Conflict, repeatedRemoval.StatusCode);
        using (var staleReinstate = await Send(adminClient, HttpMethod.Put, memberPath,
            new { role = "Manager", status = "Active", expectedVersion = 1L }))
            Assert.Equal(HttpStatusCode.Conflict, staleReinstate.StatusCode);
        using (var blindReinstate = await Send(adminClient, HttpMethod.Put, memberPath,
            new { role = "Manager", status = "Active", expectedVersion = (long?)null }))
            Assert.Equal(HttpStatusCode.Conflict, blindReinstate.StatusCode);
        Assert.Equal("Removed", (await db.OrganizationMemberships.AsNoTracking()
            .SingleAsync(x => x.OrganizationId == org.Id && x.UserId == memberId)).Status);

        using (var reinstate = await Send(adminClient, HttpMethod.Put, memberPath,
            new { role = "Support", status = "Active", expectedVersion = 2L }))
            Assert.Equal(HttpStatusCode.OK, reinstate.StatusCode);
        await Select(memberClient, org.Id);
        var restored = await memberClient.GetFromJsonAsync<JsonElement>("/api/v1/me/organization-context");
        Assert.Equal("Support", restored.GetProperty("selected").GetProperty("role").GetString());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await memberClient.GetAsync("/api/v1/organizations/" + org.Id + "/memberships")).StatusCode);
    }

    [Fact]
    public async Task ConcurrentDuplicateGrantsAndMutualRemovalsLeaveOneWinnerAndAnAdministrator()
    {
        using var provider = new ServiceCollection()
            .AddHarborOpsPersistence(TestDatabase.ConnectionString()).BuildServiceProvider();
        using var seed = provider.CreateScope();
        var db = seed.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        var identity = seed.ServiceProvider.GetRequiredService<IdentityStore>();
        var store = seed.ServiceProvider.GetRequiredService<OrganizationStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var first = "g2-race-first-" + nonce + "@example.test";
        var second = "g2-race-second-" + nonce + "@example.test";
        var target = "g2-race-target-" + nonce + "@example.test";
        await ProvisionAll(identity, first, second, target);
        var org = await store.BootstrapAsync("g2-dup-" + nonce[..12], "Duplicate Races", first,
            CancellationToken.None);
        var a = (await identity.LoginAsync(first, Password, Correlation, CancellationToken.None))!;
        var b = (await identity.LoginAsync(second, Password, Correlation, CancellationToken.None))!;
        var targetId = await UserId(db, target);
        Assert.NotNull((await store.SelectAsync(a.UserId, a.SessionId, org.Id, CancellationToken.None)).Value);
        Assert.NotNull((await store.PutMemberAsync(a.UserId, a.SessionId, org.Id, b.UserId,
            "Administrator", "Active", null, Correlation, CancellationToken.None)).Value);
        Assert.NotNull((await store.SelectAsync(b.UserId, b.SessionId, org.Id, CancellationToken.None)).Value);

        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();
        var storeA = scopeA.ServiceProvider.GetRequiredService<OrganizationStore>();
        var storeB = scopeB.ServiceProvider.GetRequiredService<OrganizationStore>();
        var grants = await Task.WhenAll(
            storeA.PutMemberAsync(a.UserId, a.SessionId, org.Id, targetId, "Analyst", "Active", null,
                Correlation, CancellationToken.None),
            storeB.PutMemberAsync(b.UserId, b.SessionId, org.Id, targetId, "Manager", "Active", null,
                Correlation, CancellationToken.None)).WaitAsync(TimeSpan.FromSeconds(25));
        Assert.Single(grants, x => x.Error == OrganizationAccessError.None);
        Assert.Single(grants, x => x.Error == OrganizationAccessError.Conflict);
        Assert.Single(await db.OrganizationMemberships.AsNoTracking()
            .Where(x => x.OrganizationId == org.Id && x.UserId == targetId).ToListAsync());

        using var removerA = provider.CreateScope();
        using var removerB = provider.CreateScope();
        var removals = await Task.WhenAll(
            removerA.ServiceProvider.GetRequiredService<OrganizationStore>().RemoveMemberAsync(
                a.UserId, a.SessionId, org.Id, b.UserId, 1, Correlation, CancellationToken.None),
            removerB.ServiceProvider.GetRequiredService<OrganizationStore>().RemoveMemberAsync(
                b.UserId, b.SessionId, org.Id, a.UserId, 1, Correlation, CancellationToken.None))
            .WaitAsync(TimeSpan.FromSeconds(25));
        Assert.Single(removals, x => x == OrganizationAccessError.None);
        // The loser is denied: either the winner already removed it (selection cleared or membership
        // revoked) or the last-administrator guard fired.
        Assert.Single(removals, x => x is OrganizationAccessError.Revoked or OrganizationAccessError.Conflict
            or OrganizationAccessError.SelectionRequired);
        var administrators = await db.OrganizationMemberships.AsNoTracking().CountAsync(x =>
            x.OrganizationId == org.Id && x.Role == "Administrator" && x.Status == "Active");
        Assert.Equal(1, administrators);
    }
}
