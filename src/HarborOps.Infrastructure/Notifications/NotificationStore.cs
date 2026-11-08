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

public sealed record NotificationMutation(string Code, long Version);

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

    private string ReadSignature(Guid organizationId, Guid userId, Guid sessionId,
        long expires)
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
            var message = $"m11-read-v1|{organizationId:N}|{userId:N}|{sessionId:N}|{expires}";
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
            ReadSignature(org, user, session, expires);
        await using var command = Command(Connection, tx,
            "SELECT set_config('harborops.m11_read_scope',@scope,true)",
            ("scope", scope));
        await command.ExecuteNonQueryAsync(ct);
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
            await using (var cmd = Command(Connection, tx, $"""
                SELECT {Columns} FROM m15_user_notifications
                WHERE organization_id=@org AND recipient_user_id=@user
                  AND created_at_utc <= @cutoff
                  AND (@afterUtc::timestamptz IS NULL OR
                       (created_at_utc,id)<(@afterUtc::timestamptz,@afterId::uuid))
                  AND (@state='All'
                       OR (@state='Unread' AND read_at_utc IS NULL)
                       OR (@state='Read' AND read_at_utc IS NOT NULL
                           AND acknowledged_at_utc IS NULL)
                       OR (@state='Acknowledged' AND acknowledged_at_utc IS NOT NULL))
                ORDER BY created_at_utc DESC,id DESC LIMIT @take
                """,
                ("org",org),("user",user),("cutoff",snapshot),
                ("afterUtc",(object?)afterUtc ?? DBNull.Value),
                ("afterId",(object?)afterId ?? DBNull.Value),
                ("state",state),("take",limit+1)))
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

    public async Task<(long Count, DateTimeOffset AsOfUtc)> UnreadAsync(
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
                SELECT COUNT(*),clock_timestamp() FROM m15_user_notifications
                WHERE organization_id=@org AND recipient_user_id=@user
                    AND read_at_utc IS NULL
                """, ("org",org),("user",user)))
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct))
                    throw new InvalidOperationException("Unread count unavailable");
                count = reader.GetInt64(0);
                now = reader.GetFieldValue<DateTimeOffset>(1);
            }
            await tx.CommitAsync(ct);
            return (count, now);
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
                SELECT result_code,result_version FROM m15_apply_personal_action(
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
                result = new NotificationMutation(reader.GetString(0),reader.GetInt64(1));
            }
            await tx.CommitAsync(ct);
            return result;
        }
        catch (PostgresException error) when (error.SqlState is "23505" or "40001" or "40P01")
        {
            // Different-target use of the same replay key or a serialization
            // conflict cannot turn into a successful second receipt mutation.
            return new NotificationMutation("Conflict",0);
        }
        finally { await Connection.CloseAsync(); }
    }

    public async Task<int> DeliverPendingAsync(CancellationToken ct)
    {
        await Connection.OpenAsync(ct);
        try
        {
            // A repeatable-read snapshot keeps audience size and the committed
            // eligible recipient set consistent for the entire fanout batch.
            await using var tx = (NpgsqlTransaction)await Connection.BeginTransactionAsync(
                IsolationLevel.RepeatableRead,ct);
            int count;
            await using (var cmd = Command(Connection,tx,
                "SELECT m15_deliver_pending(@take::integer)",("take",10)))
                count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            await tx.CommitAsync(ct);
            return count;
        }
        finally { await Connection.CloseAsync(); }
    }

    public async Task<(long Pending,long Deferred,long Quarantined)> DeliveryStatusAsync(
        CancellationToken ct)
    {
        await Connection.OpenAsync(ct);
        try
        {
            await using var cmd = Command(Connection,null,
                "SELECT pending,deferred,quarantined FROM m15_delivery_status()");
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                throw new InvalidOperationException("Notification delivery status unavailable");
            return (reader.GetInt64(0),reader.GetInt64(1),reader.GetInt64(2));
        }
        finally { await Connection.CloseAsync(); }
    }
}
