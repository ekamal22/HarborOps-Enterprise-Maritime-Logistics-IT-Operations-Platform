#!/usr/bin/env python3
"""Source-locked disposable G13 mutation checks. Never writes to Git history."""
from __future__ import annotations
import hashlib
import json
import os
import re
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "src/HarborOps.Infrastructure/Reporting/OperationalKpiStore.cs"
ENDPOINT = ROOT / "src/HarborOps.Api/Reporting/OperationalKpiEndpoints.cs"
RETENTION_MIGRATION = ROOT / "src/HarborOps.Infrastructure/Persistence/Migrations/20261110010000_M13TenantReportCapacity.cs"
POLICY = ROOT / "src/HarborOps.Domain/Reporting/OperationalKpiPolicy.cs"
PROGRAM = ROOT / "src/HarborOps.Api/Program.cs"
BASE_MIGRATION = ROOT / "src/HarborOps.Infrastructure/Persistence/Migrations/20261109020000_M13OperationalKpis.cs"
EVIDENCE = ROOT / ".verification/g13-mutations"
FILTER = "FullyQualifiedName~M13OperationalKpiRuntimeTests.TwelveShipmentsAndTwoForeignDecoysReconcileExactMembers"
ORACLE = "FullyQualifiedName~M13OperationalKpiRuntimeTests.ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys"
MUTANTS = [
    ("scoped-evaluations-authority-removed",
     "AS $evals$\n            BEGIN\n                IF session_user<>'harborops_api' OR NOT m13_can_view(p_org) THEN",
     "AS $evals$\n            BEGIN\n                IF session_user<>'harborops_api' THEN",
     "ScopedReadersAndQuotaFunctionsRequireSignedReportingAuthority",
     "retention_migration"),
    ("live-report-count-authority-removed",
     "AS $live$\n            DECLARE live_reports integer;\n            BEGIN\n                IF session_user<>'harborops_api' OR NOT m13_can_view(p_org) THEN",
     "AS $live$\n            DECLARE live_reports integer;\n            BEGIN\n                IF session_user<>'harborops_api' THEN",
     "TenantLiveReportCountRequiresAuthorityAndExcludesOtherTenantsAndExpiredRows",
     "retention_migration"),
    ("live-report-count-tenant-filter-removed",
     "WHERE r.organization_id=p_org AND r.id<>p_exclude\n                  AND r.expires_at_utc>clock_timestamp();",
     "WHERE r.id<>p_exclude\n                  AND r.expires_at_utc>clock_timestamp();",
     "TenantLiveReportCountRequiresAuthorityAndExcludesOtherTenantsAndExpiredRows",
     "retention_migration"),
    ("live-report-count-expiry-filter-removed",
     "WHERE r.organization_id=p_org AND r.id<>p_exclude\n                  AND r.expires_at_utc>clock_timestamp();",
     "WHERE r.organization_id=p_org AND r.id<>p_exclude;",
     "TenantLiveReportCountRequiresAuthorityAndExcludesOtherTenantsAndExpiredRows",
     "retention_migration"),
    ("k01-include-completed",
     "WHERE s.record_status='Active' AND s.workflow_status NOT IN ('Completed','Cancelled')",
     "WHERE s.record_status='Active' AND s.workflow_status <> 'Cancelled'"),
    ("k05-count-all-arrivals",
     "WHERE c.arrived_at <= c.planned_arrival_at_utc\n",
     "WHERE c.arrived_at IS NOT NULL\n"),
    ("unknown-evaluation-suppressed",
     "WHERE q.fresh_rules<4 OR q.unknown_rules>0 OR q.stale_rules>0",
     "WHERE q.fresh_rules<4 OR q.unknown_rules<0 OR q.stale_rules>0",
     "UnknownEvaluationDoesNotBecomeZeroRisk"),
    ("k06-exact-sixty-minute-boundary",
     "WHERE c.arrived_at <= c.planned_arrival_at_utc + interval '60 minutes'",
     "WHERE c.arrived_at < c.planned_arrival_at_utc + interval '60 minutes'",
     "MaterializedK06IncludesExactlySixtyMinutesButNotOneMicrosecondLater"),
    ("k06-remove-tolerance",
     "WHERE c.arrived_at <= c.planned_arrival_at_utc + interval '60 minutes'",
     "WHERE c.arrived_at <= c.planned_arrival_at_utc + interval '0 minutes'"),
    ("k04-drop-cancellations",
     "JOIN events e ON e.shipment_id=s.id AND e.to_status='Cancelled'",
     "JOIN events e ON e.shipment_id=s.id AND e.to_status='Booked'"),
    ("k02-false-status",
     "SELECT 'K02',s.workflow_status,s.id,s.id,NULL,NULL FROM scoped s",
     "SELECT 'K02','Draft',s.id,s.id,NULL,NULL FROM scoped s"),
    ("coverage-force-complete",
     "WHERE q.fresh_rules=4",
     "WHERE q.fresh_rules>=0"),
    ("quality-include-draft",
     "FROM heads h LEFT JOIN evaluated e ON e.id=h.id\n            WHERE h.record_status='Active' AND h.workflow_status NOT IN ('Draft','Cancelled')",
     "FROM heads h LEFT JOIN evaluated e ON e.id=h.id\n            WHERE h.record_status='Active'"),
    ("unassessed-suppress-missing",
     "WHERE q.fresh_rules<4 OR q.unknown_rules>0",
     "WHERE q.fresh_rules>10 OR q.unknown_rules>10"),
    ("report-cap-wrong-comparison",
     "if (count > OperationalKpiPolicy.MaximumReportMembers)",
     "if (count < OperationalKpiPolicy.MaximumReportMembers)"),
    ("k07-recorded-risk-dropped",
     "AND e.rule_code IN ('RecordedArrivalLate','ArrivalScheduleOverdue','DepartureSlippageRisk')\n                    AND e.condition_state='Active'",
     "AND e.rule_code IN ('ArrivalScheduleOverdue','DepartureSlippageRisk')\n                    AND e.condition_state='Active'",
     "ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys"),
    ("k10-fresh-equipment-conflict-hidden",
     "AND h.observed_state='Conflict'",
     "AND h.observed_state='Unknown'",
     "ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys"),
    ("k11-include-resolved-occurrence",
     "AND e.lifecycle_state IN ('Open','Acknowledged')",
     "AND e.lifecycle_state IN ('Open','Acknowledged','Resolved')",
     "ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys"),
    ("k12-suppress-first-openings",
     "WHERE e.organization_id=@org AND e.first_detected_at_utc>=@from",
     "WHERE e.organization_id=@org AND e.first_detected_at_utc>=@before",
     "ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys"),
    ("k03-counts-arrival-events-as-completions",
     "SELECT 'K03','',s.id,s.id,NULL,NULL FROM scoped s\n"
     "                JOIN events e ON e.shipment_id=s.id AND e.to_status='Completed'",
     "SELECT 'K03','',s.id,s.id,NULL,NULL FROM scoped s\n"
     "                JOIN events e ON e.shipment_id=s.id AND e.to_status='Arrived'",
     "ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys"),
    ("correction-generation-stale-accepted",
     "AND e.correction_generation=h.generation",
     "AND e.correction_generation IS NOT NULL",
     "ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys"),
    ("origin-member-filter-ignored",
     "AND (@origin::uuid IS NULL OR s.origin_port_id=@origin)",
     "AND (@origin::uuid IS NULL OR TRUE)",
     "OriginDestinationSourceAndVoyageFiltersAgreeWithMaterializedMembers"),
    ("tenant-quota-rejection-rollback-bypassed",
     """if (await quota.ExecuteScalarAsync(ct) is not true)
                return new(null, OrganizationAccessError.Conflict,
                    "report_tenant_capacity_exceeded");""",
     """if (await quota.ExecuteScalarAsync(ct) is not true)
            {
                await tx.CommitAsync(ct);
                return new(null, OrganizationAccessError.Conflict,
                    "report_tenant_capacity_exceeded");
            }""",
     "LateQuotaDecisionRefusesAndRollsBackWhenCapacityWasTakenAfterTheEarlyCheck"),
    ("auditor-grant-version-revocation-bypassed",
     "AND m.version=g.membership_version",
     "AND true",
     "AuditorGrantRevocationDeniesDashboardAndOldReportMembers",
     "base_migration"),
    ("tenant-quota-session-scope-bypassed",
     "DECLARE live_reports bigint; live_members bigint;\n            BEGIN\n                IF session_user<>'harborops_api' OR NOT m13_can_view(p_org) THEN",
     "DECLARE live_reports bigint; live_members bigint;\n            BEGIN\n                IF session_user<>'harborops_api' THEN",
     "FourLiveSnapshotsAreAdmittedFifthDeniedAndExpiredPruningRestoresCapacity",
     "retention_migration"),
    ("tenant-quota-active-report-cap-bypassed",
     "IF live_reports>4 THEN RETURN false; END IF;",
     "IF live_reports>400 THEN RETURN false; END IF;",
     "LateQuotaDecisionRefusesAndRollsBackWhenCapacityWasTakenAfterTheEarlyCheck",
     "retention_migration"),
    ("user-report-cap-bypassed",
     "RETURN live_user<=2;",
     "RETURN live_user<=200;",
     "OneUserIsLimitedToTwoActiveReportsAcrossSessionsInsideTheTenantLimit",
     "retention_migration"),
    ("idle-cleanup-limit-guard-widened",
     "p_limit NOT BETWEEN 1 AND 16",
     "p_limit NOT BETWEEN 1 AND 1000",
     "IdleCleanupRemovesAtMostSixteenExpiredSnapshotsPerCallAndRejectsInvalidAuthority",
     "retention_migration"),
    ("scoped-exceptions-authority-removed",
     "AS $exc$\n            BEGIN\n                IF session_user<>'harborops_api' OR NOT m13_can_view(p_org) THEN",
     "AS $exc$\n            BEGIN\n                IF session_user<>'harborops_api' THEN",
     "ScopedReadersAndQuotaFunctionsRequireSignedReportingAuthority",
     "retention_migration"),
    ("member-materialization-planner-guard-removed",
     "SET LOCAL enable_nestloop = off; SET LOCAL statement_timeout =",
     "SET LOCAL enable_nestloop = on; SET LOCAL statement_timeout =",
     "RealisticSkewedFortyThousandShipmentTenantCompletesFourSimultaneousBuilds"),
    ("member-materialization-timeout-reverted-to-15s",
     "MemberMaterializationTimeout = TimeSpan.FromSeconds(25);",
     "MemberMaterializationTimeout = TimeSpan.FromSeconds(15);",
     "BlockedMemberMaterializationRollsBackAndRecovers",
     "policy"),
    ("scoped-evaluations-organization-filter-removed",
     "RETURN QUERY SELECT e.* FROM shipment_exception_evaluations e\n                    WHERE e.organization_id=p_org;",
     "RETURN QUERY SELECT e.* FROM shipment_exception_evaluations e\n                    WHERE e.organization_id IS NOT NULL;",
     "DirectPrivilegedReaderCallsReturnOnlyTheSelectedTenantsRows",
     "retention_migration"),
    ("scoped-exceptions-organization-filter-removed",
     "RETURN QUERY SELECT e.* FROM shipment_exceptions e\n                    WHERE e.organization_id=p_org;",
     "RETURN QUERY SELECT e.* FROM shipment_exceptions e\n                    WHERE e.organization_id IS NOT NULL;",
     "DirectPrivilegedReaderCallsReturnOnlyTheSelectedTenantsRows",
     "retention_migration"),
    ("member-sql-shipment-tenant-predicate-removed",
     "FROM shipments s\n            WHERE s.organization_id=@org\n                AND (@origin::uuid IS NULL",
     "FROM shipments s\n            WHERE s.organization_id IS NOT NULL\n                AND (@origin::uuid IS NULL"),
    ("k05-reads-last-arrival-instead-of-first",
     "ORDER BY h.shipment_id,h.to_status,h.changed_at_utc,h.id",
     "ORDER BY h.shipment_id,h.to_status,h.changed_at_utc DESC,h.id",
     "FirstRecordedEventWinsWhenArrivedAndCompletedAreRecordedAgainLater"),
    ("k03-first-event-deduplication-removed",
     "SELECT DISTINCT ON (h.shipment_id,h.to_status)\n                h.shipment_id",
     "SELECT DISTINCT ON (h.shipment_id,h.to_status,h.changed_at_utc)\n                h.shipment_id",
     "FirstRecordedEventWinsWhenArrivedAndCompletedAreRecordedAgainLater"),
    ("build-slot-admission-removed",
     "reservedSlot = await TryTakeBuildSlotAsync(admission, org, liveReports, ct);",
     "reservedSlot = null;",
     "BuildSlotsRefuseExcessBuildersPromptlyAndLiveReportsShrinkTheFreeSlots"),
    ("build-slot-capacity-ignores-live-reports",
     "var remaining = OperationalKpiPolicy.MaximumLiveReportsPerTenant - liveReports;",
     "var remaining = OperationalKpiPolicy.MaximumLiveReportsPerTenant;",
     "TwoStoredReportsAndTwoNonPositionalBuildSlotsRefuseAnotherBuildEarly"),
    ("tenant-quota-advisory-lock-removed",
     "\"SELECT pg_advisory_lock(hashtextextended(@scope,0))\",\n            admission) { CommandTimeout = lockSeconds })",
     "\"SELECT hashtextextended(@scope,0)\",\n            admission) { CommandTimeout = lockSeconds })",
     "QuotaDecisionWaitsForTheTenantLockAndCommitsNothingWhileWaiting"),
    ("planner-setting-made-session-wide",
     "SET LOCAL enable_nestloop = off; SET LOCAL statement_timeout =",
     "SET enable_nestloop = off; SET statement_timeout =",
     "PlannerAndTimeoutSettingsNeverOutliveABuildOnAConnectionThatIsNotReset"),
    ("d05-plan-predicate-removed",
     "WHERE c.arrived_at IS NOT NULL AND c.planned_arrival_at_utc IS NOT NULL",
     "WHERE c.arrived_at IS NOT NULL",
     "CompletedShipmentWithoutAProvenPlanIsCompletedButNeverInTheArrivalDenominator"),
    ("voyage-assignment-end-ignored",
     "(a.ends_at_utc IS NULL OR a.ends_at_utc>@now)",
     "(a.ends_at_utc IS NULL OR a.ends_at_utc>@now OR true)",
     "OriginDestinationSourceAndVoyageFiltersAgreeWithMaterializedMembers"),
    ("idle-cleanup-registered-by-default",
     "if (builder.Configuration.GetValue<bool>(\"M13:EnableIdleCleanup\") &&",
     "if (true &&",
     "DefaultHostRegistersNeitherBackgroundServiceAndExplicitSettingsRegisterEach",
     "program"),
    ("malformed-kpi-member-cursor-admitted",
     "if (!Id(last, out var parsed))",
     "if (!Id(last, out var parsed) && false)",
     "ForgedTokensUnsafeQueriesAndDirectSqlCannotDiscloseReports",
     "endpoints"),
]

