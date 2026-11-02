using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

// G5-S21: shipment mutations that are interrupted after database work has started.
//
// Every scenario pins the API's PostgreSQL backend at a known point with an advisory-lock barrier
// that the test itself holds. A trigger scoped to one shipment reference blocks on that lock after
// the shipment row and its history row have been written inside the open transaction, so the
// interruption always lands between "work done" and "commit". The barrier is observed through
// pg_locks and pg_stat_activity rather than inferred from elapsed time.
public sealed class ShipmentInterruptionTests
{
    private static string Root(Guid org) => $"/api/v1/organizations/{org}/shipments";
    private static readonly string[] Forbidden =
    {
        "Npgsql", "Postgres", "57P01", "57014", "terminat", "trigger", "g5_barrier", "advisory",
        "SELECT ", "UPDATE ", "INSERT ", "   at ", "shipment_status_changes", "Exception"
    };

    private sealed record Row(long Version, string Workflow, DateTime UpdatedAt);
    private sealed record Change(long Version, string? From, string To);
    private sealed record Backend(bool Exists, bool HasTransactionId, string? WaitType, string? WaitEvent);

    private sealed class Probe(string connection) : IAsyncDisposable
    {
        private readonly NpgsqlDataSource source = NpgsqlDataSource.Create(connection);

        public async Task<Row?> Shipment(Guid id)
        {
            await using var cmd = source.CreateCommand(
                "SELECT version, workflow_status, updated_at_utc FROM shipments WHERE id = @id");
            cmd.Parameters.AddWithValue("id", id);
            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync()
                ? new Row(reader.GetInt64(0), reader.GetString(1), reader.GetDateTime(2)) : null;
        }

        public async Task<List<Change>> History(Guid id)
        {
            await using var cmd = source.CreateCommand(
                "SELECT version, from_status, to_status FROM shipment_status_changes WHERE shipment_id = @id ORDER BY version");
            cmd.Parameters.AddWithValue("id", id);
            await using var reader = await cmd.ExecuteReaderAsync();
            var rows = new List<Change>();
            while (await reader.ReadAsync())
                rows.Add(new Change(reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2)));
            return rows;
        }

        public async Task<(long Shipments, long History)> ByReference(Guid org, string reference)
        {
            await using var cmd = source.CreateCommand(
                "SELECT (SELECT count(*) FROM shipments WHERE organization_id = @o AND reference_code = @r), " +
                "(SELECT count(*) FROM shipment_status_changes c JOIN shipments s ON s.id = c.shipment_id " +
                " WHERE s.organization_id = @o AND s.reference_code = @r)");
            cmd.Parameters.AddWithValue("o", org);
            cmd.Parameters.AddWithValue("r", reference);
            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            return (reader.GetInt64(0), reader.GetInt64(1));
        }

