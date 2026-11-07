using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HarborOps.Domain.Exceptions;

public sealed record ShipmentExceptionInput(
    Guid OrganizationId,
    Guid ShipmentId,
    string RecordStatus,
    string WorkflowStatus,
    long ShipmentVersion,
    DateTimeOffset? PlannedDepartureAtUtc,
    DateTimeOffset? PlannedArrivalAtUtc,
    DateTimeOffset? RecordedDepartureAtUtc,
    DateTimeOffset? RecordedArrivalAtUtc,
    string ObservedTrackingState,
    bool TrackingMaterializationStale,
    long TrackingInputRevision,
    long CorrectionGeneration,
    string TrackingPolicyVersion,
    string TrackingInputDigest,
    DateTimeOffset? LastObservedAtUtc);

public sealed record ShipmentExceptionSignal(
    string RuleCode,
    string Condition,
    string Severity,
    string Confidence,
    string ReasonCode,
    DateTimeOffset? ReferenceAtUtc,
    DateTimeOffset? ThresholdAtUtc,
    string SourceFingerprint,
    string RulePolicyVersion,
    DateTimeOffset? NextEvaluationAtUtc);

public static class ShipmentExceptionPolicy
{
    public const string Version = "m11-rules-v1";
    public const string EquipmentConflict = "EquipmentConflict";
    public const string ArrivalScheduleOverdue = "ArrivalScheduleOverdue";
    public const string RecordedArrivalLate = "RecordedArrivalLate";
    public const string DepartureSlippageRisk = "DepartureSlippageRisk";

    // Local demonstrator defaults are provisional until the M11 policy decision is accepted.
    public static readonly TimeSpan ArrivalGrace = TimeSpan.FromMinutes(60);
    public static readonly TimeSpan DepartureTolerance = TimeSpan.FromMinutes(60);