def _source_identity() -> str:
    """Hosted runs use GITHUB_SHA. Local runs record the actual checked out HEAD."""
    value = os.environ.get("GITHUB_SHA")
    if value:
        return value
    return subprocess.run(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True,
                          stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                          check=True).stdout.strip()

def _postgres_filter() -> list[str]:
    """Optional exact container name for local runs with several disposable servers."""
    name = os.environ.get("HARBOROPS_EXCLUSIVE_PG_CONTAINER")
    base = ["--filter", "ancestor=postgres:16.15"]
    return base + (["--filter", "name=^/" + name + "$"] if name else [])

# Development rehearsal only: a comma separated subset of mutant names. A gate campaign
# must leave this unset; the evidence reconciler rejects a manifest with fewer variants.
_SUBSET = os.getenv("HARBOROPS_G13_MUTANT_SUBSET_DEV")
if _SUBSET:
    _names = set(_SUBSET.split(","))
    MUTANTS = [case for case in MUTANTS if case[0] in _names]

def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()

def execute(label: str, test_filter: str = FILTER) -> tuple[int, str, float]:
    args = [
        "dotnet", "test",
        "tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj",
        "-c", "Release", "--filter", test_filter,
        "--logger", "console;verbosity=normal",
    ]
    start = time.monotonic()
    try:
        p = subprocess.run(args, cwd=ROOT, stdout=subprocess.PIPE,
                           stderr=subprocess.STDOUT, text=True, timeout=1200,
                           env=os.environ.copy(), check=False)
        output = p.stdout
        code = p.returncode
    except subprocess.TimeoutExpired as exc:
        raw = exc.stdout or b""
        output = raw.decode("utf-8", errors="replace") if isinstance(raw, bytes) else raw
        output += "\nTIMEOUT after 1200 seconds\n"
        code = 124
    seconds = round(time.monotonic() - start, 2)
    (EVIDENCE / f"{label}.log").write_text(output, encoding="utf-8")
    return code, output, seconds

