using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Exceptions;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using HarborOps.Infrastructure.Shipments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace HarborOps.IntegrationTests;

// Residual G11 evidence: S08 witness permutations, S14/S18/S19 live-authority and
// atomicity, S20/S21 recovery. These tests change global PostgreSQL role attributes
// in one case, so they join the serialized role-isolation collection and require
// an exclusive disposable PostgreSQL server.
[Collection("M12ServerRoleIsolation")]
public sealed class M11ResidualRuntimeTests(ITestOutputHelper output)
{
    private void Note(string line)
    {
        output.WriteLine(line);
        var directory = Environment.GetEnvironmentVariable("HARBOROPS_G11_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        File.AppendAllText(Path.Combine(directory, "residual-notes.log"),
            $"{DateTimeOffset.UtcNow:O} {line}{Environment.NewLine}");
    }

    private static void RequireExclusive()
    {
        if (Environment.GetEnvironmentVariable("HARBOROPS_G12_EXCLUSIVE_POSTGRES") != "1")
            throw new InvalidOperationException(
                "These cases require HARBOROPS_G12_EXCLUSIVE_POSTGRES=1 on a disposable exclusive PostgreSQL server");
    }

    private static async Task<T> Scalar<T>(string connection, string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand(sql, db);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default! : (T)Convert.ChangeType(result, typeof(T));
    }

    private static async Task Exec(string connection, string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand(sql, db);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task WaitForApiLockWaiter(string connection)
    {
        await using var monitor = new NpgsqlConnection(connection);
        await monitor.OpenAsync();
        for (var attempt = 0; attempt < 400; attempt++)
        {
            await using var query = new NpgsqlCommand("""
                SELECT count(*) FROM pg_stat_activity
                WHERE datname = current_database() AND pid <> pg_backend_pid()
                  AND usename = 'harborops_api' AND wait_event_type = 'Lock'
                """, monitor);
            if (Convert.ToInt32(await query.ExecuteScalarAsync()) >= 1) return;
            await Task.Delay(25);
        }
        Assert.Fail("Expected a runtime request blocked on a database lock");
    }

    private sealed record Snapshot(long Version, string State, long History, long Replays,
        long Audit, long Nonces);

    private static async Task<Snapshot> Take(string admin, Guid exception, Guid shipment)
    {
        var version = await Scalar<long>(admin,
            "SELECT version FROM shipment_exceptions WHERE id=@e", ("e", exception));
        var state = await Scalar<string>(admin,
            "SELECT lifecycle_state FROM shipment_exceptions WHERE id=@e", ("e", exception));
        return new Snapshot(version, state,
            await Scalar<long>(admin,
                "SELECT count(*) FROM shipment_exception_history WHERE exception_id=@e", ("e", exception)),
            await Scalar<long>(admin,
                "SELECT count(*) FROM shipment_exception_replays WHERE exception_id=@e", ("e", exception)),
            await Scalar<long>(admin,
                "SELECT count(*) FROM audit_entries WHERE resource_id=@e AND event_type LIKE 'shipment.exception_%'",
                ("e", exception)),
            await Scalar<long>(admin,
                "SELECT count(*) FROM shipment_exception_proof_nonces WHERE shipment_id=@s AND operation='Disposition'",
                ("s", shipment)));
    }

    private static async Task Drain(TrackingWorkStore worker, Guid org)
    {
        for (var i = 0; i < 500; i++)
        {
            var claim = await worker.ClaimOneAsync(org, CancellationToken.None);
            if (claim is null) return;
            Assert.Equal(TrackingWorkDisposition.Processed,
                await worker.FinalizeAsync(claim, CancellationToken.None));
        }
        Assert.Fail("Tracking work did not drain");
    }

    // ---------------------------------------------------------------- G11-S14 / S18

    [Theory]
    [InlineData("none")]
    [InlineData("session_revoked")]
    [InlineData("member_removed")]
    [InlineData("role_downgraded")]
    [InlineData("organization_suspended")]
    [InlineData("account_disabled")]
    [InlineData("security_version")]
    public async Task ManagerDispositionRevalidatesLiveAuthorityAtTheTransactionBoundary(string change)
    {
        RequireExclusive();
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var _ = s.Fixture.Admin; using var __ = s.Fixture.Manager; using var ___ = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst; using var administrator = s.Administrator;
        var admin = s.Roles.Admin;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        var exception = await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        var url = M11ExceptionRuntimeTests.Collection(s.Fixture.A) + "/" + exception;
        var managerId = await Scalar<Guid>(admin, "SELECT id FROM users WHERE email=@e",
            ("e", s.Fixture.ManagerEmail));
        var before = await Take(admin, exception, shipment);

        await using var blocker = new NpgsqlConnection(admin);
        await blocker.OpenAsync();
        await using var tx = await blocker.BeginTransactionAsync();
        await using (var acquire = new NpgsqlCommand(
            "SELECT id FROM organizations WHERE id=@id FOR UPDATE", blocker, tx))
        {
            acquire.Parameters.AddWithValue("id", s.Fixture.A);
            await acquire.ExecuteScalarAsync();
        }
        var committed = false;
        try
        {
            var action = M11ExceptionRuntimeTests.SendAction(manager, url,
                new { action = "Acknowledge", expectedVersion = 1L,
                    reason = "Live authority interleaving " + change }, Guid.NewGuid());
            await WaitForApiLockWaiter(admin);
            if (change == "organization_suspended")
            {
                await using var suspend = new NpgsqlCommand(
                    "UPDATE organizations SET status='Suspended' WHERE id=@id", blocker, tx);
                suspend.Parameters.AddWithValue("id", s.Fixture.A);
                Assert.Equal(1, await suspend.ExecuteNonQueryAsync());
            }
            else if (change != "none")
            {
                var sql = change switch
                {
                    "session_revoked" => "UPDATE auth_sessions SET revoked_at_utc=now() WHERE user_id=@user AND revoked_at_utc IS NULL",
                    "member_removed" => "UPDATE organization_memberships SET status='Removed', version=version+1, updated_at_utc=now() WHERE organization_id=@org AND user_id=@user",
                    "role_downgraded" => "UPDATE organization_memberships SET role='Analyst', version=version+1, updated_at_utc=now() WHERE organization_id=@org AND user_id=@user",
                    "account_disabled" => "UPDATE users SET status='Disabled', disabled_at_utc=now() WHERE id=@user",
                    _ => "UPDATE users SET security_version=security_version+1 WHERE id=@user"
                };
                await using var other = new NpgsqlConnection(admin);
                await other.OpenAsync();
                await using var revoke = new NpgsqlCommand(sql, other);
                revoke.Parameters.AddWithValue("org", s.Fixture.A);
                revoke.Parameters.AddWithValue("user", managerId);
                Assert.True(await revoke.ExecuteNonQueryAsync() >= 1);
            }
            await tx.CommitAsync();
            committed = true;
            using var response = await action;
            var after = await Take(admin, exception, shipment);
            if (change == "none")
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(before.Version + 1, after.Version);
                Assert.Equal("Acknowledged", after.State);
                Assert.Equal(before.History + 1, after.History);
                Assert.Equal(before.Replays + 1, after.Replays);
                Assert.Equal(before.Audit + 1, after.Audit);
            }
            else
            {
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                Assert.Equal(before, after);
            }
            Note($"S14/S18 change={change} status={(int)response.StatusCode} before={before} after={after}");
        }
        finally
        {
            if (!committed) await tx.RollbackAsync();
        }
    }

