using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

[Collection("M13ExclusiveServer")]
public sealed class G15HttpOutageAcceptanceTests
{
    [ExclusiveM13PostgresFact]
    public async Task RealNotificationDatabaseLockReturnsBoundedScopedHttpFailureAndRecovers()
    {
        var setup = await M11ExceptionRuntimeTests.Prepare();
        using var factory=setup.Factory; using var runtime=setup.Runtime;
        using var manager=setup.Manager; using var analyst=setup.Analyst;
        using var administrator=setup.Administrator;
        using var originalAdmin=setup.Fixture.Admin;
        using var originalManager=setup.Fixture.Manager;
        using var originalAnalyst=setup.Fixture.Analyst;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(setup);
        await M11ExceptionRuntimeTests.EvaluateAndFind(setup, shipment);
        await using (var scope=setup.Runtime.Services.CreateAsyncScope())
        {
            var store=scope.ServiceProvider.GetRequiredService<NotificationStore>();
            Assert.True(await store.DeliverPendingAsync(CancellationToken.None) >= 1);
        }
        var root=$"/api/v1/organizations/{setup.Fixture.A}/notifications";
        Guid notificationId;
        using (var baseline=await manager.GetAsync(root))
        {
            Assert.Equal(HttpStatusCode.OK,baseline.StatusCode);
            var json=await baseline.Content.ReadFromJsonAsync<JsonElement>();
            Assert.NotEmpty(json.GetProperty("items").EnumerateArray());
            notificationId=json.GetProperty("items")[0].GetProperty("id").GetGuid();
        }

        await using var db=new NpgsqlConnection(setup.Roles.Admin);
        await db.OpenAsync();
        await using (var hold=await db.BeginTransactionAsync())
        {
            await using (var lockTable=new NpgsqlCommand(
                "LOCK TABLE m15_user_notifications IN ACCESS EXCLUSIVE MODE",db,hold))
                await lockTable.ExecuteNonQueryAsync();

            var clock=Stopwatch.StartNew();
            using var blocked=await manager.GetAsync(root);
            clock.Stop();
            Assert.Equal(HttpStatusCode.ServiceUnavailable,blocked.StatusCode);
            Assert.InRange(clock.Elapsed.TotalSeconds,15,40);
            Assert.True(blocked.Headers.CacheControl?.NoStore);
            var failed=await blocked.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Contains(failed.GetProperty("code").GetString(),
                new[] { "dependency_unavailable","notification_authority_unavailable" });
            Assert.False(failed.TryGetProperty("items",out _));
            Assert.False(failed.TryGetProperty("unreadCount",out _));

            // Count, detail and the personal action fail the same bounded, closed
            // way: no store-controlled content, no change to the receipt.
            string[] outage = { "dependency_unavailable","notification_authority_unavailable",
                "notification_unavailable","audit_unavailable" };
            foreach (var path in new[] { root + "/unread-count", root + "/" + notificationId })
            {
                var timer=Stopwatch.StartNew();
                using var other=await manager.GetAsync(path);
                timer.Stop();
                Assert.Equal(HttpStatusCode.ServiceUnavailable,other.StatusCode);
                Assert.InRange(timer.Elapsed.TotalSeconds,15,40);
                Assert.True(other.Headers.CacheControl?.NoStore);
                var body=await other.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Contains(body.GetProperty("code").GetString(),outage);
                foreach (var leak in new[] { "unreadCount","items","id","shipmentId","version" })
                    Assert.False(body.TryGetProperty(leak,out _));
            }
            var actionTimer=Stopwatch.StartNew();
            using (var action=await M11ExceptionRuntimeTests.SendAction(manager,
                root+"/"+notificationId,new { action="Acknowledge",expectedVersion=1L },Guid.NewGuid()))
            {
                actionTimer.Stop();
                Assert.Equal(HttpStatusCode.ServiceUnavailable,action.StatusCode);
                Assert.InRange(actionTimer.Elapsed.TotalSeconds,15,40);
                var body=await action.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Contains(body.GetProperty("code").GetString(),outage);
                Assert.False(body.TryGetProperty("version",out _));
            }

            // A foreign organization cannot become accessible because of an
            // outage, error handler, or pending query from this session.
            using var foreign=await manager.GetAsync(
                $"/api/v1/organizations/{setup.Fixture.B}/notifications");
            Assert.Equal(HttpStatusCode.NotFound,foreign.StatusCode);
            await hold.RollbackAsync();
        }
        using var recovered=await manager.GetAsync(root);
        Assert.Equal(HttpStatusCode.OK,recovered.StatusCode);
        var data=await recovered.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEmpty(data.GetProperty("items").EnumerateArray());
        // Nothing was changed by the failed action, and the same version still works.
        using (var count=await manager.GetAsync(root+"/unread-count"))
            Assert.Equal(HttpStatusCode.OK,count.StatusCode);
        using (var detail=await manager.GetAsync(root+"/"+notificationId))
        {
            Assert.Equal(HttpStatusCode.OK,detail.StatusCode);
            var state=await detail.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1,state.GetProperty("version").GetInt64());
            Assert.Equal(JsonValueKind.Null,state.GetProperty("acknowledgedAtUtc").ValueKind);
        }
        using (var retry=await M11ExceptionRuntimeTests.SendAction(manager,
            root+"/"+notificationId,new { action="Acknowledge",expectedVersion=1L },Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.OK,retry.StatusCode);
    }
}
