using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class ReferenceRevocationInterleavingTests
{
    [Fact]
    public async Task QueuedRoleDowngradeWinsBeforeBlockedReferenceWrite()
    {
        var raw = TestDatabase.ConnectionString();
        var name = "g3-ordered-" + Guid.NewGuid().ToString("N");
        var configured = new NpgsqlConnectionStringBuilder(raw) { ApplicationName = name };
        using var factory = new ReferenceDataHttpTests.ApiFactory(configured.ConnectionString);
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin;
        using var manager = f.Manager;
        using var analyst = f.Analyst;
        var route = $"/api/v1/organizations/{f.A}/ports";
        using var created = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post, route,
            new { referenceCode = "LOCK-RACE", name = "Original", countryCode = "US" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var recordId = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var target = await db.AuthUsers.AsNoTracking().SingleAsync(u =>
            u.NormalizedEmail == IdentityStore.NormalizeEmail(f.ManagerEmail));
        var membership = await db.OrganizationMemberships.AsNoTracking().SingleAsync(m =>
            m.OrganizationId == f.A && m.UserId == target.Id);

        await using var connection = new NpgsqlConnection(raw);
        await connection.OpenAsync();
        await using var blocker = await connection.BeginTransactionAsync();
        await using (var hold = new NpgsqlCommand(
            "SELECT id FROM organizations WHERE id = @id FOR UPDATE", connection, blocker))
        {
            hold.Parameters.AddWithValue("id", f.A);
            Assert.Equal(f.A, await hold.ExecuteScalarAsync());
        }
        Task<HttpResponseMessage>? downgrade = null;
        Task<HttpResponseMessage>? write = null;
        try
        {
            downgrade = ReferenceDataHttpTests.Send(admin, HttpMethod.Put,
                $"/api/v1/organizations/{f.A}/memberships/{target.Id}",
                new { role = "Support", status = "Active", expectedVersion = membership.Version });

            var waiting = false;
            for (var attempt = 0; attempt < 150; attempt++)
            {
                await Task.Delay(50);
                await using var probe = new NpgsqlConnection(raw);
                await probe.OpenAsync();
                await using var query = new NpgsqlCommand("""
                    SELECT count(*) FROM pg_stat_activity
                    WHERE application_name = @app
                      AND wait_event_type = 'Lock'
                      AND query ILIKE '%organizations%'
                      AND query ILIKE '%FOR UPDATE%'
                    """, probe);
                query.Parameters.AddWithValue("app", name);
                if (Convert.ToInt64(await query.ExecuteScalarAsync()) > 0)
                {
                    waiting = true;
                    break;
                }
            }
            Assert.True(waiting, "Administrator downgrade must be queued on organization lock");
            // PostgreSQL lock queue is established before the competing write.
            write = ReferenceDataHttpTests.Send(manager, HttpMethod.Patch,
                route + "/" + recordId, new { name = "Not authorized", expectedVersion = 1L });
        }
        finally
        {
            await blocker.CommitAsync();
        }
        Assert.NotNull(downgrade);
        using var changed = await downgrade!;
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.NotNull(write);
        using var rejected = await write!;
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        var record = await db.Ports.AsNoTracking().SingleAsync(p => p.Id == recordId);
        Assert.Equal("Original", record.Name);
        Assert.Equal(1L, record.Version);
    }

    [Fact]
    public async Task QueuedSecurityVersionChangeDeniesReferenceWrite()
    {
        var raw = TestDatabase.ConnectionString();
        var name = "g3-version-" + Guid.NewGuid().ToString("N");
        using var factory = new ReferenceDataHttpTests.ApiFactory(raw);
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        var root = $"/api/v1/organizations/{fixture.A}/vessels";
        using var created = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post, root,
            new { referenceCode = "RESET-RACE", name = "Before reset" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var user = await db.AuthUsers.AsNoTracking().SingleAsync(u =>
            u.NormalizedEmail == IdentityStore.NormalizeEmail(fixture.ManagerEmail));
        await using var holdConnection = new NpgsqlConnection(raw);
        await holdConnection.OpenAsync();
        await using var blocker = await holdConnection.BeginTransactionAsync();
        await using (var hold = new NpgsqlCommand(
            "SELECT id FROM users WHERE id = @id FOR UPDATE", holdConnection, blocker))
        {
            hold.Parameters.AddWithValue("id", user.Id);
            Assert.Equal(user.Id, await hold.ExecuteScalarAsync());
        }
        Task? reset = null;
        Task<HttpResponseMessage>? mutation = null;
        try
        {
            reset = Task.Run(async () =>
            {
                var cfg = new NpgsqlConnectionStringBuilder(raw) { ApplicationName = name };
                await using var connection = new NpgsqlConnection(cfg.ConnectionString);
                await connection.OpenAsync();
                await using var cmd = new NpgsqlCommand(
                    "UPDATE users SET security_version = security_version + 1 WHERE id = @id",
                    connection);
                cmd.Parameters.AddWithValue("id", user.Id);
                Assert.Equal(1, await cmd.ExecuteNonQueryAsync());
            });
            var observed = false;
            for (var attempt = 0; attempt < 150; attempt++)
            {
                await Task.Delay(50);
                await using var probe = new NpgsqlConnection(raw);
                await probe.OpenAsync();
                await using var cmd = new NpgsqlCommand("""
                    SELECT count(*) FROM pg_stat_activity
                    WHERE application_name = @app AND wait_event_type = 'Lock'
                      AND query ILIKE '%UPDATE users%'
                    """, probe);
                cmd.Parameters.AddWithValue("app", name);
                if (Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0)
                {
                    observed = true;
                    break;
                }
            }
            Assert.True(observed, "The security version change must be queued first");
            mutation = ReferenceDataHttpTests.Send(manager, HttpMethod.Patch,
                root + "/" + id, new { name = "After reset", expectedVersion = 1L });
        }
        finally
        {
            await blocker.CommitAsync();
        }
        Assert.NotNull(reset);
        await reset!;
        Assert.NotNull(mutation);
        using var denied = await mutation!;
        Assert.Contains(denied.StatusCode,
            new[] { HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized });
        var record = await db.Vessels.AsNoTracking().SingleAsync(v => v.Id == id);
        Assert.Equal("Before reset", record.Name);
        Assert.Equal(1L, record.Version);
    }

}
