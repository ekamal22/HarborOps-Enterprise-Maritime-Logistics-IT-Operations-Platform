using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Exceptions;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Shipments;
using Microsoft.Extensions.DependencyInjection;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace HarborOps.IntegrationTests;

// G11 closure cases: ambiguous commit outcomes (commit succeeds and the reply is lost, or the
// commit never arrives), concurrent recurrence, and the rule/source-confidence matrix through
// the real HTTP and restricted-role SQL path. Requires an exclusive disposable PostgreSQL server.
[Collection("M12ServerRoleIsolation")]
public sealed class M11ClosureRuntimeTests(ITestOutputHelper output)
{
    private void Note(string line)
    {
        output.WriteLine(line);
        var directory = Environment.GetEnvironmentVariable("HARBOROPS_G11_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        File.AppendAllText(Path.Combine(directory, "closure-notes.log"),
            $"{DateTimeOffset.UtcNow:O} {line}{Environment.NewLine}");
    }

    private static void RequireExclusive()
    {
        if (Environment.GetEnvironmentVariable("HARBOROPS_G12_EXCLUSIVE_POSTGRES") != "1")
            throw new InvalidOperationException(
                "Requires HARBOROPS_G12_EXCLUSIVE_POSTGRES=1 on an exclusive disposable PostgreSQL server");
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

    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
            { ["M10CorrectionProof:Key"] = ReferenceDataHttpTests.CorrectionProofKey }).Build();

    private sealed record Counts(long Exceptions, long Evaluations, long Opened, long History, long Audit);

    private static async Task<Counts> Count(string admin, Guid shipment) => new(
        await Scalar<long>(admin, "SELECT count(*) FROM shipment_exceptions WHERE shipment_id=@s", ("s", shipment)),
        await Scalar<long>(admin, "SELECT count(*) FROM shipment_exception_evaluations WHERE shipment_id=@s", ("s", shipment)),
        await Scalar<long>(admin, """
            SELECT count(*) FROM shipment_exception_history h JOIN shipment_exceptions e ON e.id=h.exception_id
            WHERE e.shipment_id=@s AND h.action='Opened'
            """, ("s", shipment)),
        await Scalar<long>(admin, """
            SELECT count(*) FROM shipment_exception_history h JOIN shipment_exceptions e ON e.id=h.exception_id
            WHERE e.shipment_id=@s
            """, ("s", shipment)),
        await Scalar<long>(admin, """
            SELECT count(*) FROM audit_entries a JOIN shipment_exceptions e ON e.id=a.resource_id
            WHERE e.shipment_id=@s
            """, ("s", shipment)));

    private static HarborOpsDbContext Context(string connection) =>
        new(new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(connection).Options);

    // ------------------------------------------------------------------ S21 ambiguous commit

