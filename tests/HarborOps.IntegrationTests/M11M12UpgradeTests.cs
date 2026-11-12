using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
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

    // Model a G13-only installation that already applied both reporting migrations.
    // This runs the exact G13 UpOperations after the accepted 15-migration base,
    // then applies only the newly pending G15 migration through EF Core.
    [Fact]
    public async Task PopulatedG13SeventeenMigrationDatabaseAcceptsG15WithoutChangingAcceptedRows()
    {
        var schema = "g15_after_g13_" + Guid.NewGuid().ToString("N");
        var scoped = M10UpgradeTests.Scoped(schema);
        await using var admin = new NpgsqlConnection(TestDatabase.ConnectionString());
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            await using (var baseline = new HarborOpsDbContext(
                new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(scoped).Options))
            {
                await baseline.Database.MigrateAsync("20261107230000_M12ShipmentExportAudit");
                Assert.Equal(15, (await baseline.Database.GetAppliedMigrationsAsync()).Count());
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
            }
            var before = await AcceptedFingerprint(scoped);
            await using (var sql = new NpgsqlConnection(scoped))
            {
                await sql.OpenAsync();
                var reporting = new (string Id, Migration Migration)[]
                {
                    ("20261109020000_M13OperationalKpis", new M13OperationalKpis()),
                    ("20261110010000_M13TenantReportCapacity", new M13TenantReportCapacity())
                };
                foreach (var (id, migration) in reporting)
                {
                    await using var tx = await sql.BeginTransactionAsync();
                    foreach (var op in migration.UpOperations.OfType<SqlOperation>())
                    {
                        await using var command = new NpgsqlCommand(op.Sql, sql, tx)
                            { CommandTimeout = 120 };
                        await command.ExecuteNonQueryAsync();
                    }
                    await using var history = new NpgsqlCommand("""
                        INSERT INTO "__EFMigrationsHistory" ("MigrationId","ProductVersion")
                        SELECT @id,"ProductVersion"
                        FROM "__EFMigrationsHistory"
                        ORDER BY "MigrationId" DESC LIMIT 1
                        """, sql, tx);
                    history.Parameters.AddWithValue("id", id);
                    Assert.Equal(1, await history.ExecuteNonQueryAsync());
                    await tx.CommitAsync();
                }
            }
            await using (var g13 = new HarborOpsDbContext(
                new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(scoped).Options))
            {
                Assert.Equal(17, (await g13.Database.GetAppliedMigrationsAsync()).Count());
                Assert.Contains("20261109020000_M13OperationalKpis",
                    await g13.Database.GetAppliedMigrationsAsync());
                Assert.Contains("20261110010000_M13TenantReportCapacity",
                    await g13.Database.GetAppliedMigrationsAsync());
                Assert.DoesNotContain("20261109010000_M15ScopedNotifications",
                    await g13.Database.GetAppliedMigrationsAsync());
            }
            Assert.Equal(before, await AcceptedFingerprint(scoped));
            Assert.Equal(true, await Scalar(scoped,
                "SELECT to_regclass('operational_kpi_reports') IS NOT NULL"));
            Assert.Equal(false, await Scalar(scoped,
                "SELECT to_regclass('m15_notification_outbox') IS NOT NULL"));
            await using (var upgrade = new HarborOpsDbContext(
                new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(scoped).Options))
            {
                await upgrade.Database.MigrateAsync();
                Assert.Equal(18, (await upgrade.Database.GetAppliedMigrationsAsync()).Count());
                Assert.Empty(await upgrade.Database.GetPendingMigrationsAsync());
                Assert.False(upgrade.Database.HasPendingModelChanges());
            }
            Assert.Equal(before, await AcceptedFingerprint(scoped));
            Assert.Equal(true, await Scalar(scoped,
                "SELECT to_regclass('m15_notification_outbox') IS NOT NULL"));
            Assert.Equal(true, await Scalar(scoped,
                "SELECT to_regclass('operational_kpi_reports') IS NOT NULL"));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task PopulatedAcceptedThirteenMigrationDatabaseSurvivesCombinedEighteenMigrationUpgrade()
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
                Assert.Equal(5, (await before.Database.GetPendingMigrationsAsync()).Count());
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
                Assert.Equal(18, (await upgraded.Database.GetAppliedMigrationsAsync()).Count());
                Assert.Empty(await upgraded.Database.GetPendingMigrationsAsync());
                Assert.Contains("20261110010000_M13TenantReportCapacity",
                    await upgraded.Database.GetAppliedMigrationsAsync());
                Assert.False(upgraded.Database.HasPendingModelChanges());
            }

            Assert.Equal(acceptedBefore, await AcceptedFingerprint(scoped));
            Assert.Equal(true, await Scalar(scoped, "SELECT to_regclass('shipment_exceptions') IS NOT NULL"));
            Assert.Equal(true, await Scalar(scoped, "SELECT to_regclass('m12_export_proof_keys') IS NOT NULL"));
            Assert.Equal(true, await Scalar(scoped, "SELECT to_regclass('m15_notification_outbox') IS NOT NULL"));
            Assert.Equal(true, await Scalar(scoped, "SELECT to_regclass('operational_kpi_reports') IS NOT NULL"));
            Assert.Equal(0L, await Scalar(scoped, "SELECT count(*) FROM shipment_exceptions"));

            // All five additive migrations have empty new business history, so rollback
            // and subsequent reapplication must leave accepted rows unchanged.
            await using (var rollback = new HarborOpsDbContext(
                new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(scoped).Options))
            {
                await rollback.Database.MigrateAsync(AcceptedMainHead);
                Assert.Equal(13, (await rollback.Database.GetAppliedMigrationsAsync()).Count());
                Assert.Equal(acceptedBefore, await AcceptedFingerprint(scoped));
                await rollback.Database.MigrateAsync();
                Assert.Equal(18, (await rollback.Database.GetAppliedMigrationsAsync()).Count());
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
