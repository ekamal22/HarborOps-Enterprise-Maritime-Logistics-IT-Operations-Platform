using HarborOps.Domain.Tracking;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class TrackingTimelinePolicyTests
{
    private static readonly Guid Org = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Shipment = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Source = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset Time = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

    private static ShipmentTimelineEvent E(int index, string code = "GTIN",
        int seconds = 0, bool superseded = false, Guid? replacement = null) =>
        new(Guid.Parse($"10000000-0000-0000-0000-{index:000000000000}"),
            Guid.Parse($"20000000-0000-0000-0000-{index:000000000000}"),
            Source, $"event-{index}", code, Time.AddSeconds(seconds),
            Time.AddMinutes(index), superseded, replacement);

    [Fact]
    public void EmptyInputRemainsUnknownAndIsReproducible()
    {
        var a = TrackingTimelinePolicy.Reduce(Org, Shipment, Array.Empty<ShipmentTimelineEvent>());
        var b = TrackingTimelinePolicy.Reduce(Org, Shipment, Array.Empty<ShipmentTimelineEvent>());
        Assert.Equal("Unknown", a.ObservedTrackingState);
        Assert.Null(a.LastEventId);
        Assert.Equal(a.InputDigest, b.InputDigest);
        Assert.Equal(64, a.InputDigest.Length);
    }

    [Fact]
    public void LateDeliveryDoesNotChangeEventTimeResult()
    {
        var entering = E(1, "GTIN", seconds: 0);
        var leaving = E(2, "GTOT", seconds: 60);
        var forward = TrackingTimelinePolicy.Reduce(Org, Shipment, [entering, leaving]);
        var reordered = TrackingTimelinePolicy.Reduce(Org, Shipment, [leaving, entering]);
        Assert.Equal("EquipmentOut", forward.ObservedTrackingState);
        Assert.Equal(leaving.EventId, forward.LastEventId);
        Assert.Equal(forward.InputDigest, reordered.InputDigest);
        Assert.Equal(forward.LastOccurredAtUtc, reordered.LastOccurredAtUtc);
    }

    [Fact]
    public void ContradictorySameTimeObservationsAreConflicts()
    {
        var state = TrackingTimelinePolicy.Reduce(Org, Shipment,
            [E(1, "GTIN"), E(2, "GTOT")]);
        Assert.Equal("Conflict", state.ObservedTrackingState);
    }

    [Fact]
    public void VoidCorrectionRemovesEffectWithoutErasingHistoricalEvent()
    {
        var before = TrackingTimelinePolicy.Reduce(Org, Shipment,
            [E(1, "GTIN"), E(2, "GTOT", seconds: 60)]);
        var after = TrackingTimelinePolicy.Reduce(Org, Shipment,
            [E(1, "GTIN"), E(2, "GTOT", seconds: 60, superseded: true)]);
        Assert.Equal("EquipmentOut", before.ObservedTrackingState);
        Assert.Equal("EquipmentIn", after.ObservedTrackingState);
        Assert.Equal(2, after.EventCount);
        Assert.NotEqual(before.InputDigest, after.InputDigest);
    }

    [Fact]
    public void ReplacementIsLinkedAndEqualInputsAreIdempotent()
    {
        var replacement = E(3, "GTIN", seconds: 120);
        var first = TrackingTimelinePolicy.Reduce(Org, Shipment,
            [E(2, "GTOT", seconds: 60, superseded: true,
                replacement: replacement.ReceiptId), replacement]);
        var again = TrackingTimelinePolicy.Reduce(Org, Shipment,
            [replacement, E(2, "GTOT", seconds: 60, superseded: true,
                replacement: replacement.ReceiptId)]);
        Assert.Equal("EquipmentIn", first.ObservedTrackingState);
        Assert.Equal(first.InputDigest, again.InputDigest);
        Assert.Equal(replacement.EventId, first.LastEventId);
    }

    [Fact]
    public void InputsAreScopedByOrganizationAndShipment()
    {
        var events = new[] { E(1) };
        var a = TrackingTimelinePolicy.Reduce(Org, Shipment, events);
        Assert.NotEqual(a.InputDigest,
            TrackingTimelinePolicy.Reduce(Guid.NewGuid(), Shipment, events).InputDigest);
        Assert.NotEqual(a.InputDigest,
            TrackingTimelinePolicy.Reduce(Org, Guid.NewGuid(), events).InputDigest);
    }

    [Fact]
    public void SupersededEventDoesNotBecomeCurrentEvenWhenReplacementIsNull()
    {
        var state = TrackingTimelinePolicy.Reduce(Org, Shipment,
            [E(1, "GTIN", superseded: true)]);
        Assert.Equal("Unknown", state.ObservedTrackingState);
        Assert.Null(state.LastEventId);
    }
    [Fact]
    public void DifferentContainerStatusesAreMixedRatherThanFalseConflicts()
    {
        var firstContainer = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var secondContainer = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var state = TrackingTimelinePolicy.Reduce(Org, Shipment, [
            E(1, "GTIN") with { ContainerId = firstContainer },
            E(2, "GTOT") with { ContainerId = secondContainer }
        ]);
        Assert.Equal("Mixed", state.ObservedTrackingState);
        Assert.Equal(2, state.Containers!.Count);
        Assert.All(state.Containers, item => Assert.NotEqual("Conflict", item.ObservedState));
    }

    [Fact]
    public void ContradictionMustBeOnSameContainerAtSameTime()
    {
        var container = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var state = TrackingTimelinePolicy.Reduce(Org, Shipment, [
            E(1, "GTIN") with { ContainerId = container },
            E(2, "GTOT") with { ContainerId = container }
        ]);
        Assert.Equal("Conflict", state.ObservedTrackingState);
    }

    [Fact]
    public void StreamingReducerProcessesHistoryBeyondUnresolvedWorkCap()
    {
        using var accumulator = new ShipmentTrackingAccumulator(Org, Shipment);
        var container = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        for (var i = 1; i <= 10_050; i++)
            accumulator.Add(E(i, "GTIN", seconds: i) with { ContainerId = container });
        var reduced = accumulator.Complete();
        Assert.Equal(10_050, reduced.EventCount);
        Assert.Equal("EquipmentIn", reduced.ObservedTrackingState);
        Assert.Equal(64, reduced.InputDigest.Length);
    }

}
