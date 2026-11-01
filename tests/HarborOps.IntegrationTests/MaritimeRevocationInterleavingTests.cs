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

    // Every revocation below commits while the Manager's requests are already blocked on the
    // organization lock inside the endpoint, after authentication succeeded. The "none" case is
    // the positive control proving the same queued requests succeed when nothing is revoked.
    [Theory]
    [InlineData("none")]
    [InlineData("member_removed")]
    [InlineData("organization_suspended")]
    [InlineData("account_disabled")]
    [InlineData("security_version")]
    public async Task QueuedMaritimeRequestsRevalidateAccessAfterTheLockIsReleased(string change)
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var containers = $"/api/v1/organizations/{f.A}/containers";
        var voyages = $"/api/v1/organizations/{f.A}/voyages";
        using var created = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post, containers,
            new { referenceCode = "QUEUED-REVALIDATE", identifierKind = "Synthetic",
                containerIdentifier = "SYN-BOX-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
                equipmentType = "Dry20" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Guid managerId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            managerId = (await db.OrganizationMemberships.AsNoTracking()
                .SingleAsync(m => m.OrganizationId == f.A && m.Role == "Manager")).UserId;
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
        var committed = false;
        try
        {
            var patch = ReferenceDataHttpTests.Send(manager, HttpMethod.Patch, containers + "/" + id,
                new { displayName = "Queued edit", expectedVersion = 1L });
            var voyage = ReferenceDataHttpTests.Send(manager, HttpMethod.Post, voyages,
                new { referenceCode = "QUEUED-VOYAGE", vesselId = Guid.NewGuid(),
                    originPortId = Guid.NewGuid(), destinationPortId = Guid.NewGuid() });
            await WaitForLockWaiters(connection, 2);
            if (change == "organization_suspended")
            {
                await using var suspend = new NpgsqlCommand(
                    "UPDATE organizations SET status = 'Suspended' WHERE id = @id", blocker, tx);
                suspend.Parameters.AddWithValue("id", f.A);
                Assert.Equal(1, await suspend.ExecuteNonQueryAsync());
            }
            else if (change != "none")
            {
                // These rows are not locked by the blocker, so the revocation commits independently
                // while both requests remain queued on the organization row.
                var sql = change switch
                {
                    "member_removed" => """
                        UPDATE organization_memberships SET status = 'Removed', version = version + 1,
                          updated_at_utc = now() WHERE organization_id = @org AND user_id = @user
                        """,
                    "account_disabled" => """
                        UPDATE users SET status = 'Disabled', disabled_at_utc = now() WHERE id = @user
                        """,
                    _ => "UPDATE users SET security_version = security_version + 1 WHERE id = @user"
                };
                await using var other = new NpgsqlConnection(connection);
                await other.OpenAsync();
                await using var revoke = new NpgsqlCommand(sql, other);
                revoke.Parameters.AddWithValue("org", f.A);
                revoke.Parameters.AddWithValue("user", managerId);
                Assert.Equal(1, await revoke.ExecuteNonQueryAsync());
            }
            await tx.CommitAsync();
            committed = true;
            using var patched = await patch;
            using var posted = await voyage;
            if (change == "none")
            {
                Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
                // Valid scope with nonexistent parents is the distinct, expected not-found result.
                Assert.Equal(HttpStatusCode.NotFound, posted.StatusCode);
            }
            else
            {
                foreach (var response in new[] { patched, posted })
                {
                    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                    Assert.Equal("organization_access_revoked", (await response.Content
                        .ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
                }
            }
            await using var verify = factory.Services.CreateAsyncScope();
            var check = verify.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
            var row = await check.Containers.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(change == "none" ? "Queued edit" : null, row.DisplayName);
            Assert.Equal(change == "none" ? 2L : 1L, row.Version);
            Assert.Equal(0, await check.Voyages.CountAsync(x => x.OrganizationId == f.A));
        }
        finally
        {
            if (!committed) await tx.RollbackAsync();
        }
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
        var committed = false;
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
            committed = true;
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
            if (!committed) await tx.RollbackAsync();
        }
    }
}
