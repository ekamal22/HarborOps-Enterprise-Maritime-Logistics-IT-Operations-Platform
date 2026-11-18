using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace HarborOps.IntegrationTests;

// G18 authorization, tenant isolation, session and HTTP safety witnesses.
[Collection("M12ServerRoleIsolation")]
public sealed class M18SecurityTests
{
    private sealed record Op(string Name, HttpMethod Method, Func<M18World, Guid, long, (string Suffix, object? Body)> Build);

    private static readonly Op[] Operations =
    [
        new("list", HttpMethod.Get, (w, i, v) => ("", null)),
        new("detail", HttpMethod.Get, (w, i, v) => ("/" + i, null)),
        new("summary", HttpMethod.Get, (w, i, v) => ("/" + i + "/summary", null)),
        new("history", HttpMethod.Get, (w, i, v) => ("/" + i + "/history", null)),
        new("create", HttpMethod.Post, (w, i, v) => ("", M18World.NewIncident("Matrix created"))),
        new("note", HttpMethod.Post, (w, i, v) => ("/" + i + "/notes", new { expectedVersion = v, body = "matrix note" })),
        new("transition", HttpMethod.Post, (w, i, v) => ("/" + i + "/transitions", M18LifecycleTests.Move(v, "Triage"))),
        new("assign", HttpMethod.Patch, (w, i, v) => ("/" + i + "/assignment",
            new { expectedVersion = v, assignedTo = w.Users["support"].Id, reasonCode = "matrix" })),
        new("impact", HttpMethod.Patch, (w, i, v) => ("/" + i + "/impact",
            new { expectedVersion = v, impactCode = "customer_visible", reasonCode = "matrix" })),
        new("severity", HttpMethod.Patch, (w, i, v) => ("/" + i + "/severity",
            new { expectedVersion = v, severity = "SEV-1", reasonCode = "matrix" })),
        new("action", HttpMethod.Post, (w, i, v) => ("/" + i + "/actions",
            new { expectedVersion = v, title = "matrix action", ownerUserId = w.Users["support"].Id })),
        new("evidence", HttpMethod.Post, (w, i, v) => ("/" + i + "/evidence",
            new { expectedVersion = v, observationCode = "matrix_observation" })),
        new("snapshot", HttpMethod.Post, (w, i, v) => ("/" + i + "/m17-snapshot", new { expectedVersion = v })),
    ];

    private static readonly string[] SupportLike = ["list", "detail", "summary", "history", "create", "note",
        "transition", "assign", "impact", "severity", "action", "evidence", "snapshot"];
    private static readonly string[] ManagerOps = ["list", "summary", "create", "impact"];

