# G17-S25 Open Findings Register

**Recorded:** November 14, 2026  
**Basis:** [Independent evidence review](M17_G17_S25_INDEPENDENT_EVIDENCE_REVIEW_20261114.md), [verified hosted run #43541279851](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43541279851), source `f174eae6b2077937c7d43a8b1f7c34ecdab9b663`  
**Status:** Technical remediation is SOURCE-IMPLEMENTED but unverified by a new full campaign. Findings remain open until actual evidence review. Separate reviewer sign-off remains pending.  
**Gate:** G17 NOT ACCEPTED; acceptance pending. Independent S25 A01, A02 and A03 remain open.  
**Scope:** 25 scenarios and 75 atomic assertions remain mandatory. 72 technical witnesses were reported PASS by the hosted campaign, subject to independent behavioral review. No skips, weakened thresholds, release, or main merge are authorized by this register.

## Required remediation and evidence

| ID | Severity | Scenario / assertions | Finding and required closure | State |
| --- | --- | --- | --- | --- |
| S25-F01 | Major | G17-S18-A01, related S19-A01/A02 | Prove actual rendered **API request rate, 5xx, and p95** behavior for no-sample and below-threshold real traffic in Chromium, Firefox and WebKit, not only Grafana noValue JSON or Prometheus queries. Capture exact per-engine, per-panel displayed states and source-tied sample-count/window controls. Test that a false healthy zero is rejected. | IMPLEMENTED_UNVERIFIED |
| S25-F02 | Major | G17-S20-A01/A02/A03 | Create one controlled **page-originated** fault and independently reconcile the same server-generated correlation through HTTP error, sanitized diagnostic trace or approved asynchronous bridge, controlled metric/alert, retained log and actual dashboard or diagnostic state. Retain a scrubbed visual/browser witness or equivalent bounded DOM record. Deny forged correlation/tenant context. Do not combine unrelated passing incidents as one causal journey. | IMPLEMENTED_UNVERIFIED |
| S25-F03 | Major | G17-S05-A02, G17-S13-A01, related S14-A02 | Capture and scan source-locked logs from participating optional worker and other owned disposable services, not only API/collector/Tempo. Include a positive participating-service log and a negative canary injection control that fails closed, and preserve original inspected log bytes or safely bounded raw records in the hashed evidence package. Never include real credentials. | IMPLEMENTED_UNVERIFIED |
| S25-F04 | Moderate | G17-S24-A02, S25-A02 | Resolve the two manifest-indexed Playwright `.last-run.json` records omitted by artifact upload: upload original hidden files using a supported source-locked procedure, or explicitly exclude unrequired metadata from the manifest and document the prior discrepancy. Preserve the old artifact unchanged. | IMPLEMENTED_UNVERIFIED |
| S25-F05 | Qualification | G17-S21-A01/A02, S25-A02 | Retain and independently recalculate every P09 matched pair, the three-round pooled route p95, mean paired peak memory and absolute baseline. Explicitly disclose third-pair baseline p95 **+22.69%**, the approved pooled evaluation rule and its variance rationale. If the policy is unclear, request explicit decision rather than silently alter the 10% latency or 15% memory budgets. | RAW_RECALCULATION_PENDING |
| S25-F06 | Procedural gate | G17-S25-A01/A02/A03 | Obtain a genuinely separate reviewer, with source and raw evidence access, who independently signs the acceptance recommendation, or record and explicitly approve any reviewer-independence exception. A technically green Actions run, this register, or a review by a remediation contributor cannot substitute for separate signed acceptance. | SEPARATE_REVIEW_PENDING |
| S25-F07 | Major | G17-S15-A01/A03 | Restart/cancellation witness observes 80 canceled **read** requests, compares `tracking_inbox_work_items`, `tracking_processing_effects`, and `tracking_inbox_receipts` **counts**, and inspects unrelated serialized spans after shutdown. Retained baseline and recovery counts are `0,0,0`; no active worker lease, specific leased receipt or correlated canceled request is positively controlled. Seed an owned lease and controlled in-flight/canceled work, preserve its identity/state before, during and after restart, check no changed lease/duplicate effect, and correlate cancellation to absence of unsafe late telemetry. | IMPLEMENTED_UNVERIFIED |
| S25-F08 | Major | G17-S22-A02/A03 | Full telemetry-store test confirms an owned tmpfs ENOSPC, healthy API responses and matching before/after `pg_dump --data-only` hashes, but does not assert an actual **degraded observation** signal during the fault or prove representative nonempty M8/M11/M15/M16 retained records before it. Seed owned representative history, record positive row identities/hashes and telemetry degradation, inject ENOSPC, then prove those exact records persist byte-for-byte and business transactions remain safe. | IMPLEMENTED_UNVERIFIED |

## Second-pass evidence, November 14, 2026

The original 33 independently executed raw-evidence controls were rerun: **31 PASS, 2 qualified exceptions** (the original two hidden metadata files not uploaded, and the approved P09 pooled gate containing one individual +22.69% p95 pair). The exact-source assertion inventory and source scripts were checked again against the retained completed campaign. Two additional behavioral gaps emerged:

- **F07:** `scripts/check-g17-restart.mjs` records `durable_counts: 0,0,0` before and after restart. Its SQL footprint compares only three counts and there is no active worker lease or cancellation-specific trace identity. The assertion inventory still explicitly requires no changed worker lease in S15-A03.
- **F08:** `scripts/check-g17-storage.mjs` records ENOSPC plus healthy business endpoints and matching whole-public-schema `pg_dump` hashes. It does not record any positive seeded M8/M11/M15/M16 row identity or a measured degraded telemetry state during ENOSPC. The assertion inventory requires safe degraded observation and preserved retained history under S22-A02/A03.

These are proof-coverage gaps, not confirmed application defects. The second pass did not establish new source, integrity, skipped-test, cardinality, tenant-boundary, or unexpected acceptance failures outside F01-F08. An absence of further findings in the inspected scope is not proof that no other issue exists.

## Closure protocol

1. Map every new behavioral witness to the exact atomic assertion and expected outcome; test the negative controls against plausible false passes. Avoid a PASS derived merely from the presence of a named test or report.
2. Reconcile the exact source, run ID, test case identities, metrics, spans, logs, browser results and raw artifact hashes. Preserve every historical failed run and the green baseline; never rewrite its original manifest or ledger.
3. On a changed executable source, run complete no-skip source preflight and the entire disposable G17 campaign again. Record every failed attempt and its correction. An updated test alone cannot make the old source-locked evidence prove the new assertion.
4. Independently review the source and actual raw evidence again, explicitly decide each open finding and disclose any exception.
5. Leave independent S25 A01/A02/A03 pending and G17 NOT ACCEPTED until a separate recorded acceptance decision. Protected `main`, accepted G13/G15/G16/Turkish behavior, M9 transport hold and M15 retention boundaries remain unchanged.

## Evidence and history

- Green technical run: [#43541279851](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43541279851), 47/47 stages PASS and 72/72 technical witnesses reported PASS. This is a technical baseline only.
- Prior independent S25 assessment: [NOT PASS](M17_G17_S25_INDEPENDENT_EVIDENCE_REVIEW_20261114.md).
- Prior failed and local campaigns: [G17 verification recovery](M17_G17_VERIFICATION_RECOVERY_20261113.md) and [c7 result](M17_G17_LOCAL_VERIFICATION_RESULT_20261114.md).
- Current evidence report: [Technical result](M17_G17_TECHNICAL_VERIFICATION_GREEN_20261114.md).


## Remediation candidate checkpoint, November 14, 2026

F01-F05 and F07-F08 have changed source or supplemental verification as mapped in [the candidate and reviewer packet](M17_G17_S25_CANDIDATE_AND_REVIEW_PACKET_20261114.md). Source syntax and synthetic negative controls were checked. The required full source-locked GitHub Actions run and independent review are still outstanding. F06 cannot be closed or self-signed by the implementation work. No original evidence or failed run has been modified.
