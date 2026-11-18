using System.Net;
using System.Text.Json;
using Xunit;

namespace HarborOps.IntegrationTests;

// G18 lifecycle, gates, history and reopen witnesses. Every case goes through the real HTTP
// pipeline as the restricted harborops_api role and then inspects PostgreSQL directly.
[Collection("M12ServerRoleIsolation")]
public sealed class M18LifecycleTests
{
    internal static object Move(long version, string to, string reason = "operator_step") =>
        new { expectedVersion = version, toState = to, reasonCode = reason };

    internal static async Task<JsonElement> History(M18World w, HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(w.Route(null, "/" + id + "/history"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await M18World.Json(response);
    }

    internal static async Task<(Guid Id, long Version)> ToInvestigating(M18World w,
        HttpClient support, string severity = "SEV-2")
    {
        var (id, v) = await w.CreateIncident(support,
            M18World.NewIncident(severity: severity));
        v = await w.Post(support, id, "/transitions", Move(v, "Triage", "triaged"));
        v = await w.Post(support, id, "/assignment", new
            { expectedVersion = v, assignedTo = w.Users["support"].Id, reasonCode = "initial_owner" },
            HttpMethod.Patch);
        v = await w.Post(support, id, "/transitions", Move(v, "Investigating", "investigating"));
        return (id, v);
    }

    internal static async Task<(Guid Id, long Version, Guid Action)> ToMonitoring(M18World w,
        HttpClient support, string severity = "SEV-2")
    {
        var (id, v) = await ToInvestigating(w, support, severity);
        v = await w.Post(support, id, "/notes", new { expectedVersion = v, body = "Readiness probe fails intermittently." });
        v = await w.Post(support, id, "/actions", new
            { expectedVersion = v, title = "Restart database pool", ownerUserId = w.Users["support"].Id });
        v = await w.Post(support, id, "/transitions", Move(v, "Mitigated", "mitigation_applied"));
        v = await w.Post(support, id, "/evidence", new { expectedVersion = v, observationCode = "pool_restarted" });
        v = await w.Post(support, id, "/transitions", Move(v, "Monitoring", "monitoring_started"));
        var history = await History(w, support, id);
        var action = history.GetProperty("actions")[0].GetProperty("id").GetGuid();
        return (id, v, action);
    }

    internal static async Task<long> DispositionAction(M18World w, HttpClient support, Guid id,
        long v, Guid action)
    {
        v = await w.Post(support, id, "/actions/" + action, new
            { expectedVersion = v, actionId = action, expectedActionVersion = 1L,
              toState = "InProgress", reasonCode = "work_started" }, HttpMethod.Patch);
        return await w.Post(support, id, "/actions/" + action, new
            { expectedVersion = v, actionId = action, expectedActionVersion = 2L,
              toState = "Verified", reasonCode = "work_verified" }, HttpMethod.Patch);
    }

    [Fact]
    public async Task SevenStateChainProducesOrderedImmutableHistoryAndMatchingProjection()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        using var second = await w.Client("support2");
        var (id, v, action) = await ToMonitoring(w, support);
        v = await DispositionAction(w, support, id, v, action);
        v = await w.Post(support, id, "/recovery", new { expectedVersion = v, rootCauseCode = "pool_exhaustion" });
        v = await w.Post(support, id, "/transitions", Move(v, "Resolved", "recovery_confirmed"));
        v = await w.Post(second, id, "/transitions", Move(v, "Closed", "independently_confirmed"));

        var history = await History(w, support, id);
        var events = history.GetProperty("events").EnumerateArray().ToList();
        Assert.Equal(Enumerable.Range(1, events.Count).Select(i => (long)i),
            events.Select(e => e.GetProperty("sequence").GetInt64()));
        Assert.Equal(v, events.Count);
        Assert.Equal(v, history.GetProperty("incident").GetProperty("version").GetInt64());
        Assert.Equal("Closed", history.GetProperty("incident").GetProperty("state").GetString());
        var states = events.Where(e => e.GetProperty("kind").GetString() is "Transitioned")
            .Select(e => e.GetProperty("fromState").GetString() + ">" + e.GetProperty("toState").GetString()).ToList();
        Assert.Equal(new[] { "New>Triage", "Triage>Investigating", "Investigating>Mitigated",
            "Mitigated>Monitoring", "Monitoring>Resolved", "Resolved>Closed" }, states);
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM it_incidents WHERE id=@i AND resolved_at_utc IS NOT NULL AND closed_at_utc IS NOT NULL AND resolved_by=@r AND closed_by=@c",
            ("i", id), ("r", w.Users["support"].Id), ("c", w.Users["support2"].Id)));
        Assert.Equal(events.Count, await w.EventCount(id));
    }

    public static IEnumerable<object[]> IllegalMoves() =>
    [
        ["New", "Closed"], ["New", "Investigating"], ["New", "Resolved"], ["New", "New"],
        ["New", "Mitigated"], ["New", "Monitoring"], ["New", "triage"], ["New", "Reopened"],
        ["New", "Closed "], ["New", ""], ["New", "Archived"]
    ];

    [Theory]
    [MemberData(nameof(IllegalMoves))]
    public async Task IllegalOrMalformedTransitionsFailWithoutAnySideEffect(string from, string to)
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var (id, v) = await w.CreateIncident(support);
        Assert.Equal("New", from);
        var events = await w.EventCount(id);
        var audit = await w.AuditCount(w.A);
        using var response = await M18World.Send(support, HttpMethod.Post,
            w.Route(null, "/" + id + "/transitions"), Move(v, to));
        Assert.True(response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest,
            $"expected conflict or invalid request but got {(int)response.StatusCode}");
        Assert.Equal(v, await w.Scalar<long>("SELECT version FROM it_incidents WHERE id=@i", ("i", id)));
        Assert.Equal("New", await w.Scalar<string>("SELECT state FROM it_incidents WHERE id=@i", ("i", id)));
        Assert.Equal(events, await w.EventCount(id));
        Assert.Equal(audit, await w.AuditCount(w.A));
    }

    [Fact]
    public async Task MissingReasonAndStaleVersionAreRejectedWithoutChange()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var (id, v) = await w.CreateIncident(support);
        foreach (var body in new object[]
        {
            new { expectedVersion = v, toState = "Triage" },
            new { expectedVersion = v, toState = "Triage", reasonCode = "" },
            new { expectedVersion = v, toState = "Triage", reasonCode = "Has Space" },
            new { expectedVersion = v, toState = "Triage", reasonCode = new string('a', 81) },
            new { expectedVersion = 0L, toState = "Triage", reasonCode = "ok" },
            new { expectedVersion = v, toState = "Triage", reasonCode = "ok", state = "Closed" },
            new { toState = "Triage", reasonCode = "ok" },
        })
        {
            using var r = await M18World.Send(support, HttpMethod.Post,
                w.Route(null, "/" + id + "/transitions"), body);
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }
        using var stale = await M18World.Send(support, HttpMethod.Post,
            w.Route(null, "/" + id + "/transitions"), Move(v + 5, "Triage"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(v, await w.Scalar<long>("SELECT version FROM it_incidents WHERE id=@i", ("i", id)));
        Assert.Equal(1L, await w.EventCount(id));
    }

    [Fact]
    public async Task GatesBlockMitigationWithoutRecordsAndResolutionWithoutRecoveryProof()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var (id, v) = await ToInvestigating(w, support);
        // no note and no corrective action yet
        using (var early = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/transitions"),
            Move(v, "Mitigated")))
            Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        var (id2, v2, action) = await ToMonitoring(w, support);
        // Monitoring to Resolved without recovery evidence and with an open action
        using (var noProof = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id2 + "/transitions"),
            Move(v2, "Resolved")))
            Assert.Equal(HttpStatusCode.Conflict, noProof.StatusCode);
        Assert.Equal("Monitoring", await w.Scalar<string>("SELECT state FROM it_incidents WHERE id=@i", ("i", id2)));
        // recovery with an action still open must not authorise resolution
        v2 = await w.Post(support, id2, "/recovery", new { expectedVersion = v2, rootCauseCode = "pool_exhaustion" });
        using (var open = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id2 + "/transitions"),
            Move(v2, "Resolved")))
            Assert.Equal(HttpStatusCode.Conflict, open.StatusCode);
        Assert.Equal("Monitoring", await w.Scalar<string>("SELECT state FROM it_incidents WHERE id=@i", ("i", id2)));
        Assert.True(action != Guid.Empty);
    }

    [Fact]
    public async Task HighSeverityClosureRequiresADifferentActiveResponder()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var (id, v, action) = await ToMonitoring(w, support, "SEV-1");
        v = await DispositionAction(w, support, id, v, action);
        v = await w.Post(support, id, "/recovery", new { expectedVersion = v, rootCauseCode = "pool_exhaustion" });
        v = await w.Post(support, id, "/transitions", Move(v, "Resolved", "recovery_confirmed"));
        using var self = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/transitions"),
            Move(v, "Closed", "self_close"));
        Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);
        Assert.Equal("Resolved", await w.Scalar<string>("SELECT state FROM it_incidents WHERE id=@i", ("i", id)));
        using var other = await w.Client("support2");
        await w.Post(other, id, "/transitions", Move(v, "Closed", "independently_confirmed"));
        Assert.Equal("Closed", await w.Scalar<string>("SELECT state FROM it_incidents WHERE id=@i", ("i", id)));
    }

    [Fact]
    public async Task ReopenFromResolvedAndClosedKeepsEarlierFactsAndRejectsReplayedVersion()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        using var other = await w.Client("support2");
        var (id, v, action) = await ToMonitoring(w, support);
        v = await DispositionAction(w, support, id, v, action);
        v = await w.Post(support, id, "/recovery", new { expectedVersion = v, rootCauseCode = "pool_exhaustion" });
        v = await w.Post(support, id, "/transitions", Move(v, "Resolved", "recovery_confirmed"));
        v = await w.Post(other, id, "/transitions", Move(v, "Closed", "independently_confirmed"));
        var closedAt = await w.Scalar<DateTime>("SELECT closed_at_utc FROM it_incidents WHERE id=@i", ("i", id));
        var versionBeforeReopen = v;
        v = await w.Post(support, id, "/reopen", Move(v, "Investigating", "regression_observed"));
        Assert.Equal(versionBeforeReopen + 1, v);
        Assert.Equal("Investigating", await w.Scalar<string>("SELECT state FROM it_incidents WHERE id=@i", ("i", id)));
        // duplicate reopen from the old version and a second reopen of a non-closed incident
        using (var replay = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/reopen"),
            Move(versionBeforeReopen, "Investigating", "regression_observed")))
            Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        using (var notClosed = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/reopen"),
            Move(v, "Investigating", "regression_observed")))
            Assert.Equal(HttpStatusCode.Conflict, notClosed.StatusCode);
        var events = (await History(w, support, id)).GetProperty("events").EnumerateArray().ToList();
        Assert.Equal(1, events.Count(e => e.GetProperty("kind").GetString() == "Reopened"));
        // the original closing event stays visible and unchanged
        var closing = Assert.Single(events, e => e.GetProperty("toState").GetString() == "Closed");
        Assert.Equal(w.Users["support2"].Id, closing.GetProperty("actorUserId").GetGuid());
        Assert.True(closedAt > DateTime.UtcNow.AddHours(-1));
    }
}
