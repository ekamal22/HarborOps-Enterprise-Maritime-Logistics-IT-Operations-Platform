using HarborOps.Domain.Organizations;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class OrganizationPolicyTests
{
    [Theory]
    [InlineData(" Analyst", false)]
    [InlineData("Analyst", true)]
    [InlineData("Manager", true)]
    [InlineData("Support", true)]
    [InlineData("Administrator", true)]
    [InlineData("Auditor", true)]
    [InlineData("SystemOwner", false)]
    public void AcceptsOnlyDeclaredRoles(string input, bool expected) =>
        Assert.Equal(expected, OrganizationPolicy.ParseRole(input, out _));

    [Theory]
    [InlineData("  Port-Team  ", "port-team")]
    [InlineData("TERM3", "term3")]
    public void NormalizesSlugs(string input, string normalized) =>
        Assert.Equal(normalized, OrganizationPolicy.NormalizeSlug(input));

    [Theory]
    [InlineData("Invalid!")]
    [InlineData("-invalid")]
    [InlineData("a")]
    [InlineData("has spaces")]
    public void RejectsInvalidSlugs(string input) =>
        Assert.Throws<ArgumentException>(() => OrganizationPolicy.NormalizeSlug(input));

    [Fact]
    public void EveryRoleCanViewOnlyTheOrganizationSummary()
    {
        foreach (var role in Enum.GetValues<OrganizationRole>())
        {
            Assert.True(OrganizationPolicy.Allows(role, OrganizationPermission.ViewOrganization));
            Assert.Equal(role == OrganizationRole.Administrator,
                OrganizationPolicy.Allows(role, OrganizationPermission.ViewMemberships));
            Assert.Equal(role == OrganizationRole.Administrator,
                OrganizationPolicy.Allows(role, OrganizationPermission.ManageMemberships));
            Assert.Equal(role == OrganizationRole.Administrator,
                OrganizationPolicy.Allows(role, OrganizationPermission.UpdateOrganization));
        }
    }
}
