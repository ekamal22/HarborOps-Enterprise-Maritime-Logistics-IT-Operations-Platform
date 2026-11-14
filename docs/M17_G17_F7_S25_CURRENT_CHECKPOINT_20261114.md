# G17 f7 technical evidence and independent S25 boundary

**Recorded:** 2026-11-14  
**Local f7 tested executable source:** `5b1dcc41d5b19dcbacd66de38193dc5020964647`  
**Remote documentation branch baseline:** `7b3d3e87a62c03ba6a7ce7d8d92b93bf7c3c78a7`  
**Local post-test documentation commit reported:** `3158985d`  
**Disposition:** **G17 NOT ACCEPTED; acceptance pending.** Technical evidence is positive but independent S25 A01, A02 and A03 remain open. [Latest hosted preflight and independent evidence verdict](M17_G17_S25_F7_REMOTE_RUN_GATE_REVIEW_20261114.md) returned NOT PASS for formal acceptance.

This document reconciles the separately supplied, corrected local f7 evidence archive with the accessible remote documentation. The tested executable source and reported post-test documentation commit remain local only, are not present in this branch, and have not been independently diffed. Do not infer that the source on the documentation branch was the source verified by f7. No code, migrations, workflows, data or accepted source branches are changed by this documentation reconciliation.

## Complete local f7 evidence

- Corrected archive: `final-f7-evidence-5b1dcc4.zip`, SHA-256 `77c6b26f5c12337f32de2ed606b1a94f91e41971441779ed943a6825b668df34`.
- Evidence manifest SHA-256: `46df381bfd026babba3ceca34eb0b1ba6539996e50c5aa6d46de6cc277653bf1`.
- 54/54 indexed raw files match manifest digests and byte counts. The archive has 55 entries including its manifest, with two Windows-style hidden Playwright filenames requiring path normalization for portable extraction.
- Run ledger: `COMPLETE`, 49/49 stages PASS, zero failed stages, exact source SHA `5b1dcc41d5b19dcbacd66de38193dc5020964647`, 72/72 technical assertions `TECHNICAL_PASS`, G17-S25-A01/A02/A03 `PENDING_INDEPENDENT_REVIEW`.
- Tests: 319 unit, 60 contract, 55 M17 integration, 34 accepted-milestone selected regressions, 222 Node, 24 dashboard browser executions (8 per engine across Chromium, Firefox and WebKit), one cardinality browser execution. No reported skip, retry or flaky result.
- The eight selected milestone regression groups do not constitute the full previous-milestone integration, earlier Playwright, or G13/G15 crash and capacity campaigns.
- Local attempts f1 through f6 remain FAILED and are not superseded for their original source-specific failure history. No GitHub Actions was used for f7.

## S25 findings after the raw-evidence review

| Finding | Evidence assessment at f7 | Formal status |
| --- | --- | --- |
| F01 | All three browser engines show Unknown, not a false-healthy zero or NaN, for insufficient 5xx/p95 traffic; 32-request positive window reported | Raw behavior substantiated; source review pending |
| F02 | 96 page-originated 503 responses; selected server correlation matched a retained trace and sanitized log, with visible firing Grafana alert | Raw behavior substantiated; source review pending |
| F03 | API and worker traces, metric series and logs retained and hashed; no obvious credentials in independent log spot-check | Negative-control source/mutation review pending |
| F04 | Both 45-byte hidden browser metadata entries present and indexed | Raw archive check closed; path separator portability disclosed |
| F05 | Approved pooled P09 rule passes f7 narrowly; f6 reportedly failed same SHA | Qualified; raw f6 comparison and variance disposition pending |
| F06 | No distinct signed reviewer decision or approved independence exception | Open |
| F07 | Real lease preserved, stale/forged completion reportedly rejected and in-flight cancellations witnessed; processing effects after restart remain 0 | Positive committed effect-once witness still pending |
| F08 | Nonempty M8/M11/M15/M16 fixtures, real telemetry ENOSPC degradation and identical protected row/schema hashes | Raw behavior substantiated; source review pending |

