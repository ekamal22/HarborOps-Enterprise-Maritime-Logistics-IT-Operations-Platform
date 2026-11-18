using System.Net;
using System.Text.Json;
using HarborOps.Api.Incidents;
using HarborOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

// G18 recovery honesty, M17 evidence, prefill and degraded-telemetry witnesses.
[Collection("M12ServerRoleIsolation")]
public sealed class M18EvidenceTests
{
    private static async Task Ready(M18World w, HttpClient client) =>
        await client.GetAsync("/health/ready");

    private static async Task RemoveBaseline(M18World w) =>
        await w.Exec("DELETE FROM bootstrap_records");

    private static async Task RestoreBaseline(M18World w)
    {
        await using var scope = w.Setup.Services.CreateAsyncScope();
        await BootstrapSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>(), CancellationToken.None);
    }

    [Fact]
    public async Task RecoveryIsAServerWitnessedServiceSpecificProbeAndNeverACallerClaim()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var (id, v, action) = await M18LifecycleTests.ToMonitoring(w, support);
        v = await M18LifecycleTests.DispositionAction(w, support, id, v, action);
        var recovery = w.Route(null, "/" + id + "/recovery");

        // a caller cannot supply the outcome
        foreach (var body in new object[]
        {
            new { expectedVersion = v, rootCauseCode = "x", recoveryVerified = true },
            new { expectedVersion = v, rootCauseCode = "x", integrityVerified = true },
            new { expectedVersion = v, rootCauseCode = "x", healthy = true },
            new { expectedVersion = v, rootCauseCode = "Bad Code" },
            new { expectedVersion = v },
        })
        {
            using var r = await M18World.Send(support, HttpMethod.Post, recovery, body);
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }
        // an incident that is not under observation cannot record recovery
        var (fresh, freshVersion) = await w.CreateIncident(support);
        using (var early = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + fresh + "/recovery"),
            new { expectedVersion = freshVersion, rootCauseCode = "pool_exhaustion" }))
            Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);

        // the live probe fails while the baseline is missing: no evidence, no flags
        await RemoveBaseline(w);
        try
        {
            using var failing = await M18World.Send(support, HttpMethod.Post, recovery,
                new { expectedVersion = v, rootCauseCode = "pool_exhaustion" });
            Assert.Equal(HttpStatusCode.Conflict, failing.StatusCode);
            Assert.Contains("recovery_not_verified", await failing.Content.ReadAsStringAsync());
        }
        finally { await RestoreBaseline(w); }
        Assert.Equal(0L, await w.Scalar<long>("SELECT count(*) FROM it_incident_evidence WHERE incident_id=@i AND kind='RecoveryProbe'", ("i", id)));
        Assert.False(await w.Scalar<bool>("SELECT recovery_verified OR integrity_verified FROM it_incidents WHERE id=@i", ("i", id)));
        Assert.Equal(v, await w.Scalar<long>("SELECT version FROM it_incidents WHERE id=@i", ("i", id)));

        // once the dependency really is healthy the server records exactly its own witnesses
        v = await w.Post(support, id, "/recovery", new { expectedVersion = v, rootCauseCode = "pool_exhaustion" });
        var codes = new List<string>();
        await using (var connection = new Npgsql.NpgsqlConnection(w.Roles.Admin))
        {
            await connection.OpenAsync();
            await using var command = new Npgsql.NpgsqlCommand("SELECT observation_code,source_status,correlation_digest,service_code FROM it_incident_evidence WHERE incident_id=@i AND kind='RecoveryProbe' ORDER BY observation_code", connection);
            command.Parameters.AddWithValue("i", id);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                codes.Add(reader.GetString(0));
                Assert.Equal("healthy", reader.GetString(1));
                Assert.Matches("^[0-9a-f]{32}$", reader.GetString(2));
                Assert.Equal("database", reader.GetString(3));
            }
        }
        Assert.Equal(["database_readiness_healthy", "incident_history_integrity_verified"], codes);

        // service coverage: api records its own readiness code, other services have no verifiable probe
        using var otherSupport = await w.Client("support2");
        var api = await M18LifecycleTests.ToMonitoring(w, support, "SEV-3", "api");
        await w.Post(support, api.Id, "/recovery", new { expectedVersion = api.Version, rootCauseCode = "api_restart" });
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM it_incident_evidence WHERE incident_id=@i AND observation_code='api_readiness_healthy'", ("i", api.Id)));
        foreach (var service in new[] { "notifications", "tracking_held" })
        {
            var unsupported = await M18LifecycleTests.ToMonitoring(w, support, "SEV-3", service);
            using var r = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + unsupported.Id + "/recovery"),
                new { expectedVersion = unsupported.Version, rootCauseCode = "x" });
            Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
            Assert.Equal(0L, await w.Scalar<long>("SELECT count(*) FROM it_incident_evidence WHERE incident_id=@i AND kind='RecoveryProbe'", ("i", unsupported.Id)));
        }
    }

    [Fact]
    public async Task ForgedFlagsAndAnOldHealthySnapshotCannotResolveAnIncident()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var (id, v, action) = await M18LifecycleTests.ToMonitoring(w, support);
        v = await M18LifecycleTests.DispositionAction(w, support, id, v, action);
        await Ready(w, support);
        v = await w.Post(support, id, "/m17-snapshot", new { expectedVersion = v });
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM it_incident_evidence WHERE incident_id=@i AND kind='M17Snapshot'", ("i", id)));
        // worst case: someone with write access sets every recovery flag directly
        await w.Exec("UPDATE it_incidents SET recovery_verified=true,integrity_verified=true,actions_dispositioned=true,root_cause_code='claimed',version=version+1 WHERE id=@i", ("i", id));
        v += 1;
        using var resolve = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/transitions"), M18LifecycleTests.Move(v, "Resolved"));
        Assert.Equal(HttpStatusCode.Conflict, resolve.StatusCode);
        Assert.Equal("Monitoring", await w.Scalar<string>("SELECT state FROM it_incidents WHERE id=@i", ("i", id)));
        // and the database guard refuses a state change that skips verified recovery
        await using var connection = new Npgsql.NpgsqlConnection(w.Roles.Admin);
        await connection.OpenAsync();
        await using var skip = new Npgsql.NpgsqlCommand("UPDATE it_incidents SET recovery_verified=false,state='Resolved',resolved_by=@u,resolved_at_utc=now(),version=version+1 WHERE id=@i", connection);
        skip.Parameters.AddWithValue("i", id);
        skip.Parameters.AddWithValue("u", w.Users["support"].Id);
        var failure = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => skip.ExecuteNonQueryAsync());
        Assert.Equal(Npgsql.PostgresErrorCodes.CheckViolation, failure.SqlState);
    }

    [Fact]
    public async Task RecoveryEvidenceExpiresBeforeResolutionAndMustBeRecordedAgain()
    {
        using var w = await M18World.CreateAsync(new Dictionary<string, string> { ["M18:RecoveryFreshnessSeconds"] = "3" });
        using var support = await w.Client("support");
        var (id, v, action) = await M18LifecycleTests.ToMonitoring(w, support);
        v = await M18LifecycleTests.DispositionAction(w, support, id, v, action);
        v = await w.Post(support, id, "/recovery", new { expectedVersion = v, rootCauseCode = "pool_exhaustion" });
        await Task.Delay(TimeSpan.FromSeconds(4));
        using (var stale = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/transitions"), M18LifecycleTests.Move(v, "Resolved")))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("Monitoring", await w.Scalar<string>("SELECT state FROM it_incidents WHERE id=@i", ("i", id)));
        v = await w.Post(support, id, "/recovery", new { expectedVersion = v, rootCauseCode = "pool_exhaustion" });
        await w.Post(support, id, "/transitions", M18LifecycleTests.Move(v, "Resolved"));
        Assert.Equal("Resolved", await w.Scalar<string>("SELECT state FROM it_incidents WHERE id=@i", ("i", id)));
    }

    [Fact]
    public async Task RegressionAfterRecoveryClearsTheVerificationAndTheNextResolutionNeedsNewProof()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        var (id, v, action) = await M18LifecycleTests.ToMonitoring(w, support);
        v = await M18LifecycleTests.DispositionAction(w, support, id, v, action);
        v = await w.Post(support, id, "/recovery", new { expectedVersion = v, rootCauseCode = "pool_exhaustion" });
        v = await w.Post(support, id, "/transitions", M18LifecycleTests.Move(v, "Investigating", "regression_observed"));
        Assert.False(await w.Scalar<bool>("SELECT recovery_verified OR integrity_verified FROM it_incidents WHERE id=@i", ("i", id)));
        Assert.Null(await w.Scalar<string?>("SELECT root_cause_code FROM it_incidents WHERE id=@i", ("i", id)));
        v = await w.Post(support, id, "/notes", new { expectedVersion = v, body = "second mitigation attempt" });
        v = await w.Post(support, id, "/transitions", M18LifecycleTests.Move(v, "Mitigated"));
        v = await w.Post(support, id, "/evidence", new { expectedVersion = v, observationCode = "second_attempt" });
        v = await w.Post(support, id, "/transitions", M18LifecycleTests.Move(v, "Monitoring"));
        using var resolve = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + id + "/transitions"), M18LifecycleTests.Move(v, "Resolved"));
        Assert.Equal(HttpStatusCode.Conflict, resolve.StatusCode);
        v = await w.Post(support, id, "/recovery", new { expectedVersion = v, rootCauseCode = "pool_exhaustion" });
        await w.Post(support, id, "/transitions", M18LifecycleTests.Move(v, "Resolved"));
    }

    [Fact]
    public async Task PrefillNeverCreatesAnIncidentAndOnlyAHumanConfirmedServerTokenStoresEvidence()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        using var other = await w.Client("support2");
        using var manager = await w.Client("manager");
        using var analyst = await w.Client("analyst");
        using var beta = await w.Client("supportb", w.B);
        await Ready(w, support);
        var count = () => w.Scalar<long>("SELECT count(*) FROM it_incidents");
        var before = await count();
        var prefillUrl = w.Route(null, "/m17-prefill");
        using (var read = await support.GetAsync(prefillUrl))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            var json = await M18World.Json(read);
            Assert.Equal("database", json.GetProperty("serviceCode").GetString());
            Assert.Equal("healthy", json.GetProperty("status").GetString());
            Assert.True(json.GetProperty("statusIsCurrent").GetBoolean());
            Assert.Equal(before, await count());
            var token = json.GetProperty("prefillToken").GetString()!;
            var observed = json.GetProperty("observedAtUtc").GetDateTimeOffset();

            // forged, copied, tampered or mismatched references create and link nothing
            var body = (string t, string service = "database") => new
            { title = "Prefilled", serviceCode = service, severity = "SEV-2", impactCode = "reduced_capacity",
              detectedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"), prefillToken = t };
            foreach (var (client, route, forged) in new[]
            {
                (support, w.Route(), "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"),
                (support, w.Route(), token[..^4] + (token.EndsWith("AAAA") ? "BBBB" : "AAAA")),
                (other, w.Route(), token),
                (beta, w.Route(w.B), token),
                (support, w.Route(), "x"),
            })
            {
                using var r = await M18World.Send(client, HttpMethod.Post, route, body(forged));
                Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            }
            using (var mismatch = await M18World.Send(support, HttpMethod.Post, w.Route(), body(token, "api")))
                Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
            using (var smuggled = await M18World.Send(support, HttpMethod.Post, w.Route(), new
            { title = "x", serviceCode = "database", severity = "SEV-2", impactCode = "x",
              detectedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"), status = "healthy", observedAtUtc = observed }))
                Assert.Equal(HttpStatusCode.BadRequest, smuggled.StatusCode);
            Assert.Equal(before, await count());

            var key = Guid.NewGuid();
            var confirmed = body(token);
            using var created = await M18World.Send(support, HttpMethod.Post, w.Route(), confirmed, key);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var id = (await M18World.Json(created)).GetProperty("id").GetGuid();
            Assert.Equal(before + 1, await count());
            Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM it_incident_evidence WHERE incident_id=@i AND kind='M17Snapshot' AND source_status='healthy' AND service_code='database' AND correlation_digest ~ '^[0-9a-f]{32}$'", ("i", id)));
            var stored = await w.Scalar<DateTime>("SELECT observed_at_utc FROM it_incident_evidence WHERE incident_id=@i", ("i", id));
            Assert.True(Math.Abs((stored - observed.UtcDateTime).TotalMilliseconds) < 1, "server observation time was not preserved");
            using (var replay = await M18World.Send(support, HttpMethod.Post, w.Route(), confirmed, key))
                Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM it_incident_evidence WHERE incident_id=@i", ("i", id)));
            Assert.Equal(1L, await w.EventCount(id));
        }
        using (var managerRead = await manager.GetAsync(prefillUrl))
            Assert.Equal(HttpStatusCode.OK, managerRead.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.GetAsync(prefillUrl)).StatusCode);
        foreach (var query in new[] { "?service=api", "?service=database&x=1", "?service=database&service=api" })
            Assert.Equal(HttpStatusCode.BadRequest, (await support.GetAsync(prefillUrl + query)).StatusCode);
    }

    [Fact]
    public async Task PrefillTokenIsShortLivedAndBoundToOrganizationUserAndSession()
    {
        var codec = new IncidentPrefillCodec(new EphemeralDataProtectionProvider());
        var org = Guid.NewGuid(); var user = Guid.NewGuid(); var session = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var (fresh, _) = codec.Encode(org, user, session, "database", "healthy", now.AddSeconds(-5), now);
        Assert.True(codec.TryDecode(fresh, org, user, session, now.AddMinutes(1), out var prefill));
        Assert.Equal("database", prefill!.ServiceCode);
        Assert.False(codec.TryDecode(fresh, org, user, session, now.AddMinutes(11), out _));
        Assert.False(codec.TryDecode(fresh, Guid.NewGuid(), user, session, now, out _));
        Assert.False(codec.TryDecode(fresh, org, Guid.NewGuid(), session, now, out _));
        Assert.False(codec.TryDecode(fresh, org, user, Guid.NewGuid(), now, out _));
        var (future, _) = codec.Encode(org, user, session, "database", "healthy", now.AddMinutes(10), now);
        Assert.False(codec.TryDecode(future, org, user, session, now, out _));
        var (badStatus, _) = codec.Encode(org, user, session, "database", "green", now, now);
        Assert.False(codec.TryDecode(badStatus, org, user, session, now, out _));
        var (badService, _) = codec.Encode(org, user, session, "notifications", "healthy", now, now);
        Assert.False(codec.TryDecode(badService, org, user, session, now, out _));
        Assert.False(new IncidentPrefillCodec(new EphemeralDataProtectionProvider())
            .TryDecode(fresh, org, user, session, now, out _));
    }

    [Fact]
    public async Task MissingOrStaleTelemetryIsUnknownAndDegradedIsDegradedNeverHealthy()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        await RemoveBaseline(w);
        try
        {
            using var notReady = await support.GetAsync("/health/ready");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, notReady.StatusCode);
            using var prefill = await support.GetAsync(w.Route(null, "/m17-prefill"));
            var json = await M18World.Json(prefill);
            Assert.Equal("degraded", json.GetProperty("status").GetString());
        }
        finally { await RestoreBaseline(w); }
        var (id, v) = await w.CreateIncident(support);
        v = await w.Post(support, id, "/m17-snapshot", new { expectedVersion = v });
        Assert.Equal("degraded", await w.Scalar<string>("SELECT source_status FROM it_incident_evidence WHERE incident_id=@i", ("i", id)));

        // a sample older than its freshness window reads as unknown, and CRUD keeps working without any exporter
        await Task.Delay(TimeSpan.FromSeconds(47));
        using var stale = await support.GetAsync(w.Route(null, "/m17-prefill"));
        var staleJson = await M18World.Json(stale);
        Assert.Equal("unknown", staleJson.GetProperty("status").GetString());
        Assert.False(staleJson.GetProperty("statusIsCurrent").GetBoolean());
        v = await w.Post(support, id, "/m17-snapshot", new { expectedVersion = v });
        Assert.Equal(1L, await w.Scalar<long>("SELECT count(*) FROM it_incident_evidence WHERE incident_id=@i AND source_status='unknown' AND kind='M17Snapshot'", ("i", id)));
        // an unknown observation is not a recovery: the incident cannot be resolved from it
        var other = await M18LifecycleTests.ToMonitoring(w, support);
        using var snapshot = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + other.Id + "/m17-snapshot"), new { expectedVersion = other.Version });
        Assert.Equal(HttpStatusCode.OK, snapshot.StatusCode);
        Assert.Equal(0L, await w.Scalar<long>("SELECT count(*) FROM it_incident_evidence WHERE incident_id=@i AND kind='RecoveryProbe'", ("i", other.Id)));
        using var create = await M18World.Send(support, HttpMethod.Post, w.Route(), M18World.NewIncident("Created with no telemetry"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
    }

    [Fact]
    public async Task ServiceCategoriesAreExactlyTheFourApprovedCodes()
    {
        using var w = await M18World.CreateAsync();
        using var support = await w.Client("support");
        foreach (var service in new[] { "api", "database", "notifications", "tracking_held" })
            await w.CreateIncident(support, M18World.NewIncident("Service " + service, service));
        foreach (var service in new[] { "tracking", "queue", "", "API", "api ", "tracking_held2", "../api", "m17" })
        {
            using var r = await M18World.Send(support, HttpMethod.Post, w.Route(), M18World.NewIncident("bad service", service));
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }
        // a snapshot is only meaningful for services M17 actually observes
        var (held, v) = await w.CreateIncident(support, M18World.NewIncident("Held", "tracking_held"));
        using var unsupported = await M18World.Send(support, HttpMethod.Post, w.Route(null, "/" + held + "/m17-snapshot"), new { expectedVersion = v });
        Assert.Equal(HttpStatusCode.Conflict, unsupported.StatusCode);
    }
}
