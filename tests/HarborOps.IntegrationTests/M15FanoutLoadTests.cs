using System.Diagnostics;
using HarborOps.Infrastructure.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// Runs only on a dedicated disposable PostgreSQL server, never on accepted data.
public sealed class G15FanoutFactAttribute : FactAttribute
{
    public G15FanoutFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("HARBOROPS_G15_EXCLUSIVE_FANOUT") != "1" ||
            Environment.GetEnvironmentVariable("HARBOROPS_G13_EXCLUSIVE_POSTGRES") != "1")
            Skip = "G15 fanout requires its own disposable PostgreSQL server";
    }
}

[Collection("M13ExclusiveServer")]
public sealed class M15FanoutLoadTests
{
    [G15FanoutFact]
    public async Task TwoDispatchersFanOutToTwoHundredFiftyAdditionalMembersAndServeOneHundredReaders()
    {
        const int additional = 250;
        const int readers = 100;
        const int databaseConcurrency = 32;
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var factory = s.Factory;
        using var runtime = s.Runtime;
        using var originalAdmin = s.Fixture.Admin;
        using var originalManager = s.Fixture.Manager;
        using var originalAnalyst = s.Fixture.Analyst;
        using var manager = s.Manager;
        using var analyst = s.Analyst;
        using var administrator = s.Administrator;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        var prefix = "g15-fanout-" + Guid.NewGuid().ToString("N")[..12] + "-";
        await using var db = new NpgsqlConnection(s.Roles.Admin);
        await db.OpenAsync();

        await using (var users = new NpgsqlCommand("""
            INSERT INTO users (id,email,normalized_email,display_name,status,
                security_version,created_at_utc)
            SELECT gen_random_uuid(),@prefix || g::text || '@example.test',
                @prefix || g::text || '@example.test',
                'Fanout fixture','Active',1,clock_timestamp()
            FROM generate_series(1,@take) AS g
            """, db))
        {
            users.Parameters.AddWithValue("prefix", prefix);
            users.Parameters.AddWithValue("take", additional);
            Assert.Equal(additional, await users.ExecuteNonQueryAsync());
        }
        await using (var members = new NpgsqlCommand("""
            INSERT INTO organization_memberships (
                id,organization_id,user_id,role,status,version,
                created_at_utc,updated_at_utc)
            SELECT gen_random_uuid(),@org,u.id,
                CASE WHEN row_number() OVER (ORDER BY u.id) % 3=0 THEN 'Auditor'
                     WHEN row_number() OVER (ORDER BY u.id) % 3=1 THEN 'Manager'
                     ELSE 'Analyst' END,
                'Active',1,clock_timestamp(),clock_timestamp()
            FROM users u WHERE normalized_email LIKE @pattern
            """, db))
        {
            members.Parameters.AddWithValue("org", s.Fixture.A);
            members.Parameters.AddWithValue("pattern", prefix + "%");
            Assert.Equal(additional, await members.ExecuteNonQueryAsync());
        }
        long expected;
        await using (var count = new NpgsqlCommand("""
            SELECT COUNT(*) FROM organization_memberships m
            JOIN users u ON u.id=m.user_id
            WHERE m.organization_id=@org AND m.status='Active' AND u.status='Active'
                AND m.role IN ('Analyst','Manager','Auditor')
            """, db))
        {
            count.Parameters.AddWithValue("org", s.Fixture.A);
            expected = Convert.ToInt64(await count.ExecuteScalarAsync());
            Assert.True(expected >= additional + 2);
            Assert.True(expected <= 1000);
        }

        var timer = Stopwatch.StartNew();
        await using (var oneScope = runtime.Services.CreateAsyncScope())
        await using (var twoScope = runtime.Services.CreateAsyncScope())
        {
            var one = oneScope.ServiceProvider.GetRequiredService<NotificationStore>();
            var two = twoScope.ServiceProvider.GetRequiredService<NotificationStore>();
            await Task.WhenAll(one.DeliverPendingAsync(CancellationToken.None),
                two.DeliverPendingAsync(CancellationToken.None));
        }
        await using (var outbox = new NpgsqlCommand("""
            SELECT COUNT(*) FROM m15_notification_outbox
            WHERE organization_id=@org AND shipment_id=@shipment
              AND completed_at_utc IS NOT NULL AND quarantined_at_utc IS NULL
            """, db))
        {
            outbox.Parameters.AddWithValue("org", s.Fixture.A);
            outbox.Parameters.AddWithValue("shipment", shipment);
            Assert.Equal(1L, Convert.ToInt64(await outbox.ExecuteScalarAsync()));
        }
        await using (var delivered = new NpgsqlCommand("""
            SELECT COUNT(*),COUNT(DISTINCT recipient_user_id)
            FROM m15_user_notifications WHERE organization_id=@org
                AND shipment_id=@shipment
            """, db))
        {
            delivered.Parameters.AddWithValue("org", s.Fixture.A);
            delivered.Parameters.AddWithValue("shipment", shipment);
            await using var result = await delivered.ExecuteReaderAsync();
            Assert.True(await result.ReadAsync());
            Assert.Equal(expected, result.GetInt64(0));
            Assert.Equal(expected, result.GetInt64(1));
        }
        var fanoutMs = timer.ElapsedMilliseconds;

        // The API creates the proof from a real selected manager session.
        // One hundred independently scheduled scoped calls exercise load
        // with bounded 32-connection admission on a normal PostgreSQL runner.
        // Queueing is part of the measured service behavior.
        Guid actor;
        Guid session;
        await using (var active = new NpgsqlCommand("""
            SELECT a.user_id,a.id FROM auth_sessions a
            JOIN organization_memberships m ON m.user_id=a.user_id
                AND m.organization_id=a.selected_organization_id
            WHERE a.selected_organization_id=@org AND m.role='Manager'
              AND m.status='Active' AND a.revoked_at_utc IS NULL
              AND a.idle_expires_at_utc>clock_timestamp()
              AND a.absolute_expires_at_utc>clock_timestamp()
            ORDER BY a.issued_at_utc DESC LIMIT 1
            """, db))
        {
            active.Parameters.AddWithValue("org", s.Fixture.A);
            await using var result = await active.ExecuteReaderAsync();
            Assert.True(await result.ReadAsync());
            actor = result.GetGuid(0);
            session = result.GetGuid(1);
        }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var readClock = Stopwatch.StartNew();
        using var admission = new SemaphoreSlim(databaseConcurrency);
        var outcomes = await Task.WhenAll(Enumerable.Range(0, readers).Select(async _ =>
        {
            await admission.WaitAsync(deadline.Token);
            try
            {
            await using var scope = runtime.Services.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<NotificationStore>();
            var page = await store.ListAsync(s.Fixture.A, actor, session,
                "All", null, null, null, 1, deadline.Token);
            return Assert.Single(page.Items).Id;
            }
            finally { admission.Release(); }
        }));
        readClock.Stop();
        Assert.Equal(readers, outcomes.Length);
        Assert.Single(outcomes.Distinct());
        Console.WriteLine(
            $"G15_FANOUT additional={additional} total={expected} " +
            $"dispatchers=2 fanout_ms={fanoutMs} readers={readers} " +
            $"max_db_readers={databaseConcurrency} " +
            $"read_ms={readClock.ElapsedMilliseconds} exact_once=true");
    }
}
