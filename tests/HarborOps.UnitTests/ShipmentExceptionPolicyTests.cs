using HarborOps.Domain.Exceptions;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class ShipmentExceptionPolicyTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static ShipmentExceptionInput Input(
        DateTimeOffset? departure = null, DateTimeOffset? arrival = null,
        DateTimeOffset? recordedDeparture = null,
        DateTimeOffset? recordedArrival = null,
        string tracking = "Unknown", bool stale = false,
        string workflow = "Booked", string status = "Active") =>
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            status, workflow, 3, departure, arrival, recordedDeparture, recordedArrival,
            tracking, stale, 2, 1, "m10-equipment-v2",
            new string('a', 64), T0);

    private static ShipmentExceptionSignal Rule(
        ShipmentExceptionInput input, string code, DateTimeOffset? at = null) =>
        ShipmentExceptionPolicy.Evaluate(input, at ?? T0).Single(x => x.RuleCode == code);

    [Fact]
    public void ArrivalScheduleBoundaryIsStrictAndDoesNotClaimPhysicalArrival()
    {
        var input = Input(arrival: T0.AddMinutes(-60));
        Assert.Equal("Inactive", Rule(input,
            ShipmentExceptionPolicy.ArrivalScheduleOverdue).Condition);
        var active = Rule(input, ShipmentExceptionPolicy.ArrivalScheduleOverdue,
            T0.AddTicks(1));
        Assert.Equal("Active", active.Condition);
        Assert.Equal("ScheduleOnly", active.Confidence);
        Assert.Equal("scheduled_arrival_overdue_unconfirmed", active.ReasonCode);
        Assert.Equal(active.SourceFingerprint, Rule(input,
            ShipmentExceptionPolicy.ArrivalScheduleOverdue, T0.AddDays(1)).SourceFingerprint);
    }

    [Fact]
    public void ExplicitWorkflowArrivalIsDistinguishedFromUnconfirmedSchedule()
    {
        var late = Input(arrival: T0.AddHours(-4),
            recordedArrival: T0.AddHours(-2), workflow: "Arrived");
        Assert.Equal("Active", Rule(late, ShipmentExceptionPolicy.RecordedArrivalLate).Condition);
        Assert.Equal("Inactive", Rule(late,
            ShipmentExceptionPolicy.ArrivalScheduleOverdue).Condition);
        Assert.Equal("RecordedWorkflow", Rule(late,
            ShipmentExceptionPolicy.RecordedArrivalLate).Confidence);
    }

    [Fact]
    public void CompletedWithoutArrivedHistoryClearsOverdueButNotLateArrival()
    {
        var input = Input(arrival: T0.AddHours(-4), workflow: "Completed");
        Assert.Equal("Inactive", Rule(input,
            ShipmentExceptionPolicy.ArrivalScheduleOverdue).Condition);
        Assert.Equal("recorded_completion_exists", Rule(input,
            ShipmentExceptionPolicy.ArrivalScheduleOverdue).ReasonCode);
        Assert.Equal("Unknown", Rule(input,
            ShipmentExceptionPolicy.RecordedArrivalLate).Condition);
    }

    [Fact]
    public void DepartureRiskNeedsRecordedWorkflowAndFutureArrival()
    {
        var input = Input(departure: T0.AddHours(-3),
            arrival: T0.AddHours(1), recordedDeparture: T0.AddHours(-1),
            workflow: "InTransit");
        Assert.Equal("Active", Rule(input,
            ShipmentExceptionPolicy.DepartureSlippageRisk).Condition);
        Assert.Equal("Inactive", Rule(input,
            ShipmentExceptionPolicy.DepartureSlippageRisk, T0.AddHours(2)).Condition);
        Assert.Equal("Unknown", Rule(input with { RecordedDepartureAtUtc = null },
            ShipmentExceptionPolicy.DepartureSlippageRisk).Condition);
    }

    [Fact]
    public void StaleProjectionIsNeverUsedAsConfirmedConflict()
    {
        Assert.Equal("Stale", Rule(Input(tracking: "Conflict", stale: true),
            ShipmentExceptionPolicy.EquipmentConflict).Condition);
        Assert.Equal("Active", Rule(Input(tracking: "Conflict"),
            ShipmentExceptionPolicy.EquipmentConflict).Condition);
        Assert.Equal("Unknown", Rule(Input(),
            ShipmentExceptionPolicy.EquipmentConflict).Condition);
    }

    [Fact]
    public void CancelledArchivedOrDraftShipmentsDoNotProduceNewConditions()
    {
        foreach (var input in new[]
        {
            Input(arrival: T0.AddDays(-1), workflow: "Cancelled"),
            Input(arrival: T0.AddDays(-1), status: "Archived"),
            Input(arrival: T0.AddDays(-1), workflow: "Draft")
        })
            Assert.All(ShipmentExceptionPolicy.Evaluate(input, T0),
                x => Assert.Equal("Inactive", x.Condition));
    }

    [Fact]
    public void DifferentInputVersionsChangeFingerprintAndSameInputIsStable()
    {
        var input = Input(arrival: T0.AddMinutes(-61));
        var first = Rule(input, ShipmentExceptionPolicy.ArrivalScheduleOverdue);
        Assert.Equal(first, Rule(input, ShipmentExceptionPolicy.ArrivalScheduleOverdue));
        Assert.NotEqual(first.SourceFingerprint, Rule(input with { ShipmentVersion = 4 },
            ShipmentExceptionPolicy.ArrivalScheduleOverdue).SourceFingerprint);
        Assert.NotEqual(first.SourceFingerprint, Rule(input with { CorrectionGeneration = 2 },
            ShipmentExceptionPolicy.ArrivalScheduleOverdue).SourceFingerprint);
    }

    [Fact]
    public void NonUtcClockIsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            ShipmentExceptionPolicy.Evaluate(Input(), T0.ToOffset(TimeSpan.FromHours(3))));
    }
}