def reset_disposable_migration_schema() -> None:
    if os.getenv("HARBOROPS_G13_EXCLUSIVE_POSTGRES") != "1":
        raise RuntimeError("Exclusive disposable PostgreSQL required")
    match = re.search(r"Database=([A-Za-z0-9_]+)", os.getenv("HARBOROPS_TEST_DB", ""))
    if not match or match.group(1) not in ("harborops_integration", "harborops_test"):
        raise RuntimeError("Schema reset allowed only on the named disposable test database")
    found = subprocess.run(
        ["docker", "ps", *_postgres_filter(),
         "--format", "{{.ID}}"], cwd=ROOT, text=True,
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
        check=False, timeout=30)
    instances = found.stdout.strip().splitlines()
    if found.returncode or len(instances) != 1:
        raise RuntimeError("Exactly one exclusive PostgreSQL container required")
    reset = subprocess.run([
        "docker", "exec", instances[0], "psql", "-v", "ON_ERROR_STOP=1",
        "-U", "harborops", "-d", match.group(1), "-c",
        "DROP SCHEMA IF EXISTS public CASCADE; "
        "CREATE SCHEMA public AUTHORIZATION harborops; "
        "GRANT USAGE,CREATE ON SCHEMA public TO PUBLIC;"
    ], cwd=ROOT, text=True, stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT, check=False, timeout=90)
    if reset.returncode:
        raise RuntimeError("Disposable schema reset failed")


