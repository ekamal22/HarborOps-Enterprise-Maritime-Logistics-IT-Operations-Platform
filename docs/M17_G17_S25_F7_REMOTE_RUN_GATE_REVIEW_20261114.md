# G17 S25 f7 review and hosted verification preflight

**Date:** 2026-11-14
**Gate disposition:** **NOT PASS for formal G17 acceptance. Acceptance pending.**
**Technical evidence under review:** local f7 on `5b1dcc41d5b19dcbacd66de38193dc5020964647`.
**Remote workflow preflight:** **BLOCKED BEFORE DISPATCH.** No new GitHub Actions run was launched by this review.
**Review limit:** The raw evidence was reviewed independently of its PASS declarations, but the exact executable source was unavailable. This record is not a separate signed reviewer attestation or a waiver.

## 1. Remote source and workflow check

The GitHub repository did not contain a reachable Git object for `5b1dcc41d5b19dcbacd66de38193dc5020964647`, and the reported local branch `feat/harborops-g17-s25-audit-6439ae` was absent. The remote `feature/m17-observability-20261112` head was `ba117f41519768008434b34464c6b2cca249649f`, and its latest hosted G17 run [#43593741977](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43593741977) failed. Re-running that SHA would not verify the later local f7 candidate.

The exact workflow `.github/workflows/verify-g17-s25.yml` allows `workflow_dispatch` and push-triggered verification for `feature/m17-observability-20261112` on selected executable and evidence-mapping paths. It runs atomic no-skip preflight before the full disposable campaign and retains failed evidence. The available connected repository operations can read job runs and rerun old jobs but cannot start an arbitrary new workflow dispatch. Triggering an obsolete run, editing documentation to masquerade as source changes or attaching a f7 PASS to an old SHA is prohibited.

The previous documentation-only commit `efde54c4f928bef12160e012d315537a4e5b82e2` cannot replace the f7 source. Protected `main` is unchanged.

## 2. Independent raw archive checks

Archive: `final-f7-evidence-5b1dcc4.zip`, SHA-256 `77c6b26f5c12337f32de2ed606b1a94f91e41971441779ed943a6825b668df34`.
Manifest SHA-256: `46df381bfd026babba3ceca34eb0b1ba6539996e50c5aa6d46de6cc277653bf1`.

Reparsed from the supplied archive: 55 ZIP members, including the manifest and 54 indexed evidence files; no missing, unindexed, wrong-hash or wrong-size files after normalizing two Windows-style hidden status paths. Source SHA matches across manifest, assertion ledger and run status. There are 49 PASS stages with zero nonzero exits. Four .NET TRX files report 319 unit, 60 contract, 55 M17 integration and 34 selected regression cases passed. Node JUnit reports 222 passing cases with no skips. The browser files report 24 dashboard cases across three engines plus one cardinality case, without skips or retries.

There are 25 scenarios and 75 unique A01-A03 assertion records. 72 are `TECHNICAL_PASS`; G17-S25-A01, A02 and A03 are `PENDING_INDEPENDENT_REVIEW`. The manifest explicitly declares `RUNTIME_CANDIDATE_NOT_REVIEWED`, `reviewed_assertion_count=0` and `gate_acceptance=NOT_ACCEPTED`. These are execution outcomes, not independently established code-source equivalence.

## 3. All 25 scenario dispositions

| Scenario | Topic | Independent disposition |
| --- | --- | --- |
| S01 | Work packages, approved policy mapping, unchanged default surface | SOURCE BLOCKED |
| S02 | Metric naming, request counters and latency observations | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S03 | Health endpoints and database outage semantics | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S04 | Server correlation and spoofing rejection | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S05 | Metric attribute privacy and stable unknown-route labels | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S06 | Cardinality saturation, coalescing and memory limits | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S07 | Trace propagation, DB linking and audit identity | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S08 | Malformed trace context and raw-header exclusion | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S09 | Database timing, classified outage and error contract | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S10 | Held worker and explicitly enabled worker behavior | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S11 | Receipt/worker trace link, forged context and retries | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S12 | Collector fail-open behavior, bounded queue and recovery | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S13 | Retained telemetry privacy, sanitized diagnostics and audit boundary | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S14 | Tenant denial and no instrumentation privilege escalation | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S15 | Cancellation, restart, lease preservation and effect-once | QUALIFIED, POSITIVE EFFECT WITNESS MISSING |
| S16 | Opt-in network isolation and safe read boundaries | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S17 | Alert trigger, window, clear and hysteresis | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S18 | Low-traffic Unknown, held-worker quietness and cooldown | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S19 | Three-engine dashboard, unavailable state and safe links | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S20 | Browser-origin fault, trace/log identity and alert | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S21 | Measured latency/memory overhead and pressure | QUALIFIED, VARIANCE DISPOSITION OPEN |
| S22 | Telemetry retention and protected history on storage exhaustion | RAW EXECUTION PASS, SOURCE UNVERIFIED |
| S23 | Accepted milestone regressions | LIMITED TO SELECTED REGRESSION GROUPS |
| S24 | Source-locked assertion mapping, manifest and tamper negatives | SOURCE BLOCKED |
| S25 | Separate review, exception closure and signed gate decision | NOT PASS |

These dispositions preserve the distinction between an internally consistent evidence packet and a complete independent review of the exact source. No scenario has received formal gate acceptance through this record.

## 4. Technical qualifications and residual findings

**F07:** The restart artifact proves a live lease preserved through shutdown and restart, 140 deliberate cancellations including 60 in-flight, and reported forged fence and duplicate-finish rejection. Its final durable counts are `2,0,2` for work items, processing effects and receipts. `effects_after=0`. It does not demonstrate a positive exactly-once committed effect. A source-reviewed test using a resolvable container, exactly one processing effect and a rejected duplicate finish is required, along with a fresh source-locked run if test code changes.

**P09 / F05:** The approved method remains three matched rounds with per-route pooled p95 and mean paired peak-memory overhead. Independent recalculation of the archived values gives -1.90% baseline p95, +9.30% authorized p95 against the 10% ceiling, and +13.7924% memory against the 15% ceiling. Third-pair p95 rises about +17.54% baseline and +11.10% authorized. The reported earlier f6 run on the same source failed P09 at roughly +12.7%; original f6 raw samples are not in this packet. f7 is a qualified technical pass, not evidence of comfortable or repeatable headroom. Do not change the budgets or discard f6.

**Source and mutations:** The claimed 637 applied mutations with 568 killed are not independently reproducible from this ZIP. The reported 33 of 57 surviving reconciliation mutations require classification against the exact source. The f7 packet is not the complete previous-milestone browser/integration, G13/G15 crash or capacity suite.

**Evidence hygiene:** Two ZIP paths use Windows separators. Seven test-report files include local worktree paths and require an explicitly labeled sanitized distribution derivative if distributed under the publication rules. One telemetry-log snapshot confuses character count with byte count; the manifest hash and actual file bytes agree. Original raw evidence stays immutable.

## 5. S25 atomic decisions

- **G17-S25-A01: NOT PASS.** Exact executable source and the negative/mutation controls cannot be inspected independently; F07 remains non-vacuous only for lease preservation, not committed effect-once.
- **G17-S25-A02: OPEN / QUALIFIED.** P09 same-source f6/f7 variance, original f6 samples and other procedural qualifications are disclosed but not formally disposed.
- **G17-S25-A03: NOT PASS for acceptance.** A separately attested signed PASS or NOT PASS decision is absent; no separation exception is presumed.

**Final recommendation: G17 NOT ACCEPTED.** This is an explicit review outcome, not a signed acceptance or an authorization to merge.

## 6. Closure and next executable run

The exact local f7 source must first be made accessible through a verifiable Git bundle or a reviewable remote branch; provide the complete f6 artifact and relevant mutation survivors. Apply and validate the positive F07 witness if it is not already present. For any executable change, freeze a new SHA, perform source and assertion preflight, then execute the whole G17 Actions workflow on that exact remote SHA, preserving its artifacts including failures. Independently parse the newly produced source-locked evidence and obtain a genuinely separate signed S25 gate decision. Do not accept a historical run, a documentation-only branch, a temporary local PASS or a missing witness as a substitute.

**This review did not launch GitHub Actions, change the executable repository, merge anything, alter performance limits or mark G17 accepted.**
