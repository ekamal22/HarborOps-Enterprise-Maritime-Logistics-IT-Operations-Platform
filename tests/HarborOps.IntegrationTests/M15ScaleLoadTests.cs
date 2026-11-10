using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Notifications;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// Dedicated GitHub Actions runner only. Never run against a retained database.
public sealed class G15ScaleFactAttribute : FactAttribute
{
    public G15ScaleFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("HARBOROPS_G15_EXCLUSIVE_SCALE") != "1" ||
            Environment.GetEnvironmentVariable("HARBOROPS_G13_EXCLUSIVE_POSTGRES") != "1")
            Skip = "G15 high-volume fixture requires an exclusive disposable PostgreSQL server";
    }
}

[Collection("M13ExclusiveServer")]
public sealed class M15ScaleLoadTests
{
    [G15ScaleFact]
    public async Task OneMillionRowsPreserveBoundedScopedKeysetPagination()
    {
        const int rows = 1_000_000;
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var adminFactory = s.Factory;
        using var apiFactory = s.Runtime;
        using var originalAdmin = s.Fixture.Admin;
        using var originalManager = s.Fixture.Manager;
        using var originalAnalyst = s.Fixture.Analyst;
        using var manager = s.Manager;
        using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        var exceptionId = await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        await using var sql = new NpgsqlConnection(s.Roles.Admin);
        await sql.OpenAsync();

        // Load data is deliberately synthetic. It tests the index, RLS and
        // pagination envelope, not the meaning of a million case openings.
        // Insertion invokes the real immutable history/outbox trigger.
        var stopwatch = Stopwatch.StartNew();
        for (var batch = 0; batch < rows / 50_000; batch++)
        {
            await using var insert = new NpgsqlCommand("""
                WITH inserted AS (
                    INSERT INTO shipment_exception_history (
                        id,organization_id,exception_id,action,old_state,new_state,
                        actor_kind,source_fingerprint,occurred_at_utc)
                    SELECT gen_random_uuid(),@org,@exception,'Opened',NULL,'Open',
                        'System',repeat('a',64),clock_timestamp()
                    FROM generate_series(1,50000)
                    RETURNING id
                )
                SELECT COUNT(*) FROM inserted
                """, sql);
            insert.CommandTimeout = 180;
            insert.Parameters.AddWithValue("org", s.Fixture.A);
            insert.Parameters.AddWithValue("exception", exceptionId);
            Assert.Equal(50_000L, Convert.ToInt64(await insert.ExecuteScalarAsync()));
        }
        var seededMs = stopwatch.ElapsedMilliseconds;

        Guid recipient;
        Guid membership;
        long version;
        await using (var audience = new NpgsqlCommand("""
            SELECT user_id,id,version FROM organization_memberships
            WHERE organization_id=@org AND role='Manager' AND status='Active'
            ORDER BY id LIMIT 1
            """, sql))
        {
            audience.Parameters.AddWithValue("org", s.Fixture.A);
            await using var reader = await audience.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            recipient = reader.GetGuid(0);
            membership = reader.GetGuid(1);
            version = reader.GetInt64(2);
        }
        await using (var fanout = new NpgsqlCommand("""
            INSERT INTO m15_user_notifications (
                source_history_id,organization_id,recipient_user_id,
                membership_id,membership_version,shipment_id,exception_id,
                event_type,rule_code,created_at_utc)
            SELECT o.source_history_id,o.organization_id,@recipient,
                @membership,@version,o.shipment_id,o.exception_id,
                o.event_type,o.rule_code,o.created_at_utc
            FROM m15_notification_outbox o
            WHERE o.organization_id=@org AND o.shipment_id=@shipment
            ON CONFLICT (source_history_id,recipient_user_id) DO NOTHING
            """, sql))
        {
            fanout.CommandTimeout = 240;
            fanout.Parameters.AddWithValue("org", s.Fixture.A);
            fanout.Parameters.AddWithValue("shipment", shipment);
            fanout.Parameters.AddWithValue("recipient", recipient);
            fanout.Parameters.AddWithValue("membership", membership);
            fanout.Parameters.AddWithValue("version", version);
            Assert.True(await fanout.ExecuteNonQueryAsync() >= rows);
        }
        // The load inserts a million rows in one burst. Production keeps statistics
        // current through autovacuum; without them a state filtered read can pick a
        // partial-index-plus-sort plan that applies the per-row policy to every
        // row. Refresh statistics the way the G13 scale fixture does before the
        // measured reads. Policy, timeouts and budgets are unchanged.
        await using (var statistics = new NpgsqlCommand(
            "ANALYZE m15_user_notifications; ANALYZE m15_notification_outbox;", sql)
            { CommandTimeout = 300 })
            await statistics.ExecuteNonQueryAsync();
        var readyMs = stopwatch.ElapsedMilliseconds;
        await using (var count = new NpgsqlCommand("""
            SELECT COUNT(*) FROM m15_user_notifications
            WHERE organization_id=@org AND recipient_user_id=@recipient
            """, sql))
        {
            count.Parameters.AddWithValue("org", s.Fixture.A);
            count.Parameters.AddWithValue("recipient", recipient);
            Assert.True(Convert.ToInt64(await count.ExecuteScalarAsync()) >= rows);
        }

        var root = $"/api/v1/organizations/{s.Fixture.A}/notifications";
        var durations = new List<long>();
        var ids = new HashSet<Guid>();
        string? cursor = null;
        for (var page = 0; page < 10; page++)
        {
            var path = root + "?limit=50&state=All";
            if (cursor is not null) path += "&cursor=" + Uri.EscapeDataString(cursor);
            var clock = Stopwatch.StartNew();
            using var response = await manager.GetAsync(path);
            clock.Stop();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            durations.Add(clock.ElapsedMilliseconds);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var items = body.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(50, items.Length);
            foreach (var item in items)
                Assert.True(ids.Add(item.GetProperty("id").GetGuid()),
                    "Repeated receipt in high-volume keyset");
            var next = body.GetProperty("nextCursor");
            Assert.Equal(JsonValueKind.String, next.ValueKind);
            cursor = next.GetString();
        }
        Assert.Equal(500, ids.Count);

        // Unread count and every state filter must also stay bounded at the
        // stated volume: the count scans the recipient's unread rows and every
        // read goes through the per-row security policy.
        var stateTimes = new List<string>();
        var probeFailures = new List<string>();
        async Task<long> Probe(string label, string path, Func<JsonElement, long> value)
        {
            var timer = Stopwatch.StartNew();
            using var response = await manager.GetAsync(root + path);
            timer.Stop();
            stateTimes.Add($"{label}={(int)response.StatusCode}/{timer.ElapsedMilliseconds}ms");
            if (response.StatusCode != HttpStatusCode.OK || timer.Elapsed >= TimeSpan.FromSeconds(20))
            {
                probeFailures.Add($"{label} status={(int)response.StatusCode} ms={timer.ElapsedMilliseconds}");
                return -1;
            }
            return value(await response.Content.ReadFromJsonAsync<JsonElement>());
        }
        var unread = await Probe("unread_count", "/unread-count",
            j => j.GetProperty("unreadCount").GetInt64());
        var unreadPage = await Probe("state_unread", "?limit=50&state=Unread",
            j => j.GetProperty("items").GetArrayLength());
        var readPage = await Probe("state_read", "?limit=50&state=Read",
            j => j.GetProperty("items").GetArrayLength());
        var ackPage = await Probe("state_acknowledged", "?limit=50&state=Acknowledged",
            j => j.GetProperty("items").GetArrayLength());
        Console.WriteLine("G15_STATE_PROBES " + string.Join(" ", stateTimes));
        Assert.True(probeFailures.Count == 0, "Unbounded probes: " + string.Join("; ", probeFailures));
        // The count is exact up to the cap and never above it.
        Assert.InRange(unread, 1L, 1000L);
        Assert.Equal(50, unreadPage);
        Assert.Equal(0, readPage);
        Assert.Equal(0, ackPage);
        using var foreign = await manager.GetAsync(
            $"/api/v1/organizations/{s.Fixture.B}/notifications?limit=50");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        durations.Sort();
        var p50 = durations[durations.Count / 2 - 1];
        var p95 = durations[(int)Math.Ceiling(durations.Count * 0.95) - 1];
        Console.WriteLine(
            $"G15_SCALE receipts>={rows} seed_ms={seededMs} ready_ms={readyMs} " +
            $"pages=10 members=500 p50_ms={p50} p95_ms={p95}");
    }
}
