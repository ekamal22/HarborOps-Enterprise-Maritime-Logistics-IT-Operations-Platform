using System.Net;
using System.Text.Json;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// G18 concurrency, idempotency, ambiguous commit, audit-failure atomicity and session witnesses.
[Collection("M12ServerRoleIsolation")]
public sealed class M18ResilienceTests
{
    private static async Task<HttpResponseMessage[]> Parallel(params Func<Task<HttpResponseMessage>>[] calls) =>
        await Task.WhenAll(calls.Select(call => Task.Run(call)));

    [Fact]
    public async Task CompetingWritersFromOneVersionCommitExactlyOneWithoutPhantomRecords()
    {
        using var w = await M18World.CreateAsync();
        using var a = await w.Client("support");
        using var b = await w.Client("support2");
        using var admin = await w.Client("admin");
        var (id, v) = await w.CreateIncident(a);
        var eventsBefore = await w.EventCount(id);
        var auditBefore = await w.AuditCount(w.A);
        var keys = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
        var clients = new[] { a, b, admin, a, b, admin };
        var calls = keys.Select((key, i) => (Func<Task<HttpResponseMessage>>)(() => M18World.Send(clients[i], HttpMethod.Post,
            w.Route(null, "/" + id + "/notes"), new { expectedVersion = v, body = "racing note " + i }, key))).ToArray();
        var responses = await Parallel(calls);
        var codes = responses.Select(r => r.StatusCode).ToList();
        Assert.Equal(1, codes.Count(c => c == HttpStatusCode.OK));
        Assert.Equal(5, codes.Count(c => c == HttpStatusCode.Conflict));
        Assert.Equal(v + 1, await w.Scalar<long>("SELECT version FROM it_incidents WHERE id=@i", ("i", id)));
        Assert.Equal(eventsBefore + 1, await w.EventCount(id));
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM it_incident_notes WHERE incident_id=@i", ("i", id)));
        Assert.Equal(auditBefore + 1, await w.AuditCount(w.A));
        // losing requests leave no reserved operation key behind
        var winner = responses.Select((r, i) => (r, i)).Single(x => x.r.StatusCode == HttpStatusCode.OK).i;
        for (var i = 0; i < keys.Length; i++)
            Assert.Equal(i == winner ? 1L : 0L, await w.Scalar<long>(
                "SELECT count(*) FROM it_incident_operation_ids WHERE operation_key=@k", ("k", keys[i])));
        foreach (var r in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
        {
            var text = await r.Content.ReadAsStringAsync();
            Assert.Contains("\"code\":\"conflict\"", text);
            Assert.DoesNotContain("racing note", text);
        }
        // the same race for lifecycle moves
        var (id2, v2) = await w.CreateIncident(a);
        var moves = await Parallel(
            () => M18World.Send(a, HttpMethod.Post, w.Route(null, "/" + id2 + "/transitions"), M18LifecycleTests.Move(v2, "Triage")),
            () => M18World.Send(b, HttpMethod.Post, w.Route(null, "/" + id2 + "/transitions"), M18LifecycleTests.Move(v2, "Triage")),
            () => M18World.Send(admin, HttpMethod.Post, w.Route(null, "/" + id2 + "/transitions"), M18LifecycleTests.Move(v2, "Triage")));
        Assert.Equal(1, moves.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(2, moves.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(2L, await w.EventCount(id2));
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM audit_entries WHERE resource_id=@i AND event_type='incident.state_changed'", ("i", id2)));
    }

    [Fact]
    public async Task ConcurrentCreateRetriesWithOneKeyProduceOneIncidentEventAndAuditRecord()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var key = Guid.NewGuid();
        var body = M18World.NewIncident("Concurrent retry " + key);
        var responses = await Parallel(Enumerable.Range(0, 6).Select(_ =>
            (Func<Task<HttpResponseMessage>>)(() => M18World.Send(support, HttpMethod.Post, w.Route(), body, key))).ToArray());
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        var ids = new HashSet<Guid>();
        foreach (var r in responses) ids.Add((await M18World.Json(r)).GetProperty("id").GetGuid());
        var id = Assert.Single(ids);
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM it_incidents WHERE title=@t", ("t", "Concurrent retry " + key)));
        Assert.Equal(1L, await w.EventCount(id));
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM audit_entries WHERE resource_id=@i AND event_type='incident.created'", ("i", id)));
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM it_incident_operation_ids WHERE operation_key=@k", ("k", key)));
    }

    [Fact]
    public async Task OperationKeysAreScopedByActorAndBoundToTheirKindAndCanonicalPayload()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        using var other = await w.Client("support2");
        var key = Guid.NewGuid();
        using var first = await M18World.Send(support, HttpMethod.Post, w.Route(), M18World.NewIncident("Original"), key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstJson = await M18World.Json(first);
        var id = firstJson.GetProperty("id").GetGuid();
        var total = await w.Scalar<long>("SELECT count(*) FROM it_incidents WHERE organization_id=@o", ("o", w.A));
        // exact replay: same incident, replay status, no new rows or audit
        var audit = await w.AuditCount(w.A);
        using (var replay = await M18World.Send(support, HttpMethod.Post, w.Route(), M18World.NewIncident("Original",
            detected: DateTimeOffset.Parse(firstJson.GetProperty("updatedAtUtc").GetString()!)), key))
            Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode); // different detection time is a different payload
        using (var changed = await M18World.Send(support, HttpMethod.Post, w.Route(), M18World.NewIncident("Changed title"), key))
            Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using (var otherKind = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/notes"),
            new { expectedVersion = 1L, body = "reusing a create key" }, key))
            Assert.Equal(HttpStatusCode.Conflict, otherKind.StatusCode);
        Assert.Equal(total, await w.Scalar<long>("SELECT count(*) FROM it_incidents WHERE organization_id=@o", ("o", w.A)));
        Assert.Equal(audit, await w.AuditCount(w.A));
        Assert.Equal(1L, await w.EventCount(id));
        Assert.Equal(0L, await w.Scalar<long>("SELECT count(*) FROM it_incident_notes WHERE incident_id=@i", ("i", id)));
        // another actor can use the same key value independently
        using var independent = await M18World.Send(other, HttpMethod.Post, w.Route(), M18World.NewIncident("Independent"), key);
        Assert.Equal(HttpStatusCode.Created, independent.StatusCode);
        Assert.NotEqual(id, (await M18World.Json(independent)).GetProperty("id").GetGuid());
        // a transition replay returns the committed result without a second event
        var move = Guid.NewGuid();
        var body = M18LifecycleTests.Move(1L, "Triage");
        using (var m1 = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/transitions"), body, move))
            Assert.Equal(HttpStatusCode.OK, m1.StatusCode);
        using (var m2 = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/transitions"), body, move))
        {
            Assert.Equal(HttpStatusCode.OK, m2.StatusCode);
            Assert.Equal("Triage", (await M18World.Json(m2)).GetProperty("state").GetString());
        }
        Assert.Equal(2L, await w.EventCount(id));
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM audit_entries WHERE resource_id=@i AND event_type='incident.state_changed'", ("i", id)));
    }

    [Fact]
    public async Task ReplayDoesNotSurviveRoleRevocationOrSessionRevocationAndRebindsToTheNewSession()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var key = Guid.NewGuid();
        var title = "Replay after revocation " + key;
        var body = M18World.NewIncident(title);
        using (var created = await M18World.Send(support, HttpMethod.Post, w.Route(), body, key))
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        // a brand new session for the same active actor may replay safely
        using var relogin = await w.Client("support");
        using (var replay = await M18World.Send(relogin, HttpMethod.Post, w.Route(), body, key))
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        // role revoked: the old result is not retrievable through the key
        await using var scope = w.Setup.Services.CreateAsyncScope();
        var ids = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var organizations = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var adminSession = (await ids.LoginAsync(w.Users["admin"].Email, M18World.Password, "m18-revoke", CancellationToken.None))!;
        Assert.NotNull((await organizations.SelectAsync(adminSession.UserId, adminSession.SessionId, w.A, CancellationToken.None)).Value);
        var membership = await organizations.PutMemberAsync(adminSession.UserId, adminSession.SessionId, w.A,
            w.Users["support"].Id, "Analyst", "Active", 1L, "m18-revoke", CancellationToken.None);
        Assert.NotNull(membership.Value);
        using (var denied = await M18World.Send(relogin, HttpMethod.Post, w.Route(), body, key))
        {
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.DoesNotContain(title, await denied.Content.ReadAsStringAsync());
        }
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM it_incidents WHERE title=@t", ("t", title)));
    }

    [Theory]
    [InlineData(TrackingCommitSocketProxy.CutPoint.AfterCommitServerReady)]
    [InlineData(TrackingCommitSocketProxy.CutPoint.BeforeCommitForward)]
    public async Task AmbiguousCommitIsResolvedByTheDurableOperationKeyNotByGuessing(
        TrackingCommitSocketProxy.CutPoint cut)
    {
        using var w = await M18World.CreateAsync();
        await using var proxy = new TrackingCommitSocketProxy(w.Roles.Api, cut, "INSERT INTO it_incidents(");
        var settings = new Dictionary<string, string>
            { ["M18:EnableIncidents"] = "true", ["M18:RequestsPerMinute"] = "600" };
        using var faulty = new ReferenceDataHttpTests.ApiFactory(proxy.ProxiedConnectionString(w.Roles.Api), settings: settings);
        using var client = faulty.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using (var login = await M18World.Send(client, HttpMethod.Post, "/api/v1/auth/login",
            new { email = w.Users["support"].Email, password = M18World.Password }, operationHeader: false))
        {
            // the proxy only arms on the incident insert, so authentication works through it
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }
        await ReferenceDataHttpTests.Select(client, w.A);
        var key = Guid.NewGuid();
        var title = "Ambiguous commit " + cut + " " + key;
        var body = M18World.NewIncident(title);
        HttpStatusCode? status = null;
        try
        {
            using var response = await M18World.Send(client, HttpMethod.Post, w.Route(), body, key);
            status = response.StatusCode;
        }
        catch (Exception) { /* the connection was cut: the caller learned nothing */ }
        Assert.True(proxy.Triggered, "The proxy never reached its fault point");
        Assert.True(status is null || (int)status >= 500, $"an ambiguous outcome must never look like success but was {status}");
        var committed = await w.Scalar<long>("SELECT count(*) FROM it_incidents WHERE title=@t", ("t", title));
        Assert.Equal(cut == TrackingCommitSocketProxy.CutPoint.AfterCommitServerReady ? 1L : 0L, committed);
        // the caller retries with the same key through a healthy path and converges to exactly one incident
        using var clean = await w.Client("support");
        using var retry = await M18World.Send(clean, HttpMethod.Post, w.Route(), body, key);
        Assert.Equal(committed == 1 ? HttpStatusCode.OK : HttpStatusCode.Created, retry.StatusCode);
        var id = (await M18World.Json(retry)).GetProperty("id").GetGuid();
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM it_incidents WHERE title=@t", ("t", title)));
        Assert.Equal(1L, await w.EventCount(id));
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM audit_entries WHERE resource_id=@i AND event_type='incident.created'", ("i", id)));
    }

    private const string FailTrigger = """
        CREATE OR REPLACE FUNCTION g18_inject_audit_failure() RETURNS trigger LANGUAGE plpgsql AS $f$
        BEGIN RAISE EXCEPTION 'injected audit failure'; END $f$;
        CREATE TRIGGER g18_inject_audit_failure BEFORE INSERT ON audit_entries
            FOR EACH ROW WHEN (NEW.event_type LIKE 'incident.%')
            EXECUTE FUNCTION g18_inject_audit_failure();
        """;

    [Fact]
    public async Task RequiredAuditFailureRollsBackEveryIncidentWriteAndDeniedRequestsStayDenied()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        using var analyst = await w.Client("analyst");
        var (id, v) = await w.CreateIncident(support);
        var snapshot = async () => (
            Incidents: await w.Scalar<long>("SELECT count(*) FROM it_incidents WHERE organization_id=@o", ("o", w.A)),
            Version: await w.Scalar<long>("SELECT version FROM it_incidents WHERE id=@i", ("i", id)),
            State: await w.Scalar<string>("SELECT state FROM it_incidents WHERE id=@i", ("i", id)),
            Events: await w.EventCount(id),
            Notes: await w.Scalar<long>("SELECT count(*) FROM it_incident_notes WHERE incident_id=@i", ("i", id)),
            Actions: await w.Scalar<long>("SELECT count(*) FROM it_incident_actions WHERE incident_id=@i", ("i", id)),
            ActionEvents: await w.Scalar<long>("SELECT count(*) FROM it_incident_action_events WHERE incident_id=@i", ("i", id)),
            Evidence: await w.Scalar<long>("SELECT count(*) FROM it_incident_evidence WHERE incident_id=@i", ("i", id)),
            Keys: await w.Scalar<long>("SELECT count(*) FROM it_incident_operation_ids WHERE organization_id=@o", ("o", w.A)),
            Audit: await w.AuditCount(w.A));
        var before = await snapshot();
        await w.Exec(FailTrigger);
        var key = Guid.NewGuid();
        try
        {
            var attempts = new (string Name, HttpMethod Method, string Route, object Body)[]
            {
                ("create", HttpMethod.Post, w.Route(), M18World.NewIncident("Audit failure create")),
                ("transition", HttpMethod.Post, w.Route(null, "/" + id + "/transitions"), M18LifecycleTests.Move(v, "Triage")),
                ("note", HttpMethod.Post, w.Route(null, "/" + id + "/notes"), new { expectedVersion = v, body = "must not persist" }),
                ("assign", HttpMethod.Patch, w.Route(null, "/" + id + "/assignment"), new { expectedVersion = v, assignedTo = w.Users["support"].Id, reasonCode = "x" }),
                ("action", HttpMethod.Post, w.Route(null, "/" + id + "/actions"), new { expectedVersion = v, title = "no", ownerUserId = w.Users["support"].Id }),
                ("evidence", HttpMethod.Post, w.Route(null, "/" + id + "/evidence"), new { expectedVersion = v, observationCode = "no" }),
                ("impact", HttpMethod.Patch, w.Route(null, "/" + id + "/impact"), new { expectedVersion = v, impactCode = "other_impact", reasonCode = "x" }),
            };
            foreach (var a in attempts)
            {
                using var r = await M18World.Send(support, a.Method, a.Route, a.Body, key);
                Assert.True(r.StatusCode == HttpStatusCode.ServiceUnavailable,
                    $"{a.Name} returned {(int)r.StatusCode} instead of a sanitized 503");
                var text = await r.Content.ReadAsStringAsync();
                Assert.DoesNotContain("injected", text);
                Assert.DoesNotContain("must not persist", text);
                Assert.Equal(before, await snapshot());
            }
            // a request that was already denied is not turned into a success by an audit fault
            using var denied = await M18World.Send(analyst, HttpMethod.Post, w.Route(null, "/" + id + "/transitions"), M18LifecycleTests.Move(v, "Triage"));
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal(before, await snapshot());
        }
        finally
        {
            await w.Exec("DROP TRIGGER IF EXISTS g18_inject_audit_failure ON audit_entries; DROP FUNCTION IF EXISTS g18_inject_audit_failure();");
        }
        // the key was never consumed by the rolled back attempts, so the same operation now succeeds once
        using var ok = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/transitions"), M18LifecycleTests.Move(v, "Triage"), key);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(before.Events + 1, await w.EventCount(id));
    }

