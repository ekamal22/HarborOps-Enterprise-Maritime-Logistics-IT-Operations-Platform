# M11 Planning Register

**Current verification policy (2026-11-08):** [M11/M12 verification operations](M11_M12_VERIFICATION_OPERATIONS_20261108.md) now requires manual, coordinated, source-locked workflows instead of automatic push-triggered campaigns. [Run-by-run progress](M11_M12_VERIFICATION_PROGRESS_20261108.md) is the current verification checkpoint; older reports and source-only notes below are historical. G11/G12 independent S25 review and acceptance remain pending. Protected `main`, the M9 transport hold, and deployment restrictions are unchanged.

**Latest hosted verification (2026-11-08):** [Executed M11/M12 run register](M11_M12_HOSTED_VERIFICATION_20261108.md) records a successful .NET and frontend source gate, G11 18/18 scoped PostgreSQL tests, G12 2/2 exclusive PostgreSQL tests, 36/36 impacted integration regressions, and direct SQL isolation of exception, history and evaluation tables with forged-scope denial. Updated source/dependency and runtime-image security scans passed. The three-engine browser rerun passed 36/36 tests, with 12/12 each in Chromium, Firefox and WebKit, including M11 exception journeys, M12 search/export and targeted M5 regressions. G11-S08 conflict-specific provenance remains open; M12 local policy decisions and independent G11/G12 S25 reviews remain pending. No G11/G12 acceptance, merge or deployment is authorized. Historical source-only notes below are retained as previous checkpoints.

**Historical combined candidate checkpoint (before hosted verification):** The isolated `feat/m11-m12-combined-integration` branch has been assembled from accepted `main` and both feature sources. [M11 local policies](M11_LOCAL_CONTRACT_DECISION_20261108.md) are now recorded, including clearance through Unknown/Stale and reviewed historical late-arrival resolution. This supersedes earlier pending-policy and no-combined-source snapshots in this document. Source-only corrections and authored tests are not executed evidence. G11/G12 technical verification, independent S25 review and acceptance pending. Protected `main`, M9 operating hold and deployment boundary are unchanged.

**M11/M12 coordination approved (2026-11-08):** [Combined integration and verification plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md). Shared verification orchestration has been approved, not M11-P01..P12 rule decisions. G11-S01 remains BLOCKED, other G11 scenarios unexecuted; a future combined candidate will retain its own G11 evidence ledger and S25 review.

**Parallel M11/M12 source status (2026-11-08):** [Source and integration register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md) records draft M11 PR #31 alongside draft M12 PR #32, with different additive migration IDs and an unresolved shared API/UI/role integration boundary. Both are source candidates, not merged or runtime-verified; G11-S01 BLOCKED, G11-S02..S25 NOT EXECUTED, G12 technical verification pending. Accepted `main` and the M9 transport hold remain unchanged.

**Historical M11 source audit (before hosted verification):** [Sixth correctness audit](M11_SIXTH_CORRECTNESS_AUDIT_20261108.md) documents informational severity on inactive outcomes, versioned rule-metadata reconciliation and typed due-work continuation. The source changes and new tests are unexecuted. G11-S01 BLOCKED, G11-S02 through S25 NOT EXECUTED, acceptance pending. The M9 transport-processing hold remains in force.

**Implementation checkpoint (2026-11-07):** A provisional source implementation exists on `feat/m11-delay-exception-lifecycle`, including additive migration 14, rule evaluation, scoped HTTP routes, guarded Manager actions, a shipment UI, and executable tests. The code has not been compiled or run against PostgreSQL in this review. M11 local policy choices remain proposed; G11-S01 is BLOCKED on policy approval; G11-S02 through G11-S25 remain NOT EXECUTED and acceptance pending. No merge, deployment, or transport-hold removal is authorized.

**Date:** 2026-11-07.
**Status:** Architecture, delivery sequence and G11 specification originated in a documentation-only planning branch. The separate draft implementation PR #31 now includes source corrections reviewed in the [audit](M11_SECOND_CORRECTNESS_AUDIT_20261107.md), with execution unverified. M11 policies and G11 acceptance pending.
**Base:** protected `main` at `6ead1d076946dd96b53401265a31efc35c4a666c`. G10 and G16 accepted and integrated. No release authorization.
**Source of truth:** [Status](STATUS.md), [Roadmap](ROADMAP.md), [execution graph](execution-graph.json).

