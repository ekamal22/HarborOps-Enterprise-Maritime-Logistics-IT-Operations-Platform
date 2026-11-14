# G17 Repository Documentation Reconciliation

**Current G17 technical verification (2026-11-14):** [#43541279851](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43541279851) completed SUCCESS on source `f174eae6b2077937c7d43a8b1f7c34ecdab9b663`. All 47 campaign stages passed and the source-locked ledger reports 72 of 72 technical assertions passing; the three independent S25 assertions are pending. No required tests were skipped. The 45-entry evidence manifest has no missing expected artifacts, although two hidden Playwright last-run metadata files are absent from the downloaded archive. **Technical G17 campaign GREEN; G17 NOT ACCEPTED; independent S25 review pending.** [Current verified technical result](M17_G17_TECHNICAL_VERIFICATION_GREEN_20261114.md).

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
- [#43470770246](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43470770246) passed all 53 scoped integration cases with no skips, then FAILED network verification: all owned service labels/networks matched, but the collector was not observed running. Source-locked failure evidence was retained and no root cause beyond the missing running process was invented. After 21 static assertions, three local negative tests, a YAML pipeline audit and passing/failing mocked end-to-end network checks, the fix was committed as `7f439b95f09467150027fb8635e15e1abb743ac0`. [#43477790029](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43477790029) was launched afterward; its no-skip preflight passed and the full campaign was still in progress at this checkpoint. G17 NOT ACCEPTED.

## Acceptance follow-through

The verification recovery register tracks the failed runs, exact corrective commits and latest relaunch. Next evidence must reconcile all 72 technical assertions individually, all required runtime and browser cases, raw artifacts, privacy and operational constraints. The three G17-S25 assertions then require separate independent review. Passing a unit or contract suite, successful compilation, completed authoring, or an in-progress run is never an accepted G17 gate decision.

[Current status](STATUS.md) | [G17 readiness](M17_G17_S25_TEST_READINESS_20261113.md) | [Verification failures and relaunches](M17_G17_VERIFICATION_RECOVERY_20261113.md).

## Update 2026-11-14

Current M17 status wording across `STATUS.md`, `ROADMAP.md`, the recovery register and the S25 readiness document now distinguishes: implemented and tested (yes), technically verified for 72 of 72 technical assertions at `085fc666` (yes, locally, run c7), independently reviewed (no), accepted (no), integrated (no). The execution graph structure is unchanged because no milestone dependency changed. Historical hosted failure records were kept as written.


## Superseded source checkpoint

This is a historical 2026-11-13 reconciliation. [The latest G17 reconciliation](M17_G17_REPOWIDE_DOCUMENTATION_RECONCILIATION_20261114.md) records the S25-remediated source, the first-incident 5xx finding and the hosted rerun. Original failed evidence remains unchanged.
