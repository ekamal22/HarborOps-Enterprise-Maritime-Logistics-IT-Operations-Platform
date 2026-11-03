using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class TrackingLifecycleRefreshTests
{
    [Fact]
    public async Task CachedOrganizationCannotIssueCredentialAfterExternalSuspension()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var staleScope = factory.Services.CreateAsyncScope();
        var store = staleScope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
        var issued = await store.CreateAsync(fixture.A, "status_refresh", "Status Refresh", CancellationToken.None);

        await using (var secondScope = factory.Services.CreateAsyncScope())
        {
            var db = secondScope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            Assert.Equal(1, await db.Organizations.Where(x => x.Id == fixture.A)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Suspended")));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.RotateAsync(fixture.A, "status_refresh", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CreateAsync(fixture.A, "status_second", "Status Second", CancellationToken.None));
        Assert.Null(await store.ValidateAsync(issued.Bearer, CancellationToken.None));
        await using var verify = factory.Services.CreateAsyncScope();
        var database = verify.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        Assert.Equal(1, await database.IntegrationCredentials.CountAsync(x => x.SourceId == issued.Id));
    }

    [Fact]
    public async Task CachedCredentialRevocationAndDisabledSourceAreIdempotent()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var staleScope = factory.Services.CreateAsyncScope();
        var stale = staleScope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
        var issued = await stale.CreateAsync(fixture.A, "revoke_refresh", "Revocation Refresh", CancellationToken.None);
        await using (var secondScope = factory.Services.CreateAsyncScope())
        {
            var second = secondScope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
            await second.RevokeAsync(fixture.A, "revoke_refresh", issued.CredentialId, CancellationToken.None);
            await second.DisableAsync(fixture.A, "revoke_refresh", CancellationToken.None);
        }
        await stale.RevokeAsync(fixture.A, "revoke_refresh", issued.CredentialId, CancellationToken.None);
        await stale.DisableAsync(fixture.A, "revoke_refresh", CancellationToken.None);
        Assert.Null(await stale.ValidateAsync(issued.Bearer, CancellationToken.None));
        await using var verification = factory.Services.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var row = await db.IntegrationSources.AsNoTracking().SingleAsync(x => x.Id == issued.Id);
        Assert.Equal("Disabled", row.Status);
        Assert.Equal(3L, row.Version);
    }

    [Fact]
    public async Task InFlightValidationWaitsForCommittedRevocation()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        ProvisionedIntegrationSource issued;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            issued = await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
                .CreateAsync(fixture.A, "locked_revoke", "Locked Revoke", CancellationToken.None);
        }

        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString());
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand(
            "UPDATE integration_credentials SET revoked_at_utc = now() WHERE id = @id", connection, tx))
        {
            command.Parameters.AddWithValue("id", issued.CredentialId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var validation = Task.Run(async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            started.SetResult();
            return await scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>()
                .ValidateAsync(issued.Bearer, CancellationToken.None);
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // Prove that validation actually reached the row lock before the revocation commits.
        await using var probe = new NpgsqlConnection(TestDatabase.ConnectionString());
        await probe.OpenAsync();
        var blocked = false;
        for (var i = 0; i < 50; i++)
        {
            await using var check = new NpgsqlCommand("""
                SELECT EXISTS (
                    SELECT 1 FROM pg_stat_activity
                    WHERE pid <> pg_backend_pid() AND wait_event_type = 'Lock'
                      AND query LIKE '%FOR SHARE OF c, s, o%'
                )
                """, probe);
            blocked = (bool)(await check.ExecuteScalarAsync())!;
            if (blocked) break;
            await Task.Delay(100);
        }
        Assert.True(blocked, "Authentication never reached the blocked PostgreSQL row lock");
        Assert.False(validation.IsCompleted);
        await tx.CommitAsync();
        Assert.Null(await validation.WaitAsync(TimeSpan.FromSeconds(15)));
    }
}
