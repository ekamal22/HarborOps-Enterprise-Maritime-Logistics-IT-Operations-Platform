using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Api.Audit;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class ReferenceDataHttpTests
{
    private const string Password = "Synthetic-reference-integration-passphrase-123";
    internal static readonly string CorrectionProofKey = Convert.ToBase64String(
        System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    internal static readonly string ExportProofKey = Convert.ToBase64String(
        System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    internal sealed class ApiFactory(string connection, int? pendingLimit = null,
        Action<IServiceCollection>? overrideServices = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            LocalPeerTestServer.Configure(builder);
            if (overrideServices is not null)
                builder.ConfigureTestServices(overrideServices);
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:HarborOps", connection);
            builder.UseSetting("M10CorrectionProof:Key", CorrectionProofKey);
            builder.UseSetting("M12ExportProof:Key", ExportProofKey);
            if (pendingLimit.HasValue)
                builder.UseSetting("M8:PendingLimit", pendingLimit.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("Identity:AllowInsecureLoopbackHttp", "true");
            builder.UseSetting("Identity:KeyRingPath",
                Path.Combine(Directory.GetCurrentDirectory(), ".identity-keys"));
        }
    }

    internal static async Task<HttpResponseMessage> Send(
        HttpClient client, HttpMethod method, string path, object? payload = null, bool csrf = true)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
        if (csrf)
        {
            using var tokenResponse = await client.GetAsync("/api/v1/auth/csrf");
            tokenResponse.EnsureSuccessStatusCode();
            var json = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
            request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",
                json.GetProperty("token").GetString());
        }
        if (payload is not null) request.Content = JsonContent.Create(payload);
        return await client.SendAsync(request);
    }

    internal static async Task Login(HttpClient client, string email)
    {
        using var response = await Send(client, HttpMethod.Post, "/api/v1/auth/login",
            new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    internal static async Task Select(HttpClient client, Guid orgId)
    {
        using var response = await Send(client, HttpMethod.Put,
            "/api/v1/auth/session/organization", new { organizationId = orgId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    internal sealed record Fixture(Guid A, Guid B, string AdminEmail, string ManagerEmail,
        string AnalystEmail, HttpClient Admin, HttpClient Manager, HttpClient Analyst);

    // migrateToHead:false is for historical-upgrade tests whose schema is deliberately held at an
    // earlier migration before seeding; the default always migrates to the current head.
    internal static async Task<Fixture> Setup(ApiFactory factory, bool migrateToHead = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        if (migrateToHead) await db.Database.MigrateAsync();
        await AuditRoleProvisioner.ProvisionCorrectionKeyAsync(db,
            CorrectionProofKey, CancellationToken.None);
        var ids = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var organizations = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var nonce = Guid.NewGuid().ToString("N");
        var admin = "m3-admin-" + nonce + "@example.test";
        var adminB = "m3-adminb-" + nonce + "@example.test";
        var manager = "m3-manager-" + nonce + "@example.test";
        var analyst = "m3-analyst-" + nonce + "@example.test";
        foreach (var email in new[] { admin, adminB, manager, analyst })
            await ids.ProvisionAsync(email, "Synthetic Reference User", Password, CancellationToken.None);
        var a = await organizations.BootstrapAsync("m3-ref-a-" + nonce[..10],
            "Reference Test A", admin, CancellationToken.None);
        var b = await organizations.BootstrapAsync("m3-ref-b-" + nonce[..10],
            "Reference Test B", adminB, CancellationToken.None);
        var actor = (await ids.LoginAsync(admin, Password, "m3-ref-setup", CancellationToken.None))!;
        var managerId = (await ids.LoginAsync(manager, Password, "m3-ref-setup", CancellationToken.None))!.UserId;
        var analystId = (await ids.LoginAsync(analyst, Password, "m3-ref-setup", CancellationToken.None))!.UserId;
        Assert.NotNull((await organizations.SelectAsync(actor.UserId, actor.SessionId, a.Id,
            CancellationToken.None)).Value);
        Assert.NotNull((await organizations.PutMemberAsync(actor.UserId, actor.SessionId,
            a.Id, managerId, "Manager", "Active", null, "m3-ref-setup", CancellationToken.None)).Value);
        Assert.NotNull((await organizations.PutMemberAsync(actor.UserId, actor.SessionId,
            a.Id, analystId, "Analyst", "Active", null, "m3-ref-setup", CancellationToken.None)).Value);
        var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var managerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var analystClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await Login(adminClient, admin);
        await Login(managerClient, manager);
        await Login(analystClient, analyst);
        await Select(adminClient, a.Id);
        await Select(managerClient, a.Id);
        await Select(analystClient, a.Id);
        return new(a.Id, b.Id, admin, manager, analyst, adminClient, managerClient, analystClient);
    }

    [Fact]
    public async Task ScopedPortAndVesselLifecycleAndRoleMatrix()
    {
        using var factory = new ApiFactory(TestDatabase.ConnectionString());
        var x = await Setup(factory);
        using var admin = x.Admin;
        using var manager = x.Manager;
        using var analyst = x.Analyst;
        var port = "/api/v1/organizations/" + x.A + "/ports";
        var vessel = "/api/v1/organizations/" + x.A + "/vessels";
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync(port)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await manager.GetAsync("/api/v1/organizations/" + x.B + "/ports")).StatusCode);

        using var createdPort = await Send(manager, HttpMethod.Post, port,
            new { referenceCode = " us-nyc ", name = "New York Terminal", countryCode = "us",
                unLocode = "US NYC" });
        Assert.Equal(HttpStatusCode.Created, createdPort.StatusCode);
        var portJson = await createdPort.Content.ReadFromJsonAsync<JsonElement>();
        var portId = portJson.GetProperty("id").GetGuid();
        Assert.Equal("US-NYC", portJson.GetProperty("referenceCode").GetString());
        Assert.Equal("USNYC", portJson.GetProperty("unLocode").GetString());
        Assert.Equal(x.A, portJson.GetProperty("organizationId").GetGuid());
        Assert.Equal(1L, portJson.GetProperty("version").GetInt64());
        Assert.Equal(HttpStatusCode.OK, (await analyst.GetAsync(port)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(analyst, HttpMethod.Post, port,
                new { referenceCode = "ROGUE", name = "Rogue", countryCode = "US" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await Send(manager, HttpMethod.Post, port,
                new { referenceCode = "US-NYC", name = "Duplicate", countryCode = "US" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Send(manager, HttpMethod.Post, port,
                new { referenceCode = "BAD-LOC", name = "Wrong country", countryCode = "GB",
                    unLocode = "USNYC" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(manager, HttpMethod.Post, port,
                new { referenceCode = "INVALID", name = "Invalid", countryCode = "US" }, csrf: false)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await manager.GetAsync(port + "/" + Guid.NewGuid())).StatusCode);

        using var editPort = await Send(manager, HttpMethod.Patch, port + "/" + portId,
            new { name = "New York Reference", expectedVersion = 1L });
        Assert.Equal(HttpStatusCode.OK, editPort.StatusCode);
        Assert.Equal("New York Reference",
            (await editPort.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.Conflict,
            (await Send(manager, HttpMethod.Patch, port + "/" + portId,
                new { name = "Stale", expectedVersion = 1L })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await Send(manager, HttpMethod.Delete, port + "/" + portId,
                new { expectedVersion = 2L })).StatusCode);
        var defaultList = await manager.GetFromJsonAsync<JsonElement>(port);
        Assert.Empty(defaultList.GetProperty("ports").EnumerateArray());
        var archived = await manager.GetFromJsonAsync<JsonElement>(port + "?status=Archived");
        Assert.Single(archived.GetProperty("ports").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK,
            (await Send(manager, HttpMethod.Patch, port + "/" + portId,
                new { status = "Active", expectedVersion = 3L })).StatusCode);

        using var createdVessel = await Send(manager, HttpMethod.Post, vessel,
            new { referenceCode = "ship-1", name = "Synthetic Vessel",
                imoNumber = "IMO 9074729", mmsi = "012345678", flagCountryCode = "bd" });
        Assert.Equal(HttpStatusCode.Created, createdVessel.StatusCode);
        var ship = await createdVessel.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("9074729", ship.GetProperty("imoNumber").GetString());
        Assert.Equal("012345678", ship.GetProperty("mmsi").GetString());
        Assert.Equal("BD", ship.GetProperty("flagCountryCode").GetString());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Send(manager, HttpMethod.Post, vessel,
                new { referenceCode = "bad-imo", name = "Invalid", imoNumber = "9074728" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await Send(manager, HttpMethod.Post, vessel,
                new { referenceCode = "ship-2", name = "Duplicate IMO", imoNumber = "9074729" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await analyst.GetAsync(vessel)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync(vessel)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await manager.GetAsync("/api/v1/organizations/" + x.B + "/vessels/" +
                ship.GetProperty("id").GetGuid())).StatusCode);
    }

    [Fact]
    public async Task CompetingUpdatesAndImmediateRoleRevocation()
    {
        using var factory = new ApiFactory(TestDatabase.ConnectionString());
        var x = await Setup(factory);
        using var admin = x.Admin;
        using var manager = x.Manager;
        using var analyst = x.Analyst;
        var port = "/api/v1/organizations/" + x.A + "/ports";
        using var created = await Send(manager, HttpMethod.Post, port,
            new { referenceCode = "RACE-1", name = "Before", countryCode = "US" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var second = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await Login(second, x.ManagerEmail);
        await Select(second, x.A);
        var requests = await Task.WhenAll(
            Send(manager, HttpMethod.Patch, port + "/" + id,
                new { name = "First", expectedVersion = 1L }),
            Send(second, HttpMethod.Patch, port + "/" + id,
                new { name = "Second", expectedVersion = 1L }));
        var outcomes = requests.Select(r => r.StatusCode).Order().ToArray();
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }.Order(), outcomes);
        foreach (var response in requests) response.Dispose();
        var afterRace = await manager.GetFromJsonAsync<JsonElement>(port + "/" + id);
        Assert.Equal(2L, afterRace.GetProperty("version").GetInt64());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var member = await db.OrganizationMemberships.SingleAsync(m =>
            m.OrganizationId == x.A && m.Role == "Manager");
        using var revoke = await Send(admin, HttpMethod.Delete,
            "/api/v1/organizations/" + x.A + "/memberships/" + member.UserId,
            new { expectedVersion = member.Version });
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await manager.GetAsync(port)).StatusCode);
        var denied = await Send(manager, HttpMethod.Patch, port + "/" + id,
            new { name = "Forbidden", expectedVersion = 2L });
        Assert.NotEqual(HttpStatusCode.OK, denied.StatusCode);
        var actual = await db.Ports.AsNoTracking().SingleAsync(v => v.Id == id);
        Assert.NotEqual("Forbidden", actual.Name);
    }

    [Fact]
    public async Task MigrationConstraintsEnforceTenantOwnershipAndExternalIdentifiers()
    {
        using var factory = new ApiFactory(TestDatabase.ConnectionString());
        var x = await Setup(factory);
        using var admin = x.Admin;
        using var manager = x.Manager;
        using var analyst = x.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.True(await db.Database.CanConnectAsync());
        var schemaPorts = await db.Ports.AsNoTracking().CountAsync(x => x.OrganizationId == Guid.Empty);
        Assert.Equal(0, schemaPorts);
        var routes = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains("20261031110000_PortVesselReferences", routes);

        var port = "/api/v1/organizations/" + x.A + "/ports";
        using var created = await Send(manager, HttpMethod.Post, port,
            new { referenceCode = "UNIQUE-1", name = "Synthetic Port", countryCode = "US", unLocode = "USNYC" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await Send(manager, HttpMethod.Post, port,
                new { referenceCode = "UNIQUE-2", name = "Collision", countryCode = "US",
                    unLocode = "USNYC" })).StatusCode);
    }
}
