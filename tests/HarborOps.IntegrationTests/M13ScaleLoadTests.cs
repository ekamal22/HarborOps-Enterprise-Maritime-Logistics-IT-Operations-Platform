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
    private static long Count(JsonElement j, string code, string dimension = "") =>
        j.GetProperty("counts").EnumerateArray()
          .Where(x => x.GetProperty("metricCode").GetString() == code &&
                      x.GetProperty("dimension").GetString() == dimension)
          .Sum(x => x.GetProperty("count").GetInt64());

    [ExclusiveM13PostgresFact]
    public async Task Mixed170kShipmentsProduceExactBoundedReport()
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
        var timer = Stopwatch.StartNew();
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO shipments (id,organization_id,reference_code,
               origin_port_id,destination_port_id,workflow_status,record_status,
               source_kind,version,created_at_utc,updated_at_utc,changed_by_user_id)
            SELECT gen_random_uuid(),@org,'M13S-' || lpad(n::text,6,'0'),
               @origin,@destination,'Draft','Active','Synthetic',1,
               '2026-10-01T00:00:00Z'::timestamptz,
               '2026-10-01T00:00:00Z'::timestamptz,@actor
            FROM generate_series(1,170000) AS n
            """, db) { CommandTimeout = 1200 })
        {
            insert.Parameters.AddWithValue("org", f.A);
            insert.Parameters.AddWithValue("origin", origin);
            insert.Parameters.AddWithValue("destination", destination);
            insert.Parameters.AddWithValue("actor", actor);
            Assert.Equal(170000, await insert.ExecuteNonQueryAsync());
        }
        await using (var update = new NpgsqlCommand("""
            UPDATE shipments SET workflow_status='Cancelled',version=version+1,
                updated_at_utc='2026-10-02T00:00:00Z'::timestamptz,
                changed_by_user_id=@actor
            WHERE organization_id=@org
              AND reference_code BETWEEN 'M13S-110001' AND 'M13S-170000'
            """, db) { CommandTimeout = 1200 })
        {
            update.Parameters.AddWithValue("org", f.A);
            update.Parameters.AddWithValue("actor", actor);
            Assert.Equal(60000, await update.ExecuteNonQueryAsync());
        }
        timer.Stop();
        Console.WriteLine($"G13_170K seeded=170000 existing=2 seed_ms={timer.Elapsed.TotalMilliseconds:F0}");
        using var runtime = new ReferenceDataHttpTests.ApiFactory(roles.Api);
        using var analyst = runtime.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        analyst.Timeout = TimeSpan.FromMinutes(5);
        await ReferenceDataHttpTests.Login(analyst, f.AnalystEmail);
        await ReferenceDataHttpTests.Select(analyst, f.A);

        var latencies = new List<double>();
        for (var i = 0; i < 3; i++)
        {
            var elapsed = Stopwatch.StartNew();
            using var result = await analyst.GetAsync($"/api/v1/organizations/{f.A}/analytics/overview");
            elapsed.Stop();
            var body = await result.Content.ReadAsStringAsync();
            Console.WriteLine($"G13_170K iteration={i + 1} status={(int)result.StatusCode} ms={elapsed.Elapsed.TotalMilliseconds:F0}");
            Assert.True(result.StatusCode == HttpStatusCode.OK,
                $"170k report unavailable, HTTP {(int)result.StatusCode}: {body[..Math.Min(body.Length,400)]}");
            using var parsed = JsonDocument.Parse(body);
            var report = parsed.RootElement;
            Assert.Equal(170002L, Count(report,"D01"));
            Assert.Equal(110002L, Count(report,"K01"));
            Assert.Equal(110002L, Count(report,"K02","Draft"));
            Assert.Equal(60000L, Count(report,"K02","Cancelled"));
            Assert.Equal(0L, Count(report,"K04"));
            Assert.Equal(450006, report.GetProperty("memberCount").GetInt32());
            latencies.Add(elapsed.Elapsed.TotalMilliseconds);
        }
        latencies.Sort();
        Console.WriteLine($"G13_170K p50_ms={latencies[1]:F0} p95_nearest_rank_ms={latencies[2]:F0}");
    }
}
