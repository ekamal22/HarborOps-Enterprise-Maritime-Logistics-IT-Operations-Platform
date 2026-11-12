using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

[Collection("M13ExclusiveServer")]
public sealed class M13ConcurrentAcceptanceTests
{
    [ExclusiveM13PostgresFact]
    public async Task FourAuthorizedPrincipalsBuildIndependentTenantScopedReportsConcurrently()
    {
        const string password = "Synthetic-reference-integration-passphrase-123";
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(seedFactory);
        using var admin = f.Admin;
        using var seedManager = f.Manager;
        using var seedAnalyst = f.Analyst;
        var seed = await AssignmentHttpTests.Seed(f);

        using var shipped = await seedManager.GetAsync(seed.Root + "/" + seed.Shipment);
        var item = await shipped.Content.ReadFromJsonAsync<JsonElement>();
        var origin = item.GetProperty("originPortId").GetGuid();
        var destination = item.GetProperty("destinationPortId").GetGuid();
        await using (var sql = new NpgsqlConnection(roles.Admin))
        {
            await sql.OpenAsync();
            await using var actorQuery = new NpgsqlCommand(
                "SELECT changed_by_user_id FROM shipments WHERE id=@id", sql);
            actorQuery.Parameters.AddWithValue("id", seed.Shipment);
            var actor = Assert.IsType<Guid>(await actorQuery.ExecuteScalarAsync());
            await using var bulk = new NpgsqlCommand("""
                INSERT INTO shipments (
                    id,organization_id,reference_code,origin_port_id,destination_port_id,
                    workflow_status,record_status,source_kind,version,
                    created_at_utc,updated_at_utc,changed_by_user_id)
                SELECT gen_random_uuid(),@org,
                    'G13-CONC-' || lpad(i::text,6,'0'),@origin,@destination,
                    'Draft','Active','Synthetic',1,clock_timestamp(),
                    clock_timestamp(),@actor
                FROM generate_series(1,5000) AS i
                """, sql);
            bulk.Parameters.AddWithValue("org", f.A);
            bulk.Parameters.AddWithValue("origin", origin);
            bulk.Parameters.AddWithValue("destination", destination);
            bulk.Parameters.AddWithValue("actor", actor);
            Assert.Equal(5000, await bulk.ExecuteNonQueryAsync());
        }

        var emails = new List<string> { f.ManagerEmail, f.AnalystEmail };
        await using (var scope = seedFactory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
            var orgs = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
            var adminSession = await identity.LoginAsync(
                f.AdminEmail, password, "g13-concurrent", CancellationToken.None);
            Assert.NotNull(adminSession);
            var selected = await orgs.SelectAsync(adminSession!.UserId,
                adminSession.SessionId, f.A, CancellationToken.None);
            Assert.NotNull(selected.Value);
            for (var i = 0; i < 2; i++)
            {
                var email = "g13-concurrent-" + Guid.NewGuid().ToString("N") +
                    "@example.test";
                await identity.ProvisionAsync(email, "Concurrent Report Reader",
                    password, CancellationToken.None);
                var login = await identity.LoginAsync(
                    email, password, "g13-concurrent", CancellationToken.None);
                Assert.NotNull(login);
                var granted = await orgs.PutMemberAsync(adminSession.UserId,
                    adminSession.SessionId, f.A, login!.UserId, "Analyst", "Active",
                    null, "g13-concurrent", CancellationToken.None);
                Assert.NotNull(granted.Value);
                emails.Add(email);
            }
        }

        using var api = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        var clients = new List<HttpClient>();
        try
        {
            foreach (var email in emails)
            {
                var client = api.CreateClient(
                    new WebApplicationFactoryClientOptions { HandleCookies = true });
                client.Timeout = TimeSpan.FromMinutes(1);
                await ReferenceDataHttpTests.Login(client, email);
                await ReferenceDataHttpTests.Select(client, f.A);
                clients.Add(client);
            }

            var clock = Stopwatch.StartNew();
            var reports = await Task.WhenAll(clients.Select(async client =>
            {
                using var response = await client.GetAsync(
                    $"/api/v1/organizations/{f.A}/analytics/overview");
                var payload = await response.Content.ReadAsStringAsync();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var json = JsonDocument.Parse(payload);
                var root = json.RootElement;
                var total = root.GetProperty("counts").EnumerateArray()
                    .Where(x => x.GetProperty("metricCode").GetString() == "D01")
                    .Sum(x => x.GetProperty("count").GetInt64());
                Assert.Equal(5002L, total);
                return root.GetProperty("reportId").GetGuid();
            }));
            clock.Stop();
            Assert.Equal(4, reports.Distinct().Count());
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(25),
                $"Four-reader report envelope exceeded 25s: {clock.Elapsed}");
            Console.WriteLine($"G13_CONCURRENCY users=4 shipments=5002 " +
                $"elapsed_ms={clock.Elapsed.TotalMilliseconds:F0} reports=4");
        }
        finally
        {
            foreach (var client in clients) client.Dispose();
        }
    }

    // Plan S20 asks for concurrent load on a skewed, history-rich tenant. A tenant
    // inside the 250,000 member capacity (about 40,000 shipments with this mix)
    // must serve four simultaneous builders with no 503 and inside the 30 second
    // proxy envelope. A tenant beyond the capacity must be refused quickly and
    // identically with the documented 413, never with a timeout.
    [ExclusiveM13PostgresFact]
    public Task RealisticSkewedFortyThousandShipmentTenantCompletesFourSimultaneousBuilds() =>
        RunRealisticTenantAsync(40_000, builders: 4, expectReports: true);

    // Eight distinct users of one tenant start at the same moment. The tenant limit
    // is four live snapshots, so exactly four builders may run the heavy member
    // statement; the other four must be refused at once with the explicit 429, not
    // after a full 25 second materialization and not with a 503.
    [ExclusiveM13PostgresFact]
    public Task RealisticSkewedFortyThousandShipmentTenantAdmitsFourOfEightBuildersAndRefusesTheRestPromptly() =>
        RunRealisticTenantAsync(40_000, builders: 8, expectReports: true);

    [ExclusiveM13PostgresFact]
    public Task RealisticSkewed170kShipmentTenantIsRefusedAsOverCapacityWithoutTimeouts() =>
        RunRealisticTenantAsync(170_000, builders: 4, expectReports: false);

    private static async Task RunRealisticTenantAsync(int shipments, int builders, bool expectReports)
    {
        const string password = "Synthetic-reference-integration-passphrase-123";
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(seedFactory);
        using var admin = f.Admin;
        using var seedManager = f.Manager;
        using var seedAnalyst = f.Analyst;
        var seed = await AssignmentHttpTests.Seed(f);
        using var shipped = await seedManager.GetAsync(seed.Root + "/" + seed.Shipment);
        var item = await shipped.Content.ReadFromJsonAsync<JsonElement>();
        var origin = item.GetProperty("originPortId").GetGuid();
        var destination = item.GetProperty("destinationPortId").GetGuid();

        await using var sql = new NpgsqlConnection(roles.Admin);
        await sql.OpenAsync();
        Guid actor;
        await using (var actorQuery = new NpgsqlCommand(
            "SELECT changed_by_user_id FROM shipments WHERE id=@id", sql))
        {
            actorQuery.Parameters.AddWithValue("id", seed.Shipment);
            actor = Assert.IsType<Guid>(await actorQuery.ExecuteScalarAsync());
        }

        var (history, evaluations, openExceptions, seedMs) = await RealisticTenantFixture.SeedAsync(
            sql, f.A, origin, destination, actor, shipments);
        Console.WriteLine($"G13_REALISTIC seeded shipments={shipments} history={history} " +
            $"evaluations={evaluations} open_exceptions={openExceptions} seed_ms={seedMs}");
        // Rolled-back restricted-role plans of the real statement on this data,
        // with the default planner and with nested loops disabled. They record
        // where the time goes under row-level security and are kept as evidence.
        var diagnostic = Environment.GetEnvironmentVariable("HARBOROPS_G13_REALISTIC_PLANS") == "1";
        if (diagnostic)
        {
            try
            {
                await M13ScaleLoadTests.CaptureRestrictedPlan(roles.Api, roles.Admin, f.A, actor,
                    $"realistic{shipments / 1000}k-default", null, 40, false);
            }
            catch (PostgresException e)
            {
                // The signed scope is valid for at most 45 seconds, so a plan that
                // needs longer fails closed (42501 or 57014) instead of completing.
                Console.WriteLine($"G13_PLAN_DEFAULT_NOT_COMPLETED sample=realistic{shipments / 1000}k state={e.SqlState}");
            }
            await M13ScaleLoadTests.CaptureRestrictedPlan(roles.Api, roles.Admin, f.A, actor,
                $"realistic{shipments / 1000}k-nestloop-off", null, 120, true);
        }
        var emails = new List<string> { f.ManagerEmail, f.AnalystEmail };
        await using (var scope = seedFactory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
            var orgs = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
            var adminSession = await identity.LoginAsync(
                f.AdminEmail, password, "g13-realistic", CancellationToken.None);
            Assert.NotNull(adminSession);
            var selected = await orgs.SelectAsync(adminSession!.UserId,
                adminSession.SessionId, f.A, CancellationToken.None);
            Assert.NotNull(selected.Value);
            for (var i = 0; i < builders - 2; i++)
            {
                var email = "g13-realistic-" + Guid.NewGuid().ToString("N") + "@example.test";
                await identity.ProvisionAsync(email, "Realistic Report Reader",
                    password, CancellationToken.None);
                var login = await identity.LoginAsync(
                    email, password, "g13-realistic", CancellationToken.None);
                Assert.NotNull(login);
                var granted = await orgs.PutMemberAsync(adminSession.UserId,
                    adminSession.SessionId, f.A, login!.UserId, "Analyst", "Active",
                    null, "g13-realistic", CancellationToken.None);
                Assert.NotNull(granted.Value);
                emails.Add(email);
            }
        }

        using var api = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        var clients = new List<HttpClient>();
        try
        {
            foreach (var email in emails)
            {
                var client = api.CreateClient(
                    new WebApplicationFactoryClientOptions { HandleCookies = true });
                client.Timeout = TimeSpan.FromMinutes(3);
                await ReferenceDataHttpTests.Login(client, email);
                await ReferenceDataHttpTests.Select(client, f.A);
                clients.Add(client);
            }
            var clock = Stopwatch.StartNew();
            var outcomes = await Task.WhenAll(clients.Select(async client =>
            {
                var timer = Stopwatch.StartNew();
                using var response = await client.GetAsync(
                    $"/api/v1/organizations/{f.A}/analytics/overview");
                var body = await response.Content.ReadAsStringAsync();
                timer.Stop();
                using var json = JsonDocument.Parse(body);
                var root = json.RootElement.Clone();
                return (Status: response.StatusCode, Root: root, Ms: timer.Elapsed.TotalMilliseconds);
            }));
            clock.Stop();

            var summary = string.Join(",", outcomes
                .GroupBy(x => (int)x.Status).OrderBy(x => x.Key)
                .Select(x => x.Key + ":" + x.Count()));
            var codes = string.Join(",", outcomes.Select(x =>
                x.Status == HttpStatusCode.OK ? "ok" : x.Root.GetProperty("code").GetString()));
            Console.WriteLine($"G13_REALISTIC shipments={shipments} outcomes={summary} codes={codes} " +
                $"reports={outcomes.Count(x => x.Status == HttpStatusCode.OK)} elapsed_ms={clock.Elapsed.TotalMilliseconds:F0} " +
                $"per_request_ms=[{string.Join(",", outcomes.Select(x => x.Ms.ToString("F0")))}]");
            var reports = new List<Guid>();
            foreach (var outcome in outcomes)
            {
                if (outcome.Status == HttpStatusCode.OK)
                {
                    var total = outcome.Root.GetProperty("counts").EnumerateArray()
                        .Where(x => x.GetProperty("metricCode").GetString() == "D01")
                        .Sum(x => x.GetProperty("count").GetInt64());
                    Assert.Equal(shipments - shipments / 37 + 2L, total); // archived rows (every 37th) are not active
                    Assert.InRange(outcome.Root.GetProperty("memberCount").GetInt32(), 1, 250_000);
                    reports.Add(outcome.Root.GetProperty("reportId").GetGuid());
                }
                else
                {
                    // Only bounded, fail-closed refusals carrying a documented code.
                    var code = outcome.Root.GetProperty("code").GetString();
                    var allowed = outcome.Status switch
                    {
                        HttpStatusCode.RequestEntityTooLarge => code == "report_capacity_exceeded",
                        HttpStatusCode.TooManyRequests => code == "report_tenant_capacity_exceeded",
                        HttpStatusCode.Conflict => code == "report_in_progress",
                        HttpStatusCode.ServiceUnavailable => code is "report_dependency_unavailable" or "report_timeout",
                        _ => false
                    };
                    Assert.True(allowed, $"Unbounded or undocumented refusal {(int)outcome.Status} {code}");
                    Assert.False(outcome.Root.TryGetProperty("counts", out _));
                }
            }
            Assert.Equal(reports.Count, reports.Distinct().Count());
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30),
                $"Concurrent realistic build exceeded the 30 second proxy envelope: {clock.Elapsed}");
            if (expectReports)
            {
                // Inside capacity there is no acceptable refusal: all four complete.
                Assert.Equal(4, reports.Count);
                Assert.Equal(4, outcomes.Count(o => o.Status == HttpStatusCode.OK));
                var refused = outcomes.Where(o => o.Status != HttpStatusCode.OK).ToList();
                Assert.Equal(builders - 4, refused.Count);
                // Extra builders are refused at admission: the explicit 429, quickly,
                // and never after consuming a full materialization.
                Assert.All(refused, o =>
                {
                    Assert.Equal(HttpStatusCode.TooManyRequests, o.Status);
                    Assert.Equal("report_tenant_capacity_exceeded", o.Root.GetProperty("code").GetString());
                    Assert.True(o.Ms < 10_000, $"Capacity refusal took {o.Ms:F0} ms");
                });
            }
            else
            {
                // Beyond capacity every builder gets the same bounded refusal.
                Assert.Empty(reports);
                Assert.All(outcomes, o =>
                {
                    Assert.Equal(HttpStatusCode.RequestEntityTooLarge, o.Status);
                    Assert.Equal("report_capacity_exceeded", o.Root.GetProperty("code").GetString());
                });
            }
            await PlannerGuard.AssertDefaultAsync(api.Services);

            // No partial snapshot may remain: exactly the successful reports exist.
            await using (var persisted = new NpgsqlCommand(
                "SELECT count(*) FROM operational_kpi_reports WHERE organization_id=@org", sql))
            {
                persisted.Parameters.AddWithValue("org", f.A);
                Assert.Equal((long)reports.Count, Convert.ToInt64(await persisted.ExecuteScalarAsync()));
            }
            // Admission locks must be released after refusals and timeouts.
            await Task.Delay(TimeSpan.FromSeconds(3));
            await using (var locks = new NpgsqlCommand(
                "SELECT count(*) FROM pg_locks WHERE locktype='advisory' AND granted", sql))
                Assert.Equal(0L, Convert.ToInt64(await locks.ExecuteScalarAsync()));

        }
        finally
        {
            foreach (var client in clients) client.Dispose();
        }
    }
}