    // ---------------------------------------------------------------- G11-S18 / S19

    [Fact]
    public async Task ConcurrentDispositionsAndShipmentChangesNeverCommitPartialState()
    {
        RequireExclusive();
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var _ = s.Fixture.Admin; using var __ = s.Fixture.Manager; using var ___ = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst; using var administrator = s.Administrator;
        var admin = s.Roles.Admin;

        // Two simultaneous Manager acknowledgements with different replay keys: exactly one
        // transaction applies, the other is a version conflict, and exactly one audit exists.
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        var exception = await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        var url = M11ExceptionRuntimeTests.Collection(s.Fixture.A) + "/" + exception;
        var before = await Take(admin, exception, shipment);
        var results = await Task.WhenAll(
            M11ExceptionRuntimeTests.SendAction(manager, url, new { action = "Acknowledge",
                expectedVersion = 1L, reason = "Concurrent disposition one" }, Guid.NewGuid()),
            M11ExceptionRuntimeTests.SendAction(manager, url, new { action = "Acknowledge",
                expectedVersion = 1L, reason = "Concurrent disposition two" }, Guid.NewGuid()));
        var codes = results.Select(r => r.StatusCode).OrderBy(c => (int)c).ToArray();
        foreach (var r in results) r.Dispose();
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, codes);
        var afterRace = await Take(admin, exception, shipment);
        Assert.Equal(before.Version + 1, afterRace.Version);
        Assert.Equal(before.History + 1, afterRace.History);
        Assert.Equal(before.Audit + 1, afterRace.Audit);
        Assert.Equal(before.Replays + 1, afterRace.Replays);
        Note($"S18 concurrent dispositions codes={string.Join(",", codes.Select(c => (int)c))} before={before} after={afterRace}");

