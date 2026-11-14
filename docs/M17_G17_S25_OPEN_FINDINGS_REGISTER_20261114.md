# G17-S25 Open Findings Register

**Recorded:** November 14, 2026  
**Basis:** [Independent evidence review](M17_G17_S25_INDEPENDENT_EVIDENCE_REVIEW_20261114.md), [verified hosted run #43541279851](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43541279851), source `f174eae6b2077937c7d43a8b1f7c34ecdab9b663`  
**Status:** OPEN. Logging is not remediation, approval, or a claim of a new test run.  
**Gate:** G17 NOT ACCEPTED; acceptance pending. Independent S25 A01, A02 and A03 remain open.  
**Scope:** 25 scenarios and 75 atomic assertions remain mandatory. 72 technical witnesses were reported PASS by the hosted campaign, subject to independent behavioral review. No skips, weakened thresholds, release, or main merge are authorized by this register.

## Required remediation and evidence

| ID | Severity | Scenario / assertions | Finding and required closure | State |
| --- | --- | --- | --- | --- |
| S25-F01 | Major | G17-S18-A01, related S19-A01/A02 | Prove actual rendered **API request rate, 5xx, and p95** behavior for no-sample and below-threshold real traffic in Chromium, Firefox and WebKit, not only Grafana noValue JSON or Prometheus queries. Capture exact per-engine, per-panel displayed states and source-tied sample-count/window controls. Test that a false healthy zero is rejected. | OPEN |
| S25-F02 | Major | G17-S20-A01/A02/A03 | Create one controlled **page-originated** fault and independently reconcile the same server-generated correlation through HTTP error, sanitized diagnostic trace or approved asynchronous bridge, controlled metric/alert, retained log and actual dashboard or diagnostic state. Retain a scrubbed visual/browser witness or equivalent bounded DOM record. Deny forged correlation/tenant context. Do not combine unrelated passing incidents as one causal journey. | OPEN |
| S25-F03 | Major | G17-S05-A02, G17-S13-A01, related S14-A02 | Capture and scan source-locked logs from participating optional worker and other owned disposable services, not only API/collector/Tempo. Include a positive participating-service log and a negative canary injection control that fails closed, and preserve original inspected log bytes or safely bounded raw records in the hashed evidence package. Never include real credentials. | OPEN |
| S25-F04 | Moderate | G17-S24-A02, S25-A02 | Resolve the two manifest-indexed Playwright `.last-run.json` records omitted by artifact upload: upload original hidden files using a supported source-locked procedure, or explicitly exclude unrequired metadata from the manifest and document the prior discrepancy. Preserve the old artifact unchanged. | OPEN |
| S25-F05 | Qualification | G17-S21-A01/A02, S25-A02 | Retain and independently recalculate every P09 matched pair, the three-round pooled route p95, mean paired peak memory and absolute baseline. Explicitly disclose third-pair baseline p95 **+22.69%**, the approved pooled evaluation rule and its variance rationale. If the policy is unclear, request explicit decision rather than silently alter the 10% latency or 15% memory budgets. | REVIEW OPEN |
| S25-F06 | Procedural gate | G17-S25-A01/A02/A03 | Obtain a genuinely separate reviewer, with source and raw evidence access, who independently signs the acceptance recommendation, or record and explicitly approve any reviewer-independence exception. A technically green Actions run, this register, or a review by a remediation contributor cannot substitute for separate signed acceptance. | OPEN |

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
