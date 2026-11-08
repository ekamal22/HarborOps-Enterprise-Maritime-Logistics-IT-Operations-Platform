# M11/M12 Local Closure Results

**Independent archive review, 2026-11-08:** [G11](M11_G11_INDEPENDENT_EVIDENCE_REVIEW_20261108.md) and [G12](M12_G12_INDEPENDENT_EVIDENCE_REVIEW_20261108.md) independently reconciled the manifest, raw tests and measured plans. The archive is internally consistent, but **both S25 reviews are NOT PASS** because required acceptance scenarios remain open. The previous recommendations below were made before this separate evidence review.

**Date:** 2026-11-08
**Branch:** `feat/m11-m12-combined-integration`
**Frozen hosted application SHA:** `f56d017d9b2619be56fe533f73ba1d11f63974c0`
**Local verification SHAs:** `5dedbb70b4d2f4088f117d905e90d59f23a26915` (export retry fix plus focused tests, final run) and `3d450ac320b00538b106f2ad6d4b86c88661d7a4` (test-only supplement)
**Accepted baseline:** protected `main` at `6ead1d076946dd96b53401265a31efc35c4a666c`, unchanged
**Status:** Technical evidence prepared for independent S25 review. **G11 and G12 acceptance pending.** No merge, deployment, release or production migration occurred.

This record executes the targeted plan in the [local closure runbook](M11_M12_LOCAL_CLOSURE_RUNBOOK_20261108.md). It does not repeat the hosted 341-case suite. Scenario dispositions are in the [G11 matrix](M11_G11_EVIDENCE_MATRIX_20261108.md) and [G12 matrix](M12_G12_EVIDENCE_MATRIX_20261108.md). The machine-readable index is [M11_M12_LOCAL_EVIDENCE_INDEX_20261108.json](M11_M12_LOCAL_EVIDENCE_INDEX_20261108.json).

## What was executed

Disposable PostgreSQL 16.15 servers (two, one exclusive for role-changing tests) and the .NET 10.0.403 SDK container, with all caches, databases and evidence on E:. Source identity, working-tree cleanliness (empty before and after), container image ids, server versions, SQL grants and the hashes of migrations and scripts are recorded in `final/env/`.

| Stage | Result | Raw report |
| --- | --- | --- |
| Release build, warnings as errors, no incremental | 0 warnings, 0 errors | `final/logs/build.log` |
| Unit tests | 291/291 | `final/raw/unit.trx` |
| Contract tests | 46/46 | `final/raw/contract.trx` |
| Existing M11 runtime (19) plus populated 13-to-15 upgrade (1) | 20/20 | `final/raw/g11-existing-and-upgrade.trx` |
| Existing G12 search/export (4) plus new G11 residual (13) plus new G12 residual (23) | 40/40 | `final/raw/g12-existing-and-residual.trx` |
| Measured query plans (1 test, 23 plans) | 1/1 | `final/raw/g12-query-plans.trx`, `final/plans/` |
| Amended G11 residual class at `3d450ac` (15 tests) | 15/15 | `final-supplement/raw/g11-residual-class.trx` |

The hosted browser, mutation and scan campaigns were not rerun locally. The full 341-case suite was not rerun on the final SHA (see Outstanding).

## Defect found and fixed: F-M12-01

**Symptom.** An authorized export could fail with 503, and a caller whose access was revoked while the export was in flight received 503 instead of 403 or 404. The failure was closed: no CSV bytes and no audit row.

**Root cause.** The export runs at REPEATABLE READ and locks the caller's session row. Every authenticated request updates that row (last-seen and idle expiry). A concurrent request from the same session, or a revocation, therefore raised PostgreSQL SQLSTATE 40001 (`could not serialize access due to concurrent update`) when the scope check locked the row. The EF Core provider wraps that error in an `InvalidOperationException`, and the API reports database failures as 503.

**Evidence.** Seven of eight export revocation cases and the per-instance test failed: `logs/30-g12-residual-run1-DEFECT-...log` and `raw/g12-residual-run1-defect-before-fix.trx`. A first fix that caught only a bare `PostgresException` did not take effect (`logs/31-...FIX-INSUFFICIENT...log`); the host log showed the wrapped exception (`logs/32-debug-...log`).

**Fix.** `ShipmentSearchStore.ExportAsync` retries up to three attempts on a fresh snapshot when any exception in the chain is SQLSTATE 40001, inside the one existing 25 second budget so the 30 second proxy envelope is unchanged. Nothing is committed before the final audit call, so a retry cannot duplicate an audit. A genuine revocation is then reported as 403 or 404. Change size: 35 lines in one file. **After the fix** all 23 G12 residual tests passed and the final run is 40/40.

