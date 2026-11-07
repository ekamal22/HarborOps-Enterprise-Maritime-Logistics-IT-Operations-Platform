using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Api.Audit;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class ShipmentSearchHttpTests
{
    private static string Root(Guid org) => $"/api/v1/organizations/{org}";
    private static async Task<Guid> Create(HttpClient client, string url, object body)
    {
        using var result = await ReferenceDataHttpTests.Send(client, HttpMethod.Post, url, body);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        var json = await result.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task AdvancedFiltersAndProtectedCursorStayTenantScoped()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        using var admin = fixture.Admin;
        var seed = await AssignmentHttpTests.Seed(fixture);
        using var baseResponse = await manager.GetAsync(seed.Root + "/" + seed.Shipment);
        var baseRow = await baseResponse.Content.ReadFromJsonAsync<JsonElement>();
        var origin = baseRow.GetProperty("originPortId").GetGuid();
        var dest = baseRow.GetProperty("destinationPortId").GetGuid();

        var prefix = "G12-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var id = await Create(manager, seed.Root, new {
                referenceCode = prefix + "-" + i,
                originPortId = origin, destinationPortId = dest,
                plannedDepartureAtUtc = "2026-11-01T10:00:00Z",
                plannedArrivalAtUtc = "2026-11-03T10:00:00Z"
            });
            ids.Add(id);
        }

        var search = Root(fixture.A) + "/shipments/search";
        using var firstResponse = await analyst.GetAsync(search + "?q=" + prefix + "&limit=1");
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(first.GetProperty("shipments").EnumerateArray());
        var cursor = first.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(cursor));
        var firstId = first.GetProperty("shipments")[0].GetProperty("id").GetGuid();

        using var secondResponse = await analyst.GetAsync(search + "?q=" + prefix +
            "&limit=1&cursor=" + Uri.EscapeDataString(cursor!));
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var second = await secondResponse.Content.ReadFromJsonAsync<JsonElement>();
        var secondId = second.GetProperty("shipments")[0].GetProperty("id").GetGuid();
        Assert.NotEqual(firstId, secondId);
        Assert.Contains(secondId, ids);

        using var mismatched = await analyst.GetAsync(search + "?q=DIFFERENT&limit=1&cursor=" +
            Uri.EscapeDataString(cursor!));
        Assert.Equal(HttpStatusCode.Conflict, mismatched.StatusCode);
        using var damaged = await analyst.GetAsync(search + "?q=" + prefix + "&limit=1&cursor=x");
        Assert.Equal(HttpStatusCode.Conflict, damaged.StatusCode);
        using var wrongOrg = await analyst.GetAsync(Root(fixture.B) + "/shipments/search");
        Assert.Equal(HttpStatusCode.NotFound, wrongOrg.StatusCode);
        using var adminDenied = await admin.GetAsync(search + "?q=" + prefix);
        Assert.Equal(HttpStatusCode.Forbidden, adminDenied.StatusCode);

        using var portFiltered = await analyst.GetAsync(search + "?q=" + prefix +
            "&originPortId=" + origin + "&destinationPortId=" + dest);
        Assert.Equal(HttpStatusCode.OK, portFiltered.StatusCode);
        var rows = (await portFiltered.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("shipments").EnumerateArray().ToArray();
        Assert.Equal(3, rows.Length);
        Assert.All(rows, row => Assert.Equal(fixture.A,
            row.GetProperty("organizationId").GetGuid()));

        using var departure = await analyst.GetAsync(search + "?q=" + prefix +
            "&plannedDepartureFromUtc=2026-11-01T10%3A00%3A00Z" +
            "&plannedDepartureBeforeUtc=2026-11-02T10%3A00%3A00Z");
        Assert.Equal(HttpStatusCode.OK, departure.StatusCode);
        Assert.Equal(3, (await departure.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("shipments").GetArrayLength());
        using var exclusive = await analyst.GetAsync(search + "?q=" + prefix +
            "&plannedDepartureFromUtc=2026-11-01T00%3A00%3A00Z" +
            "&plannedDepartureBeforeUtc=2026-11-01T10%3A00%3A00Z");
        Assert.Empty((await exclusive.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("shipments").EnumerateArray());

        var assignedAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O");
        await Create(manager, seed.Root + "/" + ids[0] + "/voyage-assignments",
            new { voyageId = seed.Voyage, startsAtUtc = assignedAt });
        using var voyageOnly = await analyst.GetAsync(search + "?q=" + prefix +
            "&voyageId=" + seed.Voyage);
        Assert.Equal(HttpStatusCode.OK, voyageOnly.StatusCode);
        var matched = (await voyageOnly.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("shipments").EnumerateArray().ToArray();
        Assert.Single(matched);
        Assert.Equal(ids[0], matched[0].GetProperty("id").GetGuid());

        foreach (var bad in new[] { "?q=" + prefix + "&limit=0",
            "?q=" + prefix + "&limit=1&limit=2",
            "?q=" + prefix + "&voyageId=nope",
            "?q=" + prefix + "&plannedArrivalFromUtc=2026-11-01T00%3A00%3A00Z",
            "?q=" + prefix + "&unrecognized=1" })
        {
            using var denied = await analyst.GetAsync(search + bad);
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        }
    }

    [Fact]
    public async Task RestrictedRuntimeExportIsScopedAndAudited()
    {
        var migratorConnection = TestDatabase.ConnectionString();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(migratorConnection);
        var fixture = await ReferenceDataHttpTests.Setup(seedFactory);
        using var admin = fixture.Admin;
        using var seedManager = fixture.Manager;
        using var seedAnalyst = fixture.Analyst;
        var seed = await AssignmentHttpTests.Seed(fixture);
        using var detail = await seedManager.GetAsync(seed.Root + "/" + seed.Shipment);
        var row = await detail.Content.ReadFromJsonAsync<JsonElement>();
        var code = row.GetProperty("referenceCode").GetString()!;
        var search = Root(fixture.A) + "/exports/shipments";

        // Provision only inside a disposable, exclusive G12 database.
        var apiPassword = "g12-api-" + Guid.NewGuid().ToString("N");
        var workerPassword = "g12-worker-" + Guid.NewGuid().ToString("N");
        await using (var scope = seedFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var config = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> {
                    ["AuditRoles:ApiPassword"] = apiPassword,
                    ["AuditRoles:WorkerPassword"] = workerPassword,
                    ["M10CorrectionProof:Key"] = ReferenceDataHttpTests.CorrectionProofKey
                }).Build();
            await AuditRoleProvisioner.ProvisionAsync(db, config, CancellationToken.None);
        }

        var csb = new NpgsqlConnectionStringBuilder(migratorConnection) {
            Username = "harborops_api", Password = apiPassword
        };
        using var runtime = new ReferenceDataHttpTests.ApiFactory(csb.ConnectionString);
        using var manager = runtime.CreateClient();
        using var analyst = runtime.CreateClient();
        using var administrator = runtime.CreateClient();
        await ReferenceDataHttpTests.Login(manager, fixture.ManagerEmail);
        await ReferenceDataHttpTests.Login(analyst, fixture.AnalystEmail);
        await ReferenceDataHttpTests.Login(administrator, fixture.AdminEmail);
        await ReferenceDataHttpTests.Select(manager, fixture.A);
        await ReferenceDataHttpTests.Select(analyst, fixture.A);
        await ReferenceDataHttpTests.Select(administrator, fixture.A);

        using (var denied = await administrator.GetAsync(search + "?q=" + code))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var wrong = await manager.GetAsync(Root(fixture.B) + "/exports/shipments?q=" + code))
            Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        using (var invalid = await manager.GetAsync(search + "?cursor=not-an-export-filter"))
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using (var foreignDownload = new HttpRequestMessage(HttpMethod.Get, search + "?q=" + code))
        {
            foreignDownload.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-site");
            foreignDownload.Headers.TryAddWithoutValidation("Origin", "http://untrusted.localhost");
            using var refused = await manager.SendAsync(foreignDownload);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        using (var download = await manager.GetAsync(search + "?q=" + code))
        {
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.Equal("text/csv", download.Content.Headers.ContentType?.MediaType);
            Assert.Contains("no-store", string.Join(",", download.Headers.GetValues("Cache-Control")));
            var csv = await download.Content.ReadAsStringAsync();
            Assert.Contains("Reference Code", csv, StringComparison.Ordinal);
            Assert.Contains(code, csv, StringComparison.Ordinal);
            Assert.DoesNotContain("password", csv, StringComparison.OrdinalIgnoreCase);
        }
        using (var analystExport = await analyst.GetAsync(search + "?q=" + code))
            Assert.Equal(HttpStatusCode.OK, analystExport.StatusCode);

        // A direct runtime SQL login may not bypass the function's required
        // metadata or invent a user/session outside active organization scope.
        await using (var direct = new NpgsqlConnection(csb.ConnectionString))
        {
            await direct.OpenAsync();
            await using (var missing = new NpgsqlCommand("""
                SELECT public.m12_record_shipment_export(
                    NULL::uuid, NULL::uuid, NULL::uuid, NULL::integer,
                    NULL::bigint, NULL::varchar, NULL::varchar)
                """, direct))
            {
                await Assert.ThrowsAsync<PostgresException>(
                    () => missing.ExecuteNonQueryAsync());
            }
            await using (var forged = new NpgsqlCommand("""
                SELECT public.m12_record_shipment_export(
                    @org, @user, @session, 1, 128,
                    @digest, @correlation)
                """, direct))
            {
                forged.Parameters.AddWithValue("org", fixture.A);
                forged.Parameters.AddWithValue("user", Guid.NewGuid());
                forged.Parameters.AddWithValue("session", Guid.NewGuid());
                forged.Parameters.AddWithValue("digest", new string('A', 64));
                forged.Parameters.AddWithValue("correlation", Guid.NewGuid().ToString("N"));
                await Assert.ThrowsAsync<PostgresException>(
                    () => forged.ExecuteNonQueryAsync());
            }
        }
        var workerCsb = new NpgsqlConnectionStringBuilder(migratorConnection) {
            Username = "harborops_worker", Password = workerPassword
        };
        await using (var worker = new NpgsqlConnection(workerCsb.ConnectionString))
        {
            await worker.OpenAsync();
            await using var denied = new NpgsqlCommand("""
                SELECT public.m12_record_shipment_export(
                    NULL::uuid, NULL::uuid, NULL::uuid, NULL::integer,
                    NULL::bigint, NULL::varchar, NULL::varchar)
                """, worker);
            var sqlError = await Assert.ThrowsAsync<PostgresException>(
                () => denied.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, sqlError.SqlState);
        }

        await using var verifyScope = seedFactory.Services.CreateAsyncScope();
        var verification = verifyScope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var events = await verification.AuditEntries.AsNoTracking().Where(e =>
            e.OrganizationId == fixture.A && e.EventType == "shipment.export_prepared")
            .ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.All(events, e =>
        {
            Assert.Equal("Succeeded", e.Outcome);
            Assert.Equal("Human", e.ActorKind);
            Assert.DoesNotContain("password", e.MetadataJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("filterDigest", e.MetadataJson, StringComparison.Ordinal);
        });
    }
}