        // Archive and cancellation races. A blocker holds the shipment row so the
        // disposition and the shipment mutation queue together; either order must leave
        // a state in which history, audit and replay counts agree.
        foreach (var mutation in new[] { "archive", "cancel" })
        {
            var raceShipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
            var raceException = await M11ExceptionRuntimeTests.EvaluateAndFind(s, raceShipment);
            var raceUrl = M11ExceptionRuntimeTests.Collection(s.Fixture.A) + "/" + raceException;
            var shipmentUrl = $"/api/v1/organizations/{s.Fixture.A}/shipments/{raceShipment}";
            var baseline = await Take(admin, raceException, raceShipment);
            await using var blocker = new NpgsqlConnection(admin);
            await blocker.OpenAsync();
            await using var tx = await blocker.BeginTransactionAsync();
            await using (var hold = new NpgsqlCommand(
                "SELECT id FROM shipments WHERE organization_id=@o AND id=@s FOR UPDATE", blocker, tx))
            {
                hold.Parameters.AddWithValue("o", s.Fixture.A);
                hold.Parameters.AddWithValue("s", raceShipment);
                await hold.ExecuteScalarAsync();
            }
            var committed = false;
            try
            {
                var disposition = M11ExceptionRuntimeTests.SendAction(manager, raceUrl, new
                {
                    action = "Acknowledge", expectedVersion = 1L,
                    reason = "Disposition racing shipment " + mutation
                }, Guid.NewGuid());
                await WaitForApiLockWaiter(admin);
                var current = await Scalar<long>(admin,
                    "SELECT version FROM shipments WHERE id=@s", ("s", raceShipment));
                var mutate = ReferenceDataHttpTests.Send(s.Fixture.Manager, HttpMethod.Patch, shipmentUrl,
                    mutation == "archive"
                        ? new { expectedVersion = current, recordStatus = "Archived" }
                        : (object)new { expectedVersion = current, workflowStatus = "Cancelled" });
                await Task.Delay(300);
                await tx.CommitAsync();
                committed = true;
                using var dispositionResponse = await disposition;
                using var mutationResponse = await mutate;
                var after = await Take(admin, raceException, raceShipment);
                Note($"S18 {mutation} race: disposition={(int)dispositionResponse.StatusCode} mutation={(int)mutationResponse.StatusCode} before={baseline} after={after}");
                if (dispositionResponse.StatusCode == HttpStatusCode.OK)
                {
                    Assert.Equal("Acknowledged", after.State);
                    Assert.Equal(baseline.History + 1, after.History);
                    Assert.Equal(baseline.Audit + 1, after.Audit);
                    Assert.Equal(baseline.Replays + 1, after.Replays);
                }
                else
                {
                    Assert.Contains(dispositionResponse.StatusCode,
                        new[] { HttpStatusCode.Conflict, HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
                    Assert.Equal(baseline, after);
                }
            }
            finally
            {
                if (!committed) await tx.RollbackAsync();
            }
        }
    }

    [Fact]
    public async Task RejectedAuditWriteRollsBackDispositionCompletelyAndKeyRemainsReusable()
    {
        RequireExclusive();
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var _ = s.Fixture.Admin; using var __ = s.Fixture.Manager; using var ___ = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst; using var administrator = s.Administrator;
        var admin = s.Roles.Admin;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        var exception = await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        var url = M11ExceptionRuntimeTests.Collection(s.Fixture.A) + "/" + exception;
        var before = await Take(admin, exception, shipment);
        var key = Guid.NewGuid();
        var body = new { action = "Acknowledge", expectedVersion = 1L,
            reason = "Acknowledge with injected audit rejection" };

        await Exec(admin, """
            CREATE OR REPLACE FUNCTION public.m11_test_reject_audit() RETURNS trigger
            LANGUAGE plpgsql AS $t$ BEGIN
                RAISE EXCEPTION 'injected audit rejection' USING ERRCODE='XX001';
            END $t$
            """);
        await Exec(admin, $"""
            CREATE TRIGGER m11_test_reject_audit BEFORE INSERT ON audit_entries
            FOR EACH ROW WHEN (NEW.resource_id = '{exception}'::uuid
                AND NEW.event_type LIKE 'shipment.exception_%')
            EXECUTE FUNCTION public.m11_test_reject_audit()
            """);
        try
        {
            using var rejected = await M11ExceptionRuntimeTests.SendAction(manager, url, body, key);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, rejected.StatusCode);
            var error = await rejected.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("audit_unavailable", error.GetProperty("code").GetString());
            var after = await Take(admin, exception, shipment);
            Assert.Equal(before, after);
            Note($"S19 rejected audit: status=503 before={before} after={after}");
        }
        finally
        {
            await Exec(admin, "DROP TRIGGER IF EXISTS m11_test_reject_audit ON audit_entries");
            await Exec(admin, "DROP FUNCTION IF EXISTS public.m11_test_reject_audit()");
        }

        // The failed attempt consumed neither the replay key nor the proof nonce.
        using (var applied = await M11ExceptionRuntimeTests.SendAction(manager, url, body, key))
        {
            Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
            Assert.Equal("Applied", (await applied.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("result").GetString());
        }
        var finalState = await Take(admin, exception, shipment);
        Assert.Equal(before.Version + 1, finalState.Version);
        Assert.Equal(before.History + 1, finalState.History);
        Assert.Equal(before.Audit + 1, finalState.Audit);
        Assert.Equal(before.Replays + 1, finalState.Replays);
    }

    [Fact]
    public async Task FiveRoleMatrixAndWrongActorReplayAreRejectedWithoutStateChange()
    {
        RequireExclusive();
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var _ = s.Fixture.Admin; using var __ = s.Fixture.Manager; using var ___ = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst; using var administrator = s.Administrator;
        var admin = s.Roles.Admin;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        var exception = await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        var url = M11ExceptionRuntimeTests.Collection(s.Fixture.A) + "/" + exception;
        const string password = "M11-Synthetic-Role-Passphrase-123";
        var suffix = Guid.NewGuid().ToString("N");

        async Task<HttpClient> Extra(string role)
        {
            var email = $"m11-{role.ToLowerInvariant()}-{suffix}@example.test";
            Guid userId;
            await using (var scope = f.Services.CreateAsyncScope())
            {
                var identities = scope.ServiceProvider.GetRequiredService<HarborOps.Infrastructure.Identity.IdentityStore>();
                await identities.ProvisionAsync(email, "M11 " + role, password, CancellationToken.None);
                userId = (await identities.LoginAsync(email, password, "m11-roles", CancellationToken.None))!.UserId;
            }
            using var membership = await ReferenceDataHttpTests.Send(s.Fixture.Admin, HttpMethod.Put,
                $"/api/v1/organizations/{s.Fixture.A}/memberships/{userId}",
                new { role, status = "Active", expectedVersion = (long?)null });
            Assert.Equal(HttpStatusCode.OK, membership.StatusCode);
            var client = runtime.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
            using var login = await ReferenceDataHttpTests.Send(client, HttpMethod.Post, "/api/v1/auth/login",
                new { email, password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            await ReferenceDataHttpTests.Select(client, s.Fixture.A);
            return client;
        }

        using var auditor = await Extra("Auditor");
        using var support = await Extra("Support");
        using var secondManager = await Extra("Manager");
        var before = await Take(admin, exception, shipment);
        var body = new { action = "Acknowledge", expectedVersion = 1L, reason = "Role matrix disposition attempt" };

        // Reads: Manager, Analyst and Auditor may view; Administrator and Support may not.
        foreach (var (client, label, expected) in new[] {
            (manager, "Manager", HttpStatusCode.OK), (analyst, "Analyst", HttpStatusCode.OK),
            (auditor, "Auditor", HttpStatusCode.OK), (administrator, "Administrator", HttpStatusCode.Forbidden),
            (support, "Support", HttpStatusCode.Forbidden) })
        {
            using var read = await client.GetAsync(url);
            Assert.True(read.StatusCode == expected, $"{label} read expected {expected} got {read.StatusCode}");
        }
        // Writes: only Manager may disposition. Every other role is refused without change.
        foreach (var (client, label) in new[] { (analyst, "Analyst"), (auditor, "Auditor"),
            (administrator, "Administrator"), (support, "Support") })
        {
            using var denied = await M11ExceptionRuntimeTests.SendAction(client, url, body, Guid.NewGuid());
            Assert.True(denied.StatusCode == HttpStatusCode.Forbidden, $"{label} disposition got {denied.StatusCode}");
        }
        Assert.Equal(before, await Take(admin, exception, shipment));

        // Same key replayed by a different Manager (wrong actor) must be refused.
        var key = Guid.NewGuid();
        using (var first = await M11ExceptionRuntimeTests.SendAction(manager, url, body, key))
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var applied = await Take(admin, exception, shipment);
        using (var wrongActor = await M11ExceptionRuntimeTests.SendAction(secondManager, url, body, key))
            Assert.Equal(HttpStatusCode.Conflict, wrongActor.StatusCode);
        Assert.Equal(applied, await Take(admin, exception, shipment));
        // Same actor, same key, changed expected version is still a replay conflict.
        using (var changedBody = await M11ExceptionRuntimeTests.SendAction(manager, url,
            new { action = "Acknowledge", expectedVersion = 2L, reason = "Role matrix disposition attempt" }, key))
            Assert.Equal(HttpStatusCode.Conflict, changedBody.StatusCode);
        Assert.Equal(applied, await Take(admin, exception, shipment));
    }

    // ---------------------------------------------------------------- G11-S20 / S21

    [Fact]
    public async Task MissedCheckpointIsRecoveredAfterRestartAndDatabaseOutageWithoutDuplicates()
    {
        RequireExclusive();
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var f = s.Factory;
        using var _ = s.Fixture.Admin; using var __ = s.Fixture.Manager; using var ___ = s.Fixture.Analyst;
        var admin = s.Roles.Admin;

        // A shipment whose arrival boundary (planned arrival + 60 minutes grace) is still in the future.
        var seed = await AssignmentHttpTests.Seed(s.Fixture);
        var shipmentUrl = $"/api/v1/organizations/{s.Fixture.A}/shipments/{seed.Shipment}";
        var now = DateTimeOffset.UtcNow;
        using (var plan = await ReferenceDataHttpTests.Send(s.Fixture.Manager, HttpMethod.Patch, shipmentUrl,
            new { expectedVersion = 1L, plannedDepartureAtUtc = M11ExceptionRuntimeTests.Utc(now.AddHours(-1)),
                plannedArrivalAtUtc = M11ExceptionRuntimeTests.Utc(now.AddHours(2)) }))
            Assert.Equal(HttpStatusCode.OK, plan.StatusCode);
        using (var booked = await ReferenceDataHttpTests.Send(s.Fixture.Manager, HttpMethod.Patch, shipmentUrl,
            new { expectedVersion = 2L, workflowStatus = "Booked" }))
            Assert.Equal(HttpStatusCode.OK, booked.StatusCode);

        var options = new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(s.Roles.Api).Options;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["M10CorrectionProof:Key"] = ReferenceDataHttpTests.CorrectionProofKey }).Build();
        await using (var db = new HarborOpsDbContext(options))
        {
            var store = new ShipmentExceptionStore(db, configuration);
            Assert.Equal(M11StoreResult.Applied,
                (await store.EvaluateAsync(s.Fixture.A, seed.Shipment, CancellationToken.None)).Result);
        }
        Assert.Equal(0L, await Scalar<long>(admin,
            "SELECT count(*) FROM shipment_exceptions WHERE shipment_id=@s", ("s", seed.Shipment)));
        var checkpoint = await Scalar<DateTime>(admin, """
            SELECT next_evaluation_at_utc FROM shipment_exception_evaluations
            WHERE shipment_id=@s AND rule_code='ArrivalScheduleOverdue'
            """, ("s", seed.Shipment));
        Assert.True(checkpoint > DateTime.UtcNow.AddMinutes(30));
        Note($"S20 pre-state: no exception, next checkpoint {checkpoint:O}");

        async Task<bool> Due()
        {
            await using var db = new HarborOpsDbContext(options);
            var store = new ShipmentExceptionStore(db, configuration);
            Guid? org = null, ship = null;
            for (var page = 0; page < 400; page++)
            {
                var batch = await store.DueAsync(org, ship, 50, CancellationToken.None);
                if (batch.Scanned == 0) return false;
                if (batch.Items.Any(i => i.Shipment == seed.Shipment)) return true;
                org = batch.LastOrganization; ship = batch.LastShipment;
            }
            return false;
        }
        Assert.False(await Due());

        // Stop the API process (and its in-process evaluator) before the checkpoint is missed.
        s.Runtime.Dispose();
        s.Manager.Dispose(); s.Analyst.Dispose(); s.Administrator.Dispose();

        // Database outage for the runtime login while wall time "passes": the stored checkpoint
        // and the schedule are moved into the past without bumping the shipment version, so the
        // only due signal is elapsed time. The database clock cannot be advanced, so this is a
        // simulated clock advance and is recorded as such.
        await Exec(admin, "ALTER ROLE harborops_api NOLOGIN");
        var restored = false;
        DateTimeOffset restoredAt = default;
        try
        {
            await Exec(admin, "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE usename='harborops_api'");
            await using (var simulate = new NpgsqlConnection(admin))
            {
                await simulate.OpenAsync();
                await using var command = new NpgsqlCommand("""
                    SET session_replication_role = replica;
                    UPDATE shipments SET planned_arrival_at_utc = now() - interval '3 hours',
                        planned_departure_at_utc = now() - interval '2 days'
                    WHERE organization_id=@o AND id=@s;
                    UPDATE shipment_exception_evaluations SET next_evaluation_at_utc = now() - interval '2 minutes'
                    WHERE organization_id=@o AND shipment_id=@s AND rule_code='ArrivalScheduleOverdue';
                    """, simulate);
                command.Parameters.AddWithValue("o", s.Fixture.A);
                command.Parameters.AddWithValue("s", seed.Shipment);
                await command.ExecuteNonQueryAsync();
            }
            Note("S20 SIMULATED clock advance applied during outage (schedule and checkpoint moved 3 hours past)");
            await Task.Delay(TimeSpan.FromSeconds(4));
            Assert.Equal(0L, await Scalar<long>(admin,
                "SELECT count(*) FROM shipment_exceptions WHERE shipment_id=@s", ("s", seed.Shipment)));
        }
        finally
        {
            await Exec(admin, "ALTER ROLE harborops_api LOGIN");
            restored = true;
            restoredAt = DateTimeOffset.UtcNow;
        }
        Assert.True(restored);
        Assert.True(await Due());

        // Restart: a new API host constructs a fresh evaluator whose scan cursor starts at zero.
        using var restartedBase = new ReferenceDataHttpTests.ApiFactory(s.Roles.Api);
        // The evaluator is an opt-in hosted service (default off).
        using var restarted = restartedBase.WithWebHostBuilder(
            host => host.UseSetting("M11:EnableEvaluator", "true"));
        using var client = restarted.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(client, s.Fixture.ManagerEmail);
        await ReferenceDataHttpTests.Select(client, s.Fixture.A);
        long count = 0;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            count = await Scalar<long>(admin,
                "SELECT count(*) FROM shipment_exceptions WHERE shipment_id=@s AND rule_code='ArrivalScheduleOverdue'",
                ("s", seed.Shipment));
            if (count > 0) break;
            await Task.Delay(500);
        }
        Assert.Equal(1L, count);
        var openedAt = await Scalar<DateTime>(admin,
            "SELECT first_detected_at_utc FROM shipment_exceptions WHERE shipment_id=@s AND rule_code='ArrivalScheduleOverdue'",
            ("s", seed.Shipment));
        Assert.True(new DateTimeOffset(DateTime.SpecifyKind(openedAt, DateTimeKind.Utc)) >= restoredAt.AddSeconds(-1),
            "The exception must be opened by recovery after the outage, not before it");
        Note($"S20/S21 recovered: exception opened at {openedAt:O}, runtime login restored at {restoredAt:O}");

        // Idempotency after recovery: keep the evaluator scanning and confirm no duplicates.
        await Task.Delay(TimeSpan.FromSeconds(20));
        Assert.Equal(1L, await Scalar<long>(admin,
            "SELECT count(*) FROM shipment_exceptions WHERE shipment_id=@s AND rule_code='ArrivalScheduleOverdue'",
            ("s", seed.Shipment)));
        Assert.Equal(1L, await Scalar<long>(admin, """
            SELECT count(*) FROM shipment_exception_history h JOIN shipment_exceptions e ON e.id=h.exception_id
            WHERE e.shipment_id=@s AND e.rule_code='ArrivalScheduleOverdue' AND h.action='Opened'
            """, ("s", seed.Shipment)));
        Assert.Equal(4L, await Scalar<long>(admin,
            "SELECT count(*) FROM shipment_exception_evaluations WHERE shipment_id=@s", ("s", seed.Shipment)));
        Assert.False(await Due());
    }

