using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HarborOps.Domain.Maritime;

namespace HarborOps.Domain.Tracking;

public sealed record TrackingEvent(string SourceEventId, string EventFamily, string EventCode, string Classifier,
    DateTimeOffset OccurredAtUtc, string? EquipmentReference, string? TransportReference, string ExternalSchemaVersion)
{
    public Guid SourceId { get; init; }
    public Guid OrganizationId { get; init; }

    public TrackingEvent Bind(Guid sourceId, Guid organizationId)
    {
        if (sourceId == Guid.Empty || organizationId == Guid.Empty)
            throw new ArgumentException("Authenticated source and organization required");
        return this with { SourceId = sourceId, OrganizationId = organizationId };
    }
}

public sealed record TrackingValidation(TrackingEvent? Value, int Status, string Code)
{
    public static TrackingValidation Fail(int status, string code) => new(null, status, code);
    public static TrackingValidation Valid(TrackingEvent value) => new(value, 0, "");
}

public static class TrackingEventPolicy
{
    public const int MaximumBodyBytes = 16384;
    public const string Profile = "tnt-2.2-harborops-v1";
    private static readonly HashSet<string> Fields = new(StringComparer.Ordinal)
    {
        "sourceEventId", "eventFamily", "eventCode", "classifier", "occurredAtUtc",
        "equipmentReference", "transportReference", "externalSchemaVersion"
    };
    private static readonly Regex EventId = new(@"\A[A-Za-z0-9._:-]{1,128}\z", RegexOptions.CultureInvariant);
    private static readonly Regex TransportId = new(@"\A[A-Z0-9][A-Z0-9_-]{2,79}\z", RegexOptions.CultureInvariant);
    private static readonly Regex Timestamp = new(
        @"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|\+00:00)\z",
        RegexOptions.CultureInvariant);

    public static TrackingValidation Parse(ReadOnlyMemory<byte> bytes, DateTimeOffset now)
    {
        if (bytes.Length > MaximumBodyBytes) return TrackingValidation.Fail(413, "body_too_large");
        if (bytes.IsEmpty) return TrackingValidation.Fail(400, "invalid_json");
        try
        {
            using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 12
            });
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return TrackingValidation.Fail(400, "invalid_json");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (!seen.Add(property.Name)) return TrackingValidation.Fail(400, "duplicate_property");
                if (!Fields.Contains(property.Name)) return TrackingValidation.Fail(422, "unsupported_tracking_field");
                if (property.Value.ValueKind != JsonValueKind.String)
                    return TrackingValidation.Fail(422, "invalid_tracking_field");
            }
            if (!Get(root, "sourceEventId", out var id) || !EventId.IsMatch(id))
                return TrackingValidation.Fail(422, "invalid_source_event_id");
            if (!Get(root, "eventFamily", out var family) || !Get(root, "eventCode", out var code) ||
                !Get(root, "classifier", out var classifier) || classifier != "ACT")
                return TrackingValidation.Fail(422, "unsupported_tracking_variant");
            if (!Get(root, "externalSchemaVersion", out var externalVersion) || externalVersion != "2.2.0")
                return TrackingValidation.Fail(422, "unsupported_tracking_variant");

            var isEquipment = family == "Equipment" && (code is "GTIN" or "GTOT");
            var isTransport = family == "Transport" && (code is "ARRI" or "DEPA");
            if (!isEquipment && !isTransport)
                return TrackingValidation.Fail(422, "unsupported_tracking_variant");

            if (!Get(root, "occurredAtUtc", out var rawTime) || !Timestamp.IsMatch(rawTime) ||
                !DateTimeOffset.TryParse(rawTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ||
                time.Offset != TimeSpan.Zero || time < new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero) ||
                time > now.AddHours(24))
                return TrackingValidation.Fail(422, "invalid_occurrence_time");

            string? equipment = null;
            string? transport = null;
            if (isEquipment)
            {
                if (!Get(root, "equipmentReference", out equipment) ||
                    root.TryGetProperty("transportReference", out _))
                    return TrackingValidation.Fail(422, "invalid_tracking_reference");
                try
                {
                    equipment = MaritimePolicy.Identifier(equipment.StartsWith("SYN-BOX-", StringComparison.Ordinal)
                        ? "Synthetic" : "ISO6346", equipment);
                }
                catch (ArgumentException)
                {
                    return TrackingValidation.Fail(422, "invalid_tracking_reference");
                }
            }
            if (isTransport)
            {
                if (!Get(root, "transportReference", out transport) ||
                    !TransportId.IsMatch(transport) || root.TryGetProperty("equipmentReference", out _))
                    return TrackingValidation.Fail(422, "invalid_tracking_reference");
            }
            return TrackingValidation.Valid(new TrackingEvent(id, family, code, classifier,
                time.ToUniversalTime(), equipment, transport, externalVersion));
        }
        catch (JsonException)
        {
            return TrackingValidation.Fail(400, "invalid_json");
        }
    }

    private static bool Get(JsonElement root, string name, out string value)
    {
        value = "";
        if (!root.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.String) return false;
        value = property.GetString() ?? "";
        return true;
    }
}
