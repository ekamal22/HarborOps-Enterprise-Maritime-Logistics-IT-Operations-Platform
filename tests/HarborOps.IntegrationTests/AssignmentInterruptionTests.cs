using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace HarborOps.IntegrationTests;

public sealed class AssignmentInterruptionTests(ITestOutputHelper output)
{
    [Fact]
    public async Task TerminatingBackendAfterAssignmentInsertRollsBackWithoutOrphanHistory()
    {
        var connection = TestDatabase.ConnectionString();
        using var factory = new ReferenceDataHttpTests.ApiFactory(connection);
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin; using var manager = f.Manager; using var analyst = f.Analyst;
        var seed = await AssignmentHttpTests.Seed(f);
        var route = seed.Root + "/" + seed.Shipment + "/container-assignments";
        var key = Random.Shared.Next(100000, int.MaxValue);
        var name = "g6_barrier_" + Guid.NewGuid().ToString("N")[..10];
        await using var holder = new NpgsqlConnection(connection);
        await using var ddl = new NpgsqlConnection(connection);
        await holder.OpenAsync();
        await ddl.OpenAsync();
        await using (var hold = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", holder))
        {
            hold.Parameters.AddWithValue("key", key);
            await hold.ExecuteNonQueryAsync();
        }
        var triggerCreated = false;
        try
        {
            var sql = "CREATE FUNCTION " + name + "() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN " +
                "IF NEW.shipment_id = '" + seed.Shipment + "'::uuid THEN " +
                "PERFORM pg_advisory_lock(" + key + "); PERFORM pg_advisory_unlock(" + key + "); END IF; " +
                "RETURN NEW; END $$; CREATE TRIGGER " + name +
                " AFTER INSERT ON shipment_container_assignments FOR EACH ROW EXECUTE FUNCTION " +
                name + "();";
            await using (var create = new NpgsqlCommand(sql, ddl))
                await create.ExecuteNonQueryAsync();
            triggerCreated = true;

            var inFlight = ReferenceDataHttpTests.Send(manager, HttpMethod.Post, route,
                new { containerId = seed.Container, startsAtUtc = "2026-11-10T10:00:00Z" });
            int pid = 0;
            var deadline = DateTime.UtcNow.AddSeconds(35);
            while (DateTime.UtcNow < deadline)
            {
                await using var observation = new NpgsqlCommand("""
                    SELECT l.pid
                    FROM pg_locks l JOIN pg_stat_activity a ON a.pid=l.pid
                    WHERE l.locktype='advisory' AND l.classid=0 AND l.objid=@key
                      AND l.objsubid=1 AND NOT l.granted AND a.backend_xid IS NOT NULL
                      AND a.wait_event_type='Lock' AND a.wait_event='advisory'
                    """, ddl);
                observation.Parameters.AddWithValue("key", (long)key);
                var observed = await observation.ExecuteScalarAsync();
                if (observed is not null and not DBNull)
                {
                    pid = Convert.ToInt32(observed);
                    break;
                }
                await Task.Delay(50);
            }
            Assert.True(pid > 0,"Assignment insert never reached the controlled precommit barrier");
            await using (var inspect = new NpgsqlConnection(connection))
            {
                await inspect.OpenAsync();
                await using var query = new NpgsqlCommand("""
                    SELECT count(*) FROM shipment_container_assignments WHERE shipment_id=@s
                    """, inspect);
                query.Parameters.AddWithValue("s",seed.Shipment);
                Assert.Equal(0L,(long)(await query.ExecuteScalarAsync())!);
            }
            output.WriteLine("[interruption] assignment row inserted, backend has write transaction, uncommitted row invisible");
            await using (var terminate = new NpgsqlCommand("SELECT pg_terminate_backend(@pid)",ddl))
            {
                terminate.Parameters.AddWithValue("pid",pid);
                Assert.True((bool)(await terminate.ExecuteScalarAsync())!);
            }
            using var response = await inFlight;
            Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
            Assert.Contains("no-store",response.Headers.CacheControl?.ToString() ?? "");
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("dependency_unavailable",json.GetProperty("code").GetString());
            await using (var released = new NpgsqlConnection(connection))
            {
                await released.OpenAsync();
                await using var query = new NpgsqlCommand("""
                    SELECT count(*) FROM shipment_container_assignments WHERE shipment_id=@s
                    """, released);
                query.Parameters.AddWithValue("s",seed.Shipment);
                Assert.Equal(0L,(long)(await query.ExecuteScalarAsync())!);
            }
            output.WriteLine("[interruption] backend terminated before commit; no assignment persisted");
        }
        finally
        {
            await using (var unlock = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",holder))
            {
                unlock.Parameters.AddWithValue("key",key);
                await unlock.ExecuteScalarAsync();
            }
            if (triggerCreated)
            {
                await using var remove = new NpgsqlCommand(
                    "DROP TRIGGER IF EXISTS " + name + " ON shipment_container_assignments; " +
                    "DROP FUNCTION IF EXISTS " + name + "();",ddl);
                await remove.ExecuteNonQueryAsync();
            }
        }
        using (var retry = await ReferenceDataHttpTests.Send(manager,HttpMethod.Post,route,
            new { containerId = seed.Container, startsAtUtc = "2026-11-10T10:00:00Z" }))
            Assert.Equal(HttpStatusCode.Created,retry.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(1,await db.ShipmentContainerAssignments.AsNoTracking()
            .CountAsync(x => x.ShipmentId==seed.Shipment && x.ContainerId==seed.Container));
        output.WriteLine("[interruption] recovery: retry committed exactly one durable row");
    }
}
