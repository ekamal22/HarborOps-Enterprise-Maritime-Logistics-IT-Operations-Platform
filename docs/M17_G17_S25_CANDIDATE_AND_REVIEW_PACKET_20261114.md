# G17-S25 Remediation Candidate and Separate Review Packet

**G17 current status (2026-11-15), local-only:** An independent review of source `5b1dcc41d5b19dcbacd66de38193dc5020964647` supported the technical evidence and left two issues, both now answered without changing any product code, verifier, budget or method. (1) The [P09 variance assessment](M17_G17_S25_P09_VARIANCE_ASSESSMENT_20261115.md) compares f6 and f7, which ran identical source: the f6 failure comes from one telemetry-on row with a transient latency tail (without that row the pooled authorized increase would be -17.1 percent, a sensitivity figure only and not a result), the f7 pass at +9.30 percent has a 95 percent block-bootstrap interval of +3.05 to +14.89, so the margin is thin and both runs stay on record; the recommended three additional complete measurements were then run (2026-11-15) and all passed (authorized pooled p95 +0.76, -0.67 and +2.55 percent; memory +12.53, +13.35 and +13.51 percent), so P09 passed in 4 of 5 complete runs of record and f6 stays on record; the f6 burst is not explained and memory, not latency, is now the thin margin (see the [controlled P09 results](M17_G17_S25_P09_CONTROLLED_VERIFICATION_RESULTS_20261115.md)). (2) The [mutation survivor classification](M17_G17_S25_MUTATION_SURVIVOR_CLASSIFICATION_20261115.md) shows that the 69 reported survivors came from a campaign whose manifest sample was drawn in a worktree carrying a leaked mutation, and that 168 manifest mutations had never been tried; the final strict exhaustive campaign tried all 805 candidates (790 killed, 4 killed at module load, 11 survived, 0 invalid). Of 120 distinct survivors seen in any campaign, 108 were material and are corrected by new negative tests, 2 are equivalent, 1 unreachable, 8 redundant and 1 is a retired identity mutant; none of the 11 that remain can change a verifier decision. Verification: the 21-file G17 Node suite passes (357 cases, none skipped), the f7 ledger re-derives byte for byte and the f7 manifest verifies in place. The f7 evidence remains evidence for source `5b1dcc41`. Later commits changed tests, fixtures, the runner's test list and documentation, plus one verification-stage fix: the Tempo search window of `check-g17-malformed-export.mjs`, found when a controlled-run attempt aborted on a quiet host (the stage could collapse to a zero-width window and Tempo answered 400). The three controlled runs are complete 49-stage campaigns at source `8d5e2413` (`5b1dcc41` plus that fix). The unlisted archive found in the f7 evidence directory was created by the repository owner and was moved, not deleted. No hosted workflow was used. **G17 remains NOT ACCEPTED:** S25-F06 and the independent S25 A01, A02 and A03 still require a genuinely separate reviewer and a signed PASS or NOT PASS.

**G17 f7 campaign status (2026-11-14, local-only; still accurate for source `5b1dcc41`, extended by the 2026-11-15 status above):** The complete no-skip G17 campaign **PASSED technically on attempt f7** at exact source `5b1dcc41d5b19dcbacd66de38193dc5020964647`: 49 of 49 stages, 72 of 72 technical witnesses, 3 independent S25 clauses pending, a 54-file hash-verified manifest, unit 319, contract 60, M17 integration 55, accepted regressions 34 and 24 plus 1 Playwright cases, no skips. It followed six failed attempts (f1 to f6), all preserved; f6 on the same source failed the P09 overhead gate at +12.7 percent authorized pooled p95, and f7 passed it at +9.3 percent with a pair-3 individual outlier of +17.54 percent disclosed, so the margin is thin. All eight S25 findings and sixteen newly found gaps are registered in the [gap closure and test audit](M17_G17_S25_GAP_CLOSURE_AND_TEST_AUDIT_20261114.md); the evidence is in the [local verification result](M17_G17_S25_LOCAL_VERIFICATION_RESULT_20261114.md). No hosted workflow was used and nothing was pushed. **G17 NOT ACCEPTED:** S25-F06 and the independent S25 A01, A02 and A03 still require a genuinely separate reviewer and a signed PASS or NOT PASS.

