using HarborOps.Domain.Maritime;
using HarborOps.Domain.Organizations;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class MaritimePolicyTests
{
    [Theory]
    [InlineData("CSQU3054383", "CSQU3054383")]
    [InlineData(" csqu 3054383 ", "CSQU3054383")]
    [InlineData("MSCU6639870", "MSCU6639870")]
    [InlineData("TGHU1234567", "TGHU1234567")]
    [InlineData("ABCU1234560", "ABCU1234560")]
    [InlineData("CMAU1234564", "CMAU1234564")]
    [InlineData("OOLU1234567", "OOLU1234567")]
    public void KnownIsoChecksumVectorsAreAccepted(string input, string normalized) =>
        Assert.Equal(normalized, MaritimePolicy.Identifier("ISO6346", input));
    [Theory]
    [InlineData("CSQU3054382")]
    [InlineData("CSQJ3054383")]
    [InlineData("CSQU305438")]
    [InlineData("CSQU30543833")]
    [InlineData("SYN-BOX-TEST1")]
    [InlineData("MSCU6639871")]
    [InlineData("TGHU1234568")]
    [InlineData("ABCU1234561")]
    [InlineData("CMAU1234565")]
    [InlineData("OOLU1234568")]
    [InlineData("CSQU3054383\u0000")]
    public void IncorrectIsoIdentifiersAreRejected(string value) =>
        Assert.Throws<ArgumentException>(() => MaritimePolicy.Identifier("ISO6346", value));
    [Fact]
    public void SyntheticIdentifiersCannotMasqueradeAsIso()
    {
        Assert.Equal("SYN-BOX-ALPHA", MaritimePolicy.Identifier("Synthetic", " syn-box-alpha "));
        Assert.Throws<ArgumentException>(() => MaritimePolicy.Identifier("Synthetic", "CSQU3054383"));
        Assert.Throws<ArgumentException>(() => MaritimePolicy.Identifier("Synthetic", "SYN-BOX-AAA"));
        Assert.Throws<ArgumentException>(() => MaritimePolicy.Identifier("Wrong", "SYN-BOX-ALPHA"));
    }
    [Fact]
    public void EquipmentCatalogIsClosed()
    {
        foreach (var value in new[] { "Dry20", "Dry40", "HighCube40", "Reefer20", "Reefer40", "Other" })
            Assert.Equal(value, MaritimePolicy.EquipmentType(value));
        Assert.Throws<ArgumentException>(() => MaritimePolicy.EquipmentType("Refrigerated"));
    }
    [Fact]
    public void VoyagesRequireConsistentNullableWindowsAndProgress()
    {
        var departure = DateTimeOffset.Parse("2026-11-01T21:00:00Z");
        var arrival = DateTimeOffset.Parse("2026-11-02T02:00:00Z");
        MaritimePolicy.ValidateTimes(departure, arrival, null, null, "Planned");
        MaritimePolicy.ValidateTimes(null, null, departure, null, "Underway");
        MaritimePolicy.ValidateTimes(null, null, departure, arrival, "Arrived");
        Assert.Throws<ArgumentException>(() =>
            MaritimePolicy.ValidateTimes(arrival, departure, null, null, "Planned"));
        Assert.Throws<ArgumentException>(() =>
            MaritimePolicy.ValidateTimes(null, null, null, arrival, "Arrived"));
        Assert.Throws<ArgumentException>(() =>
            MaritimePolicy.ValidateTimes(null, null, departure, null, "Planned"));
        Assert.Throws<ArgumentException>(() =>
            MaritimePolicy.ValidateTimes(null, null, departure, arrival, "Underway"));
    }
    [Theory]
    [InlineData("Planned", "Underway", true)]
    [InlineData("Planned", "Cancelled", true)]
    [InlineData("Underway", "Arrived", true)]
    [InlineData("Planned", "Arrived", false)]
    [InlineData("Arrived", "Underway", false)]
    [InlineData("Cancelled", "Planned", false)]
    [InlineData("Arrived", "Arrived", true)]
    [InlineData("Cancelled", "Cancelled", true)]
    [InlineData("Underway", "Cancelled", false)]
    public void TransitionGraphIsClosed(string start, string end, bool allowed) =>
        Assert.Equal(allowed, MaritimePolicy.Transition(start, end));
    [Fact]
    public void UtcInputsRequireExplicitZeroOffset()
    {
        Assert.Equal(DateTimeOffset.Parse("2026-10-31T10:00:00Z"),
            MaritimePolicy.Utc("2026-10-31T10:00:00Z"));
        Assert.Null(MaritimePolicy.Utc(null));
        Assert.Throws<ArgumentException>(() => MaritimePolicy.Utc("2026-10-31T10:00:00+03:00"));
        Assert.Throws<ArgumentException>(() => MaritimePolicy.Utc("2026-10-31"));
    }
    [Fact]
    public void RoleGrantsStayWithinBusinessPermissions()
    {
        foreach (var role in Enum.GetValues<OrganizationRole>())
        {
            var viewing = role is OrganizationRole.Analyst or OrganizationRole.Manager;
            var managing = role == OrganizationRole.Manager;
            foreach (var permission in new[] {
                OrganizationPermission.ViewVoyageData, OrganizationPermission.ViewContainerData })
                Assert.Equal(viewing, OrganizationPolicy.Allows(role, permission));
            foreach (var permission in new[] {
                OrganizationPermission.ManageVoyageData, OrganizationPermission.ManageContainerData })
                Assert.Equal(managing, OrganizationPolicy.Allows(role, permission));
        }
    }
}
