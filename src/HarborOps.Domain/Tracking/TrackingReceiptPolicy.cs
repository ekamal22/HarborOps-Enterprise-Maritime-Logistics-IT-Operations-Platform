using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace HarborOps.Domain.Tracking;

// The canonical v1 encoding is independent of original JSON field order,
// whitespace and the two permitted representations of UTC timestamps.
public sealed record TrackingCanonicalEvent(byte[] PayloadUtf8, byte[] Digest);

public static class TrackingReceiptPolicy
{
    public const string EncodingVersion = "m8-canonical-v1";

    public static TrackingCanonicalEvent Canonicalize(TrackingEvent value)
    {
        if (value.OrganizationId == Guid.Empty || value.SourceId == Guid.Empty)
            throw new ArgumentException("Server-bound source identity is required", nameof(value));

        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteString("canonicalVersion", EncodingVersion);
            json.WriteString("profile", TrackingEventPolicy.Profile);
            json.WriteString("sourceEventId", value.SourceEventId);
            json.WriteString("eventFamily", value.EventFamily);
            json.WriteString("eventCode", value.EventCode);
            json.WriteString("classifier", value.Classifier);
            json.WriteString("occurredAtUtc",
                value.OccurredAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            json.WriteString("equipmentReference", value.EquipmentReference);
            json.WriteString("transportReference", value.TransportReference);
            json.WriteString("externalSchemaVersion", value.ExternalSchemaVersion);
            json.WriteEndObject();
            json.Flush();
        }

        var encoded = stream.ToArray();
        if (encoded.Length is 0 or > TrackingEventPolicy.MaximumBodyBytes)
            throw new ArgumentException("Canonical event exceeds M7 bounds", nameof(value));
        return new TrackingCanonicalEvent(encoded, SHA256.HashData(encoded));
    }
}