## Sixth audit checkpoint

Source changes now classify inactive rule outcomes as informational, reconcile differing visible rule metadata on a stable fingerprint with immutable reevaluation history, and explicitly type due-work UUID boundaries. The [sixth audit](M11_SIXTH_CORRECTNESS_AUDIT_20261108.md) is a source-review checkpoint, not a policy decision or G11 acceptance. All outstanding decision and runtime requirements persist.

## Fifth audit checkpoint

The implementation branch now preserves new identity on direct acknowledged-and-cleared recurrence and matches Resolve UI to SQL rules. M11-P10 still requires decisions on intervening uncertainty and immutable recorded lateness. The [fifth audit](M11_FIFTH_CORRECTNESS_AUDIT_20261108.md) is not executable gate evidence or merge authorization.

## Fourth audit checkpoint

The implementation branch added scoped history continuation and a database-clock creation cutoff for exception-list cursors. These are source changes only; the M11-P01 to P12 decision list remains proposed, and G11 acceptance requires full executed evidence and independent review. This planning record is not a gate decision.

## Scope and prerequisites

| Node | Canonical prerequisite | Planned deliverable | Status |
| --- | --- | --- | --- |
| M11-01 | G10 | Versioned delay/risk/conflict rule and threshold contract | Proposed, policy acceptance pending |
| M11-02 | M11-01 | Transactional tenant-scoped exception evaluation | Source implemented; verification pending |
| M11-03 | M11-01 | Append-only, authorized and audited exception lifecycle | Source implemented; verification pending |
| M11-04 | M11-02 and M11-03 | API/UI, source-locked full G11 campaign and end-to-end acceptance package | API/UI and test sources implemented; campaign not executed |
| G11 | M11-01 through M11-04 | Independent review and explicit decision | Acceptance pending |

M11-02 and M11-03 are parallel-capable after the policy contract is resolved, but share one coordinated EF migration/snapshot and schema integration. M13-01 and M15-01 depend on G11, not on this planning document. No execution graph change is proposed.

## Deliverables

- [M11 architecture](M11_ARCHITECTURE_DESIGN.md): accepted-source inventory, evidence truth table, twelve proposed M11-specific policies, schema, roles, API/UI, concurrency and operational boundaries.
- [M11 implementation plan](M11_IMPLEMENTATION_PLAN.md): ordered work packages, target source modules, integration points, staging, engineering checkpoints and isolated delivery discipline.
- [G11 verification specification](M11_VERIFICATION_PLAN.md): 25 source-locked scenarios, negative/role/migration/restart/browser/mutation coverage, evidence requirements and independent acceptance boundary.

## Open decisions and restrictions

M11-P01 through M11-P12 are **proposed**, not approved. In particular, scheduled-arrival overdue versus confirmed physical delay, recorded workflow confidence, departure slippage risk, grace thresholds, Manager-only dispositions and database-signed authority must receive explicit review. No numerical threshold is accepted merely because it appears in the proposal.

The accepted M9 transport-processing hold remains. ARRI/DEPA are parked; the 10,000 unresolved-work-item cap is not negotiable through M11. M10 ETA remains null. G11 will not implement predictive ETA, transport-event backlog remediation, M13 KPIs, M15 notifications, incident lifecycle, deployment or public hosting.

M11 migration, runtime code, browser test source and API changes exist on the isolated implementation branch. No executable tests, local schema application, GitHub Actions, acceptance decision, merge or deployment are represented by this checkpoint.

## Next execution boundary

The M11 local policy table was decided on 2026-11-08 for the combined source candidate. Independently review the implementation, resolve remaining technical blockers and independently audit the implemented candidate on its isolated feature branch. Run tiered real PostgreSQL and three-engine G11 verification, then perform a source-locked independent evidence review. Any merge still requires separate authorization. Preserve historical evidence and the accepted M9 hold.
