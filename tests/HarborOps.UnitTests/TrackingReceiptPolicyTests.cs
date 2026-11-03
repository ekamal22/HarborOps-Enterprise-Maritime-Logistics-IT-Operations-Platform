using System.Text;
using HarborOps.Domain.Tracking;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class TrackingReceiptPolicyTests
{
    private static readonly Guid Organization = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Source = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private static TrackingEvent Event(string timestamp, string id = "receipt-1",
        string code = "GTIN")
    {
        var json = $$"""
            {"sourceEventId":"{{id}}","eventFamily":"Equipment","eventCode":"{{code}}",
             "classifier":"ACT","occurredAtUtc":"{{timestamp}}",
             "equipmentReference":"SYN-BOX-ABCD","externalSchemaVersion":"2.2.0"}
            """;
        var parsed = TrackingEventPolicy.Parse(Encoding.UTF8.GetBytes(json), Now);
        Assert.NotNull(parsed.Value);
        return parsed.Value!.Bind(Source, Organization);
    }

    [Fact]
    public void CanonicalReplayIgnoresEquivalentUtcSerialization()
    {
        var a = TrackingReceiptPolicy.Canonicalize(Event("2026-11-03T10:00:00Z"));
        var b = TrackingReceiptPolicy.Canonicalize(Event("2026-11-03T10:00:00+00:00"));
        Assert.Equal(a.PayloadUtf8, b.PayloadUtf8);
        Assert.Equal(a.Digest, b.Digest);
        Assert.Contains(TrackingReceiptPolicy.EncodingVersion, Encoding.UTF8.GetString(a.PayloadUtf8));
    }

    [Fact]
    public void ChangedEventIdentityAndMeaningProduceDistinctDigests()
    {
        var a = TrackingReceiptPolicy.Canonicalize(Event("2026-11-03T10:00:00Z"));
        Assert.NotEqual(a.Digest, TrackingReceiptPolicy.Canonicalize(
            Event("2026-11-03T10:00:00Z", "receipt-2")).Digest);
        Assert.NotEqual(a.Digest, TrackingReceiptPolicy.Canonicalize(
            Event("2026-11-03T10:00:00Z", "receipt-1", "GTOT")).Digest);
    }

    [Fact]
    public void CanonicalizerRequiresBoundServerIdentity()
    {
        var incoming = Event("2026-11-03T10:00:00Z");
        Assert.Throws<ArgumentException>(() => TrackingReceiptPolicy.Canonicalize(
            incoming with { SourceId = Guid.Empty }));
        Assert.Throws<ArgumentException>(() => TrackingReceiptPolicy.Canonicalize(
            incoming with { OrganizationId = Guid.Empty }));
    }
}
