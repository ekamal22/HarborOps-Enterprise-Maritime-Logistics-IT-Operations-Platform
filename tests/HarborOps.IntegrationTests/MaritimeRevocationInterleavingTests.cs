using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class MaritimeRevocationInterleavingTests
{
    private static async Task WaitForLockWaiters(string connection, int count)
    {
        await using var monitor = new NpgsqlConnection(connection);
        await monitor.OpenAsync();
        for (var attempt = 0; attempt < 240; attempt++)
        {
            await using var query = new NpgsqlCommand("""
                SELECT count(*) FROM pg_stat_activity
                WHERE datname = current_database()
                  AND pid <> pg_backend_pid()
                  AND wait_event_type = 'Lock'
                  AND query ILIKE '%organizations%'
                """, monitor);
            var waiting = Convert.ToInt32(await query.ExecuteScalarAsync());
            if (waiting >= count) return;
            await Task.Delay(25);
        }
        Assert.Fail($"Expected {count} blocked organization operations before releasing the transaction");
    }

    [Fact]
    public async Task QueuedAdministratorDowngradePrecedesContainerMutation()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var route = $"/api/v1/organizations/{f.A}/containers";
        using var created = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post, route,
            new { referenceCode = "QUEUED-BOX", identifierKind = "Synthetic",
                containerIdentifier = "SYN-BOX-QUEUED", equipmentType = "Dry20" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Guid managerId;
        long membershipVersion;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var membership = await db.OrganizationMemberships.AsNoTracking()
                .SingleAsync(m => m.OrganizationId == f.A && m.Role == "Manager");
            managerId = membership.UserId;
            membershipVersion = membership.Version;
        }
        var connection = TestDatabase.ConnectionString();
        await using var blocker = new NpgsqlConnection(connection);
        await blocker.OpenAsync();
        await using var tx = await blocker.BeginTransactionAsync();
        await using (var acquire = new NpgsqlCommand(
            "SELECT id FROM organizations WHERE id = @id FOR UPDATE", blocker, tx))
        {
            acquire.Parameters.AddWithValue("id", f.A);
            Assert.Equal(f.A, (Guid)(await acquire.ExecuteScalarAsync())!);
        }
        var adminChange = ReferenceDataHttpTests.Send(admin, HttpMethod.Put,
            $"/api/v1/organizations/{f.A}/memberships/{managerId}",
            new { role = "Support", status = "Active", expectedVersion = membershipVersion });
        try
        {
            await WaitForLockWaiters(connection, 1);
            var managerChange = ReferenceDataHttpTests.Send(manager, HttpMethod.Patch,
                route + "/" + id, new { displayName = "Should never commit", expectedVersion = 1L });
            await WaitForLockWaiters(connection, 2);
            await tx.CommitAsync();
            using var changed = await adminChange;
            using var denied = await managerChange;
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var original = await db.Containers.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Null(original.DisplayName);
            Assert.Equal(1L, original.Version);
        }
        finally
        {
            // Safe when committed, and ensures no stranded lock if a waiter assertion fails.
            await tx.RollbackAsync().ConfigureAwait(false);
        }
    }
}
