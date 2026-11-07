# M11 Planning Register

**Current source audit (2026-11-07):** [M11 correctness audit](M11_SECOND_CORRECTNESS_AUDIT_20261107.md) records corrected defects and unresolved verification, local-policy and security boundaries. Runtime execution is not verified and G11 acceptance pending. The accepted M9 transport hold is unchanged.

**Implementation checkpoint (2026-11-07):** A provisional source implementation exists on `feat/m11-delay-exception-lifecycle`, including additive migration 14, rule evaluation, scoped HTTP routes, guarded Manager actions, a shipment UI, and executable tests. The code has not been compiled or run against PostgreSQL in this review. M11 local policy choices remain proposed; G11-S01 is BLOCKED on policy approval; G11-S02 through G11-S25 remain NOT EXECUTED and acceptance pending. No merge, deployment, or transport-hold removal is authorized.

**Date:** 2026-11-07.
**Status:** Architecture, delivery sequence and G11 specification originated in a documentation-only planning branch. The separate draft implementation PR #31 now includes source corrections reviewed in the [audit](M11_SECOND_CORRECTNESS_AUDIT_20261107.md), with execution unverified. M11 policies and G11 acceptance pending.
**Base:** protected `main` at `6ead1d076946dd96b53401265a31efc35c4a666c`. G10 and G16 accepted and integrated. No release authorization.
**Source of truth:** [Status](STATUS.md), [Roadmap](ROADMAP.md), [execution graph](execution-graph.json).

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

Approve or revise the M11 local policy table and independently audit the implemented candidate on its isolated feature branch. Run tiered real PostgreSQL and three-engine G11 verification, then perform a source-locked independent evidence review. Any merge still requires separate authorization. Preserve historical evidence and the accepted M9 hold.
