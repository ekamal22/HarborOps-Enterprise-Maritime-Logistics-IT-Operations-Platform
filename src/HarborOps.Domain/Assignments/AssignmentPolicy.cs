using System.Globalization;

namespace HarborOps.Domain.Assignments;

public static class AssignmentPolicy
{
    public static DateTimeOffset Utc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 40 ||
            !(value.EndsWith('Z') || value.EndsWith("+00:00", StringComparison.Ordinal) ||
              value.EndsWith("-00:00", StringComparison.Ordinal)) ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var instant) || instant.Offset != TimeSpan.Zero)
            throw new ArgumentException("An explicit UTC timestamp is required");
        return instant;
    }

    public static void Window(DateTimeOffset start, DateTimeOffset? end)
    {
        if (start.Offset != TimeSpan.Zero || end.HasValue && end.Value.Offset != TimeSpan.Zero ||
            end.HasValue && end.Value <= start)
            throw new ArgumentException("Invalid half-open UTC assignment interval");
    }

    public static bool Overlaps(DateTimeOffset firstStart, DateTimeOffset? firstEnd,
        DateTimeOffset secondStart, DateTimeOffset? secondEnd) =>
        (!firstEnd.HasValue || firstEnd.Value > secondStart) &&
        (!secondEnd.HasValue || secondEnd.Value > firstStart);

    public static bool VoyageCompatible(Guid shipmentOrigin, Guid shipmentDestination,
        DateTimeOffset? shipmentDeparture, DateTimeOffset? shipmentArrival,
        Guid voyageOrigin, Guid voyageDestination,
        DateTimeOffset? voyageDeparture, DateTimeOffset? voyageArrival)
    {
        if (shipmentOrigin != voyageOrigin || shipmentDestination != voyageDestination)
            return false;
        return !(shipmentDeparture.HasValue && shipmentArrival.HasValue &&
            voyageDeparture.HasValue && voyageArrival.HasValue &&
            !Overlaps(shipmentDeparture.Value, shipmentArrival,
                voyageDeparture.Value, voyageArrival));
    }
}