    [Fact]
    public async Task EvaluatorKilledBeforeOrAfterCommitLeavesNoPartialStateAndRecoversExactlyOnce()
    {
        RequireExclusive();
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var _ = s.Fixture.Admin; using var __ = s.Fixture.Manager; using var ___ = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst; using var administrator = s.Administrator;
        var admin = s.Roles.Admin;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["M10CorrectionProof:Key"] = ReferenceDataHttpTests.CorrectionProofKey }).Build();
        int rolledBack = 0, committedThenLost = 0, completed = 0;
        for (var iteration = 0; iteration < 14; iteration++)
        {
            var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
            var appName = $"m11-chaos-{iteration}-{Guid.NewGuid():N}"[..40];
            var chaos = new NpgsqlConnectionStringBuilder(s.Roles.Api)
                { ApplicationName = appName, Pooling = false }.ConnectionString;
            await using var db = new HarborOpsDbContext(
                new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(chaos).Options);
            var store = new ShipmentExceptionStore(db, configuration);
            var evaluate = Task.Run(async () =>
            {
                try { return (await store.EvaluateAsync(s.Fixture.A, shipment, CancellationToken.None)).Result.ToString(); }
                catch (Exception error) { return "Exception:" + error.GetType().Name; }
            });
            // Sweep the kill instant across the transaction from immediately to well after commit.
            await Task.Delay(iteration * 12);
            await Exec(admin, "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE application_name=@n",
                ("n", appName));
            var outcome = await evaluate;

            var exceptions = await Scalar<long>(admin,
                "SELECT count(*) FROM shipment_exceptions WHERE shipment_id=@s", ("s", shipment));
            var evaluations = await Scalar<long>(admin,
                "SELECT count(*) FROM shipment_exception_evaluations WHERE shipment_id=@s", ("s", shipment));
            var opened = await Scalar<long>(admin, """
                SELECT count(*) FROM shipment_exception_history h JOIN shipment_exceptions e ON e.id=h.exception_id
                WHERE e.shipment_id=@s AND h.action='Opened'
                """, ("s", shipment));
            // A killed transaction is all-or-nothing: either nothing or the complete four-rule result.
            Assert.True((exceptions == 0 && evaluations == 0 && opened == 0) ||
                (exceptions == 1 && evaluations == 4 && opened == 1),
                $"partial state at iteration {iteration}: exceptions={exceptions} evaluations={evaluations} opened={opened} outcome={outcome}");
            if (exceptions == 0) rolledBack++;
            else if (outcome == "Applied") completed++;
            else committedThenLost++;
            Note($"S21 iteration={iteration} killAfterMs={iteration * 12} outcome={outcome} exceptions={exceptions} evaluations={evaluations} opened={opened}");

            // Recovery: a clean evaluator converges to exactly one exception, and stays idempotent.
            await using var cleanDb = new HarborOpsDbContext(
                new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(s.Roles.Api).Options);
            var clean = new ShipmentExceptionStore(cleanDb, configuration);
            Assert.Equal(M11StoreResult.Applied,
                (await clean.EvaluateAsync(s.Fixture.A, shipment, CancellationToken.None)).Result);
            Assert.Equal(M11StoreResult.Applied,
                (await clean.EvaluateAsync(s.Fixture.A, shipment, CancellationToken.None)).Result);
            Assert.Equal(1L, await Scalar<long>(admin,
                "SELECT count(*) FROM shipment_exceptions WHERE shipment_id=@s", ("s", shipment)));
            Assert.Equal(4L, await Scalar<long>(admin,
                "SELECT count(*) FROM shipment_exception_evaluations WHERE shipment_id=@s", ("s", shipment)));
            Assert.Equal(1L, await Scalar<long>(admin, """
                SELECT count(*) FROM shipment_exception_history h JOIN shipment_exceptions e ON e.id=h.exception_id
                WHERE e.shipment_id=@s AND h.action='Opened'
                """, ("s", shipment)));
        }
        Note($"S21 summary: rolledBack={rolledBack} committedThenConnectionLost={committedThenLost} completedBeforeKill={completed}");
    }

    // ---------------------------------------------------------------- G11-S08

    [Fact]
    public async Task EquipmentConflictWitnessIsDeterministicAttributableAndCountsLargeSets()
    {
        RequireExclusive();
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var administrator = s.Fixture.Admin; using var manager = s.Fixture.Manager;
        using var analyst = s.Fixture.Analyst;
        using var restrictedManager = s.Manager; using var restrictedAnalyst = s.Analyst;
        using var restrictedAdministrator = s.Administrator;
        var admin = s.Roles.Admin;

        var target = await G9Support.SeedResolvable(factory, s.Fixture);
        var sourceA = await G9Support.NewSource(factory, s.Fixture.A, "m11_wit_a");
        var sourceB = await G9Support.NewSource(factory, s.Fixture.A, "m11_wit_b");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        const string occurred = "2026-11-03T10:00:00Z";
        var shipmentUrl = $"/api/v1/organizations/{s.Fixture.A}/shipments/{target.Shipment}";
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var worker = new TrackingWorkStore(db, new TrackingWorkerSettings());
        var timeline = new ShipmentTimelineStore(db);
        using (var booked = await ReferenceDataHttpTests.Send(s.Fixture.Manager, HttpMethod.Patch, shipmentUrl,
            new { expectedVersion = 1L, workflowStatus = "Booked" }))
            Assert.Equal(HttpStatusCode.OK, booked.StatusCode);

        async Task<(string Reference, Guid Id)> NewContainer(int index)
        {
            var reference = $"SYN-BOX-W{suffix.ToUpperInvariant()}{index:D2}";
            using var created = await ReferenceDataHttpTests.Send(s.Fixture.Manager, HttpMethod.Post,
                $"/api/v1/organizations/{s.Fixture.A}/containers",
                new { referenceCode = $"G11W-{suffix.ToUpperInvariant()}-{index:D2}", identifierKind = "Synthetic",
                    containerIdentifier = reference, equipmentType = "Dry20" });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            using var assignment = await ReferenceDataHttpTests.Send(s.Fixture.Analyst, HttpMethod.Post,
                shipmentUrl + "/container-assignments",
                new { containerId = id, startsAtUtc = "2026-11-03T09:00:00Z" });
            Assert.Equal(HttpStatusCode.Created, assignment.StatusCode);
            return (reference, id);
        }

        async Task<JsonElement> Reevaluate()
        {
            await Drain(worker, s.Fixture.A);
            Assert.Equal(TrackingMutationResult.Applied,
                (await timeline.RebuildAsync(s.Fixture.A, target.Shipment, CancellationToken.None)).Result);
            using var evaluated = await ReferenceDataHttpTests.Send(restrictedManager,
                HttpMethod.Post, shipmentUrl + "/exceptions/evaluate");
            Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode);
            using var list = await restrictedManager.GetAsync(shipmentUrl + "/exceptions");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var items = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");
            return Assert.Single(items.EnumerateArray(),
                e => e.GetProperty("ruleCode").GetString() == "EquipmentConflict").Clone();
        }

        async Task<Guid> Event(ProvisionedIntegrationSource source, string key, string reference,
            string code, string at = occurred) =>
            await G9Support.Accept(factory, source, G9Support.EquipmentPayload(key, reference, at, code));

        // Phase 1: same-timestamp duplicates on the seeded container. The earliest received
        // GTIN (source B) and earliest received GTOT (source A) must be the witness pair.
        var inFirst = await Event(sourceB, $"p1-in-first-{suffix}", target.Reference, "GTIN");
        var outFirst = await Event(sourceA, $"p1-out-first-{suffix}", target.Reference, "GTOT");
        var inLater = await Event(sourceA, $"p1-in-later-{suffix}", target.Reference, "GTIN");
        var outLater = await Event(sourceB, $"p1-out-later-{suffix}", target.Reference, "GTOT");
        var phase1 = (await Reevaluate()).GetProperty("evidence");
        Assert.Equal(1, phase1.GetProperty("conflictContainerCount").GetInt32());
        Assert.False(phase1.GetProperty("conflictWitnessTruncated").GetBoolean());
        var w1 = phase1.GetProperty("conflictWitness");
        Assert.Equal(target.Container, w1.GetProperty("containerId").GetGuid());
        Assert.Equal(inFirst, w1.GetProperty("in").GetProperty("receiptId").GetGuid());
        Assert.Equal(outFirst, w1.GetProperty("out").GetProperty("receiptId").GetGuid());
        Assert.NotEqual(inLater, w1.GetProperty("in").GetProperty("receiptId").GetGuid());
        Assert.NotEqual(outLater, w1.GetProperty("out").GetProperty("receiptId").GetGuid());

        // Phase 2: a large conflicting set larger than the 50-container projection sample, with
        // GTIN/GTOT arrival order and source permutations across containers.
        var conflicting = new List<Guid> { target.Container };
        for (var i = 1; i <= 54; i++)
        {
            var (reference, id) = await NewContainer(i);
            conflicting.Add(id);
            switch (i % 3)
            {
                case 0:
                    await Event(sourceA, $"p2-{i}-in-{suffix}", reference, "GTIN");
                    await Event(sourceB, $"p2-{i}-out-{suffix}", reference, "GTOT");
                    break;
                case 1:
                    await Event(sourceB, $"p2-{i}-out-{suffix}", reference, "GTOT");
                    await Event(sourceA, $"p2-{i}-in-{suffix}", reference, "GTIN");
                    break;
                default:
                    await Event(sourceB, $"p2-{i}-in-{suffix}", reference, "GTIN");
                    await Event(sourceA, $"p2-{i}-out-{suffix}", reference, "GTOT");
                    break;
            }
        }
        var phase2 = (await Reevaluate()).GetProperty("evidence");
        Assert.Equal(55, phase2.GetProperty("conflictContainerCount").GetInt32());
        Assert.True(phase2.GetProperty("conflictWitnessTruncated").GetBoolean());
        var expectedMin = await Scalar<Guid>(admin, "SELECT x FROM unnest(@ids) AS x ORDER BY x LIMIT 1",
            ("ids", conflicting.ToArray()));
        var w2 = phase2.GetProperty("conflictWitness");
        Assert.Equal(expectedMin, w2.GetProperty("containerId").GetGuid());
        await AssertWitnessAttributable(admin, w2, s.Fixture.A, target.Shipment, expectedMin, supersededAllowed: false);

        // Phase 3: out-of-order arrival. A late GTIN for a container that only had a GTOT at the
        // latest instant creates a conflict only once it arrives; an older GTIN does not.
        var (lateReference, lateId) = await NewContainer(55);
        await Event(sourceA, $"p3-late-out-{suffix}", lateReference, "GTOT");
        var (olderReference, olderId) = await NewContainer(56);
        await Event(sourceA, $"p3-older-in-{suffix}", olderReference, "GTIN", "2026-11-03T09:30:00Z");
        await Event(sourceB, $"p3-older-out-{suffix}", olderReference, "GTOT");
        var phase3a = (await Reevaluate()).GetProperty("evidence");
        Assert.Equal(55, phase3a.GetProperty("conflictContainerCount").GetInt32());
        await Event(sourceB, $"p3-late-in-{suffix}", lateReference, "GTIN");
        conflicting.Add(lateId);
        var phase3b = (await Reevaluate()).GetProperty("evidence");
        Assert.Equal(56, phase3b.GetProperty("conflictContainerCount").GetInt32());
        Assert.DoesNotContain(olderId, new[] { phase3b.GetProperty("conflictWitness").GetProperty("containerId").GetGuid() });

        // Phase 4: supersede one conflicting GTOT with no replacement. The conflict count falls
        // by one and no reported witness may reference the superseded receipt.
        var victim = conflicting.First(id => id != expectedMin && id != lateId && id != target.Container);
        var victimReference = await Scalar<string>(admin,
            "SELECT container_identifier FROM containers WHERE id=@i", ("i", victim));
        var victimOut = await Scalar<Guid>(admin, """
            SELECT receipt_id FROM shipment_tracking_timeline_entries
            WHERE shipment_id=@s AND container_id=@c AND event_code='GTOT' LIMIT 1
            """, ("s", target.Shipment), ("c", victim));
        var generation = await Scalar<long>(admin,
            "SELECT COALESCE(max(generation),0) FROM shipment_tracking_correction_heads WHERE shipment_id=@s",
            ("s", target.Shipment));
        using (var correction = await M11ExceptionRuntimeTests.SendCorrection(restrictedManager,
            shipmentUrl + "/tracking-corrections",
            new { targetReceiptId = victimOut, replacementReceiptId = (Guid?)null,
                expectedGeneration = generation, reason = "Supersede one synthetic observation" }))
            Assert.Equal(HttpStatusCode.OK, correction.StatusCode);
        var phase4Row = await Reevaluate();
        var phase4 = phase4Row.GetProperty("evidence");
        Assert.Equal(55, phase4.GetProperty("conflictContainerCount").GetInt32());
        conflicting.Remove(victim);
        var expectedMin4 = await Scalar<Guid>(admin, "SELECT x FROM unnest(@ids) AS x ORDER BY x LIMIT 1",
            ("ids", conflicting.ToArray()));
        var w4 = phase4.GetProperty("conflictWitness");
        Assert.Equal(expectedMin4, w4.GetProperty("containerId").GetGuid());
        Assert.NotEqual(victimOut, w4.GetProperty("out").GetProperty("receiptId").GetGuid());
        await AssertWitnessAttributable(admin, w4, s.Fixture.A, target.Shipment, expectedMin4, supersededAllowed: false);
        Assert.NotEmpty(victimReference);

        // Phase 5: determinism and no duplicate history. Rebuild and reevaluate twice more.
        var exceptionId = phase4Row.GetProperty("id").GetGuid();
        var historyBefore = await Scalar<long>(admin,
            "SELECT count(*) FROM shipment_exception_history WHERE exception_id=@e", ("e", exceptionId));
        var again1 = (await Reevaluate()).GetProperty("evidence").GetProperty("conflictWitness").GetRawText();
        var again2 = (await Reevaluate()).GetProperty("evidence").GetProperty("conflictWitness").GetRawText();
        Assert.Equal(w4.GetRawText(), again1);
        Assert.Equal(again1, again2);
        Assert.Equal(1L, await Scalar<long>(admin,
            "SELECT count(*) FROM shipment_exceptions WHERE shipment_id=@s AND rule_code='EquipmentConflict'",
            ("s", target.Shipment)));
        Assert.Equal(historyBefore, await Scalar<long>(admin,
            "SELECT count(*) FROM shipment_exception_history WHERE exception_id=@e", ("e", exceptionId)));
        Assert.Equal(0L, await Scalar<long>(admin, """
            SELECT count(*) - count(DISTINCT (action, source_fingerprint)) FROM shipment_exception_history
            WHERE exception_id=@e
            """, ("e", exceptionId)));
        Note($"S08 phases: p1 count=1 p2 count=55 p3 count 55->56 p4 count=55 history={historyBefore}");
    }

    // Every id in a reported witness must resolve to an accepted event whose receipt has not
    // been superseded by a correction.
    private static async Task AssertWitnessAttributable(string admin, JsonElement witness, Guid org,
        Guid shipment, Guid container, bool supersededAllowed)
    {
        Assert.Equal(container, witness.GetProperty("containerId").GetGuid());
        foreach (var (side, code) in new[] { ("in", "GTIN"), ("out", "GTOT") })
        {
            var part = witness.GetProperty(side);
            var eventId = part.GetProperty("eventId").GetGuid();
            var receipt = part.GetProperty("receiptId").GetGuid();
            var source = part.GetProperty("sourceId").GetGuid();
            var sourceEvent = part.GetProperty("sourceEventId").GetString();
            var matches = await Scalar<long>(admin, """
                SELECT count(*) FROM shipment_tracking_timeline_entries e
                WHERE e.organization_id=@o AND e.shipment_id=@s AND e.container_id=@c
                  AND e.event_id=@ev AND e.receipt_id=@r AND e.source_id=@src
                  AND e.source_event_id=@se AND e.event_code=@code
                """, ("o", org), ("s", shipment), ("c", container), ("ev", eventId), ("r", receipt),
                ("src", source), ("se", sourceEvent!), ("code", code));
            Assert.Equal(1L, matches);
            var superseded = await Scalar<long>(admin, """
                SELECT count(*) FROM shipment_tracking_corrections
                WHERE organization_id=@o AND shipment_id=@s AND target_receipt_id=@r
                """, ("o", org), ("s", shipment), ("r", receipt));
            if (!supersededAllowed) Assert.Equal(0L, superseded);
            var accepted = await Scalar<long>(admin,
                "SELECT count(*) FROM tracking_accepted_events WHERE id=@ev AND receipt_id=@r",
                ("ev", eventId), ("r", receipt));
            Assert.Equal(1L, accepted);
        }
    }
}
