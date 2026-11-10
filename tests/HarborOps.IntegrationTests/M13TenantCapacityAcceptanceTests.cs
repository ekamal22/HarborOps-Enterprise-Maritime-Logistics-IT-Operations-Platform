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
            // The three remaining rows are exactly the live snapshots, by identity.
            foreach (var live in committed.Skip(1))
            {
                await using var liveReport = new NpgsqlCommand(
                    "SELECT count(*) FROM operational_kpi_reports WHERE id=@report AND organization_id=@org AND expires_at_utc>clock_timestamp()",
                    administratorDb);
                liveReport.Parameters.AddWithValue("report", live);
                liveReport.Parameters.AddWithValue("org", fixture.A);
                Assert.Equal(1L, Convert.ToInt64(await liveReport.ExecuteScalarAsync()));
            }

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

    // The idle cleanup limit must be enforced by the database function itself,
    // not only assumed by its caller. Forty synthetic snapshots are given the
    // oldest possible expiry so they are removed first regardless of leftovers
    // from other suites on the same disposable server.
    [ExclusiveM13PostgresFact]
    public async Task IdleCleanupRemovesAtMostSixteenExpiredSnapshotsPerCallAndRejectsInvalidAuthority()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(seedFactory);
        using var administrator = fixture.Admin;
        using var originalManager = fixture.Manager;
        using var originalAnalyst = fixture.Analyst;
        await AssignmentHttpTests.Seed(fixture);

        await using var administratorDb = new NpgsqlConnection(roles.Admin);
        await administratorDb.OpenAsync();
        await using var apiDb = new NpgsqlConnection(roles.Api);
        await apiDb.OpenAsync();

        Guid manager, session;
        await using (var lookup = new NpgsqlCommand("""
            SELECT a.user_id,a.id FROM auth_sessions a
            JOIN organization_memberships m ON m.user_id=a.user_id
                AND m.organization_id=a.selected_organization_id
            WHERE a.selected_organization_id=@org AND m.role='Manager'
                AND m.status='Active' AND a.revoked_at_utc IS NULL
            ORDER BY a.issued_at_utc DESC LIMIT 1
            """, administratorDb))
        {
            lookup.Parameters.AddWithValue("org", fixture.A);
            await using var row = await lookup.ExecuteReaderAsync();
            Assert.True(await row.ReadAsync());
            manager = row.GetGuid(0);
            session = row.GetGuid(1);
        }

        await using (var insert = new NpgsqlCommand("""
            INSERT INTO operational_kpi_reports
                (id,organization_id,actor_user_id,actor_session_id,
                 policy_version,filters_sha256,from_utc,before_utc,
                 evaluated_at_utc,created_at_utc,expires_at_utc)
            SELECT gen_random_uuid(),@org,@manager,@session,'m13-kpi-v1',
                repeat('0',64),clock_timestamp()-interval '9 days',
                clock_timestamp()-interval '2 days',
                clock_timestamp()-interval '100 years',
                clock_timestamp()-interval '100 years'+i*interval '1 second',
                clock_timestamp()-interval '100 years'+i*interval '1 second'
                    +interval '5 minutes'
            FROM generate_series(1,40) AS i
            """, administratorDb))
        {
            insert.Parameters.AddWithValue("org", fixture.A);
            insert.Parameters.AddWithValue("manager", manager);
            insert.Parameters.AddWithValue("session", session);
            Assert.Equal(40, await insert.ExecuteNonQueryAsync());
        }

        async Task<long> Synthetic()
        {
            await using var query = new NpgsqlCommand("""
                SELECT count(*) FROM operational_kpi_reports
                WHERE organization_id=@org
                    AND created_at_utc<clock_timestamp()-interval '50 years'
                """, administratorDb);
            query.Parameters.AddWithValue("org", fixture.A);
            return Convert.ToInt64(await query.ExecuteScalarAsync());
        }
        async Task<int> Prune(int limit)
        {
            await using var command = new NpgsqlCommand(
                "SELECT m13_prune_expired_idle(@limit)", apiDb);
            command.Parameters.AddWithValue("limit", limit);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        Assert.Equal(40L, await Synthetic());
        // The two oldest-first batches are exactly full while 40 candidates remain.
        Assert.Equal(16, await Prune(16));
        Assert.Equal(24L, await Synthetic());
        Assert.Equal(16, await Prune(16));
        Assert.Equal(8L, await Synthetic());
        // The final call can only exceed eight when other expired snapshots exist,
        // and still never exceeds the approved sixteen.
        var last = await Prune(16);
        Assert.InRange(last, 8, 16);
        Assert.Equal(0L, await Synthetic());

        // Invalid limits and any login other than the restricted runtime role
        // are rejected by the function with insufficient_privilege.
        foreach (var invalid in new object?[] { 0, 17, -1, 1000, null })
        {
            await using var command = new NpgsqlCommand(
                "SELECT m13_prune_expired_idle(@limit)", apiDb);
            command.Parameters.Add(new NpgsqlParameter("limit",
                NpgsqlTypes.NpgsqlDbType.Integer) { Value = invalid ?? DBNull.Value });
            var failure = await Assert.ThrowsAsync<PostgresException>(
                () => command.ExecuteScalarAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, failure.SqlState);
        }
        await using (var privileged = new NpgsqlCommand(
            "SELECT m13_prune_expired_idle(16)", administratorDb))
        {
            var failure = await Assert.ThrowsAsync<PostgresException>(
                () => privileged.ExecuteScalarAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, failure.SqlState);
        }

        // Cleanup is limited to reporting snapshots: business records remain.
        await using var business = new NpgsqlCommand(
            "SELECT count(*) FROM shipments WHERE organization_id=@org", administratorDb);
        business.Parameters.AddWithValue("org", fixture.A);
        Assert.True(Convert.ToInt64(await business.ExecuteScalarAsync()) > 0);
    }
}
