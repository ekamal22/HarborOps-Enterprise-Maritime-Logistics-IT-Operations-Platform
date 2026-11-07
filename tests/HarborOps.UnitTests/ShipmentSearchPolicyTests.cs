using HarborOps.Domain.Organizations;
using HarborOps.Domain.Shipments;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class ShipmentSearchPolicyTests
{
    private static ShipmentSearchFilters Make(string? q = null, string? status = null,
        string? workflow = null, DateTimeOffset? from = null,
        DateTimeOffset? before = null, int limit = 25) =>
        ShipmentSearchFilters.Create(q, status, workflow, null, null, null, from,
            before, null, null, limit);

    [Fact]
    public void SearchNormalizesTextAndDefaultStatus()
    {
        var filters = Make("  SHP-A  ");
        Assert.Equal("SHP-A", filters.Q);
        Assert.Equal("Active", filters.RecordStatus);
        Assert.Null(filters.WorkflowStatus);
        Assert.Equal(25, filters.Limit);
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("Archived")]
    [InlineData("All")]
    public void ValidRecordStatusIsPreserved(string status) =>
        Assert.Equal(status, Make(status: status).RecordStatus);

    [Theory]
    [InlineData("Draft")]
    [InlineData("Booked")]
    [InlineData("InTransit")]
    [InlineData("Arrived")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public void ValidWorkflowIsPreserved(string status) =>
        Assert.Equal(status, Make(workflow: status).WorkflowStatus);

    [Theory]
    [InlineData("active")]
    [InlineData("Unknown")]
    public void InvalidRecordStatusIsRejected(string status) =>
        Assert.Throws<ArgumentException>(() => Make(status: status));

    [Theory]
    [InlineData("booked")]
    [InlineData("In Transit")]
    public void InvalidWorkflowIsRejected(string status) =>
        Assert.Throws<ArgumentException>(() => Make(workflow: status));

    [Fact]
    public void InvalidLimitsAndControlCharactersAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Make(limit: 0));
        Assert.Throws<ArgumentException>(() => Make(limit: 101));
        Assert.Throws<ArgumentException>(() => Make(q: "one\nmore"));
        Assert.Throws<ArgumentException>(() => Make(q: new string('x', 81)));
    }

    [Fact]
    public void UtcWindowsAreHalfOpenAndBounded()
    {
        var from = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var before = from.AddDays(366);
        Assert.Equal(before, Make(from: from, before: before).PlannedDepartureBeforeUtc);
        Assert.Throws<ArgumentException>(() => Make(from: from));
        Assert.Throws<ArgumentException>(() => Make(before: before));
        Assert.Throws<ArgumentException>(() => Make(from: from, before: from));
        Assert.Throws<ArgumentException>(() => Make(from: before, before: from));
        Assert.Throws<ArgumentException>(() => Make(from: from, before: before.AddTicks(1)));
        Assert.Throws<ArgumentException>(() => Make(from: from.ToOffset(TimeSpan.FromHours(3)),
            before: before));
    }

    [Fact]
    public void FilterFingerprintIsStableAndChangesWithScope()
    {
        Assert.Equal(Make(q: "ABC").Fingerprint(), Make(q: " ABC ").Fingerprint());
        Assert.NotEqual(Make(q: "ABC").Fingerprint(), Make(q: "ABD").Fingerprint());
        Assert.NotEqual(Make(status: "Active").Fingerprint(), Make(status: "All").Fingerprint());
        Assert.Matches("^[0-9A-F]{64}$", Make().Fingerprint());
    }

    [Theory]
    [InlineData("=2+2", "\"'=2+2\"")]
    [InlineData("+SUM(1,2)", "\"'+SUM(1,2)\"")]
    [InlineData("-3", "\"'-3\"")]
    [InlineData("@action", "\"'@action\"")]
    [InlineData(" \t=cmd", "\"' \t=cmd\"")]
    [InlineData("plain", "\"plain\"")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("a\"b", "\"a\"\"b\"")]
    public void CsvEscapesAndDefangsFormulas(string input, string expected) =>
        Assert.Equal(expected, ShipmentCsv.Cell(input));

    [Fact]
    public void CsvHasFixedColumnsAndBoundedOutput()
    {
        Assert.Equal(12, ShipmentCsv.Headers.Length);
        Assert.Equal(5000, ShipmentCsv.MaximumRows);
        Assert.Equal(10 * 1024 * 1024, ShipmentCsv.MaximumBytes);
        Assert.Equal("\"\"", ShipmentCsv.Cell(null));
    }

    [Fact]
    public void ExportPermissionIsNotImpliedByShipmentRead()
    {
        foreach (var role in Enum.GetValues<OrganizationRole>())
        {
            var expected = role is OrganizationRole.Analyst or OrganizationRole.Manager;
            Assert.Equal(expected, OrganizationPolicy.Allows(role,
                OrganizationPermission.ExportShipmentData));
        }
        Assert.True(OrganizationPolicy.Allows(OrganizationRole.Auditor,
            OrganizationPermission.ViewShipmentData));
        Assert.False(OrganizationPolicy.Allows(OrganizationRole.Auditor,
            OrganizationPermission.ExportShipmentData));
        Assert.False(OrganizationPolicy.Allows(OrganizationRole.Administrator,
            OrganizationPermission.ExportShipmentData));
    }
}
