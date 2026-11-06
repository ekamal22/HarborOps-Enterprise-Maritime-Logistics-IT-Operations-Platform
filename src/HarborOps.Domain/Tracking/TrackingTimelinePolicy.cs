using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HarborOps.Domain.Tracking;

public sealed record ShipmentTimelineEvent(
    Guid EventId, Guid ReceiptId, Guid SourceId, string SourceEventId,
    string EventCode, DateTimeOffset OccurredAtUtc, DateTimeOffset ReceivedAtUtc,
    bool Superseded, Guid? ReplacedByReceiptId,
    Guid ContainerId = default, Guid? CorrectionId = null);

public sealed record ShipmentTimelineKey(
    DateTimeOffset OccurredAtUtc, DateTimeOffset ReceivedAtUtc, Guid SourceId,
    string SourceEventId, Guid ReceiptId, Guid EventId)
{
    public static ShipmentTimelineKey Of(ShipmentTimelineEvent e) =>
        new(e.OccurredAtUtc, e.ReceivedAtUtc, e.SourceId,
            e.SourceEventId, e.ReceiptId, e.EventId);
}

public sealed record ContainerTrackingObservation(
    Guid ContainerId, string ObservedState, DateTimeOffset OccurredAtUtc, Guid EventId);

public sealed record ShipmentTrackingState(
    string ObservedTrackingState, Guid? LastEventId, DateTimeOffset? LastOccurredAtUtc,
    string InputDigest, string PolicyVersion, int EventCount,
    IReadOnlyList<ContainerTrackingObservation>? Containers = null);

public static class TrackingTimelinePolicy
{
    public const string Version = "m10-equipment-v2";
    // PostgreSQL compares uuid values in network-byte (canonical hex) order.
    // Guid.CompareTo can differ because its leading field is stored as a signed int.
    private static int CompareUuid(Guid left, Guid right) =>
        StringComparer.Ordinal.Compare(left.ToString("N"), right.ToString("N"));

    public static int Compare(ShipmentTimelineKey a, ShipmentTimelineKey b)
    {
        var compare = a.OccurredAtUtc.CompareTo(b.OccurredAtUtc);
        if (compare != 0) return compare;
        compare = a.ReceivedAtUtc.CompareTo(b.ReceivedAtUtc);
        if (compare != 0) return compare;
        compare = CompareUuid(a.SourceId, b.SourceId);
        if (compare != 0) return compare;
        compare = StringComparer.Ordinal.Compare(a.SourceEventId, b.SourceEventId);
        if (compare != 0) return compare;
        compare = CompareUuid(a.ReceiptId, b.ReceiptId);
        return compare != 0 ? compare : CompareUuid(a.EventId, b.EventId);
    }

    public static IReadOnlyList<ShipmentTimelineEvent> Order(IEnumerable<ShipmentTimelineEvent> items)
        => items.OrderBy(e => e, Comparer<ShipmentTimelineEvent>.Create(
            (a, b) => Compare(ShipmentTimelineKey.Of(a), ShipmentTimelineKey.Of(b)))).ToArray();

    public static ShipmentTrackingState Reduce(Guid organizationId, Guid shipmentId,
        IEnumerable<ShipmentTimelineEvent> input)
    {
        using var accumulator = new ShipmentTrackingAccumulator(organizationId, shipmentId);
        foreach (var item in Order(input)) accumulator.Add(item);
        return accumulator.Complete();
    }
}

// Streams retained history in canonical order without loading it into a large array.
public sealed class ShipmentTrackingAccumulator : IDisposable
{
    private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly Dictionary<Guid, ContainerTrackingObservation> containers = new();
    private Guid? lastEvent;
    private DateTimeOffset? lastOccurred;
    private int count;
    private bool finished;

    public ShipmentTrackingAccumulator(Guid organizationId, Guid shipmentId)
    {
        Append(TrackingTimelinePolicy.Version + "|" +
            organizationId.ToString("N") + "|" + shipmentId.ToString("N"));
    }

    private void Append(string value) => hash.AppendData(Encoding.UTF8.GetBytes(value));

    public void Add(ShipmentTimelineEvent e)
    {
        if (finished) throw new InvalidOperationException("Projection accumulator already completed");
        count = checked(count + 1);
        Append("|" + e.EventId.ToString("N") + ":" + e.ReceiptId.ToString("N") +
            ":" + e.SourceId.ToString("N") + ":" +
            e.SourceEventId.Length.ToString(CultureInfo.InvariantCulture) + ":" +
            e.SourceEventId + ":" + e.EventCode + ":" +
            e.OccurredAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture) + ":" +
            e.ReceivedAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture) + ":" +
            (e.Superseded ? "1" : "0") + ":" +
            (e.ReplacedByReceiptId?.ToString("N") ?? "") + ":" +
            e.ContainerId.ToString("N") + ":" +
            (e.CorrectionId?.ToString("N") ?? ""));

        if (e.Superseded || e.EventCode is not ("GTIN" or "GTOT")) return;
        var next = e.EventCode == "GTIN" ? "EquipmentIn" : "EquipmentOut";
        if (containers.TryGetValue(e.ContainerId, out var prior) &&
            prior.OccurredAtUtc == e.OccurredAtUtc && prior.ObservedState != next)
            next = "Conflict";
        containers[e.ContainerId] = new ContainerTrackingObservation(
            e.ContainerId, next, e.OccurredAtUtc, e.EventId);
        if (lastOccurred is null || e.OccurredAtUtc >= lastOccurred.Value)
        {
            lastOccurred = e.OccurredAtUtc;
            lastEvent = e.EventId;
        }
    }

    public ShipmentTrackingState Complete()
    {
        if (finished) throw new InvalidOperationException("Projection accumulator already completed");
        finished = true;
        var digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        var active = containers.Values.OrderBy(x => x.ContainerId).ToArray();
        string result;
        if (active.Length == 0) result = "Unknown";
        else if (active.Any(x => x.ObservedState == "Conflict")) result = "Conflict";
        else if (active.All(x => x.ObservedState == "EquipmentIn")) result = "EquipmentIn";
        else if (active.All(x => x.ObservedState == "EquipmentOut")) result = "EquipmentOut";
        else result = "Mixed";
        return new ShipmentTrackingState(result, lastEvent, lastOccurred, digest,
            TrackingTimelinePolicy.Version, count, active);
    }

    public void Dispose() => hash.Dispose();
}
