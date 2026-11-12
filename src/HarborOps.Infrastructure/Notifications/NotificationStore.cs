using System.Data;
using System.Security.Cryptography;
using System.Text;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace HarborOps.Infrastructure.Notifications;

public sealed record NotificationRecord(
    Guid Id, Guid ShipmentId, Guid ExceptionId, string EventType, string RuleCode,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? ReadAtUtc,
    DateTimeOffset? AcknowledgedAtUtc, long Version);

public sealed record NotificationPage(
    IReadOnlyList<NotificationRecord> Items, DateTimeOffset SnapshotAtUtc,
    DateTimeOffset? NextAfterUtc, Guid? NextAfterId);

public sealed record NotificationMutation(string Code, long Version,
    DateTimeOffset? ReadAtUtc, DateTimeOffset? AcknowledgedAtUtc);

public static class NotificationText
{
    public static string Title(string rule) => rule switch
    {
        "EquipmentConflict" => "Conflicting recorded equipment observations",
        "ArrivalScheduleOverdue" => "Planned arrival overdue; physical arrival unconfirmed",
        "RecordedArrivalLate" => "Late arrival recorded in the manual workflow",
        "DepartureSlippageRisk" => "Recorded workflow departure suggests a schedule risk",
        _ => "Shipment exception"
    };

    public static string Label(string type) => type switch
    {
        "ExceptionOpened" => "Opened",
        "ExceptionResolved" => "Reviewed and resolved",
        "ExceptionAutoResolved" => "Closed by evaluation",
        _ => "Updated"
    };
}

