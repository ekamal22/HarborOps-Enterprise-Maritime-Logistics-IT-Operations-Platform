# G17 Final Source-Locked Local Verification Result

**Recorded:** 2026-11-16
**Branch:** `feat/harborops-g17-final-verification-60b4bf` (descends from the review branch `feat/harborops-g17-s25-audit-6439ae` at `9cbcc369883dd86a779147fc0614e4e26c202184`)
**Frozen verification source:** `7cc25dabc42b78debf6c9b200fd5209e45b0f56d`
**Campaign outcome:** attempt **k4 COMPLETED technically**: 49 of 49 stages PASS, 72 of 72 technical witnesses, 3 independent S25 assertions pending.
**G17:** **NOT ACCEPTED; acceptance pending.** This is a technical result only. It is not an independent review and does not sign any S25 assertion.

## Why a new campaign was required

The earlier PASS records apply to other sources and were not transferred: f7 to `5b1dcc41d5b19dcbacd66de38193dc5020964647`, r1c, r2 and r3 to `8d5e24137ad2943a6b2787b6472e4cea7bd9947e`. The review branch afterwards gained tests, fixtures and a runner list, and this assignment added corrections. The frozen candidate below was therefore verified from scratch. Nothing from f6, f7, r1c, r2 or r3 is used as evidence for it.

## Source identity

| Commit | Content | Changes executable product code |
| --- | --- | --- |
| `9cbcc369` | Review branch documentation head before this work | no |
| `00ab256d` | Diagnostic producer ordering fix, independent hash-list verifier, its tests, preserved previous producer as a negative-control fixture | no |
| `8d48d082` | Readiness check no longer publishes "database unavailable" for a check the caller abandoned, plus 4 regression tests | **yes, one file** |
| `6cbdb762` | Runner runs the new diagnostic tests | no |
| `56a34ee9` | Test gives the child PowerShell host its own module path (found by attempt k1) | no |
| `7cc25dab` | **Frozen candidate.** Runner installs browser test dependencies before the first stage that needs them, with an order test (found by attempt k2) | no |

Production source against earlier tested sources: relative to both `5b1dcc41` and `8d5e2413` the only product file that differs is `src/HarborOps.Api/Health/DatabaseReadinessCheck.cs` (5 insertions, 1 deletion). The Tempo search-window fix of `8d5e2413` is present as commit `1374779f` on the review branch. All other differences are tests, fixtures, the runner and documentation. The worktree was clean and `HEAD` equalled the frozen SHA for the whole of k4 (the runner enforces both and the independent check below re-confirmed it).

## Every attempt, preserved

No failed attempt was deleted, edited or rerun to obtain a better result. Source-code or runner changes were made only to correct a defect in new verification code or in the runner itself, never to change a requirement, threshold, assertion or policy.

| Attempt | Source | Result | Root cause | Disposition |
| --- | --- | --- | --- | --- |
| k1 | `6cbdb762` | FAILED after 30 s at stage `source-and-negative-controls` (4 Node test failures) | The new diagnostic producer test spawned Windows PowerShell 5.1 under the PowerShell 7 campaign runner. The inherited `PSModulePath` hid the 5.1 utility module, so `Get-FileHash` was not found. Reproduced through a pwsh-launched Node before the fix | Test-harness defect in new code. Test now removes `PSModulePath` from the child environment. No product behavior involved |
| k2 | `56a34ee9` | FAILED at stage `postgres-readiness-alert-and-durable-recovery` after 28 passing stages | The database fault stage drives a real page through `tests/browser/m17/page-fault-journey.mjs`, which imports `playwright`, while the runner installed `tests/browser` dependencies only in a later stage. Earlier local campaigns passed only because their worktree already held `node_modules` from earlier sessions. The first outage step of the same stage PASSED (readiness correction not implicated) | Latent runner ordering defect. The install is now a named stage `browser-test-dependencies` before the first stage that needs it, with a static order test that fails against the k2 runner |
| k3 | `7cc25dab` | FAILED at stage `docker-21` (compose build of the databases and migrator) after 21 passing stages | k2's stopped stack still held the fixed compose subnet `172.30.247.0/24` (the runner stops containers but does not remove networks) | Environmental. Only containers and networks of the owned projects `ho-verify-m17-final-k2` and `-k3` were removed, by exact project label. No source change. The new `browser-test-dependencies` stage PASSED here |
| **k4** | `7cc25dab` | **COMPLETE, exit 0, 49 of 49 stages PASS** | none | Final technical candidate result, below |

The k2 and k3 failures changed no accepted gate. They are listed because they show that the previous local campaigns depended on an undeclared workstation precondition, which is now removed and pinned by a test.

## Result of attempt k4

Started 2026-11-16T15:15:04Z, finished 2026-11-16T15:54:04Z (39 minutes), local runner `scripts/run-g17-local.ps1` with the disposable stack, collector fault probe, worker fixture, full-G17 reconciliation and owned cleanup, project `ho-verify-m17-final-k4`. No GitHub Actions workflow was dispatched. Run state COMPLETE, `gate_acceptance` NOT_ACCEPTED, `g17_assertion_review` NOT_REVIEWED.

