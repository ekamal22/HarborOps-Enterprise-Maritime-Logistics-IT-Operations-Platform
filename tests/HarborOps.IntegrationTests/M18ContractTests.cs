using System.Net;
using Xunit;

namespace HarborOps.IntegrationTests;

// G18 creation record, event field, generic mutation, evidence input and exception witnesses.
[Collection("M12ServerRoleIsolation")]
public sealed class M18ContractTests
{
    [Fact]
    public async Task CreatePersistsANewUtcIncidentWithTheRequestedServiceAndSeverityForEverySeverityAndService()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        foreach (var service in new[] { "api", "database", "notifications", "tracking_held" })
            foreach (var severity in new[] { "SEV-1", "SEV-2", "SEV-3", "SEV-4" })
            {
                var detected = DateTimeOffset.UtcNow.AddMinutes(-7);
                var before = DateTimeOffset.UtcNow;
                var (id, version) = await w.CreateIncident(support,
                    M18World.NewIncident("Create " + service + severity, service, severity, "minor_issue", detected));
                Assert.Equal(1L, version);
                var row = await w.Scalar<string>("SELECT concat_ws('|',state,service_code,severity,version,impact_code,created_by,organization_id,assigned_to IS NULL,resolved_at_utc IS NULL,closed_at_utc IS NULL,recovery_verified,integrity_verified) FROM it_incidents WHERE id=@i", ("i", id));
                Assert.Equal($"New|{service}|{severity}|1|minor_issue|{w.Users["support"].Id}|{w.A}|t|t|t|f|f", row);
                var created = await w.Scalar<DateTime>("SELECT created_at_utc FROM it_incidents WHERE id=@i", ("i", id));
                Assert.InRange(created, before.UtcDateTime.AddSeconds(-5), DateTime.UtcNow.AddSeconds(5));
                Assert.Equal(detected.UtcDateTime, await w.Scalar<DateTime>("SELECT detected_at_utc FROM it_incidents WHERE id=@i", ("i", id)), TimeSpan.FromMilliseconds(1));
                Assert.Equal("Created|New|1", await w.Scalar<string>("SELECT concat_ws('|',kind,to_state,sequence) FROM it_incident_events WHERE incident_id=@i", ("i", id)));
            }
    }

    [Fact]
    public async Task EveryTransitionEventRecordsExactStatesActorServerTimeAndIncrementedVersion()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        using var other = await w.Client("support2");
        var (id, v, action) = await M18LifecycleTests.ToMonitoring(w, support);
        v = await M18LifecycleTests.DispositionAction(w, support, id, v, action);
        v = await w.Post(support, id, "/recovery", new { expectedVersion = v, rootCauseCode = "pool_exhaustion" });
        v = await w.Post(support, id, "/transitions", M18LifecycleTests.Move(v, "Resolved"));
        v = await w.Post(other, id, "/transitions", M18LifecycleTests.Move(v, "Closed"));
        var history = await M18LifecycleTests.History(w, support, id);
        var events = history.GetProperty("events").EnumerateArray().ToList();
        var last = DateTimeOffset.MinValue;
        foreach (var e in events)
        {
            var at = e.GetProperty("occurredAtUtc").GetDateTimeOffset();
            Assert.Equal(TimeSpan.Zero, at.Offset);
            Assert.True(at >= last, "event time must not go backwards");
            Assert.True(at <= DateTimeOffset.UtcNow.AddSeconds(5));
            last = at;
        }
        var transitions = events.Where(e => e.GetProperty("kind").GetString() == "Transitioned").ToList();
        Assert.Equal(6, transitions.Count);
        var versions = await w.Scalar<string>("SELECT string_agg(sequence::text||':'||incident_version, ',' ORDER BY sequence) FROM it_incident_events WHERE incident_id=@i", ("i", id));
        Assert.Equal(string.Join(",", Enumerable.Range(1, (int)v).Select(i => $"{i}:{i}")), versions);
        Assert.Equal(w.Users["support"].Id, transitions[0].GetProperty("actorUserId").GetGuid());
        Assert.Equal(w.Users["support2"].Id, transitions[^1].GetProperty("actorUserId").GetGuid());
        var resolvedEvent = transitions.Single(e => e.GetProperty("toState").GetString() == "Resolved");
        var closedEvent = transitions.Single(e => e.GetProperty("toState").GetString() == "Closed");
        var resolvedAt = await w.Scalar<DateTime>("SELECT resolved_at_utc FROM it_incidents WHERE id=@i", ("i", id));
        var closedAt = await w.Scalar<DateTime>("SELECT closed_at_utc FROM it_incidents WHERE id=@i", ("i", id));
        Assert.Equal(resolvedEvent.GetProperty("occurredAtUtc").GetDateTimeOffset().UtcDateTime, resolvedAt, TimeSpan.FromMilliseconds(1));
        Assert.Equal(closedEvent.GetProperty("occurredAtUtc").GetDateTimeOffset().UtcDateTime, closedAt, TimeSpan.FromMilliseconds(1));
        Assert.True(resolvedAt <= closedAt);
        Assert.Equal(v, history.GetProperty("incident").GetProperty("version").GetInt64());
        Assert.Equal("Closed", history.GetProperty("incident").GetProperty("state").GetString());
    }

    [Fact]
    public async Task NoGenericWritePathExistsForStateActorVersionOrClosureFields()
    {
        using var w = await M18World.CreateAsync();
        using var admin = await w.Client("admin");
        var (id, v) = await w.CreateIncident(admin);
        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete, HttpMethod.Post })
        {
            using var r = await M18World.Send(admin, method, w.Route(null, "/" + id),
                new { state = "Closed", version = 99, createdBy = Guid.NewGuid(), closedBy = Guid.NewGuid() });
            Assert.True(r.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, $"{method} returned {(int)r.StatusCode}");
        }
        foreach (var (suffix, method, body) in new (string, HttpMethod, object)[]
        {
            ("/transitions", HttpMethod.Post, new { expectedVersion = v, toState = "Triage", reasonCode = "x", actorUserId = Guid.NewGuid() }),
            ("/transitions", HttpMethod.Post, new { expectedVersion = v, toState = "Triage", reasonCode = "x", version = 50 }),
            ("/transitions", HttpMethod.Post, new { expectedVersion = v, toState = "Triage", reasonCode = "x", closedBy = Guid.NewGuid() }),
            ("/notes", HttpMethod.Post, new { expectedVersion = v, body = "x", actorUserId = Guid.NewGuid() }),
            ("/assignment", HttpMethod.Patch, new { expectedVersion = v, assignedTo = w.Users["admin"].Id, reasonCode = "x", organizationId = w.B }),
            ("/severity", HttpMethod.Patch, new { expectedVersion = v, severity = "SEV-1", reasonCode = "x", state = "Resolved" }),
        })
        {
            using var r = await M18World.Send(admin, method, w.Route(null, "/" + id + suffix), body);
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }
        Assert.Equal("New|1|1", await w.Scalar<string>("SELECT concat_ws('|',state,version,(SELECT count(*) FROM it_incident_events WHERE incident_id=@i)) FROM it_incidents WHERE id=@i", ("i", id)));
    }

    [Fact]
    public async Task EvidenceAcceptsOnlyBoundedCodesAndNeverRawTracesUrlsIdentifiersOrFreeText()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var (id, v) = await w.CreateIncident(support);
        var route = w.Route(null, "/" + id + "/evidence");
        foreach (var bad in new object[]
        {
            new { expectedVersion = v, observationCode = "http://grafana.internal/d/abc?trace=1" },
            new { expectedVersion = v, observationCode = "traceparent:00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01" },
            new { expectedVersion = v, observationCode = "user@example.test" },
            new { expectedVersion = v, observationCode = new string('a', 81) },
            new { expectedVersion = v, observationCode = "Has Space" },
            new { expectedVersion = v, observationCode = "" },
            new { expectedVersion = v, observationCode = "ok_code", traceId = "4bf92f3577b34da6a3ce929d0e0e4736" },
            new { expectedVersion = v, observationCode = "ok_code", url = "http://x" },
            new { expectedVersion = v, observationCode = "ok_code", sourceStatus = "healthy" },
            new { expectedVersion = v, observationCode = "ok_code", organizationId = w.B },
        })
        {
            using var r = await M18World.Send(support, HttpMethod.Post, route, bad);
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }
        Assert.Equal(0L, await w.Scalar<long>("SELECT count(*) FROM it_incident_evidence WHERE incident_id=@i", ("i", id)));
        await w.Post(support, id, "/evidence", new { expectedVersion = v, observationCode = "operator_saw_errors" });
        Assert.Equal("ManualObservation|unknown", await w.Scalar<string>("SELECT kind||'|'||source_status FROM it_incident_evidence WHERE incident_id=@i", ("i", id)));
        foreach (var bad in new object[]
        {
            new { expectedVersion = v + 1, status = "healthy" }, new { expectedVersion = v + 1, observedAtUtc = DateTimeOffset.UtcNow },
            new { expectedVersion = v + 1, traceId = "x" }, new { expectedVersion = v + 1, url = "http://x" }
        })
        {
            using var r = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/m17-snapshot"), bad);
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }
    }

    [Fact]
    public async Task ClosureHasNoExceptionPathSoAnUnauthorizedExceptionChangesNothing()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var (id, v, action) = await M18LifecycleTests.ToMonitoring(w, support, "SEV-1");
        v = await M18LifecycleTests.DispositionAction(w, support, id, v, action);
        v = await w.Post(support, id, "/recovery", new { expectedVersion = v, rootCauseCode = "pool_exhaustion" });
        v = await w.Post(support, id, "/transitions", M18LifecycleTests.Move(v, "Resolved"));
        foreach (var body in new object[]
        {
            new { expectedVersion = v, toState = "Closed", reasonCode = "x", exception = true },
            new { expectedVersion = v, toState = "Closed", reasonCode = "x", approvedException = "single_responder" },
            new { expectedVersion = v, toState = "Closed", reasonCode = "x", independentlyConfirmed = true },
            new { expectedVersion = v, toState = "Closed", reasonCode = "x", confirmedBy = w.Users["support2"].Id },
        })
        {
            using var r = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/transitions"), body);
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }
        using var plain = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/transitions"), M18LifecycleTests.Move(v, "Closed"));
        Assert.Equal(HttpStatusCode.Conflict, plain.StatusCode);
        Assert.Equal("Resolved", await w.Scalar<string>("SELECT state FROM it_incidents WHERE id=@i", ("i", id)));
        Assert.Equal(v, await w.EventCount(id));
        using var analyst = await w.Client("analyst");
        using var denied = await M18World.Send(analyst, HttpMethod.Post, w.Route(null, "/" + id + "/transitions"), M18LifecycleTests.Move(v, "Closed"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task AdministratorSeesAuditorGrantsAndNobodyElseCan()
    {
        using var w = await M18World.CreateAsync();
        using var admin = await w.Client("admin");
        foreach (var user in new[] { "support", "manager", "analyst", "auditor" })
        {
            using var client = await w.Client(user);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(w.Route(null, "/read-grants"))).StatusCode);
        }
        using (var empty = await admin.GetAsync(w.Route(null, "/read-grants")))
            Assert.Equal(0, (await M18World.Json(empty)).GetProperty("grants").GetArrayLength());
        using (var granted = await M18World.Send(admin, HttpMethod.Put, w.Route(null, "/read-grants/" + w.Users["auditor"].Id),
            new { expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1).ToString("O") }, operationHeader: false))
            Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        using var listed = await admin.GetAsync(w.Route(null, "/read-grants"));
        var grant = (await M18World.Json(listed)).GetProperty("grants")[0];
        Assert.Equal(w.Users["auditor"].Id, grant.GetProperty("userId").GetGuid());
        Assert.True(grant.GetProperty("effective").GetBoolean());
        using var adminB = await w.Client("adminb", w.B);
        using var other = await adminB.GetAsync(w.Route(w.B, "/read-grants"));
        Assert.Equal(0, (await M18World.Json(other)).GetProperty("grants").GetArrayLength());
    }
}
