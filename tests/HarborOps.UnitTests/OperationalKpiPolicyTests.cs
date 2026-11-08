using HarborOps.Domain.Organizations;
using HarborOps.Domain.Reporting;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class OperationalKpiPolicyTests
{
    [Fact]
    public void CatalogHasFourteenDistinctVersionedMetricsAndClearDenominators()
    {
        Assert.Equal("m13-kpi-v1", OperationalKpiPolicy.Version);
        Assert.Equal(14, OperationalKpiPolicy.Definitions.Count);
        Assert.Equal(14, OperationalKpiPolicy.Definitions.Select(x => x.Code).Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 14).Select(x => $"K{x:00}"),
            OperationalKpiPolicy.Definitions.Select(x => x.Code));
        Assert.All(OperationalKpiPolicy.Definitions,
            x => Assert.False(string.IsNullOrWhiteSpace(x.Description)));
        Assert.Equal("D05",
            OperationalKpiPolicy.Definitions.Single(x => x.Code == "K05").DenominatorCode);
        Assert.Equal("D05",
            OperationalKpiPolicy.Definitions.Single(x => x.Code == "K06").DenominatorCode);
        Assert.Equal("D13",
            OperationalKpiPolicy.Definitions.Single(x => x.Code == "K13").DenominatorCode);
        Assert.Equal("D10",
            OperationalKpiPolicy.Definitions.Single(x => x.Code == "K10").DenominatorCode);
        Assert.Equal("D13",
            OperationalKpiPolicy.Definitions.Single(x => x.Code == "K14").DenominatorCode);
    }

    [Theory]
    [InlineData("2026-11-09T00:00:00Z", true)]
    [InlineData("2026-11-09T00:00:00+00:00", true)]
    [InlineData("2026-11-09T00:00:00.000001Z", true)]
    [InlineData("2026-11-09T00:00:00.1234567Z", false)]
    [InlineData("2026-11-09", false)]
    [InlineData("2026-11-09T00:00:00", false)]
    [InlineData("2026-11-09T03:00:00+03:00", false)]
    [InlineData("", false)]
    public void RequiresExplicitRepresentableUtcTimestamp(string input, bool accepted)
    {
        Assert.Equal(accepted, OperationalKpiPolicy.TryUtc(input, out var result));
        if (accepted) Assert.Equal(TimeSpan.Zero, result.Offset);
    }

    [Fact]
    public void WindowsAreStrictHalfOpenAndBounded()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Assert.True(OperationalKpiPolicy.ValidWindow(start, start.AddDays(366)));
        Assert.False(OperationalKpiPolicy.ValidWindow(start, start));
        Assert.False(OperationalKpiPolicy.ValidWindow(start.AddDays(1), start));
        Assert.False(OperationalKpiPolicy.ValidWindow(start, start.AddDays(366).AddTicks(1)));
        Assert.False(OperationalKpiPolicy.ValidWindow(start.ToOffset(TimeSpan.FromHours(3)),
            start.AddDays(7)));
    }

    [Fact]
    public void DefaultWindowContainsSevenCompleteUtcDays()
    {
        var now = new DateTimeOffset(2026, 11, 9, 19, 31, 0, TimeSpan.Zero);
        var window = OperationalKpiPolicy.DefaultWindow(now);
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero),
            window.FromUtc);
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 0, 0, 0, TimeSpan.Zero),
            window.BeforeUtc);
        Assert.Equal("day", window.Bucket);
        Assert.True(OperationalKpiPolicy.ValidWindow(window.FromUtc, window.BeforeUtc));
    }

    [Fact]
    public void DrillDimensionsAreStrictAndCannotExposeInternalDenominators()
    {
        foreach (var metric in OperationalKpiPolicy.Definitions)
            Assert.True(OperationalKpiPolicy.ValidMetric(metric.Code));
        Assert.False(OperationalKpiPolicy.ValidMetric("D05"));
        Assert.False(OperationalKpiPolicy.ValidMetric("Q04"));
        Assert.False(OperationalKpiPolicy.ValidMetric("K15"));
        Assert.True(OperationalKpiPolicy.ValidDimension("K01", ""));
        Assert.False(OperationalKpiPolicy.ValidDimension("K01", "Booked"));
        Assert.True(OperationalKpiPolicy.ValidDimension("K02", "Booked"));
        Assert.False(OperationalKpiPolicy.ValidDimension("K02", "Archived"));
        Assert.True(OperationalKpiPolicy.ValidDimension("K12", "EquipmentConflict"));
        Assert.False(OperationalKpiPolicy.ValidDimension("K12", "Unknown"));
    }

    [Fact]
    public void DedicatedPermissionDoesNotBroadenReadOrExportRights()
    {
        foreach (var role in Enum.GetValues<OrganizationRole>())
        {
            var basicView = role is OrganizationRole.Analyst or OrganizationRole.Manager;
            Assert.Equal(basicView,
                OrganizationPolicy.Allows(role, OrganizationPermission.ViewOperationalKpis));
            Assert.Equal(basicView,
                OrganizationPolicy.Allows(role, OrganizationPermission.ExportShipmentData));
        }
        Assert.True(OrganizationPolicy.Allows(OrganizationRole.Auditor,
            OrganizationPermission.ViewShipmentData));
        Assert.False(OrganizationPolicy.Allows(OrganizationRole.Auditor,
            OrganizationPermission.ViewOperationalKpis));
    }
}