using HarborOps.Domain.Assignments;
using HarborOps.Domain.Organizations;
using HarborOps.Infrastructure.Assignments;
using System.Text.Json;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class AssignmentPolicyTests
{
    [Theory]
    [InlineData("2026-11-03T10:00:00Z")]
    [InlineData("2026-11-03T10:00:00+00:00")]
    public void AcceptsExplicitUtc(string value) =>
        Assert.Equal(TimeSpan.Zero, AssignmentPolicy.Utc(value).Offset);

    [Theory]
    [InlineData("2026-11-03")]
    [InlineData("2026-11-03T10:00:00+03:00")]
    [InlineData("2026-11-03T10:00:00")]
    [InlineData("bad")]
    [InlineData("")]
    public void RejectsNonUtcAndAmbiguousInstants(string value) =>
        Assert.Throws<ArgumentException>(() => AssignmentPolicy.Utc(value));

    [Fact]
    public void HalfOpenWindowsAllowAdjacencyButNotOverlap()
    {
        var first = AssignmentPolicy.Utc("2026-11-03T10:00:00Z");
        var mid = first.AddHours(1);
        var end = mid.AddHours(1);
        AssignmentPolicy.Window(first, mid);
        AssignmentPolicy.Window(mid, end);
        Assert.False(AssignmentPolicy.Overlaps(first, mid, mid, end));
        Assert.True(AssignmentPolicy.Overlaps(first, mid, mid.AddTicks(-1), end));
        Assert.True(AssignmentPolicy.Overlaps(first, null, end, null));
        Assert.False(AssignmentPolicy.Overlaps(end, null, first, mid));
        Assert.Throws<ArgumentException>(() => AssignmentPolicy.Window(first, first));
        Assert.Throws<ArgumentException>(() => AssignmentPolicy.Window(end, first));
    }

    [Fact]
    public void RouteAndKnownScheduleMustMatch()
    {
        var origin = Guid.NewGuid();
        var destination = Guid.NewGuid();
        var a = AssignmentPolicy.Utc("2026-11-03T10:00:00Z");
        var b = a.AddHours(1);
        Assert.True(AssignmentPolicy.VoyageCompatible(origin, destination, a, b,
            origin, destination, b.AddMinutes(-1), b.AddMinutes(30)));
        Assert.False(AssignmentPolicy.VoyageCompatible(origin, destination, a, b,
            destination, origin, a, b));
        Assert.False(AssignmentPolicy.VoyageCompatible(origin, destination, a, b,
            origin, destination, b, b.AddHours(1)));
        Assert.True(AssignmentPolicy.VoyageCompatible(origin, destination, a, b,
            origin, destination, null, null));
    }

    [Fact]
    public void AssignmentPermissionsAreNarrow()
    {
        foreach (var role in Enum.GetValues<OrganizationRole>())
        {
            bool write = role is OrganizationRole.Analyst or OrganizationRole.Manager;
            Assert.Equal(write || role is OrganizationRole.Auditor,
                OrganizationPolicy.Allows(role, OrganizationPermission.ViewShipmentAssignments));
            Assert.Equal(write,
                OrganizationPolicy.Allows(role, OrganizationPermission.ManageShipmentAssignments));
            if (role is OrganizationRole.Auditor)
                Assert.False(OrganizationPolicy.Allows(role, OrganizationPermission.ViewVoyageData));
        }
    }

    [Fact]
    public void StrictPayloadRejectsForbiddenFieldsAndMalformedClose()
    {
        static AssignmentInput Parse(string json, bool close = false)
        {
            using var document = JsonDocument.Parse(json);
            return AssignmentInput.Parse(document.RootElement, false, close);
        }
        var id = Guid.NewGuid();
        var create = Parse("{\"containerId\":\"" + id +
            "\",\"startsAtUtc\":\"2026-11-03T10:00:00Z\"}");
        Assert.Equal(id, create.TargetId);
        Assert.Null(create.EndsAtUtc);
        Assert.Throws<ArgumentException>(() => Parse("{\"containerId\":\"" + id +
            "\",\"startsAtUtc\":\"2026-11-03T10:00:00Z\",\"organizationId\":\"" + id + "\"}"));
        Assert.Throws<ArgumentException>(() => Parse("{\"containerId\":\"" + id +
            "\",\"containerId\":\"" + id + "\",\"startsAtUtc\":\"2026-11-03T10:00:00Z\"}"));
        Assert.Throws<ArgumentException>(() => Parse("{\"expectedVersion\":1}", true));
        Assert.Throws<ArgumentException>(() => Parse("{\"expectedVersion\":0,\"endsAtUtc\":\"2026-11-04T10:00:00Z\"}", true));
        Assert.Equal(1L, Parse("{\"expectedVersion\":1,\"endsAtUtc\":\"2026-11-04T10:00:00Z\"}", true).ExpectedVersion);
    }
}