    [Theory]
    [InlineData(TrackingCommitSocketProxy.CutPoint.AfterCommitServerReady)]
    [InlineData(TrackingCommitSocketProxy.CutPoint.BeforeCommitForward)]
    public async Task EvaluationWithLostCommitOutcomeRecoversWithoutDuplicates(
        TrackingCommitSocketProxy.CutPoint cut)
    {
        RequireExclusive();
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var _ = s.Fixture.Admin; using var __ = s.Fixture.Manager; using var ___ = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst; using var administrator = s.Administrator;
        var admin = s.Roles.Admin;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        Assert.Equal(new Counts(0, 0, 0, 0, 0), await Count(admin, shipment));

        await using var proxy = new TrackingCommitSocketProxy(s.Roles.Api, cut, "m11_apply_signed_evaluation");
        await using (var db = Context(proxy.ProxiedConnectionString(s.Roles.Api)))
        {
            var store = new ShipmentExceptionStore(db, Config());
            await Assert.ThrowsAnyAsync<Exception>(
                () => store.EvaluateAsync(s.Fixture.A, shipment, CancellationToken.None));
        }
        Assert.True(proxy.Triggered, "The proxy never reached its fault point");
        var ambiguous = await Count(admin, shipment);
        if (cut == TrackingCommitSocketProxy.CutPoint.AfterCommitServerReady)
        {
            // Durable commit with the reply lost: the full four-rule result exists exactly once.
            Assert.Equal(new Counts(1, 4, 1, 1, 0), ambiguous);
        }
        else
        {
            // The commit never reached the server: nothing is visible.
            Assert.Equal(new Counts(0, 0, 0, 0, 0), ambiguous);
        }

        // The caller cannot know which happened; the same operation must converge to one result.
        await using var clean = Context(s.Roles.Api);
        var recovery = new ShipmentExceptionStore(clean, Config());
        for (var attempt = 0; attempt < 3; attempt++)
            Assert.Equal(M11StoreResult.Applied,
                (await recovery.EvaluateAsync(s.Fixture.A, shipment, CancellationToken.None)).Result);
        var final = await Count(admin, shipment);
        Assert.Equal(1L, final.Exceptions);
        Assert.Equal(4L, final.Evaluations);
        Assert.Equal(1L, final.Opened);
        Assert.Equal(1L, final.History);
        Assert.Equal(1L, await Scalar<long>(admin,
            "SELECT occurrence FROM shipment_exceptions WHERE shipment_id=@s", ("s", shipment)));
        Note($"S21 lost-commit {cut}: ambiguous={ambiguous} final={final}");
    }

