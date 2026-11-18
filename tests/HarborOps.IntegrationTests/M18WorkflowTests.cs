using System.Net;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

// G18 assignment, severity, notes, corrective action, pagination, envelope and privacy witnesses.
[Collection("M12ServerRoleIsolation")]
public sealed class M18WorkflowTests
{
    private static async Task SetMembership(M18World w, string user, string role, string status, long version)
    {
        await using var scope = w.Setup.Services.CreateAsyncScope();
        var ids = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var organizations = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var admin = (await ids.LoginAsync(w.Users["admin"].Email, M18World.Password, "m18-members", CancellationToken.None))!;
        Assert.NotNull((await organizations.SelectAsync(admin.UserId, admin.SessionId, w.A, CancellationToken.None)).Value);
        Assert.NotNull((await organizations.PutMemberAsync(admin.UserId, admin.SessionId, w.A, w.Users[user].Id,
            role, status, version, "m18-members", CancellationToken.None)).Value);
    }

    [Fact]
    public async Task AssignmentIsLimitedToActiveSameOrganizationTechnicalResponders()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var (id, v) = await w.CreateIncident(support);
        v = await w.Post(support, id, "/transitions", M18LifecycleTests.Move(v, "Triage"));
        var assign = (Guid to, long version) => new { expectedVersion = version, assignedTo = to, reasonCode = "assign_owner" };
        async Task<HttpStatusCode> Try(Guid to, long version)
        {
            using var r = await M18World.Send(support, HttpMethod.Patch, w.Route(null, "/" + id + "/assignment"), assign(to, version));
            return r.StatusCode;
        }
        var events = await w.EventCount(id);
        foreach (var bad in new[] { w.Users["analyst"].Id, w.Users["manager"].Id, w.Users["auditor"].Id,
            w.Users["supportb"].Id, w.Users["adminb"].Id, Guid.NewGuid() })
            Assert.Equal(HttpStatusCode.Forbidden, await Try(bad, v));
        using (var empty = await M18World.Send(support, HttpMethod.Patch, w.Route(null, "/" + id + "/assignment"),
            new { expectedVersion = v, assignedTo = Guid.Empty, reasonCode = "x" }))
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(events, await w.EventCount(id));
        Assert.Null(await w.Scalar<Guid?>("SELECT assigned_to FROM it_incidents WHERE id=@i", ("i", id)));

        Assert.Equal(HttpStatusCode.OK, await Try(w.Users["support2"].Id, v));
        v += 1;
        var reassignEvents = await w.EventCount(id);
        Assert.Equal(HttpStatusCode.OK, await Try(w.Users["admin"].Id, v));
        v += 1;
        Assert.Equal(reassignEvents + 1, await w.EventCount(id));
        Assert.Equal(w.Users["admin"].Id, await w.Scalar<Guid>("SELECT assigned_to FROM it_incidents WHERE id=@i", ("i", id)));
        Assert.Equal(2L, await w.Scalar<long>("SELECT count(*) FROM it_incident_events WHERE incident_id=@i AND kind='Assigned' AND reason_code='assign_owner'", ("i", id)));
        Assert.Equal(HttpStatusCode.Conflict, await Try(w.Users["support"].Id, v - 1)); // stale

