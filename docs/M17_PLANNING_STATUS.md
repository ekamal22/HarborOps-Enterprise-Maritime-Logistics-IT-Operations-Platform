# M17 Observability Planning Register

**M17 correctness remediation (2026-11-13):** Candidate fixes for findings F17-001 through F17-014 are committed on the isolated feature branch, but .NET compilation, local Compose startup, collector/alert behavior, three-browser results, performance and G17-S25 have not been executed or accepted. **G17 remains NOT ACCEPTED with 0/25 scenarios and 0/75 atomic assertions reviewed.** [Remediation register](M17_CORRECTNESS_REMEDIATION_20261113.md).

**Source candidate:** [M17 implementation candidate status](M17_IMPLEMENTATION_CANDIDATE_STATUS_20261112.md). G17 remains unverified and not accepted.

**Snapshot:** 2026-11-12.
**Plan baseline:** `056b3a77f6520ac7555890f4666b368ccd351d79` on protected main.
**Milestone:** M17 observability and service health, gate G17.
**Status:** End-to-end plan source-baselined and **M17-P01 through M17-P09 approved as the design baseline on 2026-11-12**. P02/P04/P09 numeric limits remain provisional pending measurements; P05 alert thresholds are initial local rules requiring calibration. **An isolated source candidate now exists; G17 executable tests have not run, 0/25 scenarios and 0/75 assertions have source-locked evidence, acceptance pending.**
**Prerequisite:** G3 accepted and merged. **Downstream:** M18 incident workflows require accepted G17 and G16.

## Planned source deliverables

| Document | Role | State |
| --- | --- | --- |
| [M17 architecture](M17_ARCHITECTURE_DESIGN.md) | Accepted-source analysis, signal/trace/dashboard architecture, privacy and integration boundaries | Proposed |
| [M17 policy register](M17_POLICY_PROPOSAL_20261112.md) | P01-P09 approved design baseline for export, cardinality, tracing, thresholds, storage, exposure and performance | **9 approved; P02/P04/P09 numerical targets provisional and P05 rules to calibrate** |
| [M17 implementation plan](M17_IMPLEMENTATION_PLAN.md) | Ordered work packages, source map, dependencies, checkpoints, handoff and merge discipline | Proposed |
| [G17 verification plan](M17_VERIFICATION_PLAN.md) | 25 scenarios, 75 atomic assertions, evidence contract and independent S25 decision | **0 executed** |
| [M17 operations runbook](M17_OPERATIONS_RUNBOOK.md) | Disposable local startup/fault/recovery/evidence procedures | Source-locked wrapper drafted, not run |
| [Execution graph](EXECUTION_GRAPH.md) | Canonical M17-01 through M17-04 -> G17 dependency wiring, already present | Unchanged planning DAG |

## Observed baseline, not new capability

- `Program.cs` already uses JSON console logging and a server-generated per-request `X-Correlation-ID` shared with audit scope.
- `/health/live` and `/health/ready` exist. Ready checks a seeded PostgreSQL connection with a five-second bounded check.
- M9 transport operation remains held; optional M10/M11/M13/M15 services are opt-in or guarded. Instrumentation must not activate them.
- `compose.yaml` already provides local PostgreSQL, migrator, API and web, but not an accepted general telemetry collector/dashboard.
- M16 audit and M8/M11/M15 histories are not telemetry storage and their accepted permanence/retention rules must remain intact.

## Decision and implementation gates

| Step | Observable completion condition | Current |
| --- | --- | --- |
| Baseline review | Actual source, health and worker flags checked against protected main | Completed for planning snapshot |
| P01-P09 policy decisions | Design baseline explicitly approved 2026-11-12; measured numerical calibration remains under M17-01 and G17 | **APPROVED for design; measurements pending** |
| M17-01 | Names, units, confidentiality, thresholds and finite cardinality frozen | Initial contract in candidate; calibration pending |
| M17-02 | Traces, structured logs, health continuity and bounded export implemented and tested | Source candidate committed; no runtime tests |
| M17-03 | Opt-in private local dashboard, alert rules, safe fault runbook implemented and tested | Local profile and rules authored; no runtime tests |
| M17-04 | Integrated fault journey, affected regressions, privacy/load and source-locked evidence | Verification harness authored; campaign not executed |
| G17-S25 | Independent raw-evidence review with technical PASS/NOT PASS | Not started |
| Gate acceptance | Explicit G17 decision after all mandatory evidence | Pending |
| Feature merge and M18 handoff | Reviewed implementation PR and post-merge reconciliation | Not authorized by planning |

## Next authorized planning handoff

Before writing runtime code, confirm/update the then-current main SHA; apply the approved P01-P09 baseline; pin compatible OpenTelemetry dependencies and local collector/dashboard image versions; freeze source/evidence contracts; and validate the provisional P02/P04/P09 numerical targets and P05 alert calibration during M17-01 and the G17 evidence campaign. Changes to the approved controls require a recorded decision, not silent modification. The implementation may then proceed on its own feature branch after separate authorization. Do not dispatch GitHub Actions, modify deployment/runtime configuration or claim G17 evidence from a docs-only plan.

## Preserved boundaries

G13 is conditionally accepted; G15 is accepted with its disclosed procedural reviewer exception; English/Turkish frontend integration is already merged. No M17 or M18 acceptance is implied. M9 transport remains held, M10/M11/M13/M15 feature activation remains controlled, M15 P10 fail-closed and P12 no deletion remain active, M16 audit remains authoritative. No production deployment, telemetry external export, paging or final G24 release is authorized.
