using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

[Collection("M13ExclusiveServer")]
public sealed class M13ScaleLoadTests
{
    private sealed record DatabaseResources(long TempFiles, long TempBytes, long WalBytes);

    // PostgreSQL-side temporary-file and write-amplification measurements.
    // These are the database's own cumulative counters, not the testhost's
    // process memory or CPU measurements.
    private static async Task<DatabaseResources> ReadDatabaseResources(NpgsqlConnection db)
    {
        await using (var clear = new NpgsqlCommand("SELECT pg_stat_clear_snapshot()", db))
            await clear.ExecuteNonQueryAsync();
        await using var sample = new NpgsqlCommand("""
            SELECT d.temp_files,d.temp_bytes,w.wal_bytes::bigint
            FROM pg_stat_database d CROSS JOIN pg_stat_wal w
            WHERE d.datname=current_database()
            """, db);
        await using var reader = await sample.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new DatabaseResources(reader.GetInt64(0), reader.GetInt64(1),
            reader.GetInt64(2));
    }

    private static long Count(JsonElement j, string code, string dimension = "") =>
        j.GetProperty("counts").EnumerateArray()
          .Where(x => x.GetProperty("metricCode").GetString() == code &&
                      x.GetProperty("dimension").GetString() == dimension)
          .Sum(x => x.GetProperty("count").GetInt64());


