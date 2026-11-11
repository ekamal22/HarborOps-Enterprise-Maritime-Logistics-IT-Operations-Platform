using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        // Four live snapshots come from two users with two sessions each (the
        // per-user limit is two); the fifth request is a third, different user.
        var thirdUser = await ProvisionAnalystAsync(seedFactory, fixture.AdminEmail, fixture.A);
        var emails = new[] { fixture.AnalystEmail, fixture.AnalystEmail,
            fixture.ManagerEmail, fixture.ManagerEmail, thirdUser };
        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        var clients = new List<HttpClient>();
        try
        {
            foreach (var email in emails)
            {
                var client = runtime.CreateClient(new WebApplicationFactoryClientOptions
                    { HandleCookies = true });
                clients.Add(client);
                await ReferenceDataHttpTests.Login(client, email);
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
            async Task<long> BusinessRows()
            {
                await using var query = new NpgsqlCommand("""
                    SELECT (SELECT count(*) FROM shipments WHERE organization_id=@org)
                         + (SELECT count(*) FROM shipment_status_changes WHERE organization_id=@org)
                         + (SELECT count(*) FROM shipment_voyage_assignments WHERE organization_id=@org)
                         + (SELECT count(*) FROM shipment_tracking_projections WHERE organization_id=@org)
                         + (SELECT count(*) FROM shipment_tracking_corrections WHERE organization_id=@org)
                         + (SELECT count(*) FROM shipment_exception_evaluations WHERE organization_id=@org)
                         + (SELECT count(*) FROM shipment_exceptions WHERE organization_id=@org)
                         + (SELECT count(*) FROM shipment_exception_history WHERE organization_id=@org)
                         + (SELECT count(*) FROM audit_entries WHERE organization_id=@org)
                    """, administratorDb);
                query.Parameters.AddWithValue("org", fixture.A);
                return Convert.ToInt64(await query.ExecuteScalarAsync());
            }
            var businessBefore = await BusinessRows();
            Assert.True(businessBefore > 0);
            using (var denied = await clients[4].GetAsync(url))
            {
                Assert.Equal(HttpStatusCode.TooManyRequests, denied.StatusCode);
                var error = await denied.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("report_tenant_capacity_exceeded",
                    error.GetProperty("code").GetString());
                // Exact error body: a code, a fixed message and a correlation id.
                // Nothing else, so no count, member, report or tenant detail leaks.
                Assert.Equal(new[] { "code", "correlationId", "message" },
                    error.EnumerateObject().Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));
                Assert.Equal("The organization's reporting capacity is currently in use",
                    error.GetProperty("message").GetString());
            }
            await PlannerGuard.AssertDefaultAsync(runtime.Services);
            Assert.Equal(4, await ReportCount());
            Assert.Equal(businessBefore, await BusinessRows());

            // No unsigned SQL connection can enumerate another user's quota.
            await using (var unauthorized = new NpgsqlCommand(
                "SELECT m13_tenant_quota(@org)", apiDb))
            {
                unauthorized.Parameters.AddWithValue("org", fixture.A);
                var failure = await Assert.ThrowsAsync<PostgresException>(
                    () => unauthorized.ExecuteScalarAsync());
                Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, failure.SqlState);
            }

            await using (var unauthorizedUser = new NpgsqlCommand(
                "SELECT m13_user_quota(@org)", apiDb))
            {
                unauthorizedUser.Parameters.AddWithValue("org", fixture.A);
                var failure = await Assert.ThrowsAsync<PostgresException>(
                    () => unauthorizedUser.ExecuteScalarAsync());
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
            await PlannerGuard.AssertDefaultAsync(runtime.Services);
            Assert.Equal(4, await ReportCount());

            // Neither the refusal, the bounded pruning nor the readmission removed or
            // changed a single business record of the tenant.
            Assert.Equal(businessBefore, await BusinessRows());
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

        async Task<long> Business()
        {
            await using var query = new NpgsqlCommand("""
                SELECT (SELECT count(*) FROM shipments)
                     + (SELECT count(*) FROM shipment_status_changes)
                     + (SELECT count(*) FROM shipment_voyage_assignments)
                     + (SELECT count(*) FROM shipment_tracking_projections)
                     + (SELECT count(*) FROM shipment_tracking_corrections)
                     + (SELECT count(*) FROM shipment_exception_evaluations)
                     + (SELECT count(*) FROM shipment_exceptions)
                     + (SELECT count(*) FROM shipment_exception_history)
                     + (SELECT count(*) FROM audit_entries)
                """, administratorDb);
            return Convert.ToInt64(await query.ExecuteScalarAsync());
        }
        var businessBefore = await Business();
        Assert.True(businessBefore > 0);
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

        // Cleanup is limited to reporting snapshots: not one business row anywhere
        // was removed or added by the four calls above.
        Assert.Equal(businessBefore, await Business());
    }

    internal static async Task<string> ProvisionAnalystAsync(
        ReferenceDataHttpTests.ApiFactory factory, string adminEmail, Guid org)
    {
        const string password = "Synthetic-reference-integration-passphrase-123";
        await using var scope = factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var orgs = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var admin = await identity.LoginAsync(adminEmail, password, "g13-capacity",
            CancellationToken.None);
        Assert.NotNull(admin);
        var selected = await orgs.SelectAsync(admin!.UserId, admin.SessionId, org,
            CancellationToken.None);
        Assert.NotNull(selected.Value);
        var email = "g13-capacity-" + Guid.NewGuid().ToString("N") + "@example.test";
        await identity.ProvisionAsync(email, "Capacity Reader", password,
            CancellationToken.None);
        var login = await identity.LoginAsync(email, password, "g13-capacity",
            CancellationToken.None);
        Assert.NotNull(login);
        var granted = await orgs.PutMemberAsync(admin.UserId, admin.SessionId, org,
            login!.UserId, "Analyst", "Active", null, "g13-capacity",
            CancellationToken.None);
        Assert.NotNull(granted.Value);
        return email;
    }

    // Decision: a single user may hold at most two live reports across all
    // sessions, while the four-per-tenant limit stays in force.
    [ExclusiveM13PostgresFact]
    public async Task OneUserIsLimitedToTwoActiveReportsAcrossSessionsInsideTheTenantLimit()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(seedFactory);
        using var administrator = fixture.Admin;
        using var originalManager = fixture.Manager;
        using var originalAnalyst = fixture.Analyst;
        await AssignmentHttpTests.Seed(fixture);
        var other = await ProvisionAnalystAsync(seedFactory, fixture.AdminEmail, fixture.A);

        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        var clients = new List<HttpClient>();
        try
        {
            foreach (var email in new[] { fixture.AnalystEmail, fixture.AnalystEmail,
                fixture.AnalystEmail, other })
            {
                var client = runtime.CreateClient(new WebApplicationFactoryClientOptions
                    { HandleCookies = true });
                clients.Add(client);
                await ReferenceDataHttpTests.Login(client, email);
                await ReferenceDataHttpTests.Select(client, fixture.A);
            }
            var url = $"/api/v1/organizations/{fixture.A}/analytics/overview";
            await using var administratorDb = new NpgsqlConnection(roles.Admin);
            await administratorDb.OpenAsync();
            async Task<long> Reports()
            {
                await using var query = new NpgsqlCommand(
                    "SELECT count(*) FROM operational_kpi_reports WHERE organization_id=@org",
                    administratorDb);
                query.Parameters.AddWithValue("org", fixture.A);
                return Convert.ToInt64(await query.ExecuteScalarAsync());
            }

            using var first = await clients[0].GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            using var second = await clients[1].GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Equal(2L, await Reports());

            // A third session of the same user is refused with its own code and
            // leaves no partial snapshot.
            using (var third = await clients[2].GetAsync(url))
            {
                Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
                var error = await third.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("report_user_capacity_exceeded", error.GetProperty("code").GetString());
                Assert.False(error.TryGetProperty("counts", out _));
            }
            await PlannerGuard.AssertDefaultAsync(runtime.Services);
            Assert.Equal(2L, await Reports());

            // The refusal is per user: another user is still admitted inside the
            // four-per-tenant limit, and the first user's own session can refresh.
            using (var otherUser = await clients[3].GetAsync(url))
                Assert.Equal(HttpStatusCode.OK, otherUser.StatusCode);
            Assert.Equal(3L, await Reports());
            using (var refresh = await clients[0].GetAsync(url))
                Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
            Assert.Equal(3L, await Reports());

            // Expiry of the user's oldest snapshot restores that user's slot.
            await using (var expire = new NpgsqlCommand("""
                UPDATE operational_kpi_reports
                SET created_at_utc=clock_timestamp()-interval '15 minutes',
                    expires_at_utc=clock_timestamp()-interval '5 minutes'
                WHERE id=(SELECT r.id FROM operational_kpi_reports r
                          JOIN users u ON u.id=r.actor_user_id
                          WHERE r.organization_id=@org
                            AND lower(u.normalized_email)=lower(@email)
                          ORDER BY r.created_at_utc LIMIT 1)
                """, administratorDb))
            {
                expire.Parameters.AddWithValue("org", fixture.A);
                expire.Parameters.AddWithValue("email", fixture.AnalystEmail);
                Assert.Equal(1, await expire.ExecuteNonQueryAsync());
            }
            using var admitted = await clients[2].GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, admitted.StatusCode);
        }
        finally
        {
            foreach (var client in clients) client.Dispose();
        }
    }

    // Decision: the idle sweeper is off unless explicitly enabled. Request-scoped
    // bounded expiry handling is unaffected by the switch.
    [ExclusiveM13PostgresFact]
    public async Task IdleCleanupIsOffByDefaultAndRunsOnlyWhenExplicitlyEnabled()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(seedFactory);
        using var administrator = fixture.Admin;
        using var originalManager = fixture.Manager;
        using var originalAnalyst = fixture.Analyst;
        await AssignmentHttpTests.Seed(fixture);

        await using var db = new NpgsqlConnection(roles.Admin);
        await db.OpenAsync();
        Guid manager, session;
        await using (var lookup = new NpgsqlCommand("""
            SELECT a.user_id,a.id FROM auth_sessions a
            JOIN organization_memberships m ON m.user_id=a.user_id
                AND m.organization_id=a.selected_organization_id
            WHERE a.selected_organization_id=@org AND m.role='Manager'
                AND m.status='Active' AND a.revoked_at_utc IS NULL
            ORDER BY a.issued_at_utc DESC LIMIT 1
            """, db))
        {
            lookup.Parameters.AddWithValue("org", fixture.A);
            await using var row = await lookup.ExecuteReaderAsync();
            Assert.True(await row.ReadAsync());
            manager = row.GetGuid(0);
            session = row.GetGuid(1);
        }
        async Task SeedExpired(int count)
        {
            await using var insert = new NpgsqlCommand("""
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
                FROM generate_series(1,@count) AS i
                """, db);
            insert.Parameters.AddWithValue("org", fixture.A);
            insert.Parameters.AddWithValue("manager", manager);
            insert.Parameters.AddWithValue("session", session);
            insert.Parameters.AddWithValue("count", count);
            Assert.Equal(count, await insert.ExecuteNonQueryAsync());
        }
        async Task<long> Synthetic()
        {
            await using var query = new NpgsqlCommand("""
                SELECT count(*) FROM operational_kpi_reports
                WHERE organization_id=@org
                    AND created_at_utc<clock_timestamp()-interval '50 years'
                """, db);
            query.Parameters.AddWithValue("org", fixture.A);
            return Convert.ToInt64(await query.ExecuteScalarAsync());
        }

        // Default: the hosted service is not registered, even for the restricted login.
        using (var byDefault = new ReferenceDataHttpTests.ApiFactory(roles.Api))
        {
            using var client = byDefault.CreateClient();
            Assert.DoesNotContain(byDefault.Services.GetServices<IHostedService>(),
                x => x.GetType().Name == "OperationalKpiCleanupService");
        }
        // An explicit false keeps it off.
        using (var explicitOff = new ReferenceDataHttpTests.ApiFactory(roles.Api, null, null,
            new Dictionary<string, string> { ["M13:EnableIdleCleanup"] = "false" }))
        {
            using var client = explicitOff.CreateClient();
            Assert.DoesNotContain(explicitOff.Services.GetServices<IHostedService>(),
                x => x.GetType().Name == "OperationalKpiCleanupService");
        }
        await SeedExpired(40);
        await Task.Delay(TimeSpan.FromSeconds(4));
        Assert.Equal(40L, await Synthetic());

        // Enabled with a one second poll: expired snapshots are removed, nothing else.
        long shipmentsBefore;
        await using (var count = new NpgsqlCommand(
            "SELECT count(*) FROM shipments WHERE organization_id=@org", db))
        {
            count.Parameters.AddWithValue("org", fixture.A);
            shipmentsBefore = Convert.ToInt64(await count.ExecuteScalarAsync());
        }
        using (var enabled = new ReferenceDataHttpTests.ApiFactory(roles.Api, null, null,
            new Dictionary<string, string>
            {
                ["M13:EnableIdleCleanup"] = "true",
                ["M13:IdleCleanupPollSeconds"] = "1"
            }))
        {
            using var client = enabled.CreateClient();
            Assert.Contains(enabled.Services.GetServices<IHostedService>(),
                x => x.GetType().Name == "OperationalKpiCleanupService");
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (await Synthetic() > 0 && DateTime.UtcNow < deadline)
                await Task.Delay(500);
            Assert.Equal(0L, await Synthetic());
        }
        await using (var count = new NpgsqlCommand(
            "SELECT count(*) FROM shipments WHERE organization_id=@org", db))
        {
            count.Parameters.AddWithValue("org", fixture.A);
            Assert.Equal(shipmentsBefore, Convert.ToInt64(await count.ExecuteScalarAsync()));
        }
    }

    internal static string SignedAuthority(Guid org, Guid actor, Guid session)
    {
        var expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30;
        var data = System.Text.Encoding.UTF8.GetBytes(
            $"m11-read-v1|{org:N}|{actor:N}|{session:N}|{expires}");
        var digest = System.Security.Cryptography.HMACSHA256.HashData(
            Convert.FromBase64String(ReferenceDataHttpTests.CorrectionProofKey), data);
        return $"{org:D}|{actor:D}|{session:D}|{expires}|" +
            Convert.ToHexString(digest).ToLowerInvariant();
    }

    // The migration-owned reporting readers and quota functions authorize every
    // call: unsigned logins, an Administrator, and a scope for another tenant are
    // all refused with insufficient_privilege; a signed Analyst reads.
    [ExclusiveM13PostgresFact]
    public async Task ScopedReadersAndQuotaFunctionsRequireSignedReportingAuthority()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var fixture = await ReferenceDataHttpTests.Setup(seedFactory);
        using var administrator = fixture.Admin;
        using var originalManager = fixture.Manager;
        using var originalAnalyst = fixture.Analyst;
        await AssignmentHttpTests.Seed(fixture);
        await using var admin = new NpgsqlConnection(roles.Admin);
        await admin.OpenAsync();
        async Task<(Guid User, Guid Session)> Principal(string email)
        {
            await using var find = new NpgsqlCommand("""
                SELECT u.id,(SELECT a.id FROM auth_sessions a WHERE a.user_id=u.id
                    AND a.selected_organization_id=@org AND a.revoked_at_utc IS NULL
                    ORDER BY a.issued_at_utc DESC LIMIT 1)
                FROM users u WHERE lower(u.normalized_email)=lower(@email)
                """, admin);
            find.Parameters.AddWithValue("org", fixture.A);
            find.Parameters.AddWithValue("email", email);
            await using var row = await find.ExecuteReaderAsync();
            Assert.True(await row.ReadAsync());
            return (row.GetGuid(0), row.GetGuid(1));
        }
        var analyst = await Principal(fixture.AnalystEmail);
        var adminPrincipal = await Principal(fixture.AdminEmail);
        string[] calls =
        {
            "SELECT count(*) FROM m13_scoped_evaluations(@org)",
            "SELECT count(*) FROM m13_scoped_exceptions(@org)",
            "SELECT m13_user_quota(@org)",
            "SELECT m13_tenant_quota(@org)",
            "SELECT m13_tenant_live_reports(@org,'00000000-0000-0000-0000-000000000000'::uuid)"
        };
        async Task<string?> Run(string sql, Guid org, string? authority)
        {
            await using var api = new NpgsqlConnection(roles.Api);
            await api.OpenAsync();
            await using var tx = await api.BeginTransactionAsync();
            if (authority is not null)
            {
                await using var scope = new NpgsqlCommand(
                    "SELECT set_config('harborops.m11_read_scope',@authority,true)", api, tx);
                scope.Parameters.AddWithValue("authority", authority);
                await scope.ExecuteNonQueryAsync();
            }
            try
            {
                await using var command = new NpgsqlCommand(sql, api, tx);
                command.Parameters.AddWithValue("org", org);
                await command.ExecuteScalarAsync();
                return null;
            }
            catch (PostgresException e) { return e.SqlState; }
            finally { await tx.RollbackAsync(); }
        }
        foreach (var sql in calls)
        {
            // Unsigned login, Administrator scope, and an Analyst scope aimed at another tenant.
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await Run(sql, fixture.A, null));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await Run(sql, fixture.A,
                SignedAuthority(fixture.A, adminPrincipal.User, adminPrincipal.Session)));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, await Run(sql, fixture.B,
                SignedAuthority(fixture.A, analyst.User, analyst.Session)));
            // The authorized Analyst of the same tenant reads.
            Assert.Null(await Run(sql, fixture.A,
                SignedAuthority(fixture.A, analyst.User, analyst.Session)));
        }
    }
    // Cancellation while the cleanup DELETE is actually executing must roll
    // back the snapshot deletion. The trigger is created only in a disposable
    // exclusive verification database, and is always removed afterwards.
    [ExclusiveM13PostgresFact]
    public async Task InterruptedIdleCleanupRollsBackWithoutRemovingReportsOrBusinessEvidence()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(seedFactory);
        using var administrator = f.Admin;
        using var seedManager = f.Manager;
        using var seedAnalyst = f.Analyst;
        await AssignmentHttpTests.Seed(f);

        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var manager = runtime.CreateClient(new WebApplicationFactoryClientOptions
            { HandleCookies = true });
        await ReferenceDataHttpTests.Login(manager, f.ManagerEmail);
        await ReferenceDataHttpTests.Select(manager, f.A);
        Guid report;
        using (var built = await manager.GetAsync(
            $"/api/v1/organizations/{f.A}/analytics/overview"))
        {
            Assert.Equal(HttpStatusCode.OK, built.StatusCode);
            var body = await built.Content.ReadFromJsonAsync<JsonElement>();
            report = body.GetProperty("reportId").GetGuid();
        }

        await using var db = new NpgsqlConnection(roles.Admin);
        await db.OpenAsync();
        await using (var expire = new NpgsqlCommand("""
            UPDATE operational_kpi_reports SET
                created_at_utc=clock_timestamp()-interval '15 minutes',
                expires_at_utc=clock_timestamp()-interval '5 minutes'
            WHERE id=@id AND organization_id=@org
            """, db))
        {
            expire.Parameters.AddWithValue("id", report);
            expire.Parameters.AddWithValue("org", f.A);
            Assert.Equal(1, await expire.ExecuteNonQueryAsync());
        }
        async Task<long> SourceCount()
        {
            await using var query = new NpgsqlCommand("""
                SELECT (SELECT count(*) FROM shipments)
                     + (SELECT count(*) FROM shipment_status_changes)
                     + (SELECT count(*) FROM shipment_voyage_assignments)
                     + (SELECT count(*) FROM shipment_tracking_projections)
                     + (SELECT count(*) FROM shipment_tracking_corrections)
                     + (SELECT count(*) FROM shipment_exception_evaluations)
                     + (SELECT count(*) FROM shipment_exceptions)
                     + (SELECT count(*) FROM shipment_exception_history)
                     + (SELECT count(*) FROM audit_entries)
                """, db);
            return Convert.ToInt64(await query.ExecuteScalarAsync());
        }
        var before = await SourceCount();
        var triggerCreated = false;
        try
        {
            await using (var setup = new NpgsqlCommand("""
                CREATE FUNCTION g13_verification_delay_cleanup() RETURNS trigger
                LANGUAGE plpgsql AS $delay$
                BEGIN
                    PERFORM pg_sleep(20);
                    RETURN OLD;
                END $delay$;
                CREATE TRIGGER tr_g13_verification_delay_cleanup
                BEFORE DELETE ON operational_kpi_reports FOR EACH ROW
                EXECUTE FUNCTION g13_verification_delay_cleanup();
                """, db))
                await setup.ExecuteNonQueryAsync();
            triggerCreated = true;

            await using var api = new NpgsqlConnection(roles.Api);
            await api.OpenAsync();
            using var cancellation = new CancellationTokenSource();
            await using var cleanup = new NpgsqlCommand(
                "SELECT m13_prune_expired_idle(1)", api);
            var pending = cleanup.ExecuteScalarAsync(cancellation.Token);
            var insideTrigger = false;
            for (var attempt = 0; attempt < 50 && !insideTrigger; attempt++)
            {
                await using var monitor = new NpgsqlCommand("""
                    SELECT EXISTS(
                        SELECT 1 FROM pg_stat_activity
                        WHERE usename='harborops_api'
                          AND wait_event_type='Timeout'
                          AND wait_event='PgSleep'
                          AND query LIKE '%m13_prune_expired_idle%'
                    )
                    """, db);
                insideTrigger = await monitor.ExecuteScalarAsync() is true;
                if (!insideTrigger) await Task.Delay(100);
            }
            Assert.True(insideTrigger, "Cleanup did not reach its DELETE trigger");
            cancellation.Cancel();
            var error = await Record.ExceptionAsync(async () => await pending);
            Assert.NotNull(error);
            Assert.True(error is OperationCanceledException or PostgresException,
                "Unexpected cleanup cancellation exception: " + error.GetType().Name);
            if (error is PostgresException pg)
                Assert.Equal(PostgresErrorCodes.QueryCanceled, pg.SqlState);
        }
        finally
        {
            if (triggerCreated)
            {
                await using var remove = new NpgsqlCommand("""
                    DROP TRIGGER tr_g13_verification_delay_cleanup ON operational_kpi_reports;
                    DROP FUNCTION g13_verification_delay_cleanup();
                    """, db);
                await remove.ExecuteNonQueryAsync();
            }
        }
        await using (var stillThere = new NpgsqlCommand("""
            SELECT count(*) FROM operational_kpi_reports
            WHERE id=@id AND organization_id=@org
            """, db))
        {
            stillThere.Parameters.AddWithValue("id", report);
            stillThere.Parameters.AddWithValue("org", f.A);
            Assert.Equal(1L, Convert.ToInt64(await stillThere.ExecuteScalarAsync()));
        }
        Assert.Equal(before, await SourceCount());

        await using var retryApi = new NpgsqlConnection(roles.Api);
        await retryApi.OpenAsync();
        await using var retry = new NpgsqlCommand(
            "SELECT m13_prune_expired_idle(1)", retryApi);
        Assert.Equal(1, Convert.ToInt32(await retry.ExecuteScalarAsync()));
        Assert.Equal(before, await SourceCount());
    }

    // The privileged live-count reader is bound to the signed tenant and
    // excludes expired snapshots and the report currently being assembled.
    [ExclusiveM13PostgresFact]
    public async Task TenantLiveReportCountRequiresAuthorityAndExcludesOtherTenantsAndExpiredRows()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(seedFactory);
        using var administrator = f.Admin;
        using var originalManager = f.Manager;
        using var originalAnalyst = f.Analyst;
        await AssignmentHttpTests.Seed(f);
        await using var admin = new NpgsqlConnection(roles.Admin);
        await admin.OpenAsync();
        Guid actor, session;
        await using (var principal = new NpgsqlCommand("""
            SELECT a.user_id,a.id FROM auth_sessions a
            JOIN users u ON u.id=a.user_id
            WHERE a.selected_organization_id=@org AND
                lower(u.normalized_email)=lower(@email) AND
                a.revoked_at_utc IS NULL
            ORDER BY a.issued_at_utc DESC LIMIT 1
            """, admin))
        {
            principal.Parameters.AddWithValue("org", f.A);
            principal.Parameters.AddWithValue("email", f.AnalystEmail);
            await using var row = await principal.ExecuteReaderAsync();
            Assert.True(await row.ReadAsync());
            actor = row.GetGuid(0);
            session = row.GetGuid(1);
        }

        var liveId = Guid.NewGuid();
        async Task Add(Guid id, Guid org, bool expired)
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO operational_kpi_reports
                    (id,organization_id,actor_user_id,actor_session_id,
                     policy_version,filters_sha256,from_utc,before_utc,
                     evaluated_at_utc,created_at_utc,expires_at_utc)
                VALUES (@id,@org,@actor,@session,'m13-kpi-v1',repeat('0',64),
                    clock_timestamp()-interval '9 days',
                    clock_timestamp()-interval '2 days',
                    clock_timestamp(),
                    CASE WHEN @expired THEN clock_timestamp()-interval '20 minutes'
                        ELSE clock_timestamp() END,
                    CASE WHEN @expired THEN clock_timestamp()-interval '10 minutes'
                        ELSE clock_timestamp()+interval '5 minutes' END)
                """, admin);
            insert.Parameters.AddWithValue("id", id);
            insert.Parameters.AddWithValue("org", org);
            insert.Parameters.AddWithValue("actor", actor);
            insert.Parameters.AddWithValue("session", session);
            insert.Parameters.AddWithValue("expired", expired);
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());
        }
        await Add(liveId, f.A, false);
        await Add(Guid.NewGuid(), f.A, true);
        await Add(Guid.NewGuid(), f.B, false);

        async Task<int> Count(Guid org, Guid exclude, string? authority)
        {
            await using var api = new NpgsqlConnection(roles.Api);
            await api.OpenAsync();
            await using var tx = await api.BeginTransactionAsync();
            if (authority is not null)
            {
                await using var set = new NpgsqlCommand(
                    "SELECT set_config('harborops.m11_read_scope',@proof,true)", api, tx);
                set.Parameters.AddWithValue("proof", authority);
                await set.ExecuteScalarAsync();
            }
            try
            {
                await using var command = new NpgsqlCommand(
                    "SELECT m13_tenant_live_reports(@org,@exclude)", api, tx);
                command.Parameters.AddWithValue("org", org);
                command.Parameters.AddWithValue("exclude", exclude);
                return Convert.ToInt32(await command.ExecuteScalarAsync());
            }
            finally { await tx.RollbackAsync(); }
        }
        var proof = SignedAuthority(f.A, actor, session);
        Assert.Equal(1, await Count(f.A, Guid.Empty, proof));
        Assert.Equal(0, await Count(f.A, liveId, proof));
        var unsigned = await Assert.ThrowsAsync<PostgresException>(
            () => Count(f.A, Guid.Empty, null));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, unsigned.SqlState);
        var foreign = await Assert.ThrowsAsync<PostgresException>(
            () => Count(f.B, Guid.Empty, proof));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, foreign.SqlState);
    }

}
