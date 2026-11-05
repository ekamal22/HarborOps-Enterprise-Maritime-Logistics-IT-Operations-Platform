# G16 Independent Raw-Evidence Review: Main Campaign

**Reviewed:** 2026-11-05  
**Tested source:** `e5711573839b185a2dfc7d9f2c64ce4fc18d4c13` (`feat/m16-audit-access-hardening`)  
**Scope:** Main standalone G16 source-locked campaign only. The independently executed supplemental probe and combined M9/M16 gate are outside this review.  
**Disposition:** Main campaign evidence reviewed and internally consistent. **G16 remains acceptance pending.** No authorization to merge M16 or deploy.

## Reviewed inputs and file integrity

The review used the supplied `mutations.zip`, `playwright.zip`, `trivy.zip`, `trx.zip`, `others.zip`, `evidence-manifest.sha256` and `technical-exit-code.txt`, checked as a group. The five ZIPs collectively contained 162 non-directory files and no missing or duplicate manifest paths. Every file's SHA-256 matched its corresponding manifest entry, with **162/162 matches, 0 mismatches and 0 missing files**. The SHA-256 of `evidence-manifest.sha256` was `a9a13a6abf4f6b05ca305affb020c8237f12f81ed65de7cda5337fab3fa72671`, matching the published final campaign report. The technical exit code was 0, the source identifier matched the tested revision, and **25/25 recorded runner stages had exit code 0**.

The supplied evidence is from the local E:-backed campaign at `E:\HarborOps-Verification\evidence\new-campaign\m16`. The unmodified private raw files remain outside the repository. This review records parsed counts and file integrity, not a new runtime execution or a guarantee that the test design is complete.

## Reparsed raw technical results

| Evidence | Independently observed outcome |
| --- | --- |
| Unit TRX | 205 total, 205 passed, 0 failed, 0 unexecuted |
| Contract TRX | 39 total, 39 passed, 0 failed, 0 unexecuted |
| Integration TRX, pass 1 | 252 total, 252 passed, 0 failed, 0 unexecuted |
| Integration TRX, pass 2 | 252 total, 252 passed, 0 failed, 0 unexecuted |
| Playwright Chromium | 61 expected, 0 unexpected, 0 skipped, 0 flaky, across 17 reports |
| Playwright Firefox | 61 expected, 0 unexpected, 0 skipped, 0 flaky, across 17 reports |
| Playwright WebKit | 61 expected, 0 unexpected, 0 skipped, 0 flaky, across 17 reports |
| Security mutation ledger | Clean control passed; all 13 mutants marked killed and each included an intended failed test |
| HTTP authorization results | 74 records, 74 passed |
| Database stop/restart results | 14 records, 14 passed |
| Trivy JSON | No HIGH/CRITICAL vulnerabilities reported for the three scanned images |
| Stage results | 25/25 exit 0; final technical exit 0 |

The raw results are consistent with the [published technical report](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/verify/g16-local-evidence/docs/verification/g16-corrective/04-final-report.md) and its [scenario register](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/verify/g16-local-evidence/docs/verification/g16-corrective/03-scenario-matrix.md). This does not independently reproduce the campaign or validate every assertion's adequacy.

## Evidence expressly unreviewed

**`supplement-s07-s08-s28.log`: UNREVIEWED.** This log was not among the uploaded main-campaign files and was not independently inspected or hash-verified. The published report states that a separate 6/6 final-source probe covers G16-S07 (unauthorized/invalid changes), G16-S08 (concurrent writes and API interruption), and the operator-identity portion of G16-S28. Those **6/6 results remain reported, not independently verified**. Do not mark the supplemental evidence as reviewed, do not infer independent closure of the three affected scenarios from this record, and preserve the original scenario matrix.

Other disclosed boundaries remain: the specific 1024-byte metadata CHECK lacks a dedicated negative probe; statement-time cursors do not provide a commit-order snapshot for late commits; migration uses a privileged PostgreSQL identity; and the unreleased M16 migration was edited in place. All require risk treatment as specified in the final report.

## M9 integration and acceptance boundary

M9/G9 was accepted and merged into `main` on 2026-11-05 with the mandatory transport-backlog operating restriction. The earlier M9/M16 disposable diagnostic combined M16 `e571157` with M9 `7665bc4`, not the accepted final M9 source, and reportedly passed 273 combined integration tests only after correcting hard-coded migration-count expectations. It did not execute combined Compose, browsers, outage recovery or the final source-locked G9/G16 gates. This record does not establish M16 compatibility with the accepted M9 baseline.

Before a G16 acceptance or merge decision: inspect and reconcile `supplement-s07-s08-s28.log`; close or expressly decide the disclosed evidence gaps; resolve worker-role provisioning, migration privilege/history and M9 worker-effect audit policies; verify the integrated source with accepted M9; and obtain explicit G16 acceptance and separate merge authorization. The dependency graph remains unchanged.

**Review status: main 162-file campaign reviewed; supplemental probe UNREVIEWED; combined M9 verification incomplete; G16 acceptance pending.**
