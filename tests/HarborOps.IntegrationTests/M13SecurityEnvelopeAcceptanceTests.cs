using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// G13 security envelope for the KPI routes: response caching, CSRF and same-origin
// protection, identical answers for foreign and missing resources, the full role
// matrix across catalog, overview and drill, and log hygiene.
[Collection("M13ExclusiveServer")]
public sealed class M13SecurityEnvelopeAcceptanceTests
{
    private const string Password = "Synthetic-reference-integration-passphrase-123";

    private static string Root(Guid org) => $"/api/v1/organizations/{org}/analytics";

    private static async Task<string> ProvisionRoleAsync(
        ReferenceDataHttpTests.ApiFactory factory, string adminEmail, Guid org, string role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var orgs = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var admin = await identity.LoginAsync(adminEmail, Password, "g13-envelope", CancellationToken.None);
        Assert.NotNull(admin);
        var selected = await orgs.SelectAsync(admin!.UserId, admin.SessionId, org, CancellationToken.None);
        Assert.NotNull(selected.Value);
        var email = $"g13-{role.ToLowerInvariant()}-" + Guid.NewGuid().ToString("N") + "@example.test";
        await identity.ProvisionAsync(email, role + " Reader", Password, CancellationToken.None);
        var login = await identity.LoginAsync(email, Password, "g13-envelope", CancellationToken.None);
        Assert.NotNull(login);
        var granted = await orgs.PutMemberAsync(admin.UserId, admin.SessionId, org, login!.UserId,
            role, "Active", null, "g13-envelope", CancellationToken.None);
        Assert.NotNull(granted.Value);
        return email;
    }

    private static async Task<HttpClient> ClientFor(
        ReferenceDataHttpTests.ApiFactory api, string email, Guid org)
    {
        var client = api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(client, email);
        await ReferenceDataHttpTests.Select(client, org);
        return client;
    }

    private static async Task<Guid> UserId(NpgsqlConnection admin, string email)
    {
        await using var find = new NpgsqlCommand(
            "SELECT id FROM users WHERE lower(normalized_email)=lower(@email)", admin);
        find.Parameters.AddWithValue("email", email);
        return Assert.IsType<Guid>(await find.ExecuteScalarAsync());
    }

    private static bool NoStore(HttpResponseMessage response) =>
        response.Headers.CacheControl?.NoStore == true;

