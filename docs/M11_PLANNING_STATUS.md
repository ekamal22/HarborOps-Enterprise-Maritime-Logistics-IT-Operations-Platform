# M11 Planning Register

**Date:** 2026-11-07.
**Status:** M11 architecture, delivery sequence and G11 test specification proposed on a documentation-only feature branch. Implementation NOT STARTED. All G11 scenarios NOT EXECUTED. M11 policy decisions and G11 acceptance pending.
**Base:** protected `main` at `6ead1d076946dd96b53401265a31efc35c4a666c`. G10 and G16 accepted and integrated. No release authorization.
**Source of truth:** [Status](STATUS.md), [Roadmap](ROADMAP.md), [execution graph](execution-graph.json).

## Scope and prerequisites

| Node | Canonical prerequisite | Planned deliverable | Status |
| --- | --- | --- | --- |
| M11-01 | G10 | Versioned delay/risk/conflict rule and threshold contract | Proposed, policy acceptance pending |
| M11-02 | M11-01 | Transactional tenant-scoped exception evaluation | Not started |
| M11-03 | M11-01 | Append-only, authorized and audited exception lifecycle | Not started |
| M11-04 | M11-02 and M11-03 | API/UI, source-locked full G11 campaign and end-to-end acceptance package | Not started |
| G11 | M11-01 through M11-04 | Independent review and explicit decision | Acceptance pending |

M11-02 and M11-03 are parallel-capable after the policy contract is resolved, but share one coordinated EF migration/snapshot and schema integration. M13-01 and M15-01 depend on G11, not on this planning document. No execution graph change is proposed.

## Deliverables

- [M11 architecture](M11_ARCHITECTURE_DESIGN.md): accepted-source inventory, evidence truth table, twelve proposed M11-specific policies, schema, roles, API/UI, concurrency and operational boundaries.
- [M11 implementation plan](M11_IMPLEMENTATION_PLAN.md): ordered work packages, target source modules, integration points, staging, engineering checkpoints and isolated delivery discipline.
- [G11 verification specification](M11_VERIFICATION_PLAN.md): 25 source-locked scenarios, negative/role/migration/restart/browser/mutation coverage, evidence requirements and independent acceptance boundary.

## Open decisions and restrictions

M11-P01 through M11-P12 are **proposed**, not approved. In particular, scheduled-arrival overdue versus confirmed physical delay, recorded workflow confidence, departure slippage risk, grace thresholds, Manager-only dispositions and database-signed authority must receive explicit review. No numerical threshold is accepted merely because it appears in the proposal.

The accepted M9 transport-processing hold remains. ARRI/DEPA are parked; the 10,000 unresolved-work-item cap is not negotiable through M11. M10 ETA remains null. G11 will not implement predictive ETA, transport-event backlog remediation, M13 KPIs, M15 notifications, incident lifecycle, deployment or public hosting.

No runtime tests, browser suites, migrations, source modifications, GitHub Actions, acceptance decisions, merges or deployments are represented by these planning documents.

## Next execution boundary

Approve or revise the M11 local policy table, then explicitly authorize implementation on a separate feature branch. Run the tiered G11 verification after integration. Keep documentation aligned with actual executed progress, preserving historical failed attempts and accepted prior gate evidence.