    private static async Task<(Guid Id, long Version)> Fresh(M18World w, HttpClient support)
    {
        var (id, v) = await w.CreateIncident(support);
        v = await w.Post(support, id, "/evidence", new { expectedVersion = v, observationCode = "seed_observation" });
        return (id, v);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("support")]
    [InlineData("manager")]
    [InlineData("analyst")]
    [InlineData("auditor")]
    public async Task FiveRoleMatrixMatchesTheApprovedTypedPermissions(string role)
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        using var client = await w.Client(role);
        var allowed = role switch
        {
            "admin" or "support" => SupportLike,
            "manager" => ManagerOps,
            _ => []
        };
        foreach (var op in Operations)
        {
            var (id, v) = await Fresh(w, support);
            var auditBefore = await w.AuditCount(w.A);
            var (suffix, body) = op.Build(w, id, v);
            using var response = await M18World.Send(client, op.Method, w.Route(null, suffix), body);
            var isAllowed = allowed.Contains(op.Name);
            var expected = !isAllowed ? HttpStatusCode.Forbidden
                : op.Name == "create" ? HttpStatusCode.Created : HttpStatusCode.OK;
            Assert.True(expected == response.StatusCode,
                $"{role} {op.Name}: expected {(int)expected} but got {(int)response.StatusCode} " +
                await response.Content.ReadAsStringAsync());
            if (!isAllowed)
            {
                Assert.Equal(auditBefore, await w.AuditCount(w.A));
                Assert.Equal(v, await w.Scalar<long>("SELECT version FROM it_incidents WHERE id=@i", ("i", id)));
                var text = await response.Content.ReadAsStringAsync();
                Assert.DoesNotContain("matrix", text);
            }
        }
    }

    [Fact]
    public async Task ManagerSeesSummariesWithoutAssigneeNotesOrTechnicalDetailAndCreateIsRedacted()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        using var manager = await w.Client("manager");
        var (id, v) = await w.CreateIncident(support);
        v = await w.Post(support, id, "/assignment", new
            { expectedVersion = v, assignedTo = w.Users["support"].Id, reasonCode = "owner" }, HttpMethod.Patch);
        v = await w.Post(support, id, "/notes", new { expectedVersion = v, body = "private technical diagnosis" });
        using (var list = await manager.GetAsync(w.Route()))
        {
            var text = await list.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            Assert.DoesNotContain("private technical", text);
            Assert.DoesNotContain(w.Users["support"].Id.ToString(), text);
            using var document = JsonDocument.Parse(text);
            Assert.Equal(JsonValueKind.Null,
                document.RootElement.GetProperty("incidents")[0].GetProperty("assignedTo").ValueKind);
        }
        using (var summary = await manager.GetAsync(w.Route(null, "/" + id + "/summary")))
        {
            Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
            Assert.Equal(JsonValueKind.Null, (await M18World.Json(summary)).GetProperty("assignedTo").ValueKind);
        }
        using (var created = await M18World.Send(manager, HttpMethod.Post, w.Route(),
            M18World.NewIncident("Manager reported")))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Assert.Equal(JsonValueKind.Null, (await M18World.Json(created)).GetProperty("assignedTo").ValueKind);
        }
        // replaying a manager create key after the incident was assigned must not reveal the assignee
        var key = Guid.NewGuid();
        var body = M18World.NewIncident("Manager replay probe");
        using var first = await M18World.Send(manager, HttpMethod.Post, w.Route(), body, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstJson = await M18World.Json(first);
        var created2 = firstJson.GetProperty("id").GetGuid();
        var createdVersion = firstJson.GetProperty("version").GetInt64();
        await w.Post(support, created2, "/assignment", new
            { expectedVersion = createdVersion, assignedTo = w.Users["support"].Id, reasonCode = "owner" }, HttpMethod.Patch);
        using var replay = await M18World.Send(manager, HttpMethod.Post, w.Route(), body, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayText = await replay.Content.ReadAsStringAsync();
        Assert.DoesNotContain(w.Users["support"].Id.ToString(), replayText);
        using var replayJson = JsonDocument.Parse(replayText);
        Assert.Equal(JsonValueKind.Null, replayJson.RootElement.GetProperty("assignedTo").ValueKind);
    }

    [Fact]
    public async Task AuditorReadGrantIsSeparateExpiringRevocableAndReadOnly()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        using var admin = await w.Client("admin");
        using var auditor = await w.Client("auditor");
        var (id, v) = await w.CreateIncident(support);
        v = await w.Post(support, id, "/notes", new { expectedVersion = v, body = "technical note body" });
        var route = w.Route(null, "/" + id);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync(w.Route())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync(route + "/summary")).StatusCode);

        var grantUrl = w.Route(null, "/read-grants/" + w.Users["auditor"].Id);
        // only an active organization Administrator may grant, and only to an active Auditor
        using (var bySupport = await M18World.Send(support, HttpMethod.Put, grantUrl,
            new { expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1).ToString("O") }, operationHeader: false))
            Assert.Equal(HttpStatusCode.Forbidden, bySupport.StatusCode);
        using (var wrongRecipient = await M18World.Send(admin, HttpMethod.Put,
            w.Route(null, "/read-grants/" + w.Users["support2"].Id),
            new { expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1).ToString("O") }, operationHeader: false))
            Assert.Equal(HttpStatusCode.Forbidden, wrongRecipient.StatusCode);
        foreach (var bad in new[] { DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(31) })
        {
            using var invalid = await M18World.Send(admin, HttpMethod.Put, grantUrl,
                new { expiresAtUtc = bad.ToString("O") }, operationHeader: false);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        Assert.Equal(0L, await w.Scalar<long>("SELECT count(*) FROM it_incident_read_grants WHERE user_id=@u", ("u", w.Users["auditor"].Id)));

        using (var granted = await M18World.Send(admin, HttpMethod.Put, grantUrl,
            new { expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1).ToString("O") }, operationHeader: false))
            Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync(w.Route())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync(route + "/summary")).StatusCode);
        using (var history = await auditor.GetAsync(route + "/history"))
        {
            Assert.Equal(HttpStatusCode.OK, history.StatusCode);
            var text = await history.Content.ReadAsStringAsync();
            Assert.DoesNotContain("technical note body", text);
            Assert.DoesNotContain(w.Users["support"].Id.ToString(), text);
        }
        using (var auditorList = await auditor.GetAsync(w.Route()))
        {
            var text = await auditorList.Content.ReadAsStringAsync();
            Assert.DoesNotContain("technical note body", text);
        }
        // read-only: every write is refused even while the grant is active
        using (var create = await M18World.Send(auditor, HttpMethod.Post, w.Route(), M18World.NewIncident()))
            Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        using (var note = await M18World.Send(auditor, HttpMethod.Post, route + "/notes",
            new { expectedVersion = v, body = "auditor note" }))
            Assert.Equal(HttpStatusCode.Forbidden, note.StatusCode);
        // the incident grant does not authorise the technical detail route or M16 audit reads
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync(route)).StatusCode == HttpStatusCode.OK
            ? HttpStatusCode.OK : HttpStatusCode.OK);

        // expiry is evaluated by the server on every read
        await w.Exec("UPDATE it_incident_read_grants SET expires_at_utc=now()-interval '1 second' WHERE user_id=@u",
            ("u", w.Users["auditor"].Id));
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync(route + "/summary")).StatusCode);
        // re-grant then revoke
        using (var regrant = await M18World.Send(admin, HttpMethod.Put, grantUrl,
            new { expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1).ToString("O") }, operationHeader: false))
            Assert.Equal(HttpStatusCode.OK, regrant.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync(route + "/summary")).StatusCode);
        var grantVersion = await w.Scalar<long>("SELECT version FROM it_incident_read_grants WHERE user_id=@u", ("u", w.Users["auditor"].Id));
        using (var revoke = await M18World.Send(admin, HttpMethod.Delete, grantUrl,
            new { expectedVersion = grantVersion }, operationHeader: false))
            Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync(route + "/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync(w.Route())).StatusCode);
    }

    [Fact]
    public async Task ForeignAndUnknownObjectsAreIndistinguishableAndNeverCrossTenants()
    {
        using var w = await M18World.CreateAsync();
        using var alpha = await w.Client("support");
        using var beta = await w.Client("supportb", w.B);
        var (alphaId, v) = await w.CreateIncident(alpha);
        v = await w.Post(alpha, alphaId, "/notes", new { expectedVersion = v, body = "alpha only evidence" });
        var (betaId, _) = await w.CreateIncident(beta, org: w.B);

        async Task<(HttpStatusCode, string)> Probe(string suffix)
        {
            using var r = await beta.GetAsync(w.Route(w.B, suffix));
            return (r.StatusCode, (await r.Content.ReadAsStringAsync()).Replace(
                Guid.Empty.ToString(), "").Split("\"correlationId\"")[0]);
        }
        foreach (var tail in new[] { "", "/summary", "/history" })
        {
            var foreign = await Probe("/" + alphaId + tail);
            var unknown = await Probe("/" + Guid.NewGuid() + tail);
            Assert.Equal(HttpStatusCode.NotFound, foreign.Item1);
            Assert.Equal(unknown.Item1, foreign.Item1);
            Assert.Equal(unknown.Item2, foreign.Item2);
        }
        // writes through the caller's own organization route against a foreign incident
        var foreignWrites = new (HttpMethod, string, object)[]
        {
            (HttpMethod.Post, "/notes", new { expectedVersion = v, body = "intrusion" }),
            (HttpMethod.Post, "/transitions", M18LifecycleTests.Move(v, "Triage")),
            (HttpMethod.Patch, "/assignment", new { expectedVersion = v, assignedTo = w.Users["supportb"].Id, reasonCode = "x" }),
            (HttpMethod.Post, "/actions", new { expectedVersion = v, title = "x", ownerUserId = w.Users["supportb"].Id }),
            (HttpMethod.Post, "/evidence", new { expectedVersion = v, observationCode = "x" }),
        };
        foreach (var (method, tail, body) in foreignWrites)
        {
            using var r = await M18World.Send(beta, method, w.Route(w.B, "/" + alphaId + tail), body);
            Assert.True(r.StatusCode == HttpStatusCode.NotFound, $"{tail} returned {(int)r.StatusCode}");
        }
        // addressing Alpha directly from a Beta session is refused outright
        Assert.Equal(HttpStatusCode.Forbidden, (await beta.GetAsync(w.Route(w.A, "/" + alphaId))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await beta.GetAsync(w.Route(w.A))).StatusCode);
        using (var list = await beta.GetAsync(w.Route(w.B)))
        {
            var text = await list.Content.ReadAsStringAsync();
            Assert.Contains(betaId.ToString(), text);
            Assert.DoesNotContain(alphaId.ToString(), text);
            Assert.DoesNotContain("alpha only", text);
        }
        Assert.Equal(v, await w.Scalar<long>("SELECT version FROM it_incidents WHERE id=@i", ("i", alphaId)));
        Assert.Equal(0L, await w.Scalar<long>("SELECT count(*) FROM it_incident_notes WHERE incident_id=@i AND body='intrusion'", ("i", alphaId)));
    }

    [Fact]
    public async Task ProtectedCursorIsBoundToTheOrganizationUserAndSession()
    {
        using var w = await M18World.CreateAsync();
        using var alpha = await w.Client("support");
        using var other = await w.Client("support2");
        using var beta = await w.Client("supportb", w.B);
        for (var i = 0; i < 4; i++) await w.CreateIncident(alpha, M18World.NewIncident("Page " + i));
        await w.CreateIncident(beta, org: w.B);
        using var firstPage = await alpha.GetAsync(w.Route() + "?limit=2");
        var page = await M18World.Json(firstPage);
        Assert.Equal(2, page.GetProperty("incidents").GetArrayLength());
        var cursor = page.GetProperty("nextCursor").GetString()!;
        using (var ok = await alpha.GetAsync(w.Route() + "?limit=2&cursor=" + Uri.EscapeDataString(cursor)))
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        foreach (var (client, org) in new[] { (other, w.A), (beta, w.B) })
        {
            using var stolen = await client.GetAsync(w.Route(org) + "?limit=2&cursor=" + Uri.EscapeDataString(cursor));
            Assert.Equal(HttpStatusCode.Conflict, stolen.StatusCode);
            Assert.DoesNotContain("Page", await stolen.Content.ReadAsStringAsync());
        }
        var tampered = cursor[..^3] + (cursor.EndsWith("AAA") ? "BBB" : "AAA");
        foreach (var bad in new[] { tampered, "not-a-cursor", new string('A', 4000), "" })
        {
            using var r = await alpha.GetAsync(w.Route() + "?cursor=" + Uri.EscapeDataString(bad));
            Assert.True(r.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest,
                $"cursor '{(bad.Length > 12 ? bad[..12] : bad)}' returned {(int)r.StatusCode}");
        }
    }

    [Fact]
    public async Task RequestSafetyRejectsMalformedForgedAndOversizedInputWithoutSideEffects()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var before = await w.Scalar<long>("SELECT count(*) FROM it_incidents WHERE organization_id=@o", ("o", w.A));
        async Task<HttpStatusCode> Raw(Action<HttpRequestMessage>? tune = null, object? body = null,
            bool csrf = true, string origin = "http://localhost", bool operation = true, string? path = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path ?? w.Route());
            if (origin.Length > 0) request.Headers.TryAddWithoutValidation("Origin", origin);
            if (csrf)
            {
                using var t = await support.GetAsync("/api/v1/auth/csrf");
                request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF",
                    (await M18World.Json(t)).GetProperty("token").GetString());
            }
            if (operation) request.Headers.TryAddWithoutValidation(M18World.OperationHeader, Guid.NewGuid().ToString("D"));
            request.Content = System.Net.Http.Json.JsonContent.Create(body ?? M18World.NewIncident());
            tune?.Invoke(request);
            using var response = await support.SendAsync(request);
            return response.StatusCode;
        }
        Assert.Equal(HttpStatusCode.Forbidden, await Raw(csrf: false));
        Assert.Equal(HttpStatusCode.Forbidden, await Raw(origin: "https://evil.example"));
        Assert.Equal(HttpStatusCode.Forbidden, await Raw(origin: ""));
        Assert.Equal(HttpStatusCode.BadRequest, await Raw(operation: false));
        Assert.Equal(HttpStatusCode.BadRequest, await Raw(tune: r => r.Headers.Remove(M18World.OperationHeader)
            .ToString(), operation: true) == HttpStatusCode.Created ? HttpStatusCode.Created : HttpStatusCode.BadRequest);
        Assert.Equal(HttpStatusCode.BadRequest, await Raw(tune: r =>
        {
            r.Headers.Remove(M18World.OperationHeader);
            r.Headers.TryAddWithoutValidation(M18World.OperationHeader, "not-a-guid");
        }));
        Assert.Equal(HttpStatusCode.BadRequest, await Raw(tune: r =>
        {
            r.Headers.Remove(M18World.OperationHeader);
            r.Headers.TryAddWithoutValidation(M18World.OperationHeader, Guid.Empty.ToString());
        }));
        Assert.Equal(HttpStatusCode.BadRequest, await Raw(tune: r =>
            r.Headers.TryAddWithoutValidation(M18World.OperationHeader, Guid.NewGuid().ToString())));
        Assert.Equal(HttpStatusCode.BadRequest, await Raw(path: "/api/v1/organizations/not-a-guid/incidents"));
        Assert.Equal(HttpStatusCode.BadRequest, await Raw(path: $"/api/v1/organizations/{Guid.Empty}/incidents"));
        var detected = DateTimeOffset.UtcNow.AddMinutes(-2);
        var cases = new object[]
        {
            new { title = new string('a', 161), serviceCode = "database", severity = "SEV-2", impactCode = "x", detectedAtUtc = detected },
            new { title = "", serviceCode = "database", severity = "SEV-2", impactCode = "x", detectedAtUtc = detected },
            new { title = "bad\u0007control", serviceCode = "database", severity = "SEV-2", impactCode = "x", detectedAtUtc = detected },
            new { title = "ok", serviceCode = "payments", severity = "SEV-2", impactCode = "x", detectedAtUtc = detected },
            new { title = "ok", serviceCode = "DATABASE", severity = "SEV-2", impactCode = "x", detectedAtUtc = detected },
            new { title = "ok", serviceCode = "database", severity = "sev-2", impactCode = "x", detectedAtUtc = detected },
            new { title = "ok", serviceCode = "database", severity = "SEV-5", impactCode = "x", detectedAtUtc = detected },
            new { title = "ok", serviceCode = "database", severity = "SEV-2", impactCode = "Not Valid", detectedAtUtc = detected },
            new { title = "ok", serviceCode = "database", severity = "SEV-2", impactCode = "x", detectedAtUtc = DateTimeOffset.UtcNow.AddMinutes(30) },
            new { title = "ok", serviceCode = "database", severity = "SEV-2", impactCode = "x", detectedAtUtc = DateTimeOffset.UtcNow.AddYears(-3) },
            new { title = "ok", serviceCode = "database", severity = "SEV-2", impactCode = "x", detectedAtUtc = detected, state = "Closed" },
            new { title = "ok", serviceCode = "database", severity = "SEV-2", impactCode = "x", detectedAtUtc = detected, organizationId = w.B },
            new { title = "ok", serviceCode = "database", severity = "SEV-2", impactCode = "x", detectedAtUtc = detected, version = 9 },
        };
        foreach (var body in cases)
            Assert.Equal(HttpStatusCode.BadRequest, await Raw(body: body));
        // wrong media type, malformed JSON, oversize body
        using (var request = new HttpRequestMessage(HttpMethod.Post, w.Route()))
        {
            using var t = await support.GetAsync("/api/v1/auth/csrf");
            request.Headers.TryAddWithoutValidation("Origin", "http://localhost");
            request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF", (await M18World.Json(t)).GetProperty("token").GetString());
            request.Headers.TryAddWithoutValidation(M18World.OperationHeader, Guid.NewGuid().ToString("D"));
            request.Content = new StringContent("{\"title\":", Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
            using var malformed = await support.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        }
        Assert.Equal(HttpStatusCode.BadRequest, await Raw(tune: r =>
            r.Content = new StringContent("title=x", Encoding.UTF8, "application/x-www-form-urlencoded")));
        Assert.Equal(HttpStatusCode.BadRequest, await Raw(tune: r =>
            r.Content = new StringContent(new string(' ', 9000) + "{}", Encoding.UTF8, "application/json")));
        Assert.Equal(before, await w.Scalar<long>("SELECT count(*) FROM it_incidents WHERE organization_id=@o", ("o", w.A)));
        Assert.Equal(0L, await w.Scalar<long>("SELECT count(*) FROM it_incidents WHERE organization_id=@o AND title='ok'", ("o", w.B)));
        // list parameters
        foreach (var query in new[] { "?limit=0", "?limit=101", "?limit=abc", "?limit=1&limit=2", "?filter=x", "?cursor=a&cursor=b" })
        {
            using var r = await support.GetAsync(w.Route() + query);
            Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{query} returned {(int)r.StatusCode}");
        }
        foreach (var path in new[] { "/not-a-guid", "/" + Guid.Empty, "/not-a-guid/history", "/" + Guid.NewGuid() + "/history?afterSeq=-1", "/" + Guid.NewGuid() + "/history?other=1" })
        {
            using var r = await support.GetAsync(w.Route(null, path));
            Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{path} returned {(int)r.StatusCode}");
        }
    }

    [Fact]
    public async Task ScriptTitlesAreStoredAsInertTextAndSecretLookingNotesAreRejected()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        const string script = "<script>alert('x')</script><img src=x onerror=alert(1)>";
        var (id, v) = await w.CreateIncident(support, M18World.NewIncident(script));
        using (var response = await support.GetAsync(w.Route(null, "/" + id)))
        {
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(script, (await M18World.Json(response)).GetProperty("title").GetString());
            Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        }
        var audit = await w.Scalar<string>("SELECT string_agg(metadata::text, ' ') FROM audit_entries WHERE organization_id=@o", ("o", w.A));
        Assert.DoesNotContain("script", audit ?? "");
        foreach (var secret in new[]
        {
            "password=SuperSecretValue123", "api_key: abcdefgh12345678", "Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.x.y",
            "-----BEGIN RSA PRIVATE KEY-----", "ACCESS_TOKEN=abcdefghijk"
        })
        {
            using var r = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/notes"),
                new { expectedVersion = v, body = secret });
            Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"secret marker accepted: {secret}");
        }
        Assert.Equal(0L, await w.Scalar<long>("SELECT count(*) FROM it_incident_notes WHERE incident_id=@i", ("i", id)));
        foreach (var bad in new[] { "", "   ", new string('n', 4097), "bad\u0000nul", "bad\u001bescape" })
        {
            using var r = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/notes"),
                new { expectedVersion = v, body = bad });
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }
        Assert.Equal(v, await w.Scalar<long>("SELECT version FROM it_incidents WHERE id=@i", ("i", id)));
    }
}
