# M11/M12 Verification Operations

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
