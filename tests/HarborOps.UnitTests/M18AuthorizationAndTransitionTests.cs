using HarborOps.Domain.Incidents;
using HarborOps.Domain.Organizations;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class M18AuthorizationAndTransitionTests
{
    [Theory]
    [InlineData(OrganizationRole.Support,true,true,true)]
    [InlineData(OrganizationRole.Administrator,true,true,true)]
    [InlineData(OrganizationRole.Manager,true,false,true)]
    [InlineData(OrganizationRole.Analyst,false,false,false)]
    [InlineData(OrganizationRole.Auditor,false,false,false)]
    public void NewTypedPermissionsDoNotWidenExistingRoleAccess(
        OrganizationRole role,bool summary,bool technical,bool create)
    {
        Assert.Equal(summary,OrganizationPolicy.Allows(role,OrganizationPermission.ViewIncidentSummary));
        Assert.Equal(technical,OrganizationPolicy.Allows(role,OrganizationPermission.ViewIncidentDetail));
        Assert.Equal(create,OrganizationPolicy.Allows(role,OrganizationPermission.CreateIncident));
        Assert.Equal(technical,OrganizationPolicy.Allows(role,OrganizationPermission.ManageIncidentLifecycle));
        Assert.Equal(technical,OrganizationPolicy.Allows(role,OrganizationPermission.AssignIncident));
        Assert.Equal(technical,OrganizationPolicy.Allows(role,OrganizationPermission.ConfirmIncidentClosure));
    }

    [Fact]
    public void AuditorMustReceiveSeparateActiveGrantAtRuntime()
    {
        Assert.False(OrganizationPolicy.Allows(OrganizationRole.Auditor,
            OrganizationPermission.ViewIncidentDetail));
        Assert.True(OrganizationPolicy.Allows(OrganizationRole.Auditor,
            OrganizationPermission.ViewAuditEvents));
        Assert.False(OrganizationPolicy.Allows(OrganizationRole.Support,
            OrganizationPermission.ViewAuditEvents));
        Assert.False(OrganizationPolicy.Allows(OrganizationRole.Administrator,
            OrganizationPermission.ViewAuditEvents));
    }

    [Fact]
    public void ManagerCanReportImpactButCannotChangeTechnicalIncident()
    {
        Assert.True(OrganizationPolicy.Allows(OrganizationRole.Manager,
            OrganizationPermission.ReportIncidentImpact));
        Assert.False(OrganizationPolicy.Allows(OrganizationRole.Manager,
            OrganizationPermission.AttachIncidentEvidence));
        Assert.False(OrganizationPolicy.Allows(OrganizationRole.Manager,
            OrganizationPermission.AppendIncidentNote));
        Assert.False(OrganizationPolicy.Allows(OrganizationRole.Manager,
            OrganizationPermission.ManageIncidentActions));
    }

    [Fact]
    public void ClosureAndRecoveryRequireDistinctConditions()
    {
        Assert.False(IncidentPolicy.CanTransition(IncidentState.Monitoring,IncidentState.Resolved,
            true,true,false,true));
        Assert.False(IncidentPolicy.CanTransition(IncidentState.Resolved,IncidentState.Closed,
            true,true,false,true));
        Assert.False(IncidentPolicy.CanTransition(IncidentState.Resolved,IncidentState.Closed,
            true,true,true,false));
        Assert.True(IncidentPolicy.CanTransition(IncidentState.Resolved,IncidentState.Closed,
            true,true,true,true));
        Assert.True(IncidentPolicy.IndependentClosureRequired(IncidentSeverity.SEV2));
        Assert.False(IncidentPolicy.IndependentClosureRequired(IncidentSeverity.SEV3));
    }

    [Fact]
    public void ServiceCodesAndReasonsUseFiniteValidatedFormats()
    {
        foreach (var code in new[]{"api","database","notifications","tracking_held"})
            Assert.Equal(code,IncidentPolicy.ServiceCode(code));
        foreach (var code in new[]{"custom","API","../api","customer-123",""})
            Assert.Throws<ArgumentException>(()=>IncidentPolicy.ServiceCode(code));
        Assert.Throws<ArgumentException>(()=>IncidentPolicy.Reason("Other Tenant"));
        Assert.Equal("unknown_cause",IncidentPolicy.Reason("unknown_cause"));
    }
}