    private static async Task CapturePrivilegedPlan(
        NpgsqlConnection db, Guid org, Guid actor)
    {
        Guid session;
        await using (var lookup = new NpgsqlCommand("""
            SELECT id FROM auth_sessions
            WHERE user_id=@actor AND selected_organization_id=@org
              AND revoked_at_utc IS NULL
            ORDER BY issued_at_utc DESC LIMIT 1
            """, db))
        {
            lookup.Parameters.AddWithValue("actor", actor);
            lookup.Parameters.AddWithValue("org", org);
            session = Assert.IsType<Guid>(await lookup.ExecuteScalarAsync());
        }
        var query = (string?)typeof(HarborOps.Infrastructure.Reporting.OperationalKpiStore)
            .GetField("MemberSql",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static)?.GetValue(null);
        Assert.False(string.IsNullOrWhiteSpace(query));
        var now = DateTimeOffset.UtcNow;
        var end = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var report = Guid.NewGuid();
        await using var tx = await db.BeginTransactionAsync();
        try
        {
            await using (var create = new NpgsqlCommand("""
                INSERT INTO operational_kpi_reports
                  (id,organization_id,actor_user_id,actor_session_id,
                   policy_version,filters_sha256,from_utc,before_utc,
                   evaluated_at_utc,expires_at_utc)
                VALUES (@report,@org,@actor,@session,'m13-kpi-v1',
                    repeat('0',64),@from,@before,@now,
                    clock_timestamp()+interval '10 minutes')
                """, db, tx))
            {
                create.Parameters.AddWithValue("report", report);
                create.Parameters.AddWithValue("org", org);
                create.Parameters.AddWithValue("actor", actor);
                create.Parameters.AddWithValue("session", session);
                create.Parameters.AddWithValue("from", end.AddDays(-7));
                create.Parameters.AddWithValue("before", end);
                create.Parameters.AddWithValue("now", now);
                Assert.Equal(1, await create.ExecuteNonQueryAsync());
            }
            await using var explain = new NpgsqlCommand(
                "EXPLAIN (ANALYZE, BUFFERS, WAL, FORMAT JSON) " + query,
                db, tx) { CommandTimeout = 300 };
            explain.Parameters.AddWithValue("org", org);
            explain.Parameters.AddWithValue("report", report);
            explain.Parameters.AddWithValue("from", end.AddDays(-7));
            explain.Parameters.AddWithValue("before", end);
            explain.Parameters.AddWithValue("now", now);
            explain.Parameters.AddWithValue("bucket", "day");
            foreach (var key in new[] { "origin", "destination", "voyage" })
                explain.Parameters.Add(key, NpgsqlTypes.NpgsqlDbType.Uuid).Value = DBNull.Value;
            explain.Parameters.Add("source", NpgsqlTypes.NpgsqlDbType.Text).Value = DBNull.Value;
            explain.Parameters.AddWithValue("maxrows", 250001);
            var duration = Stopwatch.StartNew();
            var raw = Assert.IsType<string>(await explain.ExecuteScalarAsync());
            duration.Stop();
            var evidence = Path.Combine(
                (Environment.GetEnvironmentVariable("HARBOROPS_EVIDENCE_WORKSPACE")
                    ?? Environment.GetEnvironmentVariable("GITHUB_WORKSPACE"))
                    ?? Directory.GetCurrentDirectory(),
                ".verification", "g13-scale");
            Directory.CreateDirectory(evidence);
            await File.WriteAllTextAsync(
                Path.Combine(evidence,"explain-admin-reference85k.json"), raw);
            using (var parsed = JsonDocument.Parse(raw))
            {
                var plan = parsed.RootElement[0].GetProperty("Plan");
                Console.WriteLine($"G13_REFERENCE_85K top_node={plan.GetProperty("Node Type").GetString()} total_ms={plan.GetProperty("Actual Total Time").GetDouble():F0}");
                var slow = new List<(string Node, double Ms, long Rows)>();
                static void Walk(JsonElement element, List<(string Node,double Ms,long Rows)> slow)
                {
                    if (element.TryGetProperty("Node Type", out var node) &&
                        element.TryGetProperty("Actual Total Time", out var ms))
                    {
                        var rows = element.TryGetProperty("Actual Rows", out var count) ? (long)count.GetDouble() : 0L;
                        slow.Add((node.GetString() ?? "", ms.GetDouble(), rows));
                    }
                    if (element.TryGetProperty("Plans", out var children))
                        foreach (var child in children.EnumerateArray())
                            Walk(child, slow);
                }
                Walk(plan,slow);
                foreach (var item in slow.OrderByDescending(x=>x.Ms).Take(12))
                    Console.WriteLine($"G13_PLAN node={item.Node} ms={item.Ms:F0} rows={item.Rows}");
            }
            Console.WriteLine($"G13_REFERENCE_85K privileged_explain_analyze_ms={duration.Elapsed.TotalMilliseconds:F0} plan_chars={raw.Length}");
        }
        catch (Exception e)
        {
            var evidence = Path.Combine(
                (Environment.GetEnvironmentVariable("HARBOROPS_EVIDENCE_WORKSPACE")
                    ?? Environment.GetEnvironmentVariable("GITHUB_WORKSPACE"))
                    ?? Directory.GetCurrentDirectory(),
                ".verification", "g13-scale");
            Directory.CreateDirectory(evidence);
            await File.WriteAllTextAsync(Path.Combine(evidence,"explain-error.txt"),
                e.GetType().Name + ": " + e.Message);
            Console.WriteLine($"G13_REFERENCE_85K privileged_explain_error={e.GetType().Name}: {e.Message}");
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // Exercise the actual materialization statement while logged in as
    // harborops_api. The signed, live M11 scope and ephemeral report exist only
    // inside this transaction; EXPLAIN ANALYZE is rolled back with its inserts.
    internal static async Task CaptureRestrictedPlan(string apiConnection,
        string adminConnection, Guid org, Guid actor, string sample,
        Guid? origin = null, int timeoutSeconds = 15, bool nestedLoopsOff = false)
    {
        Guid session;
        await using (var admin = new NpgsqlConnection(adminConnection))
        {
            await admin.OpenAsync();
            await using var find = new NpgsqlCommand("""
                SELECT id FROM auth_sessions
                WHERE user_id=@actor AND selected_organization_id=@org
                    AND revoked_at_utc IS NULL
                ORDER BY issued_at_utc DESC LIMIT 1
                """, admin);
            find.Parameters.AddWithValue("actor", actor);
            find.Parameters.AddWithValue("org", org);
            session = Assert.IsType<Guid>(await find.ExecuteScalarAsync());
        }
        var query = (string?)typeof(HarborOps.Infrastructure.Reporting.OperationalKpiStore)
            .GetField("MemberSql", System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static)?.GetValue(null);
        Assert.False(string.IsNullOrWhiteSpace(query));
        var now = DateTimeOffset.UtcNow;
        var end = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var report = Guid.NewGuid();
        var expires = now.ToUnixTimeSeconds() + 30;
        var data = System.Text.Encoding.UTF8.GetBytes(
            $"m11-read-v1|{org:N}|{actor:N}|{session:N}|{expires}");
        var digest = System.Security.Cryptography.HMACSHA256.HashData(
            Convert.FromBase64String(ReferenceDataHttpTests.CorrectionProofKey), data);
        var signature = Convert.ToHexString(digest).ToLowerInvariant();
        var authority = $"{org:D}|{actor:D}|{session:D}|{expires}|{signature}";
        await using var db = new NpgsqlConnection(apiConnection);
        await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead);
        try
        {
            await using (var who = new NpgsqlCommand(
                "SELECT current_user, session_user", db, tx))
            await using (var identity = await who.ExecuteReaderAsync())
            {
                Assert.True(await identity.ReadAsync());
                Assert.Equal("harborops_api", identity.GetString(0));
                Assert.Equal("harborops_api", identity.GetString(1));
            }
            await using (var scope = new NpgsqlCommand("""
                SELECT set_config('harborops.m11_read_scope',@authority,true),
                    set_config('harborops.m13_materialize_org',@org::text,true),
                    set_config('harborops.m13_materialize_report',@report::text,true)
                """, db, tx))
            {
                scope.Parameters.AddWithValue("authority", authority);
                scope.Parameters.AddWithValue("org", org);
                scope.Parameters.AddWithValue("report", report);
                await scope.ExecuteNonQueryAsync();
            }
            await using (var timeout = new NpgsqlCommand(
                $"SET LOCAL statement_timeout = '{timeoutSeconds}s'", db, tx))
                await timeout.ExecuteNonQueryAsync();
            if (nestedLoopsOff)
                await using (var plan = new NpgsqlCommand(
                    "SET LOCAL enable_nestloop = off", db, tx))
                    await plan.ExecuteNonQueryAsync();
            await using (var check = new NpgsqlCommand(
                "SELECT m13_can_view(@org)", db, tx))
            {
                check.Parameters.AddWithValue("org", org);
                Assert.Equal(true, await check.ExecuteScalarAsync());
            }
            await using (var create = new NpgsqlCommand("""
                INSERT INTO operational_kpi_reports
                    (id,organization_id,actor_user_id,actor_session_id,
                     policy_version,filters_sha256,from_utc,before_utc,
                     evaluated_at_utc,expires_at_utc)
                VALUES (@report,@org,@actor,@session,'m13-kpi-v1',
                    repeat('0',64),@from,@before,@now,@expires)
                """, db, tx))
            {
                create.Parameters.AddWithValue("report", report);
                create.Parameters.AddWithValue("org", org);
                create.Parameters.AddWithValue("actor", actor);
                create.Parameters.AddWithValue("session", session);
                create.Parameters.AddWithValue("from", end.AddDays(-7));
                create.Parameters.AddWithValue("before", end);
                create.Parameters.AddWithValue("now", now);
                create.Parameters.AddWithValue("expires", now.AddMinutes(10));
                Assert.Equal(1, await create.ExecuteNonQueryAsync());
            }
            await using var command = new NpgsqlCommand(
                "EXPLAIN (ANALYZE, BUFFERS, WAL, FORMAT JSON) " + query, db, tx)
                { CommandTimeout = timeoutSeconds + 20 };
            command.Parameters.AddWithValue("org", org);
            command.Parameters.AddWithValue("report", report);
            command.Parameters.AddWithValue("from", end.AddDays(-7));
            command.Parameters.AddWithValue("before", end);
            command.Parameters.AddWithValue("now", now);
            command.Parameters.AddWithValue("bucket", "day");
            command.Parameters.Add("origin", NpgsqlTypes.NpgsqlDbType.Uuid).Value =
                (object?)origin ?? DBNull.Value;
            foreach (var name in new[] { "destination", "voyage" })
                command.Parameters.Add(name, NpgsqlTypes.NpgsqlDbType.Uuid).Value = DBNull.Value;
            command.Parameters.Add("source", NpgsqlTypes.NpgsqlDbType.Text).Value = DBNull.Value;
            command.Parameters.AddWithValue("maxrows", 500001);
            var elapsed = Stopwatch.StartNew();
            var raw = Assert.IsType<string>(await command.ExecuteScalarAsync());
            elapsed.Stop();
            using var result = JsonDocument.Parse(raw);
            Assert.True(result.RootElement[0].GetProperty("Plan")
                .GetProperty("Actual Total Time").GetDouble() >= 0);
            var directory = Path.Combine(
                (Environment.GetEnvironmentVariable("HARBOROPS_EVIDENCE_WORKSPACE")
                    ?? Environment.GetEnvironmentVariable("GITHUB_WORKSPACE"))
                    ?? Directory.GetCurrentDirectory(), ".verification", "g13-scale");
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(
                Path.Combine(directory, "explain-api-" + sample + ".json"), raw);
            var nodes = new List<(string Node, string Relation, double Ms, long Rows)>();
            static void Collect(JsonElement e, List<(string, string, double, long)> into)
            {
                if (e.TryGetProperty("Node Type", out var n) &&
                    e.TryGetProperty("Actual Total Time", out var t))
                    into.Add((n.GetString() ?? "",
                        e.TryGetProperty("Relation Name", out var r) ? r.GetString() ?? "" : "",
                        t.GetDouble() * (e.TryGetProperty("Actual Loops", out var l) ? l.GetDouble() : 1),
                        e.TryGetProperty("Actual Rows", out var rows) ? (long)rows.GetDouble() : 0L));
                if (e.TryGetProperty("Plans", out var children))
                    foreach (var child in children.EnumerateArray()) Collect(child, into);
            }
            Collect(result.RootElement[0].GetProperty("Plan"), nodes);
            foreach (var node in nodes.OrderByDescending(x => x.Ms).Take(8))
                Console.WriteLine($"G13_PLAN_NODE sample={sample} node={node.Node} rel={node.Relation} ms={node.Ms:F0} rows={node.Rows}");
            Console.WriteLine($"G13_RESTRICTED_PLAN sample={sample} elapsed_ms={elapsed.Elapsed.TotalMilliseconds:F0} " +
                $"role=harborops_api bytes={System.Text.Encoding.UTF8.GetByteCount(raw)} sha256={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)))}");
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    [ExclusiveM13PostgresFact]
    public async Task Mixed85kShipmentsProduceExactBoundedReport()
    {
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var factory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var administrator = f.Admin;
        using var manager = f.Manager;
        using var analystSeed = f.Analyst;
        var seed = await AssignmentHttpTests.Seed(f);
        using var item = await manager.GetAsync(seed.Root + "/" + seed.Shipment);
        var shipment = await item.Content.ReadFromJsonAsync<JsonElement>();
        var origin = shipment.GetProperty("originPortId").GetGuid();
        var destination = shipment.GetProperty("destinationPortId").GetGuid();
        await using var db = new NpgsqlConnection(roles.Admin);
        await db.OpenAsync();
        Guid actor;
        await using (var owner = new NpgsqlCommand(
            "SELECT changed_by_user_id FROM shipments WHERE id=@id", db))
        {
            owner.Parameters.AddWithValue("id", seed.Shipment);
            actor = Assert.IsType<Guid>(await owner.ExecuteScalarAsync());
        }
        await CaptureRestrictedPlan(roles.Api, roles.Admin, f.A, actor, "small");
        var timer = Stopwatch.StartNew();
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO shipments (id,organization_id,reference_code,
               origin_port_id,destination_port_id,workflow_status,record_status,
               source_kind,version,created_at_utc,updated_at_utc,changed_by_user_id)
            SELECT gen_random_uuid(),@org,'M13S-' || lpad(n::text,6,'0'),
               @origin,@destination,'Draft','Active','Synthetic',1,
               '2026-10-01T00:00:00Z'::timestamptz,
               '2026-10-01T00:00:00Z'::timestamptz,@actor
            FROM generate_series(@start,@finish) AS n
            """, db) { CommandTimeout = 1200 })
        {
            insert.Parameters.AddWithValue("org", f.A);
            insert.Parameters.AddWithValue("origin", origin);
            insert.Parameters.AddWithValue("destination", destination);
            insert.Parameters.AddWithValue("actor", actor);
            var start = insert.Parameters.Add("start", NpgsqlTypes.NpgsqlDbType.Integer);
            var finish = insert.Parameters.Add("finish", NpgsqlTypes.NpgsqlDbType.Integer);
            start.Value = 1;
            finish.Value = 10000;
            Assert.Equal(10000, await insert.ExecuteNonQueryAsync());
            await CaptureRestrictedPlan(roles.Api, roles.Admin, f.A, actor, "medium10k");
            start.Value = 10001;
            finish.Value = 85000;
            Assert.Equal(75000, await insert.ExecuteNonQueryAsync());
        }
        await using (var update = new NpgsqlCommand("""
            UPDATE shipments SET workflow_status='Cancelled',version=version+1,
                updated_at_utc='2026-10-02T00:00:00Z'::timestamptz,
                changed_by_user_id=@actor
            WHERE organization_id=@org
              AND reference_code BETWEEN 'M13S-055001' AND 'M13S-085000'
            """, db) { CommandTimeout = 1200 })
        {
            update.Parameters.AddWithValue("org", f.A);
            update.Parameters.AddWithValue("actor", actor);
            Assert.Equal(30000, await update.ExecuteNonQueryAsync());
        }
        timer.Stop();
        Console.WriteLine($"G13_REFERENCE_85K seeded=85000 existing=2 seed_ms={timer.Elapsed.TotalMilliseconds:F0}");
        // The fixture makes 85k inserts and 30k workflow updates in one burst.
        // Capture production-like fresh optimizer statistics before admission
        // tests. The policy timeout and p50/p95 budgets remain unchanged.
        await using (var statistics = new NpgsqlCommand("""
            ANALYZE shipments;
            ANALYZE shipment_status_changes;
            ANALYZE shipment_exception_evaluations;
            ANALYZE shipment_tracking_projections;
            """, db) { CommandTimeout = 120 })
            await statistics.ExecuteNonQueryAsync();

        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var analyst = runtime.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        analyst.Timeout = TimeSpan.FromMinutes(5);
        await ReferenceDataHttpTests.Login(analyst, f.AnalystEmail);
        await ReferenceDataHttpTests.Select(analyst, f.A);

        var latencies = new List<double>();
        var cpuStart = Process.GetCurrentProcess().TotalProcessorTime;
        long initialStorage;
        await using (var storage = new NpgsqlCommand("""
            SELECT pg_total_relation_size('operational_kpi_reports') +
                   pg_total_relation_size('operational_kpi_members')
            """, db))
            initialStorage = Convert.ToInt64(await storage.ExecuteScalarAsync());
        var dbResourcesBefore = await ReadDatabaseResources(db);
        for (var i = 0; i < 30; i++)
        {
            var elapsed = Stopwatch.StartNew();
            using var result = await analyst.GetAsync($"/api/v1/organizations/{f.A}/analytics/overview");
            elapsed.Stop();
            var body = await result.Content.ReadAsStringAsync();
            Console.WriteLine($"G13_REFERENCE_85K iteration={i + 1} status={(int)result.StatusCode} ms={elapsed.Elapsed.TotalMilliseconds:F0}");
            Assert.True(result.StatusCode == HttpStatusCode.OK,
                $"85k reference report unavailable, HTTP {(int)result.StatusCode}: {body[..Math.Min(body.Length,400)]}");
            using var parsed = JsonDocument.Parse(body);
            var report = parsed.RootElement;
            Assert.Equal(85002L, Count(report,"D01"));
            Assert.Equal(55002L, Count(report,"K01"));
            Assert.Equal(55002L, Count(report,"K02","Draft"));
            Assert.Equal(30000L, Count(report,"K02","Cancelled"));
            Assert.Equal(0L, Count(report,"K04"));
            Assert.Equal(225006, report.GetProperty("memberCount").GetInt32());
            latencies.Add(elapsed.Elapsed.TotalMilliseconds);
            // Keep the measurement below the accepted six-builds/minute user
            // limiter without raising or disabling an application safeguard.
            if (i < 29 && elapsed.Elapsed < TimeSpan.FromSeconds(11))
                await Task.Delay(TimeSpan.FromSeconds(11) - elapsed.Elapsed);
        }
        latencies.Sort();
        var median = latencies[(int)Math.Ceiling(latencies.Count * 0.50) - 1];
        var p95 = latencies[(int)Math.Ceiling(latencies.Count * 0.95) - 1];
        long finalStorage;
        await using (var storage = new NpgsqlCommand("""
            SELECT pg_total_relation_size('operational_kpi_reports') +
                   pg_total_relation_size('operational_kpi_members')
            """, db))
            finalStorage = Convert.ToInt64(await storage.ExecuteScalarAsync());
        // Allow the PostgreSQL statistics collector to flush counters after
        // the API backends return to idle.
        await Task.Delay(TimeSpan.FromSeconds(2));
        var dbResourcesAfter = await ReadDatabaseResources(db);
        Assert.True(dbResourcesAfter.WalBytes >= dbResourcesBefore.WalBytes);
        Assert.True(dbResourcesAfter.TempBytes >= dbResourcesBefore.TempBytes);
        Assert.True(dbResourcesAfter.TempFiles >= dbResourcesBefore.TempFiles);
        Console.WriteLine($"G13_DB_RESOURCES workload=reference85k " +
            $"wal_bytes={dbResourcesAfter.WalBytes-dbResourcesBefore.WalBytes} " +
            $"temp_bytes={dbResourcesAfter.TempBytes-dbResourcesBefore.TempBytes} " +
            $"temp_files={dbResourcesAfter.TempFiles-dbResourcesBefore.TempFiles}");
        var process = Process.GetCurrentProcess();
        Console.WriteLine($"G13_BUDGET count={latencies.Count} p50_ms={median:F0} " +
            $"p95_ms={p95:F0} initial_report_bytes={initialStorage} " +
            $"final_report_bytes={finalStorage} peak_working_set_bytes={process.PeakWorkingSet64} " +
            $"cpu_ms={(process.TotalProcessorTime-cpuStart).TotalMilliseconds:F0} " +
            $"samples_ms=[{string.Join(",", latencies.Select(v=>v.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)))}]");
        Assert.True(median <= 15_000, $"Approved G13 p50 exceeded: {median:F0}ms");
        Assert.True(p95 <= 20_000, $"Approved G13 p95 exceeded: {p95:F0}ms");
        // Measured provisional verification envelope, not a permanent
        // production storage policy or permission to delete business evidence.
        // Prior source-locked 30-report campaign: 1,630,085,120 final bytes
        // and 608,702,464 peak process working-set bytes.
        const long ProvisionalThirtyReportGrowthCeiling = 2L * 1024 * 1024 * 1024;
        const long ProvisionalProcessWorkingSetCeiling = 1L * 1024 * 1024 * 1024;
        Assert.InRange(finalStorage - initialStorage,
            0L, ProvisionalThirtyReportGrowthCeiling);
        Assert.InRange(process.PeakWorkingSet64,
            0L, ProvisionalProcessWorkingSetCeiling);
        // High-volume EXPLAIN ANALYZE inserts rolled-back report rows. Run
        // those plans after the 30-sample storage/latency measurement so
        // EXPLAIN's dead tuples cannot contaminate the benchmark deltas.
        await CaptureRestrictedPlan(roles.Api, roles.Admin, f.A, actor, "reference85k");
        await CaptureRestrictedPlan(roles.Api, roles.Admin, f.A, actor, "empty", Guid.NewGuid());
        // Privileged EXPLAIN ANALYZE also rolls back large member inserts;
        // run it after all 30 measurements to prevent dead-tuple pollution.
        await CapturePrivilegedPlan(db, f.A, actor);


        // This is a separate cap-negative control after the 30 measured
        // successful builds. Adding 9,000 Draft rows contributes three report
        // member rows per shipment, taking the source-locked fixture over the
        // 250,000 member ceiling without changing any production limit.
        long beforeCapacityCount;
        await using (var existing = new NpgsqlCommand("""
            SELECT COUNT(*) FROM operational_kpi_reports
            WHERE organization_id=@org
            """, db))
        {
            existing.Parameters.AddWithValue("org", f.A);
            beforeCapacityCount = Convert.ToInt64(await existing.ExecuteScalarAsync());
        }
        await using (var extra = new NpgsqlCommand("""
            INSERT INTO shipments (id,organization_id,reference_code,
                origin_port_id,destination_port_id,workflow_status,record_status,
                source_kind,version,created_at_utc,updated_at_utc,changed_by_user_id)
            SELECT gen_random_uuid(),@org,'M13S-' || lpad(n::text,6,'0'),
                @origin,@destination,'Draft','Active','Synthetic',1,
                '2026-10-01T00:00:00Z'::timestamptz,
                '2026-10-01T00:00:00Z'::timestamptz,@actor
            FROM generate_series(85001,94000) AS n
            """, db) { CommandTimeout = 1200 })
        {
            extra.Parameters.AddWithValue("org", f.A);
            extra.Parameters.AddWithValue("origin", origin);
            extra.Parameters.AddWithValue("destination", destination);
            extra.Parameters.AddWithValue("actor", actor);
            Assert.Equal(9000, await extra.ExecuteNonQueryAsync());
        }
        using (var overCapacity = await analyst.GetAsync(
            $"/api/v1/organizations/{f.A}/analytics/overview"))
        {
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, overCapacity.StatusCode);
            var denied = await overCapacity.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("report_capacity_exceeded", denied.GetProperty("code").GetString());
        }
        await using (var existing = new NpgsqlCommand("""
            SELECT COUNT(*) FROM operational_kpi_reports
            WHERE organization_id=@org
            """, db))
        {
            existing.Parameters.AddWithValue("org", f.A);
            Assert.Equal(beforeCapacityCount,
                Convert.ToInt64(await existing.ExecuteScalarAsync()));
        }
        Console.WriteLine("G13_250K_CAP extra_draft=9000 no_partial_report=true");
    }

    // Plan S20: the approved latency and resource budgets must also hold on a
    // realistic skewed tenant under the restricted API role, not only on the
    // Draft and Cancelled reference load. 40,000 shipments with this mix produce
    // about 220,000 report members, inside the 250,000 member capacity.
    [ExclusiveM13PostgresFact]
    public async Task RealisticSkewedTenantMeetsApprovedLatencyAndResourceBudgetsUnderTheRestrictedRole()
    {
        const int shipments = 40_000;
        var roles = await M10RuntimeIdentityTests.ProvisionAsync();
        using var factory = new ReferenceDataHttpTests.ApiFactory(roles.Admin);
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var administrator = f.Admin;
        using var manager = f.Manager;
        using var analystSeed = f.Analyst;
        var seed = await AssignmentHttpTests.Seed(f);
        using var item = await manager.GetAsync(seed.Root + "/" + seed.Shipment);
        var shipment = await item.Content.ReadFromJsonAsync<JsonElement>();
        var origin = shipment.GetProperty("originPortId").GetGuid();
        var destination = shipment.GetProperty("destinationPortId").GetGuid();
        await using var db = new NpgsqlConnection(roles.Admin);
        await db.OpenAsync();
        Guid actor;
        await using (var owner = new NpgsqlCommand(
            "SELECT changed_by_user_id FROM shipments WHERE id=@id", db))
        {
            owner.Parameters.AddWithValue("id", seed.Shipment);
            actor = Assert.IsType<Guid>(await owner.ExecuteScalarAsync());
        }
        var seeded = await RealisticTenantFixture.SeedAsync(db, f.A, origin, destination, actor, shipments);
        Console.WriteLine($"G13_REALISTIC_SEED shipments={shipments} history={seeded.History} " +
            $"evaluations={seeded.Evaluations} open_exceptions={seeded.OpenExceptions} " +
            $"seed_ms={seeded.Milliseconds}");

        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var analyst = runtime.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        analyst.Timeout = TimeSpan.FromMinutes(5);
        await ReferenceDataHttpTests.Login(analyst, f.AnalystEmail);
        await ReferenceDataHttpTests.Select(analyst, f.A);
        long Storage()
        {
            using var read = new NpgsqlCommand("""
                SELECT pg_total_relation_size('operational_kpi_reports') +
                       pg_total_relation_size('operational_kpi_members')
                """, db);
            return Convert.ToInt64(read.ExecuteScalar());
        }
        var initialStorage = Storage();
        var dbResourcesBeforeRealistic = await ReadDatabaseResources(db);
        var cpuStart = Process.GetCurrentProcess().TotalProcessorTime;
        var latencies = new List<double>();
        var members = 0;
        for (var i = 0; i < 30; i++)
        {
            var elapsed = Stopwatch.StartNew();
            using var result = await analyst.GetAsync($"/api/v1/organizations/{f.A}/analytics/overview");
            elapsed.Stop();
            var body = await result.Content.ReadAsStringAsync();
            Console.WriteLine($"G13_REALISTIC iteration={i + 1} status={(int)result.StatusCode} ms={elapsed.Elapsed.TotalMilliseconds:F0}");
            Assert.True(result.StatusCode == HttpStatusCode.OK,
                $"Realistic report unavailable, HTTP {(int)result.StatusCode}: {body[..Math.Min(body.Length, 300)]}");
            using var parsed = JsonDocument.Parse(body);
            Assert.Equal(shipments - shipments / 37 + 2L, Count(parsed.RootElement, "D01")); // archived rows (every 37th) are not active
            members = parsed.RootElement.GetProperty("memberCount").GetInt32();
            Assert.InRange(members, 1, 250_000);
            latencies.Add(elapsed.Elapsed.TotalMilliseconds);
            // Stay below the accepted six-builds-per-minute user limiter.
            if (i < 29 && elapsed.Elapsed < TimeSpan.FromSeconds(11))
                await Task.Delay(TimeSpan.FromSeconds(11) - elapsed.Elapsed);
        }
        latencies.Sort();
        var median = latencies[(int)Math.Ceiling(latencies.Count * 0.50) - 1];
        var p95 = latencies[(int)Math.Ceiling(latencies.Count * 0.95) - 1];
        var finalStorage = Storage();
        await Task.Delay(TimeSpan.FromSeconds(2));
        var dbResourcesAfterRealistic = await ReadDatabaseResources(db);
        Assert.True(dbResourcesAfterRealistic.WalBytes >= dbResourcesBeforeRealistic.WalBytes);
        Assert.True(dbResourcesAfterRealistic.TempBytes >= dbResourcesBeforeRealistic.TempBytes);
        Assert.True(dbResourcesAfterRealistic.TempFiles >= dbResourcesBeforeRealistic.TempFiles);
        Console.WriteLine($"G13_DB_RESOURCES workload=realistic40k " +
            $"wal_bytes={dbResourcesAfterRealistic.WalBytes-dbResourcesBeforeRealistic.WalBytes} " +
            $"temp_bytes={dbResourcesAfterRealistic.TempBytes-dbResourcesBeforeRealistic.TempBytes} " +
            $"temp_files={dbResourcesAfterRealistic.TempFiles-dbResourcesBeforeRealistic.TempFiles}");
        var process = Process.GetCurrentProcess();
        Console.WriteLine($"G13_REALISTIC_BUDGET count={latencies.Count} p50_ms={median:F0} " +
            $"p95_ms={p95:F0} max_ms={latencies[^1]:F0} members={members} " +
            $"initial_report_bytes={initialStorage} final_report_bytes={finalStorage} " +
            $"peak_working_set_bytes={process.PeakWorkingSet64} " +
            $"cpu_ms={(process.TotalProcessorTime - cpuStart).TotalMilliseconds:F0}");
        Assert.True(median <= 15_000, $"Approved G13 p50 exceeded on a realistic tenant: {median:F0}ms");
        Assert.True(p95 <= 20_000, $"Approved G13 p95 exceeded on a realistic tenant: {p95:F0}ms");
        Assert.InRange(finalStorage - initialStorage, 0L, 2L * 1024 * 1024 * 1024);
        Assert.InRange(process.PeakWorkingSet64, 0L, 1L * 1024 * 1024 * 1024);
        await PlannerGuard.AssertDefaultAsync(runtime.Services);
        // The same statement as the application runs it (nested loops off, 15 s limit).
        await CaptureRestrictedPlan(roles.Api, roles.Admin, f.A, actor, "realistic40k",
            null, 25, true);
    }
}
