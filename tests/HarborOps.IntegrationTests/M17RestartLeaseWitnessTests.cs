using System.Net;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

// A positive leased receipt is preserved across an actual API-host restart, while
// requests are canceled before and after it. The lease is then proven still
// exclusive (a competing claim and forged fences are rejected), usable by its
// original holder exactly once, and the one durable effect is not duplicated.
// This is deliberately separate from the worker's own lease-recovery test.
[Collection("M17WorkerShellIsolation")]
public sealed class M17RestartLeaseWitnessTests
{
    private sealed record Snapshot(string State, Guid? Token, long Epoch, int Attempts,
        DateTimeOffset? Expires, DateTimeOffset Updated, int Generation, int Receipts,
        int Events, int Effects, int ClaimedEvents, int AllAttemptEvents);

    private static async Task<Snapshot> Observe(HarborOpsDbContext db, Guid receipt)
    {
        var item = await db.TrackingInboxWorkItems.AsNoTracking()
            .SingleAsync(x => x.ReceiptId == receipt);
        return new Snapshot(item.State, item.LeaseToken, item.LeaseEpoch, item.AttemptCount,
            item.LeaseExpiresAtUtc, item.UpdatedAtUtc, item.RedriveGeneration,
            await db.TrackingInboxReceipts.CountAsync(x => x.Id == receipt),
            await db.TrackingAcceptedEvents.CountAsync(x => x.ReceiptId == receipt),
            await db.TrackingProcessingEffects.CountAsync(x => x.ReceiptId == receipt),
            await db.TrackingWorkAttemptEvents.CountAsync(x =>
                x.ReceiptId == receipt && x.EventKind == "Claimed"),
            await db.TrackingWorkAttemptEvents.CountAsync(x => x.ReceiptId == receipt));
    }

