using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class M11M12UpgradeTests
{
    private const string AcceptedMainHead = "20261107120000_M10CorrectionAuthority";

    private static Task<object?> Scalar(string connection, string sql) =>
        M10UpgradeTests.Scalar(connection, sql);

    private static async Task<string> AcceptedFingerprint(string connection)
    {
        var result = await Scalar(connection, """
            SELECT concat_ws('/',
              (SELECT md5(COALESCE(string_agg(s::text,'|' ORDER BY s.id),'')) FROM shipments s),
              (SELECT md5(COALESCE(string_agg(c::text,'|' ORDER BY c.id),'')) FROM shipment_status_changes c),
              (SELECT md5(COALESCE(string_agg(a::text,'|' ORDER BY a.id),'')) FROM audit_entries a),
              (SELECT md5(COALESCE(string_agg(v::text,'|' ORDER BY v.id),'')) FROM voyages v),
              (SELECT md5(COALESCE(string_agg(m::text,'|' ORDER BY m.id),'')) FROM organization_memberships m))
            """);
        return Assert.IsType<string>(result);
    }

    [Fact]
    public async Task PopulatedAcceptedThirteenMigrationDatabaseSurvivesSixteenMigrationUpgrade()
    {
        var schema = "m1112_upgrade_" + Guid.NewGuid().ToString("N");
        var scoped = M10UpgradeTests.Scoped(schema);
        await using var admin = new NpgsqlConnection(TestDatabase.ConnectionString());
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            await using (var before = new HarborOpsDbContext(
                new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(scoped).Options))
            {
                await before.Database.MigrateAsync(AcceptedMainHead);
                Assert.Equal(13, (await before.Database.GetAppliedMigrationsAsync()).Count());
                Assert.Equal(3, (await before.Database.GetPendingMigrationsAsync()).Count());
            }

            using (var factory = new ReferenceDataHttpTests.ApiFactory(scoped))
            {
                var fixture = await ReferenceDataHttpTests.Setup(factory, migrateToHead: false);
                using var manager = fixture.Manager;
                using var analyst = fixture.Analyst;
                using var administrator = fixture.Admin;
                var seed = await AssignmentHttpTests.Seed(fixture);
                using var changed = await ReferenceDataHttpTests.Send(manager,
                    HttpMethod.Patch, seed.Root + "/" + seed.Shipment,
                    new { expectedVersion = 1L, workflowStatus = "Booked" });
                Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
                using var existing = await analyst.GetAsync(seed.Root + "/" + seed.Shipment);
                Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
            }

            Assert.Equal(2L, await Scalar(scoped, "SELECT count(*) FROM shipments"));
            var acceptedBefore = await AcceptedFingerprint(scoped);
            await using (var upgraded = new HarborOpsDbContext(
                new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(scoped).Options))
            {
                await upgraded.Database.MigrateAsync();
                Assert.Equal(16, (await upgraded.Database.GetAppliedMigrationsAsync()).Count());
                Assert.Empty(await upgraded.Database.GetPendingMigrationsAsync());
                Assert.False(upgraded.Database.HasPendingModelChanges());
            }

            Assert.Equal(acceptedBefore, await AcceptedFingerprint(scoped));
            Assert.Equal(true, await Scalar(scoped, "SELECT to_regclass('shipment_exceptions') IS NOT NULL"));
            Assert.Equal(true, await Scalar(scoped, "SELECT to_regclass('m12_export_proof_keys') IS NOT NULL"));
            Assert.Equal(0L, await Scalar(scoped, "SELECT count(*) FROM shipment_exceptions"));

            // All three additive migrations have empty new business history, so rollback
            // and subsequent reapplication must leave accepted rows unchanged.
            await using (var rollback = new HarborOpsDbContext(
                new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(scoped).Options))
            {
                await rollback.Database.MigrateAsync(AcceptedMainHead);
                Assert.Equal(13, (await rollback.Database.GetAppliedMigrationsAsync()).Count());
                Assert.Equal(acceptedBefore, await AcceptedFingerprint(scoped));
                await rollback.Database.MigrateAsync();
                Assert.Equal(16, (await rollback.Database.GetAppliedMigrationsAsync()).Count());
                Assert.False(rollback.Database.HasPendingModelChanges());
            }
            Assert.Equal(acceptedBefore, await AcceptedFingerprint(scoped));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
