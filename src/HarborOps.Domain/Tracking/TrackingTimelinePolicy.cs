using System.Security.Cryptography;
using System.Text;

namespace HarborOps.Domain.Tracking;

public sealed record ShipmentTimelineEvent(
    Guid EventId, Guid ReceiptId, Guid SourceId, string SourceEventId,
    string EventCode, DateTimeOffset OccurredAtUtc, DateTimeOffset ReceivedAtUtc,
    bool Superseded, Guid? ReplacedByReceiptId);

public sealed record ShipmentTrackingState(
    string ObservedTrackingState, Guid? LastEventId, DateTimeOffset? LastOccurredAtUtc,
    string InputDigest, string PolicyVersion, int EventCount);

public static class TrackingTimelinePolicy
{
    public const string Version = "m10-equipment-v1";

    public static IReadOnlyList<ShipmentTimelineEvent> Order(IEnumerable<ShipmentTimelineEvent> items)
        => items.OrderBy(e => e.OccurredAtUtc)
            .ThenBy(e => e.ReceivedAtUtc)
            .ThenBy(e => e.SourceId)
            .ThenBy(e => e.SourceEventId, StringComparer.Ordinal)
            .ThenBy(e => e.ReceiptId)
            .ThenBy(e => e.EventId)
            .ToArray();

    public static ShipmentTrackingState Reduce(Guid organizationId, Guid shipmentId,
        IEnumerable<ShipmentTimelineEvent> input)
    {
        var ordered = Order(input);
        var data = new StringBuilder(Version).Append('|').Append(organizationId.ToString("N"))
            .Append('|').Append(shipmentId.ToString("N"));
        foreach (var e in ordered)
        {
            data.Append('|').Append(e.EventId.ToString("N"))
                .Append(':').Append(e.ReceiptId.ToString("N"))
                .Append(':').Append(e.SourceId.ToString("N"))
                .Append(':').Append(e.SourceEventId.Length).Append(':').Append(e.SourceEventId)
                .Append(':').Append(e.EventCode)
                .Append(':').Append(e.OccurredAtUtc.UtcTicks)
                .Append(':').Append(e.ReceivedAtUtc.UtcTicks)
                .Append(':').Append(e.Superseded ? "1" : "0")
                .Append(':').Append(e.ReplacedByReceiptId?.ToString("N") ?? "");
        }
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data.ToString())))
            .ToLowerInvariant();

        var effective = ordered.Where(e => !e.Superseded &&
            e.EventCode is ("GTIN" or "GTOT")).ToArray();
        if (effective.Length == 0)
            return new ShipmentTrackingState("Unknown", null, null, digest, Version, ordered.Count);

        var last = effective[^1];
        var sameInstant = effective.Where(e => e.OccurredAtUtc == last.OccurredAtUtc).ToArray();
        var conflicting = sameInstant.Any(e => e.EventCode != last.EventCode);
        return new ShipmentTrackingState(conflicting ? "Conflict" :
            last.EventCode == "GTIN" ? "EquipmentIn" : "EquipmentOut",
            last.EventId, last.OccurredAtUtc, digest, Version, ordered.Count);
    }
}
