# M10 Planning Status

**As of:** 2026-11-05  
**Milestone:** M10 Event timeline and projection rebuild  
**State:** Architecture, implementation sequence and G10 verification plan drafted. No M10 runtime implementation or source-locked execution is claimed. G10 acceptance pending.  
**Entry:** G9 accepted on 2026-11-05 with its transport-backlog operating condition.

## Planning package

- [Architecture design](M10_ARCHITECTURE_DESIGN.md): M9 effect-driven, tenant-scoped accepted timeline, separate observed tracking state, append-only corrections, deterministic reducer, idempotent materialization, rebuild and transport-backlog boundary.
- [Implementation plan](M10_IMPLEMENTATION_PLAN.md): M10-01 contract freeze; parallel M10-02 scoped APIs and M10-03 versioned projections; M10-04 final integrated verification.
- [G10 verification matrix](M10_VERIFICATION_PLAN.md): 25 planned scenarios, no executed tests.
- [Release documentation follow-up](RELEASE_READINESS_TODO.md): PR #21 reconciliation and final local documentation checks before M24-03/G24.

## Decisions requiring explicit resolution before implementation contract freeze

1. Confirm deterministic order, same-time contradictory observations and supported tracking-state vocabulary, with no invented source sequence.
2. Confirm Manager-only append-only void/supersede corrections, provenance and authorization policy.
3. Confirm read model revision/digest, anti-join processing and rebuild concurrency rules without changing accepted M9 semantics.
4. Confirm whether transport-reference association and bounded `NeedsReconciliation` backlog handling belong within M10 or a separately approved mechanism. The mandatory M9 transport hold **remains active** unless and until its release criteria are independently proven and explicitly approved.
5. Coordinate schema/permission overlap with M16 before any integration. M16 standalone main evidence was independently inspected, but `supplement-s07-s08-s28.log` remains UNREVIEWED and combined M9/M16 testing plus G16 acceptance are pending.

## Current disposition

M10 planning documents do **not** amend the version 3.0 execution graph, approve policy choices, change existing runtime source, grant G10 acceptance, merge M16 or permit hosting/deployment. All test and evidence steps are future work until executed and documented.
