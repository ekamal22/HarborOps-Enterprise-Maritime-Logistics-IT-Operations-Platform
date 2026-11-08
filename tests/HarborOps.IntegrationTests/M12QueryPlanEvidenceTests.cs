using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HarborOps.Api.Audit;
using HarborOps.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// G12-S20 measured query-plan probe. The test seeds an isolated multi-tenant
// dataset into a disposable database, drives the real search and export HTTP
// endpoints, captures the exact SQL the application emits for shipments, then
// replays each captured statement (same text, same parameters) under
// EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON). Raw plans are written to
// HARBOROPS_PLAN_EVIDENCE_DIR. The test never touches production data and
// refuses to run unless the evidence directory is supplied.
[Collection("M12ServerRoleIsolation")]
public sealed class M12QueryPlanEvidenceTests
{
    private sealed record Captured(string Label, string Sql,
        IReadOnlyList<NpgsqlParameter> Parameters);

    private sealed class CaptureInterceptor : DbCommandInterceptor
    {
        private readonly List<Captured> items = [];
        public string Label { get; set; } = "";
        public IReadOnlyList<Captured> Items { get { lock (items) return items.ToArray(); } }

        private void Record(DbCommand command)
        {
            var sql = command.CommandText;
            if (Label.Length == 0 || !sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                || !sql.Contains("shipments", StringComparison.OrdinalIgnoreCase)
                || sql.Contains("auth_sessions", StringComparison.OrdinalIgnoreCase)
                || sql.Contains("organization_memberships", StringComparison.OrdinalIgnoreCase))
                return;
            var copies = command.Parameters.Cast<NpgsqlParameter>()
                .Select(p => p.Clone()).ToArray();
            lock (items) items.Add(new Captured(Label, sql, copies));
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result)
        { Record(command); return result; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Record(command); return ValueTask.FromResult(result); }
    }

