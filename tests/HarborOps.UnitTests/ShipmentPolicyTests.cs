using HarborOps.Domain.Organizations;
using HarborOps.Domain.Shipments;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class ShipmentPolicyTests
{
    public static IEnumerable<object[]> AllWorkflowPairs()
    {
        var states = new[] { "Draft", "Booked", "InTransit", "Arrived", "Completed", "Cancelled" };
        foreach (var from in states)
            foreach (var to in states)
            {
                var valid = from == to ||
                    from == "Draft" && to is "Booked" or "Cancelled" ||
                    from == "Booked" && to is "InTransit" or "Cancelled" ||
                    from == "InTransit" && to is "Arrived" or "Cancelled" ||
                    from == "Arrived" && to == "Completed";
                yield return new object[] { from, to, valid };
            }
    }
    [Theory]
    [MemberData(nameof(AllWorkflowPairs))]
    public void FullWorkflowMatrix(string from, string to, bool valid) =>
        Assert.Equal(valid, ShipmentPolicy.CanTransition(from, to));

    [Theory]
    [InlineData("Draft", "Booked", true)]
    [InlineData("Draft", "Cancelled", true)]
    [InlineData("Booked", "InTransit", true)]
    [InlineData("Booked", "Cancelled", true)]
    [InlineData("InTransit", "Arrived", true)]
    [InlineData("InTransit", "Cancelled", true)]
    [InlineData("Arrived", "Completed", true)]
    [InlineData("Draft", "Completed", false)]
    [InlineData("Booked", "Arrived", false)]
    [InlineData("InTransit", "Booked", false)]
    [InlineData("Arrived", "Cancelled", false)]
    [InlineData("Completed", "Draft", false)]
    [InlineData("Cancelled", "Draft", false)]
    [InlineData("Completed", "Completed", true)]
    [InlineData("Cancelled", "Cancelled", true)]
    public void WorkflowMatrix(string from, string to, bool expected) =>
        Assert.Equal(expected, ShipmentPolicy.CanTransition(from, to));

    [Theory]
    [InlineData(" draft ")]
    [InlineData("Unknown")]
    [InlineData("Delivered")]
    public void RejectUnknownStatus(string value) =>
        Assert.Throws<ArgumentException>(() => ShipmentPolicy.Workflow(value));

    [Fact]
    public void ReferencesAndUtcAreStrict()
    {
        Assert.Equal("SHIP-123", ShipmentPolicy.Code(" ship-123 "));
        Assert.Equal("EXT/1-A", ShipmentPolicy.ExternalReference(" ext/1-a "));
        Assert.Null(ShipmentPolicy.ExternalReference(null));
        Assert.Null(ShipmentPolicy.Name(""));
        Assert.Throws<ArgumentException>(() => ShipmentPolicy.Code("bad ref"));
        Assert.Throws<ArgumentException>(() => ShipmentPolicy.ExternalReference("ref space"));
        Assert.Throws<ArgumentException>(() => ShipmentPolicy.ExternalReference("ref\u0000"));
        Assert.Throws<ArgumentException>(() => ShipmentPolicy.ExternalReference(new string('X', 81)));
        Assert.Throws<ArgumentException>(() => ShipmentPolicy.Utc("2026-11-01T10:00:00+03:00"));
        Assert.Throws<ArgumentException>(() => ShipmentPolicy.Utc("2026-11-01"));
        var departure = ShipmentPolicy.Utc("2026-11-01T09:00:00Z");
        var arrival = ShipmentPolicy.Utc("2026-11-01T10:00:00Z");
        ShipmentPolicy.ValidateTimes(departure, arrival);
        ShipmentPolicy.ValidateTimes(null, arrival);
        ShipmentPolicy.ValidateTimes(departure, null);
        Assert.Throws<ArgumentException>(() => ShipmentPolicy.ValidateTimes(arrival, departure));
        Assert.Throws<ArgumentException>(() => ShipmentPolicy.ValidateTimes(departure, departure));
    }

    [Fact]
    public void ShipmentRolesAreIndependentOfM4()
    {
        foreach (var role in Enum.GetValues<OrganizationRole>())
        {
            var manage = role is OrganizationRole.Analyst or OrganizationRole.Manager;
            Assert.Equal(manage || role == OrganizationRole.Auditor,
                OrganizationPolicy.Allows(role, OrganizationPermission.ViewShipmentData));
            Assert.Equal(manage, OrganizationPolicy.Allows(role, OrganizationPermission.ManageShipmentData));
            Assert.Equal(role == OrganizationRole.Manager,
                OrganizationPolicy.Allows(role, OrganizationPermission.ManageContainerData));
        }
    }
}
