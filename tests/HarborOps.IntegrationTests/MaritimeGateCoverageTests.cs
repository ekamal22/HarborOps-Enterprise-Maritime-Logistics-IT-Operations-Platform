using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Maritime;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// These cases extend the original M4 tests; each case uses the real API and PostgreSQL.
public sealed class MaritimeGateCoverageTests
{
    private static string Root(Guid org, string kind) => $"/api/v1/organizations/{org}/{kind}";
    private static object PostBody(string kind) => kind == "voyages"
        ? new { referenceCode = "G4-NO-PARENTS", vesselId = Guid.NewGuid(),
            originPortId = Guid.NewGuid(), destinationPortId = Guid.NewGuid() }
        : new { referenceCode = "G4-NEW-BOX", identifierKind = "Synthetic",
            containerIdentifier = "SYN-BOX-NEWG4", equipmentType = "Dry20" };
    private static object PatchBody(string kind) => kind == "voyages"
        ? new { plannedDepartureAtUtc = "2026-11-01T22:00:00Z", expectedVersion = 1L }
        : new { displayName = "Checked", expectedVersion = 1L };
    private static (HttpMethod Method, string Path, object? Body, bool Read)[] Routes(Guid org, string kind)
    {
        var root = Root(org, kind);
        var id = Guid.NewGuid();
        return [
            (HttpMethod.Get, root, null, true),
            (HttpMethod.Get, root + "/" + id, null, true),
            (HttpMethod.Post, root, PostBody(kind), false),
            (HttpMethod.Patch, root + "/" + id, PatchBody(kind), false),
            (HttpMethod.Delete, root + "/" + id, new { expectedVersion = 1L }, false)
        ];
    }
    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method,
        string path, object? body = null, bool csrf = true) =>
        ReferenceDataHttpTests.Send(client, method, path, body, csrf);

    [Theory]
    [InlineData("revoked")]
    [InlineData("disabled")]
    [InlineData("idle_expired")]
    [InlineData("absolute_expired")]
    [InlineData("security_version")]
    public async Task AllTenM4RoutesRejectInvalidatedAuthentication(string invalidation)
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
        switch (invalidation)
        {
            case "revoked":
                await db.AuthSessions.Where(s => s.UserId == user.Id)
                    .ExecuteUpdateAsync(u => u.SetProperty(s => s.RevokedAtUtc, now));
                break;
            case "disabled":
                await db.AuthUsers.Where(u => u.Id == user.Id).ExecuteUpdateAsync(u =>
                    u.SetProperty(s => s.Status, "Disabled"));
                break;
            case "security_version":
                await db.AuthUsers.Where(u => u.Id == user.Id).ExecuteUpdateAsync(u =>
                    u.SetProperty(s => s.SecurityVersion, s => s.SecurityVersion + 1));
                break;
            case "absolute_expired":
                var old = now.AddDays(-2);
                await db.AuthSessions.Where(s => s.UserId == user.Id).ExecuteUpdateAsync(upd =>
                    upd.SetProperty(s => s.IssuedAtUtc, old)
                        .SetProperty(s => s.LastSeenAtUtc, old.AddMinutes(5))
                        .SetProperty(s => s.IdleExpiresAtUtc, old.AddMinutes(20))
                        .SetProperty(s => s.AbsoluteExpiresAtUtc, old.AddHours(1)));
                break;
            case "idle_expired":
                var sessions = await db.AuthSessions.AsNoTracking()
                    .Where(s => s.UserId == user.Id).Select(s => new { s.Id, s.IssuedAtUtc })
                    .ToListAsync();
                foreach (var session in sessions)
                {
                    var expires = session.IssuedAtUtc.AddMilliseconds(1);
                    await db.AuthSessions.Where(s => s.Id == session.Id)
                        .ExecuteUpdateAsync(u => u.SetProperty(s => s.IdleExpiresAtUtc, expires));
                }
                break;
        }
        foreach (var kind in new[] { "voyages", "containers" })
        foreach (var (method, path, body, _) in Routes(f.A, kind))
        {
            using var response = await Send(manager, method, path, body, csrf: false);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains("authentication_required", await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task AllTenM4RoutesRejectAnonymousAndForeignOrganization()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var analyst = f.Analyst;
        using var manager = f.Manager;
        using var anonymous = factory.CreateClient();
        foreach (var kind in new[] { "voyages", "containers" })
        {
            foreach (var (method, path, body, _) in Routes(f.A, kind))
            {
                using var anon = await Send(anonymous, method, path, body, csrf: false);
                Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);
            }
            foreach (var (method, path, body, _) in Routes(f.B, kind))
            {
                using var foreign = await Send(manager, method, path, body);
                Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
                Assert.Contains("resource_not_found", await foreign.Content.ReadAsStringAsync());
            }
        }
    }

    [Fact]
    public async Task CompleteMaritimeRoleAndVerbMatrixDeniesUnauthorizedChanges()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var analyst = f.Analyst;
        using var manager = f.Manager;
        foreach (var kind in new[] { "voyages", "containers" })
        {
            foreach (var (method, path, body, read) in Routes(f.A, kind))
            {
                using var allowed = await Send(manager, method, path, body);
                // Existing collection, nonexistent detail, valid container creation,
                // and nonexistent parent/record mutations have distinct exact results.
                var expected = read
                    ? (path.EndsWith(kind) ? HttpStatusCode.OK : HttpStatusCode.NotFound)
                    : method == HttpMethod.Post && kind == "containers"
                        ? HttpStatusCode.Created : HttpStatusCode.NotFound;
                Assert.Equal(expected, allowed.StatusCode);
                using var reader = await Send(analyst, method, path, body);
                Assert.Equal(read ? (path.EndsWith(kind) ? HttpStatusCode.OK : HttpStatusCode.NotFound)
                    : HttpStatusCode.Forbidden, reader.StatusCode);
                using var administrator = await Send(admin, method, path, body);
                Assert.Equal(HttpStatusCode.Forbidden, administrator.StatusCode);
            }
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var member = await db.OrganizationMemberships.AsNoTracking()
            .SingleAsync(x => x.OrganizationId == f.A && x.Role == "Manager");
        foreach (var role in new[] { "Support", "Auditor" })
        {
            using var update = await Send(admin, HttpMethod.Put,
                $"/api/v1/organizations/{f.A}/memberships/{member.UserId}",
                new { role, status = "Active", expectedVersion = member.Version });
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            member.Version += 1;
            foreach (var kind in new[] { "voyages", "containers" })
            foreach (var (method, path, body, _) in Routes(f.A, kind))
            {
                using var denied = await Send(manager, method, path, body);
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            }
        }
    }

    [Fact]
    public async Task ContainerPaginationFiltersAndWildcardSearchRemainTenantScoped()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var analyst = f.Analyst;
        using var manager = f.Manager;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var now = DateTimeOffset.UtcNow;
            for (var i = 0; i < 29; i++)
                db.Containers.Add(new ContainerRecord {
                    Id = Guid.NewGuid(), OrganizationId = f.A,
                    ReferenceCode = $"PAGED-{i:000}",
                    IdentifierKind = "Synthetic", ContainerIdentifier = $"SYN-BOX-PG{i:0000}",
                    EquipmentType = i % 2 == 0 ? "Dry20" : "Reefer40",
                    SourceKind = "Synthetic", RecordStatus = i % 3 == 0 ? "Archived" : "Active",
                    CreatedAtUtc = now, UpdatedAtUtc = now
                });
            db.Containers.Add(new ContainerRecord {
                Id = Guid.NewGuid(), OrganizationId = f.B,
                ReferenceCode = "PAGED-FOREIGN", IdentifierKind = "Synthetic",
                ContainerIdentifier = "SYN-BOX-FOREIGNPG", EquipmentType = "Dry20",
                SourceKind = "Synthetic", CreatedAtUtc = now, UpdatedAtUtc = now
            });
            await db.SaveChangesAsync();
        }
        var root = Root(f.A, "containers");
        var seen = new HashSet<Guid>();
        string? after = null;
        do
        {
            var route = root + "?recordStatus=All&limit=7" +
                (after is null ? "" : "&after=" + Uri.EscapeDataString(after));
            var result = await manager.GetFromJsonAsync<JsonElement>(route);
            var rows = result.GetProperty("containers").EnumerateArray().ToArray();
            Assert.InRange(rows.Length, 1, 7);
            foreach (var row in rows)
            {
                Assert.True(seen.Add(row.GetProperty("id").GetGuid()));
                Assert.Equal(f.A, row.GetProperty("organizationId").GetGuid());
            }
            after = result.GetProperty("nextAfter").ValueKind == JsonValueKind.Null
                ? null : result.GetProperty("nextAfter").GetString();
        } while (after is not null);
        Assert.Equal(29, seen.Count);
        var active = await manager.GetFromJsonAsync<JsonElement>(root + "?recordStatus=Active&limit=100");
        Assert.Equal(19, active.GetProperty("containers").GetArrayLength());
        var archived = await manager.GetFromJsonAsync<JsonElement>(root + "?recordStatus=Archived&limit=100");
        Assert.Equal(10, archived.GetProperty("containers").GetArrayLength());
        var filtered = await manager.GetFromJsonAsync<JsonElement>(
            root + "?recordStatus=All&equipmentType=Reefer40&identifierKind=Synthetic&limit=100");
        Assert.Equal(14, filtered.GetProperty("containers").GetArrayLength());
        var literal = await manager.GetFromJsonAsync<JsonElement>(root + "?q=%25&recordStatus=All");
        Assert.Empty(literal.GetProperty("containers").EnumerateArray());
        var literalUnderscore = await manager.GetFromJsonAsync<JsonElement>(root + "?q=_&recordStatus=All");
        Assert.Empty(literalUnderscore.GetProperty("containers").EnumerateArray());
    }

    [Fact]
    public async Task ArchivedIdentifiersRemainReservedAndDirectSqlCannotArchiveWithEdits()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var analyst = f.Analyst;
        using var manager = f.Manager;
        var root = Root(f.A, "containers");
        using var created = await Send(manager, HttpMethod.Post, root, new {
            referenceCode = "ARCHIVE-BOX", identifierKind = "Synthetic",
            containerIdentifier = "SYN-BOX-ARCHIVED", equipmentType = "Dry20"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE containers SET record_status = {"Archived"}, display_name = {"Tampered"}, version = {2L}, updated_at_utc = {DateTimeOffset.UtcNow.AddMinutes(1)} WHERE id = {id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        var active = await db.Containers.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal("Active", active.RecordStatus);
        Assert.Null(active.DisplayName);
        Assert.Equal(1L, active.Version);
        using var archive = await Send(manager, HttpMethod.Delete, root + "/" + id,
            new { expectedVersion = 1L });
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        using var duplicate = await Send(manager, HttpMethod.Post, root, new {
            referenceCode = "NEW-ARCHIVE-BOX", identifierKind = "Synthetic",
            containerIdentifier = "SYN-BOX-ARCHIVED", equipmentType = "Reefer40"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var row = await db.Containers.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal("Archived", row.RecordStatus);
        Assert.Null(row.DisplayName);
        Assert.Equal(2L, row.Version);
    }

    [Fact]
    public async Task HostileMaritimeQueriesAndBodiesFailWithoutCreatingRows()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var analyst = f.Analyst;
        using var manager = f.Manager;
        foreach (var kind in new[] { "voyages", "containers" })
        {
            var root = Root(f.A, kind);
            foreach (var query in new[] {
                "?limit=-1", "?limit=2147483648", "?limit=1&limit=2",
                "?after=00000000-0000-0000-0000-000000000000",
                "?recordStatus=Unknown", "?q=%00", "?q=x&q=y",
                "?status=Active", "?unexpected=true",
                "?identifierKind=UNSAFE", "?equipmentType=UNSAFE",
                "?voyageStatus=UNSAFE", "?originPortId=invalid"
            })
            {
                // A filter valid for the other entity can still be invalid for this route.
                using var result = await manager.GetAsync(root + query);
                Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
            }
            foreach (var raw in new[] { "[]", "null", "{}", "{",
                "{\"referenceCode\":\"ABC\",\"referenceCode\":\"DUP\"}",
                "{\"referenceCode\":\"ABC\",\"expectedVersion\":1}",
                "{\"referenceCode\":\"ABC\",\"displayName\":{\"nested\":true}}"
            })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, root) {
                    Content = new StringContent(raw, System.Text.Encoding.UTF8, "application/json")
                };
                request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
                using var csrf = await manager.GetAsync("/api/v1/auth/csrf");
                var token = await csrf.Content.ReadFromJsonAsync<JsonElement>();
                request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",
                    token.GetProperty("token").GetString());
                using var response = await manager.SendAsync(request);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(0, await db.Containers.CountAsync(x => x.OrganizationId == f.A));
        Assert.Equal(0, await db.Voyages.CountAsync(x => x.OrganizationId == f.A));
    }

    [Fact]
    public async Task ForeignOriginIsRejectedWithRealCsrfOnBothMaritimeResources()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var manager = f.Manager;
        using var admin = f.Admin;
        using var analyst = f.Analyst;
        using var csrfResponse = await manager.GetAsync("/api/v1/auth/csrf");
        csrfResponse.EnsureSuccessStatusCode();
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var kind in new[] { "voyages", "containers" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Root(f.A, kind));
            request.Headers.TryAddWithoutValidation("Origin", "https://untrusted-origin.example");
            request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",
                csrf.GetProperty("token").GetString());
            request.Content = JsonContent.Create(PostBody(kind));
            using var response = await manager.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Contains("csrf_invalid", await response.Content.ReadAsStringAsync());
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(0, await db.Containers.CountAsync(x => x.OrganizationId == f.A));
        Assert.Equal(0, await db.Voyages.CountAsync(x => x.OrganizationId == f.A));
    }

    [Fact]
    public async Task IdenticalContainerIdentifierIsAllowedInDifferentOrganizations()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var analyst = f.Analyst;
        using var manager = f.Manager;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var now = DateTimeOffset.UtcNow;
        const string sharedIdentifier = "SYN-BOX-SHAREDG4";
        db.Containers.AddRange(
            new ContainerRecord {
                Id = Guid.NewGuid(), OrganizationId = f.A, ReferenceCode = "SAME-ID-A",
                IdentifierKind = "Synthetic", ContainerIdentifier = sharedIdentifier,
                SourceKind = "Synthetic", EquipmentType = "Dry20",
                CreatedAtUtc = now, UpdatedAtUtc = now
            },
            new ContainerRecord {
                Id = Guid.NewGuid(), OrganizationId = f.B, ReferenceCode = "SAME-ID-B",
                IdentifierKind = "Synthetic", ContainerIdentifier = sharedIdentifier,
                SourceKind = "Synthetic", EquipmentType = "Dry40",
                CreatedAtUtc = now, UpdatedAtUtc = now
            }
        );
        await db.SaveChangesAsync();
        var rows = await db.Containers.AsNoTracking()
            .Where(x => x.ContainerIdentifier == sharedIdentifier)
            .Select(x => new { x.OrganizationId, x.Version }).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, x => x.OrganizationId == f.A && x.Version == 1L);
        Assert.Contains(rows, x => x.OrganizationId == f.B && x.Version == 1L);
        var local = await manager.GetFromJsonAsync<JsonElement>(Root(f.A, "containers") +
            "?q=SHAREDG4&recordStatus=All");
        Assert.Single(local.GetProperty("containers").EnumerateArray());
        Assert.Equal(f.A, local.GetProperty("containers")[0]
            .GetProperty("organizationId").GetGuid());
    }

}