These assessments do not retroactively close the original eight findings on their old source revisions. The f7 independent review recommendation is NOT PASS for formal S25 acceptance because executable-source equivalence and signature are unavailable and F05/F07 remain qualified.

## P09 performance decision boundary

The originally approved rule is unchanged: three matched rounds; p95 computed from pooled request samples for each route; memory overhead assessed as mean of three paired peak increases; **10% latency and 15% memory ceilings**. The independent recalculation of f7's raw samples supports:

| Measurement | f7 result | Limit |
| --- | ---: | ---: |
| Baseline pooled p95 overhead | approximately -1.90% | 10% |
| Authorized pooled p95 overhead | approximately +9.30% | 10% |
| Mean peak-memory overhead | +13.7924% | 15% |
| Third matched pair baseline p95 | approximately +17.51% | Informational pair exceedance |
| Third matched pair authorized p95 | approximately +11.10% | Informational pair exceedance |

The f6 campaign, reportedly on the unchanged `5b1dcc41d5b19dcbacd66de38193dc5020964647`, FAILED the P09 gate at about +12.7%. Its complete raw archive was not included with f7, so the exact f6 calculation has not been independently reproduced. The qualified f7 PASS must not be described as reproducible overhead headroom. No threshold, formula or earlier result is relaxed or silently replaced.

## Source, test quality and artifact caveats

- Exact tested source `5b1dcc41d5b19dcbacd66de38193dc5020964647` is not reachable through the connected GitHub repository. Obtain a Git bundle or read-only exact source export and verify the reported post-test docs-only diff.
- The reported mutation audit has 637 operators with 568 killed and 69 surviving; the uploaded f7 archive does not contain a reviewer-reproducible mutation inventory. The reported reconciliation subset killed 24 of 57; remaining material survivors must be classified or addressed with independent negative checks.
- The f7 restart artifact reports zero committed processing effects. An additional positive F07 test must use a resolvable container and demonstrate one committed effect surviving restart, rejected stale/forged lease tokens and epochs, and rejected duplicate finalization. If source/test code changes, the original f7 evidence does not certify the changed SHA.
- Seven original raw report files contain absolute local test-worktree paths. Preserve them unmodified and explicitly label any sanitized distribution copy. A service-log snapshot's character count is not its UTF-8 byte count, while the authoritative file digest and indexed bytes match.
- The existing `scripts/check-planning-docs.mjs` is reported to contain stale acceptance-string assertions and to fail on the remote baseline. This documentation-only reconciliation does not alter that executable checker or claim it passed.

## Required next steps and operating boundaries

1. Independently review the exact executable source, generated test identities, source mutation results and local docs-only diff.
2. Preserve original f6 and f7 raw artifacts; review the f6 performance samples, pair distribution, test conditions and an explicit variance disposition under the approved unchanged P09 rule.
3. Add and validate a non-vacuous F07 positive processing effect after restart. Review relevant mutations and complete targeted local tests before another complete exact-SHA disposable campaign, if executable source changes.
4. Obtain a separate signed S25 PASS or NOT PASS recommendation for A01/A02/A03. Only an explicit acceptance decision can close G17.

A new GitHub Actions run has been explicitly requested, but source-locked preflight is blocked because the tested commit is absent from GitHub; no new hosted run has been launched. Do not alter protected main or accepted branches. G13 conditional integration, accepted G15/G16, the accepted Turkish presentation, M9 transport hold, default-off telemetry, data-history protections and the final G24 release restrictions are unchanged. M18 still requires accepted G17 and G16.

**Historical records:** [G17 prior independent review](M17_G17_S25_INDEPENDENT_EVIDENCE_REVIEW_20261114.md), [S25 findings register](M17_G17_S25_OPEN_FINDINGS_REGISTER_20261114.md), [historical hosted green result](M17_G17_TECHNICAL_VERIFICATION_GREEN_20261114.md), [repo-wide reconciliation](M17_G17_F7_DOCUMENTATION_RECONCILIATION_20261114.md).