    private static HttpRequestMessage Members(Guid org, JsonElement report, string metric)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            Root(org) + "/reports/" + report.GetProperty("reportId").GetGuid() +
            "/members?metricCode=" + metric + "&limit=50");
        request.Headers.TryAddWithoutValidation("X-HarborOps-Report",
            report.GetProperty("reportToken").GetString());
        return request;
    }

    // G13-S23-A03: authenticated KPI responses are never cached, and the only state
    // changing KPI route (the Auditor grant) needs both a CSRF token and a same-origin
    // request. The web tier adds the CSP; the browser suite asserts it on the page.
    [ExclusiveM13PostgresFact]
    public async Task AuthenticatedKpiResponsesAreNoStoreAndStateChangesNeedCsrfAndSameOrigin()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(seedFactory);
        using var seedAdmin = f.Admin; using var seedManager = f.Manager; using var seedAnalyst = f.Analyst;
        await AssignmentHttpTests.Seed(f);
        var auditorEmail = await ProvisionRoleAsync(seedFactory, f.AdminEmail, f.A, "Auditor");
        await using var db = new NpgsqlConnection(roles.Admin);
        await db.OpenAsync();
        var auditor = await UserId(db, auditorEmail);
        using var api = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var analyst = await ClientFor(api, f.AnalystEmail, f.A);
        using var administrator = await ClientFor(api, f.AdminEmail, f.A);

        using (var catalog = await analyst.GetAsync(Root(f.A) + "/catalog"))
        {
            Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
            Assert.True(NoStore(catalog), "catalog response must be no-store");
        }
        JsonElement overview;
        using (var response = await analyst.GetAsync(Root(f.A) + "/overview"))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(NoStore(response), "overview response must be no-store");
            overview = (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        }
        using (var request = Members(f.A, overview, "K01"))
        using (var page = await analyst.SendAsync(request))
        {
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.True(NoStore(page), "member page must be no-store");
        }
        // Refusals are not cacheable either.
        using (var foreign = await analyst.GetAsync(Root(f.B) + "/overview"))
        {
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
            Assert.True(NoStore(foreign));
        }
        using (var forged = Members(f.A, overview, "K01"))
        {
            forged.Headers.Remove("X-HarborOps-Report");
            forged.Headers.TryAddWithoutValidation("X-HarborOps-Report", "forged-token");
            using var rejected = await analyst.SendAsync(forged);
            Assert.True((int)rejected.StatusCode is 400 or 409);
            Assert.True(NoStore(rejected));
        }

        // The grant route: CSRF and same-origin are both mandatory.
        var grant = Root(f.A) + "/grants/" + auditor;
        async Task<HttpStatusCode> PutGrant(bool csrf, string origin)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, grant);
            request.Headers.TryAddWithoutValidation("Origin", origin);
            if (csrf)
            {
                using var token = await administrator.GetAsync("/api/v1/auth/csrf");
                token.EnsureSuccessStatusCode();
                request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",
                    (await token.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
            }
            request.Content = JsonContent.Create(new { enabled = true });
            using var response = await administrator.SendAsync(request);
            return response.StatusCode;
        }
        Assert.Equal(HttpStatusCode.Forbidden, await PutGrant(false, "http://localhost"));
        Assert.Equal(HttpStatusCode.Forbidden, await PutGrant(true, "https://attacker.example"));
        await using (var granted = new NpgsqlCommand(
            "SELECT count(*) FROM operational_kpi_auditor_grants WHERE organization_id=@org AND user_id=@user AND active", db))
        {
            granted.Parameters.AddWithValue("org", f.A);
            granted.Parameters.AddWithValue("user", auditor);
            Assert.Equal(0L, Convert.ToInt64(await granted.ExecuteScalarAsync()));
        }
        Assert.Equal(HttpStatusCode.OK, await PutGrant(true, "http://localhost"));
    }

    // G13-S12-A05: a foreign tenant and a tenant that does not exist are answered
    // identically, so the response is no tenancy oracle.
    [ExclusiveM13PostgresFact]
    public async Task ForeignAndNonexistentKpiResourcesAreIndistinguishable()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(seedFactory);
        using var seedAdmin = f.Admin; using var seedManager = f.Manager; using var seedAnalyst = f.Analyst;
        await AssignmentHttpTests.Seed(f);
        using var api = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var analyst = await ClientFor(api, f.AnalystEmail, f.A);

        async Task<(HttpStatusCode Status, string Code, string Message)> Probe(string path)
        {
            using var response = await analyst.GetAsync(path);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return (response.StatusCode, body.GetProperty("code").GetString()!,
                body.GetProperty("message").GetString()!);
        }
        var missing = Guid.NewGuid();
        foreach (var suffix in new[] { "/overview", "/catalog", "/grants" })
        {
            var foreign = await Probe(Root(f.B) + suffix);
            var nonexistent = await Probe(Root(missing) + suffix);
            Assert.Equal(HttpStatusCode.NotFound, foreign.Status);
            Assert.Equal(nonexistent, foreign);
        }
        using var own = await analyst.GetAsync(Root(f.A) + "/overview");
        var report = (await own.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        // The same report id and token presented under a foreign tenant route and under
        // a tenant route that does not exist answer identically.
        async Task<(HttpStatusCode Status, string Code)> Drill(Guid org)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                Root(org) + "/reports/" + report.GetProperty("reportId").GetGuid() + "/members?metricCode=K01");
            request.Headers.TryAddWithoutValidation("X-HarborOps-Report",
                report.GetProperty("reportToken").GetString());
            using var response = await analyst.SendAsync(request);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return (response.StatusCode, body.GetProperty("code").GetString()!);
        }
        var viaForeign = await Drill(f.B);
        var viaMissing = await Drill(Guid.NewGuid());
        Assert.NotEqual(HttpStatusCode.OK, viaForeign.Status);
        Assert.Equal(viaMissing, viaForeign);
    }

    // G13-S13-A02 and A06: Analyst and Manager view; Administrator, Support and an
    // Auditor without an explicit grant are denied on the catalog, the overview and
    // the drill route alike, and a denied role learns nothing about the report.
    [ExclusiveM13PostgresFact]
    public async Task RoleMatrixCoversCatalogOverviewAndDrillForEveryRole()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(seedFactory);
        using var seedAdmin = f.Admin; using var seedManager = f.Manager; using var seedAnalyst = f.Analyst;
        await AssignmentHttpTests.Seed(f);
        var support = await ProvisionRoleAsync(seedFactory, f.AdminEmail, f.A, "Support");
        var auditor = await ProvisionRoleAsync(seedFactory, f.AdminEmail, f.A, "Auditor");
        using var api = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var analyst = await ClientFor(api, f.AnalystEmail, f.A);
        using var manager = await ClientFor(api, f.ManagerEmail, f.A);
        using var administrator = await ClientFor(api, f.AdminEmail, f.A);
        using var supportClient = await ClientFor(api, support, f.A);
        using var auditorClient = await ClientFor(api, auditor, f.A);

        JsonElement analystReport;
        using (var response = await analyst.GetAsync(Root(f.A) + "/overview"))
            analystReport = (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        foreach (var (name, client) in new[] { ("Analyst", analyst), ("Manager", manager) })
        {
            using var catalog = await client.GetAsync(Root(f.A) + "/catalog");
            Assert.True(catalog.StatusCode == HttpStatusCode.OK, name + " catalog");
            using var overview = await client.GetAsync(Root(f.A) + "/overview");
            Assert.True(overview.StatusCode == HttpStatusCode.OK, name + " overview");
            var own = (await overview.Content.ReadFromJsonAsync<JsonElement>()).Clone();
            using var request = Members(f.A, own, "K01");
            using var drill = await client.SendAsync(request);
            Assert.True(drill.StatusCode == HttpStatusCode.OK, name + " drill");
        }
        foreach (var (name, client) in new[] { ("Administrator", administrator), ("Support", supportClient),
                     ("Auditor without grant", auditorClient) })
        {
            using var catalog = await client.GetAsync(Root(f.A) + "/catalog");
            Assert.True(catalog.StatusCode == HttpStatusCode.Forbidden, name + " catalog");
            using var overview = await client.GetAsync(Root(f.A) + "/overview");
            Assert.True(overview.StatusCode == HttpStatusCode.Forbidden, name + " overview");
            // Even holding another user's report id and token, the drill is refused.
            using var request = Members(f.A, analystReport, "K01");
            using var drill = await client.SendAsync(request);
            // Refused either by the role or by the report binding to its owner: never data.
            Assert.True(drill.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Conflict,
                name + " drill " + (int)drill.StatusCode);
            var text = await drill.Content.ReadAsStringAsync();
            Assert.DoesNotContain("memberKey", text);
            Assert.DoesNotContain("items", text);
        }
    }

    private sealed class CapturingProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Capture(this, categoryName);
        public void Dispose() { }

        private sealed class Capture(CapturingProvider owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner.Entries.Enqueue(category + "|" + logLevel + "|" +
                    formatter(state, exception) + "|" + exception);
        }
    }

    // G13-S23-A04: report tokens, session and CSRF material, and member identifiers
    // never reach the service log, whether a request succeeds, is forged, is refused
    // or targets another tenant.
    [ExclusiveM13PostgresFact]
    public async Task KpiRequestsNeverPutTokensOrMemberIdentifiersInLogs()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(seedFactory);
        using var seedAdmin = f.Admin; using var seedManager = f.Manager; using var seedAnalyst = f.Analyst;
        await AssignmentHttpTests.Seed(f);
        var capture = new CapturingProvider();
        using var api = new ReferenceDataHttpTests.ApiFactory(roles.Api, null,
            services => services.AddLogging(b => b.AddProvider(capture)));
        using var analyst = api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(analyst, f.AnalystEmail);
        await ReferenceDataHttpTests.Select(analyst, f.A);

        var secrets = new List<string>();
        using (var token = await analyst.GetAsync("/api/v1/auth/csrf"))
            secrets.Add((await token.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!);
        using (var response = await analyst.GetAsync(Root(f.A) + "/overview"))
        {
            var report = (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
            secrets.Add(report.GetProperty("reportToken").GetString()!);
            using var request = Members(f.A, report, "K01");
            using var page = await analyst.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            foreach (var item in (await page.Content.ReadFromJsonAsync<JsonElement>())
                         .GetProperty("items").EnumerateArray())
                secrets.Add(item.GetProperty("memberKey").GetGuid().ToString());
            using var forged = Members(f.A, report, "K01");
            forged.Headers.Remove("X-HarborOps-Report");
            forged.Headers.TryAddWithoutValidation("X-HarborOps-Report", "forged-" + Guid.NewGuid().ToString("N"));
            using var rejected = await analyst.SendAsync(forged);
            Assert.True((int)rejected.StatusCode is 400 or 409);
        }
        using (await analyst.GetAsync(Root(f.B) + "/overview")) { }
        using (await analyst.GetAsync(Root(Guid.NewGuid()) + "/overview")) { }
        using var anonymous = api.CreateClient();
        using (await anonymous.GetAsync(Root(f.A) + "/overview")) { }

        var cookies = analyst.DefaultRequestHeaders.TryGetValues("Cookie", out var values)
            ? values.ToArray() : Array.Empty<string>();
        secrets.AddRange(cookies);
        secrets.Add(Password);
        secrets.Add(f.AnalystEmail);
        Assert.NotEmpty(capture.Entries);
        foreach (var entry in capture.Entries)
            foreach (var secret in secrets.Where(x => !string.IsNullOrEmpty(x)))
                Assert.False(entry.Contains(secret, StringComparison.OrdinalIgnoreCase),
                    "A log entry contained protected material: " + secret[..Math.Min(8, secret.Length)] + "...");
    }
}
