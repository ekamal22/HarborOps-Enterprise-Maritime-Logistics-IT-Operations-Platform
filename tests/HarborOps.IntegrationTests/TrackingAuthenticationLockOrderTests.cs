using HarborOps.Infrastructure.Integrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// Revocation locks organization, then source, then credential. Bearer authentication must
// acquire its shared locks in that same order, otherwise PostgreSQL reports a deadlock
// (40P01) when a revocation overlaps an authentication.
public sealed class TrackingAuthenticationLockOrderTests
{
    private static async Task<object?> ScalarAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, Guid id)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", id);
        return await command.ExecuteScalarAsync();
    }

    private static async Task WaitForLockWaiterAsync(
        NpgsqlConnection observer, Task authentication, int lockerPid)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            Assert.False(authentication.IsCompleted, "Authentication returned while locks were held");
            await using var command = new NpgsqlCommand("""
                SELECT count(*) FROM pg_stat_activity AS a
                 WHERE a.pid <> pg_backend_pid()
                   AND a.wait_event_type = 'Lock'
                   AND @lockerPid = ANY(pg_blocking_pids(a.pid))
                """, observer);
            command.Parameters.AddWithValue("lockerPid", lockerPid);
            if (Convert.ToInt64(await command.ExecuteScalarAsync()) > 0) return;
            await Task.Delay(50);
        }
        throw new Xunit.Sdk.XunitException("PostgreSQL did not report a blocked authentication waiter");
    }

    [Fact]
    public async Task AuthenticationWaitsForOrganizationAndSourceBeforeLockingCredential()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        ProvisionedIntegrationSource issued;
        await using (var setup = factory.Services.CreateAsyncScope())
            issued = await setup.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
                .CreateAsync(fixture.A, "m8_lock_order", "Lock Order", CancellationToken.None);

        await using var locker = new NpgsqlConnection(TestDatabase.ConnectionString());
        await locker.OpenAsync();
        await using var observer = new NpgsqlConnection(TestDatabase.ConnectionString());
        await observer.OpenAsync();
        await using var tx = await locker.BeginTransactionAsync();
        int lockerPid;
        await using (var pid = new NpgsqlCommand("SELECT pg_backend_pid()", locker, tx))
            lockerPid = Convert.ToInt32(await pid.ExecuteScalarAsync());

        // Same prefix as a revocation: organization, then source, both exclusive.
        Assert.NotNull(await ScalarAsync(locker, tx,
            "SELECT 1 FROM organizations WHERE id = @id FOR UPDATE", fixture.A));
        Assert.NotNull(await ScalarAsync(locker, tx,
            "SELECT 1 FROM integration_sources WHERE id = @id FOR UPDATE", issued.Id));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var authentication = Task.Run(async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
                .AuthenticateAsync(issued.Bearer, cancellation.Token);
        }, cancellation.Token);
        await WaitForLockWaiterAsync(observer, authentication, lockerPid);

        // Revocation now needs the credential row. Authentication must not hold it while it
        // waits for the organization or source, or this update and the wait deadlock.
        await using (var revoke = new NpgsqlCommand("""
            UPDATE integration_credentials
               SET revoked_at_utc = GREATEST(created_at_utc, clock_timestamp())
             WHERE id = @id AND revoked_at_utc IS NULL
            """, locker, tx))
        {
            revoke.Parameters.AddWithValue("id", issued.CredentialId);
            Assert.Equal(1, await revoke.ExecuteNonQueryAsync());
        }
        await tx.CommitAsync();

        var outcome = await authentication.WaitAsync(cancellation.Token);
        Assert.Null(outcome.Source);
    }
}
