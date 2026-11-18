using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// Shared G18 fixture. Two synthetic organizations, the five fixed roles, the real restricted
// harborops_api PostgreSQL role for every HTTP request and a privileged connection that is used
// only to arrange state and to observe results. Nothing here bypasses the HTTP authorization path.
internal sealed class M18World : IDisposable
{
    public const string Password = "Synthetic-m18-incident-passphrase-123";
    public const string OperationHeader = "X-HarborOps-Operation-Id";

    public required M10RuntimeIdentityTests.Roles Roles { get; init; }
    public required ReferenceDataHttpTests.ApiFactory Setup { get; init; }
    public required ReferenceDataHttpTests.ApiFactory Runtime { get; init; }
    public Guid A { get; private set; }
    public Guid B { get; private set; }
    public Dictionary<string, (string Email, Guid Id)> Users { get; } = new();
    private readonly List<HttpClient> clients = [];

    public static void RequireExclusive()
    {
        if (Environment.GetEnvironmentVariable("HARBOROPS_G12_EXCLUSIVE_POSTGRES") != "1")
            throw new InvalidOperationException(
                "Requires HARBOROPS_G12_EXCLUSIVE_POSTGRES=1 on an exclusive disposable PostgreSQL server");
    }

    public static async Task<M18World> CreateAsync(
        IReadOnlyDictionary<string, string>? extraSettings = null)
    {
        RequireExclusive();
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        var settings = new Dictionary<string, string>
        {
            ["M18:EnableIncidents"] = "true",
            ["M18:RequestsPerMinute"] = "600"
        };
        if (extraSettings is not null)
            foreach (var (key, value) in extraSettings) settings[key] = value;
        var world = new M18World
        {
            Roles = roles,
            Setup = new ReferenceDataHttpTests.ApiFactory(roles.Admin, settings: settings),
            Runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api, settings: settings)
        };
        await world.SeedAsync();
        return world;
    }

    private async Task SeedAsync()
    {
        await using var scope = Setup.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await db.Database.MigrateAsync();
        // The readiness check that backs verified recovery requires the baseline record.
        await BootstrapSeeder.SeedAsync(db, CancellationToken.None);
        var ids = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var organizations = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var names = new[] { "admin", "support", "support2", "manager", "analyst", "auditor",
            "adminb", "supportb", "multi" };
        foreach (var name in names)
        {
            var email = $"m18-{name}-{nonce}@example.test";
            await ids.ProvisionAsync(email, "Synthetic M18 " + name, Password, CancellationToken.None);
            var session = (await ids.LoginAsync(email, Password, "m18-setup", CancellationToken.None))!;
            Users[name] = (email, session.UserId);
        }
        var orgA = await organizations.BootstrapAsync("m18-a-" + nonce[..10], "M18 Alpha",
            Users["admin"].Email, CancellationToken.None);
        var orgB = await organizations.BootstrapAsync("m18-b-" + nonce[..10], "M18 Beta",
            Users["adminb"].Email, CancellationToken.None);
        A = orgA.Id; B = orgB.Id;
        await AddMembers(ids, organizations, "admin", A, [
            ("support", "Support"), ("support2", "Support"), ("manager", "Manager"),
            ("analyst", "Analyst"), ("auditor", "Auditor"), ("multi", "Support")]);
        await AddMembers(ids, organizations, "adminb", B, [
            ("supportb", "Support"), ("multi", "Support")]);
    }

    private async Task AddMembers(IdentityStore ids, OrganizationStore organizations,
        string admin, Guid org, (string User, string Role)[] members)
    {
        var session = (await ids.LoginAsync(Users[admin].Email, Password, "m18-setup",
            CancellationToken.None))!;
        Assert.NotNull((await organizations.SelectAsync(session.UserId, session.SessionId, org,
            CancellationToken.None)).Value);
        foreach (var (user, role) in members)
            Assert.NotNull((await organizations.PutMemberAsync(session.UserId, session.SessionId,
                org, Users[user].Id, role, "Active", null, "m18-setup", CancellationToken.None)).Value);
    }

    public async Task<HttpClient> Client(string user, Guid? org = null, bool select = true)
    {
        var client = Runtime.CreateClient(new WebApplicationFactoryClientOptions
            { HandleCookies = true, AllowAutoRedirect = false });
        clients.Add(client);
        using (var login = await Send(client, HttpMethod.Post, "/api/v1/auth/login",
            new { email = Users[user].Email, password = Password }, operationHeader: false))
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        if (select) await ReferenceDataHttpTests.Select(client, org ?? A);
        return client;
    }

    public static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method,
        string path, object? payload = null, Guid? operation = null, bool csrf = true,
        string origin = "http://localhost", bool operationHeader = true)
    {
        using var request = new HttpRequestMessage(method, path);
        if (origin.Length > 0) request.Headers.TryAddWithoutValidation("Origin", origin);
        if (csrf)
        {
            using var tokenResponse = await client.GetAsync("/api/v1/auth/csrf");
            tokenResponse.EnsureSuccessStatusCode();
            var json = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
            request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",
                json.GetProperty("token").GetString());
        }
        if (operationHeader && method != HttpMethod.Get)
            request.Headers.TryAddWithoutValidation(OperationHeader,
                (operation ?? Guid.NewGuid()).ToString("D"));
        if (payload is not null) request.Content = JsonContent.Create(payload);
        return await client.SendAsync(request);
    }

    public string Route(Guid? org = null, string suffix = "") =>
        $"/api/v1/organizations/{org ?? A}/incidents{suffix}";

    public static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    public static object NewIncident(string title = "Database readiness degraded",
        string service = "database", string severity = "SEV-2", string impact = "reduced_capacity",
        DateTimeOffset? detected = null) => new
        {
            title, serviceCode = service, severity, impactCode = impact,
            detectedAtUtc = (detected ?? DateTimeOffset.UtcNow.AddMinutes(-3)).ToString("O")
        };

    public async Task<(Guid Id, long Version)> CreateIncident(HttpClient client,
        object? body = null, Guid? org = null, Guid? operation = null)
    {
        using var response = await Send(client, HttpMethod.Post, Route(org), body ?? NewIncident(),
            operation);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await Json(response);
        return (json.GetProperty("id").GetGuid(), json.GetProperty("version").GetInt64());
    }

    public async Task<long> Post(HttpClient client, Guid incident, string suffix, object body,
        HttpMethod? method = null, HttpStatusCode expected = HttpStatusCode.OK, Guid? operation = null)
    {
        using var response = await Send(client, method ?? HttpMethod.Post,
            Route(null, "/" + incident + suffix), body, operation);
        Assert.True(expected == response.StatusCode,
            $"{method ?? HttpMethod.Post} {suffix} expected {(int)expected} but got {(int)response.StatusCode}: " +
            await response.Content.ReadAsStringAsync());
        return expected == HttpStatusCode.OK
            ? (await Json(response)).GetProperty("version").GetInt64() : 0;
    }

    // ---- observation through the privileged connection (never used to change state under test)

    public async Task<T> Scalar<T>(string sql, params (string Name, object Value)[] values)
    {
        await using var connection = new NpgsqlConnection(Roles.Admin);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default! : (T)Convert.ChangeType(result, typeof(T));
    }

    public async Task Exec(string sql, params (string Name, object Value)[] values)
    {
        await using var connection = new NpgsqlConnection(Roles.Admin);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    public Task<long> AuditCount(Guid org, string? resourceType = null) => Scalar<long>(
        "SELECT count(*) FROM audit_entries WHERE organization_id=@o AND (@t::text IS NULL OR resource_type=@t)",
        ("o", org), ("t", (object?)resourceType ?? DBNull.Value));

    public Task<long> EventCount(Guid incident) => Scalar<long>(
        "SELECT count(*) FROM it_incident_events WHERE incident_id=@i", ("i", incident));

    public void Dispose()
    {
        foreach (var client in clients) client.Dispose();
        Setup.Dispose();
        Runtime.Dispose();
    }
}
