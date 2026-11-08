using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Infrastructure.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// All tests use a local disposable PostgreSQL database and real restricted API
// credentials. Historical M11 facts are never modified by M15 notification actions.
public sealed class M15NotificationBoundaryTests
{
    private static async Task<long> Count(NpgsqlConnection db,string sql,
        params (string Name,object Value)[] values)
    {
        await using var cmd = new NpgsqlCommand(sql,db);
        foreach (var (name,value) in values) cmd.Parameters.AddWithValue(name,value);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private static async Task<(M11ExceptionRuntimeTests.Setup Setup,Guid Shipment,Guid Exception)>
        OpenException()
    {
        var setup = await M11ExceptionRuntimeTests.Prepare();
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(setup);
        var exceptionId = await M11ExceptionRuntimeTests.EvaluateAndFind(setup,shipment);
        return (setup,shipment,exceptionId);
    }

    [Fact]
    public async Task OnlyImmutableM11OpeningAndClosureActionsGenerateOutboxRows()
    {
        var (s,shipment,exceptionId) = await OpenException();
        using var adminFactory=s.Factory; using var runtime=s.Runtime;
        using var manager=s.Manager; using var analyst=s.Analyst; using var admin=s.Administrator;
        using var originalAdmin=s.Fixture.Admin; using var originalManager=s.Fixture.Manager;
        using var originalAnalyst=s.Fixture.Analyst;
        await using var sql = new NpgsqlConnection(s.Roles.Admin);
        await sql.OpenAsync();
        const string count = """
            SELECT COUNT(*) FROM m15_notification_outbox
            WHERE organization_id=@org AND shipment_id=@shipment
            """;
        Assert.Equal(1,await Count(sql,count,("org",s.Fixture.A),("shipment",shipment)));
        foreach (var action in new[] { "Reevaluated","InputUnavailable" })
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO shipment_exception_history
                    (organization_id,exception_id,action,old_state,new_state,
                     actor_kind,source_fingerprint)
                SELECT e.organization_id,e.id,@action,'Open','Open','System',
                    e.source_fingerprint FROM shipment_exceptions e
                WHERE e.id=@id AND e.organization_id=@org
                """,sql);
            insert.Parameters.AddWithValue("action",action);
            insert.Parameters.AddWithValue("id",exceptionId);
            insert.Parameters.AddWithValue("org",s.Fixture.A);
            Assert.Equal(1,await insert.ExecuteNonQueryAsync());
        }
        Assert.Equal(1,await Count(sql,count,("org",s.Fixture.A),("shipment",shipment)));
    }

    [Fact]
    public async Task FailedOutboxInsertRollsBackNewImmutableHistoryInSameTransaction()
    {
        var (s,_,exceptionId) = await OpenException();
        using var adminFactory=s.Factory; using var runtime=s.Runtime;
        using var manager=s.Manager; using var analyst=s.Analyst; using var admin=s.Administrator;
        using var originalAdmin=s.Fixture.Admin; using var originalManager=s.Fixture.Manager;
        using var originalAnalyst=s.Fixture.Analyst;
        var newHistory=Guid.NewGuid();
        await using var sql=new NpgsqlConnection(s.Roles.Admin);
        await sql.OpenAsync();
        await using (var transaction=await sql.BeginTransactionAsync())
        {
            await using (var constraint=new NpgsqlCommand("""
                ALTER TABLE m15_notification_outbox ADD CONSTRAINT m15_test_fail_capture
                    CHECK (false) NOT VALID
                """,sql,transaction))
                await constraint.ExecuteNonQueryAsync();
            await using var insert=new NpgsqlCommand("""
                INSERT INTO shipment_exception_history
                    (id,organization_id,exception_id,action,old_state,new_state,
                     actor_kind,source_fingerprint)
                SELECT @history,e.organization_id,e.id,'Resolved','Open','Resolved',
                    'System',e.source_fingerprint FROM shipment_exceptions e
                WHERE e.id=@exception
                """,sql,transaction);
            insert.Parameters.AddWithValue("history",newHistory);
            insert.Parameters.AddWithValue("exception",exceptionId);
            var failed=await Assert.ThrowsAsync<PostgresException>(
                () => insert.ExecuteNonQueryAsync());
            Assert.Equal("23514",failed.SqlState);
            await transaction.RollbackAsync();
        }
        Assert.Equal(0,await Count(sql,
            "SELECT COUNT(*) FROM shipment_exception_history WHERE id=@history",
            ("history",newHistory)));
        Assert.Equal(0,await Count(sql,
            "SELECT COUNT(*) FROM m15_notification_outbox WHERE source_history_id=@history",
            ("history",newHistory)));
    }

    [Fact]
    public async Task ConcurrentDispatchAndMembershipVersionChangesNeverExposeStaleReceipts()
    {
        var (s,shipment,_) = await OpenException();
        using var adminFactory=s.Factory; using var runtime=s.Runtime;
        using var manager=s.Manager; using var analyst=s.Analyst; using var admin=s.Administrator;
        using var originalAdmin=s.Fixture.Admin; using var originalManager=s.Fixture.Manager;
        using var originalAnalyst=s.Fixture.Analyst;
        await using var first=s.Runtime.Services.CreateAsyncScope();
        await using var second=s.Runtime.Services.CreateAsyncScope();
        var one=first.ServiceProvider.GetRequiredService<NotificationStore>();
        var two=second.ServiceProvider.GetRequiredService<NotificationStore>();
        var result=await Task.WhenAll(one.DeliverPendingAsync(CancellationToken.None),
            two.DeliverPendingAsync(CancellationToken.None));
        Assert.True(result.Sum() >= 1);
        await M15NotificationRuntimeTests.EnsureShipmentDelivered(s,shipment);
        var route=$"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var initial=await manager.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK,initial.StatusCode);
        var data=await initial.Content.ReadFromJsonAsync<JsonElement>();
        var notice=Assert.Single(data.GetProperty("items").EnumerateArray());
        var id=notice.GetProperty("id").GetGuid();
        using var otherTenant=await manager.GetAsync(
            $"/api/v1/organizations/{s.Fixture.B}/notifications/{id}");
        Assert.Equal(HttpStatusCode.NotFound,otherTenant.StatusCode);
        await using var sql=new NpgsqlConnection(s.Roles.Admin);
        await sql.OpenAsync();
        Assert.Equal(1,await Count(sql,"""
            SELECT COUNT(*) FROM m15_user_notifications
            WHERE id=@id AND shipment_id=@shipment
            """,("id",id),("shipment",shipment)));
        // The admin authority changes the membership version without changing
        // role/status. Existing delivered rows must become unavailable.
        await using (var change=new NpgsqlCommand("""
            UPDATE organization_memberships SET version=version+1,
                updated_at_utc=clock_timestamp()
            WHERE organization_id=@org AND user_id=
                (SELECT recipient_user_id FROM m15_user_notifications WHERE id=@id)
            """,sql))
        {
            change.Parameters.AddWithValue("org",s.Fixture.A);
            change.Parameters.AddWithValue("id",id);
            Assert.Equal(1,await change.ExecuteNonQueryAsync());
        }
        using var hidden=await manager.GetAsync(route+"/"+id);
        Assert.Equal(HttpStatusCode.NotFound,hidden.StatusCode);
        using var unread=await manager.GetAsync(route+"/unread-count");
        Assert.Equal(0,(await unread.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("unreadCount").GetInt64());
        using var mutation=await M11ExceptionRuntimeTests.SendAction(manager,
            route+"/"+id,new { action="Acknowledge",expectedVersion=1L },Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound,mutation.StatusCode);
    }


    [Fact]
    public async Task CursorPaginationIsStableAndRejectsCrossFilterAndTamperedTokens()
    {
        var (setup,shipment,exceptionId) = await OpenException();
        using var factory=setup.Factory; using var runtime=setup.Runtime;
        using var manager=setup.Manager; using var analyst=setup.Analyst;
        using var administrator=setup.Administrator;
        using var originalAdmin=setup.Fixture.Admin;
        using var originalManager=setup.Fixture.Manager;
        using var originalAnalyst=setup.Fixture.Analyst;
        await using var sql=new NpgsqlConnection(setup.Roles.Admin);
        await sql.OpenAsync();

        // Two additional immutable source-history UUIDs create distinct
        // delivery intents for a source-locked pagination fixture.
        for (var i=0; i<2; i++)
        {
            await using var insert=new NpgsqlCommand("""
                INSERT INTO shipment_exception_history
                    (organization_id,exception_id,action,old_state,new_state,
                     actor_kind,source_fingerprint)
                SELECT e.organization_id,e.id,'Opened',NULL,'Open','System',
                    e.source_fingerprint
                FROM shipment_exceptions e
                WHERE e.id=@exception AND e.organization_id=@org
                """,sql);
            insert.Parameters.AddWithValue("exception",exceptionId);
            insert.Parameters.AddWithValue("org",setup.Fixture.A);
            Assert.Equal(1,await insert.ExecuteNonQueryAsync());
        }
        const string count = """
            SELECT COUNT(*) FROM m15_user_notifications
            WHERE organization_id=@org AND shipment_id=@shipment
            """;
        await using var scope=setup.Runtime.Services.CreateAsyncScope();
        var store=scope.ServiceProvider.GetRequiredService<NotificationStore>();
        for (var attempt=0; attempt<100; attempt++)
        {
            await store.DeliverPendingAsync(CancellationToken.None);
            if (await Count(sql,count,("org",setup.Fixture.A),
                ("shipment",shipment))>=3) break;
        }
        Assert.Equal(3,await Count(sql,count,
            ("org",setup.Fixture.A),("shipment",shipment)));

        var root=$"/api/v1/organizations/{setup.Fixture.A}/notifications";
        using var first=await manager.GetAsync(root+"?limit=1&state=All");
        Assert.Equal(HttpStatusCode.OK,first.StatusCode);
        var firstData=await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(firstData.GetProperty("items").EnumerateArray());
        var id=firstData.GetProperty("items")[0].GetProperty("id").GetGuid();
        var cursor=firstData.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(cursor));

        var continuation=root+"?limit=1&state=All&cursor="+
            Uri.EscapeDataString(cursor!);
        using var second=await manager.GetAsync(continuation);
        Assert.Equal(HttpStatusCode.OK,second.StatusCode);
        var secondData=await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(secondData.GetProperty("items").EnumerateArray());
        Assert.NotEqual(id,secondData.GetProperty("items")[0].GetProperty("id").GetGuid());

        using var changedFilter=await manager.GetAsync(
            root+"?limit=1&state=Unread&cursor="+Uri.EscapeDataString(cursor!));
        Assert.Equal(HttpStatusCode.BadRequest,changedFilter.StatusCode);
        var last=cursor![^1];
        var altered=cursor[..^1]+(last=='A'?'B':'A');
        using var tampered=await manager.GetAsync(
            root+"?limit=1&state=All&cursor="+Uri.EscapeDataString(altered));
        Assert.Equal(HttpStatusCode.BadRequest,tampered.StatusCode);

        // Duplicate/unknown query parameters cannot bypass cursor scoping.
        using var duplicates=await manager.GetAsync(root+"?limit=1&limit=50");
        Assert.Equal(HttpStatusCode.BadRequest,duplicates.StatusCode);
        using var unknown=await manager.GetAsync(root+"?recipientId="+Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest,unknown.StatusCode);
    }

    [Fact]
    public async Task RequiredAuditFailureRollsBackReceiptAndReplay()
    {
        var (s,shipment,_) = await OpenException();
        using var adminFactory=s.Factory; using var runtime=s.Runtime;
        using var manager=s.Manager; using var analyst=s.Analyst; using var admin=s.Administrator;
        using var originalAdmin=s.Fixture.Admin; using var originalManager=s.Fixture.Manager;
        using var originalAnalyst=s.Fixture.Analyst;
        await M15NotificationRuntimeTests.EnsureShipmentDelivered(s,shipment);
        var root=$"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var page=await manager.GetAsync(root);
        var body=await page.Content.ReadFromJsonAsync<JsonElement>();
        var id=Assert.Single(body.GetProperty("items").EnumerateArray())
            .GetProperty("id").GetGuid();
        var request=root+"/"+id;
        var replay=Guid.NewGuid();
        await using var sql=new NpgsqlConnection(s.Roles.Admin);
        await sql.OpenAsync();
        try
        {
            await using (var fail=new NpgsqlCommand("""
                ALTER TABLE audit_entries ADD CONSTRAINT m15_test_fail_audit
                    CHECK(event_type<>'notification.receipt_acknowledged') NOT VALID
                """,sql))
                await fail.ExecuteNonQueryAsync();
            using var response=await M11ExceptionRuntimeTests.SendAction(manager,request,
                new { action="Acknowledge",expectedVersion=1L },replay);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
            var fault=await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("audit_unavailable",fault.GetProperty("code").GetString());
            using var unchanged=await manager.GetAsync(request);
            var state=await unchanged.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1,state.GetProperty("version").GetInt64());
            Assert.Equal(JsonValueKind.Null,state.GetProperty("readAtUtc").ValueKind);
            Assert.Equal(JsonValueKind.Null,state.GetProperty("acknowledgedAtUtc").ValueKind);
            Assert.Equal(0,await Count(sql,
                "SELECT COUNT(*) FROM m15_notification_replays WHERE idempotency_key=@replay",
                ("replay",replay)));
        }
        finally
        {
            await using var drop=new NpgsqlCommand("""
                ALTER TABLE audit_entries DROP CONSTRAINT IF EXISTS m15_test_fail_audit
                """,sql);
            await drop.ExecuteNonQueryAsync();
        }
        using var retry=await M11ExceptionRuntimeTests.SendAction(manager,request,
            new { action="Acknowledge",expectedVersion=1L },replay);
        Assert.Equal(HttpStatusCode.OK,retry.StatusCode);
        Assert.Equal(2,(await retry.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("version").GetInt64());
        using var invalid=await M11ExceptionRuntimeTests.SendAction(manager,request,
            new { action="Acknowledge",expectedVersion=2L },Guid.NewGuid());
        Assert.Equal(HttpStatusCode.UnprocessableEntity,invalid.StatusCode);
    }
}
