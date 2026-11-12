using HarborOps.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// G15-S15-A04 and G13-S18-A05: a downgrade that would destroy notification delivery
// evidence fails closed. The test runs the real Down script of the M15 migration
// against a database that holds delivered receipts: the guard raises, the script's
// own transaction removes nothing, and every receipt, intent and M11 row remains.
[Collection("M13ExclusiveServer")]
public sealed class M15DowngradeGuardAcceptanceTests
{
    [ExclusiveM13PostgresFact]
    public async Task DowngradeOfAMigrationHoldingDeliveryEvidenceFailsClosedAndLeavesEveryRow()
    {
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var seedAdmin = s.Fixture.Admin; using var seedManager = s.Fixture.Manager;
        using var seedAnalyst = s.Fixture.Analyst;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        await M15NotificationRuntimeTests.EnsureShipmentDelivered(s, shipment);

        var down = new M15ScopedNotifications().DownOperations
            .OfType<SqlOperation>().Select(x => x.Sql).ToList();
        Assert.NotEmpty(down);
        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        async Task<string> Evidence()
        {
            await using var count = new NpgsqlCommand("""
                SELECT concat_ws('/',
                  (SELECT count(*) FROM m15_notification_outbox),
                  (SELECT count(*) FROM m15_user_notifications),
                  (SELECT count(*) FROM m15_notification_replays),
                  (SELECT count(*) FROM shipment_exceptions),
                  (SELECT count(*) FROM shipment_exception_history))
                """, db);
            return Assert.IsType<string>(await count.ExecuteScalarAsync());
        }
        var before = await Evidence();
        Assert.NotEqual("0/0/0/0/0", before);
        Assert.False(before.StartsWith("0/"), "the fixture must hold at least one intent");

        foreach (var sql in down)
        {
            await using var tx = await db.BeginTransactionAsync();
            await using var script = new NpgsqlCommand(sql, db, tx);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => script.ExecuteNonQueryAsync());
            Assert.Contains("M15 downgrade would destroy delivery evidence", failure.MessageText);
            await tx.RollbackAsync();
        }
        Assert.Equal(before, await Evidence());
        foreach (var table in new[] { "m15_notification_outbox", "m15_user_notifications", "m15_notification_replays" })
        {
            await using var exists = new NpgsqlCommand("SELECT to_regclass(@name) IS NOT NULL", db);
            exists.Parameters.AddWithValue("name", table);
            Assert.True(Assert.IsType<bool>(await exists.ExecuteScalarAsync()), table + " must still exist");
        }
    }
}
