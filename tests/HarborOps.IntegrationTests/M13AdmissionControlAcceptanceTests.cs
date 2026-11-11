using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// Early admission control for the heavy KPI member statement. A tenant may hold
// four live snapshots, so no more than the remaining capacity may run the 9 to 19
// second statement at once; every other builder is refused immediately with the
// explicit 429 instead of consuming a full materialization and a 503.
[Collection("M13ExclusiveServer")]
public sealed class M13AdmissionControlAcceptanceTests
{
    private static string OverviewUrl(Guid org) =>
        $"/api/v1/organizations/{org}/analytics/overview";

    private static string SlotScope(Guid org, int slot) =>
        "m13-build-slot:" + org.ToString("N") + ":" + slot;

    private static string QuotaScope(Guid org) =>
        "m13-report-quota:" + org.ToString("N");

    private static async Task Lock(NpgsqlConnection admin, string scope)
    {
        await using var take = new NpgsqlCommand(
            "SELECT pg_advisory_lock(hashtextextended(@scope,0))", admin);
        take.Parameters.AddWithValue("scope", scope);
        await take.ExecuteScalarAsync();
    }

    private static async Task Unlock(NpgsqlConnection admin)
    {
        await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock_all()", admin);
        await release.ExecuteScalarAsync();
    }

    private static async Task<HttpClient> ClientFor(
        ReferenceDataHttpTests.ApiFactory api, string email, Guid org)
    {
        var client = api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        client.Timeout = TimeSpan.FromMinutes(2);
        await ReferenceDataHttpTests.Login(client, email);
        await ReferenceDataHttpTests.Select(client, org);
        return client;
    }

    private static async Task<long> Reports(NpgsqlConnection admin, Guid org)
    {
        await using var query = new NpgsqlCommand(
            "SELECT count(*) FROM operational_kpi_reports WHERE organization_id=@org", admin);
        query.Parameters.AddWithValue("org", org);
        return Convert.ToInt64(await query.ExecuteScalarAsync());
    }

