# M6 Assignment Planning Status

**As of:** 2026-11-02
**Milestone:** M6 container and voyage assignments
**Planning branch:** `feat/m6-container-voyage-assignments`
**Accepted implementation baseline:** `main` at `344c6eb96a9af600b5843cc97c56a8449174bad8`, G0-G5 accepted
**State:** M6 implementation, API, persistence, React, tests and local verification harness committed on the feature branch and executed locally on `a94d7cd406a93806527d28ba7d2ce2d6ba3307b6`; technical evidence is complete except G6-S01. M6-local contract and G6 acceptance pending. Nothing merged into `main`. See [executed verification](M6_EXECUTED_VERIFICATION.md) and the [implementation register](M6_IMPLEMENTATION_STATUS.md).

## Planning deliverables

| Record | Scope | Disposition |
| --- | --- | --- |
| [M6 local contract](M6_LOCAL_CONTRACT_DECISION.md) | 12 explicit choices covering interval, route, permissions, archive, history and acceptance | Proposed, awaiting decision |
| [M6 architecture](M6_ARCHITECTURE_DESIGN.md) | Two association entities, GiST exclusion, scoped FKs, parent guards, eight routes, React integration | Proposed |
| [M6 implementation plan](M6_IMPLEMENTATION_PLAN.md) | M6-01 policy; container/voyage source; M6-04 workflow and verification requirements | Source authored; not executed |
| [M6 implementation status](M6_IMPLEMENTATION_STATUS.md) | Concrete source files, tests, schema and outstanding G6 verification | Committed; runtime unverified |
| [G6 verification plan](M6_VERIFICATION_PLAN.md) | G6-S01 to G6-S25, direct SQL, concurrency, three-browser regression, raw evidence and Q1 snapshot checkpoint | Planned, not executed |
| [Execution graph](execution-graph.json) | G4/G5 -> M6-01 -> parallel M6-02/M6-03 -> M6-04 -> G6 | Unchanged |

## Confirmed prerequisites

G4, D-003 Option B, the M5 local contract and G5 are accepted on the baseline. G6 is a separate gate and is not implied by acceptance of its parents. M6 owns FR-005 and the connected shipment allocation journey. M8 ingestion and M12 search/export remain outside M6.

## Technical decisions requiring approval

The proposed contract needs agreement on half-open UTC intervals and immutable association history, `btree_gist` exclusions, exactly route-matched single-leg voyage association, explicit parent closure before terminal/archival operations, and assignment-specific Analyst/Manager write plus Auditor read rights. These are not recorded as accepted. Concurrency and direct-SQL parent-guard design must be experimentally proven at M6-01, especially SQL lock-order inversions.

## Required G6 evidence

The future technical gate must run full M0-M5 regressions, fresh/repeat PostgreSQL tests, historical M5 upgrade/rollback/reapply, role/tenant matrix, adversarial overlapping writes, three browsers, dependency/image scans and recovery. Raw results belong under `E:\HarborOps-Verification\m6`, with isolated `ho-verify-g6` resources, SHA-256 integrity and independent review. The Q1 retained historical volume check happens at G6 if a representative snapshot exists; otherwise document `NOT APPLICABLE` with reason. No GitHub Actions and no persistent C: storage are authorized.

## Next action

Review and explicitly approve or revise [the M6-local contract](M6_LOCAL_CONTRACT_DECISION.md). The requested initial implementation is now authored on the feature branch, but none of its tests, migration or browser suites has executed. Complete the mandatory E:-backed G6 verification and independent raw evidence review before requesting separate G6 acceptance and merge authorization.