**Historical G17-S25 status, November 14, 2026 (hosted run #43589790235, superseded):** [Hosted run #43589790235](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43589790235) finished **FAILED** at executable SHA `e238a48013948f28c8ab5042583e6e6bfaaa4553`. Exact-source preflight, 319 unit, 60 contract, 55 M17 integration and 34 accepted regression tests passed without required skips. Prometheus exposed a firing `HarborOpsDatabaseUnavailable` alert, but the browser did not render its label in the local Grafana alert table. The feature-branch correction sets the panel target to table format, narrows the browser witness to the actual alert panel, and adds fail-closed configuration and raw-evidence negative checks. This new candidate has **not** passed a complete hosted verification. The earlier green run #43541279851 is evidence for its own older source only. All S25-F01 through F08 remain open until new raw evidence is reviewed, and F06 requires a separate signed reviewer decision. Protected `main`, the M9 transport hold and accepted data protections are unchanged. **G17 NOT ACCEPTED; acceptance pending.**

**G17-S25 rerun checkpoint (November 14, 2026):** [#43589790235](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43589790235) at exact executable SHA `e238a48013948f28c8ab5042583e6e6bfaaa4553` passed the no-skip source and assertion preflight and entered its full disposable campaign. This was the in-progress checkpoint; the run later completed FAILED, as recorded above. The preceding [#43589093044](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43589093044) failed only the newly added S15 lease-restart integration fixture due to missing zero event/link export limits; that fixture and the uppercase S22 shipment-code fixture were corrected. Eight S25 findings are tracked; seven have implemented candidate controls awaiting exact-source runtime proof, and F06 still requires a separate signed review. Historical evidence is retained, protected `main` unchanged, **G17 NOT ACCEPTED; acceptance pending**.

**Remediation verification checkpoint, November 14, 2026:** [#43589093044](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43589093044) FAILED the new positive S15 lease-restart integration test after 319 unit, 60 contract and 54 other integration tests passed without skips. The new test launched opted-in telemetry without the required zero span-event and link limits; the existing privacy guard correctly rejected the host. Source corrections now set and restore both limits within the isolated test and use an uppercase-only synthetic shipment reference for the S22 database fixture. These changes need a fresh full campaign. Historical failure artifact `g17-raw-evidence-43589093044-1` is retained. **G17 NOT ACCEPTED; separate S25 sign-off remains pending.**

**Date:** November 14, 2026  
**Technical baseline:** GitHub Actions [#43541279851](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43541279851), executable source `f174eae6b2077937c7d43a8b1f7c34ecdab9b663`, technical PASS, independent S25 NOT PASS.  
**Candidate:** New source-locked G17 feature branch descendant; new full campaign still pending at the time of this record.  
**Gate:** G17 NOT ACCEPTED. Acceptance pending.

## Corrective source and required evidence

| Finding | Implemented source | Fail-closed requirement |
| --- | --- | --- |
| F01 | Three-engine browser test records actual no-data states for API request rate, 5xx and p95, with per-engine raw JSON | No report consisting only of panel titles or saved noValue fields passes |
| F02 | Real page-originated HTTP 503 injection during the actual owned PostgreSQL outage; retains server correlation, source trace ID, sanitized correlated log, and rendered database alert | No unrelated trace, mismatched correlation, unrendered alert, or browser API-client-only claim passes |
| F03 | Participating worker logs retained and scanned after real worker receipt processing | Private synthetic canary or missing positive worker log fails |
| F04 | Artifact upload explicitly includes hidden metadata, but the manifest scans the complete owned evidence directory first | Original Playwright last-run metadata must be delivered and hash-verified |
| F05 | A second script reconstructs all P09 per-pair, pooled p95 and memory numbers from retained raw samples and discloses individual exceedances | Numeric budgets remain 10 percent p95 and 15 percent mean peak memory; no silent waiver or rewritten samples |
| F07 | M17 integration test creates and claims a real leased receipt, restarts the API host, and checks exact lease token, epoch, state, attempt count and absence of duplicate effects | The source-locked integration TRX must show this specific method passed |
| F08 | Owned storage-fault test seeds nonempty M8/M11/M15/M16 synthetic history and records actual ENOSPC plus whole-public-schema data-only before/after SHA-256 | Missing protected fixture row, no real write rejection, or different history hash fails |

## Independent review requirement (F06)

The implementation work cannot certify itself as independent. The following review fields intentionally remain incomplete until a distinct authorized reviewer inspects the new raw source-locked evidence.

| Independent assertion | Requirement | Disposition |
| --- | --- | --- |
| G17-S25-A01 | Review source, exact raw artifacts, all 75 clauses, security and browser behaviors separately from the remediation | PENDING |
| G17-S25-A02 | Record explicit dispositions for all eight findings and the P09/packaging procedural qualifications | PENDING |
| G17-S25-A03 | Make a signed explicit PASS or NOT PASS recommendation, before any acceptance | PENDING |

**Reviewer:** Acceptance pending  
**Source SHA reviewed:** Acceptance pending  
**Evidence run ID and artifact hash:** Acceptance pending  
**Signed recommendation and date:** Acceptance pending

The separate review may record NOT PASS if any required witness is absent, skipped, uninspectable, or only inferred from another test. A passing Actions campaign is necessary but not sufficient.

## Boundaries

Keep original local c7, the green #43541279851 archive, and every failed hosted attempt intact. Do not modify protected `main` or accepted G13/G15/G16/Turkish behavior. The M9 transport hold and M15 retained-data and authorization protections stay intact. Source changes are limited to the M17 G17 technical witness and source-locked evidence path. No production activation is authorized.


## First hosted remediation attempt and corrections

- [#43589093044](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43589093044) was pinned to `773c65e2239d2fc238bc29b995f5ed6c8952733c`. Exact-source preflight PASS, .NET build PASS, 319/319 unit PASS, 60/60 contract PASS, 54/55 integration PASS. No required skips. The one failing method was `M17RestartLeaseWitnessTests.ApiRestartPreservesActiveWorkerLeaseAndNoDuplicateEffects`, rejected before fixture creation by `M17TracePrivacyGuard.RequireSafeExporterLimits` because event/link export limits were not zero. This is a correctly enforced guard, not a reason to weaken production safety policy.
- The test now requires `OTEL_SPAN_EVENT_COUNT_LIMIT=0` and `OTEL_SPAN_LINK_COUNT_LIMIT=0` for both API host lifetimes, restores their prior values in `finally`, and joins the existing worker-isolation test collection.
- A source audit also identified that a random lowercase hexadecimal suffix violated the shipment reference-code CHECK expression in the later S22 fixture. The synthetic shipment reference is now uppercase, while the separate tenant slug stays lowercase.
- The original run and artifact remain unchanged. A new exact-source campaign must complete before any finding is marked closed.

## Run #43589790235 failure and strengthened witness

Run #43589790235 at executable SHA `e238a48013948f28c8ab5042583e6e6bfaaa4553` passed the source preflight, 319 unit tests, 60 contract tests, 55 M17 integration tests and 34 accepted regressions. The real PostgreSQL outage generated 120 controlled 503 responses. The retained database-fault artifact confirms both the API failure-rate alert and `HarborOpsDatabaseUnavailable` were firing in Prometheus. The browser observer failed with `Live local alert name must be rendered in the real browser dashboard`. PostgreSQL recovery and the subsequent alert clear passed. The full campaign failed and no later stage may be inferred to pass.

Corrective source configures the active-alert panel's Prometheus query as `format: table` while preserving its precise firing-alert expression. The browser observer now requires matching saved panel ID, type, query and format, and extracts the alert label only from the bounded local panel subtree. The raw S25 evidence verifier rejects a different panel format and requires explicit panel-scoped evidence. Browser and source-boundary tests additionally enforce the table configuration. This correction is a source candidate, **not** executed proof.

A complete post-correction disposable run and independent inspection must determine whether F02 can close. F01, F03, F04, F05, F07 and F08 also require new source-locked raw review. F06 still requires a genuinely separate reviewer and signed PASS or NOT PASS. All 25 scenarios, 75 atomic assertions, raw artifacts, negative controls and no-skip requirements remain mandatory.

## Worker-log negative control audit

The S25 raw-evidence verifier contained a double-escaped regular expression for long Bearer and Password values. The old negative control altered raw worker-log bytes without adjusting the declared digest, so an integrity error could mask a broken privacy matcher. The correction uses actual word-boundary and whitespace regex operators and a controlled negative case that updates the hash and byte count to match the injected secret. Acceptance requires the raw verifier to reject the leak itself, even when the manifest and report hashes match. This is source implemented, not yet an executed hosted result. F03 remains open until the new campaign and separate review.

## F08 data-history audit correction

The disposable pressure fixture now seeds one valid source-scoped M8 receipt, accepted event and future-available work item, in addition to M11 exception history, M15 outbox and M16 audit records. This removes the gap where an empty M8 data set could be reported as protected. The raw-evidence verifier requires all seven protected table counts to be positive, rejects empty M8 controls, and compares the complete PostgreSQL public-schema hash across the storage fault. The new proof remains unverified until the complete campaign runs.

## Local campaign f7: what the independent reviewer should inspect

- Source `5b1dcc41d5b19dcbacd66de38193dc5020964647`, evidence `E:\HarborOps-Verification\g17-s25-audit\final-f7-evidence`, manifest SHA-256 `46df381bfd026babba3ceca34eb0b1ba6539996e50c5aa6d46de6cc277653bf1`, archive `final-f7-evidence-5b1dcc4.zip` SHA-256 `77c6b26f5c12337f32de2ed606b1a94f91e41971441779ed943a6825b668df34`.
- Attempts f1 to f6 failed and are preserved unmodified next to it; the table of causes is in the [local verification result](M17_G17_S25_LOCAL_VERIFICATION_RESULT_20261114.md).
- The P09 margin is thin and varies between runs of the same source. Disclosed qualifications are listed in the [gap closure and test audit](M17_G17_S25_GAP_CLOSURE_AND_TEST_AUDIT_20261114.md).
- The three independent assertions stay PENDING and the reviewer fields above stay incomplete until a distinct reviewer with source and raw evidence access signs.

## Post-review additions (2026-11-15): what the independent reviewer should inspect

- [P09 variance assessment](M17_G17_S25_P09_VARIANCE_ASSESSMENT_20261115.md): f6 and f7 raw evidence is preserved with hash lists (`preserved-20261115\f6-evidence.sha256`, `f7-evidence.sha256`) beside the evidence folders; the analysis can be reproduced from the two `m17-performance-raw.json` files with `p09-variance.mjs`. Decide whether additional controlled P09 runs are required before unconditional acceptance.
- [Mutation survivor classification](M17_G17_S25_MUTATION_SURVIVOR_CLASSIFICATION_20261115.md) and the data in `docs/m17-s25-mutation-evidence-20261115/`: check the eleven residual survivors and their executable probes in `survivor-evidence/`, and the retired original manifest figure.
- The tested source is still `5b1dcc41`. The commits above it change tests, fixtures, the runner's Node test list and documentation only, which can be confirmed with `git diff --name-status 5b1dcc41..HEAD`.
- The three independent assertions remain PENDING. Nothing in this packet is a signed review.