    // Real canceled HTTP reads against the running host. Each one is either
    // observed as canceled or completes with 200; none may change the lease.
    private static async Task<(int Canceled, int Completed)> CancelReads(
        HttpClient client, int count)
    {
        // Warm the host first so canceled requests are measured against a running API.
        await BaselineWithinAsync(client, TimeSpan.FromSeconds(30));
        // Waves of six keep the bounded read inside its capacity, so a completed read is a
        // real 200 on any machine and the only variable is the cancellation itself.
        var outcomes = new List<bool>();
        for (var wave = 0; wave * 6 < count; wave++)
        {
            var tasks = Enumerable.Range(wave * 6, Math.Min(6, count - wave * 6)).Select(async i =>
            {
                using var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromMilliseconds(i % 3));
                try
                {
                    using var response = await client.GetAsync("/api/v1/system/baseline", cts.Token);
                    // A read that finished before its cancellation fired is a completed
                    // request and must be a real 200.
                    Assert.True(response.StatusCode == HttpStatusCode.OK,
                        "Completed read returned " + (int)response.StatusCode + ": " +
                        await response.Content.ReadAsStringAsync());
                    return false;
                }
                catch (OperationCanceledException) { return true; }
            }).ToArray();
            outcomes.AddRange(await Task.WhenAll(tasks));
        }
        return (outcomes.Count(x => x), outcomes.Count(x => !x));
    }

    // A freshly created host may need to finish JIT, model and connection warm-up before
    // its first bounded database read can succeed. Poll within a fixed bound and require
    // a real 200; a host that never reaches 200 within the bound still fails.
    private static async Task BaselineWithinAsync(HttpClient client, TimeSpan bound)
    {
        var deadline = DateTimeOffset.UtcNow + bound;
        string last;
        do
        {
            using var response = await client.GetAsync("/api/v1/system/baseline");
            if (response.StatusCode == HttpStatusCode.OK) return;
            last = (int)response.StatusCode + " " + await response.Content.ReadAsStringAsync();
            await Task.Delay(500);
        } while (DateTimeOffset.UtcNow < deadline);
        Assert.Fail("Baseline read never returned 200 within " + bound + ": " + last);
    }

    [Fact]
    public async Task ApiRestartPreservesActiveWorkerLeaseAndNoDuplicateEffects()
    {
        var priorEvents = Environment.GetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT");
        var priorLinks = Environment.GetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT");
        Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT", "0");
        Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT", "0");
        try
        {
            var connection = TestDatabase.ConnectionString();
            // The baseline row is shared database state that other tests create or delete.
            // This test depends on it for its health probes, so it establishes it itself
            // rather than relying on whichever test ran first.
            await using (var seedDb = new HarborOpsDbContext(
                new DbContextOptionsBuilder<HarborOpsDbContext>().UseNpgsql(connection).Options))
            {
                await seedDb.Database.MigrateAsync();
                await BootstrapSeeder.SeedAsync(seedDb, CancellationToken.None);
            }
            var nonce = Guid.NewGuid().ToString("N")[..12];
            var options = new Dictionary<string, string>
            {
                ["M17:Enabled"] = "true",
                ["M17:OtlpEndpoint"] = "http://127.0.0.1:4317",
                ["M17:LinkKey"] = Convert.ToBase64String(
                    Enumerable.Range(1, 32).Select(x => (byte)x).ToArray())
            };
            // A lease long enough that the whole test cannot outlive it.
            var settings = new TrackingWorkerSettings(LeaseSeconds: 300, HeartbeatSeconds: 60);
            settings.Validate();
            Guid receipt, org;
            TrackingWorkClaim claim;
            Snapshot before;
            var canceledBefore = 0;
            using (var first = new ReferenceDataHttpTests.ApiFactory(connection, settings: options))
            {
                var tenant = await ReferenceDataHttpTests.Setup(first);
                using var admin = tenant.Admin;
                using var manager = tenant.Manager;
                using var analyst = tenant.Analyst;
                org = tenant.A;
                // A reference that resolves to a real container makes the one
                // durable processing effect observable (not only a zero count).
                var target = await G9Support.SeedResolvable(first, tenant);
                var source = await G9Support.NewSource(first, org, "g17lease");
                receipt = await G9Support.Accept(first, source,
                    G9Support.EquipmentPayload("g17-lease-" + nonce, target.Reference));
                await using var scope = first.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
                var store = new TrackingWorkStore(db, settings);
                var claimed = await store.ClaimOneAsync(org, CancellationToken.None);
                Assert.NotNull(claimed);
                claim = claimed!;
                Assert.Equal(receipt, claim.ReceiptId);
                before = await Observe(db, receipt);
                Assert.Equal("Leased", before.State);
                Assert.NotNull(before.Token);
                Assert.Equal(claim.LeaseToken, before.Token);
                Assert.Equal(claim.LeaseEpoch, before.Epoch);
                Assert.True(before.Epoch > 0);
                Assert.Equal(1, before.Attempts);
                Assert.True(before.Expires > DateTimeOffset.UtcNow.AddMinutes(4));
                Assert.Equal(1, before.Receipts);
                Assert.Equal(1, before.Events);
                Assert.Equal(0, before.Effects);
                Assert.Equal(1, before.ClaimedEvents);

                using var http = first.CreateClient();
                using var live = await http.GetAsync("/health/live");
                Assert.Equal(HttpStatusCode.OK, live.StatusCode);
                var (canceled, completed) = await CancelReads(http, 60);
                canceledBefore = canceled;
                Assert.Equal(60, canceled + completed);
                // Exclusivity holds on the original host: the lease is neither
                // claimable by a competitor nor usable with a forged fence.
                Assert.Null(await store.ClaimOneAsync(org, CancellationToken.None));
                Assert.Equal(TrackingWorkDisposition.Stale, await store.FinalizeAsync(
                    claim with { LeaseToken = Guid.NewGuid() }, CancellationToken.None));
                Assert.False(await store.HeartbeatAsync(
                    claim with { LeaseEpoch = claim.LeaseEpoch + 1 }, CancellationToken.None));
                Assert.Equal(before, await Observe(db, receipt));
            }
            // Disposing the first host, then creating another from the same
            // PostgreSQL database is an actual API lifetime boundary. No worker
            // runs in this isolated test, so only API instrumentation can act.
            using (var restarted = new ReferenceDataHttpTests.ApiFactory(connection, settings: options))
            {
                using var client = restarted.CreateClient();
                using var live = await client.GetAsync("/health/live");
                Assert.Equal(HttpStatusCode.OK, live.StatusCode);
                await BaselineWithinAsync(client, TimeSpan.FromSeconds(30));
                await using var scope = restarted.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
                var store = new TrackingWorkStore(db, settings);

                // Whole-row identity: state, token, epoch, attempts, expiry and update
                // time are exactly those from before the restart.
                Assert.Equal(before, await Observe(db, receipt));
                var (canceled, completed) = await CancelReads(client, 60);
                Assert.Equal(60, canceled + completed);
                Assert.True(canceledBefore + canceled > 0,
                    "No canceled request was observed across the restart");
                Assert.Equal(before, await Observe(db, receipt));

                // The restarted API cannot steal or fence the active lease.
                Assert.Null(await store.ClaimOneAsync(org, CancellationToken.None));
                Assert.Equal(TrackingWorkDisposition.Stale, await store.FinalizeAsync(
                    claim with { LeaseToken = Guid.NewGuid() }, CancellationToken.None));
                Assert.Equal(TrackingWorkDisposition.Stale, await store.FinalizeAsync(
                    claim with { LeaseEpoch = claim.LeaseEpoch + 1 }, CancellationToken.None));
                Assert.Equal(before, await Observe(db, receipt));

                // The original lease holder still owns it: extend, then finish once.
                Assert.True(await store.HeartbeatAsync(claim, CancellationToken.None));
                var extended = await Observe(db, receipt);
                Assert.Equal(before.Token, extended.Token);
                Assert.Equal(before.Epoch, extended.Epoch);
                Assert.Equal(before.Attempts, extended.Attempts);
                Assert.True(extended.Expires > before.Expires);
                Assert.Equal(TrackingWorkDisposition.Processed,
                    await store.FinalizeAsync(claim, CancellationToken.None));
                var finished = await Observe(db, receipt);
                Assert.Equal("Processed", finished.State);
                Assert.Null(finished.Token);
                Assert.Equal(1, finished.Effects);
                // A duplicate completion attempt, even with the exact original
                // fence, is stale and creates no second durable effect.
                Assert.Equal(TrackingWorkDisposition.Stale,
                    await store.FinalizeAsync(claim, CancellationToken.None));
                var final = await Observe(db, receipt);
                Assert.Equal(1, final.Effects);
                Assert.Equal(1, final.Receipts);
                Assert.Equal(1, final.Events);
                Assert.Equal(1, final.ClaimedEvents);
                Assert.Equal("Processed", final.State);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT", priorEvents);
            Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT", priorLinks);
        }
    }
}
