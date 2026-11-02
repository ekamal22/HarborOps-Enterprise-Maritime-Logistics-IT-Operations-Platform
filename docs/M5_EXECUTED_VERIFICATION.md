# M5 Executed Verification (G5)

**Milestone:** M5, shipment lifecycle and concurrency
**Gate state:** G5 OPEN, acceptance pending. G5-S01 is incomplete until the M5 local contract decision is accepted. Evidence for G5-S02 through G5-S25 is present and requires independent review.
**Verified source:** `c143e67906d3a9e0c1ab5f4b259ad1e5befd2a67` on `feat/m5-shipment-lifecycle`
**Execution:** local run 11 of the dedicated `ho-verify-g5` pipeline, Windows Git Bash, Docker Desktop with its data image on E:. No GitHub Actions were run.
**Baseline:** accepted `main` at `18d00e6710097e4e31a87190be8edb65337d1b50`. Nothing was merged.

Documentation commits made after the run are documentation only. They do not change the verified source, and the run is not claimed for any later revision.

## 1. Results at the verified revision

| Suite | Total | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| .NET unit | 138 | 138 | 0 | 0 |
| .NET contract | 12 | 12 | 0 | 0 |
| PostgreSQL integration, fresh database | 139 | 139 | 0 | 0 |
| PostgreSQL integration, repeated on the same database | 139 | 139 | 0 | 0 |
| Playwright, Chromium | 39 | 39 | 0 | 0 |
| Playwright, Firefox | 39 | 39 | 0 | 0 |
| Playwright, WebKit | 39 | 39 | 0 | 0 |

- Fresh and repeated integration passes executed the identical set of tests.
- Parameterized rows observed in the TRX: 36 workflow transition pairs, 9 queued read revocation variants, 9 queued write revocation variants, 5 invalidated-authentication variants.
- The Release build had 0 warnings and 0 errors with analyzers as errors.
- All 26 executable pipeline stages exited 0, including dependency review (no vulnerable NuGet or npm packages), documentation checks, M3/M4/M5 fixtures, the three outage and recovery checks, and Trivy.
- Trivy HIGH and CRITICAL findings: 0 for the postgres, api and web images.
- Teardown proof: 0 remaining containers, 0 volumes, 0 integration database containers for the G5 project.
- The evidence stage exited 1 only because G5-S01 is incomplete. It reports EVIDENCE PRESENT, REVIEW REQUIRED for G5-S02 through G5-S25.

## 2. Scenario dispositions

The per-scenario mapping to executed tests, with row counts taken from the raw TRX and Playwright JSON, is in `reports/G5_EVIDENCE_INDEX.md` inside the evidence package.

| Case | Disposition |
| --- | --- |
| G5-S01 | INCOMPLETE. The [M5 local contract decision](M5_LOCAL_CONTRACT_DECISION.md) is proposed, acceptance pending |
| G5-S02 to G5-S25 | EVIDENCE PRESENT, REVIEW REQUIRED |

No case is marked accepted.

## 3. Defects found and corrected during verification

| Defect | Class | Correction |
| --- | --- | --- |
| EF context and snapshot did not compile: end-anchor text in shipment check constraints had been replaced by file content during a scripted edit | product source | `5c9f83b` |
| Integration tests did not compile (analyzer errors); SQL invariant test accepted any database error | test | `3d5af36` |
| Shipment workspace did not build (missing closing brace) | product source | `ade42f1` |
| Model snapshot differed from the live model: missing pinned indexes on `changed_by_user_id` for both shipment tables | product source | `10b40cc` |
| Atomicity fault-injection used single dollar quoting; read revocation expectation treated Auditor as having lost read access | test | `0d047f7` |
| Repeat run on the same database failed: assertion counted rows across the whole database | test | `eb1f059` |
| Docs stage failed: validators did not recognize the accepted D-003 section heading (inconsistency present on `main`) | harness | `c2cdaf9` |
| Search or filter change during a save left the form permanently busy and skipped the refresh; list rows carried no port labels | product source (frontend) | `d1decce`, with the parallel label change in `38717c2` |
| Specs asserted a unique row while the open detail heading repeated the code | test | `062515c` |
| Several workspaces reporting one revocation cancelled the in-flight organization reload and left an empty list | product source (frontend) | `3321e65` |
| Shipment outage check ran after stages that downgrade the shared Manager account | harness | `7c30c45` |
| Per-engine browser JSON reports were not written (reporter reads a different variable) | harness | `bb22a3b` |
| Accepted M4 tenant-switch spec raced the asynchronous organization switch | test | `d831dca`, `523e1c9` |
| Paging test seeded only the caller's tenant, so a missing list tenant filter was undetected | test | `23836ae` |
| Long stable stage ran first, delaying failure feedback | harness | `c143e67` |

