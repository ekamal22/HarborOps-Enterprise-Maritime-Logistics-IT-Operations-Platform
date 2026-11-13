# G17 Repository Documentation Reconciliation

**Date:** 2026-11-13
**Branch:** `feature/m17-observability-20261112`
**Disposition:** Documentation aligned with source-locked G17 recovery. **G17 NOT ACCEPTED; acceptance pending.**

## Verified documentation baseline

The M17 branch tree contains 826 tracked blobs across the application, worker, domain, persistence, monitoring configuration, test suites, browser tests, scripts and documentation. The live project documents were reconciled against the accepted main gate dispositions and the current G17 authored-test register.

The authoritative G17 coverage JSON contains exactly 25 scenarios, 75 assertions, 72 technical `TEST_AUTHORED` records, no `PARTIAL` or `MISSING_TEST` records, and three `INDEPENDENT_REVIEW` records under G17-S25. Source preflight passed on the linked campaigns. All gate acceptance decisions still require runtime artifact inspection and the separate independent review.

## Updated living documents

- Project entry and gate status: `README.md`, `docs/STATUS.md`, `docs/ROADMAP.md`, `docs/DOCUMENT_STATUS_GUIDE.md`.
- Scope and architecture: `docs/PRD.md`, `docs/REQUIREMENTS.md`, `docs/ARCHITECTURE.md`, `docs/ACCEPTANCE_CRITERIA.md`, `docs/DECISIONS.md`, `docs/IMPLEMENTATION_PLAN.md`.
- Engineering, security and operation: `docs/TEST_STRATEGY.md`, `docs/SECURITY.md`, `docs/OPERATIONS.md`.
- Current M17 plans and source status: `docs/M17_ARCHITECTURE_DESIGN.md`, `docs/M17_IMPLEMENTATION_PLAN.md`, `docs/M17_PLANNING_STATUS.md`, `docs/M17_IMPLEMENTATION_CANDIDATE_STATUS_20261112.md`, `docs/M17_VERIFICATION_PLAN.md`, `docs/M17_OPERATIONS_RUNBOOK.md`, `docs/M17_POLICY_PROPOSAL_20261112.md`.
- Verification and retained reviews: `docs/M17_G17_S25_TEST_READINESS_20261113.md`, `docs/M17_G17_VERIFICATION_RECOVERY_20261113.md`, `docs/M17_CORRECTNESS_REMEDIATION_20261113.md`, `docs/M17_CORRECTNESS_ROUND2_REMEDIATION_20261113.md`.

The repeated obsolete 26-authored, 32-partial and 14-missing summaries and claims of no G17 verification dispatch were replaced on these live status surfaces. Historical review findings remain recorded as historical snapshots instead of being overwritten with claims of passing tests.

## Unchanged authorities and protected history

- `docs/execution-graph.json` is the canonical dependency graph, and `docs/EXECUTION_GRAPH.md` is generated from it. Neither was manually rewritten because the dependency edges and its G17 not-accepted gate distinction have not changed.
- The original 25-scenario/75-assertion inventory remains the requirement baseline. The current coverage JSON remains the source of technical witness identities.
- Dated reports for previously accepted milestones, historical failed G17 runs, protected `main`, optional/default-off policies, the M9 transport hold and the no-deletion boundary remain intact.
- Documentation-only commits after source SHA `3500bc33997b0ad4fa803e5983c5444d7d1d598a` do not constitute a new tested application source. [Run #43445954783](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43445954783) is pinned to that executable candidate and was still in progress when this reconciliation was recorded.

## Acceptance follow-through

The verification recovery register tracks the failed runs, exact corrective commits and latest relaunch. Next evidence must reconcile all 72 technical assertions individually, all required runtime and browser cases, raw artifacts, privacy and operational constraints. The three G17-S25 assertions then require separate independent review. Passing a unit or contract suite, successful compilation, completed authoring, or an in-progress run is never an accepted G17 gate decision.

[Current status](STATUS.md) | [G17 readiness](M17_G17_S25_TEST_READINESS_20261113.md) | [Verification failures and relaunches](M17_G17_VERIFICATION_RECOVERY_20261113.md).