    private sealed class PlanProbeFactAttribute : FactAttribute
    {
        public PlanProbeFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("HARBOROPS_PLAN_EVIDENCE_DIR")) ||
                Environment.GetEnvironmentVariable("HARBOROPS_G12_EXCLUSIVE_POSTGRES") != "1")
                Skip = "Requires HARBOROPS_PLAN_EVIDENCE_DIR and an exclusive disposable PostgreSQL server";
        }
    }

    private const string SeedSql = """
        SET session_replication_role = replica;
        INSERT INTO ports (id, organization_id, reference_code, name, country_code, un_locode,
            status, source_kind, version, created_at_utc, updated_at_utc)
        SELECT md5('prt' || @tag || p)::uuid, @org, 'PP-' || @tag || '-' || lpad(p::text, 3, '0'),
            'Plan port ' || p, 'US', NULL, 'Active', 'Synthetic', 1, now(), now()
        FROM generate_series(1, 40) p;
        INSERT INTO vessels (id, organization_id, reference_code, name, imo_number, mmsi,
            flag_country_code, status, source_kind, version, created_at_utc, updated_at_utc)
        VALUES (md5('ves' || @tag)::uuid, @org, 'PV-' || @tag, 'Plan vessel', NULL, NULL, NULL,
            'Active', 'Synthetic', 1, now(), now());
        INSERT INTO voyages (id, organization_id, reference_code, vessel_id, origin_port_id,
            destination_port_id, planned_departure_at_utc, planned_arrival_at_utc,
            actual_departure_at_utc, actual_arrival_at_utc, voyage_status, record_status,
            source_kind, version, created_at_utc, updated_at_utc)
        SELECT md5('voy' || @tag || v)::uuid, @org, 'PY-' || @tag || '-' || lpad(v::text, 4, '0'),
            md5('ves' || @tag)::uuid, md5('prt' || @tag || (1 + v % 40))::uuid,
            md5('prt' || @tag || (1 + (v + 7) % 40))::uuid, NULL, NULL, NULL, NULL,
            'Planned', 'Active', 'Synthetic', 1, now(), now()
        FROM generate_series(1, 150) v;
        INSERT INTO shipments (id, organization_id, reference_code, external_reference,
            display_name, origin_port_id, destination_port_id, planned_departure_at_utc,
            planned_arrival_at_utc, workflow_status, record_status, source_kind, version,
            created_at_utc, updated_at_utc, archived_at_utc, changed_by_user_id)
        SELECT md5('shp' || @tag || n)::uuid, @org,
            'PS-' || @tag || '-' || lpad(n::text, 7, '0'),
            CASE WHEN n % 2 = 0 THEN 'EXT/' || @tag || '/' || n END,
            (ARRAY['Alpha','Bravo','Cargo','Delta','Echo','Foxtrot'])[1 + n % 6] || ' consignment ' || n
                || CASE WHEN n % 24 = 0 THEN ' zephyr5k' ELSE '' END,
            md5('prt' || @tag || (1 + (n * 13) % 40))::uuid,
            md5('prt' || @tag || (1 + ((n * 13) % 40 + 1 + n % 38) % 40))::uuid,
            timestamptz '2025-01-01 00:00:00+00' + ((n * 7919) % 730) * interval '1 day'
                + ((n * 31) % 24) * interval '1 hour',
            timestamptz '2025-01-01 00:00:00+00' + ((n * 7919) % 730) * interval '1 day'
                + ((n * 31) % 24) * interval '1 hour' + (2 + n % 28) * interval '1 day',
            CASE WHEN n % 100 < 30 THEN 'Draft' WHEN n % 100 < 60 THEN 'Booked'
                 WHEN n % 100 < 80 THEN 'InTransit' WHEN n % 100 < 90 THEN 'Arrived'
                 WHEN n % 100 < 97 THEN 'Completed' ELSE 'Cancelled' END,
            CASE WHEN n % 100 >= 92 THEN 'Archived' ELSE 'Active' END,
            'Synthetic', 1, now(), now(),
            CASE WHEN n % 100 >= 92 THEN now() END, @user
        FROM generate_series(1, @count) n;
        INSERT INTO shipment_voyage_assignments (id, organization_id, shipment_id, voyage_id,
            starts_at_utc, ends_at_utc, version, created_at_utc, updated_at_utc,
            created_by_user_id, closed_by_user_id)
        SELECT md5('asg' || @tag || n)::uuid, @org, md5('shp' || @tag || n)::uuid,
            md5('voy' || @tag || (1 + n % 150))::uuid,
            CASE WHEN n % 5 = 2 THEN now() - interval '60 days' ELSE now() - interval '30 days' END,
            CASE WHEN n % 5 = 2 THEN now() - interval '30 days' END,
            1, now(), now(), @user, CASE WHEN n % 5 = 2 THEN @user END
        FROM generate_series(1, @count) n WHERE n % 5 IN (0, 1, 2);
        """;

    private static async Task Seed(string connection, Guid org, Guid user, string tag, int count)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand(SeedSql, db) { CommandTimeout = 600 };
        command.Parameters.AddWithValue("org", org);
        command.Parameters.AddWithValue("user", user);
        command.Parameters.AddWithValue("tag", tag);
        command.Parameters.AddWithValue("count", count);
        await command.ExecuteNonQueryAsync();
    }

    private static string Safe(string label) =>
        new(label.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_').ToArray());

    // Replays one captured statement under EXPLAIN ANALYZE twice. The first run
    // is recorded as the cold-ish run, the second as the warm run used for gating.
    private static async Task<(string First, string Warm)> Explain(string connection, Captured c,
        Func<NpgsqlParameter, NpgsqlParameter>? rewrite = null)
    {
        var results = new List<string>();
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        for (var run = 0; run < 2; run++)
        {
            await using var tx = await db.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            await using (var guard = new NpgsqlCommand(
                "SET TRANSACTION READ ONLY; SET LOCAL ROLE harborops_api", db, tx))
                await guard.ExecuteNonQueryAsync();
            await using var command = new NpgsqlCommand(
                "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + c.Sql, db, tx)
            { CommandTimeout = 120 };
            foreach (var p in c.Parameters)
                command.Parameters.Add(rewrite is null ? p.Clone() : rewrite(p.Clone()));
            results.Add((string)(await command.ExecuteScalarAsync())!);
            await tx.RollbackAsync();
        }
        return (results[0], results[1]);
    }

    private sealed record PlanSummary(string Label, string Kind, int Rows, double ExecutionMs,
        double PlanningMs, long SharedHit, long SharedRead, string[] Nodes,
        bool SeqScanOnShipments, long SeqRowsRemoved, double WorstEstimateRatio);

    private static PlanSummary Summarize(string label, string kind, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var top = doc.RootElement[0];
        var plan = top.GetProperty("Plan");
        var nodes = new List<string>();
        var seq = false;
        long removed = 0;
        var worst = 1.0;
        void Walk(JsonElement n)
        {
            var type = n.GetProperty("Node Type").GetString()!;
            var relation = n.TryGetProperty("Relation Name", out var r) ? r.GetString() : null;
            var index = n.TryGetProperty("Index Name", out var i) ? i.GetString() : null;
            nodes.Add(type + (relation is null ? "" : " on " + relation) +
                (index is null ? "" : " using " + index));
            if (type == "Seq Scan" && relation == "shipments")
            {
                seq = true;
                if (n.TryGetProperty("Rows Removed by Filter", out var rf)) removed += rf.GetInt64();
            }
            var loops = n.TryGetProperty("Actual Loops", out var l) ? Math.Max(1, l.GetInt64()) : 1;
            if (n.TryGetProperty("Actual Rows", out var a) && n.TryGetProperty("Plan Rows", out var e))
            {
                var actual = Math.Max(1.0, a.GetDouble());
                var est = Math.Max(1.0, e.GetDouble() / 1.0);
                worst = Math.Max(worst, Math.Max(actual / est, est / actual));
            }
            _ = loops;
            if (n.TryGetProperty("Plans", out var kids))
                foreach (var k in kids.EnumerateArray()) Walk(k);
        }
        Walk(plan);
        return new PlanSummary(label, kind,
            plan.TryGetProperty("Actual Rows", out var rows) ? (int)rows.GetDouble() : -1,
            top.GetProperty("Execution Time").GetDouble(), top.GetProperty("Planning Time").GetDouble(),
            plan.TryGetProperty("Shared Hit Blocks", out var hit) ? hit.GetInt64() : 0,
            plan.TryGetProperty("Shared Read Blocks", out var read) ? read.GetInt64() : 0,
            nodes.ToArray(), seq, removed, Math.Round(worst, 1));
    }

    [PlanProbeFact]
    public async Task SearchAndExportPlansAreMeasuredOnRepresentativeMultiTenantData()
    {
        var evidence = Environment.GetEnvironmentVariable("HARBOROPS_PLAN_EVIDENCE_DIR")!;
        Directory.CreateDirectory(evidence);
        var connection = TestDatabase.ConnectionString();
        var capture = new CaptureInterceptor();
        using var factory = new ReferenceDataHttpTests.ApiFactory(connection);
        var big = await ReferenceDataHttpTests.Setup(factory);
        var mid = await ReferenceDataHttpTests.Setup(factory);
        var small = await ReferenceDataHttpTests.Setup(factory);
        foreach (var setupClient in new[] { big.Manager, big.Analyst, big.Admin, mid.Manager,
            mid.Analyst, mid.Admin, small.Manager, small.Analyst, small.Admin })
            setupClient.Dispose();

        async Task<Guid> UserId(string email)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            return await db.AuthUsers.AsNoTracking().Where(u => u.Email == email)
                .Select(u => u.Id).SingleAsync();
        }

        var started = DateTimeOffset.UtcNow;
        await Seed(connection, big.A, await UserId(big.ManagerEmail), "T1", 120_000);
        await Seed(connection, mid.A, await UserId(mid.ManagerEmail), "T2", 40_000);
        await Seed(connection, small.A, await UserId(small.ManagerEmail), "T3", 10_000);
        await using (var analyze = new NpgsqlConnection(connection))
        {
            await analyze.OpenAsync();
            await using var command = new NpgsqlCommand("ANALYZE", analyze) { CommandTimeout = 600 };
            await command.ExecuteNonQueryAsync();
        }
        var seedSeconds = (DateTimeOffset.UtcNow - started).TotalSeconds;

        // Provision the restricted runtime logins (the signed export function requires
        // session_user = harborops_api) and drive every request through that role.
        var apiPassword = "plan-api-" + Guid.NewGuid().ToString("N");
        var workerPassword = "plan-worker-" + Guid.NewGuid().ToString("N");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var config = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> {
                    ["AuditRoles:ApiPassword"] = apiPassword,
                    ["AuditRoles:WorkerPassword"] = workerPassword,
                    ["M10CorrectionProof:Key"] = ReferenceDataHttpTests.CorrectionProofKey,
                    ["M12ExportProof:Key"] = ReferenceDataHttpTests.ExportProofKey
                }).Build();
            await AuditRoleProvisioner.ProvisionAsync(db, config, CancellationToken.None);
        }
        var runtimeConnection = new NpgsqlConnectionStringBuilder(connection)
        { Username = "harborops_api", Password = apiPassword }.ConnectionString;
        using var runtime = new ReferenceDataHttpTests.ApiFactory(runtimeConnection, null,
            services => services.ConfigureDbContext<HarborOpsDbContext>(
                options => options.AddInterceptors(capture)));
        async Task<HttpClient> RuntimeClient(string email, Guid org)
        {
            var client = runtime.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
            await ReferenceDataHttpTests.Login(client, email);
            await ReferenceDataHttpTests.Select(client, org);
            return client;
        }
        using var bigManager = await RuntimeClient(big.ManagerEmail, big.A);
        using var bigAnalyst = await RuntimeClient(big.AnalystEmail, big.A);
        using var smallManager = await RuntimeClient(small.ManagerEmail, small.A);
        using var smallAnalyst = await RuntimeClient(small.AnalystEmail, small.A);

        await using var inventory = new NpgsqlConnection(connection);
        await inventory.OpenAsync();
        async Task<string> Scalar(string sql)
        {
            await using var c = new NpgsqlCommand(sql, inventory);
            return Convert.ToString(await c.ExecuteScalarAsync(), CultureInfo.InvariantCulture)!;
        }
        var shipmentsTotal = await Scalar("SELECT count(*) FROM shipments");
        var indexList = await Scalar("""
            SELECT string_agg(indexrelname || ' ' || pg_size_pretty(pg_relation_size(indexrelid)), '; ' ORDER BY indexrelname)
            FROM pg_stat_user_indexes WHERE relname='shipments'
            """);
        var serverVersion = await Scalar("SHOW server_version");
        var settings = await Scalar("""
            SELECT string_agg(name || '=' || setting, '; ' ORDER BY name) FROM pg_settings
            WHERE name IN ('shared_buffers','work_mem','random_page_cost','effective_cache_size','jit','default_statistics_target')
            """);

        // Pick selective values from the seeded data (not hardcoded).
        Guid origin, destination, voyage;
        await using (var pick = new NpgsqlCommand("""
            SELECT origin_port_id, destination_port_id FROM shipments
            WHERE organization_id=@org AND record_status='Active'
            GROUP BY origin_port_id, destination_port_id ORDER BY count(*) DESC, 1, 2 LIMIT 1
            """, inventory))
        {
            pick.Parameters.AddWithValue("org", big.A);
            await using var reader = await pick.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            origin = reader.GetGuid(0); destination = reader.GetGuid(1);
        }
        await using (var pick = new NpgsqlCommand(
            "SELECT voyage_id FROM shipment_voyage_assignments WHERE organization_id=@org AND ends_at_utc IS NULL GROUP BY voyage_id ORDER BY voyage_id LIMIT 1",
            inventory))
        {
            pick.Parameters.AddWithValue("org", big.A);
            voyage = (Guid)(await pick.ExecuteScalarAsync())!;
        }

        string Search(Guid org) => $"/api/v1/organizations/{org}/shipments/search";
        string Export(Guid org) => $"/api/v1/organizations/{org}/exports/shipments";
        const string win = "plannedDepartureFromUtc=2026-03-01T00%3A00%3A00Z&plannedDepartureBeforeUtc=2026-03-31T00%3A00%3A00Z";
        const string winA = "plannedArrivalFromUtc=2026-03-01T00%3A00%3A00Z&plannedArrivalBeforeUtc=2026-03-31T00%3A00%3A00Z";

        var searches = new (string Label, HttpClient Client, string Url, HttpStatusCode Expect)[]
        {
            ("s01-big-text-unique-reference", bigAnalyst, Search(big.A) + "?q=PS-T1-0060001&limit=25", HttpStatusCode.OK),
            ("s02-big-text-broad-first-page", bigAnalyst, Search(big.A) + "?q=consignment&limit=25", HttpStatusCode.OK),
            ("s03-big-text-5000-matches", bigAnalyst, Search(big.A) + "?q=zephyr5k&limit=25", HttpStatusCode.OK),
            ("s04-big-text-no-match-full-tenant-scan", bigAnalyst, Search(big.A) + "?q=nomatchqzx&limit=25", HttpStatusCode.OK),
            ("s05-big-workflow-status", bigAnalyst, Search(big.A) + "?workflowStatus=InTransit&limit=25", HttpStatusCode.OK),
            ("s06-big-record-status-archived", bigAnalyst, Search(big.A) + "?recordStatus=Archived&limit=25", HttpStatusCode.OK),
            ("s07-big-record-status-all-limit-100", bigAnalyst, Search(big.A) + "?recordStatus=All&limit=100", HttpStatusCode.OK),
            ("s08-big-origin-destination-ports", bigAnalyst, Search(big.A) + $"?originPortId={origin}&destinationPortId={destination}&limit=25", HttpStatusCode.OK),
            ("s09-big-voyage-assignment", bigAnalyst, Search(big.A) + $"?voyageId={voyage}&limit=25", HttpStatusCode.OK),
            ("s10-big-departure-utc-window", bigAnalyst, Search(big.A) + "?" + win + "&limit=25", HttpStatusCode.OK),
            ("s11-big-arrival-utc-window", bigAnalyst, Search(big.A) + "?" + winA + "&limit=25", HttpStatusCode.OK),
            ("s12-big-combined-status-port-window", bigAnalyst, Search(big.A) + $"?workflowStatus=Booked&originPortId={origin}&{win}&limit=25", HttpStatusCode.OK),
            ("s13-small-text-broad", smallAnalyst, Search(small.A) + "?q=consignment&limit=25", HttpStatusCode.OK),
            ("s14-small-workflow-status", smallAnalyst, Search(small.A) + "?workflowStatus=InTransit&limit=25", HttpStatusCode.OK),
            ("s15-small-text-no-match", smallAnalyst, Search(small.A) + "?q=nomatchqzx&limit=25", HttpStatusCode.OK),
        };
        string? firstCursor = null;
        var responseBytes = new Dictionary<string, int>();
        foreach (var (label, client, url, expect) in searches)
        {
            capture.Label = label;
            using var response = await client.GetAsync(url);
            Assert.Equal(expect, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            responseBytes[label] = Encoding.UTF8.GetByteCount(body);
            if (label == "s05-big-workflow-status")
                firstCursor = JsonDocument.Parse(body).RootElement.GetProperty("nextCursor").GetString();
        }
        Assert.False(string.IsNullOrEmpty(firstCursor));
        capture.Label = "s16-big-keyset-page-2-after-cursor";
        using (var page2 = await bigAnalyst.GetAsync(Search(big.A) +
            "?workflowStatus=InTransit&limit=25&cursor=" + Uri.EscapeDataString(firstCursor!)))
            Assert.Equal(HttpStatusCode.OK, page2.StatusCode);

        var exports = new (string Label, HttpClient Client, string Url, HttpStatusCode Expect)[]
        {
            ("e01-big-export-active-text-matches", bigManager, Export(big.A) + "?q=zephyr5k", HttpStatusCode.OK),
            ("e02-big-export-row-limit-rejection-draft", bigManager, Export(big.A) + "?workflowStatus=Draft", (HttpStatusCode)413),
            ("e03-big-export-origin-destination", bigManager, Export(big.A) + $"?originPortId={origin}&destinationPortId={destination}", HttpStatusCode.OK),
            ("e04-big-export-window-and-status", bigManager, Export(big.A) + $"?workflowStatus=Booked&{win}", HttpStatusCode.OK),
            ("e06-big-export-exactly-5000-all-statuses", bigManager, Export(big.A) + "?q=zephyr5k&recordStatus=All", HttpStatusCode.OK),
            ("e05-small-export-cancelled", smallManager, Export(small.A) + "?workflowStatus=Cancelled&recordStatus=All", HttpStatusCode.OK),
        };
        var exportBytes = new Dictionary<string, int>();
        var exportLines = new Dictionary<string, int>();
        foreach (var (label, client, url, expect) in exports)
        {
            capture.Label = label;
            using var response = await client.GetAsync(url);
            Assert.True(response.StatusCode == expect,
                $"{label}: expected {(int)expect} got {(int)response.StatusCode}");
            var payload = await response.Content.ReadAsByteArrayAsync();
            exportBytes[label] = payload.Length;
            exportLines[label] = payload.Count(b => b == 10);
        }
        capture.Label = "";

        // Deep keyset: reuse the captured page-2 statement with the cursor id replaced by
        // the id at offset 60,000 of the tenant ordering.
        Guid deepId;
        await using (var pick = new NpgsqlCommand(
            "SELECT id FROM shipments WHERE organization_id=@org ORDER BY id OFFSET 60000 LIMIT 1", inventory))
        {
            pick.Parameters.AddWithValue("org", big.A);
            deepId = (Guid)(await pick.ExecuteScalarAsync())!;
        }

        var summaries = new List<PlanSummary>();
        var statementLog = new StringBuilder();
        foreach (var group in capture.Items.GroupBy(c => c.Label))
        {
            var statements = group.ToArray();
            for (var index = 0; index < statements.Length; index++)
            {
                var c = statements[index];
                var name = Safe(c.Label) + (statements.Length > 1 ? "-stmt" + index : "");
                var (first, warm) = await Explain(connection, c);
                await File.WriteAllTextAsync(Path.Combine(evidence, name + ".first.explain.json"), first);
                await File.WriteAllTextAsync(Path.Combine(evidence, name + ".warm.explain.json"), warm);
                await File.WriteAllTextAsync(Path.Combine(evidence, name + ".sql.txt"),
                    c.Sql + "\n-- parameters\n" + string.Join("\n",
                        c.Parameters.Select(p => $"{p.ParameterName} ({p.NpgsqlDbType}) = {p.Value}")));
                var kind = c.Label.StartsWith("e", StringComparison.Ordinal) ? "export" : "search";
                var summary = Summarize(name, kind, warm);
                summaries.Add(summary);
                statementLog.AppendLine($"{name}\t{kind}\t{summary.Rows}\t{summary.ExecutionMs:F2}");
            }
        }
        var page2Captured = capture.Items.Single(c => c.Label == "s16-big-keyset-page-2-after-cursor");
        var (deepFirst, deepWarm) = await Explain(connection, page2Captured, p =>
            p.Value is Guid g && g != big.A ? Rewrite(p, deepId) : p);
        await File.WriteAllTextAsync(Path.Combine(evidence, "s17-big-keyset-deep-offset-60000.warm.explain.json"), deepWarm);
        await File.WriteAllTextAsync(Path.Combine(evidence, "s17-big-keyset-deep-offset-60000.first.explain.json"), deepFirst);
        summaries.Add(Summarize("s17-big-keyset-deep-offset-60000", "search", deepWarm));

        var report = new
        {
            capturedAtUtc = DateTimeOffset.UtcNow,
            serverVersion, settings, shipmentsTotal,
            tenants = new[] { new { tag = "T1", rows = 120000 }, new { tag = "T2", rows = 40000 }, new { tag = "T3", rows = 10000 } },
            seedSeconds, shipmentIndexes = indexList,
            note = "Plans are EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) of the exact SQL emitted by the API, replayed read-only. The warm run gates.",
            responseBytes, exportBytes, exportLines,
            summaries
        };
        await File.WriteAllTextAsync(Path.Combine(evidence, "summary.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

        // Criteria declared before the first measured run:
        // C1 every search page statement executes below 1000 ms and every export statement below 3000 ms (warm).
        // C2 the small tenant statements never sequentially scan shipments and touch fewer than 2500 buffers,
        //    so cost does not scale with other tenants' rows.
        // C3 the exactly-5000 export returns 200 with 5001 CSV lines.
        Assert.All(summaries.Where(s => s.Kind == "search"), s =>
            Assert.True(s.ExecutionMs < 1000, $"{s.Label} took {s.ExecutionMs} ms"));
        Assert.All(summaries.Where(s => s.Kind == "export"), s =>
            Assert.True(s.ExecutionMs < 3000, $"{s.Label} took {s.ExecutionMs} ms"));
        Assert.All(summaries.Where(s => s.Label.Contains("small", StringComparison.Ordinal)), s =>
        {
            Assert.False(s.SeqScanOnShipments, $"{s.Label} sequentially scans shipments");
            Assert.True(s.SharedHit + s.SharedRead < 2500, $"{s.Label} touched {s.SharedHit + s.SharedRead} buffers");
        });
        var activeMatches = long.Parse(await Scalar(
            $"SELECT count(*) FROM shipments WHERE organization_id='{big.A}' AND record_status='Active' AND display_name LIKE '%zephyr5k%'"));
        var allMatches = long.Parse(await Scalar(
            $"SELECT count(*) FROM shipments WHERE organization_id='{big.A}' AND display_name LIKE '%zephyr5k%'"));
        Assert.Equal(5000, allMatches);
        Assert.Equal(activeMatches + 1, exportLines["e01-big-export-active-text-matches"]);
        Assert.Equal(5001, exportLines["e06-big-export-exactly-5000-all-statuses"]);
        Assert.Equal(1L, long.Parse(await Scalar(
            $"SELECT count(*) FROM audit_entries WHERE organization_id='{big.A}' AND event_type='shipment.export_prepared' AND (metadata->>'rowCount')::int=5000")));
    }

    private static NpgsqlParameter Rewrite(NpgsqlParameter p, Guid replacement)
    {
        p.Value = replacement;
        return p;
    }
}