    private static async Task AssertRefusedAsync(HttpClient client, Guid org, string code, int maxMs)
    {
        var timer = Stopwatch.StartNew();
        using var response = await client.GetAsync(OverviewUrl(org));
        timer.Stop();
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal(new[] { "code", "correlationId", "message" },
            body.EnumerateObject().Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));
        Assert.True(timer.ElapsedMilliseconds < maxMs,
            $"Capacity refusal took {timer.ElapsedMilliseconds} ms");
    }

    // Admission uses one build slot per concurrent build, limited by the capacity
    // the tenant has left. Slots are taken from outside so the outcome does not
    // depend on thread timing.
    [ExclusiveM13PostgresFact]
    public async Task BuildSlotsRefuseExcessBuildersPromptlyAndLiveReportsShrinkTheFreeSlots()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(seedFactory);
        using var administrator = f.Admin;
        using var originalManager = f.Manager;
        using var originalAnalyst = f.Analyst;
        await AssignmentHttpTests.Seed(f);
        var u4 = await M13TenantCapacityAcceptanceTests.ProvisionAnalystAsync(seedFactory, f.AdminEmail, f.A);
        var u5 = await M13TenantCapacityAcceptanceTests.ProvisionAnalystAsync(seedFactory, f.AdminEmail, f.A);

        await using var admin = new NpgsqlConnection(roles.Admin);
        await admin.OpenAsync();
        using var api = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var manager = await ClientFor(api, f.ManagerEmail, f.A);
        using var analyst = await ClientFor(api, f.AnalystEmail, f.A);
        using var fourth = await ClientFor(api, u4, f.A);
        using var fifth = await ClientFor(api, u5, f.A);

        // 1. All four slots taken: the builder is refused at once and leaves nothing.
        for (var slot = 0; slot < 4; slot++) await Lock(admin, SlotScope(f.A, slot));
        await AssertRefusedAsync(fourth, f.A, "report_tenant_capacity_exceeded", 3000);
        Assert.Equal(0L, await Reports(admin, f.A));
        await Unlock(admin);

        // 2. Three slots taken, none of the tenant's capacity used: one slot remains.
        for (var slot = 0; slot < 3; slot++) await Lock(admin, SlotScope(f.A, slot));
        using (var admitted = await fourth.GetAsync(OverviewUrl(f.A)))
            Assert.Equal(HttpStatusCode.OK, admitted.StatusCode);
        await Unlock(admin);
        Assert.Equal(1L, await Reports(admin, f.A));

        // 3. Two more live snapshots leave exactly one slot of tenant capacity, so a
        //    single held slot is enough to refuse the next builder even though three
        //    other slot numbers are free.
        foreach (var client in new[] { manager, analyst })
        {
            using var built = await client.GetAsync(OverviewUrl(f.A));
            Assert.Equal(HttpStatusCode.OK, built.StatusCode);
        }
        Assert.Equal(3L, await Reports(admin, f.A));
        await Lock(admin, SlotScope(f.A, 0));
        await AssertRefusedAsync(fifth, f.A, "report_tenant_capacity_exceeded", 3000);
        Assert.Equal(3L, await Reports(admin, f.A));
        await Unlock(admin);
        using (var last = await fifth.GetAsync(OverviewUrl(f.A)))
            Assert.Equal(HttpStatusCode.OK, last.StatusCode);
        Assert.Equal(4L, await Reports(admin, f.A));
        await PlannerGuard.AssertDefaultAsync(api.Services);
    }

    // The live-report count and all four occupied build slots must be checked
    // together. Slot numbers 2 and 3 are deliberately occupied: the old
    // positional scan of only slots 0 and 1 would admit work that cannot fit.
    [ExclusiveM13PostgresFact]
    public async Task TwoStoredReportsAndTwoNonPositionalBuildSlotsRefuseAnotherBuildEarly()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(seedFactory);
        using var administrator = f.Admin;
        using var originalManager = f.Manager;
        using var originalAnalyst = f.Analyst;
        await AssignmentHttpTests.Seed(f);
        var additional = await M13TenantCapacityAcceptanceTests.ProvisionAnalystAsync(
            seedFactory, f.AdminEmail, f.A);
        await using var admin = new NpgsqlConnection(roles.Admin);
        await admin.OpenAsync();
        using var api = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var manager = await ClientFor(api, f.ManagerEmail, f.A);
        using var analyst = await ClientFor(api, f.AnalystEmail, f.A);
        using var candidate = await ClientFor(api, additional, f.A);
        foreach (var client in new[] { manager, analyst })
        {
            using var response = await client.GetAsync(OverviewUrl(f.A));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        Assert.Equal(2L, await Reports(admin, f.A));

        await Lock(admin, SlotScope(f.A, 2));
        await Lock(admin, SlotScope(f.A, 3));
        try
        {
            await AssertRefusedAsync(candidate, f.A,
                "report_tenant_capacity_exceeded", 3000);
            Assert.Equal(2L, await Reports(admin, f.A));
        }
        finally
        {
            await Unlock(admin);
        }
        using var admitted = await candidate.GetAsync(OverviewUrl(f.A));
        Assert.Equal(HttpStatusCode.OK, admitted.StatusCode);
        Assert.Equal(3L, await Reports(admin, f.A));
    }

    // Six distinct users start together on one tenant. Exactly four snapshots
    // exist afterwards and the other two requests got the explicit 429.
    [ExclusiveM13PostgresFact]
    public async Task SixDistinctUsersGetExactlyFourReportsAndTwoCapacityRefusals()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(seedFactory);
        using var administrator = f.Admin;
        using var originalManager = f.Manager;
        using var originalAnalyst = f.Analyst;
        await AssignmentHttpTests.Seed(f);
        var emails = new List<string> { f.ManagerEmail, f.AnalystEmail };
        for (var i = 0; i < 4; i++)
            emails.Add(await M13TenantCapacityAcceptanceTests.ProvisionAnalystAsync(
                seedFactory, f.AdminEmail, f.A));

        await using var admin = new NpgsqlConnection(roles.Admin);
        await admin.OpenAsync();
        using var api = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        var clients = new List<HttpClient>();
        try
        {
            foreach (var email in emails) clients.Add(await ClientFor(api, email, f.A));
            var outcomes = await Task.WhenAll(clients.Select(async client =>
            {
                using var response = await client.GetAsync(OverviewUrl(f.A));
                var root = (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
                return (response.StatusCode, Root: root);
            }));
            Assert.Equal(4, outcomes.Count(x => x.StatusCode == HttpStatusCode.OK));
            var refused = outcomes.Where(x => x.StatusCode != HttpStatusCode.OK).ToList();
            Assert.Equal(2, refused.Count);
            Assert.All(refused, x =>
            {
                Assert.Equal(HttpStatusCode.TooManyRequests, x.StatusCode);
                Assert.Equal("report_tenant_capacity_exceeded", x.Root.GetProperty("code").GetString());
                Assert.False(x.Root.TryGetProperty("counts", out _));
            });
            Assert.Equal(4L, await Reports(admin, f.A));
            Assert.Equal(4, outcomes.Where(x => x.StatusCode == HttpStatusCode.OK)
                .Select(x => x.Root.GetProperty("reportId").GetGuid()).Distinct().Count());
            await PlannerGuard.AssertDefaultAsync(api.Services);
            await Task.Delay(TimeSpan.FromSeconds(2));
            // No report build holds a SQL transaction open once its response was sent, so
            // drill pages never run inside a report transaction.
            await using (var open = new NpgsqlCommand("""
                SELECT count(*) FROM pg_stat_activity
                WHERE usename='harborops_api' AND state LIKE 'idle in transaction%'
                """, admin))
                Assert.Equal(0L, Convert.ToInt64(await open.ExecuteScalarAsync()));
            await using var locks = new NpgsqlCommand(
                "SELECT count(*) FROM pg_locks WHERE locktype='advisory' AND granted", admin);
            Assert.Equal(0L, Convert.ToInt64(await locks.ExecuteScalarAsync()));
        }
        finally
        {
            foreach (var client in clients) client.Dispose();
        }
    }

    // A real PostgreSQL 40001 must abort the first request transaction and
    // retry once against the same source. This disposable-only fault replaces
    // and restores the migration-owned function without changing a migration.
    [ExclusiveM13PostgresFact]
    public async Task SerializationFailureAtAdmissionRetriesAndPublishesOnlyOneSnapshot()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(seedFactory);
        using var administrator = f.Admin;
        using var initialManager = f.Manager;
        using var initialAnalyst = f.Analyst;
        await AssignmentHttpTests.Seed(f);
        await using var admin = new NpgsqlConnection(roles.Admin);
        await admin.OpenAsync();
        string original;
        await using (var definition = new NpgsqlCommand("""
            SELECT pg_get_functiondef('m13_tenant_live_reports(uuid,uuid)'::regprocedure)
            """, admin))
            original = Assert.IsType<string>(await definition.ExecuteScalarAsync());

        await using (var sequence = new NpgsqlCommand(
            "CREATE SEQUENCE g13_verification_retry_sequence START WITH 1", admin))
            await sequence.ExecuteNonQueryAsync();
        try
        {
            await using (var inject = new NpgsqlCommand("""
                CREATE OR REPLACE FUNCTION m13_tenant_live_reports(p_org uuid,p_exclude uuid)
                RETURNS integer LANGUAGE plpgsql SECURITY DEFINER
                SET search_path=pg_catalog,public AS $retry$
                DECLARE active integer;
                BEGIN
                    IF session_user<>'harborops_api' OR NOT m13_can_view(p_org) THEN
                        RAISE EXCEPTION 'Reporting quota authority denied'
                            USING ERRCODE='42501';
                    END IF;
                    IF nextval('g13_verification_retry_sequence')=1 THEN
                        RAISE EXCEPTION 'Injected retryable serialization conflict'
                            USING ERRCODE='40001';
                    END IF;
                    SELECT count(*) INTO active FROM operational_kpi_reports r
                    WHERE r.organization_id=p_org AND r.id<>p_exclude
                      AND r.expires_at_utc>clock_timestamp();
                    RETURN active;
                END $retry$;
                """, admin))
                await inject.ExecuteNonQueryAsync();

            using var api = new ReferenceDataHttpTests.ApiFactory(roles.Api);
            using var manager = await ClientFor(api, f.ManagerEmail, f.A);
            using var response = await manager.GetAsync(OverviewUrl(f.A));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("memberCount").GetInt32() > 0);
            await using var observed = new NpgsqlCommand(
                "SELECT last_value FROM g13_verification_retry_sequence", admin);
            Assert.Equal(2L, Convert.ToInt64(await observed.ExecuteScalarAsync()));
            Assert.Equal(1L, await Reports(admin, f.A));
            await PlannerGuard.AssertDefaultAsync(api.Services);
        }
        finally
        {
            await using (var restore = new NpgsqlCommand(original, admin))
                await restore.ExecuteNonQueryAsync();
            await using var remove = new NpgsqlCommand(
                "DROP SEQUENCE g13_verification_retry_sequence", admin);
            await remove.ExecuteNonQueryAsync();
        }
    }

    // The tenant quota decision and the commit stay serialized: a builder that
    // finished the member statement waits for the tenant quota lock, commits nothing
    // while it waits, and commits exactly one snapshot once the lock is free.
    [ExclusiveM13PostgresFact]
    public async Task QuotaDecisionWaitsForTheTenantLockAndCommitsNothingWhileWaiting()
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
        using var api = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var client = await ClientFor(api, f.AnalystEmail, f.A);

        await Lock(admin, QuotaScope(f.A));
        var build = client.GetAsync(OverviewUrl(f.A));
        await Task.Delay(TimeSpan.FromSeconds(4));
        Assert.False(build.IsCompleted, "The build committed without the tenant quota lock");
        Assert.Equal(0L, await Reports(admin, f.A));
        await Unlock(admin);
        using var response = await build;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1L, await Reports(admin, f.A));
        await PlannerGuard.AssertDefaultAsync(api.Services);
    }

    // A client that gives up while its build waits must leave no snapshot, no
    // advisory lock and no changed planner setting, and the tenant capacity it
    // briefly held must be usable at once.
    [ExclusiveM13PostgresFact]
    public async Task ClientCancellationRollsBackReleasesTheSlotAndLeavesNoSnapshot()
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
        using var api = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var client = await ClientFor(api, f.AnalystEmail, f.A);

        await Lock(admin, QuotaScope(f.A));
        using var cancel = new CancellationTokenSource();
        var build = client.GetAsync(OverviewUrl(f.A), cancel.Token);
        await Task.Delay(TimeSpan.FromSeconds(3));
        Assert.False(build.IsCompleted);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => build);

        // The abandoned builder gives up its slot and rolls back within seconds.
        var deadline = DateTime.UtcNow.AddSeconds(30);
        long held;
        do
        {
            await using var slots = new NpgsqlCommand("""
                SELECT count(*) FROM pg_locks l
                WHERE l.locktype='advisory' AND l.granted AND l.pid<>pg_backend_pid()
                    AND l.pid IN (SELECT pid FROM pg_stat_activity
                                  WHERE usename='harborops_api')
                """, admin);
            held = Convert.ToInt64(await slots.ExecuteScalarAsync());
            if (held == 0) break;
            await Task.Delay(500);
        } while (DateTime.UtcNow < deadline);
        Assert.Equal(0L, held);
        Assert.Equal(0L, await Reports(admin, f.A));
        await Unlock(admin);
        using var again = await client.GetAsync(OverviewUrl(f.A));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(1L, await Reports(admin, f.A));
        await PlannerGuard.AssertDefaultAsync(api.Services);
    }

    // Defense in depth: the early check can be stale, because other snapshots may be
    // committed while a builder runs its heavy statement. The authoritative decision
    // under the tenant lock must still refuse and must roll the new snapshot back.
    [ExclusiveM13PostgresFact]
    public async Task LateQuotaDecisionRefusesAndRollsBackWhenCapacityWasTakenAfterTheEarlyCheck()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(seedFactory);
        using var administrator = f.Admin;
        using var originalManager = f.Manager;
        using var originalAnalyst = f.Analyst;
        await AssignmentHttpTests.Seed(f);
        var third = await M13TenantCapacityAcceptanceTests.ProvisionAnalystAsync(seedFactory, f.AdminEmail, f.A);
        var builder = await M13TenantCapacityAcceptanceTests.ProvisionAnalystAsync(seedFactory, f.AdminEmail, f.A);
        await using var admin = new NpgsqlConnection(roles.Admin);
        await admin.OpenAsync();
        using var api = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var manager = await ClientFor(api, f.ManagerEmail, f.A);
        using var analyst = await ClientFor(api, f.AnalystEmail, f.A);
        using var thirdClient = await ClientFor(api, third, f.A);
        using var fourth = await ClientFor(api, builder, f.A);
        foreach (var client in new[] { manager, analyst, thirdClient })
        {
            using var built = await client.GetAsync(OverviewUrl(f.A));
            Assert.Equal(HttpStatusCode.OK, built.StatusCode);
        }
        Assert.Equal(3L, await Reports(admin, f.A));

        // The fourth builder passes the early check (three live, one free), builds, and
        // waits at the tenant quota lock that this test holds.
        await Lock(admin, QuotaScope(f.A));
        var pending = fourth.GetAsync(OverviewUrl(f.A));
        await Task.Delay(TimeSpan.FromSeconds(4));
        Assert.False(pending.IsCompleted, "The builder must be waiting at the quota lock");

        // Meanwhile somebody else commits the last free snapshot.
        Guid actor, session;
        await using (var lookup = new NpgsqlCommand("""
            SELECT a.user_id,a.id FROM auth_sessions a
            JOIN organization_memberships m ON m.user_id=a.user_id
                AND m.organization_id=a.selected_organization_id
            WHERE a.selected_organization_id=@org AND m.role='Manager'
                AND m.status='Active' AND a.revoked_at_utc IS NULL
            ORDER BY a.issued_at_utc DESC LIMIT 1
            """, admin))
        {
            lookup.Parameters.AddWithValue("org", f.A);
            await using var row = await lookup.ExecuteReaderAsync();
            Assert.True(await row.ReadAsync());
            actor = row.GetGuid(0);
            session = row.GetGuid(1);
        }
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO operational_kpi_reports
                (id,organization_id,actor_user_id,actor_session_id,policy_version,filters_sha256,
                 from_utc,before_utc,evaluated_at_utc,created_at_utc,expires_at_utc)
            VALUES (gen_random_uuid(),@org,@actor,@session,'m13-kpi-v1',repeat('0',64),
                clock_timestamp()-interval '9 days',clock_timestamp()-interval '2 days',
                clock_timestamp(),clock_timestamp(),clock_timestamp()+interval '5 minutes')
            """, admin))
        {
            insert.Parameters.AddWithValue("org", f.A);
            insert.Parameters.AddWithValue("actor", actor);
            insert.Parameters.AddWithValue("session", session);
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());
        }
        Assert.Equal(4L, await Reports(admin, f.A));
        await Unlock(admin);

        using var response = await pending;
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("report_tenant_capacity_exceeded", body.GetProperty("code").GetString());
        // Refused and rolled back: exactly the four live snapshots remain and none is the
        // refused builder's.
        Assert.Equal(4L, await Reports(admin, f.A));
        await using var mine = new NpgsqlCommand("""
            SELECT count(*) FROM operational_kpi_reports r
            JOIN users u ON u.id=r.actor_user_id
            WHERE r.organization_id=@org AND lower(u.normalized_email)=lower(@email)
            """, admin);
        mine.Parameters.AddWithValue("org", f.A);
        mine.Parameters.AddWithValue("email", builder);
        Assert.Equal(0L, Convert.ToInt64(await mine.ExecuteScalarAsync()));
        await PlannerGuard.AssertDefaultAsync(api.Services);
    }

    // The planner and time limit changes must be transaction-local. Npgsql normally
    // resets a connection when it returns to the pool, which would hide a session
    // level SET, and the shared test logins do not pool at all. This host pools its
    // connections and disables the reset, so any setting that outlives a committed
    // build is still on the pooled connection when probed.
    [ExclusiveM13PostgresFact]
    public async Task PlannerAndTimeoutSettingsNeverOutliveABuildOnAConnectionThatIsNotReset()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var seedFactory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(seedFactory);
        using var administrator = f.Admin;
        using var originalManager = f.Manager;
        using var originalAnalyst = f.Analyst;
        await AssignmentHttpTests.Seed(f);
        // The shared test logins disable pooling, which would hand every probe a fresh
        // session and hide a leaked setting. Pool the connections and keep their state.
        var pooled = new NpgsqlConnectionStringBuilder(roles.Api)
        {
            Pooling = true, NoResetOnClose = true, MaxPoolSize = 40
        }.ConnectionString;
        using var api = new ReferenceDataHttpTests.ApiFactory(pooled);
        using var manager = await ClientFor(api, f.ManagerEmail, f.A);
        using var analyst = await ClientFor(api, f.AnalystEmail, f.A);
        foreach (var client in new[] { manager, analyst, manager, analyst })
        {
            using var built = await client.GetAsync(OverviewUrl(f.A));
            Assert.True(built.StatusCode is HttpStatusCode.OK or HttpStatusCode.TooManyRequests,
                $"Unexpected {(int)built.StatusCode}");
        }
        // A repeated build by the same session replaces its snapshot (the heavy statement runs again).
        using (var third = await manager.GetAsync(OverviewUrl(f.A)))
            Assert.True(third.StatusCode is HttpStatusCode.OK or HttpStatusCode.TooManyRequests);
        await PlannerGuard.AssertDefaultAsync(api.Services, 24);
    }

    // The migration-owned readers bypass row security, so their organization
    // predicate is the only per-row tenant filter. Two real tenants hold evaluated
    // exceptions; a signed principal of one reads only that tenant through the
    // function, and a scope for one tenant cannot read the other tenant.
    [ExclusiveM13PostgresFact]
    public async Task DirectPrivilegedReaderCallsReturnOnlyTheSelectedTenantsRows()
    {
        var first = await M11ExceptionRuntimeTests.Prepare();
        using var firstFactory = first.Factory; using var firstRuntime = first.Runtime;
        using var a1 = first.Fixture.Admin; using var m1 = first.Fixture.Manager;
        using var n1 = first.Fixture.Analyst; using var rm1 = first.Manager;
        using var ra1 = first.Analyst; using var rd1 = first.Administrator;
        await M11ExceptionRuntimeTests.EvaluateAndFind(first,
            await M11ExceptionRuntimeTests.ScheduledShipment(first));
        var second = await M11ExceptionRuntimeTests.Prepare();
        using var secondFactory = second.Factory; using var secondRuntime = second.Runtime;
        using var a2 = second.Fixture.Admin; using var m2 = second.Fixture.Manager;
        using var n2 = second.Fixture.Analyst; using var rm2 = second.Manager;
        using var ra2 = second.Analyst; using var rd2 = second.Administrator;
        await M11ExceptionRuntimeTests.EvaluateAndFind(second,
            await M11ExceptionRuntimeTests.ScheduledShipment(second));
        var orgA = first.Fixture.A;
        var orgB = second.Fixture.A;
        Assert.NotEqual(orgA, orgB);

        await using var admin = new NpgsqlConnection(first.Roles.Admin);
        await admin.OpenAsync();
        // Foreign rows really exist: both tenants have evaluations and exceptions.
        foreach (var table in new[] { "shipment_exception_evaluations", "shipment_exceptions" })
        {
            await using var both = new NpgsqlCommand(
                $"SELECT count(DISTINCT organization_id) FROM {table} WHERE organization_id IN (@a,@b)", admin);
            both.Parameters.AddWithValue("a", orgA);
            both.Parameters.AddWithValue("b", orgB);
            Assert.Equal(2L, Convert.ToInt64(await both.ExecuteScalarAsync()));
        }

        async Task<(Guid User, Guid Session)> Principal(string email, Guid org)
        {
            await using var find = new NpgsqlCommand("""
                SELECT u.id,(SELECT a.id FROM auth_sessions a WHERE a.user_id=u.id
                    AND a.selected_organization_id=@org AND a.revoked_at_utc IS NULL
                    ORDER BY a.issued_at_utc DESC LIMIT 1)
                FROM users u WHERE lower(u.normalized_email)=lower(@email)
                """, admin);
            find.Parameters.AddWithValue("org", org);
            find.Parameters.AddWithValue("email", email);
            await using var row = await find.ExecuteReaderAsync();
            Assert.True(await row.ReadAsync());
            return (row.GetGuid(0), row.GetGuid(1));
        }
        var analystA = await Principal(first.Fixture.AnalystEmail, orgA);
        var analystB = await Principal(second.Fixture.AnalystEmail, orgB);

        async Task<(long Total, long Own)> Read(string function, Guid org, string authority)
        {
            await using var api = new NpgsqlConnection(first.Roles.Api);
            await api.OpenAsync();
            await using var tx = await api.BeginTransactionAsync();
            await using (var scope = new NpgsqlCommand(
                "SELECT set_config('harborops.m11_read_scope',@authority,true)", api, tx))
            {
                scope.Parameters.AddWithValue("authority", authority);
                await scope.ExecuteNonQueryAsync();
            }
            await using var read = new NpgsqlCommand($"""
                SELECT count(*), count(*) FILTER (WHERE organization_id=@org)
                FROM {function}(@org)
                """, api, tx);
            read.Parameters.AddWithValue("org", org);
            await using var row = await read.ExecuteReaderAsync();
            Assert.True(await row.ReadAsync());
            return (row.GetInt64(0), row.GetInt64(1));
        }
        foreach (var function in new[] { "m13_scoped_evaluations", "m13_scoped_exceptions" })
        {
            var ownA = await Read(function, orgA,
                M13TenantCapacityAcceptanceTests.SignedAuthority(orgA, analystA.User, analystA.Session));
            Assert.True(ownA.Own >= 1, $"{function} returned no row for its own tenant");
            Assert.Equal(ownA.Total, ownA.Own); // no foreign row
            var ownB = await Read(function, orgB,
                M13TenantCapacityAcceptanceTests.SignedAuthority(orgB, analystB.User, analystB.Session));
            Assert.True(ownB.Own >= 1);
            Assert.Equal(ownB.Total, ownB.Own);
        }
    }
}