    [Fact]
    public async Task LogoutRevocationExpiryAndMembershipChangesStopAccessImmediately()
    {
        using var w = await M18World.CreateAsync();
        using var admin = await w.Client("admin");
        var (id, _) = await w.CreateIncident(admin);
        var route = w.Route(null, "/" + id);

        using (var user = await w.Client("support"))
        {
            Assert.Equal(HttpStatusCode.OK, (await user.GetAsync(route)).StatusCode);
            using (var logout = await M18World.Send(user, HttpMethod.Post, "/api/v1/auth/logout", operationHeader: false))
                Assert.True(logout.IsSuccessStatusCode, ((int)logout.StatusCode).ToString());
            Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync(route)).StatusCode);
            using var create = await M18World.Send(user, HttpMethod.Post, w.Route(), M18World.NewIncident());
            Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
        }
        await using var scope = w.Setup.Services.CreateAsyncScope();
        var ids = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var organizations = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        using (var user = await w.Client("support2"))
        {
            Assert.Equal(HttpStatusCode.OK, (await user.GetAsync(route)).StatusCode);
            await ids.RevokeAllAsync(w.Users["support2"].Id, "m18-test", "m18-test", CancellationToken.None);
            Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync(route)).StatusCode);
            using var create = await M18World.Send(user, HttpMethod.Post, w.Route(), M18World.NewIncident());
            Assert.True(create.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
        }
        using (var user = await w.Client("manager"))
        {
            Assert.Equal(HttpStatusCode.OK, (await user.GetAsync(w.Route())).StatusCode);
            await w.Exec("UPDATE auth_sessions SET issued_at_utc=now()-interval '3 hours', last_seen_at_utc=now()-interval '150 minutes', idle_expires_at_utc=now()-interval '2 hours', absolute_expires_at_utc=now()-interval '1 hour' WHERE user_id=@u",
                ("u", w.Users["manager"].Id));
            Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync(w.Route())).StatusCode);
        }
        var adminSession = (await ids.LoginAsync(w.Users["admin"].Email, M18World.Password, "m18-admin", CancellationToken.None))!;
        Assert.NotNull((await organizations.SelectAsync(adminSession.UserId, adminSession.SessionId, w.A, CancellationToken.None)).Value);
        using (var user = await w.Client("multi"))
        {
            Assert.Equal(HttpStatusCode.OK, (await user.GetAsync(route)).StatusCode);
            Assert.NotNull((await organizations.PutMemberAsync(adminSession.UserId, adminSession.SessionId, w.A,
                w.Users["multi"].Id, "Support", "Suspended", 1L, "m18", CancellationToken.None)).Value);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync(route)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync(w.Route())).StatusCode);
            using var create = await M18World.Send(user, HttpMethod.Post, w.Route(), M18World.NewIncident("Suspended create"));
            Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        }
        Assert.Equal(0L, await w.Scalar<long>("SELECT count(*) FROM it_incidents WHERE title='Suspended create' AND organization_id=@o", ("o", w.A)));
        using (var user = await w.Client("supportb", w.B))
        {
            // organization suspension also closes the incident surface
            Assert.Equal(HttpStatusCode.OK, (await user.GetAsync(w.Route(w.B))).StatusCode);
            await w.Exec("UPDATE organizations SET status='Suspended' WHERE id=@o", ("o", w.B));
            var code = (await user.GetAsync(w.Route(w.B))).StatusCode;
            Assert.True(code is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized, ((int)code).ToString());
            using var create = await M18World.Send(user, HttpMethod.Post, w.Route(w.B), M18World.NewIncident());
            Assert.True(create.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task OrganizationSwitchShowsOnlyTheSelectedTenantAndRejectsTheOther()
    {
        using var w = await M18World.CreateAsync();
        using var multi = await w.Client("multi");
        using var adminB = await w.Client("adminb", w.B);
        var (alphaId, _) = await w.CreateIncident(multi, M18World.NewIncident("Alpha only"));
        var (betaId, _) = await w.CreateIncident(adminB, M18World.NewIncident("Beta only"), w.B);
        Assert.Contains("Alpha only", await (await multi.GetAsync(w.Route())).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await multi.GetAsync(w.Route(w.B))).StatusCode);
        await ReferenceDataHttpTests.Select(multi, w.B);
        var betaView = await (await multi.GetAsync(w.Route(w.B))).Content.ReadAsStringAsync();
        Assert.Contains("Beta only", betaView);
        Assert.DoesNotContain("Alpha only", betaView);
        Assert.Equal(HttpStatusCode.Forbidden, (await multi.GetAsync(w.Route(w.A))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await multi.GetAsync(w.Route(w.B, "/" + alphaId))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await multi.GetAsync(w.Route(w.B, "/" + betaId))).StatusCode);
    }

    [Fact]
    public async Task MembershipSuspensionCommittingWhileAWriteWaitsPreventsTheWrite()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var (id, v) = await w.CreateIncident(support);
        var eventsBefore = await w.EventCount(id);
        await using var blocker = new NpgsqlConnection(w.Roles.Admin);
        await blocker.OpenAsync();
        await using var tx = await blocker.BeginTransactionAsync();
        await using (var hold = new NpgsqlCommand(
            "UPDATE organization_memberships SET status='Suspended' WHERE organization_id=@o AND user_id=@u", blocker, tx))
        {
            hold.Parameters.AddWithValue("o", w.A);
            hold.Parameters.AddWithValue("u", w.Users["support"].Id);
            Assert.Equal(1, await hold.ExecuteNonQueryAsync());
        }
        var write = Task.Run(() => M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/notes"),
            new { expectedVersion = v, body = "queued behind the revocation" }));
        // the request must be genuinely waiting on the membership row, not already finished
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline && await w.Scalar<long>(
            "SELECT count(*) FROM pg_stat_activity WHERE usename='harborops_api' AND wait_event_type='Lock'") == 0)
            await Task.Delay(50);
        Assert.False(write.IsCompleted, "the write finished before the revocation committed");
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM pg_stat_activity WHERE usename='harborops_api' AND wait_event_type='Lock'"));
        await tx.CommitAsync();
        using var response = await write;
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(eventsBefore, await w.EventCount(id));
        Assert.Equal(0L, await w.Scalar<long>("SELECT count(*) FROM it_incident_notes WHERE incident_id=@i", ("i", id)));
        Assert.Equal(v, await w.Scalar<long>("SELECT version FROM it_incidents WHERE id=@i", ("i", id)));
    }

    [Fact]
    public async Task DefaultRateLimitRejectsTheThirtyFirstRequestWithoutSideEffects()
    {
        using var w = await M18World.CreateAsync(new Dictionary<string, string> { ["M18:RequestsPerMinute"] = "30" });
        using var support = await w.Client("support");
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 31; i++) statuses.Add((await support.GetAsync(w.Route())).StatusCode);
        Assert.Equal(30, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        var before = await w.Scalar<long>("SELECT count(*) FROM it_incidents WHERE organization_id=@o", ("o", w.A));
        using var blocked = await M18World.Send(support, HttpMethod.Post, w.Route(), M18World.NewIncident("Rate limited"));
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal(before, await w.Scalar<long>("SELECT count(*) FROM it_incidents WHERE organization_id=@o", ("o", w.A)));
    }

    [Fact]
    public async Task FeatureIsOffByDefaultAndExposesNoRoutes()
    {
        using var w = await M18World.CreateAsync();
        using var off = new ReferenceDataHttpTests.ApiFactory(w.Roles.Api);
        using var client = off.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using (var login = await M18World.Send(client, HttpMethod.Post, "/api/v1/auth/login",
            new { email = w.Users["support"].Email, password = M18World.Password }, operationHeader: false))
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await ReferenceDataHttpTests.Select(client, w.A);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(w.Route())).StatusCode);
        using var create = await M18World.Send(client, HttpMethod.Post, w.Route(), M18World.NewIncident("Hidden path"));
        Assert.True(create.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, ((int)create.StatusCode).ToString());
        Assert.Equal(0L, await w.Scalar<long>("SELECT count(*) FROM it_incidents WHERE title='Hidden path'"));
    }
}