        // the owner must still be eligible when work resumes
        Assert.Equal(HttpStatusCode.OK, await Try(w.Users["support2"].Id, v));
        v += 1;
        await SetMembership(w, "support2", "Support", "Suspended", 1);
        using (var blocked = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/transitions"),
            M18LifecycleTests.Move(v, "Investigating")))
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, await Try(w.Users["support2"].Id, v)); // not assignable while suspended
        Assert.Equal(HttpStatusCode.OK, await Try(w.Users["support"].Id, v));
        v += 1;
        await w.Post(support, id, "/transitions", M18LifecycleTests.Move(v, "Investigating"));
    }

    [Fact]
    public async Task SeverityReassessmentAndImpactReportsAreAuditedVersionedAndBounded()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        using var manager = await w.Client("manager");
        var (id, v) = await w.CreateIncident(support, M18World.NewIncident(severity: "SEV-3"));
        async Task<HttpStatusCode> Reassess(HttpClient client, object body)
        {
            using var r = await M18World.Send(client, HttpMethod.Patch, w.Route(null, "/" + id + "/severity"), body);
            return r.StatusCode;
        }
        // evidence is required before severity may change, and nothing changed on failure
        Assert.Equal(HttpStatusCode.Conflict, await Reassess(support, new { expectedVersion = v, severity = "SEV-1", reasonCode = "reassessed" }));
        v = await w.Post(support, id, "/evidence", new { expectedVersion = v, observationCode = "wider_scope_seen" });
        Assert.Equal(HttpStatusCode.Conflict, await Reassess(support, new { expectedVersion = v, severity = "SEV-3", reasonCode = "same" }));
        Assert.Equal(HttpStatusCode.BadRequest, await Reassess(support, new { expectedVersion = v, severity = "SEV-9", reasonCode = "x" }));
        Assert.Equal(HttpStatusCode.BadRequest, await Reassess(support, new { expectedVersion = v, severity = "SEV-1", reasonCode = "" }));
        Assert.Equal(HttpStatusCode.Forbidden, await Reassess(manager, new { expectedVersion = v, severity = "SEV-1", reasonCode = "no" }));
        Assert.Equal("SEV-3", await w.Scalar<string>("SELECT severity FROM it_incidents WHERE id=@i", ("i", id)));
        Assert.Equal(HttpStatusCode.OK, await Reassess(support, new { expectedVersion = v, severity = "SEV-1", reasonCode = "wider_scope" }));
        v += 1;
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM it_incident_events WHERE incident_id=@i AND kind='SeverityChanged' AND from_state='SEV-3' AND to_state='SEV-1' AND reason_code='wider_scope' AND incident_version=@v", ("i", id), ("v", v)));
        // a Manager may report impact only
        using (var impact = await M18World.Send(manager, HttpMethod.Patch, w.Route(null, "/" + id + "/impact"),
            new { expectedVersion = v, impactCode = "customer_visible", reasonCode = "customer_report" }))
        {
            Assert.Equal(HttpStatusCode.OK, impact.StatusCode);
            Assert.DoesNotContain(w.Users["manager"].Id.ToString(), await impact.Content.ReadAsStringAsync());
        }
        v += 1;
        Assert.Equal("customer_visible", await w.Scalar<string>("SELECT impact_code FROM it_incidents WHERE id=@i", ("i", id)));
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM it_incident_events WHERE incident_id=@i AND kind='ImpactUpdated' AND actor_user_id=@m", ("i", id), ("m", w.Users["manager"].Id)));
    }

    [Fact]
    public async Task CorrectiveActionsHaveVersionedImmutableHistoryAndRejectInvalidMoves()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var (id, v) = await w.CreateIncident(support);
        var other = await w.CreateIncident(support, M18World.NewIncident("Other incident"));
        foreach (var owner in new[] { w.Users["analyst"].Id, w.Users["manager"].Id, w.Users["supportb"].Id, Guid.NewGuid() })
        {
            using var r = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/actions"),
                new { expectedVersion = v, title = "invalid owner", ownerUserId = owner });
            Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        }
        Assert.Equal(0L, await w.Scalar<long>("SELECT count(*) FROM it_incident_actions WHERE incident_id=@i", ("i", id)));
        v = await w.Post(support, id, "/actions", new { expectedVersion = v, title = "Replace pool", ownerUserId = w.Users["support2"].Id });
        var action = await w.Scalar<Guid>("SELECT id FROM it_incident_actions WHERE incident_id=@i", ("i", id));
        object Move(long incident, long actionVersion, string to, Guid? actionId = null) => new
        { expectedVersion = incident, actionId = actionId ?? action, expectedActionVersion = actionVersion, toState = to, reasonCode = "move" };
        async Task<HttpStatusCode> Try(Guid incidentId, Guid route, object body)
        {
            using var r = await M18World.Send(support, HttpMethod.Patch, w.Route(null, "/" + incidentId + "/actions/" + route), body);
            return r.StatusCode;
        }
        Assert.Equal(HttpStatusCode.Conflict, await Try(id, action, Move(v, 1, "Verified")));      // skips InProgress
        Assert.Equal(HttpStatusCode.Conflict, await Try(id, action, Move(v, 7, "InProgress")));    // stale action version
        Assert.Equal(HttpStatusCode.Conflict, await Try(id, action, Move(v + 3, 1, "InProgress"))); // stale incident version
        Assert.Equal(HttpStatusCode.BadRequest, await Try(id, action, Move(v, 1, "inprogress")));
        Assert.Equal(HttpStatusCode.BadRequest, await Try(id, action, Move(v, 1, "Deleted")));
        Assert.Equal(HttpStatusCode.BadRequest, await Try(id, Guid.NewGuid(), Move(v, 1, "InProgress"))); // route/body mismatch
        // an action of one incident cannot be driven through another incident
        Assert.Equal(HttpStatusCode.Conflict, await Try(other.Id, action, Move(other.Version, 1, "InProgress")));
        Assert.Equal(1L, await w.Scalar<long>("SELECT version FROM it_incident_actions WHERE id=@a", ("a", action)));
        Assert.Equal(HttpStatusCode.OK, await Try(id, action, Move(v, 1, "InProgress")));
        v += 1;
        Assert.Equal(HttpStatusCode.OK, await Try(id, action, Move(v, 2, "Cancelled")));
        v += 1;
        Assert.Equal(HttpStatusCode.Conflict, await Try(id, action, Move(v, 3, "InProgress"))); // terminal
        var history = await w.Scalar<string>("SELECT string_agg(action_version||':'||coalesce(previous_state,'-')||'>'||new_state, ',' ORDER BY action_version) FROM it_incident_action_events WHERE action_id=@a", ("a", action));
        Assert.Equal("1:->Proposed,2:Proposed>InProgress,3:InProgress>Cancelled", history);
        Assert.Equal(3L, await w.Scalar<long>("SELECT version FROM it_incident_actions WHERE id=@a", ("a", action)));
        Assert.Equal(0L, await w.Scalar<long>("SELECT count(*) FROM it_incident_action_events WHERE incident_id=@i", ("i", other.Id)));
    }

    [Fact]
    public async Task NotesKeepAuthorshipAndOrderAndAreInvisibleToManagersAnalystsAndOtherTenants()
    {
        using var w = await M18World.CreateAsync(captureSinks: true);
        using var support = await w.Client("support");
        using var other = await w.Client("support2");
        using var manager = await w.Client("manager");
        using var analyst = await w.Client("analyst");
        using var beta = await w.Client("supportb", w.B);
        const string canary = "CANARY-diagnostic-8f21c0";
        var (id, v) = await w.CreateIncident(support, M18World.NewIncident("Notes " + canary[..6]));
        v = await w.Post(support, id, "/notes", new { expectedVersion = v, body = "first " + canary });
        v = await w.Post(other, id, "/notes", new { expectedVersion = v, body = "second <b>markup</b> " + canary });
        var history = await M18LifecycleTests.History(w, support, id);
        var notes = history.GetProperty("notes").EnumerateArray().ToList();
        Assert.Equal(2, notes.Count);
        Assert.Equal(w.Users["support"].Id, notes[0].GetProperty("actorUserId").GetGuid());
        Assert.Equal(w.Users["support2"].Id, notes[1].GetProperty("actorUserId").GetGuid());
        Assert.True(notes[0].GetProperty("occurredAtUtc").GetDateTimeOffset() <= notes[1].GetProperty("occurredAtUtc").GetDateTimeOffset());
        Assert.Equal("second <b>markup</b> " + canary, notes[1].GetProperty("body").GetString());
        foreach (var denied in new[] { manager, analyst })
        {
            using var r = await denied.GetAsync(w.Route(null, "/" + id + "/history"));
            Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
            Assert.DoesNotContain(canary, await r.Content.ReadAsStringAsync());
        }
        using (var foreign = await beta.GetAsync(w.Route(w.B, "/" + id + "/history")))
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using (var managerNote = await M18World.Send(manager, HttpMethod.Post, w.Route(null, "/" + id + "/notes"),
            new { expectedVersion = v, body = "manager note " + canary }))
            Assert.Equal(HttpStatusCode.Forbidden, managerNote.StatusCode);
        // a rejected secret-looking note is neither stored nor echoed
        using (var secret = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/notes"),
            new { expectedVersion = v, body = "password=Sup3rSecretValue " + canary }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, secret.StatusCode);
            Assert.DoesNotContain("Sup3rSecretValue", await secret.Content.ReadAsStringAsync());
        }
        Assert.Equal(2L, await w.Scalar<long>("SELECT count(*) FROM it_incident_notes WHERE incident_id=@i", ("i", id)));

        // the note body appears only in the technical history response: audit rows, logs, metrics and
        // error bodies never carry it
        Assert.Equal(0L, await w.Scalar<long>("SELECT count(*) FROM audit_entries WHERE metadata::text LIKE '%CANARY%' OR event_type LIKE '%CANARY%' OR resource_type LIKE '%CANARY%'"));
        var captured = w.Sinks!.All;
        Assert.True(w.Sinks.Count > 0, "the sink captured nothing, so it proves nothing");
        Assert.DoesNotContain("CANARY-diagnostic", captured);
        Assert.DoesNotContain("Sup3rSecretValue", captured);
        Assert.DoesNotContain(canary[..6], captured.Replace("Notes", ""));
    }

    [Fact]
    public async Task ListAndHistoryPaginationIsStableBoundedAndComplete()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var created = new List<Guid>();
        for (var i = 0; i < 7; i++) created.Add((await w.CreateIncident(support, M18World.NewIncident("Page item " + i))).Id);
        var seen = new List<Guid>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var url = w.Route() + "?limit=3" + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor));
            using var response = await support.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = await M18World.Json(response);
            var items = page.GetProperty("incidents").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
            Assert.True(items.Count <= 3);
            seen.AddRange(items);
            cursor = page.GetProperty("nextCursor").ValueKind == JsonValueKind.Null ? null : page.GetProperty("nextCursor").GetString();
            // changing an earlier item between pages must not duplicate or lose another
            if (pages == 0) await w.Post(support, created[0], "/notes", new { expectedVersion = 1L, body = "touch" });
            pages++;
            Assert.True(pages <= 5);
        } while (cursor is not null);
        Assert.Equal(created.Count, seen.Distinct().Count());
        Assert.Equal(3, pages);
        Assert.Equal(created.OrderBy(x => x), seen.OrderBy(x => x));

        // event history continues without overlap after the first 100 entries
        var (id, v) = await w.CreateIncident(support);
        for (var i = 0; i < 104; i++)
            v = await w.Post(support, id, "/notes", new { expectedVersion = v, body = "bulk " + i });
        var first = await M18LifecycleTests.History(w, support, id);
        Assert.Equal(100, first.GetProperty("events").GetArrayLength());
        var next = first.GetProperty("nextEventAfter").GetInt64();
        Assert.Equal(100, next);
        using var tailResponse = await support.GetAsync(w.Route(null, "/" + id + "/history?afterSeq=" + next));
        var tail = await M18World.Json(tailResponse);
        var tailSeq = tail.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("sequence").GetInt64()).ToList();
        Assert.Equal(Enumerable.Range(101, (int)v - 100).Select(i => (long)i), tailSeq);
        Assert.Equal(JsonValueKind.Null, tail.GetProperty("nextEventAfter").ValueKind);
        Assert.Equal(100, first.GetProperty("notes").GetArrayLength()); // notes are bounded too
    }

    [Fact]
    public async Task ErrorEnvelopesAreConsistentSanitizedAndNeverCached()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        using var analyst = await w.Client("analyst");
        var (id, v) = await w.CreateIncident(support);
        var probes = new List<(HttpStatusCode Expected, HttpResponseMessage Response)>
        {
            (HttpStatusCode.BadRequest, await support.GetAsync(w.Route(null, "/not-a-guid"))),
            (HttpStatusCode.Forbidden, await analyst.GetAsync(w.Route())),
            (HttpStatusCode.NotFound, await support.GetAsync(w.Route(null, "/" + Guid.NewGuid()))),
            (HttpStatusCode.Conflict, await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/transitions"), M18LifecycleTests.Move(v + 9, "Triage"))),
            (HttpStatusCode.BadRequest, await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/transitions"), new { toState = "Triage" })),
            (HttpStatusCode.Forbidden, await M18World.Send(support, HttpMethod.Post, w.Route(), M18World.NewIncident(), csrf: false)),
        };
        foreach (var (expected, response) in probes)
        {
            Assert.Equal(expected, response.StatusCode);
            var correlation = response.Headers.GetValues("X-Correlation-ID").Single();
            var text = await response.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(text);
            Assert.Equal(["code", "correlationId", "message"],
                json.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
            Assert.Equal(correlation, json.RootElement.GetProperty("correlationId").GetString());
            Assert.Matches("^[a-z_]{3,40}$", json.RootElement.GetProperty("code").GetString()!);
            Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? "");
            foreach (var leak in new[] { "Npgsql", "Exception", "at HarborOps", "SELECT ", "it_incident", "stack" })
                Assert.DoesNotContain(leak, text);
            response.Dispose();
        }
        using var unauthenticated = w.Runtime.CreateClient();
        using var anonymous = await unauthenticated.GetAsync(w.Route());
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task TimelineIsIdenticalAfterRestartAndThroughArbitraryFurtherActivity()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var (id, v, action) = await M18LifecycleTests.ToMonitoring(w, support);
        string Canonical(JsonElement history) => string.Join("|", history.GetProperty("events").EnumerateArray()
            .Select(e => $"{e.GetProperty("sequence")}:{e.GetProperty("kind")}:{e.GetProperty("fromState")}>{e.GetProperty("toState")}:{e.GetProperty("reasonCode")}:{e.GetProperty("actorUserId")}:{e.GetProperty("occurredAtUtc")}"));
        var before = Canonical(await M18LifecycleTests.History(w, support, id));
        using var restarted = new ReferenceDataHttpTests.ApiFactory(w.Roles.Api,
            settings: new Dictionary<string, string> { ["M18:EnableIncidents"] = "true", ["M18:RequestsPerMinute"] = "600" });
        using var client = restarted.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        using (var login = await M18World.Send(client, HttpMethod.Post, "/api/v1/auth/login",
            new { email = w.Users["support"].Email, password = M18World.Password }, operationHeader: false))
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await ReferenceDataHttpTests.Select(client, w.A);
        var after = Canonical(await M18LifecycleTests.History(w, client, id));
        Assert.Equal(before, after);
        v = await DispositionAndMore(w, client, id, v, action);
        var extended = Canonical(await M18LifecycleTests.History(w, client, id));
        Assert.StartsWith(before, extended);
        Assert.True(extended.Length > before.Length);
    }

    private static async Task<long> DispositionAndMore(M18World w, HttpClient client, Guid id, long v, Guid action)
    {
        v = await M18LifecycleTests.DispositionAction(w, client, id, v, action);
        return await w.Post(client, id, "/notes", new { expectedVersion = v, body = "post restart note" });
    }
}
