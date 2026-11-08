# M11/M12 Verification Operations

**Local closure update (2026-11-08):** [Local closure results](M11_M12_LOCAL_CLOSURE_RESULTS_20261108.md) record focused local verification on disposable PostgreSQL: measured G12-S20 query plans over 170,000 multi-tenant shipments, G11 witness, authority, atomicity and recovery cases, and G12 boundary, CSV, revocation, cancellation and signed-audit cases. All final stages passed (291 unit, 46 contract, 20 existing G11 plus upgrade, 40 existing G12 plus residual, 15 amended G11 supplement, 1 plan probe). Defect F-M12-01 (export returned 503 on a serialization failure caused by concurrent session activity or in-flight revocation) was fixed and verified at `5dedbb7`. The updated [G11](M11_G11_EVIDENCE_MATRIX_20261108.md) and [G12](M12_G12_EVIDENCE_MATRIX_20261108.md) matrices keep PARTIAL, NOT EXECUTED and BLOCKED rows. Independent S25 reviews, a full-suite rerun decision and acceptance remain pending for both gates. Protected `main`, the M9 hold and all deployment restrictions are unchanged.

**Final M11/M12 technical verification (2026-11-08):** [Run reconciliation](M11_M12_VERIFIED_COVERAGE_RECONCILIATION_20261108.md) confirms **341/341 integration PASS** on `f56d017` with no skips, plus 40/40 impacted, G11 19/19, G12 4/4, three-engine browser 36/36, effective mutants 5/5, SQL read isolation and security scans. [G11/G12 acceptance sufficiency review](M11_M12_ACCEPTANCE_REVIEW_20261108.md), [G11 matrix](M11_G11_EVIDENCE_MATRIX_20261108.md) and [G12 matrix](M12_G12_EVIDENCE_MATRIX_20261108.md) record exact remaining evidence gaps. Independent S25 reviews and acceptance pending. Older 340-case and 'in progress' notes below describe historical checkpoints, not current execution.

**Current executed evidence (2026-11-08):** [M11/M12 verification reconciliation](M11_M12_VERIFIED_COVERAGE_RECONCILIATION_20261108.md) is the authoritative run-by-run record: 340/340 earlier whole integration, 40/40 impacted tests including 13-to-15 populated upgrade, G11 19/19, G12 4/4, 36/36 three-engine browser, 5/5 effective mutants, restricted SQL isolation and security scans, plus 291/291 unit and 46/46 contract on their individually identified SHAs. Full run `42443065884` is still in progress. Preserve historical failures and pending independent G11-S25/G12-S25 reviews; no acceptance, merge or deployment is implied.

**Date:** 2026-11-08  
**Branch:** `feat/m11-m12-combined-integration`  
**Accepted main:** `6ead1d076946dd96b53401265a31efc35c4a666c`  
**Disposition:** Verification governance only. G11 and G12 acceptance, merge and deployment remain pending.

## Verification source of truth

The [source-locked progress register](M11_M12_VERIFICATION_PROGRESS_20261108.md) records exact run outcomes and tested SHAs. The 338/338 integration suite passed on `4b5d5c2692311cbc00a00475e1f28f29c3f55685`. Later 340+ case source-locked runs, enriched G11/G12 database tests, browser, image and mutation campaigns require independent per-run reconciliation. Earlier component results are not proof of a full final-source gate.

The local M11 policy contract is recorded. The M12 technical policy selection and Q2 snapshot applicability have separate decision records. G11-S08 completeness, raw evidence with hashes, independent G11-S25/G12-S25 reviews and explicit gate decisions remain open unless independently resolved on the same frozen source.

## Campaign control

One push at `f56d017` started ten overlapping verification workflows. This is not an acceptable default. All ten M11/M12 source, database, SQL isolation, impacted, full integration, browser, image, mutation, dependency and documentation workflows are now **manual dispatch only**. Pushes must not automatically initiate those resource-intensive campaigns.

For future work:

1. Develop using focused, efficient change-impact tests. Run fast checks first. Do not start the whole campaign for every source or documentation push.
2. Freeze a single exact commit SHA for a final campaign and record all related artifacts under that revision.
3. Trigger only the necessary manual verification jobs in a coordinated sequence. Parallelize only independent resource-isolated workloads, especially where PostgreSQL roles are modified.
4. Keep G11 and G12 scenario evidence and independent S25 reviews separate, even when safely sharing a compiled build or fixture.
5. Use targeted reruns for substantive source changes, with reasons and superseded-SHA links. Do not rerun expensive suites for documentation-only edits.
6. Preserve historical failures, skips and cancelled attempts. Do not treat a green workflow as equivalent to complete acceptance.
7. Capture raw TRX/browser/security/SQL evidence and checked hashes where required; logs and summaries alone do not close S25.

Switching workflows to manual-only does not cancel runs already in flight. Monitor the in-progress cohort through terminal status and report its outcomes, including failures. Avoid creating new runs solely to refresh documentation.

## Safety and repository boundaries

Protected `main` remains unchanged until separate merge authorization. Do not authorize production migrations, deployment, G11/G12 acceptance or M9 hold clearance from this governance update. ARRI/DEPA remain parked, the unresolved-work-item cap is 10,000, the M10 projector is disabled by default and ETA remains null. Retain existing evidence, worktrees and databases. Persistent local storage belongs on E:, with unavoidable short-lived temporary C: use permitted.

For ongoing state consult [STATUS](STATUS.md), [combined plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md), [G11 plan](M11_VERIFICATION_PLAN.md), [G12 plan](M12_VERIFICATION_PLAN.md) and the [progress register](M11_M12_VERIFICATION_PROGRESS_20261108.md). Older source-only and failed results remain historical, not current implementation claims.
