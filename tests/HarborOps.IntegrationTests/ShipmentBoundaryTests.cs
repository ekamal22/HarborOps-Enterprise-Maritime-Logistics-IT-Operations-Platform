using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.References;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class ShipmentBoundaryTests
{
    private static string Root(Guid org) => $"/api/v1/organizations/{org}/shipments";
    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method,
        string url, object? body = null, bool csrf = true) =>
        ReferenceDataHttpTests.Send(client, method, url, body, csrf);
    private static async Task<Guid> Create(HttpClient client, string url, object body)
    {
        using var response = await Send(client, HttpMethod.Post, url, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private static async Task<(Guid A, Guid B)> Parents(HttpClient manager, Guid org)
    {
        var root = $"/api/v1/organizations/{org}/ports";
        var a = await Create(manager, root, new {
            referenceCode = "G5-ORIGIN", name = "G5 Origin", countryCode = "US" });
        var b = await Create(manager, root, new {
            referenceCode = "G5-DEST", name = "G5 Destination", countryCode = "US" });
        return (a, b);
    }
    private static async Task<Guid> Seed(HttpClient manager, Guid org, Guid a, Guid b,
        string code = "G5-BASE") =>
        await Create(manager, Root(org), new { referenceCode = code,
            originPortId = a, destinationPortId = b });

    [Theory]
    [InlineData("revoked")]
    [InlineData("disabled")]
    [InlineData("security_version")]
    [InlineData("idle_expired")]
    [InlineData("absolute_expired")]
    public async Task AllShipmentRoutesRejectInvalidatedAuthentication(string kind)
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var analyst = f.Analyst;
        using var manager = f.Manager;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var user = await db.AuthUsers.AsNoTracking().SingleAsync(x =>
            x.NormalizedEmail == IdentityStore.NormalizeEmail(f.ManagerEmail));
        var now = DateTimeOffset.UtcNow;
        if (kind == "disabled")
            await db.AuthUsers.Where(x => x.Id == user.Id).ExecuteUpdateAsync(x =>
                x.SetProperty(u => u.Status, "Disabled"));
        else if (kind == "security_version")
            await db.AuthUsers.Where(x => x.Id == user.Id).ExecuteUpdateAsync(x =>
                x.SetProperty(u => u.SecurityVersion, u => u.SecurityVersion + 1));
        else if (kind == "revoked")
            await db.AuthSessions.Where(x => x.UserId == user.Id).ExecuteUpdateAsync(x =>
                x.SetProperty(u => u.RevokedAtUtc, now));
        else if (kind == "absolute_expired")
        {
            var older = now.AddDays(-2);
            await db.AuthSessions.Where(x => x.UserId == user.Id).ExecuteUpdateAsync(u =>
                u.SetProperty(s => s.IssuedAtUtc, older)
                    .SetProperty(s => s.LastSeenAtUtc, older.AddMinutes(5))
                    .SetProperty(s => s.IdleExpiresAtUtc, older.AddMinutes(20))
                    .SetProperty(s => s.AbsoluteExpiresAtUtc, older.AddHours(1)));
        }
        else
        {
            var sessions = await db.AuthSessions.AsNoTracking()
                .Where(x => x.UserId == user.Id)
                .Select(x => new { x.Id, x.IssuedAtUtc }).ToListAsync();
            foreach (var session in sessions)
                await db.AuthSessions.Where(x => x.Id == session.Id).ExecuteUpdateAsync(u =>
                    u.SetProperty(s => s.IdleExpiresAtUtc,
                        session.IssuedAtUtc.AddMilliseconds(1)));
        }
        var root = Root(f.A);
        var id = Guid.NewGuid();
        var routes = new (HttpMethod Method, string Route, object? Body)[] {
            (HttpMethod.Get,root,null),
            (HttpMethod.Get,root+"/"+id,null),
            (HttpMethod.Get,root+"/"+id+"/status-changes",null),
            (HttpMethod.Post,root,new { referenceCode="G5-AUTH",
                originPortId=id,destinationPortId=Guid.NewGuid() }),
            (HttpMethod.Patch,root+"/"+id,new { workflowStatus="Booked",expectedVersion=1 }),
            (HttpMethod.Delete,root+"/"+id,new { expectedVersion=1 })
        };
        foreach (var (method,url,body) in routes)
        {
            using var response = await Send(manager, method, url, body, csrf:false);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains("authentication_required", await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task SelectedOrganizationAndRoleChangesFailClosed()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var root = Root(f.A);
        using (var foreign = await manager.GetAsync(Root(f.B)))
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var user = await db.AuthUsers.AsNoTracking().SingleAsync(x =>
            x.NormalizedEmail == IdentityStore.NormalizeEmail(f.ManagerEmail));
        await db.OrganizationMemberships.Where(m => m.OrganizationId == f.A && m.UserId == user.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Status, "Removed")
                .SetProperty(m => m.Version, m => m.Version + 1));
        using (var removed = await manager.GetAsync(root))
            Assert.Equal(HttpStatusCode.Forbidden, removed.StatusCode);
        await db.OrganizationMemberships.Where(m => m.OrganizationId == f.A && m.UserId == user.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Status, "Active")
                .SetProperty(m => m.Version, m => m.Version + 1));
        await db.Organizations.Where(o => o.Id == f.A).ExecuteUpdateAsync(u =>
            u.SetProperty(o => o.Status, "Suspended"));
        using var suspended = await manager.GetAsync(root);
        Assert.Equal(HttpStatusCode.Forbidden, suspended.StatusCode);
    }

    [Fact]
    public async Task ForeignAndArchivedPortsRejectShipmentCreation()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var p = await Parents(manager, f.A);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var now = DateTimeOffset.UtcNow;
        var foreign = new PortReference {
            Id = Guid.NewGuid(), OrganizationId = f.B, ReferenceCode = "G5-FOREIGN",
            Name = "Foreign Port", CountryCode = "US",
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.Ports.Add(foreign);
        await db.SaveChangesAsync();
        using (var denied = await Send(manager, HttpMethod.Post, Root(f.A),
            new { referenceCode="G5-BAD-PARENT", originPortId=foreign.Id,
                destinationPortId=p.B }))
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using (var archive = await Send(manager, HttpMethod.Delete,
            $"/api/v1/organizations/{f.A}/ports/{p.A}", new { expectedVersion = 1L }))
            Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        using (var archived = await Send(manager, HttpMethod.Post, Root(f.A),
            new { referenceCode="G5-ARCH-PARENT", originPortId=p.A,
                destinationPortId=p.B }))
            Assert.Equal(HttpStatusCode.NotFound, archived.StatusCode);
        Assert.Equal(0, await db.Shipments.CountAsync(x => x.OrganizationId == f.A));
    }

    [Fact]
    public async Task FullFiveRoleShipmentMatrix()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var p = await Parents(manager, f.A);
        var id = await Seed(analyst, f.A, p.A, p.B, "G5-MATRIX");
        var root = Root(f.A);
        using (var allow = await manager.GetAsync(root))
            Assert.Equal(HttpStatusCode.OK, allow.StatusCode);
        using (var allow = await analyst.GetAsync(root + "/" + id))
            Assert.Equal(HttpStatusCode.OK, allow.StatusCode);
        using (var denied = await admin.GetAsync(root))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var user = await db.AuthUsers.AsNoTracking().SingleAsync(x =>
            x.NormalizedEmail == IdentityStore.NormalizeEmail(f.ManagerEmail));
        await db.OrganizationMemberships.Where(m => m.OrganizationId == f.A && m.UserId == user.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Role, "Auditor")
                .SetProperty(m => m.Version, m => m.Version + 1));
        using (var reader = await manager.GetAsync(root + "/" + id))
            Assert.Equal(HttpStatusCode.OK, reader.StatusCode);
        using (var audit = await manager.GetAsync(root + "/" + id + "/status-changes"))
            Assert.Equal(HttpStatusCode.OK, audit.StatusCode);
        using (var denyWrite = await Send(manager, HttpMethod.Patch, root + "/" + id,
            new { workflowStatus = "Booked", expectedVersion = 1L }))
            Assert.Equal(HttpStatusCode.Forbidden, denyWrite.StatusCode);
        using (var noM4 = await manager.GetAsync($"/api/v1/organizations/{f.A}/voyages"))
            Assert.Equal(HttpStatusCode.Forbidden, noM4.StatusCode);
        await db.OrganizationMemberships.Where(m => m.OrganizationId == f.A && m.UserId == user.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Role, "Support")
                .SetProperty(m => m.Version, m => m.Version + 1));
        using var support = await manager.GetAsync(root);
        Assert.Equal(HttpStatusCode.Forbidden, support.StatusCode);
    }

    [Fact]
    public async Task InvalidPayloadOriginAndCsrfAreRejected()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var p = await Parents(manager, f.A);
        var root = Root(f.A);
        var body = new { referenceCode = "G5-UNSAFE", originPortId = p.A,
            destinationPortId = p.B };
        using (var csrfMissing = await Send(manager, HttpMethod.Post, root, body, csrf: false))
            Assert.Equal(HttpStatusCode.Forbidden, csrfMissing.StatusCode);
        using (var csrf = await manager.GetAsync("/api/v1/auth/csrf"))
        {
            var token = (await csrf.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("token").GetString();
            using var forged = new HttpRequestMessage(HttpMethod.Post, root);
            forged.Headers.TryAddWithoutValidation("Origin", "https://other.example");
            forged.Headers.TryAddWithoutValidation("X-HarborOps-CSRF", token);
            forged.Content = JsonContent.Create(body);
            using var result = await manager.SendAsync(forged);
            Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        }
        using (var extra = await Send(manager, HttpMethod.Post, root, new {
            referenceCode = "G5-UNSAFE", originPortId = p.A, destinationPortId = p.B,
            containerIds = new[] { Guid.NewGuid() } }))
            Assert.Equal(HttpStatusCode.BadRequest, extra.StatusCode);
        using (var invalidFilter = await manager.GetAsync(root + "?limit=25&limit=50"))
            Assert.Equal(HttpStatusCode.BadRequest, invalidFilter.StatusCode);
        using (var badCursor = await manager.GetAsync(root + "?after=not-a-uuid"))
            Assert.Equal(HttpStatusCode.BadRequest, badCursor.StatusCode);
    }

    [Fact]
    public async Task PaginationAndFiltersStayTenantScoped()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var p = await Parents(manager, f.A);
        var root = Root(f.A);
        var prefix = "G5PAGE" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        for (var i = 0; i < 27; i++)
            await Seed(manager, f.A, p.A, p.B, prefix + "-" + i);
        using var first = await manager.GetAsync(root + "?limit=25&q=" + prefix);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var j1 = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(25, j1.GetProperty("shipments").GetArrayLength());
        var firstIds = j1.GetProperty("shipments").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToHashSet();
        Assert.Equal(25, firstIds.Count);
        var after = j1.GetProperty("nextAfter").GetString();
        Assert.False(string.IsNullOrEmpty(after));
        using var second = await manager.GetAsync(root + "?limit=25&q=" + prefix + "&after=" + after);
        var j2 = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, j2.GetProperty("shipments").GetArrayLength());
        Assert.All(j2.GetProperty("shipments").EnumerateArray(), item =>
            Assert.DoesNotContain(item.GetProperty("id").GetGuid(), firstIds));
        Assert.Equal(JsonValueKind.Null, j2.GetProperty("nextAfter").ValueKind);
        using (var lower = await manager.GetAsync(root + "?q=" + prefix.ToLowerInvariant()))
        {
            Assert.Equal(HttpStatusCode.OK, lower.StatusCode);
            Assert.Equal(25, (await lower.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("shipments").GetArrayLength());
        }
        using (var percent = await manager.GetAsync(root + "?q=%25"))
        {
            Assert.Equal(HttpStatusCode.OK, percent.StatusCode);
            Assert.Empty((await percent.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("shipments").EnumerateArray());
        }
        using (var booked = await manager.GetAsync(root + "?q=" + prefix + "&workflowStatus=Booked"))
        {
            Assert.Equal(HttpStatusCode.OK, booked.StatusCode);
            Assert.Empty((await booked.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("shipments").EnumerateArray());
        }
        using (var archived = await manager.GetAsync(root + "?q=" + prefix + "&recordStatus=Archived"))
        {
            Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
            Assert.Empty((await archived.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("shipments").EnumerateArray());
        }
        using var foreign = await analyst.GetAsync(Root(f.B) + "?q=" + prefix);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task FailedTransitionLeavesNoPartialHistory()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var p = await Parents(manager, f.A);
        var id = await Seed(manager, f.A, p.A, p.B, "G5-ATOMIC");
        using (var denied = await Send(manager, HttpMethod.Patch, Root(f.A) + "/" + id,
            new { workflowStatus = "Completed", expectedVersion = 1L }))
            Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE shipments SET workflow_status='Completed', version=version+1 WHERE id={id}"));
        // A database failure during the status-history insert must roll back the shipment update.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION g5_test_abort_history() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'G5 injected history failure' USING ERRCODE='23514';
            END $$;
            CREATE TRIGGER g5_test_abort_history AFTER INSERT ON shipment_status_changes
                FOR EACH ROW EXECUTE FUNCTION g5_test_abort_history();
            """);
        try
        {
            using var failed = await Send(manager, HttpMethod.Patch, Root(f.A) + "/" + id,
                new { workflowStatus = "Booked", expectedVersion = 1L });
            Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("""
                DROP TRIGGER IF EXISTS g5_test_abort_history ON shipment_status_changes;
                DROP FUNCTION IF EXISTS g5_test_abort_history();
                """);
        }
        var row = await db.Shipments.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal("Draft", row.WorkflowStatus);
        Assert.Equal(1L, row.Version);
        Assert.Equal(1, await db.ShipmentStatusChanges.CountAsync(x => x.ShipmentId == id));
    }

    [Fact]
    public async Task QueuedShipmentWriteRechecksSessionRevocation()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var p = await Parents(manager, f.A);
        var id = await Seed(manager, f.A, p.A, p.B, "G5-QUEUED");
        var cs = TestDatabase.ConnectionString();
        await using var lockConnection = new NpgsqlConnection(cs);
        await lockConnection.OpenAsync();
        await using var hold = await lockConnection.BeginTransactionAsync();
        await using (var lockCommand = new NpgsqlCommand(
            "SELECT id FROM organizations WHERE id=@id FOR UPDATE", lockConnection, hold))
        {
            lockCommand.Parameters.AddWithValue("id", f.A);
            Assert.Equal(f.A, (Guid)(await lockCommand.ExecuteScalarAsync())!);
        }
        using var csrfResponse = await manager.GetAsync("/api/v1/auth/csrf");
        var csrfToken = (await csrfResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetString();
        using var request = new HttpRequestMessage(HttpMethod.Patch, Root(f.A) + "/" + id);
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
        request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF", csrfToken);
        request.Content = JsonContent.Create(new { workflowStatus = "Booked", expectedVersion = 1L });
        var queued = manager.SendAsync(request);
        var observedLock = false;
        await using (var observer = new NpgsqlConnection(cs))
        {
            await observer.OpenAsync();
            for (var i = 0; i < 100; i++)
            {
                await using var check = new NpgsqlCommand(
                    "SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() " +
                    "AND wait_event_type='Lock' AND query ILIKE '%organizations%'", observer);
                if (Convert.ToInt64(await check.ExecuteScalarAsync()) > 0)
                {
                    observedLock = true;
                    break;
                }
                if (queued.IsCompleted) break;
                await Task.Delay(100);
            }
        }
        try
        {
            Assert.True(observedLock, "Queued shipment write was not observed waiting on the organization lock");
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var user = await db.AuthUsers.AsNoTracking().SingleAsync(x =>
                x.NormalizedEmail == IdentityStore.NormalizeEmail(f.ManagerEmail));
            await db.AuthSessions.Where(s => s.UserId == user.Id).ExecuteUpdateAsync(u =>
                u.SetProperty(s => s.RevokedAtUtc, DateTimeOffset.UtcNow));
        }
        finally
        {
            await hold.CommitAsync();
        }
        using var result = await queued;
        Assert.True(result.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
        await using var verify = factory.Services.CreateAsyncScope();
        var read = verify.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(1L, (await read.Shipments.AsNoTracking().SingleAsync(x => x.Id == id)).Version);
        Assert.Equal(1, await read.ShipmentStatusChanges.CountAsync(x => x.ShipmentId == id));
    }
}