## Other findings and observations (no defect unless stated)

1. **Free-text search scales with the whole table for large tenants.** For the largest tenant (70% of 170,000 rows) the plan is a parallel sequential scan of the entire shipments heap (4,974 buffers, 32 to 49 ms). The small tenant uses an index and 300 buffers. The leading-wildcard `LIKE` cannot use the existing btree indexes. Acceptable at the tested size; a trigram index would be the remediation if text search must scale to millions of rows. Not changed.
2. **The 10 MiB export limit cannot be reached over HTTP.** Field limits (32, 80 and 160 characters) bound a worst-case 5,000-row export to 4,870,215 bytes (46% of the limit, measured). The limit is enforced and verified at the SQL function (10,485,760 accepted, 10,485,761 refused) and remains defense in depth.
3. **The M11 evaluator is an opt-in hosted service** (`M11:EnableEvaluator`, default off). The recovery test enables it explicitly. Its due scan pages 25 shipments per 200 ms when nothing is due, so one sweep takes about N/125 seconds for N shipments (computed from the code, not measured).
4. **Export lease and rate limit are per API instance** (documented M12-P10). Two instances admit two concurrent exports for one principal and 10 attempts per minute each. There is no cross-instance aggregate limit.
5. **The export audit proves preparation, not receipt** (documented M12-P12). A client that discards the body after the headers still leaves exactly one prepared audit.
6. **Estimate versus actual ratios** in the plan summary are dominated by `LIMIT` early termination and are not a planner-quality signal. Raw plans are retained for reviewers.
7. **Limitations of the simulations.** The clock advance in the recovery test is simulated by moving the schedule and checkpoint into the past. Database outage is simulated by disabling the runtime login and terminating backends, and crashes by terminating backends at 14 instants per run (final run: 4 rolled back, 10 already complete; supplement: 2 and 12). The window where a commit succeeds and the reply is lost was not observed. No OS process kill, container stop or real nginx proxy path was run, and the in-memory test server cannot interrupt a download mid-body.

## G12-S20 measured plans

170,000 shipments over three tenants (120,000, 40,000 and 10,000) with 40 ports and 150 voyages each, 60% voyage-assigned, 8% archived, planner statistics refreshed. Requests went through the restricted `harborops_api` login; the exact SQL emitted by the API was captured with an EF interceptor and replayed read-only under that role with `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)`, cold and warm. Criteria were fixed before the first measured run: search statements under 1,000 ms, export statements under 3,000 ms, small-tenant statements without a sequential scan and under 2,500 buffers, and the exactly-5,000 export returning 5,001 CSV lines with one audit.

| Case | Rows | Warm ms | Buffers | Plan |
| --- | ---: | ---: | ---: | --- |
| Text, unique reference (big) | 1 | 32.4 | 4,974 | parallel Seq Scan, sort |
| Text, broad, first page | 26 | 44.7 | 4,974 | parallel Seq Scan, sort |
| Text, 5,000 matches | 26 | 36.2 | 4,974 | parallel Seq Scan, sort |
| Text, no match | 0 | 33.6 | 4,974 | parallel Seq Scan, sort |
| Workflow status | 26 | 0.11 | 29 | Index Scan `IX_..._WorkflowStatus_Id` |
| Record status Archived | 26 | 0.07 | 29 | Index Scan `IX_..._RecordStatus_Id` |
| Record status All, limit 100 | 101 | 0.15 | 105 | Index Scan `AK_..._Id` |
| Origin and destination ports | 26 | 2.5 | 3,004 | Index Scan origin, sort |
| Voyage assignment | 26 | 3.3 | 5,835 | merge join with `IX_m6_voyage_shipment` |
| Departure UTC window | 26 | 0.44 | 601 | Index Scan |
| Arrival UTC window | 26 | 0.47 | 430 | Index Scan |
| Combined status, port, window | 24 | 2.3 | 3,004 | Index Scan origin, sort |
| Keyset page 2 after cursor | 26 | 0.07 | 29 | Index Scan |
| Keyset 60,000 rows deep | 26 | 0.12 | 30 | Index Scan |
| Small tenant: text, status, no match | 26, 26, 0 | 8.8, 0.07, 6.1 | 300, 29, 300 | index and bitmap, no Seq Scan |
| Export, text matches (4,600 active) | 4,600 | 49.3 | 4,974 | parallel Seq Scan |
| Export, exactly 5,000 (all statuses) | 5,000 | 35.6 | 4,974 | parallel Seq Scan |
| Export, 5,001-row refusal (Draft) | 5,001 | 3.9 | 5,043 | Index Scan |
| Export, ports / window+status / small tenant | 158 / 1,480 / 300 | 2.5 / 7.8 / 0.21 | 3,004 / 2,553 / 305 | Index Scan, sort |