    [Fact]
    public async Task DispositionWithLostReplyIsReplayedByIdempotencyKeyWithoutDuplicates()
    {
        RequireExclusive();
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var _ = s.Fixture.Admin; using var __ = s.Fixture.Manager; using var ___ = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst; using var administrator = s.Administrator;
        var admin = s.Roles.Admin;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        var exception = await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        var actor = await Scalar<Guid>(admin, "SELECT id FROM users WHERE email=@e", ("e", s.Fixture.ManagerEmail));
        var session = await Scalar<Guid>(admin, """
            SELECT id FROM auth_sessions WHERE user_id=@u AND selected_organization_id=@o
              AND revoked_at_utc IS NULL ORDER BY issued_at_utc DESC LIMIT 1
            """, ("u", actor), ("o", s.Fixture.A));
        var key = Guid.NewGuid();
        const string reason = "Acknowledged while the reply is lost";

        await using var proxy = new TrackingCommitSocketProxy(s.Roles.Api,
            TrackingCommitSocketProxy.CutPoint.AfterArmedStatementReady, "m11_apply_signed_action");
        await using (var db = Context(proxy.ProxiedConnectionString(s.Roles.Api)))
        {
            var store = new ShipmentExceptionStore(db, Config());
            await Assert.ThrowsAnyAsync<Exception>(() => store.ActionAsync(s.Fixture.A, exception, actor,
                session, "Acknowledge", 1, reason, key, CancellationToken.None));
        }
        Assert.True(proxy.Triggered, "The proxy never reached its fault point");
        // The disposition committed even though the caller never saw the result.
        Assert.Equal(2L, await Scalar<long>(admin, "SELECT version FROM shipment_exceptions WHERE id=@e", ("e", exception)));
        var afterLoss = await Count(admin, shipment);

        // Replay with the same key (what a retrying client does): no second effect.
        await using var clean = Context(s.Roles.Api);
        var recovery = new ShipmentExceptionStore(clean, Config());
        var replay = await recovery.ActionAsync(s.Fixture.A, exception, actor, session, "Acknowledge", 1,
            reason, key, CancellationToken.None);
        Assert.Equal(M11StoreResult.Replayed, replay.Result);
        Assert.Equal(2L, replay.Version);
        Assert.Equal(afterLoss, await Count(admin, shipment));
        Assert.Equal(1L, await Scalar<long>(admin,
            "SELECT count(*) FROM shipment_exception_replays WHERE exception_id=@e", ("e", exception)));
        Assert.Equal(1L, await Scalar<long>(admin, """
            SELECT count(*) FROM audit_entries WHERE resource_id=@e AND event_type='shipment.exception_acknowledged'
            """, ("e", exception)));

        // A fresh key with the stale expected version is a genuine conflict, not a duplicate.
        var stale = await recovery.ActionAsync(s.Fixture.A, exception, actor, session, "Acknowledge", 1,
            reason, Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(M11StoreResult.Conflict, stale.Result);
        Assert.Equal(afterLoss, await Count(admin, shipment));
        Note($"S21 lost-reply disposition: committed version 2, replay=Replayed, counts unchanged {afterLoss}");
    }

    // ------------------------------------------------------------------ S16 concurrent recurrence

    [Fact]
    public async Task ConcurrentEvaluatorsOpenExactlyOneRecurrenceOccurrence()
    {
        RequireExclusive();
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var _ = s.Fixture.Admin; using var __ = s.Fixture.Manager; using var ___ = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst; using var administrator = s.Administrator;
        var admin = s.Roles.Admin;
        var shipment = await M11ExceptionRuntimeTests.ScheduledShipment(s);
        var first = await M11ExceptionRuntimeTests.EvaluateAndFind(s, shipment);
        var route = $"/api/v1/organizations/{s.Fixture.A}/shipments/{shipment}";
        var firstPath = M11ExceptionRuntimeTests.Collection(s.Fixture.A) + "/" + first;
        using (var ack = await M11ExceptionRuntimeTests.SendAction(manager, firstPath,
            new { action = "Acknowledge", expectedVersion = 1L, reason = "Acknowledged before clearance" }, Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.OK, ack.StatusCode);
        // Clear the condition, evaluate, then make it recur and evaluate from two evaluators at once.
        var v1 = await Scalar<long>(admin, "SELECT version FROM shipments WHERE id=@s", ("s", shipment));
        using (var clear = await ReferenceDataHttpTests.Send(s.Fixture.Manager, HttpMethod.Patch, route,
            new { expectedVersion = v1, plannedArrivalAtUtc = M11ExceptionRuntimeTests.Utc(DateTimeOffset.UtcNow.AddHours(5)) }))
            Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        await using (var db = Context(s.Roles.Api))
            Assert.Equal(M11StoreResult.Applied,
                (await new ShipmentExceptionStore(db, Config()).EvaluateAsync(s.Fixture.A, shipment, CancellationToken.None)).Result);
        var v2 = await Scalar<long>(admin, "SELECT version FROM shipments WHERE id=@s", ("s", shipment));
        using (var recur = await ReferenceDataHttpTests.Send(s.Fixture.Manager, HttpMethod.Patch, route,
            new { expectedVersion = v2, plannedArrivalAtUtc = M11ExceptionRuntimeTests.Utc(DateTimeOffset.UtcNow.AddHours(-3)) }))
            Assert.Equal(HttpStatusCode.OK, recur.StatusCode);

        await using var one = Context(s.Roles.Api);
        await using var two = Context(s.Roles.Api);
        await using var three = Context(s.Roles.Api);
        var results = await Task.WhenAll(
            new ShipmentExceptionStore(one, Config()).EvaluateAsync(s.Fixture.A, shipment, CancellationToken.None),
            new ShipmentExceptionStore(two, Config()).EvaluateAsync(s.Fixture.A, shipment, CancellationToken.None),
            new ShipmentExceptionStore(three, Config()).EvaluateAsync(s.Fixture.A, shipment, CancellationToken.None));
        Assert.All(results, r => Assert.Contains(r.Result, new[] { M11StoreResult.Applied, M11StoreResult.Conflict }));
        Assert.Contains(results, r => r.Result == M11StoreResult.Applied);
        await using var settle = Context(s.Roles.Api);
        Assert.Equal(M11StoreResult.Applied,
            (await new ShipmentExceptionStore(settle, Config()).EvaluateAsync(s.Fixture.A, shipment, CancellationToken.None)).Result);

        Assert.Equal(2L, await Scalar<long>(admin,
            "SELECT count(*) FROM shipment_exceptions WHERE shipment_id=@s AND rule_code='ArrivalScheduleOverdue'", ("s", shipment)));
        Assert.Equal(1L, await Scalar<long>(admin,
            "SELECT count(*) FROM shipment_exceptions WHERE shipment_id=@s AND occurrence=2 AND lifecycle_state='Open'", ("s", shipment)));
        Assert.Equal(1L, await Scalar<long>(admin, """
            SELECT count(*) FROM shipment_exception_history h JOIN shipment_exceptions e ON e.id=h.exception_id
            WHERE e.shipment_id=@s AND e.occurrence=2 AND h.action='Opened'
            """, ("s", shipment)));
        Assert.Equal(1L, await Scalar<long>(admin, """
            SELECT count(*) FROM shipment_exception_history h
            WHERE h.exception_id=@e AND h.action='AutoResolved'
            """, ("e", first)));
        Note($"S16 concurrent recurrence: results={string.Join(",", results.Select(r => r.Result))} occurrences=2");
    }

    // ------------------------------------------------------------------ S08 forced equal-instant tie

    [Fact]
    public async Task WitnessTieBreaksOnSourceThenCollationOrderedSourceEventWhenReceiptInstantsAreEqual()
    {
        RequireExclusive();
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var factory = s.Factory; using var runtime = s.Runtime;
        using var administrator = s.Fixture.Admin; using var manager = s.Fixture.Manager; using var analyst = s.Fixture.Analyst;
        using var restrictedManager = s.Manager; using var restrictedAnalyst = s.Analyst; using var restrictedAdministrator = s.Administrator;
        var admin = s.Roles.Admin;
        var target = await G9Support.SeedResolvable(factory, s.Fixture);
        var sourceA = await G9Support.NewSource(factory, s.Fixture.A, "m11_tie_a");
        var sourceB = await G9Support.NewSource(factory, s.Fixture.A, "m11_tie_b");
        var shipmentUrl = $"/api/v1/organizations/{s.Fixture.A}/shipments/{target.Shipment}";
        using (var booked = await ReferenceDataHttpTests.Send(s.Fixture.Manager, HttpMethod.Patch, shipmentUrl,
            new { expectedVersion = 1L, workflowStatus = "Booked" }))
            Assert.Equal(HttpStatusCode.OK, booked.StatusCode);
        const string at = "2026-11-03T10:00:00Z";
        // Uppercase Z sorts before lowercase a under the C collation but after it in linguistic collations.
        await G9Support.Accept(factory, sourceA, G9Support.EquipmentPayload("tie-Z", target.Reference, at, "GTIN"));
        await G9Support.Accept(factory, sourceA, G9Support.EquipmentPayload("tie-a", target.Reference, at, "GTIN"));
        await G9Support.Accept(factory, sourceB, G9Support.EquipmentPayload("tie-Z", target.Reference, at, "GTIN"));
        await G9Support.Accept(factory, sourceB, G9Support.EquipmentPayload("tie-a", target.Reference, at, "GTIN"));
        await G9Support.Accept(factory, sourceA, G9Support.EquipmentPayload("tie-out-a", target.Reference, at, "GTOT"));
        await G9Support.Accept(factory, sourceB, G9Support.EquipmentPayload("tie-out-b", target.Reference, at, "GTOT"));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var worker = new TrackingWorkStore(db, new TrackingWorkerSettings());
        for (var i = 0; i < 6; i++)
        {
            var claim = await worker.ClaimOneAsync(s.Fixture.A, CancellationToken.None);
            Assert.NotNull(claim);
            Assert.Equal(TrackingWorkDisposition.Processed, await worker.FinalizeAsync(claim!, CancellationToken.None));
        }
        Assert.Equal(TrackingMutationResult.Applied,
            (await new ShipmentTimelineStore(db).RebuildAsync(s.Fixture.A, target.Shipment, CancellationToken.None)).Result);

        // Make every candidate arrive at the same instant. The timeline is append-only for ordinary roles, so the
        // fixture bypasses its guard in an administrator session; the evaluation under test is unchanged.
        await using (var tie = new NpgsqlConnection(admin))
        {
            await tie.OpenAsync();
            await using var command = new NpgsqlCommand("""
                SET session_replication_role = replica;
                UPDATE shipment_tracking_timeline_entries SET received_at_utc = timestamptz '2026-11-03T12:00:00Z'
                WHERE shipment_id = @s
                """, tie);
            command.Parameters.AddWithValue("s", target.Shipment);
            Assert.Equal(6, await command.ExecuteNonQueryAsync());
        }
        using (var evaluated = await ReferenceDataHttpTests.Send(restrictedManager, HttpMethod.Post, shipmentUrl + "/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode);
        using var list = await restrictedManager.GetAsync(shipmentUrl + "/exceptions");
        var row = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray()
            .Single(e => e.GetProperty("ruleCode").GetString() == "EquipmentConflict");
        var witness = row.GetProperty("evidence").GetProperty("conflictWitness");

        // Independent expectation: lowest source id by UUID byte order, then lowest source event id by ordinal order.
        bool AFirst = string.CompareOrdinal(sourceA.Id.ToString("D"), sourceB.Id.ToString("D")) < 0;
        var expectedIn = AFirst ? (sourceA.Id, "tie-Z") : (sourceB.Id, "tie-Z");
        var expectedOut = AFirst ? (sourceA.Id, "tie-out-a") : (sourceB.Id, "tie-out-b");
        Assert.True(string.CompareOrdinal("tie-Z", "tie-a") < 0);
        Assert.Equal(expectedIn.Item1, witness.GetProperty("in").GetProperty("sourceId").GetGuid());
        Assert.Equal(expectedIn.Item2, witness.GetProperty("in").GetProperty("sourceEventId").GetString());
        Assert.Equal(expectedOut.Item1, witness.GetProperty("out").GetProperty("sourceId").GetGuid());
        Assert.Equal(expectedOut.Item2, witness.GetProperty("out").GetProperty("sourceEventId").GetString());
        // Repeating the evaluation selects the same witness and adds no history.
        var historyBefore = await Scalar<long>(admin, "SELECT count(*) FROM shipment_exception_history WHERE exception_id=@e",
            ("e", row.GetProperty("id").GetGuid()));
        using (var again = await ReferenceDataHttpTests.Send(restrictedManager, HttpMethod.Post, shipmentUrl + "/exceptions/evaluate"))
            Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        using var list2 = await restrictedManager.GetAsync(shipmentUrl + "/exceptions");
        var row2 = (await list2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray()
            .Single(e => e.GetProperty("ruleCode").GetString() == "EquipmentConflict");
        Assert.Equal(witness.GetRawText(), row2.GetProperty("evidence").GetProperty("conflictWitness").GetRawText());
        Assert.Equal(historyBefore, await Scalar<long>(admin, "SELECT count(*) FROM shipment_exception_history WHERE exception_id=@e",
            ("e", row.GetProperty("id").GetGuid())));
        Note($"S08 forced tie: AFirst={AFirst} witness in={expectedIn.Item2} out={expectedOut.Item2}");
    }

    // ------------------------------------------------------------------ S05 / S06 / S07 matrix

    private sealed record Case(string Name, int? ArrivalHours, int? DepartureHours, string[] Path,
        Dictionary<string, string> Expect, string? OpenedRule = null, string? OpenedConfidence = null,
        string? OpenedSeverity = null);

    [Fact]
    public async Task RuleStateAndSourceConfidenceMatrixHoldsThroughHttpAndRestrictedSql()
    {
        RequireExclusive();
        var s = await M11ExceptionRuntimeTests.Prepare();
        using var f = s.Factory; using var runtime = s.Runtime;
        using var _ = s.Fixture.Admin; using var __ = s.Fixture.Manager; using var ___ = s.Fixture.Analyst;
        using var manager = s.Manager; using var analyst = s.Analyst; using var administrator = s.Administrator;
        var admin = s.Roles.Admin;
        const string Overdue = "ArrivalScheduleOverdue", Late = "RecordedArrivalLate", Slip = "DepartureSlippageRisk", Equip = "EquipmentConflict";

        var cases = new[]
        {
            // S05: unconfirmed schedule versus an explicit workflow arrival.
            new Case("booked-past-arrival-unconfirmed", -3, -30, ["Booked"],
                new() { [Overdue] = "Active", [Late] = "Unknown", [Slip] = "Unknown", [Equip] = "Stale" },
                Overdue, "ScheduleOnly", "Warning"),
            new Case("arrived-after-grace", -3, -30, ["Booked", "InTransit", "Arrived"],
                new() { [Overdue] = "Inactive", [Late] = "Active", [Slip] = "Inactive", [Equip] = "Stale" },
                Late, "RecordedWorkflow", "High"),
            new Case("arrived-within-grace", 3, -30, ["Booked", "InTransit", "Arrived"],
                new() { [Overdue] = "Inactive", [Late] = "Inactive", [Slip] = "Inactive", [Equip] = "Stale" }),
            new Case("booked-future-arrival-not-yet-overdue", 4, -2, ["Booked"],
                new() { [Overdue] = "Inactive", [Late] = "Unknown", [Slip] = "Unknown", [Equip] = "Stale" }),
            // S06: departure slippage needs recorded workflow departure and a future arrival.
            new Case("intransit-late-departure-future-arrival", 6, -5, ["Booked", "InTransit"],
                new() { [Overdue] = "Inactive", [Late] = "Unknown", [Slip] = "Active", [Equip] = "Stale" },
                Slip, "RecordedWorkflow", "Warning"),
            new Case("intransit-departure-within-tolerance", 6, 0, ["Booked", "InTransit"],
                new() { [Overdue] = "Inactive", [Late] = "Unknown", [Slip] = "Inactive", [Equip] = "Stale" }),
            new Case("booked-no-recorded-departure", 6, -5, ["Booked"],
                new() { [Overdue] = "Inactive", [Late] = "Unknown", [Slip] = "Unknown", [Equip] = "Stale" }),
            // S07: absent schedule and ineligible workflow states never invent delay.
            new Case("booked-no-schedule", null, null, ["Booked"],
                new() { [Overdue] = "Unknown", [Late] = "Unknown", [Slip] = "Unknown", [Equip] = "Stale" }),
            new Case("draft-with-past-schedule-ineligible", -3, -30, [],
                new() { [Overdue] = "Inactive", [Late] = "Inactive", [Slip] = "Inactive", [Equip] = "Inactive" }),
            new Case("cancelled-with-past-schedule-ineligible", -3, -30, ["Booked", "Cancelled"],
                new() { [Overdue] = "Inactive", [Late] = "Inactive", [Slip] = "Inactive", [Equip] = "Inactive" }),
        };

        foreach (var c in cases)
        {
            var seed = await AssignmentHttpTests.Seed(s.Fixture);
            var url = $"/api/v1/organizations/{s.Fixture.A}/shipments/{seed.Shipment}";
            async Task<long> Version() => await Scalar<long>(admin, "SELECT version FROM shipments WHERE id=@s", ("s", seed.Shipment));
            var now = DateTimeOffset.UtcNow;
            if (c.ArrivalHours.HasValue || c.DepartureHours.HasValue)
            {
                var body = new Dictionary<string, object?>
                {
                    ["expectedVersion"] = await Version(),
                    ["plannedArrivalAtUtc"] = c.ArrivalHours.HasValue ? M11ExceptionRuntimeTests.Utc(now.AddHours(c.ArrivalHours.Value)) : null,
                    ["plannedDepartureAtUtc"] = c.DepartureHours.HasValue
                        ? M11ExceptionRuntimeTests.Utc(now.AddHours(c.DepartureHours.Value)) : null
                };
                using var plan = await ReferenceDataHttpTests.Send(s.Fixture.Manager, HttpMethod.Patch, url, body);
                Assert.True(plan.StatusCode == HttpStatusCode.OK, $"{c.Name}: schedule {plan.StatusCode}");
            }
            foreach (var step in c.Path)
            {
                using var move = await ReferenceDataHttpTests.Send(s.Fixture.Manager, HttpMethod.Patch, url,
                    new { expectedVersion = await Version(), workflowStatus = step });
                Assert.True(move.StatusCode == HttpStatusCode.OK, $"{c.Name}: workflow {step} {move.StatusCode}");
            }
            // Alternate cases use the HTTP route; the others call the same store under the restricted login.
            // The route has its own per-principal rate limit, so it cannot carry every evaluation.
            async Task EvaluateOnce()
            {
                if (Array.IndexOf(cases, c) % 2 == 0)
                {
                    using var evaluate = await ReferenceDataHttpTests.Send(manager, HttpMethod.Post, url + "/exceptions/evaluate");
                    Assert.Equal(HttpStatusCode.OK, evaluate.StatusCode);
                }
                else
                {
                    await using var db = Context(s.Roles.Api);
                    Assert.Equal(M11StoreResult.Applied, (await new ShipmentExceptionStore(db, Config())
                        .EvaluateAsync(s.Fixture.A, seed.Shipment, CancellationToken.None)).Result);
                }
            }
            await EvaluateOnce();
            foreach (var (rule, expected) in c.Expect)
            {
                var actual = await Scalar<string>(admin, """
                    SELECT condition_state FROM shipment_exception_evaluations WHERE shipment_id=@s AND rule_code=@r
                    """, ("s", seed.Shipment), ("r", rule));
                Assert.True(expected == actual, $"{c.Name}/{rule}: expected {expected} got {actual}");
            }
            var opened = await Scalar<long>(admin, "SELECT count(*) FROM shipment_exceptions WHERE shipment_id=@s", ("s", seed.Shipment));
            Assert.Equal(c.OpenedRule is null ? 0L : 1L, opened);
            if (c.OpenedRule is not null)
            {
                Assert.Equal(c.OpenedConfidence, await Scalar<string>(admin,
                    "SELECT confidence FROM shipment_exceptions WHERE shipment_id=@s", ("s", seed.Shipment)));
                Assert.Equal(c.OpenedSeverity, await Scalar<string>(admin,
                    "SELECT severity FROM shipment_exceptions WHERE shipment_id=@s", ("s", seed.Shipment)));
                Assert.Equal(c.OpenedRule, await Scalar<string>(admin,
                    "SELECT rule_code FROM shipment_exceptions WHERE shipment_id=@s", ("s", seed.Shipment)));
            }
            // A repeat evaluation is a no-op for the lifecycle tables.
            var before = await Count(admin, seed.Shipment);
            await using (var repeatDb = Context(s.Roles.Api))
                Assert.Equal(M11StoreResult.Applied, (await new ShipmentExceptionStore(repeatDb, Config())
                    .EvaluateAsync(s.Fixture.A, seed.Shipment, CancellationToken.None)).Result);
            Assert.Equal(before, await Count(admin, seed.Shipment));
            Note($"S05-S07 case {c.Name}: ok");
        }
    }
}
