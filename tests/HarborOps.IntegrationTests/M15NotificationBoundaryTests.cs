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
            SELECT COUNT(DISTINCT source_history_id) FROM m15_user_notifications
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
        // Change a significant leading Base64Url character, not unused
        // trailing padding bits that could decode to identical bytes.
        var initial=cursor![0];
        var altered=(initial=='A'?'B':'A')+cursor[1..];
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

    [Fact]
    public async Task MoreThanOneHundredPersonalRowsPageOnceThroughLiveReceiptChange()
    {
        var (s, shipment, exceptionId) = await OpenException();
        using var adminFactory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var originalAdmin = s.Fixture.Admin;
        using var originalManager = s.Fixture.Manager;
        using var originalAnalyst = s.Fixture.Analyst;
        await using var sql = new NpgsqlConnection(s.Roles.Admin);
        await sql.OpenAsync();
        // The initial event is already committed. Add 104 unique source events
        // with the same permitted action, each captured by the real trigger.
        for (var i = 0; i < 104; i++)
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO shipment_exception_history
                  (organization_id,exception_id,action,old_state,new_state,
                   actor_kind,source_fingerprint)
                SELECT e.organization_id,e.id,'Opened',NULL,'Open','System',
                       e.source_fingerprint
                FROM shipment_exceptions e
                WHERE e.id=@exception AND e.organization_id=@org
                """, sql);
            insert.Parameters.AddWithValue("exception", exceptionId);
            insert.Parameters.AddWithValue("org", s.Fixture.A);
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());
        }

        await using var scope = s.Runtime.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<NotificationStore>();
        for (var attempt = 0; attempt < 40; attempt++)
        {
            await store.DeliverPendingAsync(CancellationToken.None);
            if (await Count(sql, """
                SELECT COUNT(DISTINCT source_history_id) FROM m15_user_notifications
                WHERE organization_id=@org AND shipment_id=@shipment
                """, ("org", s.Fixture.A), ("shipment", shipment)) == 105) break;
        }
        Assert.Equal(105, await Count(sql, """
            SELECT COUNT(DISTINCT source_history_id) FROM m15_user_notifications
            WHERE organization_id=@org AND shipment_id=@shipment
            """, ("org", s.Fixture.A), ("shipment", shipment)));

        var root = "/api/v1/organizations/" + s.Fixture.A + "/notifications";
        string? cursor = null;
        var seen = new HashSet<Guid>();
        var pages = 0;
        do
        {
            var url = root + "?limit=50&state=All";
            if (cursor is not null)
                url += "&cursor=" + Uri.EscapeDataString(cursor);
            using var response = await manager.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = await response.Content.ReadFromJsonAsync<JsonElement>();
            var items = page.GetProperty("items").EnumerateArray().ToArray();
            Assert.InRange(items.Length, 1, 50);
            foreach (var item in items)
                Assert.True(seen.Add(item.GetProperty("id").GetGuid()),
                    "Live keyset returned a duplicate receipt");
            pages++;
            if (pages == 1)
            {
                var id = items[0].GetProperty("id").GetGuid();
                using var changed = await M11ExceptionRuntimeTests.SendAction(
                    manager, root + "/" + id,
                    new { action = "MarkRead", expectedVersion = 1L }, Guid.NewGuid());
                Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            }
            var next = page.GetProperty("nextCursor");
            cursor = next.ValueKind == JsonValueKind.Null ? null : next.GetString();
            Assert.InRange(pages, 1, 4);
        } while (cursor is not null);
        Assert.Equal(3, pages);
        Assert.Equal(105, seen.Count);
        Console.WriteLine("G15_S17 pages=3 distinct_receipts=105 read_mutation_between_pages=true");
    }

    [Fact]
    public async Task CursorCannotCrossSessionsAndReplayCannotChangeItsOriginalMeaning()
    {
        var (s, shipment, exceptionId) = await OpenException();
        using var adminFactory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var originalAdmin = s.Fixture.Admin;
        using var originalManager = s.Fixture.Manager;
        using var originalAnalyst = s.Fixture.Analyst;
        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        for (var i = 0; i < 2; i++)
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO shipment_exception_history (
                    organization_id,exception_id,action,old_state,new_state,
                    actor_kind,source_fingerprint)
                SELECT organization_id,id,'Opened',NULL,'Open','System',source_fingerprint
                FROM shipment_exceptions WHERE id=@id
                """, db);
            insert.Parameters.AddWithValue("id", exceptionId);
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());
        }
        await using (var scope = runtime.Services.CreateAsyncScope())
        {
            var delivery = scope.ServiceProvider.GetRequiredService<NotificationStore>();
            for (var attempt = 0; attempt < 10; attempt++)
            {
                await delivery.DeliverPendingAsync(CancellationToken.None);
                if (await Count(db, """
                    SELECT COUNT(DISTINCT source_history_id) FROM m15_user_notifications
                    WHERE organization_id=@org AND shipment_id=@shipment
                    """, ("org", s.Fixture.A), ("shipment", shipment)) >= 3) break;
            }
        }
        Assert.Equal(3, await Count(db, """
            SELECT COUNT(DISTINCT source_history_id) FROM m15_user_notifications
            WHERE organization_id=@org AND shipment_id=@shipment
            """, ("org", s.Fixture.A), ("shipment", shipment)));

        using var separateSession = runtime.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            { HandleCookies = true });
        await ReferenceDataHttpTests.Login(separateSession, s.Fixture.ManagerEmail);
        await ReferenceDataHttpTests.Select(separateSession, s.Fixture.A);
        var root = $"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var first = await manager.GetAsync(root + "?limit=1&state=All");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var body = await first.Content.ReadFromJsonAsync<JsonElement>();
        var cursor = body.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrEmpty(cursor));
        var id = body.GetProperty("items")[0].GetProperty("id").GetGuid();

        using var invalidSessionCursor = await separateSession.GetAsync(
            root + "?limit=1&state=All&cursor=" + Uri.EscapeDataString(cursor!));
        Assert.Equal(HttpStatusCode.BadRequest, invalidSessionCursor.StatusCode);
        using var validOriginalCursor = await manager.GetAsync(
            root + "?limit=1&state=All&cursor=" + Uri.EscapeDataString(cursor!));
        Assert.Equal(HttpStatusCode.OK, validOriginalCursor.StatusCode);

        var replay = Guid.NewGuid();
        using var applied = await M11ExceptionRuntimeTests.SendAction(
            manager, root + "/" + id,
            new { action = "Acknowledge", expectedVersion = 1L }, replay);
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        var appliedReceipt = await applied.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2L, appliedReceipt.GetProperty("version").GetInt64());
        Assert.NotEqual(JsonValueKind.Null, appliedReceipt.GetProperty("readAtUtc").ValueKind);
        Assert.NotEqual(JsonValueKind.Null,
            appliedReceipt.GetProperty("acknowledgedAtUtc").ValueKind);
        using var changedPayload = await M11ExceptionRuntimeTests.SendAction(
            manager, root + "/" + id,
            new { action = "MarkRead", expectedVersion = 1L }, replay);
        Assert.Equal(HttpStatusCode.Conflict, changedPayload.StatusCode);
        using var changedSession = await M11ExceptionRuntimeTests.SendAction(
            separateSession, root + "/" + id,
            new { action = "Acknowledge", expectedVersion = 1L }, replay);
        Assert.Equal(HttpStatusCode.Conflict, changedSession.StatusCode);
        using var originalReplay = await M11ExceptionRuntimeTests.SendAction(
            manager, root + "/" + id,
            new { action = "Acknowledge", expectedVersion = 1L }, replay);
        Assert.Equal(HttpStatusCode.OK, originalReplay.StatusCode);
        Assert.Equal(2L,(await originalReplay.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("version").GetInt64());
        using var staleAttempt = await M11ExceptionRuntimeTests.SendAction(
            manager, root + "/" + id,
            new { action = "MarkRead", expectedVersion = 1L }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, staleAttempt.StatusCode);
        using var invalidTransition = await M11ExceptionRuntimeTests.SendAction(
            manager, root + "/" + id,
            new { action = "MarkRead", expectedVersion = 2L }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidTransition.StatusCode);

        Assert.Equal(1, await Count(db, """
            SELECT COUNT(*) FROM m15_notification_replays
            WHERE organization_id=@org AND idempotency_key=@key
            """, ("org", s.Fixture.A), ("key", replay)));
        Assert.Equal(1, await Count(db, """
            SELECT COUNT(*) FROM audit_entries
            WHERE organization_id=@org AND event_type='notification.receipt_acknowledged'
              AND resource_id=@id
            """, ("org", s.Fixture.A), ("id", id)));
    }

    [Fact]
    public async Task FailedFanoutIsRetriedWithoutPartialOrDuplicateRecipients()
    {
        var (s, shipment, _) = await OpenException();
        using var adminFactory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var originalAdmin = s.Fixture.Admin;
        using var originalManager = s.Fixture.Manager;
        using var originalAnalyst = s.Fixture.Analyst;
        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        await using var scope = runtime.Services.CreateAsyncScope();
        var delivery = scope.ServiceProvider.GetRequiredService<NotificationStore>();
        try
        {
            await using (var poison = new NpgsqlCommand("""
                ALTER TABLE m15_user_notifications
                ADD CONSTRAINT m15_test_reject_fanout CHECK (false) NOT VALID
                """, db))
                await poison.ExecuteNonQueryAsync();
            await delivery.DeliverPendingAsync(CancellationToken.None);
            Assert.Equal(0, await Count(db, """
                SELECT COUNT(*) FROM m15_user_notifications
                WHERE organization_id=@org AND shipment_id=@shipment
                """, ("org", s.Fixture.A), ("shipment", shipment)));
            Assert.Equal(1, await Count(db, """
                SELECT COUNT(*) FROM m15_notification_outbox
                WHERE organization_id=@org AND shipment_id=@shipment
                  AND attempt_count=1 AND completed_at_utc IS NULL
                    AND last_error_code='delivery_failure'
                """, ("org", s.Fixture.A), ("shipment", shipment)));
        }
        finally
        {
            await using var restore = new NpgsqlCommand("""
                ALTER TABLE m15_user_notifications
                DROP CONSTRAINT IF EXISTS m15_test_reject_fanout
                """, db);
            await restore.ExecuteNonQueryAsync();
        }
        await using (var due = new NpgsqlCommand("""
            UPDATE m15_notification_outbox
            SET next_attempt_at_utc=clock_timestamp()
            WHERE organization_id=@org AND shipment_id=@shipment
            """, db))
        {
            due.Parameters.AddWithValue("org", s.Fixture.A);
            due.Parameters.AddWithValue("shipment", shipment);
            Assert.Equal(1, await due.ExecuteNonQueryAsync());
        }
        await delivery.DeliverPendingAsync(CancellationToken.None);
        await delivery.DeliverPendingAsync(CancellationToken.None);
        var recipientCount = await Count(db, """
            SELECT COUNT(*) FROM m15_user_notifications
            WHERE organization_id=@org AND shipment_id=@shipment
            """, ("org", s.Fixture.A), ("shipment", shipment));
        Assert.True(recipientCount >= 2);
        Assert.Equal(recipientCount, await Count(db, """
            SELECT COUNT(DISTINCT recipient_user_id) FROM m15_user_notifications
            WHERE organization_id=@org AND shipment_id=@shipment
            """, ("org", s.Fixture.A), ("shipment", shipment)));
        Assert.Equal(1, await Count(db, """
            SELECT COUNT(*) FROM m15_notification_outbox
            WHERE organization_id=@org AND shipment_id=@shipment
              AND completed_at_utc IS NOT NULL AND attempt_count=1
            """, ("org", s.Fixture.A), ("shipment", shipment)));
    }

    [Fact]
    public async Task ExistingReceiptsDoNotReappearAfterMembershipRemovalAndRejoin()
    {
        var (s, shipment, _) = await OpenException();
        using var adminFactory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var originalAdmin = s.Fixture.Admin;
        using var originalManager = s.Fixture.Manager;
        using var originalAnalyst = s.Fixture.Analyst;
        await M15NotificationRuntimeTests.EnsureShipmentDelivered(s, shipment);
        var root = $"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var before = await manager.GetAsync(root);
        var receipt = Assert.Single((await before.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid();
        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        foreach (var status in new[] { "Suspended", "Active" })
        {
            await using var change = new NpgsqlCommand("""
                UPDATE organization_memberships
                SET status=@status,version=version+1,updated_at_utc=clock_timestamp()
                WHERE id=(SELECT membership_id FROM m15_user_notifications WHERE id=@id)
                """, db);
            change.Parameters.AddWithValue("status", status);
            change.Parameters.AddWithValue("id", receipt);
            Assert.Equal(1, await change.ExecuteNonQueryAsync());
        }
        using var hidden = await manager.GetAsync(root + "/" + receipt);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var list = await manager.GetAsync(root);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Empty((await list.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray());
        using var count = await manager.GetAsync(root + "/unread-count");
        Assert.Equal(0, (await count.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("unreadCount").GetInt64());
    }


    [Fact]
    public async Task CommittedPersonalActionCanBeRecoveredAfterResponseIsDiscarded()
    {
        var (s, shipment, _) = await OpenException();
        using var adminFactory = s.Factory; using var runtime = s.Runtime;
        using var manager = s.Manager; using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        using var originalAdmin = s.Fixture.Admin;
        using var originalManager = s.Fixture.Manager;
        using var originalAnalyst = s.Fixture.Analyst;
        await M15NotificationRuntimeTests.EnsureShipmentDelivered(s, shipment);
        var root = $"/api/v1/organizations/{s.Fixture.A}/notifications";
        using var page = await manager.GetAsync(root);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var body = await page.Content.ReadFromJsonAsync<JsonElement>();
        var id = Assert.Single(body.GetProperty("items").EnumerateArray())
            .GetProperty("id").GetGuid();

        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();
        Guid user;
        Guid session;
        await using (var active = new NpgsqlCommand("""
            SELECT a.user_id,a.id FROM auth_sessions a
            JOIN organization_memberships m ON m.user_id=a.user_id
                AND m.organization_id=a.selected_organization_id
            WHERE a.selected_organization_id=@org AND m.role='Manager'
                AND m.status='Active' AND a.revoked_at_utc IS NULL
                AND a.idle_expires_at_utc>clock_timestamp()
                AND a.absolute_expires_at_utc>clock_timestamp()
            ORDER BY a.last_seen_at_utc DESC,a.issued_at_utc DESC LIMIT 1
            """, db))
        {
            active.Parameters.AddWithValue("org", s.Fixture.A);
            await using var reader = await active.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            user = reader.GetGuid(0);
            session = reader.GetGuid(1);
        }

        var key = Guid.NewGuid();
        // The committed internal result is deliberately discarded. A later
        // HTTP retry must not attempt a second write or duplicate the audit.
        await using (var scope = runtime.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<NotificationStore>();
            var committed = await store.UpdateAsync(
                s.Fixture.A, user, session, id, "Acknowledge", 1L,
                key, Guid.NewGuid().ToString("N"), CancellationToken.None);
            Assert.Equal("Applied", committed.Code);
            Assert.Equal(2L, committed.Version);
        }

        using var recovered = await M11ExceptionRuntimeTests.SendAction(
            manager, root + "/" + id,
            new { action = "Acknowledge", expectedVersion = 1L }, key);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(2L, (await recovered.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("version").GetInt64());
        using var latest = await manager.GetAsync(root + "/" + id);
        Assert.Equal(2L, (await latest.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("version").GetInt64());

        await using var audit = new NpgsqlCommand("""
            SELECT COUNT(*) FROM audit_entries
            WHERE organization_id=@org
                AND event_type='notification.receipt_acknowledged'
                AND resource_id=@id
            """, db);
        audit.Parameters.AddWithValue("org", s.Fixture.A);
        audit.Parameters.AddWithValue("id", id);
        Assert.Equal(1L, Convert.ToInt64(await audit.ExecuteScalarAsync()));
        Console.WriteLine("G15_AFTER_COMMIT_REPLAY no_second_write=true audit_once=true");
    }

}