All declared criteria were met. Raw JSON for every statement, the SQL text with parameters and the dataset inventory (index sizes, server settings) are in `final/plans/`.

## Scenario dispositions

Original local campaign disposition: G11 PASS 16, PARTIAL 6, NOT EXECUTED 1, BLOCKED 2; G12 PASS 9, PARTIAL 12, NOT EXECUTED 1, NOT APPLICABLE 1, BLOCKED 2. **Independent review adjustment:** G11-S21 is PARTIAL rather than PASS because post-commit lost-reply and operating-system process termination were not executed. Current G11 count: PASS 15, PARTIAL 7, NOT EXECUTED 1, BLOCKED 2. G12 count remains unchanged. PARTIAL means the executed subset passed and the remainder was not run; those rows are not accepted. The partial G12 rows (S03 to S08, S10, S12) are HTTP matrices outside the targeted plan. Remaining items are listed per row in the matrices.

## Evidence package for the independent S25 reviews

- Directory: `E:\HarborOps-Verification\evidence\m11m12-local-01` (277 files).
- Manifest: `MANIFEST.sha256`, SHA-256 `7cadf6cb1edfd27ebaf7f65c357ace7496f1c331b3ee1d6d4fbb5abfbcfc12ba`. The first seal of 257 files is kept as `scripts/MANIFEST-first-seal-257-files.sha256`.
- Read-back: `READBACK.txt` (277 files re-hashed, 0 problems, every TRX re-parsed and compared with `trx-summary.json`).
- Safety scan: `safety-scan.json`, 0 hits for passwords, bearer tokens, cookies, 32-byte keys, private keys and tokens. Console host log lines were excluded from the retained logs; test data is synthetic.
- Archive for transfer: `E:\HarborOps-Verification\archives\m11m12-local-evidence-20261108.tar.gz`, SHA-256 `35d43c916bc4242cafa5ecf232d437aee9e1ef7918855f3cc866de8453b69b4d`; the extracted copy was verified against the manifest.
- Historical failures are retained, not erased: five plan-probe attempts (four test-code defects), two G11 residual attempts and two rehearsals (evaluator opt-in flag, `min(uuid)` absent in PostgreSQL 16, a selector, and an archive-route test error that had silently skipped the archive) and two G12 attempts (the F-M12-01 defect and an insufficient first fix).
- Reproduce: `scripts/final-run.sh`, `supplement-run.sh`, `sdk.sh` (the exact runners) against a clean checkout of the stated SHAs.

A reviewer for each gate should re-hash the files, reparse the TRX files, confirm the source SHA and an empty working-tree record, confirm the application diff against the frozen SHA is only the 35-line export change, read the historical failures, and issue a separate written G11-S25 and G12-S25 decision. This record is not that review.

## Outstanding

1. **Independent G11-S25 and G12-S25 reviews** and the separate acceptance decisions.
2. **Full 341-case suite at the final SHA.** One method changed after the hosted run. Local change-impact coverage is complete for the touched area, but G11-S23 and G12-S23 stay PARTIAL until the full suite is run once (about 30 to 40 minutes locally), or the reviewers accept the change-impact argument.
3. **Browser, mutation and scan reruns** if the reviewers require locally retained raw reports (hosted logs only today).
4. **Real proxy path and mid-download interruption** (nginx in front of Kestrel with a TCP client), and a CPU-bound deadline case, for G12-S17 and S19.
5. **PARTIAL scenario matrices** listed in the two evidence matrices, if the gates require them.
6. Signed M9 hold reconciliation and the formal G11-S01 and G12-S01 policy decisions.

## Technical recommendations

- **G11:** ready for independent S25 review. Technical evidence for S08 and S14 to S21 is complete within the stated simulations. Do not accept before items 1 and 2 and the PARTIAL rows are decided.
- **G12:** ready for independent S25 review. S09, S13, S18 and S20 are closed; F-M12-01 is fixed and verified. Do not accept before items 1, 2 and 4 and the PARTIAL rows are decided. Consider a trigram index only if text search must scale past the tested volume.

These are recommendations, not acceptance. Protected `main`, the M9 ARRI/DEPA hold, the 10,000 unresolved-work cap, the disabled-by-default M10 projector and null M10 ETA are unchanged.
