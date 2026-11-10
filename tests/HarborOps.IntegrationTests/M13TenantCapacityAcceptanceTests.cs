using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

[Collection("M13ExclusiveServer")]
public sealed class M13TenantCapacityAcceptanceTests
{
    [ExclusiveM13PostgresFact]
    public async Task FourLiveSnapshotsAreAdmittedFifthDeniedAndExpiredPruningRestoresCapacity()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(seedFactory);
        using var administrator = fixture.Admin;
        using var originalManager = fixture.Manager;
        using var originalAnalyst = fixture.Analyst;
        await AssignmentHttpTests.Seed(fixture);

        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        var clients = new List<HttpClient>();
        try
        {
            for (var i = 0; i < 5; i++)
            {
                var client = runtime.CreateClient(new WebApplicationFactoryClientOptions
                    { HandleCookies = true });
                clients.Add(client);
                await ReferenceDataHttpTests.Login(client, fixture.AnalystEmail);
                await ReferenceDataHttpTests.Select(client, fixture.A);
            }

            var url = $"/api/v1/organizations/{fixture.A}/analytics/overview";
            var committed = new List<Guid>();
            foreach (var client in clients.Take(4))
            {
                using var response = await client.GetAsync(url);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
                committed.Add(payload.GetProperty("reportId").GetGuid());
            }

            await using var administratorDb = new NpgsqlConnection(roles.Admin);
            await administratorDb.OpenAsync();
            await using var apiDb = new NpgsqlConnection(roles.Api);
            await apiDb.OpenAsync();

            async Task<long> ReportCount()
            {
                await using var query = new NpgsqlCommand(
                    "SELECT count(*) FROM operational_kpi_reports WHERE organization_id=@org",
                    administratorDb);
                query.Parameters.AddWithValue("org", fixture.A);
                return Convert.ToInt64(await query.ExecuteScalarAsync());
            }

            Assert.Equal(4, await ReportCount());
            using (var denied = await clients[4].GetAsync(url))
            {
                Assert.Equal(HttpStatusCode.TooManyRequests, denied.StatusCode);
                var error = await denied.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("report_tenant_capacity_exceeded",
                    error.GetProperty("code").GetString());
                Assert.False(error.TryGetProperty("counts", out _));
            }
            Assert.Equal(4, await ReportCount());

            // No unsigned SQL connection can enumerate another user's quota.
            await using (var unauthorized = new NpgsqlCommand(
                "SELECT m13_tenant_quota(@org)", apiDb))
            {
                unauthorized.Parameters.AddWithValue("org", fixture.A);
                var failure = await Assert.ThrowsAsync<PostgresException>(
                    () => unauthorized.ExecuteScalarAsync());
                Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, failure.SqlState);
            }

            // Advance the first disposable snapshot into the past while
            // preserving the migration's expiry-window check constraint.
            await using (var expire = new NpgsqlCommand("""
                UPDATE operational_kpi_reports
                SET created_at_utc=clock_timestamp()-interval '15 minutes',
                    expires_at_utc=clock_timestamp()-interval '5 minutes'
                WHERE id=@report AND organization_id=@org
                """, administratorDb))
            {
                expire.Parameters.AddWithValue("report", committed[0]);
                expire.Parameters.AddWithValue("org", fixture.A);
                Assert.Equal(1, await expire.ExecuteNonQueryAsync());
            }
            // Idle cleanup is database-wide. Earlier tests on the same
            // disposable server can leave additional expired report snapshots.
            // Every invocation must remain capped at 16, while this tenant's
            // expired snapshot must actually be removed before readmission.
            long expiredBacklog;
            await using (var backlog = new NpgsqlCommand(
                "SELECT count(*) FROM operational_kpi_reports WHERE expires_at_utc<=clock_timestamp()",
                administratorDb))
            {
                expiredBacklog = Convert.ToInt64(await backlog.ExecuteScalarAsync());
            }
            Assert.True(expiredBacklog >= 1);
            var maxBatches = (expiredBacklog + 15) / 16;
            for (var batch = 0L; batch < maxBatches; batch++)
            {
                await using var maintenance = new NpgsqlCommand(
                    "SELECT m13_prune_expired_idle(16)", apiDb);
                var removed = Convert.ToInt32(await maintenance.ExecuteScalarAsync());
                Assert.InRange(removed, 1, 16);
                if (await ReportCount() == 3)
                    break;
            }
            Assert.Equal(3, await ReportCount());
            await using (var expiredReport = new NpgsqlCommand(
                "SELECT count(*) FROM operational_kpi_reports WHERE id=@report AND organization_id=@org",
                administratorDb))
            {
                expiredReport.Parameters.AddWithValue("report", committed[0]);
                expiredReport.Parameters.AddWithValue("org", fixture.A);
                Assert.Equal(0L, Convert.ToInt64(await expiredReport.ExecuteScalarAsync()));
            }
            // A repeated cleanup may still find other tenants' expired
            // snapshots, but it must not remove any live snapshot here.
            await using (var repeat = new NpgsqlCommand(
                "SELECT m13_prune_expired_idle(16)", apiDb))
            {
                var removed = Convert.ToInt32(await repeat.ExecuteScalarAsync());
                Assert.InRange(removed, 0, 16);
            }
            Assert.Equal(3, await ReportCount());

            using var admitted = await clients[4].GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, admitted.StatusCode);
            Assert.Equal(4, await ReportCount());

            await using var noBusinessDeletion = new NpgsqlCommand("""
                SELECT count(*) FROM shipments WHERE organization_id=@org
                """, administratorDb);
            noBusinessDeletion.Parameters.AddWithValue("org", fixture.A);
            Assert.True(Convert.ToInt64(await noBusinessDeletion.ExecuteScalarAsync()) > 0);
        }
        finally
        {
            foreach (var client in clients) client.Dispose();
        }
    }
}