| Check | Result |
| --- | --- |
| Runner stages | 49 of 49 PASS, exit code 0, no skipped or filtered stage |
| Node source and negative-control suite | 374 cases from 23 files, 0 failures, 0 errors, 0 skipped |
| .NET unit tests | 319 of 319 |
| .NET contract tests | 60 of 60 |
| M17 integration tests (includes the 4 new readiness tests) | 59 of 59 |
| Accepted-milestone regressions with M17 opted in | 34 of 34 |
| Browser dashboard witness, Chromium, Firefox and WebKit | 24 expected, 0 unexpected, 0 skipped, 0 flaky |
| Cardinality browser witness | 1 expected, 0 unexpected, 0 skipped, 0 flaky |
| Technical assertion ledger | 72 of 72 TECHNICAL_PASS, 3 PENDING_INDEPENDENT_REVIEW, 75 total, 25 scenarios |
| Evidence manifest | 54 files indexed, 54 on disk, 0 hash or size mismatches, 0 missing expected artifacts |
| P09 independent recalculation | PASS (see below) |

### Independent verification performed after the run

A separate script (written for this check, reading only) confirmed: the worktree `HEAD` equals the frozen SHA and is clean; `source-sha.txt`, the manifest and every raw report carry the same SHA and project; every one of the 54 manifest-indexed files matches its recorded size and SHA-256, and the directory contains no unindexed file; all 54 expected artifacts exist; the assertion inventory hash matches; the ledger re-derives from the raw artifacts record for record, 72 technical passes and 3 pending; all four result files show 0 failures and 0 not-executed tests; and the four new readiness regression tests, the new diagnostic tests and the new order test appear as executed and passed in the raw result files. Output: `k4-independent-verification.json` in the evidence location below.

### P09 at this source (unchanged limits: 10% pooled p95 per route, 15% mean paired peak memory)

| Measure | Value | Limit |
| --- | ---: | ---: |
| Pooled p95 increase, baseline route | +3.95% (16.96 ms to 17.63 ms) | 10% |
| Pooled p95 increase, authorized route | +8.96% (40.75 ms to 44.40 ms) | 10% |
| Mean paired peak-memory increase | +13.97% | 15% |

| Pair | Baseline route p95 | Authorized route p95 | Peak memory |
| --- | ---: | ---: | ---: |
| 1 | +2.74% | **+25.71% (individual exceedance)** | +14.45% |
| 2 | **+15.76% (individual exceedance)** | -0.52% | +13.01% |
| 3 | -2.46% | +4.80% | +14.44% |

The gate passes by its approved pooled rule. Two individual pair exceedances are disclosed here and are not waived by silence. The authorized-route margin (8.96% against 10%) and the memory margin (13.97% against 15%) are thin, as the [qualified P09 decision](M17_G17_P09_QUALIFIED_PASS_DECISION_20261116.md) already recorded. This is the sixth complete run of record: f6 FAIL, f7, r1c, r2, r3 and k4 PASS, so P09 stands at 5 of 6 passing, with the f6 FAIL and its unexplained burst preserved. **The P09 status remains QUALIFIED PASS and the pre-release optimization obligation is unchanged.** No benchmark was repeated to obtain this figure; k4 is the first run to reach the performance stage on this source.

## Evidence location and hashes

Raw evidence is retained locally, was not pushed and is not published, because it contains exported traces, service logs and raw measurement samples that a reviewer should inspect in place. The manifest records 0 binary files requiring manual review (all 54 indexed files are text or JSON).

| Item | Location or value |
| --- | --- |
| Evidence directory | `E:\HarborOps-Verification\g17-final\final-k4-evidence` (and `final-k4-evidence-disposable-stack`) |
| Evidence archive | `E:\HarborOps-Verification\g17-final\final-k4-evidence-7cc25da.zip`, SHA-256 `3d5bc1964035e00e34b768f2edeb083853625b1377ec744bbd6e4abaaa2c658b` |
| `manifest.json` SHA-256 | `84695b017a7ff4f37355e2a31cc9631de5e02f1ad5f640509551ce4f31432f50` |
| `g17-technical-assertion-ledger.json` SHA-256 | `bf431f2bd6fd017929c1e3cb3af4ebdf1c9bb4f91d4c68a02182b5454fd66023` |
| `run-status.json` SHA-256 | `febcf923b22bafafe80c5d691c9ca661a29b899f7ebbfec5229a14378fa487f8` |
| Independent verification output | `E:\HarborOps-Verification\g17-final\k4-independent-verification.json`, SHA-256 `7dd8a85a9bb860eb18b63472dfc7525788300215dc37bca0a15edd9a8ed8689c` |
| Run log | `E:\HarborOps-Verification\g17-final\final-k4-run.log` |
| Failed attempts | `final-k1-evidence`, `final-k2-evidence`, `final-k3-evidence` and their `-run.log` files, untouched, in the same folder |
| Continuity record | `E:\HarborOps-Verification\g17-final\STATUS-continuity.md` |
| Launcher | `E:\HarborOps-Verification\g17-final\run-final.sh <id>` |

## What this result does and does not establish

- It establishes that the frozen source, tests, fixtures and runner pass the complete no-skip local G17 gate (25 scenarios, 72 technical assertions) and that the evidence is internally consistent and source-locked.
- It does not establish G17 acceptance. S25-F06 and the independent S25-A01, A02 and A03 assertions still require a genuinely separate reviewer, and no reviewer has signed anything. No merge, deployment or release is authorized.
- Test counts above are for this source only and replace no accepted figure for any other source.

[Correctness and test audit](M17_G17_FINAL_CORRECTNESS_AND_TEST_AUDIT_20261116.md) | [Independent S25 handoff packet](M17_G17_S25_FINAL_HANDOFF_PACKET_20261116.md) | [Diagnostic hash finding](M17_G17_DIAGNOSTIC_HASH_FINDING_20261116.md) | [Qualified P09 decision](M17_G17_P09_QUALIFIED_PASS_DECISION_20261116.md)
