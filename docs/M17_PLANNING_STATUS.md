# M17 Observability Planning Register

**Current reconciliation (2026-11-17):** G17 observability has been accepted for integration and merged into protected `main` through [PR #53](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/53) at `1a35838acf2d96d561c6d0288e972c33f3a5d2c5`. Authenticated Campaign 01 recorded 51/51 stages PASS and 72/72 technical assertions supported across 25 scenarios. The independent technical assessment recommends PASS; separate signed S25-A03/F06 reviewer attestation remains outstanding. P09 remains QUALIFIED PASS and must be optimized and remeasured before G24. No production deployment is authorized. Earlier status notes in this document are historical snapshots.

**G17 integration decision (2026-11-17): ACCEPTED FOR MERGE with a documented procedural condition.** Campaign 01 on authenticated frozen source `4dd456c852b8baf8e3babe973e81110405a5bef8` (tree `6d6cc5969bad94bb68c99025799b7deb716fb332`) passed 51/51 stages and supported 72/72 technical assertions across 25 scenarios. Independent technical review recommends PASS; a separately signed S25-A03/F06 instrument has not been supplied. Explicit acceptance authorization permits integration without representing that signature as complete. P09 remains QUALIFIED PASS; historical f6/m3 failures and pre-release optimization remain binding. See the [G17 acceptance decision](M17_G17_S25_ACCEPTANCE_DECISION_20261117.md).

**Historical G17 policy decision (2026-11-16):** P09 is **QUALIFIED PASS** only, based on four passing complete runs out of five, with the f6 FAIL and its unexplained latency burst preserved. The approved pooled p95 latency cap is 10% and paired mean peak-memory cap is 15%, unchanged. Performance optimization is deferred to pre-release readiness or an earlier jointly agreed remediation if source-locked verification fails. [Decision](M17_G17_P09_QUALIFIED_PASS_DECISION_20261116.md). **G17 NOT ACCEPTED:** final source-locked verification and distinct S25 sign-off remain pending.

**Sequence constraint:** This is not a new implementation scope or accepted gate. Do not change the P09 method or thresholds to match a failing run. Freeze the final verified source, retain independent assertions A01-A03, and revisit overhead optimization before G24 or when an earlier full gate fails.

**Historical G17 technical verification (2026-11-14):** [GitHub Actions #43541279851](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43541279851) completed **SUCCESS** on executable source `f174eae6b2077937c7d43a8b1f7c34ecdab9b663`. The downloaded evidence reports 47/47 campaign stages passed, 25 scenario IDs, 72/72 source-bound technical assertions passed, and three independent S25 review assertions still pending. No required test was skipped. The raw suite files show 319 unit, 60 contract, 54 M17 integration and 34 regression passes. The 45-entry manifest has no missing expected artifacts, although the GitHub download omits two hidden Playwright `.last-run.json` metadata files listed in that manifest. All earlier failures and the separate c7 archive remain historical. **Technical verification GREEN; G17 NOT ACCEPTED; independent S25 acceptance pending.** [Technical evidence checkpoint](M17_G17_TECHNICAL_VERIFICATION_GREEN_20261114.md).

**Historical G17 checkpoint (2026-11-13, superseded):** The G17 coverage register has 72 authored technical assertions and three pending independent S25 clauses. [Run #43462771279](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43462771279) failed scoped integration with 52 passed, one failed and zero skipped: its process-wide worker span listener and shared database could include unrelated queued work. The strict two-span, single receipt and durable generation assertions remain in the corrected test. [Run #43470770246](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43470770246) was launched on source `a5c3af3829517ec10aed19861dc49ba936b63f97`; its final status is pending at this documentation checkpoint. All historical failed evidence is preserved. **G17 NOT ACCEPTED; acceptance pending.** [S25 readiness](M17_G17_S25_TEST_READINESS_20261113.md) | [Source-locked verification recovery](M17_G17_VERIFICATION_RECOVERY_20261113.md).

**Source candidate:** [M17 implementation candidate status](M17_IMPLEMENTATION_CANDIDATE_STATUS_20261112.md). Historical source candidate status, superseded by the 2026-11-17 G17 integration decision.

**Snapshot:** 2026-11-12.
**Plan baseline:** `056b3a77f6520ac7555890f4666b368ccd351d79` on protected main.
**Milestone:** M17 observability and service health, gate G17.
**Status:** Original planning baseline approved 2026-11-12; implementation and technical verification completed and accepted for integration 2026-11-17. The 75-assertion inventory contains 72 technically supported assertions and 3 separate independent-review assertions. The separately signed S25-A03/F06 instrument is outstanding; P09 remains qualified.
**Prerequisite:** G3 accepted and merged. **Downstream:** M18 incident workflows require accepted G17 and G16.

## Planned source deliverables

| Document | Role | State |
| --- | --- | --- |
| [M17 architecture](M17_ARCHITECTURE_DESIGN.md) | Accepted-source analysis, signal/trace/dashboard architecture, privacy and integration boundaries | Implemented, integrated |
| [M17 policy register](M17_POLICY_PROPOSAL_20261112.md) | P01-P09 approved design baseline for export, cardinality, tracing, thresholds, storage, exposure and performance | P01-P09 approved and technically exercised; P09 qualified, pre-release remeasurement required |
| [M17 implementation plan](M17_IMPLEMENTATION_PLAN.md) | Ordered work packages, source map, dependencies, checkpoints, handoff and merge discipline | Proposed |
| [G17 verification plan](M17_VERIFICATION_PLAN.md) | 25 scenarios, 75 atomic assertions, evidence contract and independent S25 decision | Campaign 01 complete: 51/51 stages, 72/72 technical; reviewer signature pending |
| [M17 operations runbook](M17_OPERATIONS_RUNBOOK.md) | Disposable local startup/fault/recovery/evidence procedures | Source-locked local Campaign 01 complete; no production activation |
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
| P01-P09 policy decisions | Design baseline explicitly approved 2026-11-12; measured numerical calibration remains under M17-01 and G17 | Approved design, technical verification complete with P09 qualification |
| M17-01 | Names, units, confidentiality, thresholds and finite cardinality frozen | Implemented and source-locked technical checks passed |
| M17-02 | Traces, structured logs, health continuity and bounded export implemented and tested | Implemented and integrated after complete source-locked campaign |
| M17-03 | Opt-in private local dashboard, alert rules, safe fault runbook implemented and tested | Monitoring/alerts technically verified and integrated, opt-in only |
| M17-04 | Integrated fault journey, affected regressions, privacy/load and source-locked evidence | 72/72 technical assertions supported, 25 scenarios, 51/51 stages passed |
| G17-S25 | Independent raw-evidence review with technical PASS/NOT PASS | Technical A01/A02 PASS recommended with qualifications; signed A03/F06 pending |
| Gate acceptance | Explicit G17 integration decision and source-locked evidence | Accepted for integration 2026-11-17; separate signed S25-A03/F06 record pending |
| Feature merge and M18 handoff | Reviewed implementation PR and post-merge reconciliation | PR #53 merged into main; G18 remains separately scoped and must satisfy its own gate |

## Historical planning handoff (superseded by integration)

Before writing runtime code, confirm/update the then-current main SHA; apply the approved P01-P09 baseline; pin compatible OpenTelemetry dependencies and local collector/dashboard image versions; freeze source/evidence contracts; and validate the provisional P02/P04/P09 numerical targets and P05 alert calibration during M17-01 and the G17 evidence campaign. Changes to the approved controls require a recorded decision, not silent modification. The implementation may then proceed on its own feature branch after separate authorization. Do not dispatch GitHub Actions, modify deployment/runtime configuration or claim G17 evidence from a docs-only plan.

## Preserved boundaries

G13 is conditionally accepted; G15 is accepted with its disclosed procedural reviewer exception; English/Turkish frontend integration is already merged. M17/G17 is accepted for integration; no M18 implementation or G18 acceptance is implied. M9 transport remains held, M10/M11/M13/M15 feature activation remains controlled, M15 P10 fail-closed and P12 no deletion remain active, M16 audit remains authoritative. No production deployment, telemetry external export, paging or final G24 release is authorized.
