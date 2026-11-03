using System.Text;
using HarborOps.Domain.Tracking;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class TrackingEventPolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    private static string Valid(string family = "Equipment", string code = "GTIN",
        string reference = "\"equipmentReference\":\"SYN-BOX-ABCD\"") =>
        "{\"sourceEventId\":\"event-1\",\"eventFamily\":\"" + family +
        "\",\"eventCode\":\"" + code +
        "\",\"classifier\":\"ACT\",\"occurredAtUtc\":\"2026-11-03T09:00:00Z\"," +
        reference + ",\"externalSchemaVersion\":\"2.2.0\"}";

    private static TrackingValidation Parse(string json) =>
        TrackingEventPolicy.Parse(Encoding.UTF8.GetBytes(json), Now);

    [Theory]
    [InlineData("Equipment", "GTIN", "\"equipmentReference\":\"SYN-BOX-ABCD\"")]
    [InlineData("Equipment", "GTOT", "\"equipmentReference\":\"SYN-BOX-WXYZ\"")]
    [InlineData("Transport", "ARRI", "\"transportReference\":\"VOYAGE-12\"")]
    [InlineData("Transport", "DEPA", "\"transportReference\":\"VOYAGE-12\"")]
    public void ParsesOnlyExplicitSupportedSubset(string family, string code, string reference)
    {
        var result = Parse(Valid(family, code, reference));
        Assert.NotNull(result.Value);
        Assert.Equal(family, result.Value!.EventFamily);
        Assert.Equal(code, result.Value.EventCode);
        Assert.Equal(TimeSpan.Zero, result.Value.OccurredAtUtc.Offset);
    }

    [Theory]
    [InlineData("Shipment", "RECE")]
    [InlineData("IoT", "TEMP")]
    [InlineData("Reefer", "TEMP")]
    [InlineData("Transport", "BAD")]
    public void RejectsUnsupportedFamilyAndCodes(string family, string code) =>
        Assert.Equal("unsupported_tracking_variant",
            Parse(Valid(family, code)).Code);

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("{\"sourceEventId\":\"a\",\"sourceEventId\":\"b\"}")]
    public void RejectsMalformedAndDuplicateJson(string json) =>
        Assert.Equal(400, Parse(json).Status);

    [Theory]
    [InlineData("\"organizationId\":\"any\"")]
    [InlineData("\"sourceId\":\"any\"")]
    [InlineData("\"unknownFutureField\":\"yes\"")]
    public void RejectsSpoofedAndUnrecognizedKeys(string field)
    {
        var value = Valid().Replace("\"externalSchemaVersion\":", field +
            ",\"externalSchemaVersion\":", StringComparison.Ordinal);
        Assert.Equal(422, Parse(value).Status);
    }

    [Theory]
    [InlineData("2026-11-03T09:00:00+03:00")]
    [InlineData("2026-11-03T09:00:00")]
    [InlineData("1999-12-31T23:59:59Z")]
    [InlineData("2026-11-04T10:00:01Z")]
    public void RejectsNonUtcAndOutOfWindowEvents(string time)
    {
        var value = Valid().Replace("2026-11-03T09:00:00Z", time, StringComparison.Ordinal);
        Assert.Equal("invalid_occurrence_time", Parse(value).Code);
    }

    [Fact]
    public void AcceptsExplicitZeroOffsetAndRejectsWrongReferenceFamily()
    {
        Assert.NotNull(Parse(Valid().Replace("2026-11-03T09:00:00Z",
            "2026-11-03T09:00:00+00:00", StringComparison.Ordinal)).Value);
        Assert.Equal("invalid_tracking_reference",
            Parse(Valid("Equipment", "GTIN", "\"transportReference\":\"VOYAGE-12\"")).Code);
    }

    [Fact]
    public void AppliesStrictBodyBoundIncludingInvalidUtf8()
    {
        Assert.Equal(413, TrackingEventPolicy.Parse(new byte[16385], Now).Status);
        Assert.Equal(400, TrackingEventPolicy.Parse(new byte[] { 0xFF }, Now).Status);
    }

    [Fact]
    public void SourceCodesAndCredentialWindowsAreConstrained()
    {
        Assert.Equal("carrier_1", IntegrationSourcePolicy.NormalizeCode("Carrier_1"));
        Assert.Throws<ArgumentException>(() => IntegrationSourcePolicy.NormalizeCode("../bad"));
        Assert.True(IntegrationSourcePolicy.IsValidWindow(Now, Now.AddDays(90), Now));
        Assert.False(IntegrationSourcePolicy.IsValidWindow(Now, Now.AddDays(91), Now));
    }

    [Theory]
    [InlineData("event-1\\n", "invalid_source_event_id")]
    [InlineData("event-1\\r", "invalid_source_event_id")]
    [InlineData("event-1\\t", "invalid_source_event_id")]
    [InlineData("event 1", "invalid_source_event_id")]
    public void EventIdentityRejectsWhitespaceAndDecodedControls(string escaped, string expected)
    {
        var value = Valid().Replace("event-1", escaped, StringComparison.Ordinal);
        Assert.Equal(expected, Parse(value).Code);
    }

    [Theory]
    [InlineData("VOYAGE-12\\n")]
    [InlineData("VOYAGE-12\\r")]
    [InlineData("VOYAGE-12\\t")]
    [InlineData("voyage-12")]
    [InlineData("V")]
    public void TransportReferenceRejectsMalformedValues(string escaped)
    {
        var json = Valid("Transport", "ARRI", "\"transportReference\":\"" + escaped + "\"");
        Assert.Equal("invalid_tracking_reference", Parse(json).Code);
    }

    [Fact]
    public void RejectsLegacyWireNamesAndReplayedFields()
    {
        Assert.Equal(422, Parse(Valid().Replace("\"eventFamily\":", "\"family\":",
            StringComparison.Ordinal)).Status);
        Assert.Equal(422, Parse(Valid().Replace("\"eventCode\":", "\"code\":",
            StringComparison.Ordinal)).Status);
        Assert.Equal(400, Parse(Valid().Replace("\"eventFamily\":\"Equipment\"",
            "\"eventFamily\":\"Equipment\",\"eventFamily\":\"Transport\"",
            StringComparison.Ordinal)).Status);
    }

    [Fact]
    public void ValidJsonAtExactBodyLimitIsAcceptedAndOneExtraByteIsRejected()
    {
        var original = Valid();
        var body = original + new string(' ', TrackingEventPolicy.MaximumBodyBytes -
            Encoding.UTF8.GetByteCount(original));
        Assert.NotNull(Parse(body).Value);
        Assert.Equal(413, Parse(body + " ").Status);
    }

    [Fact]
    public void TimestampPolicyUsesAbsoluteEndAndStrictUtc()
    {
        var atCutoff = Valid().Replace("2026-11-03T09:00:00Z",
            "2026-11-04T10:00:00Z", StringComparison.Ordinal);
        Assert.NotNull(Parse(atCutoff).Value);
        Assert.Equal("invalid_occurrence_time", Parse(atCutoff.Replace(
            "2026-11-04T10:00:00Z", "2026-11-04T10:00:00Z\\n",
            StringComparison.Ordinal)).Code);
        var invalidLeap = Valid().Replace("2026-11-03T09:00:00Z",
            "2026-02-29T10:00:00Z", StringComparison.Ordinal);
        Assert.Equal("invalid_occurrence_time", Parse(invalidLeap).Code);
    }

    [Theory]
    [InlineData("SYN-BOX-ABCD\\n")]
    [InlineData("SYN-BOX-ABCD\\t")]
    [InlineData("SYN-BOX-ABCD ")]
    [InlineData(" SYN-BOX-ABCD")]
    public void EquipmentReferencesDoNotSilentlyTrimControlCharacters(string reference)
    {
        var value = Valid("Equipment", "GTIN",
            "\"equipmentReference\":\"" + reference + "\"");
        Assert.Equal("invalid_tracking_reference", Parse(value).Code);
    }

    [Fact]
    public void CanonicalEventBindsOnlyValidatedServerIdentity()
    {
        var original = Parse(Valid()).Value!;
        Assert.Equal(Guid.Empty, original.OrganizationId);
        var sourceId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var bound = original.Bind(sourceId, organizationId);
        Assert.Equal(sourceId, bound.SourceId);
        Assert.Equal(organizationId, bound.OrganizationId);
        Assert.Equal(Guid.Empty, original.SourceId);
        Assert.Throws<ArgumentException>(() => original.Bind(Guid.Empty, organizationId));
    }
}
