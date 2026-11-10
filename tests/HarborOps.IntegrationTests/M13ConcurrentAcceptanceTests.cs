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

    // Plan S20 asks for concurrent load on a skewed, history-rich tenant. These
    // tests measure and record the outcome distribution instead of assuming it.
    // They assert only safety: every response is a correct report or a bounded,
    // fail-closed, code-bearing refusal; no partial snapshot or lock survives.
    [ExclusiveM13PostgresFact]
    public Task RealisticSkewedHundredThousandShipmentTenantServesFourPrincipalsSafely() =>
        RunRealisticTenantAsync(100_000, requireAtLeastOneReport: true);

    [ExclusiveM13PostgresFact]
    public Task RealisticSkewed170kShipmentTenantServesFourPrincipalsSafely() =>
        RunRealisticTenantAsync(170_000, requireAtLeastOneReport: false);

    private static async Task RunRealisticTenantAsync(int shipments, bool requireAtLeastOneReport)
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

        var seedClock = Stopwatch.StartNew();
        // Skewed status mix, about 3 percent archived, planned dates, a full
        // status-change history per shipment, four fresh rule evaluations for
        // every in-flight shipment and open exceptions for 5 percent of them.
        // The shipment trigger only admits Draft version 1 rows, then legal
        // single-step transitions with version increments, so the skew is
        // built the same way production data is: insert, then transition.
        await using (var bulk = new NpgsqlCommand("""
            INSERT INTO shipments (id,organization_id,reference_code,origin_port_id,
                destination_port_id,planned_departure_at_utc,planned_arrival_at_utc,
                workflow_status,record_status,source_kind,version,created_at_utc,
                updated_at_utc,changed_by_user_id)
            SELECT gen_random_uuid(),@org,'G13R-' || lpad(n::text,6,'0'),@origin,
                @destination,
                clock_timestamp()-interval '10 days'+(n%5000)*interval '1 minute',
                clock_timestamp()-interval '5 days'+(n%5000)*interval '1 minute',
                'Draft','Active','Synthetic',1,
                clock_timestamp()-interval '30 days',clock_timestamp()-interval '30 days',@actor
            FROM generate_series(1,@count) AS n
            """, sql) { CommandTimeout = 1800 })
        {
            bulk.Parameters.AddWithValue("org", f.A);
            bulk.Parameters.AddWithValue("origin", origin);
            bulk.Parameters.AddWithValue("destination", destination);
            bulk.Parameters.AddWithValue("actor", actor);
            bulk.Parameters.AddWithValue("count", shipments);
            Assert.Equal(shipments, await bulk.ExecuteNonQueryAsync());
        }
        // Rank by the numeric suffix: 1 Draft 35%, 2 Booked 20%, 3 InTransit 20%,
        // 4 Arrived 12%, 5 Completed 9%, 6 Cancelled 4%.
        const string Rank = """
            CASE WHEN right(reference_code,6)::int%100<35 THEN 1
                 WHEN right(reference_code,6)::int%100<55 THEN 2
                 WHEN right(reference_code,6)::int%100<75 THEN 3
                 WHEN right(reference_code,6)::int%100<87 THEN 4
                 WHEN right(reference_code,6)::int%100<96 THEN 5 ELSE 6 END
            """;
        var steps = new (string From, string To, string Condition)[]
        {
            ("Draft","Booked",$"({Rank}) BETWEEN 2 AND 5"),
            ("Booked","InTransit",$"({Rank}) BETWEEN 3 AND 5"),
            ("InTransit","Arrived",$"({Rank}) BETWEEN 4 AND 5"),
            ("Arrived","Completed",$"({Rank})=5"),
            ("Draft","Cancelled",$"({Rank})=6")
        };
        foreach (var step in steps)
        {
            await using var move = new NpgsqlCommand($"""
                UPDATE shipments SET workflow_status='{step.To}',version=version+1,
                    updated_at_utc=clock_timestamp(),changed_by_user_id=@actor
                WHERE organization_id=@org AND reference_code LIKE 'G13R-%'
                  AND workflow_status='{step.From}' AND {step.Condition}
                """, sql) { CommandTimeout = 1800 };
            move.Parameters.AddWithValue("org", f.A);
            move.Parameters.AddWithValue("actor", actor);
            await move.ExecuteNonQueryAsync();
        }
        // About 3 percent are archived afterwards.
        await using (var archive = new NpgsqlCommand("""
            UPDATE shipments SET record_status='Archived',archived_at_utc=clock_timestamp(),
                version=version+1,updated_at_utc=clock_timestamp(),changed_by_user_id=@actor
            WHERE organization_id=@org AND reference_code LIKE 'G13R-%'
              AND right(reference_code,6)::int%37=0
            """, sql) { CommandTimeout = 1800 })
        {
            archive.Parameters.AddWithValue("org", f.A);
            archive.Parameters.AddWithValue("actor", actor);
            await archive.ExecuteNonQueryAsync();
        }
        long history;
        await using (var changes = new NpgsqlCommand("""
            INSERT INTO shipment_status_changes (id,organization_id,shipment_id,
                from_status,to_status,version,changed_at_utc,changed_by_user_id)
            SELECT gen_random_uuid(),s.organization_id,s.id,t.f,t.t,t.v,
                clock_timestamp()-interval '3 days'+t.v*interval '6 hours',@actor
            FROM shipments s
            JOIN (VALUES (1,NULL,'Draft'),(2,'Draft','Booked'),(3,'Booked','InTransit'),
                         (4,'InTransit','Arrived'),(5,'Arrived','Completed'))
                AS t(v,f,t) ON s.workflow_status<>'Cancelled' AND t.v<=CASE s.workflow_status
                    WHEN 'Draft' THEN 1 WHEN 'Booked' THEN 2 WHEN 'InTransit' THEN 3
                    WHEN 'Arrived' THEN 4 ELSE 5 END
            WHERE s.organization_id=@org AND s.reference_code LIKE 'G13R-%'
            UNION ALL
            SELECT gen_random_uuid(),s.organization_id,s.id,t.f,t.t,t.v,
                clock_timestamp()-interval '3 days'+t.v*interval '6 hours',@actor
            FROM shipments s
            JOIN (VALUES (1,NULL,'Draft'),(2,'Draft','Cancelled')) AS t(v,f,t)
                ON s.workflow_status='Cancelled'
            WHERE s.organization_id=@org AND s.reference_code LIKE 'G13R-%'
            """, sql) { CommandTimeout = 1800 })
        {
            changes.Parameters.AddWithValue("org", f.A);
            changes.Parameters.AddWithValue("actor", actor);
            history = await changes.ExecuteNonQueryAsync();
        }
        long evaluations, openExceptions;
        await using (var evaluate = new NpgsqlCommand("""
            INSERT INTO shipment_exception_evaluations (organization_id,shipment_id,
                rule_code,rule_policy_version,condition_state,source_fingerprint,
                shipment_version,tracking_input_revision,correction_generation)
            SELECT s.organization_id,s.id,r.code,'m11-rules-v1',
                CASE WHEN right(s.reference_code,6)::int%20=0 THEN 'Active' ELSE 'Inactive' END,
                repeat('a',64),s.version,0,0
            FROM shipments s
            CROSS JOIN (VALUES ('EquipmentConflict'),('ArrivalScheduleOverdue'),
                ('RecordedArrivalLate'),('DepartureSlippageRisk')) AS r(code)
            WHERE s.organization_id=@org AND s.reference_code LIKE 'G13R-%'
              AND s.record_status='Active'
              AND s.workflow_status IN ('Booked','InTransit')
            """, sql) { CommandTimeout = 1800 })
        {
            evaluate.Parameters.AddWithValue("org", f.A);
            evaluations = await evaluate.ExecuteNonQueryAsync();
        }
        await using (var exceptions = new NpgsqlCommand("""
            INSERT INTO shipment_exceptions (organization_id,shipment_id,rule_code,
                occurrence,condition_state,lifecycle_state,severity,confidence,
                reason_code,source_fingerprint,shipment_version,tracking_input_revision,
                correction_generation,evidence)
            SELECT e.organization_id,e.shipment_id,e.rule_code,1,'Active','Open',
                'Warning','ScheduleOnly','seed_fixture',repeat('a',64),
                e.shipment_version,0,0,'{}'::jsonb
            FROM shipment_exception_evaluations e
            WHERE e.organization_id=@org AND e.condition_state='Active'
            """, sql) { CommandTimeout = 1800 })
        {
            exceptions.Parameters.AddWithValue("org", f.A);
            openExceptions = await exceptions.ExecuteNonQueryAsync();
        }
        await using (var statistics = new NpgsqlCommand("""
            ANALYZE shipments; ANALYZE shipment_status_changes;
            ANALYZE shipment_exception_evaluations; ANALYZE shipment_exceptions;
            """, sql) { CommandTimeout = 300 })
            await statistics.ExecuteNonQueryAsync();
        seedClock.Stop();
        Console.WriteLine($"G13_REALISTIC seeded shipments={shipments} history={history} " +
            $"evaluations={evaluations} open_exceptions={openExceptions} " +
            $"seed_ms={seedClock.Elapsed.TotalMilliseconds:F0}");

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
            for (var i = 0; i < 2; i++)
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

            var reports = new List<Guid>();
            foreach (var outcome in outcomes)
            {
                if (outcome.Status == HttpStatusCode.OK)
                {
                    var total = outcome.Root.GetProperty("counts").EnumerateArray()
                        .Where(x => x.GetProperty("metricCode").GetString() == "D01")
                        .Sum(x => x.GetProperty("count").GetInt64());
                    Assert.Equal(shipments + 2L, total);
                    Assert.InRange(outcome.Root.GetProperty("memberCount").GetInt32(), 1, 500_000);
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
                        HttpStatusCode.ServiceUnavailable => code == "report_dependency_unavailable",
                        _ => false
                    };
                    Assert.True(allowed, $"Unbounded or undocumented refusal {(int)outcome.Status} {code}");
                    Assert.False(outcome.Root.TryGetProperty("counts", out _));
                }
            }
            Assert.Equal(reports.Count, reports.Distinct().Count());
            Assert.True(clock.Elapsed < TimeSpan.FromMinutes(2.5),
                $"Concurrent realistic build exceeded the request envelope: {clock.Elapsed}");
            if (requireAtLeastOneReport)
                Assert.NotEmpty(reports);

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

            var summary = string.Join(",", outcomes
                .GroupBy(x => (int)x.Status).OrderBy(x => x.Key)
                .Select(x => x.Key + ":" + x.Count()));
            var codes = string.Join(",", outcomes.Select(x =>
                x.Status == HttpStatusCode.OK ? "ok" : x.Root.GetProperty("code").GetString()));
            Console.WriteLine($"G13_REALISTIC shipments={shipments} outcomes={summary} codes={codes} " +
                $"reports={reports.Count} elapsed_ms={clock.Elapsed.TotalMilliseconds:F0} " +
                $"per_request_ms=[{string.Join(",", outcomes.Select(x => x.Ms.ToString("F0")))}]");
        }
        finally
        {
            foreach (var client in clients) client.Dispose();
        }
    }
}
