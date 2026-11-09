using HarborOps.Infrastructure.Notifications;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class NotificationTextTests
{
    [Theory]
    [InlineData("EquipmentConflict", "Conflicting recorded equipment observations")]
    [InlineData("ArrivalScheduleOverdue", "Planned arrival overdue; physical arrival unconfirmed")]
    [InlineData("RecordedArrivalLate", "Late arrival recorded in the manual workflow")]
    [InlineData("DepartureSlippageRisk", "Recorded workflow departure suggests a schedule risk")]
    public void RuleTemplatesPreserveAcceptedM11EvidenceBoundary(string rule, string expected)
    {
        Assert.Equal(expected, NotificationText.Title(rule));
        Assert.DoesNotContain("ETA", NotificationText.Title(rule));
        Assert.DoesNotContain("vessel arrived", NotificationText.Title(rule),
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ExceptionOpened","Opened")]
    [InlineData("ExceptionResolved","Reviewed and resolved")]
    [InlineData("ExceptionAutoResolved","Closed by evaluation")]
    public void EventLabelsDescribeHistoryRatherThanMovement(string type, string expected)
    {
        Assert.Equal(expected, NotificationText.Label(type));
    }

    [Fact]
    public void UnknownCodesHaveNeutralText()
    {
        Assert.Equal("Shipment exception",NotificationText.Title("Unknown"));
        Assert.Equal("Updated",NotificationText.Label("Unknown"));
    }
}
