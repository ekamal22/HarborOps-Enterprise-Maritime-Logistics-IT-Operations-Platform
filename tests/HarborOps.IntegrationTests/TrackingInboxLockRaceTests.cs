using System.Text;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class TrackingInboxLockRaceTests
{
    private static byte[] Payload(string id) => Encoding.UTF8.GetBytes($$"""
        {"sourceEventId":"{{id}}","eventFamily":"Equipment","eventCode":"GTIN",
         "classifier":"ACT","occurredAtUtc":"2026-11-03T10:00:00Z",
         "equipmentReference":"SYN-BOX-ABCD","externalSchemaVersion":"2.2.0"}
        """);

    private static async Task AcquireAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, Guid id)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", id);
        Assert.NotNull(await command.ExecuteScalarAsync());
    }

    private static async Task WaitForPostgresLockWaitAsync(NpgsqlConnection observer, Task receiving)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            Assert.False(receiving.IsCompleted, "Intake returned before the held row lock was released");
            await using var command = new NpgsqlCommand("""
                SELECT count(*) FROM pg_stat_activity
                 WHERE pid <> pg_backend_pid()
                   AND wait_event_type = 'Lock'
                   AND (query LIKE '%FOR UPDATE%' OR query LIKE '%FOR SHARE OF s, c%')
                """, observer);
            if (Convert.ToInt64(await command.ExecuteScalarAsync()) > 0) return;
            await Task.Delay(50);
        }
        throw new Xunit.Sdk.XunitException("PostgreSQL did not report a blocked row-lock waiter");
    }

    [Theory]
    [InlineData("organization")]
    [InlineData("source")]
    [InlineData("credential")]
    public async Task OverlappingRevocationDisableAndSuspensionRespectCommitOrder(string target)
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        ProvisionedIntegrationSource issued;
        await using (var setup = factory.Services.CreateAsyncScope())
            issued = await setup.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
                .CreateAsync(fixture.A, "m8_wait_" + target, "Locked Source",
                    CancellationToken.None);

        var payload = Payload("lock-" + target);
        var ev = TrackingEventPolicy.Parse(payload, TimeProvider.System.GetUtcNow()).Value!
            .Bind(issued.Id, fixture.A);
        await using var locker = new NpgsqlConnection(TestDatabase.ConnectionString());
        await locker.OpenAsync();
        await using var observer = new NpgsqlConnection(TestDatabase.ConnectionString());
        await observer.OpenAsync();
        await using var tx = await locker.BeginTransactionAsync();
        var (query, id, mutation) = target switch
        {
            "organization" => (
                "SELECT id FROM organizations WHERE id = @id FOR UPDATE",
                fixture.A,
                "UPDATE organizations SET status = 'Suspended' WHERE id = @id"),
            "source" => (
                "SELECT id FROM integration_sources WHERE id = @id FOR UPDATE",
                issued.Id,
                "UPDATE integration_sources SET status = 'Disabled', disabled_at_utc = clock_timestamp(), updated_at_utc = clock_timestamp(), version = version + 1 WHERE id = @id"),
            _ => (
                "SELECT id FROM integration_credentials WHERE id = @id FOR UPDATE",
                issued.CredentialId,
                "UPDATE integration_credentials SET revoked_at_utc = clock_timestamp() WHERE id = @id")
        };
        await AcquireAsync(locker, tx, query, id);

        // A new scoped DbContext runs the receiving transaction on another
        // PostgreSQL session. A held row lock delays its final authorization.
        var receiving = Task.Run(async () =>
        {
            await using var incoming = factory.Services.CreateAsyncScope();
            var store = incoming.ServiceProvider.GetRequiredService<TrackingInboxStore>();
            return await store.ReceiveAsync(ev, issued.Bearer, payload, "lock-" + target,
                CancellationToken.None);
        });
        await WaitForPostgresLockWaitAsync(observer, receiving);

        await using (var update = new NpgsqlCommand(mutation, locker, tx))
        {
            update.Parameters.AddWithValue("id", id);
            Assert.Equal(1, await update.ExecuteNonQueryAsync());
        }
        await tx.CommitAsync();
        var result = await receiving.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(TrackingReceiptOutcome.AccessDenied, result.Outcome);
        await using var verify = factory.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Empty(await db.TrackingInboxReceipts.Where(x => x.SourceId == issued.Id).ToListAsync());
        Assert.Empty(await db.TrackingAcceptedEvents.Where(x => x.SourceId == issued.Id).ToListAsync());
    }
}
