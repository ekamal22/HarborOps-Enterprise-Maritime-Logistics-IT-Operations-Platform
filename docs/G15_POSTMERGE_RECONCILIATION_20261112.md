# G15 conditional post-merge reconciliation, 2026-11-12

## Status and exact merge
G15 scoped notifications is **conditionally accepted and integrated into protected `main`** through [PR #41](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/41), merge commit `47af1994b9958188666e5dde0aa6b5a536d7c55c`. G13 remains conditionally accepted and integrated through PR #38. The [G15 acceptance decision](G15_CONDITIONAL_ACCEPTANCE_DECISION_20261112.md) discloses that formal independent reviewer attestation was not established. Neither conditional decision is unconditional S25 approval.

## Provenance and technical checks
The [complete run 43139153300](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43139153300) tested `479616a5b7f17c494d897834a66d10b6276bb517`. All 15 jobs passed, 591 TRX tests passed without failures or skips, the combined campaign mapped 248 automated assertions, and the 48 technical scenario checks passed. G15's clean control and 25 negative mutants produced the expected outcomes, including an explicit bounded forward-progress kill for the removed `SKIP LOCKED` variant. The populated G13-only database upgraded from 17 migrations to 18 without changing accepted business rows.

The evidence-ledger, mutation, browser, database and scale artifacts have matching SHA-256 manifests. The merge candidate was assembled from the same tested blobs for all `src/`, `web/`, `tests/`, `compose.yaml` and `.env.example` files. The final merge commit was not itself subjected to a fresh executable campaign, and the technical source identity must not be described as the merge SHA. The temporary G15 verification workflow is not included in `main`.

## Scope and restrictions
The merged source contains the notification service, schema, scoped API, explicit default-off dispatcher, notification frontend and coverage. No Turkish localization was integrated. M15-P10 fail-closed outbox capture remains conditional on verified rollback behavior, which passed. M15-P12 retention periods are provisional, and **no deletion is authorized**. No deployment, production migration or persistent notification delivery activation is approved.

Residual operational concerns include large revoked-membership backlogs, planner statistics and autovacuum sensitivity, quarantined-intent operator recovery, and production performance sizing. These concerns remain explicit, not represented as production SLAs. The M9 transport hold, unresolved-work cap, null ETA, M10 projector default-off and M11 evaluator opt-in remain binding.

## Historical evidence
Previously failed test runs, review outcomes and prior source-specific statements are retained in the original branches and hosted artifacts. The live project status is in [STATUS](STATUS.md). The execution graph is a prerequisite plan and has not been reclassified as observed evidence.