public sealed class NotificationStore(
    HarborOpsDbContext database, IConfiguration configuration)
{
    private NpgsqlConnection Connection =>
        (NpgsqlConnection)database.Database.GetDbConnection();

    private static NpgsqlCommand Command(NpgsqlConnection connection,
        NpgsqlTransaction? tx, string sql, params (string Name, object? Value)[] args)
    {
        var command = new NpgsqlCommand(sql, connection, tx) { CommandTimeout = 25 };
        foreach (var (name, value) in args)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    private string Sign(string message)
    {
        var encoded = configuration["M10CorrectionProof:Key"]
            ?? throw new InvalidOperationException("Notification read authority is unavailable");
        byte[] key;
        try { key = Convert.FromBase64String(encoded); }
        catch (FormatException error)
        {
            throw new InvalidOperationException("Notification read authority is invalid", error);
        }
        if (key.Length != 32)
            throw new InvalidOperationException("Notification read authority has invalid length");
        try
        {
            return Convert.ToHexString(HMACSHA256.HashData(
                key, Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private async Task SetReadScopeAsync(NpgsqlTransaction tx, Guid org,
        Guid user, Guid session, CancellationToken ct)
    {
        var expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30;
        var scope = $"{org:D}|{user:D}|{session:D}|{expires}|"+
            Sign($"m11-read-v1|{org:N}|{user:N}|{session:N}|{expires}");
        await using var command = Command(Connection, tx,
            "SELECT set_config('harborops.m11_read_scope',@scope,true)",
            ("scope", scope));
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task SetDispatchScopeAsync(NpgsqlTransaction tx,CancellationToken ct)
    {
        var expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30;
        var scope = $"{expires}|"+Sign($"m15-dispatch-v1|{expires}");
        await using var cmd = Command(Connection,tx,
            "SELECT set_config('harborops.m15_dispatch_scope',@scope,true)",("scope",scope));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static NotificationRecord Read(NpgsqlDataReader r) => new(
        r.GetGuid(0), r.GetGuid(1), r.GetGuid(2),
        r.GetString(3), r.GetString(4), r.GetFieldValue<DateTimeOffset>(5),
        r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6),
        r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7),
        r.GetInt64(8));

    private const string Columns = """
        id,shipment_id,exception_id,event_type,rule_code,created_at_utc,
        read_at_utc,acknowledged_at_utc,version
        """;

    public async Task<NotificationPage> ListAsync(Guid org, Guid user, Guid session,
        string state, DateTimeOffset? afterUtc, Guid? afterId, DateTimeOffset? cutoff,
        int limit, CancellationToken ct)
    {
        await Connection.OpenAsync(ct);
        try
        {
            await using var tx = (NpgsqlTransaction)await Connection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);
            await SetReadScopeAsync(tx, org, user, session, ct);
            DateTimeOffset snapshot;
            await using (var clock = Command(Connection, tx, "SELECT clock_timestamp()"))
            await using (var clockReader = await clock.ExecuteReaderAsync(ct))
            {
                if (!await clockReader.ReadAsync(ct))
                    throw new InvalidOperationException("Database clock unavailable");
                snapshot = cutoff ?? clockReader.GetFieldValue<DateTimeOffset>(0);
            }

            var rows = new List<NotificationRecord>(limit + 1);
            // Use a fixed filter chosen from the recognized state values.
            // A parameterized OR across every state suppresses the partial
            // unread index during stale-statistics and bulk-load windows.
            var statePredicate = state switch
            {
                "All" => "",
                "Unread" => "AND read_at_utc IS NULL",
                "Read" => "AND read_at_utc IS NOT NULL AND acknowledged_at_utc IS NULL",
                "Acknowledged" => "AND acknowledged_at_utc IS NOT NULL",
                _ => "AND FALSE"
            };
            await using (var cmd = Command(Connection, tx, $"""
                SELECT {Columns} FROM m15_user_notifications
                WHERE organization_id=@org AND recipient_user_id=@user
                  AND created_at_utc <= @cutoff
                  AND (@afterUtc::timestamptz IS NULL OR
                       (created_at_utc,id)<(@afterUtc::timestamptz,@afterId::uuid))
                  {statePredicate}
                ORDER BY created_at_utc DESC,id DESC LIMIT @take
                """,
                ("org",org),("user",user),("cutoff",snapshot),
                ("afterUtc",(object?)afterUtc ?? DBNull.Value),
                ("afterId",(object?)afterId ?? DBNull.Value),
                ("take",limit+1)))
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct)) rows.Add(Read(reader));
            await tx.CommitAsync(ct);
            var hasMore = rows.Count > limit;
            if (hasMore) rows.RemoveAt(rows.Count - 1);
            var last = hasMore ? rows[^1] : null;
            return new NotificationPage(rows, snapshot,
                last?.CreatedAtUtc, last?.Id);
        }
        finally { await Connection.CloseAsync(); }
    }

    public async Task<NotificationRecord?> GetAsync(Guid org, Guid user, Guid session,
        Guid notificationId, CancellationToken ct)
    {
        await Connection.OpenAsync(ct);
        try
        {
            await using var tx = (NpgsqlTransaction)await Connection.BeginTransactionAsync(ct);
            await SetReadScopeAsync(tx, org, user, session, ct);
            NotificationRecord? item = null;
            await using (var cmd = Command(Connection, tx, $"""
                SELECT {Columns} FROM m15_user_notifications
                WHERE id=@id AND organization_id=@org AND recipient_user_id=@user
                """, ("id",notificationId),("org",org),("user",user)))
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
                if (await reader.ReadAsync(ct)) item = Read(reader);
            await tx.CommitAsync(ct);
            return item;
        }
        finally { await Connection.CloseAsync(); }
    }

    // The per-row read policy makes an exact count over a very large unread
    // backlog cost one policy evaluation per row, which exceeded the request
    // envelope at one million rows. The count is therefore exact up to
    // UnreadCountCap and reports a capped flag beyond it.
    public const int UnreadCountCap = 1000;

    public async Task<(long Count, bool Capped, DateTimeOffset AsOfUtc)> UnreadAsync(
        Guid org, Guid user, Guid session, CancellationToken ct)
    {
        await Connection.OpenAsync(ct);
        try
        {
            await using var tx = (NpgsqlTransaction)await Connection.BeginTransactionAsync(ct);
            await SetReadScopeAsync(tx, org, user, session, ct);
            long count;
            DateTimeOffset now;
            await using (var cmd = Command(Connection, tx, """
                SELECT COUNT(*),clock_timestamp() FROM (
                    SELECT 1 FROM m15_user_notifications
                    WHERE organization_id=@org AND recipient_user_id=@user
                        AND read_at_utc IS NULL
                    LIMIT @cap) bounded
                """, ("org",org),("user",user),("cap",UnreadCountCap + 1)))
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct))
                    throw new InvalidOperationException("Unread count unavailable");
                count = reader.GetInt64(0);
                now = reader.GetFieldValue<DateTimeOffset>(1);
            }
            await tx.CommitAsync(ct);
            return (Math.Min(count, UnreadCountCap), count > UnreadCountCap, now);
        }
        finally { await Connection.CloseAsync(); }
    }

    public async Task<NotificationMutation> UpdateAsync(Guid org, Guid user,
        Guid session, Guid notification, string action, long expectedVersion,
        Guid replayKey, string correlation, CancellationToken ct)
    {
        await Connection.OpenAsync(ct);
        try
        {
            await using var tx = (NpgsqlTransaction)await Connection.BeginTransactionAsync(ct);
            await SetReadScopeAsync(tx, org, user, session, ct);
            NotificationMutation result;
            await using (var cmd = Command(Connection, tx, """
                SELECT result_code,result_version,result_read_at_utc,
                    result_acknowledged_at_utc FROM m15_apply_personal_action(
                    @org::uuid,@user::uuid,@session::uuid,@notification::uuid,
                    @action::varchar,@version::bigint,@replay::uuid,@correlation::varchar)
                """,("org",org),("user",user),("session",session),
                ("notification",notification),("action",action),
                ("version",expectedVersion),("replay",replayKey),
                ("correlation",correlation)))
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct))
                    throw new InvalidOperationException("Notification authority unavailable");
                result = new NotificationMutation(reader.GetString(0),reader.GetInt64(1),
                    reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
                    reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3));
            }
            await tx.CommitAsync(ct);
            return result;
        }
        catch (PostgresException error) when (error.SqlState is "23505" or "40001" or "40P01")
        {
            // Different-target use of the same replay key or a serialization
            // conflict cannot turn into a successful second receipt mutation.
            return new NotificationMutation("Conflict",0,null,null);
        }
        finally { await Connection.CloseAsync(); }
    }

    // Each intent is delivered by its own database statement with its own time
    // limit. A PostgreSQL statement timeout is one shot for the whole statement, so
    // handing a batch to one call would bound only the first slow intent and leave
    // the rest to the client deadline. The limit is applied in application code, not
    // by a database role setting, so it holds in every deployment.
    public const int DeliveryBatchSize = 10;

    private int EventTimeoutSeconds => Math.Clamp(
        int.TryParse(configuration["M15:DeliveryEventTimeoutSeconds"], out var configured)
            ? configured : 15, 1, 60);

    public async Task<int> DeliverPendingAsync(CancellationToken ct)
    {
        // The poll continues past an intent that failed or timed out (it was claimed
        // and scheduled for a retry) and ends when a call finds nothing to claim.
        var delivered = 0;
        for (var i = 0; i < DeliveryBatchSize; i++)
        {
            var (one, claimed) = await DeliverOneAsync(ct);
            delivered += one;
            if (claimed == 0) break;
        }
        return delivered;
    }

    private async Task<(int Delivered, int Claimed)> DeliverOneAsync(CancellationToken ct)
    {
        await Connection.OpenAsync(ct);
        try
        {
            // A repeatable-read snapshot keeps audience size and the committed
            // eligible recipient set consistent for the whole fanout of the intent.
            await using var tx = (NpgsqlTransaction)await Connection.BeginTransactionAsync(
                IsolationLevel.RepeatableRead,ct);
            await SetDispatchScopeAsync(tx,ct);
            await using (var limit = Command(Connection,tx,
                $"SET LOCAL statement_timeout = '{EventTimeoutSeconds}s'"))
                await limit.ExecuteNonQueryAsync(ct);
            int count, claimed;
            await using (var cmd = Command(Connection,tx,
                "SELECT m15_deliver_pending(@take::integer)",("take",1)))
            {
                cmd.CommandTimeout = EventTimeoutSeconds + 10;
                count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            }
            await using (var found = Command(Connection,tx,
                "SELECT COALESCE(NULLIF(current_setting('harborops.m15_claimed',true),'')::integer,0)"))
                claimed = Convert.ToInt32(await found.ExecuteScalarAsync(ct));
            await tx.CommitAsync(ct);
            return (count, claimed);
        }
        catch (PostgresException error) when (error.SqlState is "40001" or "40P01")
        {
            // Concurrent SKIP LOCKED claims can lose a repeatable-read
            // serialization race. A future poll safely retries the intent.
            return (0, 0);
        }
        catch (PostgresException error) when (error.SqlState == "57014" && !ct.IsCancellationRequested)
        {
            // The time limit fired outside an intent's own retry handling. Nothing
            // was committed; the next poll claims the intent again.
            return (0, 0);
        }
        finally { await Connection.CloseAsync(); }
    }

    public async Task<(long Pending,long Deferred,long Quarantined)> DeliveryStatusAsync(
        CancellationToken ct)
    {
        await Connection.OpenAsync(ct);
        try
        {
            await using var tx = (NpgsqlTransaction)await Connection.BeginTransactionAsync(ct);
            await SetDispatchScopeAsync(tx,ct);
            (long Pending,long Deferred,long Quarantined) status;
            await using (var cmd = Command(Connection,tx,
                "SELECT pending,deferred,quarantined FROM m15_delivery_status()"))
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct))
                    throw new InvalidOperationException("Notification delivery status unavailable");
                status = (reader.GetInt64(0),reader.GetInt64(1),reader.GetInt64(2));
            }
            await tx.CommitAsync(ct);
            return status;
        }
        finally { await Connection.CloseAsync(); }
    }
}
