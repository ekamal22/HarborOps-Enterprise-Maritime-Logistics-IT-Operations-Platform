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
}