        public async Task<Backend> Inspect(int pid)
        {
            await using var cmd = source.CreateCommand(
                "SELECT backend_xid IS NOT NULL, wait_event_type, wait_event FROM pg_stat_activity WHERE pid = @p");
            cmd.Parameters.AddWithValue("p", pid);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return new Backend(false, false, null, null);
            return new Backend(true, reader.GetBoolean(0),
                reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2));
        }

        public async Task<bool> RowLockedByAnotherTransaction(Guid id)
        {
            await using var connection = await source.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var cmd = new NpgsqlCommand("SELECT 1 FROM shipments WHERE id = @id FOR UPDATE NOWAIT", connection, transaction);
            cmd.Parameters.AddWithValue("id", id);
            try { await cmd.ExecuteScalarAsync(); return false; }
            catch (PostgresException ex) when (ex.SqlState == "55P03") { return true; }
        }

        public async Task Terminate(int pid)
        {
            await using var cmd = source.CreateCommand("SELECT pg_terminate_backend(@p)");
            cmd.Parameters.AddWithValue("p", pid);
            Assert.True((bool)(await cmd.ExecuteScalarAsync())!);
        }

        public async Task<bool> WaitUntilGone(int pid, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (!(await Inspect(pid)).Exists) return true;
                await Task.Delay(100);
            }
            return false;
        }

        // True once the backend has left the lock wait and holds no write transaction: it either
        // ended, or it was cancelled, rolled back and returned to the connection pool.
        public async Task<bool> WaitUntilReleased(int pid, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var backend = await Inspect(pid);
                if (!backend.Exists || (!backend.HasTransactionId && backend.WaitType != "Lock")) return true;
                await Task.Delay(100);
            }
            return false;
        }

        public ValueTask DisposeAsync() => source.DisposeAsync();
    }

    // A scoped fault point: after the history row for one reference code is inserted, block on an
    // advisory lock that the test holds. Other shipments and other tests are never affected.
    private sealed class Barrier : IAsyncDisposable
    {
        private readonly string connection;
        private readonly string name = "g5_barrier_" + Guid.NewGuid().ToString("N")[..10];
        private NpgsqlConnection? holder;
        private bool held;
        public int Key { get; } = Random.Shared.Next(1000, int.MaxValue);

        private Barrier(string connection) => this.connection = connection;

        public static async Task<Barrier> Install(string connection, string reference)
        {
            var barrier = new Barrier(connection);
            barrier.holder = new NpgsqlConnection(connection);
            await barrier.holder.OpenAsync();
            await using (var lockCommand = new NpgsqlCommand("SELECT pg_advisory_lock(" + barrier.Key + ")", barrier.holder))
                await lockCommand.ExecuteNonQueryAsync();
            barrier.held = true;
            await using var ddl = new NpgsqlConnection(connection);
            await ddl.OpenAsync();
            var sql = "CREATE FUNCTION " + barrier.name + "() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN " +
                "IF (SELECT reference_code FROM shipments WHERE id = NEW.shipment_id) = '" + reference + "' THEN " +
                "PERFORM pg_advisory_lock(" + barrier.Key + "); PERFORM pg_advisory_unlock(" + barrier.Key + "); END IF; " +
                "RETURN NULL; END $$; " +
                "CREATE TRIGGER " + barrier.name + " AFTER INSERT ON shipment_status_changes " +
                "FOR EACH ROW EXECUTE FUNCTION " + barrier.name + "();";
            await using var create = new NpgsqlCommand(sql, ddl);
            await create.ExecuteNonQueryAsync();
            return barrier;
        }

        // The pid of the backend that is waiting for the advisory lock, found from pg_locks.
        public async Task<int> WaitForBlockedBackend(TimeSpan timeout)
        {
            await using var observer = new NpgsqlConnection(connection);
            await observer.OpenAsync();
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                await using var cmd = new NpgsqlCommand(
                    "SELECT pid FROM pg_locks WHERE locktype = 'advisory' AND classid = 0 AND objid = @k " +
                    "AND objsubid = 1 AND NOT granted", observer);
                cmd.Parameters.AddWithValue("k", (long)Key);
                var pid = await cmd.ExecuteScalarAsync();
                if (pid is not null and not DBNull) return Convert.ToInt32(pid);
                await Task.Delay(50);
            }
            throw new TimeoutException("The shipment write never reached the barrier");
        }

        public async Task Release()
        {
            if (!held || holder is null) return;
            await using var cmd = new NpgsqlCommand("SELECT pg_advisory_unlock(" + Key + ")", holder);
            await cmd.ExecuteScalarAsync();
            held = false;
        }

        public async ValueTask DisposeAsync()
        {
            try { await Release(); } catch { /* connection may already be closed */ }
            if (holder is not null) await holder.DisposeAsync();
            await using var ddl = new NpgsqlConnection(connection);
            await ddl.OpenAsync();
            await using var drop = new NpgsqlCommand(
                "DROP TRIGGER IF EXISTS " + name + " ON shipment_status_changes; DROP FUNCTION IF EXISTS " + name + "();", ddl);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method,
        string path, object? payload, CancellationToken ct = default, string origin = "http://localhost",
        IDictionary<string, string>? headers = null)
    {
        using var tokenResponse = await client.GetAsync("/api/v1/auth/csrf", ct);
        tokenResponse.EnsureSuccessStatusCode();
        var token = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("token").GetString();
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation("X-HarborOps-CSRF", token);
        if (headers is not null) foreach (var pair in headers) request.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
        if (payload is not null) request.Content = JsonContent.Create(payload);
        return await client.SendAsync(request, ct);
    }

    private static async Task<Guid> CreatePort(HttpClient manager, Guid org, string code)
    {
        using var response = await SendAsync(manager, HttpMethod.Post, $"/api/v1/organizations/{org}/ports",
            new { referenceCode = code, name = code, countryCode = "US" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private sealed record Seeded(ReferenceDataHttpTests.Fixture F, Guid Origin, Guid Destination,
        Guid Id, string Reference, Row Before, List<Change> BeforeHistory)
    {
        public string Url => Root(F.A) + "/" + Id;
    }

    private static async Task<(Guid Origin, Guid Destination)> Ports(HttpClient manager, Guid org)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return (await CreatePort(manager, org, "G5I-A-" + suffix), await CreatePort(manager, org, "G5I-B-" + suffix));
    }

    private static async Task<Seeded> Seed(ReferenceDataHttpTests.Fixture f, Probe probe)
    {
        var (origin, destination) = await Ports(f.Manager, f.A);
        var reference = "G5I-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        using var response = await SendAsync(f.Manager, HttpMethod.Post, Root(f.A),
            new { referenceCode = reference, originPortId = origin, destinationPortId = destination });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var row = await probe.Shipment(id) ?? throw new InvalidOperationException("seed row missing");
        var history = await probe.History(id);
        Assert.Equal(1L, row.Version);
        Assert.Single(history);
        return new Seeded(f, origin, destination, id, reference, row, history);
    }

    private static async Task AssertUnchanged(Probe probe, Seeded seeded)
    {
        var now = await probe.Shipment(seeded.Id);
        Assert.NotNull(now);
        Assert.Equal(seeded.Before.Version, now.Version);
        Assert.Equal(seeded.Before.Workflow, now.Workflow);
        Assert.Equal(seeded.Before.UpdatedAt, now.UpdatedAt);
        Assert.Equal(seeded.BeforeHistory, await probe.History(seeded.Id));
    }

    // Proof that the transaction really was open, with writes, when the interruption happened.
    private static async Task AssertInFlight(Probe probe, int pid, Seeded seeded)
    {
        var backend = await probe.Inspect(pid);
        Assert.True(backend.Exists);
        Assert.True(backend.HasTransactionId, "the blocked transaction has not written yet");
        Assert.Equal("Lock", backend.WaitType);
        Assert.Equal("advisory", backend.WaitEvent);
        Assert.True(await probe.RowLockedByAnotherTransaction(seeded.Id), "the shipment row is not locked by the open transaction");
        // Uncommitted changes are invisible to every other session.
        await AssertUnchanged(probe, seeded);
    }

    private static async Task AssertSanitizedUnavailable(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? "");
        var raw = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(raw).RootElement;
        Assert.Equal("dependency_unavailable", json.GetProperty("code").GetString());
        Assert.True(json.TryGetProperty("correlationId", out _));
        Assert.True(raw.Length < 512);
        foreach (var token in Forbidden)
            Assert.DoesNotContain(token, raw, StringComparison.OrdinalIgnoreCase);
    }

    // After any precommit interruption: the service accepts a valid request, the retry of the
    // interrupted request commits exactly once, and replaying it cannot duplicate history.
    private static async Task AssertRecoveryAndSafeRetry(ReferenceDataHttpTests.Fixture f, Probe probe, Seeded seeded)
    {
        using (var read = await f.Manager.GetAsync(seeded.Url))
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using (var retry = await SendAsync(f.Manager, HttpMethod.Patch, seeded.Url,
            new { workflowStatus = "Booked", expectedVersion = 1L }))
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var row = await probe.Shipment(seeded.Id);
        Assert.Equal(2L, row!.Version);
        Assert.Equal("Booked", row.Workflow);
        var history = await probe.History(seeded.Id);
        Assert.Equal(new[] { new Change(1, null, "Draft"), new Change(2, "Draft", "Booked") }, history);
        using (var replay = await SendAsync(f.Manager, HttpMethod.Patch, seeded.Url,
            new { workflowStatus = "Booked", expectedVersion = 1L }))
            Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        using (var skipped = await SendAsync(f.Manager, HttpMethod.Patch, seeded.Url,
            new { workflowStatus = "InTransit", expectedVersion = 1L }))
            Assert.Equal(HttpStatusCode.Conflict, skipped.StatusCode);
        Assert.Equal(history, await probe.History(seeded.Id));
        Assert.Equal(2L, (await probe.Shipment(seeded.Id))!.Version);
    }

    [Fact]
    public async Task TerminatedDatabaseConnectionMidTransitionRollsBackAndRecovers()
    {
        var connection = TestDatabase.ConnectionString();
        using var factory = new ReferenceDataHttpTests.ApiFactory(connection);
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin; using var manager = f.Manager; using var analyst = f.Analyst;
        await using var probe = new Probe(connection);
        var seeded = await Seed(f, probe);
        await using var barrier = await Barrier.Install(connection, seeded.Reference);
        var inflight = SendAsync(f.Manager, HttpMethod.Patch, seeded.Url,
            new { workflowStatus = "Booked", expectedVersion = 1L });
        var pid = await barrier.WaitForBlockedBackend(TimeSpan.FromSeconds(30));
        await AssertInFlight(probe, pid, seeded);

        await probe.Terminate(pid);
        using var response = await inflight;
        await AssertSanitizedUnavailable(response);
        Assert.True(await probe.WaitUntilGone(pid, TimeSpan.FromSeconds(10)));
        await AssertUnchanged(probe, seeded);

        await barrier.Release();
        await AssertRecoveryAndSafeRetry(f, probe, seeded);
    }

    [Fact]
    public async Task TerminatedDatabaseConnectionMidCreateLeavesNoShipmentOrHistory()
    {
        var connection = TestDatabase.ConnectionString();
        using var factory = new ReferenceDataHttpTests.ApiFactory(connection);
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin; using var manager = f.Manager; using var analyst = f.Analyst;
        await using var probe = new Probe(connection);
        var (origin, destination) = await Ports(f.Manager, f.A);
        var reference = "G5C-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        await using var barrier = await Barrier.Install(connection, reference);
        var body = new { referenceCode = reference, originPortId = origin, destinationPortId = destination };
        var inflight = SendAsync(f.Manager, HttpMethod.Post, Root(f.A), body);
        var pid = await barrier.WaitForBlockedBackend(TimeSpan.FromSeconds(30));
        var backend = await probe.Inspect(pid);
        Assert.True(backend.HasTransactionId, "creation had not written yet");
        // The new shipment and its history row exist only inside the open transaction.
        Assert.Equal((0L, 0L), await probe.ByReference(f.A, reference));

        await probe.Terminate(pid);
        using var response = await inflight;
        await AssertSanitizedUnavailable(response);
        Assert.True(await probe.WaitUntilGone(pid, TimeSpan.FromSeconds(10)));
        Assert.Equal((0L, 0L), await probe.ByReference(f.A, reference));

        await barrier.Release();
        // The reference was never reserved, so the retry creates exactly one shipment and one history row.
        using (var retry = await SendAsync(f.Manager, HttpMethod.Post, Root(f.A), body))
            Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal((1L, 1L), await probe.ByReference(f.A, reference));
        using (var duplicate = await SendAsync(f.Manager, HttpMethod.Post, Root(f.A), body))
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal((1L, 1L), await probe.ByReference(f.A, reference));
    }

    [Fact]
    public async Task CancelledRequestMidTransitionRollsBackAndRecovers()
    {
        var connection = TestDatabase.ConnectionString();
        using var factory = new ReferenceDataHttpTests.ApiFactory(connection);
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin; using var manager = f.Manager; using var analyst = f.Analyst;
        await using var probe = new Probe(connection);
        var seeded = await Seed(f, probe);
        // A status transition is the operation that writes history, so cancel that one.
        await using var barrier = await Barrier.Install(connection, seeded.Reference);
        using var cancel = new CancellationTokenSource();
        var inflight = SendAsync(f.Manager, HttpMethod.Patch, seeded.Url,
            new { workflowStatus = "Booked", expectedVersion = 1L }, cancel.Token);
        var pid = await barrier.WaitForBlockedBackend(TimeSpan.FromSeconds(30));
        await AssertInFlight(probe, pid, seeded);

        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inflight);
        // The server cancels the statement; the blocked backend ends without committing.
        Assert.True(await probe.WaitUntilReleased(pid, TimeSpan.FromSeconds(15)),
            "the cancelled transaction is still open");
        await barrier.Release();
        await AssertUnchanged(probe, seeded);
        await AssertRecoveryAndSafeRetry(f, probe, seeded);
    }

    [Fact]
    public async Task CommandTimeoutMidTransitionRollsBackWithSanitizedFailure()
    {
        var connection = TestDatabase.ConnectionString();
        var timed = new NpgsqlConnectionStringBuilder(connection) { CommandTimeout = 6 }.ConnectionString;
        using var factory = new ReferenceDataHttpTests.ApiFactory(timed);
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin; using var manager = f.Manager; using var analyst = f.Analyst;
        await using var probe = new Probe(connection);
        var seeded = await Seed(f, probe);
        await using var barrier = await Barrier.Install(connection, seeded.Reference);
        var inflight = SendAsync(f.Manager, HttpMethod.Patch, seeded.Url,
            new { workflowStatus = "Booked", expectedVersion = 1L });
        var pid = await barrier.WaitForBlockedBackend(TimeSpan.FromSeconds(5));
        await AssertInFlight(probe, pid, seeded);

        // Nothing releases the barrier: the enforced command timeout ends the statement.
        using var response = await inflight;
        await AssertSanitizedUnavailable(response);
        Assert.True(await probe.WaitUntilReleased(pid, TimeSpan.FromSeconds(15)),
            "the timed-out transaction is still open");
        await barrier.Release();
        await AssertUnchanged(probe, seeded);
        await AssertRecoveryAndSafeRetry(f, probe, seeded);
    }

    [Fact]
    public async Task KilledApplicationProcessMidTransitionCommitsNothing()
    {
        var connection = TestDatabase.ConnectionString();
        using var factory = new ReferenceDataHttpTests.ApiFactory(connection);
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin; using var manager = f.Manager; using var analyst = f.Analyst;
        await using var probe = new Probe(connection);
        var seeded = await Seed(f, probe);

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var origin = "http://127.0.0.1:" + port;
        var apiDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location)!;
        var start = new ProcessStartInfo(Environment.ProcessPath ?? "dotnet", "\"" + Path.Combine(apiDirectory, "HarborOps.Api.dll") + "\"")
        {
            WorkingDirectory = apiDirectory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_URLS"] = origin;
        start.Environment["ConnectionStrings__HarborOps"] = connection;
        start.Environment["Identity__AllowInsecureLoopbackHttp"] = "true";
        start.Environment["Identity__KeyRingPath"] = Path.Combine(Path.GetTempPath(), "g5-interrupt-keys-" + Guid.NewGuid().ToString("N"));
        using var child = Process.Start(start)!;
        var output = new StringBuilder();
        child.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
        child.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
        child.BeginOutputReadLine();
        child.BeginErrorReadLine();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { UseCookies = true }) { BaseAddress = new Uri(origin) };
            var ready = false;
            for (var i = 0; i < 120 && !ready && !child.HasExited; i++)
            {
                try { using var r = await client.GetAsync("/health/live"); ready = r.IsSuccessStatusCode; }
                catch (HttpRequestException) { }
                if (!ready) await Task.Delay(500);
            }
            Assert.True(ready, "the API child process did not become ready: " + output);
            using (var login = await SendAsync(client, HttpMethod.Post, "/api/v1/auth/login",
                new { email = f.ManagerEmail, password = "Synthetic-reference-integration-passphrase-123" }, default, origin))
                Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            using (var select = await SendAsync(client, HttpMethod.Put, "/api/v1/auth/session/organization",
                new { organizationId = f.A }, default, origin))
                Assert.Equal(HttpStatusCode.OK, select.StatusCode);

            await using var barrier = await Barrier.Install(connection, seeded.Reference);
            var inflight = SendAsync(client, HttpMethod.Patch, seeded.Url,
                new { workflowStatus = "Booked", expectedVersion = 1L }, default, origin);
            var pid = await barrier.WaitForBlockedBackend(TimeSpan.FromSeconds(30));
            await AssertInFlight(probe, pid, seeded);

            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync();
            await Assert.ThrowsAnyAsync<Exception>(() => inflight);
            // The database cannot know the client died while the backend waits, so the
            // transaction is still open and uncommitted until the barrier is released.
            Assert.True((await probe.Inspect(pid)).Exists);
            await AssertUnchanged(probe, seeded);

            await barrier.Release();
            Assert.True(await probe.WaitUntilGone(pid, TimeSpan.FromSeconds(20)),
                "the orphaned backend did not end after its client was killed");
            await AssertUnchanged(probe, seeded);
        }
        finally
        {
            if (!child.HasExited) child.Kill(entireProcessTree: true);
        }
        await AssertRecoveryAndSafeRetry(f, probe, seeded);
    }

    private sealed class FailAfterHandlerFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, pipeline) =>
            {
                if (!context.Request.Headers.ContainsKey("X-G5-Lose-Response"))
                {
                    await pipeline();
                    return;
                }
                // Let the handler finish and commit, then discard its response and break the
                // connection, so the client sees a failure for a request that did succeed.
                var original = context.Response.Body;
                context.Response.Body = new MemoryStream();
                await pipeline();
                context.Response.Body = original;
                context.Abort();
            });
            next(app);
        };
    }

    [Fact]
    public async Task LostResponseAfterCommitIsDistinguishableFromRollbackAndRetrySafe()
    {
        var connection = TestDatabase.ConnectionString();
        using var factory = new ReferenceDataHttpTests.ApiFactory(connection);
        var f = await ReferenceDataHttpTests.Setup(factory);
        using var admin = f.Admin; using var manager = f.Manager; using var analyst = f.Analyst;
        await using var probe = new Probe(connection);
        var seeded = await Seed(f, probe);
        using var lossy = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter, FailAfterHandlerFilter>()));
        using var client = lossy.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        await ReferenceDataHttpTests.Login(client, f.ManagerEmail);
        await ReferenceDataHttpTests.Select(client, f.A);

        HttpResponseMessage? received = null;
        Exception? failure = null;
        try
        {
            received = await SendAsync(client, HttpMethod.Patch, seeded.Url,
                new { workflowStatus = "Booked", expectedVersion = 1L }, default, "http://localhost",
                new Dictionary<string, string> { ["X-G5-Lose-Response"] = "1" });
        }
        catch (Exception ex) { failure = ex; }
        // The client saw a failure, not the success response, so it cannot tell the outcome.
        Assert.True(failure is not null || received is not { StatusCode: HttpStatusCode.OK },
            "the client unexpectedly received the successful response");

        // Unlike every precommit interruption, the change is fully committed.
        var row = await probe.Shipment(seeded.Id);
        Assert.Equal(2L, row!.Version);
        Assert.Equal("Booked", row.Workflow);
        var history = await probe.History(seeded.Id);
        Assert.Equal(new[] { new Change(1, null, "Draft"), new Change(2, "Draft", "Booked") }, history);

        // Reading resolves the ambiguity, and a blind retry is rejected rather than repeated.
        using (var read = await f.Manager.GetAsync(seeded.Url))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            var json = await read.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(2L, json.GetProperty("version").GetInt64());
            Assert.Equal("Booked", json.GetProperty("workflowStatus").GetString());
        }
        using (var retry = await SendAsync(f.Manager, HttpMethod.Patch, seeded.Url,
            new { workflowStatus = "Booked", expectedVersion = 1L }))
            Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
        Assert.Equal(history, await probe.History(seeded.Id));
        Assert.Equal(2L, (await probe.Shipment(seeded.Id))!.Version);
    }
}
