using System.Text;
using HarborOps.Domain.Tracking;
using HarborOps.Infrastructure.Integrations;
using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HarborOps.IntegrationTests;

public sealed class TrackingInboxCredentialWindowTests
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static TrackingEvent ValidEvent(Guid source, Guid organization, string id)
    {
        var raw = Encoding.UTF8.GetBytes($$"""
            {"sourceEventId":"{{id}}","eventFamily":"Equipment","eventCode":"GTIN",
             "classifier":"ACT","occurredAtUtc":"2026-11-03T10:00:00Z",
             "equipmentReference":"SYN-BOX-ABCD","externalSchemaVersion":"2.2.0"}
            """);
        return TrackingEventPolicy.Parse(raw, TimeProvider.System.GetUtcNow()).Value!
            .Bind(source, organization);
    }

    private static async Task<TrackingReceiptResult> Receive(
        HarborOpsDbContext db, TrackingEvent value, string bearer, TimeProvider clock)
    {
        var raw = Encoding.UTF8.GetBytes($$"""
            {"sourceEventId":"{{value.SourceEventId}}","eventFamily":"Equipment",
             "eventCode":"GTIN","classifier":"ACT","occurredAtUtc":"2026-11-03T10:00:00Z",
             "equipmentReference":"SYN-BOX-ABCD","externalSchemaVersion":"2.2.0"}
            """);
        var store = new TrackingInboxStore(db, clock, new ConfigurationBuilder().Build(),
            new NoopTrackingReceiptCommitHook());
        return await store.ReceiveAsync(value, bearer, raw, "time-window", CancellationToken.None);
    }

    [Fact]
    public async Task RotationOverlapExpiryAndNotBeforeAreCheckedAtFinalReceipt()
    {
        using var factory = new ReferenceDataHttpTests.ApiFactory(TestDatabase.ConnectionString());
        var fixture = await ReferenceDataHttpTests.Setup(factory);
        using var admin = fixture.Admin;
        using var manager = fixture.Manager;
        using var analyst = fixture.Analyst;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HarborOpsDbContext>();
        var sources = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
        var first = await sources.CreateAsync(fixture.A, "m8_window", "Temporal Source",
            CancellationToken.None);
        var firstRecord = await db.IntegrationCredentials.AsNoTracking()
            .SingleAsync(x => x.Id == first.CredentialId);
        var beforeActivation = new FixedClock(firstRecord.NotBeforeUtc.AddTicks(-1));
        Assert.Equal(TrackingReceiptOutcome.AccessDenied,
            (await Receive(db, ValidEvent(first.Id, fixture.A, "too-early"),
                first.Bearer, beforeActivation)).Outcome);

        var second = await sources.RotateAsync(fixture.A, "m8_window", CancellationToken.None);
        var activeClock = new FixedClock(TimeProvider.System.GetUtcNow());
        Assert.Equal(TrackingReceiptOutcome.Received,
            (await Receive(db, ValidEvent(first.Id, fixture.A, "overlap-old"),
                first.Bearer, activeClock)).Outcome);
        Assert.Equal(TrackingReceiptOutcome.Received,
            (await Receive(db, ValidEvent(first.Id, fixture.A, "overlap-new"),
                second.Bearer, activeClock)).Outcome);

        var shortened = await db.IntegrationCredentials.AsNoTracking()
            .SingleAsync(x => x.Id == first.CredentialId);
        var replacement = await db.IntegrationCredentials.AsNoTracking()
            .SingleAsync(x => x.Id == second.CredentialId);
        var expiredClock = new FixedClock(shortened.ExpiresAtUtc.AddTicks(1));
        Assert.True(expiredClock.GetUtcNow() < replacement.ExpiresAtUtc);
        Assert.Equal(TrackingReceiptOutcome.AccessDenied,
            (await Receive(db, ValidEvent(first.Id, fixture.A, "expired-old"),
                first.Bearer, expiredClock)).Outcome);
        Assert.Equal(TrackingReceiptOutcome.Received,
            (await Receive(db, ValidEvent(first.Id, fixture.A, "still-new"),
                second.Bearer, expiredClock)).Outcome);
        Assert.Equal(3, await db.TrackingInboxReceipts
            .CountAsync(x => x.SourceId == first.Id));
    }
}
