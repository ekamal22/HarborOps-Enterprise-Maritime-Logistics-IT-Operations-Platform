using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HarborOps.Api.Audit;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.Shipments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace HarborOps.IntegrationTests;

// Residual G12 evidence: export boundaries, CSV behaviour end to end, live-page semantics,
// in-flight revocation, cancellation, per-instance limits, deadline budgets and the signed
// audit failure paths. Every case runs through the restricted harborops_api login on an
// exclusive disposable PostgreSQL server.
[Collection("M12ServerRoleIsolation")]
public sealed class M12ResidualBoundaryTests(ITestOutputHelper output)
{
    private void Note(string line)
    {
        output.WriteLine(line);
        var directory = Environment.GetEnvironmentVariable("HARBOROPS_G12_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        File.AppendAllText(Path.Combine(directory, "residual-notes.log"),
            $"{DateTimeOffset.UtcNow:O} {line}{Environment.NewLine}");
    }

    private sealed class OffsetClock(TimeSpan offset) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow + offset;
    }

    internal sealed record Ctx(ReferenceDataHttpTests.ApiFactory Seed,
        ReferenceDataHttpTests.Fixture Fixture, ReferenceDataHttpTests.ApiFactory Runtime,
        string Admin, string ApiConnection, string ApiPassword, string WorkerPassword,
        HttpClient Manager, HttpClient Analyst, Guid Template, Guid ManagerId)
    {
        public string Export => $"/api/v1/organizations/{Fixture.A}/exports/shipments";
        public string Search => $"/api/v1/organizations/{Fixture.A}/shipments/search";
    }

    internal static async Task<Ctx> Prepare(string? exportKey = null)
    {
        if (Environment.GetEnvironmentVariable("HARBOROPS_G12_EXCLUSIVE_POSTGRES") != "1")
            throw new InvalidOperationException(
                "Requires HARBOROPS_G12_EXCLUSIVE_POSTGRES=1 on an exclusive disposable PostgreSQL server");
        var admin = TestDatabase.ConnectionString();
        var seed = new ReferenceDataHttpTests.ApiFactory(admin);
        var fixture = await ReferenceDataHttpTests.Setup(seed);
        var maritime = await AssignmentHttpTests.Seed(fixture);
        var apiPassword = "g12r-api-" + Guid.NewGuid().ToString("N");
        var workerPassword = "g12r-worker-" + Guid.NewGuid().ToString("N");
        await using (var scope = seed.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuditRoles:ApiPassword"] = apiPassword,
                ["AuditRoles:WorkerPassword"] = workerPassword,
                ["M10CorrectionProof:Key"] = ReferenceDataHttpTests.CorrectionProofKey,
                ["M12ExportProof:Key"] = exportKey ?? ReferenceDataHttpTests.ExportProofKey
            }).Build();
            await AuditRoleProvisioner.ProvisionAsync(db, config, CancellationToken.None);
        }
        var apiConnection = new NpgsqlConnectionStringBuilder(admin)
        { Username = "harborops_api", Password = apiPassword }.ConnectionString;
        var runtime = new ReferenceDataHttpTests.ApiFactory(apiConnection);
        var manager = await Client(runtime, fixture.ManagerEmail, fixture.A);
        var analyst = await Client(runtime, fixture.AnalystEmail, fixture.A);
        var managerId = await Scalar<Guid>(admin, "SELECT id FROM users WHERE email=@e", ("e", fixture.ManagerEmail));
        return new Ctx(seed, fixture, runtime, admin, apiConnection, apiPassword, workerPassword,
            manager, analyst, maritime.Shipment, managerId);
    }

    internal static async Task<HttpClient> Client(WebApplicationFactory<Program> factory, string email, Guid org)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(client, email);
        await ReferenceDataHttpTests.Select(client, org);
        return client;
    }

    internal static async Task<T> Scalar<T>(string connection, string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand(sql, db);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default! : (T)Convert.ChangeType(result, typeof(T));
    }

    internal static async Task Exec(string connection, string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand(sql, db) { CommandTimeout = 300 };
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    internal static Task<long> AuditCount(Ctx c) => Scalar<long>(c.Admin,
        "SELECT count(*) FROM audit_entries WHERE organization_id=@o AND event_type='shipment.export_prepared'",
        ("o", c.Fixture.A));

    private static async Task WaitForApiLockWaiters(string connection, int count)
    {
        await using var monitor = new NpgsqlConnection(connection);
        await monitor.OpenAsync();
        for (var attempt = 0; attempt < 600; attempt++)
        {
            await using var query = new NpgsqlCommand("""
                SELECT count(*) FROM pg_stat_activity
                WHERE datname = current_database() AND pid <> pg_backend_pid()
                  AND usename = 'harborops_api' AND wait_event_type = 'Lock'
                """, monitor);
            if (Convert.ToInt32(await query.ExecuteScalarAsync()) >= count) return;
            await Task.Delay(25);
        }
        Assert.Fail($"Expected {count} runtime request(s) blocked on a database lock");
    }

    internal static async Task InsertShipments(Ctx c, Guid org, string tag, int count,
        string displayExpression, string externalExpression, string codeExpression)
    {
        await using var db = new NpgsqlConnection(c.Admin);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand($"""
            INSERT INTO shipments (id, organization_id, reference_code, external_reference, display_name,
                origin_port_id, destination_port_id, planned_departure_at_utc, planned_arrival_at_utc,
                workflow_status, record_status, source_kind, version, created_at_utc, updated_at_utc,
                archived_at_utc, changed_by_user_id)
            SELECT gen_random_uuid(), @org, {codeExpression}, {externalExpression}, {displayExpression},
                t.origin_port_id, t.destination_port_id, NULL, NULL, 'Draft', 'Active', 'Synthetic',
                1, now(), now(), NULL, t.changed_by_user_id
            FROM shipments t CROSS JOIN generate_series(1, @count) n WHERE t.id = @template
            """, db) { CommandTimeout = 300 };
        command.Parameters.AddWithValue("org", org);
        command.Parameters.AddWithValue("tag", tag);
        command.Parameters.AddWithValue("count", count);
        command.Parameters.AddWithValue("template", c.Template);
        command.Parameters.AddWithValue("emoji", string.Concat(Enumerable.Repeat("\U0001F600", 160)));
        await command.ExecuteNonQueryAsync();
    }

    internal static string Tag() => "W" + Guid.NewGuid().ToString("N")[..9].ToUpperInvariant();

    private static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else field.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == ',') { fields.Add(field.ToString()); field.Clear(); }
            else if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            { fields.Add(field.ToString()); field.Clear(); rows.Add(fields.ToArray()); fields.Clear(); i++; }
            else field.Append(ch);
        }
        Assert.False(quoted, "unterminated quoted field");
        Assert.Empty(fields);
        Assert.Equal(0, field.Length);
        return rows;
    }

    // ------------------------------------------------------------------ S17 boundaries

    [Fact]
    public async Task ExportAcceptsExactlyFiveThousandWorstCaseRowsAndSqlEnforcesByteBoundary()
    {
        var c = await Prepare();
        using var seed = c.Seed; using var runtime = c.Runtime;
        using var a = c.Fixture.Admin; using var b = c.Fixture.Manager; using var d = c.Fixture.Analyst;
        using var manager = c.Manager; using var analyst = c.Analyst;
        var tag = Tag();
        // Worst case per row: 160 four-byte characters, 80-character external reference, 32-character code.
        await InsertShipments(c, c.Fixture.A, tag, 5000, displayExpression: "@emoji",
            externalExpression: "rpad(@tag || lpad(n::text, 5, '0'), 80, 'Z')",
            codeExpression: "rpad(@tag || lpad(n::text, 5, '0'), 32, 'X')");
        var auditBefore = await AuditCount(c);
        var stopwatch = Stopwatch.StartNew();
        using var response = await manager.GetAsync(c.Export + "?q=" + tag);
        stopwatch.Stop();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "no BOM");
        var csv = Encoding.UTF8.GetString(bytes);
        var rows = ParseCsv(csv);
        Assert.Equal(5001, rows.Count);
        Assert.All(rows, r => Assert.Equal(12, r.Length));
        Assert.True(bytes.Length < 10 * 1024 * 1024);
        var perRow = bytes.Length / 5001.0;
        Assert.Equal(auditBefore + 1, await AuditCount(c));
        var recorded = await Scalar<long>(c.Admin, """
            SELECT (metadata->>'byteCount')::bigint FROM audit_entries
            WHERE organization_id=@o AND event_type='shipment.export_prepared'
              AND (metadata->>'rowCount')::int=5000
            """, ("o", c.Fixture.A));
        Assert.Equal(bytes.LongLength, recorded);
        Note($"S17 exact 5000 worst-case rows: status=200 bytes={bytes.Length} bytesPerRow={perRow:F0} " +
             $"projectedAt5000={perRow * 5000:F0} limit={10 * 1024 * 1024} seconds={stopwatch.Elapsed.TotalSeconds:F2}");
        // The byte limit cannot be reached over HTTP: the worst-case 5,000-row response is far below it.
        Assert.True(perRow * 5000 < 0.6 * 10 * 1024 * 1024);

        // 5,001 rows are rejected and no further audit is prepared.
        await InsertShipments(c, c.Fixture.A, tag + "Q", 1, "NULL", "NULL",
            "rpad(@tag || 'Q' || lpad(n::text, 5, '0'), 20, 'Y')");
        using var over = await manager.GetAsync(c.Export + "?q=" + tag);
        Assert.Equal((HttpStatusCode)413, over.StatusCode);
        Assert.Equal("export_row_limit",
            (await over.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(auditBefore + 1, await AuditCount(c));

        // SQL boundary: 10 MiB and 5,000 rows are accepted, one more byte or row is refused.
        var sessionId = await Scalar<Guid>(c.Admin, """
            SELECT id FROM auth_sessions WHERE user_id=@u AND selected_organization_id=@o
              AND revoked_at_utc IS NULL ORDER BY issued_at_utc DESC LIMIT 1
            """, ("u", c.ManagerId), ("o", c.Fixture.A));
        async Task<string?> Call(int rows, long byteCount)
        {
            var correlation = Guid.NewGuid().ToString("N");
            var digest = new string('A', 64);
            var proof = M12ExportProofSigner.Sign(ReferenceDataHttpTests.ExportProofKey, c.Fixture.A,
                c.ManagerId, sessionId, rows, byteCount, digest, correlation, TimeProvider.System)!;
            await using var db = new NpgsqlConnection(c.ApiConnection);
            await db.OpenAsync();
            await using var command = new NpgsqlCommand("""
                SELECT public.m12_record_shipment_export(@o,@u,@s,@r,@b,@d,@c,@e,@g)
                """, db);
            command.Parameters.AddWithValue("o", c.Fixture.A);
            command.Parameters.AddWithValue("u", c.ManagerId);
            command.Parameters.AddWithValue("s", sessionId);
            command.Parameters.AddWithValue("r", rows);
            command.Parameters.AddWithValue("b", byteCount);
            command.Parameters.AddWithValue("d", digest);
            command.Parameters.AddWithValue("c", correlation);
            command.Parameters.AddWithValue("e", proof.ExpiresEpoch);
            command.Parameters.AddWithValue("g", proof.Signature);
            try { await command.ExecuteNonQueryAsync(); return null; }
            catch (PostgresException error) { return error.SqlState; }
        }
        Assert.Null(await Call(5000, 10_485_760));
        Assert.Equal("22023", await Call(5000, 10_485_761));
        Assert.Equal("22023", await Call(5001, 100));
        Assert.Equal("22023", await Call(1, 0));
        Assert.Null(await Call(0, 1));
        Note("S17 SQL boundary: bytes 10485760 accepted, 10485761 refused (22023), rows 5001 refused, bytes 0 refused");
    }

    // ------------------------------------------------------------------ S15 / S16 / S14

    [Fact]
    public async Task CsvPreservesUnicodeUsesCrlfDefangsFormulasAndMatchesSearchResults()
    {
        var c = await Prepare();
        using var seed = c.Seed; using var runtime = c.Runtime;
        using var a = c.Fixture.Admin; using var b = c.Fixture.Manager; using var d = c.Fixture.Analyst;
        using var manager = c.Manager; using var analyst = c.Analyst;
        var tag = Tag();
        var names = new Dictionary<int, string>
        {
            [1] = "=1+1", [2] = "+SUM(A1:A2)", [3] = "-5+5", [4] = "@SUM(1)",
            [5] = "  =padded", [6] = "​=HYPERLINK(\"http://x\")",
            [7] = "Ünïcödé 東京 العربية \U0001F600",
            [8] = "Quote \"inside\", comma; semicolon", [9] = "plain text", [10] = "x=safe"
        };
        foreach (var (n, name) in names)
        {
            await using var db = new NpgsqlConnection(c.Admin);
            await db.OpenAsync();
            await using var command = new NpgsqlCommand("""
                INSERT INTO shipments (id, organization_id, reference_code, external_reference, display_name,
                    origin_port_id, destination_port_id, workflow_status, record_status, source_kind, version,
                    created_at_utc, updated_at_utc, changed_by_user_id)
                SELECT gen_random_uuid(), @org, @code, NULL, @name, t.origin_port_id, t.destination_port_id,
                    'Draft', 'Active', 'Synthetic', 1, now(), now(), t.changed_by_user_id
                FROM shipments t WHERE t.id=@template
                """, db);
            command.Parameters.AddWithValue("org", c.Fixture.A);
            command.Parameters.AddWithValue("code", $"{tag}-{n:D2}");
            command.Parameters.AddWithValue("name", name);
            command.Parameters.AddWithValue("template", c.Template);
            await command.ExecuteNonQueryAsync();
        }
        // Parity set: 250 more rows, none of which carry a risky display name.
        await InsertShipments(c, c.Fixture.A, tag, 250, "'Parity row ' || n", "NULL",
            "@tag || '-P' || lpad(n::text, 4, '0')");

        using var response = await manager.GetAsync(c.Export + "?q=" + tag);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.Contains("attachment", response.Content.Headers.ContentDisposition?.ToString() ?? "");
        Assert.Matches(@"^shipments-\d{8}T\d{6}Z\.csv$",
            response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "");
        Assert.Equal("nosniff", string.Join(",", response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Contains("no-store", string.Join(",", response.Headers.GetValues("Cache-Control")));
        var raw = await response.Content.ReadAsByteArrayAsync();
        var csv = Encoding.UTF8.GetString(raw);
        // Every record terminator is CRLF and no bare LF or CR exists anywhere.
        Assert.DoesNotContain("\n", csv.Replace("\r\n", ""));
        Assert.DoesNotContain("\r", csv.Replace("\r\n", ""));
        var rows = ParseCsv(csv);
        var headers = new[] { "Shipment ID", "Reference Code", "External Reference", "Display Name",
            "Workflow Status", "Record Status", "Origin Port ID", "Destination Port ID",
            "Planned Departure UTC", "Planned Arrival UTC", "Created UTC", "Updated UTC" };
        Assert.Equal(headers, rows[0]);
        Assert.All(rows, r => Assert.Equal(12, r.Length));
        Assert.Equal(1 + 10 + 250, rows.Count);
        string DisplayOf(int n) => rows.Single(r => r[1] == $"{tag}-{n:D2}")[3];
        Assert.Equal("'=1+1", DisplayOf(1));
        Assert.Equal("'+SUM(A1:A2)", DisplayOf(2));
        Assert.Equal("'-5+5", DisplayOf(3));
        Assert.Equal("'@SUM(1)", DisplayOf(4));
        Assert.Equal("'  =padded", DisplayOf(5));
        Assert.Equal("'​=HYPERLINK(\"http://x\")", DisplayOf(6));
        Assert.Equal(names[7], DisplayOf(7));
        Assert.Equal(names[8], DisplayOf(8));
        Assert.Equal(names[9], DisplayOf(9));
        Assert.Equal(names[10], DisplayOf(10));
        // Only the twelve fixed columns exist and nothing private is serialized.
        foreach (var forbidden in new[] { "password", "session", "email", "csrf", c.Fixture.A.ToString("D") })
            Assert.DoesNotContain(forbidden, csv, StringComparison.OrdinalIgnoreCase);

        // Search-to-export parity under the same filter.
        var searched = new HashSet<Guid>();
        string? cursor = null;
        do
        {
            using var page = await analyst.GetAsync(c.Search + "?limit=100&q=" + tag +
                (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)));
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            var json = await page.Content.ReadFromJsonAsync<JsonElement>();
            foreach (var item in json.GetProperty("shipments").EnumerateArray())
                Assert.True(searched.Add(item.GetProperty("id").GetGuid()), "duplicate across pages");
            cursor = json.GetProperty("nextCursor").GetString();
        } while (cursor is not null);
        var exported = rows.Skip(1).Select(r => Guid.Parse(r[0])).ToHashSet();
        Assert.Equal(searched, exported);
        Note($"S15/S16/S14 csv bytes={raw.Length} rows={rows.Count - 1} searchRows={searched.Count} parity=equal");
    }

    // ------------------------------------------------------------------ S09

    [Fact]
    public async Task LivePagesReflectInsertUpdateAndArchiveBetweenPagesWithoutLeakingTenants()
    {
        var c = await Prepare();
        using var seed = c.Seed; using var runtime = c.Runtime;
        using var a = c.Fixture.Admin; using var b = c.Fixture.Manager; using var d = c.Fixture.Analyst;
        using var manager = c.Manager; using var analyst = c.Analyst;
        var tag = Tag();
        await InsertShipments(c, c.Fixture.A, tag, 30, "'Live ' || n", "NULL",
            "@tag || '-' || lpad(n::text, 3, '0')");

        using var firstResponse = await analyst.GetAsync(c.Search + "?limit=10&q=" + tag);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        var page1 = first.GetProperty("shipments").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        var cursor = first.GetProperty("nextCursor").GetString()!;
        Assert.Equal(10, page1.Count);

        var lowId = Guid.Parse("00000000-0000-4000-8000-000000000001");
        var highId = Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff");
        foreach (var (id, suffix) in new[] { (lowId, "-LOW"), (highId, "-HIGH") })
            await Exec(c.Admin, """
                INSERT INTO shipments (id, organization_id, reference_code, external_reference, display_name,
                    origin_port_id, destination_port_id, workflow_status, record_status, source_kind, version,
                    created_at_utc, updated_at_utc, changed_by_user_id)
                SELECT @id, @org, @code, NULL, 'late insert', t.origin_port_id, t.destination_port_id,
                    'Draft', 'Active', 'Synthetic', 1, now(), now(), t.changed_by_user_id
                FROM shipments t WHERE t.id=@template
                """, ("id", id), ("org", c.Fixture.A), ("code", tag + suffix), ("template", c.Template));
        // Foreign-tenant row with the same tag must never appear.
        var otherOrgShipments = await Scalar<long>(c.Admin,
            "SELECT count(*) FROM shipments WHERE organization_id=@o", ("o", c.Fixture.B));
        Assert.Equal(0L, otherOrgShipments);

        // Archive one not-yet-paged row and rename another while paging is in progress.
        var remaining = await Scalar<Guid>(c.Admin, """
            SELECT id FROM shipments WHERE organization_id=@o AND reference_code LIKE @p
              AND id > @after ORDER BY id OFFSET 2 LIMIT 1
            """, ("o", c.Fixture.A), ("p", tag + "-0%"), ("after", page1[^1]));
        await Exec(c.Admin, """
            SET session_replication_role = replica;
            UPDATE shipments SET record_status='Archived', archived_at_utc=now(), version=version+1,
                updated_at_utc=now() WHERE id=@id
            """, ("id", remaining));
        var renamed = await Scalar<Guid>(c.Admin, """
            SELECT id FROM shipments WHERE organization_id=@o AND reference_code LIKE @p
              AND id > @after AND id <> @archived ORDER BY id LIMIT 1
            """, ("o", c.Fixture.A), ("p", tag + "-0%"), ("after", page1[^1]), ("archived", remaining));
        await Exec(c.Admin, "UPDATE shipments SET display_name='renamed between pages', version=version+1, updated_at_utc=now() WHERE id=@id",
            ("id", renamed));

        var rest = new List<Guid>();
        string? next = cursor;
        var names = new Dictionary<Guid, string?>();
        while (next is not null)
        {
            using var page = await analyst.GetAsync(c.Search + "?limit=10&q=" + tag + "&cursor=" + Uri.EscapeDataString(next));
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            var json = await page.Content.ReadFromJsonAsync<JsonElement>();
            foreach (var item in json.GetProperty("shipments").EnumerateArray())
            {
                rest.Add(item.GetProperty("id").GetGuid());
                names[item.GetProperty("id").GetGuid()] = item.GetProperty("displayName").GetString();
            }
            next = json.GetProperty("nextCursor").GetString();
        }
        var all = page1.Concat(rest).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());               // no row appears twice
        Assert.DoesNotContain(lowId, all);                              // inserted behind the cursor is not revisited
        Assert.Contains(highId, rest);                                  // inserted ahead of the cursor is visible
        Assert.DoesNotContain(remaining, rest);                         // archived ahead of the cursor is excluded
        Assert.Equal("renamed between pages", names[renamed]);          // live value, not a frozen snapshot
        Assert.Equal(30 - 1 + 1, rest.Count + page1.Count - 0 - 0);     // 30 seeded - archived + high insert
        // A cursor minted in organization A is rejected for organization B.
        using var crossTenant = await analyst.GetAsync(
            $"/api/v1/organizations/{c.Fixture.B}/shipments/search?limit=10&q={tag}&cursor={Uri.EscapeDataString(cursor)}");
        Assert.Contains(crossTenant.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.Conflict });
        Note($"S09 live pages: page1={page1.Count} rest={rest.Count} insertedBehindCursorMissed=true insertedAheadSeen=true archivedExcluded=true renamedLive=true");
    }

    // ------------------------------------------------------------------ S13

    [Theory]
    [InlineData("search", "none")]
    [InlineData("search", "session_revoked")]
    [InlineData("search", "member_removed")]
    [InlineData("search", "role_downgraded")]
    [InlineData("search", "organization_suspended")]
    [InlineData("search", "account_disabled")]
    [InlineData("search", "security_version")]
    [InlineData("search", "organization_switched")]
    [InlineData("export", "none")]
    [InlineData("export", "session_revoked")]
    [InlineData("export", "member_removed")]
    [InlineData("export", "role_downgraded")]
    [InlineData("export", "organization_suspended")]
    [InlineData("export", "account_disabled")]
    [InlineData("export", "security_version")]
    [InlineData("export", "organization_switched")]
    public async Task InFlightRequestsRevalidateAuthorityAfterTheOrganizationLockIsReleased(
        string target, string change)
    {
        var c = await Prepare();
        using var seed = c.Seed; using var runtime = c.Runtime;
        using var a = c.Fixture.Admin; using var b = c.Fixture.Manager; using var d = c.Fixture.Analyst;
        using var manager = c.Manager; using var analyst = c.Analyst;
        var tag = Tag();
        await InsertShipments(c, c.Fixture.A, tag, 5, "'In flight ' || n", "NULL", "@tag || '-' || lpad(n::text, 3, '0')");
        var auditBefore = await AuditCount(c);
        var rowsBefore = await Scalar<long>(c.Admin, "SELECT count(*) FROM shipments WHERE organization_id=@o", ("o", c.Fixture.A));

        await using var blocker = new NpgsqlConnection(c.Admin);
        await blocker.OpenAsync();
        await using var tx = await blocker.BeginTransactionAsync();
        await using (var acquire = new NpgsqlCommand("SELECT id FROM organizations WHERE id=@id FOR UPDATE", blocker, tx))
        {
            acquire.Parameters.AddWithValue("id", c.Fixture.A);
            await acquire.ExecuteScalarAsync();
        }
        var committed = false;
        try
        {
            var url = (target == "search" ? c.Search : c.Export) + "?q=" + tag;
            var request = manager.GetAsync(url);
            await WaitForApiLockWaiters(c.Admin, 1);
            if (change == "organization_suspended")
            {
                await using var suspend = new NpgsqlCommand("UPDATE organizations SET status='Suspended' WHERE id=@id", blocker, tx);
                suspend.Parameters.AddWithValue("id", c.Fixture.A);
                Assert.Equal(1, await suspend.ExecuteNonQueryAsync());
            }
            else if (change != "none")
            {
                var sql = change switch
                {
                    "session_revoked" => "UPDATE auth_sessions SET revoked_at_utc=now() WHERE user_id=@user AND revoked_at_utc IS NULL",
                    "member_removed" => "UPDATE organization_memberships SET status='Removed', version=version+1, updated_at_utc=now() WHERE organization_id=@org AND user_id=@user",
                    "role_downgraded" => "UPDATE organization_memberships SET role='Auditor', version=version+1, updated_at_utc=now() WHERE organization_id=@org AND user_id=@user",
                    "account_disabled" => "UPDATE users SET status='Disabled', disabled_at_utc=now() WHERE id=@user",
                    "organization_switched" => "UPDATE auth_sessions SET selected_organization_id=@other WHERE user_id=@user AND revoked_at_utc IS NULL",
                    _ => "UPDATE users SET security_version=security_version+1 WHERE id=@user"
                };
                await using var other = new NpgsqlConnection(c.Admin);
                await other.OpenAsync();
                await using var revoke = new NpgsqlCommand(sql, other);
                revoke.Parameters.AddWithValue("org", c.Fixture.A);
                revoke.Parameters.AddWithValue("other", c.Fixture.B);
                revoke.Parameters.AddWithValue("user", c.ManagerId);
                Assert.True(await revoke.ExecuteNonQueryAsync() >= 1);
            }
            await tx.CommitAsync();
            committed = true;
            using var response = await request;
            var expected = change switch
            {
                "none" => HttpStatusCode.OK,
                "role_downgraded" => target == "search" ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
                "organization_switched" => HttpStatusCode.NotFound,
                _ => HttpStatusCode.Forbidden
            };
            Assert.True(response.StatusCode == expected,
                $"{target}/{change}: expected {expected} got {response.StatusCode}");
            if (expected != HttpStatusCode.OK)
                Assert.NotEqual("text/csv", response.Content.Headers.ContentType?.MediaType);
            var auditAfter = await AuditCount(c);
            Assert.Equal(target == "export" && expected == HttpStatusCode.OK ? auditBefore + 1 : auditBefore, auditAfter);
            Assert.Equal(rowsBefore, await Scalar<long>(c.Admin, "SELECT count(*) FROM shipments WHERE organization_id=@o", ("o", c.Fixture.A)));
            Note($"S13 {target}/{change}: status={(int)response.StatusCode} auditDelta={auditAfter - auditBefore}");
        }
        finally
        {
            if (!committed) await tx.RollbackAsync();
        }
    }

    // ------------------------------------------------------------------ S19 cancellation, instances

    [Fact]
    public async Task ClientAbortReleasesTheLeaseWithoutAuditAndLateDiscardKeepsPreparationAudit()
    {
        var c = await Prepare();
        using var seed = c.Seed; using var runtime = c.Runtime;
        using var a = c.Fixture.Admin; using var b = c.Fixture.Manager; using var d = c.Fixture.Analyst;
        using var manager = c.Manager; using var analyst = c.Analyst;
        var tag = Tag();
        await InsertShipments(c, c.Fixture.A, tag, 20, "'Abort ' || n", "NULL", "@tag || '-' || lpad(n::text, 3, '0')");
        var auditBefore = await AuditCount(c);

        await using (var blocker = new NpgsqlConnection(c.Admin))
        {
            await blocker.OpenAsync();
            await using var tx = await blocker.BeginTransactionAsync();
            await using (var acquire = new NpgsqlCommand("SELECT id FROM organizations WHERE id=@id FOR UPDATE", blocker, tx))
            {
                acquire.Parameters.AddWithValue("id", c.Fixture.A);
                await acquire.ExecuteScalarAsync();
            }
            using var cts = new CancellationTokenSource();
            var aborted = manager.GetAsync(c.Export + "?q=" + tag, cts.Token);
            await WaitForApiLockWaiters(c.Admin, 1);
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => aborted);
            await Task.Delay(500);
            await tx.RollbackAsync();
        }
        await Task.Delay(1500);
        Assert.Equal(auditBefore, await AuditCount(c));
        // The per-principal lease is free again: a new export must not be refused with 429.
        using (var next = await manager.GetAsync(c.Export + "?q=" + tag))
            Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        Assert.Equal(auditBefore + 1, await AuditCount(c));

        // Documented M12-P12 semantics: the audit proves preparation, not receipt. A client that
        // reads headers and discards the body leaves exactly one prepared audit and no second one.
        using (var partial = await manager.GetAsync(c.Export + "?q=" + tag, HttpCompletionOption.ResponseHeadersRead))
        {
            Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
        }
        Assert.Equal(auditBefore + 2, await AuditCount(c));
        Note("S19 abort: audit unchanged by aborted request, lease released, discarded body keeps single preparation audit");
    }

    [Fact]
    public async Task ExportLeaseAndRateLimitAreEnforcedPerApiInstance()
    {
        var c = await Prepare();
        using var seed = c.Seed; using var runtime = c.Runtime;
        using var a = c.Fixture.Admin; using var b = c.Fixture.Manager; using var d = c.Fixture.Analyst;
        using var manager = c.Manager; using var analyst = c.Analyst;
        var tag = Tag();
        await InsertShipments(c, c.Fixture.A, tag, 10, "'Inst ' || n", "NULL", "@tag || '-' || lpad(n::text, 3, '0')");
        using var secondInstance = new ReferenceDataHttpTests.ApiFactory(c.ApiConnection);
        using var manager2 = await Client(secondInstance, c.Fixture.ManagerEmail, c.Fixture.A);
        var auditBefore = await AuditCount(c);

        await using (var blocker = new NpgsqlConnection(c.Admin))
        {
            await blocker.OpenAsync();
            await using var tx = await blocker.BeginTransactionAsync();
            await using (var acquire = new NpgsqlCommand("SELECT id FROM organizations WHERE id=@id FOR UPDATE", blocker, tx))
            {
                acquire.Parameters.AddWithValue("id", c.Fixture.A);
                await acquire.ExecuteScalarAsync();
            }
            var onFirst = manager.GetAsync(c.Export + "?q=" + tag);
            await WaitForApiLockWaiters(c.Admin, 1);
            // Same instance, same principal: the concurrent export is refused immediately.
            using var concurrent = await manager.GetAsync(c.Export + "?q=" + tag);
            Assert.Equal(HttpStatusCode.TooManyRequests, concurrent.StatusCode);
            Assert.Equal("export_in_progress",
                (await concurrent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            // Different instance, same principal: allowed by the documented per-instance lease.
            var onSecond = manager2.GetAsync(c.Export + "?q=" + tag);
            await WaitForApiLockWaiters(c.Admin, 2);
            await tx.CommitAsync();
            using var r1 = await onFirst; using var r2 = await onSecond;
            Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
            Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        }
        Assert.Equal(auditBefore + 2, await AuditCount(c));

        // Rate limit: ten attempts per minute per principal on each instance independently.
        var firstStatuses = new List<HttpStatusCode>();
        for (var i = 0; i < 12; i++)
        {
            using var r = await manager.GetAsync(c.Export + "?q=" + tag);
            firstStatuses.Add(r.StatusCode);
        }
        var okFirst = firstStatuses.Count(s => s == HttpStatusCode.OK);
        Assert.Contains(HttpStatusCode.TooManyRequests, firstStatuses);
        using var stillAllowed = await manager2.GetAsync(c.Export + "?q=" + tag);
        Assert.Equal(HttpStatusCode.OK, stillAllowed.StatusCode);
        Note($"S19 per-instance: first instance successes after contention={okFirst}, 429 observed, second instance independent. " +
             "No cross-instance aggregate limit exists; documented M12-P10 per-instance policy.");
    }

    // ------------------------------------------------------------------ S17 / S19 timing

    [Fact]
    public async Task StatementTimeoutAndTwentyFiveSecondBudgetBoundSearchAndExportWithoutOutput()
    {
        var c = await Prepare();
        using var seed = c.Seed; using var runtime = c.Runtime;
        using var a = c.Fixture.Admin; using var b = c.Fixture.Manager; using var d = c.Fixture.Analyst;
        using var manager = c.Manager; using var analyst = c.Analyst;
        var auditBefore = await AuditCount(c);

        // 1. Real 15-second PostgreSQL statement timeout on the search read (table lock).
        await using (var blocker = new NpgsqlConnection(c.Admin))
        {
            await blocker.OpenAsync();
            await using var tx = await blocker.BeginTransactionAsync();
            await using (var lockCommand = new NpgsqlCommand("LOCK TABLE shipments IN ACCESS EXCLUSIVE MODE", blocker, tx))
                await lockCommand.ExecuteNonQueryAsync();
            var watch = Stopwatch.StartNew();
            using var response = await analyst.GetAsync(c.Search + "?q=timeout-probe");
            watch.Stop();
            await tx.RollbackAsync();
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.InRange(watch.Elapsed.TotalSeconds, 14.0, 20.0);
            Note($"S17 search statement timeout: 503 after {watch.Elapsed.TotalSeconds:F2}s");
        }

        // 2. The internal 25-second budget: wait 12s on the organization lock (inside the 15s
        // statement timeout), then block the membership lock check, so the next statement would
        // only time out at about 27s. The 25s budget must cancel first.
        foreach (var target in new[] { "search", "export" })
        {
            await using var orgLock = new NpgsqlConnection(c.Admin);
            await using var membershipLock = new NpgsqlConnection(c.Admin);
            await orgLock.OpenAsync(); await membershipLock.OpenAsync();
            await using var orgTx = await orgLock.BeginTransactionAsync();
            await using var memberTx = await membershipLock.BeginTransactionAsync();
            await using (var one = new NpgsqlCommand("SELECT id FROM organizations WHERE id=@id FOR UPDATE", orgLock, orgTx))
            {
                one.Parameters.AddWithValue("id", c.Fixture.A);
                await one.ExecuteScalarAsync();
            }
            await using (var two = new NpgsqlCommand(
                "SELECT id FROM organization_memberships WHERE organization_id=@o AND user_id=@u FOR UPDATE", membershipLock, memberTx))
            {
                two.Parameters.AddWithValue("o", c.Fixture.A);
                two.Parameters.AddWithValue("u", c.ManagerId);
                await two.ExecuteScalarAsync();
            }
            var watch = Stopwatch.StartNew();
            var request = manager.GetAsync((target == "search" ? c.Search : c.Export) + "?q=budget-probe");
            await Task.Delay(TimeSpan.FromSeconds(12));
            await orgTx.RollbackAsync();
            using var response = await request;
            watch.Stop();
            await memberTx.RollbackAsync();
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("request budget", body, StringComparison.Ordinal);
            Assert.NotEqual("text/csv", response.Content.Headers.ContentType?.MediaType);
            Assert.InRange(watch.Elapsed.TotalSeconds, 24.0, 27.0);
            Note($"S17/S19 {target} 25s internal budget: 503 after {watch.Elapsed.TotalSeconds:F2}s, body mentions request budget");
        }
        Assert.Equal(auditBefore, await AuditCount(c));

        // 3. The proxy envelope: the API budget plus measured margin stays below nginx's 30s read timeout.
        string conf = "";
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "web", "nginx.conf")))
            directory = directory.Parent;
        if (directory is not null) conf = await File.ReadAllTextAsync(Path.Combine(directory.FullName, "web", "nginx.conf"));
        Assert.Contains("proxy_read_timeout 30s;", conf);
        Note("S17/S19 proxy: nginx proxy_read_timeout 30s present; measured API failure latency <= 27s. Real proxy path NOT EXECUTED here.");
    }

    // ------------------------------------------------------------------ S18 remainder

    [Fact]
    public async Task ExportAuthorityHandlesExpiryRotationOutageAuditRejectionAndRefusesDowngrade()
    {
        var c = await Prepare();
        using var seed = c.Seed; using var runtime = c.Runtime;
        using var a = c.Fixture.Admin; using var b = c.Fixture.Manager; using var d = c.Fixture.Analyst;
        using var manager = c.Manager; using var analyst = c.Analyst;
        var tag = Tag();
        await InsertShipments(c, c.Fixture.A, tag, 5, "'Auth ' || n", "NULL", "@tag || '-' || lpad(n::text, 3, '0')");
        var sessionId = await Scalar<Guid>(c.Admin, """
            SELECT id FROM auth_sessions WHERE user_id=@u AND selected_organization_id=@o
              AND revoked_at_utc IS NULL ORDER BY issued_at_utc DESC LIMIT 1
            """, ("u", c.ManagerId), ("o", c.Fixture.A));

        async Task<string?> Direct(TimeProvider clock)
        {
            var correlation = Guid.NewGuid().ToString("N");
            var proof = M12ExportProofSigner.Sign(ReferenceDataHttpTests.ExportProofKey, c.Fixture.A,
                c.ManagerId, sessionId, 1, 128, new string('C', 64), correlation, clock)!;
            await using var db = new NpgsqlConnection(c.ApiConnection);
            await db.OpenAsync();
            await using var command = new NpgsqlCommand(
                "SELECT public.m12_record_shipment_export(@o,@u,@s,1,128,@d,@c,@e,@g)", db);
            command.Parameters.AddWithValue("o", c.Fixture.A);
            command.Parameters.AddWithValue("u", c.ManagerId);
            command.Parameters.AddWithValue("s", sessionId);
            command.Parameters.AddWithValue("d", new string('C', 64));
            command.Parameters.AddWithValue("c", correlation);
            command.Parameters.AddWithValue("e", proof.ExpiresEpoch);
            command.Parameters.AddWithValue("g", proof.Signature);
            try { await command.ExecuteNonQueryAsync(); return null; }
            catch (PostgresException error) { return error.SqlState; }
        }

        // Expired and not-yet-valid proofs are refused; a fresh one is accepted.
        Assert.Equal("42501", await Direct(new OffsetClock(TimeSpan.FromSeconds(-90))));
        Assert.Equal("42501", await Direct(new OffsetClock(TimeSpan.FromSeconds(90))));
        Assert.Null(await Direct(TimeProvider.System));
        var baseline = await AuditCount(c);

        // Verifier key rotation: the runtime still signs with the old key, so every export is refused.
        var rotatedKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        async Task Provision(string key)
        {
            await using var scope = seed.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuditRoles:ApiPassword"] = c.ApiPassword,
                ["AuditRoles:WorkerPassword"] = c.WorkerPassword,
                ["M10CorrectionProof:Key"] = ReferenceDataHttpTests.CorrectionProofKey,
                ["M12ExportProof:Key"] = key
            }).Build();
            await AuditRoleProvisioner.ProvisionAsync(db, config, CancellationToken.None);
        }
        await Provision(rotatedKey);
        using (var stale = await manager.GetAsync(c.Export + "?q=" + tag))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, stale.StatusCode);
            Assert.NotEqual("text/csv", stale.Content.Headers.ContentType?.MediaType);
        }
        Assert.Equal(baseline, await AuditCount(c));
        using (var rotated = runtime.WithWebHostBuilder(h => h.UseSetting("M12ExportProof:Key", rotatedKey)))
        using (var client = await Client(rotated, c.Fixture.ManagerEmail, c.Fixture.A))
        using (var ok = await client.GetAsync(c.Export + "?q=" + tag))
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(baseline + 1, await AuditCount(c));

        // Verifier key unavailable (row removed): export refused, no CSV, no audit.
        await Exec(c.Admin, "DELETE FROM m12_export_proof_keys WHERE key_id=1");
        using (var rotated = runtime.WithWebHostBuilder(h => h.UseSetting("M12ExportProof:Key", rotatedKey)))
        using (var client = await Client(rotated, c.Fixture.ManagerEmail, c.Fixture.A))
        using (var missing = await client.GetAsync(c.Export + "?q=" + tag))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, missing.StatusCode);
            Assert.NotEqual("text/csv", missing.Content.Headers.ContentType?.MediaType);
        }
        Assert.Equal(baseline + 1, await AuditCount(c));
        await Provision(rotatedKey);

        // Audit write rejected inside the signed function: complete rollback, no CSV.
        await Exec(c.Admin, """
            CREATE OR REPLACE FUNCTION public.m12_test_reject_audit() RETURNS trigger
            LANGUAGE plpgsql AS $t$ BEGIN
                RAISE EXCEPTION 'injected export audit rejection' USING ERRCODE='XX001';
            END $t$
            """);
        await Exec(c.Admin, $"""
            CREATE TRIGGER m12_test_reject_audit BEFORE INSERT ON audit_entries
            FOR EACH ROW WHEN (NEW.event_type='shipment.export_prepared'
                AND NEW.organization_id='{c.Fixture.A}'::uuid)
            EXECUTE FUNCTION public.m12_test_reject_audit()
            """);
        try
        {
            using var rotated = runtime.WithWebHostBuilder(h => h.UseSetting("M12ExportProof:Key", rotatedKey));
            using var client = await Client(rotated, c.Fixture.ManagerEmail, c.Fixture.A);
            using var rejected = await client.GetAsync(c.Export + "?q=" + tag);
            Note($"S18 audit rejection status={(int)rejected.StatusCode}");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, rejected.StatusCode);
            Assert.NotEqual("text/csv", rejected.Content.Headers.ContentType?.MediaType);
            Assert.Equal(baseline + 1, await AuditCount(c));
        }
        finally
        {
            await Exec(c.Admin, "DROP TRIGGER IF EXISTS m12_test_reject_audit ON audit_entries");
            await Exec(c.Admin, "DROP FUNCTION IF EXISTS public.m12_test_reject_audit()");
        }

        // Downgrade after a retained authorized export is refused and leaves the authority intact.
        await using (var scope = seed.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var migrator = db.GetService<IMigrator>();
            var failure = await Assert.ThrowsAnyAsync<Exception>(
                () => migrator.MigrateAsync("20261107180000_ShipmentExceptions"));
            Assert.Contains("M12 audit history exists", failure.ToString(), StringComparison.Ordinal);
        }
        Assert.True(await Scalar<bool>(c.Admin,
            "SELECT to_regprocedure('public.m12_record_shipment_export(uuid,uuid,uuid,integer,bigint,character varying,character varying,bigint,character varying)') IS NOT NULL"));
        Assert.True(await Scalar<long>(c.Admin,
            "SELECT count(*) FROM audit_entries WHERE event_type='shipment.export_prepared'") >= 2);
        Note("S18 remainder: expiry (both directions), rotation, key removal, audit rejection rollback, downgrade refusal verified");
    }
}