def main() -> int:
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    originals = {SOURCE: SOURCE.read_bytes(), ENDPOINT: ENDPOINT.read_bytes(),
                 RETENTION_MIGRATION: RETENTION_MIGRATION.read_bytes(),
                 POLICY: POLICY.read_bytes(),
                 PROGRAM: PROGRAM.read_bytes(),
                 BASE_MIGRATION: BASE_MIGRATION.read_bytes()}
    original = originals[SOURCE]
    source_sha = digest(original)
    identity = _source_identity()
    records: list[dict[str, object]] = []
    try:
        code, output, elapsed = execute("control")
        records.append({"name":"control","source_sha256":source_sha,
                        "exit_code":code,"elapsed_seconds":elapsed,
                        "outcome":"PASS" if code == 0 else "FAIL"})
        if code:
            print("G13 mutation control failed. Mutations not attempted.")
            return 2
        oracle_code, _, _ = execute("oracle-control", ORACLE)
        if oracle_code:
            print("G13 oracle clean control failed. Mutations not attempted.")
            return 2
        # A failed clean targeted test must never count as an effective killed
        # mutation. Run all distinct targeted assertions against unmodified
        # source first and preserve their independent controls.
        extra_targets = sorted(set(
            case[3] for case in MUTANTS if len(case) > 3
            and case[3] not in (
                "TwelveShipmentsAndTwoForeignDecoysReconcileExactMembers",
                "ApprovedTwelvePlusTwoReferenceMetricsReconcileAllMemberKeys")
        ))
        for target in extra_targets:
            # Realistic tenants leave gigabytes of reports and history behind. Every
            # such target runs against a freshly created schema.
            if "Realistic" in target:
                reset_disposable_migration_schema()
            target_code, _, _ = execute("control-" + target,
                "FullyQualifiedName~" + target)
            if target_code:
                print("G13 targeted clean control failed: " + target)
                return 2
        migration_changed = False
        for case in MUTANTS:
            name, before, after, *target_case = case
            test_filter = ("FullyQualifiedName~" + target_case[0] if target_case
                           else FILTER)
            test_name = (target_case[0] if target_case else
                         "TwelveShipmentsAndTwoForeignDecoysReconcileExactMembers")
            selected = (RETENTION_MIGRATION
                if len(target_case) > 1 and target_case[1] == "retention_migration"
                else BASE_MIGRATION if len(target_case) > 1
                    and target_case[1] == "base_migration"
                else PROGRAM if len(target_case) > 1 and target_case[1] == "program"
                else POLICY if len(target_case) > 1 and target_case[1] == "policy"
                else ENDPOINT if len(target_case) > 1 and target_case[1] == "endpoints"
                else SOURCE)
            # EF migrations already applied in a previous test process are
            # not re-executed merely because their C# source is mutated.
            # Rebuild the disposable schema for each effective SQL mutation.
            if selected in (RETENTION_MIGRATION,BASE_MIGRATION) or migration_changed:
                reset_disposable_migration_schema()
                migration_changed = selected in (RETENTION_MIGRATION,BASE_MIGRATION)
            if "Realistic" in test_name:
                reset_disposable_migration_schema()
                migration_changed = False
            text = originals[selected].decode("utf-8")
            expected_replacements = (2
                if name == "auditor-grant-version-revocation-bypassed" else 1)
            if text.count(before) != expected_replacements:
                raise RuntimeError(f"{name}: expected {expected_replacements} replacements, found {text.count(before)}")
            changed = text.replace(before, after)
            selected.write_text(changed, encoding="utf-8")
            code, output, elapsed = execute(name, test_filter)
            if code == 124 or "Build FAILED" in output or "error CS" in output or "error xUnit" in output:
                outcome = "INVALID"
            elif code == 0:
                outcome = "SURVIVED"
            elif test_name in output and (
                "Failed " in output or "[FAIL]" in output):
                outcome = "KILLED"
            else:
                outcome = "INVALID"
            # Two source mutations are intentionally stopped by database
            # constraints and return HTTP 503. They establish fail-closed
            # behavior, not semantic assertion coverage of the SQL predicate.
            expected_constraints = {
                "member-sql-shipment-tenant-predicate-removed":
                    ("23503", "fk_m13_member_shipment"),
                "k03-first-event-deduplication-removed":
                    ("23505", "operational_kpi_members_pkey"),
            }
            basis = "named_behavior_assertion" if outcome == "KILLED" else "none"
            if name in expected_constraints and outcome == "KILLED":
                state, constraint = expected_constraints[name]
                if state not in output or constraint not in output:
                    outcome = "INVALID"
                    basis = "constraint_witness_not_observed"
                else:
                    basis = "database_constraint_fail_closed"
            records.append({"name":name,"source_sha256":digest(changed.encode()),
                            "exit_code":code,"elapsed_seconds":elapsed,
                            "outcome":outcome,"kill_basis":basis})
            print(f"G13_MUTANT {name}: {outcome} ({elapsed}s)", flush=True)
            selected.write_bytes(originals[selected])
    finally:
        for target, content in originals.items():
            target.write_bytes(content)
            if digest(target.read_bytes()) != digest(content):
                raise RuntimeError("G13 mutant source not restored: " + str(target))
        (EVIDENCE / "manifest.json").write_text(json.dumps({
            "source_commit":identity,"clean_source_sha256":source_sha,
            "filter":FILTER,"oracle_filter":ORACLE,
            "additional_clean_controls":extra_targets if 'extra_targets' in locals() else [],
            "records":records
        }, indent=2) + "\n", encoding="utf-8")
    return 0 if records and all(x["outcome"] == "KILLED" for x in records[1:]) else 1

if __name__ == "__main__":
    sys.exit(main())
