using HarborOps.Domain.Organizations;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class AuditPermissionTests
{
    [Fact]
    public void OnlyAuditorHasExplicitAuditReadPermission()
    {
        foreach (var role in Enum.GetValues<OrganizationRole>())
        {
            Assert.Equal(role == OrganizationRole.Auditor,
                OrganizationPolicy.Allows(role, OrganizationPermission.ViewAuditEvents));
        }
    }

    [Fact]
    public void AuditPermissionDoesNotBroadenAdministrativeGrants()
    {
        Assert.False(OrganizationPolicy.Allows(OrganizationRole.Administrator,
            OrganizationPermission.ViewAuditEvents));
        Assert.False(OrganizationPolicy.Allows(OrganizationRole.Support,
            OrganizationPermission.ViewAuditEvents));
        Assert.True(OrganizationPolicy.Allows(OrganizationRole.Administrator,
            OrganizationPermission.ManageMemberships));
        Assert.False(OrganizationPolicy.Allows(OrganizationRole.Auditor,
            OrganizationPermission.ManageMemberships));
    }
}