Commits `38717c2` and `bb22a3b` were made on the same branch by a parallel session; `4490a9c` merges them.

## 4. Security mutation proofs

Run in a detached worktree against source identical in `src/` to the verified revision (`23836ae`; the differences to `c143e67` are one browser spec and the runner script). Filter: all 47 shipment integration tests, fresh database per mutation.

| Mutation | Result |
| --- | --- |
| None (control) | 47 of 47 pass |
| Tenant predicate removed from single-record lookups | 1 test fails |
| Tenant predicate removed from list queries only | survives: equivalent mutant, because the list is filtered at three redundant layers |
| Tenant predicate removed everywhere | 2 tests fail, including the paging test |
| Post-lock session and account revalidation removed | 7 tests fail |

The first mutation round found that list scoping was undetected. The paging test was strengthened and the round rerun. The first-round logs are retained.

## 5. Storage

Controllable storage was on E:. Details are in `reports/C_DRIVE_CLEANUP_REPORT.md` and the storage records in the evidence package.

- C: free space fell by about 303 MB over the session. No new or changed file in a path controlled by this task was found, and the new and changed files seen were Windows, Office and Docker Desktop logs. Nothing was deleted from C:.
- A wider scan of C: to locate the remainder of the decrease was not performed. That portion is unattributed.
- The Docker Desktop data image on E: was not moved or deleted.

## 6. Evidence package

- Package: `E:\HarborOps-Verification\m5\archives\G5-final-package-c143e67906d3.zip`
- Digest file: `E:\HarborOps-Verification\m5\archives\G5-final-package-c143e67906d3.zip.sha256`
- Manifest: `SHA256SUMS.csv` at the root of the package (161 files). After packaging the ZIP was extracted and all 161 files re-hashed with 0 mismatches.
- Contents: original console stream, stage logs, TRX, Playwright JSON, traces and artifacts, Trivy JSON, migration and SQL test results, outage and recovery output, Docker service logs, teardown proof, storage records, mutation proofs, evidence index and the skipped, failed and incomplete report.
- The pipeline's own bundle for the run is `G5-incomplete-20261102T015136Z-c143e67906d3.zip` in the same directory, named incomplete because G5-S01 is pending.
- Earlier failed and superseded runs are preserved unmodified as directories in the same `archives` directory and are listed in the skipped, failed and incomplete report.

## 7. Known limitations and residual risks

- The evidence checker maps scenarios to tests by name and counts. It does not read test bodies, so independent review of the assertions behind each scenario is still required.
- Browser specs keep conditional skip guards for missing fixtures. They did not trigger (0 skipped).
- Several browser failures during verification were timing races; the fixes make preconditions explicit and do not reduce assertions.
- The shipment list reloads when a browser tab becomes visible again, which closes an open edit form. This did not fail any test and was not changed.
- The accepted M2 and M4 workspace code and one M4 browser spec were corrected as part of this work; the M0 to M4 regression results above are on the corrected revision.
- Disk space on C: is low and outside the control of this work.

## 8. Decisions awaiting acceptance

1. Acceptance of the [M5 local contract](M5_LOCAL_CONTRACT_DECISION.md), which closes G5-S01.
2. G5 acceptance after independent review of the evidence package.
3. Integration of `feat/m5-shipment-lifecycle` into `main`, only after the above.
