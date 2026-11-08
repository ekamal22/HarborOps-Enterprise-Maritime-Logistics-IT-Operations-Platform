# Combined M11/M12 Evidence Sufficiency Review

**Current disposition (2026-11-08):** M11/G11 and M12/G12 accepted following separate S01 policy and S25 evidence decisions; the combined source merged into `main` through [PR #33](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/33). [Formal gate closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md) and [project status](STATUS.md) supersede any earlier proposed, blocked or unmerged milestones in this dated record. The original evidence and earlier failed runs remain valid historical observations. M9 ARRI/DEPA hold remains binding; no deployment authorized.

**Date:** 2026-11-08
**Reviewed candidate application SHA:** `f56d017d9b2619be56fe533f73ba1d11f63974c0`
**Combined documentation/workflow branch at review:** `0edbe06ff5c0b61ba124f20b3de315425e4f73b3`
**Accepted main:** `6ead1d076946dd96b53401265a31efc35c4a666c`
**Historical recommendation at this review checkpoint:** Advance both milestones to targeted evidence closure without granting acceptance based on this earlier incomplete evidence. The subsequent [gate closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md) records their formal acceptance and merge. Deployment and the M9 hold remain unchanged.

## Local closure and independent review update (2026-11-08)

The first targeted local campaign ([results](M11_M12_LOCAL_CLOSURE_RESULTS_20261108.md)) was independently reviewed on the same day: [G11-S25 NOT PASS](M11_G11_INDEPENDENT_EVIDENCE_REVIEW_20261108.md) and [G12-S25 NOT PASS](M12_G12_INDEPENDENT_EVIDENCE_REVIEW_20261108.md), each with open mandatory findings. A closure campaign then addressed those findings: the complete 388-case integration suite with no skips, deterministic ambiguous-commit and recovery cases, real process kills and PostgreSQL crash and restart in an isolated Compose stack, the real Nginx path with the 15, 25 and 30 second boundaries, CPU starvation, client resets and disconnect cancellation, 48/48 three-engine browser tests, 17/17 effective mutants killed and clean scans. Two defects were fixed (export serialization retry, explicit UTC designator). The [G11](M11_G11_EVIDENCE_MATRIX_20261108.md) and [G12](M12_G12_EVIDENCE_MATRIX_20261108.md) matrices carry the current dispositions. **G11-S25 and G12-S25 remain BLOCKED pending an independent re-review of the closure package; acceptance is a separate decision.** The sections below describe the earlier hosted-only evidence and are retained as history.

## Reconciled technical result

[Final whole integration run](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065884) **PASS: 341/341**, zero failed/not-executed, clean warning-as-error build. The run log records TRX SHA-256 `6b6559da3a968e4f9f7c1732d1d78674929a160c7b25c3f90b375ff07c6ea786`. The hash was logged by the runner, not independently recomputed from a downloadable TRX. [G11 19/19 and G12 4/4](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065861), [impacted 40/40](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065810), [36/36 browsers](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065925), [5/5 effective mutants](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065914), [SQL read isolation](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065978) and the image/dependency scans also passed on `f56d017d9b2619be56fe533f73ba1d11f63974c0`. Source, docs, 291/291 unit and 46/46 contract passed on the later source-review commit `4b9cd458`.

Comparison from the frozen application SHA to the branch review HEAD shows 11 commits changing docs, generated view and verification workflows, with no src/, web/, runtime scripts or tests changed. Those later documentation commits were not themselves all verified with a fresh source gate and are not treated as a new application candidate.

The earlier failed verification and SQL-leakage attempts are preserved in [run index](M11_M12_EXECUTED_RUN_INDEX_20261108.json). GitHub reported no downloadable artifacts for the final full integration, G11/G12 database, browser, mutation and source-check runs when checked. No raw archive has been recovered. GitHub logs and the logged hash establish good preliminary evidence, but not an independent verified raw checksum manifest.

## G11-S08 source and executed witness review

At `f56d017d9b2619be56fe533f73ba1d11f63974c0`, the G11 SQL migration selects effective GTIN/GTOT records, excludes superseded receipts, uses correction replacement IDs and deterministic ordering, and emits `conflictWitness` plus `conflictContainerCount` and `conflictWitnessTruncated`. The executed integration test `EquipmentConflictCarriesExactAcceptedEventAndContainerWitness` checks accepted event/receipt/source IDs, a replacement correction, and two conflicting containers with count 2 and truncated=true. Thus **the earlier claim of no multi-conflict test is superseded**.

Remaining G11-S08 sufficiency questions are exact deterministic-first-witness ordering under competing UUID/timestamp permutations, bounds at larger container counts and full correction-out-of-order behavior. The source uses `ORDER BY container_id LIMIT 1` and a count, but no independently retained raw large-fixture result was found. No claim of full S08 or S25 closure is justified without the required negative and provenance package.

## G12 timing, plans and failure review

A live export fixture rejects 5,001 rows without a recorded prepared export, and a real PostgreSQL blocking lock triggers a statement-timeout response without partial CSV or audit. The latest targeted suite passed those tests. The search/export store sets a 15-second SQL timeout and an internal linked cancellation budget. This proves the *tested paths*, not every boundary and environment.

The mandatory S20 measured PostgreSQL `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` for representative search/filter/keyset/export volumes is **not evidenced** by the checked G12 tests and run logs. An assertion that the query looks indexed is not an executed plan. Additional specific evidence is needed for 10 MiB +/-1 boundary, 5,000-row accepted boundary, end-to-end 25/30-second proxy timing, mid-response/browser cancellation, multi-process export/rate behavior, and audit-outage failure atomicity. These should be isolated, focused local probes rather than a repeat of all 341 integration cases.

## Independent S25 sufficiency determination

This is an evidence sufficiency and code/test-source review, not an independent reparse of a sealed G11 or G12 raw evidence bundle. **G11-S25: BLOCKED** for independent raw report, manifest and full scenario-level dispositions. **G12-S25: BLOCKED** for the same reason and missing measured S20 performance evidence. The [G11 matrix](M11_G11_EVIDENCE_MATRIX_20261108.md) and [G12 matrix](M12_G12_EVIDENCE_MATRIX_20261108.md) identify every scenario and subset.

Do not certify S25 until an independent reviewer can access the E:-backed raw TRX/JSON/SQL/browser/scan evidence, verify a SHA-256 manifest, validate exact tested source and mandatory failures/negative controls, and issue separate written S25 decisions. Technical policy selections for M12-P01..P16 remain subject to formal G12-S01 reconciliation. The Q2 production snapshot applicability is documented as NOT APPLICABLE for a known repository-owned dataset; this does not waive the successfully executed populated 13-to-15 upgrade.

See [targeted local closure runbook](M11_M12_LOCAL_CLOSURE_RUNBOOK_20261108.md) for exact isolated probes and raw evidence capture. The runbook is planned, not executed.

## Originally recommended local execution (completed in the later closure campaign)

1. Freeze `f56d017d9b2619be56fe533f73ba1d11f63974c0` in a clean E:-backed worktree. Reuse existing verified 341/341 evidence where appropriate; do not repeat the whole suite without a functional change.
2. Perform targeted G11 evidence for deterministic multi-conflict selection and correction-order permutations, due-scan restart/missed checkpoint, injected M16 audit failure/rollback and crash-before/after commit. Retain exact fixtures, outcomes and transaction logs.
3. Perform targeted G12 EXPLAIN plans with measured latency and buffers over representative tenant/search/assignment/keyset/export data. Exercise row/byte exact boundaries, multi-instance rate/concurrency, revocation, proxy deadline and cancellation, audit-key/SQL and audit-failure paths.
4. Capture raw reports and SHA-256 manifest under an isolated E: evidence root; quarantine secrets, preserve all earlier failures. Require one independent G11-S25 and one independent G12-S25 raw reparse before requesting acceptance.
5. Update living docs with only executed results. Maintain ten manual-only verification workflows. Do not trigger GitHub Actions or change protected `main`.

The accepted M9 ARRI/DEPA processing hold, unresolved-work cap of 10,000, disabled-by-default M10 projector and null ETA remain unchanged.
