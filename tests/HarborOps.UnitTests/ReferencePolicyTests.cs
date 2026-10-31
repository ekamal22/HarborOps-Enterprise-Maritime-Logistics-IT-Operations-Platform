using HarborOps.Domain.Organizations;
using HarborOps.Domain.References;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class ReferencePolicyTests
{
    [Theory]
    [InlineData("port-a", "PORT-A")]
    [InlineData("  vessel001 ", "VESSEL001")]
    public void NormalizesLocalCodes(string input, string expected) =>
        Assert.Equal(expected, ReferencePolicy.Code(input));

    [Theory]
    [InlineData("bad code")]
    [InlineData("a")]
    [InlineData("-PORT")]
    [InlineData("PORT_1")]
    public void RejectsInvalidLocalCodes(string input) =>
        Assert.Throws<ArgumentException>(() => ReferencePolicy.Code(input));

    [Theory]
    [InlineData("USNYC", "US", "USNYC")]
    [InlineData("US NYC", "us", "USNYC")]
    [InlineData("trist", "TR", "TRIST")]
    public void ValidatesOptionalLocodes(string input, string country, string expected) =>
        Assert.Equal(expected, ReferencePolicy.UnLocode(input, ReferencePolicy.Country(country)));

    [Theory]
    [InlineData("USN1C", "US")]
    [InlineData("USN0C", "US")]
    [InlineData("USNYC", "GB")]
    [InlineData("US NY C", "US")]
    public void RejectsInvalidLocodes(string input, string country) =>
        Assert.Throws<ArgumentException>(() => ReferencePolicy.UnLocode(input, country));

    [Fact]
    public void ExternalLocodeDoesNotBecomeMandatory() =>
        Assert.Null(ReferencePolicy.UnLocode(null, "US"));

    [Theory]
    [InlineData("9074729", "9074729")]
    [InlineData("IMO9074729", "9074729")]
    [InlineData("IMO 9074729", "9074729")]
    public void AcceptsChecksumValidImo(string input, string expected) =>
        Assert.Equal(expected, ReferencePolicy.Imo(input));

    [Theory]
    [InlineData("9074728")]
    [InlineData("IMO90747280")]
    [InlineData("907472")]
    [InlineData("ABC1234")]
    public void RejectsInvalidImo(string input) =>
        Assert.Throws<ArgumentException>(() => ReferencePolicy.Imo(input));

    [Fact]
    public void OptionalImoAndMmsiAreNotMandatory()
    {
        Assert.Null(ReferencePolicy.Imo(null));
        Assert.Null(ReferencePolicy.Mmsi(""));
        Assert.Equal("012345678", ReferencePolicy.Mmsi("012345678"));
    }

    [Theory]
    [InlineData("000000000")]
    [InlineData("12345678")]
    [InlineData("12345678x")]
    [InlineData("1234567890")]
    public void RejectsInvalidMmsi(string input) =>
        Assert.Throws<ArgumentException>(() => ReferencePolicy.Mmsi(input));

    [Fact]
    public void ReferencePermissionsAreExplicitAndDoNotEscalateOrganizationRoles()
    {
        foreach (var role in Enum.GetValues<OrganizationRole>())
        {
            Assert.Equal(role is OrganizationRole.Analyst or OrganizationRole.Manager,
                OrganizationPolicy.Allows(role, OrganizationPermission.ViewReferenceData));
            Assert.Equal(role == OrganizationRole.Manager,
                OrganizationPolicy.Allows(role, OrganizationPermission.ManageReferenceData));
            Assert.True(OrganizationPolicy.Allows(role, OrganizationPermission.ViewOrganization));
            Assert.Equal(role == OrganizationRole.Administrator,
                OrganizationPolicy.Allows(role, OrganizationPermission.ManageMemberships));
        }
    }

    [Fact]
    public void RequiresBoundedNameCountryAndStatus()
    {
        Assert.Equal("North Terminal", ReferencePolicy.Name(" North Terminal "));
        Assert.Equal("BD", ReferencePolicy.Country(" bd "));
        Assert.Throws<ArgumentException>(() => ReferencePolicy.Name(new string('X', 161)));
        Assert.Throws<ArgumentException>(() => ReferencePolicy.Country("TUR"));
        Assert.Throws<ArgumentException>(() => ReferencePolicy.Status("Removed"));
        Assert.Equal("Archived", ReferencePolicy.Status("Archived"));
    }
}
