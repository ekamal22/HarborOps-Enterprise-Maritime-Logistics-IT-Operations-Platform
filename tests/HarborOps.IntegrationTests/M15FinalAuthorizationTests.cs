using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Notifications;
using HarborOps.Infrastructure.Organizations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class M15FinalAuthorizationTests
{
    private const string Password = "Synthetic-reference-integration-passphrase-123";

    private static async Task<(string Email, Guid User)> CreateMember(
        M11ExceptionRuntimeTests.Setup s, string role)
    {
        var email = "m15-" + role.ToLowerInvariant() + "-" +
            Guid.NewGuid().ToString("N") + "@example.test";
        await using var scope = s.Factory.Services.CreateAsyncScope();
        var ids = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var orgs = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        await ids.ProvisionAsync(email, "Notification Role Fixture",
            Password, CancellationToken.None);
        var grantedUser = await ids.LoginAsync(
            email, Password, "m15-role-test", CancellationToken.None);
        var administrator = await ids.LoginAsync(
            s.Fixture.AdminEmail, Password, "m15-role-test", CancellationToken.None);
        Assert.NotNull(grantedUser);
        Assert.NotNull(administrator);
        var selected = await orgs.SelectAsync(administrator!.UserId,
            administrator.SessionId, s.Fixture.A, CancellationToken.None);
        Assert.NotNull(selected.Value);
        var membership = await orgs.PutMemberAsync(administrator.UserId,
            administrator.SessionId, s.Fixture.A, grantedUser!.UserId,
            role, "Active", null, "m15-role-test", CancellationToken.None);
        Assert.NotNull(membership.Value);
        return (email, grantedUser.UserId);
    }

    private static async Task<HttpClient> Client(
        M11ExceptionRuntimeTests.Setup s, string email)
    {
        var client = s.Runtime.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(client, email);
        await ReferenceDataHttpTests.Select(client, s.Fixture.A);
        return client;
    }

    [Fact]
    public async Task FiveRolesFanOutOnlyEligibleRecipientsAndLateJoinersGetNoBackfill()
    {
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var factory = s.Factory;
        using var runtime = s.Runtime;
        using var manager = s.Manager;
        using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var originalAdmin = s.Fixture.Admin;
        using var originalManager = s.Fixture.Manager;
        using var originalAnalyst = s.Fixture.Analyst;
        var support = await CreateMember(s, "Support");
        var auditor = await CreateMember(s, "Auditor");
        using var supportClient = await Client(s, support.Email);
        using var auditorClient = await Client(s, auditor.Email);
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        await M15NotificationRuntimeTests.EnsureShipmentDelivered(s, shipment);
        var route = $"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var managerList = await manager.GetAsync(route);
        using var analystList = await analyst.GetAsync(route);
        using var auditorList = await auditorClient.GetAsync(route);
        using var adminList = await administrator.GetAsync(route);
        using var supportList = await supportClient.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, managerList.StatusCode);
        Assert.Equal(HttpStatusCode.OK, analystList.StatusCode);
        Assert.Equal(HttpStatusCode.OK, auditorList.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adminList.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, supportList.StatusCode);
        var a = await auditorList.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(a.GetProperty("items").EnumerateArray());

        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        await using (var recipients = new NpgsqlCommand("""
            SELECT m.role,count(*) FROM m15_user_notifications n
            JOIN organization_memberships m ON m.id=n.membership_id
            WHERE n.organization_id=@org AND n.shipment_id=@shipment
            GROUP BY m.role ORDER BY m.role
            """, db))
        {
            recipients.Parameters.AddWithValue("org", s.Fixture.A);
            recipients.Parameters.AddWithValue("shipment", shipment);
            var seen = new Dictionary<string, long>();
            await using var rows = await recipients.ExecuteReaderAsync();
            while (await rows.ReadAsync()) seen.Add(rows.GetString(0), rows.GetInt64(1));
            Assert.Equal(3, seen.Count);
            Assert.Equal(1L, seen["Manager"]);
            Assert.Equal(1L, seen["Analyst"]);
            Assert.Equal(1L, seen["Auditor"]);
        }

        var newcomer = await CreateMember(s, "Analyst");
        using var late = await Client(s, newcomer.Email);
        using var empty = await late.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Empty((await empty.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray());
        Console.WriteLine("G15_ROLE_MATRIX eligible=3 administrator_denied=true " +
            "support_denied=true late_joiner_backfill=false");
    }

    [Fact]
    public async Task ShortLivedProtectedCursorExpiresAndNewEventsDoNotLeakIntoOldCutoff()
    {
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var factory = s.Factory;
        using var runtime = s.Runtime;
        using var manager = s.Manager;
        using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var originalAdmin = s.Fixture.Admin;
        using var originalManager = s.Fixture.Manager;
        using var originalAnalyst = s.Fixture.Analyst;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        var exceptionId = await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        for (var i = 0; i < 2; i++)
        {
            await using var history = new NpgsqlCommand("""
                INSERT INTO shipment_exception_history
                    (organization_id,exception_id,action,old_state,new_state,
                     actor_kind,source_fingerprint)
                SELECT organization_id,id,'Opened',NULL,'Open','System',
                       source_fingerprint
                FROM shipment_exceptions WHERE id=@exception
                """, db);
            history.Parameters.AddWithValue("exception", exceptionId);
            Assert.Equal(1, await history.ExecuteNonQueryAsync());
        }
        await using var scope = runtime.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<NotificationStore>();
        async Task<long> DeliveredForFixture()
        {
            await using var count = new NpgsqlCommand("""
                SELECT COUNT(DISTINCT source_history_id) FROM m15_user_notifications
                WHERE organization_id=@org AND shipment_id=@shipment
                """, db);
            count.Parameters.AddWithValue("org", s.Fixture.A);
            count.Parameters.AddWithValue("shipment", shipment);
            return Convert.ToInt64(await count.ExecuteScalarAsync());
        }
        for (var attempt = 0; attempt < 100 &&
             await DeliveredForFixture() < 3; attempt++)
            await store.DeliverPendingAsync(CancellationToken.None);
        Assert.Equal(3L, await DeliveredForFixture());

        var root = $"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var first = await manager.GetAsync(root + "?state=All&limit=1");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var body = await first.Content.ReadFromJsonAsync<JsonElement>();
        var cursor = body.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(cursor));
        var initialId = body.GetProperty("items")[0].GetProperty("id").GetGuid();

        var dataProtection = runtime.Services.GetRequiredService<IDataProtectionProvider>();
        var protector = dataProtection
            .CreateProtector("HarborOps.M15.PersonalNotificationCursor.v1")
            .ToTimeLimitedDataProtector();
        var original = protector.Unprotect(cursor!);
        var expired = protector.Protect(original, TimeSpan.FromMilliseconds(1));
        await Task.Delay(TimeSpan.FromSeconds(2));
        using var stale = await manager.GetAsync(root +
            "?state=All&limit=1&cursor=" + Uri.EscapeDataString(expired));
        Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);

        await using (var newest = new NpgsqlCommand("""
            INSERT INTO shipment_exception_history (
                organization_id,exception_id,action,old_state,new_state,
                actor_kind,source_fingerprint)
            SELECT organization_id,id,'Opened',NULL,'Open','System',source_fingerprint
            FROM shipment_exceptions WHERE id=@exception
            """, db))
        {
            newest.Parameters.AddWithValue("exception", exceptionId);
            Assert.Equal(1, await newest.ExecuteNonQueryAsync());
        }
        for (var attempt = 0; attempt < 100 &&
             await DeliveredForFixture() < 4; attempt++)
            await store.DeliverPendingAsync(CancellationToken.None);
        Assert.Equal(4L, await DeliveredForFixture());

        var beforeCutoff = new HashSet<Guid> { initialId };
        string? next = cursor;
        for (var page = 0; page < 3 && next is not null; page++)
        {
            using var response = await manager.GetAsync(root +
                "?state=All&limit=1&cursor=" + Uri.EscapeDataString(next));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var parsed = await response.Content.ReadFromJsonAsync<JsonElement>();
            foreach (var row in parsed.GetProperty("items").EnumerateArray())
                Assert.True(beforeCutoff.Add(row.GetProperty("id").GetGuid()));
            var token = parsed.GetProperty("nextCursor");
            next = token.ValueKind == JsonValueKind.String ? token.GetString() : null;
        }
        Assert.Equal(3, beforeCutoff.Count);
        using var fresh = await manager.GetAsync(root + "?state=All&limit=1");
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        var firstNew = await fresh.Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(firstNew.GetProperty("items")[0].GetProperty("id").GetGuid(),
            beforeCutoff);
        Console.WriteLine("G15_CURSOR expired_rejected=true new_event_excluded_from_old_cutoff=true");
    }
}
