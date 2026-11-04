using System.Diagnostics;
using System.Text;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class TrackingWorkerProcessRaceTests
{
    [Fact]
    public async Task TwoIndependentWorkersContendOnOneReceiptAndOnlyOneClaimsIt()
    {
        var originalConnection = TestDatabase.ConnectionString();
        var nonce = Guid.NewGuid().ToString("N");
        var schema = "g9_worker_race_" + nonce;
        var scoped = new NpgsqlConnectionStringBuilder(originalConnection)
        {
            SearchPath = schema + ",public"
        };

        await using var administration = new NpgsqlConnection(originalConnection);
        await administration.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", administration))
            await create.ExecuteNonQueryAsync();

        Process? first = null;
        Process? second = null;
        try
        {
            var options = new DbContextOptionsBuilder<HarborOpsDbContext>()
                .UseNpgsql(scoped.ConnectionString).Options;
            await using var database = new HarborOpsDbContext(options);
            await database.Database.MigrateAsync();

            var email = "g9-worker-race-" + nonce + "@example.test";
            var identity = new IdentityStore(database, new PasswordHasher(), TimeProvider.System);
            await identity.ProvisionAsync(email, "Synthetic Race Operator",
                "Synthetic-g9-process-race-password-123", CancellationToken.None);
            var organizations = new OrganizationStore(database, TimeProvider.System);
            var org = await organizations.BootstrapAsync("g9-process-" + nonce[..12],
                "Isolated Worker Race", email, CancellationToken.None);
            var sources = new IntegrationSourceStore(database, TimeProvider.System);
            var source = await sources.CreateAsync(org.Id, "g9_worker_race", "Worker Race Source",
                CancellationToken.None);
            var raw = Encoding.UTF8.GetBytes("""
                {"sourceEventId":"g9-two-process-one-receipt","eventFamily":"Equipment",
                 "eventCode":"GTIN","classifier":"ACT","occurredAtUtc":"2026-11-03T10:00:00Z",
                 "equipmentReference":"SYN-BOX-ABCD","externalSchemaVersion":"2.2.0"}
                """);
            var parsed = TrackingEventPolicy.Parse(raw, TimeProvider.System.GetUtcNow()).Value!;
            var inbox = new TrackingInboxStore(database, TimeProvider.System,
                new ConfigurationBuilder().Build(), new NoopTrackingReceiptCommitHook());
            var received = await inbox.ReceiveAsync(parsed.Bind(source.Id, org.Id),
                source.Bearer, raw, "g9-two-process-race", CancellationToken.None);
            Assert.Equal(TrackingReceiptOutcome.Received, received.Outcome);

            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "HarborOps.sln")))
                root = root.Parent;
            Assert.NotNull(root);
            var executable = Path.Combine(root!.FullName, "src", "HarborOps.Worker", "bin",
                "Release", "net10.0", "HarborOps.Worker.dll");
            Assert.True(File.Exists(executable),
                "Build the worker in Release before executing the process-concurrency suite");

            // Hold the first lock in the established M8/M9 organization-first order.
            // Both child processes must be seen waiting on a PostgreSQL lock before release.
            await using var blocker = new NpgsqlConnection(scoped.ConnectionString);
            await blocker.OpenAsync();
            await using var blockerTx = await blocker.BeginTransactionAsync();
            await using (var lockCommand = new NpgsqlCommand(
                "SELECT id FROM organizations WHERE id=@organization FOR UPDATE", blocker, blockerTx))
            {
                lockCommand.Parameters.AddWithValue("organization", org.Id);
                Assert.Equal(org.Id, await lockCommand.ExecuteScalarAsync());
            }

            var nameA = "g9-race-a-" + nonce[..12];
            var nameB = "g9-race-b-" + nonce[..12];

            Process StartWorker(string applicationName)
            {
                var connection = new NpgsqlConnectionStringBuilder(scoped.ConnectionString)
                {
                    ApplicationName = applicationName
                };
                var start = new ProcessStartInfo("dotnet")
                {
                    WorkingDirectory = root!.FullName,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                start.ArgumentList.Add(executable);
                start.Environment["ConnectionStrings__HarborOps"] = connection.ConnectionString;
                start.Environment["TrackingWorker__PollMilliseconds"] = "100";
                return Process.Start(start)
                    ?? throw new InvalidOperationException("The independent worker process failed to start");
            }

            var lockReleased = false;
            try
            {
                first = StartWorker(nameA);
                second = StartWorker(nameB);

                var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
                var bothContending = false;
                while (DateTimeOffset.UtcNow < deadline)
                {
                    if (first.HasExited || second.HasExited)
                        break;
                    await using var inspect = new NpgsqlConnection(originalConnection);
                    await inspect.OpenAsync();
                    await using var locks = new NpgsqlCommand("""
                        SELECT count(*) FROM pg_stat_activity
                        WHERE application_name IN (@first, @second)
                          AND wait_event_type='Lock'
                        """, inspect);
                    locks.Parameters.AddWithValue("first", nameA);
                    locks.Parameters.AddWithValue("second", nameB);
                    if (Convert.ToInt64(await locks.ExecuteScalarAsync()) == 2)
                    {
                        bothContending = true;
                        break;
                    }
                    await Task.Delay(100);
                }
                Assert.True(bothContending,
                    "Both independently started workers must overlap on the held organization lock");
                await blockerTx.CommitAsync();
                lockReleased = true;

                var completed = false;
                deadline = DateTimeOffset.UtcNow.AddSeconds(20);
                while (DateTimeOffset.UtcNow < deadline)
                {
                    var work = await database.TrackingInboxWorkItems.AsNoTracking()
                        .SingleAsync(x => x.ReceiptId == received.ReceiptId);
                    if (work.State == "NeedsReconciliation")
                    {
                        completed = true;
                        break;
                    }
                    await Task.Delay(100);
                }
                Assert.True(completed, "One worker must durably park the unknown reference");
                Assert.Equal(1, await database.TrackingWorkAttemptEvents.CountAsync(x =>
                    x.ReceiptId == received.ReceiptId && x.EventKind == "Claimed"));
                Assert.Equal(1, await database.TrackingWorkAttemptEvents.CountAsync(x =>
                    x.ReceiptId == received.ReceiptId && x.EventKind == "ReconciliationRequired"));
                Assert.Empty(await database.TrackingProcessingEffects.Where(x =>
                    x.ReceiptId == received.ReceiptId).ToListAsync());
                Assert.Equal(1, await database.TrackingInboxReceipts.CountAsync(x =>
                    x.Id == received.ReceiptId));
            }
            finally
            {
                if (!lockReleased)
                {
                    try { await blockerTx.RollbackAsync(); }
                    catch (InvalidOperationException) { }
                }
                foreach (var process in new[] { first, second })
                {
                    if (process is null) continue;
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try { await process.WaitForExitAsync(shutdown.Token); }
                    catch (OperationCanceledException) { }
                    process.Dispose();
                }
            }
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", administration);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