    public static IReadOnlyList<ShipmentExceptionSignal> Evaluate(
        ShipmentExceptionInput input, DateTimeOffset evaluationAtUtc)
    {
        if (input.OrganizationId == Guid.Empty || input.ShipmentId == Guid.Empty ||
            input.ShipmentVersion <= 0 || input.TrackingInputRevision < 0 ||
            input.CorrectionGeneration < 0)
            throw new ArgumentException("Invalid exception input");
        if (evaluationAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Evaluation clock must be UTC");

        var eligible = input.RecordStatus == "Active" &&
            input.WorkflowStatus is not ("Cancelled" or "Draft");
        var results = new List<ShipmentExceptionSignal>(4);

        ShipmentExceptionSignal Build(string rule, string condition,
            string severity, string reason, DateTimeOffset? evidence,
            DateTimeOffset? boundary, DateTimeOffset? nextDue, string confidence)
        {
            var canonical = string.Join("|",
                Version, input.OrganizationId.ToString("N"),
                input.ShipmentId.ToString("N"), rule,
                input.ShipmentVersion.ToString(CultureInfo.InvariantCulture),
                input.RecordStatus, input.WorkflowStatus,
                input.PlannedDepartureAtUtc?.UtcTicks.ToString(CultureInfo.InvariantCulture) ?? "",
                input.PlannedArrivalAtUtc?.UtcTicks.ToString(CultureInfo.InvariantCulture) ?? "",
                input.RecordedDepartureAtUtc?.UtcTicks.ToString(CultureInfo.InvariantCulture) ?? "",
                input.RecordedArrivalAtUtc?.UtcTicks.ToString(CultureInfo.InvariantCulture) ?? "",
                input.ObservedTrackingState, input.TrackingMaterializationStale ? "1" : "0",
                input.TrackingInputRevision.ToString(CultureInfo.InvariantCulture),
                input.CorrectionGeneration.ToString(CultureInfo.InvariantCulture),
                input.TrackingPolicyVersion, input.TrackingInputDigest,
                input.LastObservedAtUtc?.UtcTicks.ToString(CultureInfo.InvariantCulture) ?? "",
                condition, reason, confidence,
                boundary?.UtcTicks.ToString(CultureInfo.InvariantCulture) ?? "");
            return new ShipmentExceptionSignal(rule, condition, severity, confidence,
                reason, evidence, boundary,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
                    .ToLowerInvariant(),
                Version, nextDue);
        }

        if (!eligible)
        {
            foreach (var rule in new[] { EquipmentConflict, ArrivalScheduleOverdue,
                         RecordedArrivalLate, DepartureSlippageRisk })
                results.Add(Build(rule, "Inactive", "Info", "shipment_not_eligible",
                    null, null, null, "NotApplicable"));
            return results;
        }

        results.Add(input.TrackingMaterializationStale
            ? Build(EquipmentConflict, "Stale", "Warning", "tracking_materialization_stale",
                null, null, null, "Unverified")
            : input.ObservedTrackingState == "Conflict"
                ? Build(EquipmentConflict, "Active", "High", "equipment_observations_conflict",
                    input.LastObservedAtUtc, null, null, "RecordedEquipment")
                : input.ObservedTrackingState is "Unknown" or ""
                    ? Build(EquipmentConflict, "Unknown", "Info", "tracking_evidence_missing",
                        null, null, null, "Unverified")
                    : Build(EquipmentConflict, "Inactive", "Info", "no_equipment_conflict",
                        input.LastObservedAtUtc, null, null, "RecordedEquipment"));

        if (input.PlannedArrivalAtUtc is null)
        {
            results.Add(Build(ArrivalScheduleOverdue, "Unknown", "Info",
                "arrival_schedule_missing", null, null, null, "Unverified"));
            results.Add(Build(RecordedArrivalLate, "Unknown", "Info",
                "arrival_schedule_missing", null, null, null, "Unverified"));
        }
        else
        {
            var arrivalBoundary = input.PlannedArrivalAtUtc.Value + ArrivalGrace;
            if (input.RecordedArrivalAtUtc.HasValue)
            {
                results.Add(Build(ArrivalScheduleOverdue, "Inactive", "Info",
                    "recorded_arrival_exists", input.RecordedArrivalAtUtc,
                    arrivalBoundary, null, "RecordedWorkflow"));
                results.Add(Build(RecordedArrivalLate,
                    input.RecordedArrivalAtUtc > arrivalBoundary ? "Active" : "Inactive",
                    "High", input.RecordedArrivalAtUtc > arrivalBoundary
                        ? "recorded_arrival_after_grace" : "recorded_arrival_within_grace",
                    input.RecordedArrivalAtUtc, arrivalBoundary, null, "RecordedWorkflow"));
            }
            else
            {
                var condition = evaluationAtUtc > arrivalBoundary ? "Active" : "Inactive";
                var nextDue = condition == "Active" ? null : arrivalBoundary.AddTicks(1);
                results.Add(Build(ArrivalScheduleOverdue, condition, "Warning",
                    condition == "Active" ? "scheduled_arrival_overdue_unconfirmed"
                        : "scheduled_arrival_not_yet_overdue",
                    null, arrivalBoundary, nextDue, "ScheduleOnly"));
                results.Add(Build(RecordedArrivalLate, "Unknown", "Info",
                    "recorded_arrival_missing", null, arrivalBoundary, null, "Unverified"));
            }
        }

        if (!input.PlannedDepartureAtUtc.HasValue ||
            !input.PlannedArrivalAtUtc.HasValue ||
            !input.RecordedDepartureAtUtc.HasValue)
        {
            results.Add(Build(DepartureSlippageRisk, "Unknown", "Info",
                "departure_evidence_or_schedule_missing", input.RecordedDepartureAtUtc,
                null, null, "Unverified"));
        }
        else
        {
            var deadline = input.PlannedDepartureAtUtc.Value + DepartureTolerance;
            var late = input.RecordedDepartureAtUtc > deadline &&
                       evaluationAtUtc < input.PlannedArrivalAtUtc.Value &&
                       !input.RecordedArrivalAtUtc.HasValue;
            results.Add(Build(DepartureSlippageRisk, late ? "Active" : "Inactive",
                "Warning", late ? "recorded_departure_after_tolerance"
                    : "no_current_departure_slippage_risk",
                input.RecordedDepartureAtUtc, deadline,
                late ? input.PlannedArrivalAtUtc : null, "RecordedWorkflow"));
        }
        return results;
    }
}
