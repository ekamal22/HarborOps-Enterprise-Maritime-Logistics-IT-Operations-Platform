# M5 Executed Verification (G5)

**Milestone:** M5, shipment lifecycle and concurrency
**Gate state:** G5 OPEN, acceptance pending. G5-S01 is incomplete until the M5 local contract decision is accepted. Evidence for G5-S02 through G5-S25 is present on the current revision and requires independent review.
**Current verified source:** `3e57af490d9a28dd1f0c482b6d20cd44f8a9fc91` on `feat/m5-shipment-lifecycle` (run 13)
**Previously verified source:** `c143e67906d3a9e0c1ab5f4b259ad1e5befd2a67` (run 11), described in section 8. Its evidence package was independently reviewed and is retained unmodified.
**Execution:** local runs of the dedicated `ho-verify-g5` pipeline, Windows Git Bash, Docker Desktop with its data image on E:. No GitHub Actions were run.
**Baseline:** accepted `main` at `18d00e6710097e4e31a87190be8edb65337d1b50`. Nothing was merged.

Documentation commits made after a run are documentation only. They do not change the verified source, and no run is claimed for a later revision.

## 0. Status vocabulary

| Term | Meaning here |
| --- | --- |
| Implemented | The behavior exists in source on the feature branch |
| Tested | An automated test asserts it and passed in the final run at the verified revision |
| Evidence present | The raw results are in the evidence package and the checker maps them to the scenario |
| Independently reviewed | A person other than the implementer inspected the raw evidence. Only the run 11 package has been reviewed; the run 13 package is pending review |
| Acceptance pending | The decision is with the project and has not been given |
| Accepted | Recorded only when an explicit decision is given. Nothing in M5 is accepted |

## 1. Results at the current verified revision (run 13)

| Suite | Total | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| .NET unit | 138 | 138 | 0 | 0 |
| .NET contract | 12 | 12 | 0 | 0 |
| PostgreSQL integration, fresh database | 146 | 146 | 0 | 0 |
| PostgreSQL integration, repeated on the same database | 146 | 146 | 0 | 0 |
| Playwright, Chromium | 47 | 47 | 0 | 0 |
| Playwright, Firefox | 47 | 47 | 0 | 0 |
| Playwright, WebKit | 47 | 47 | 0 | 0 |
| Web unit (draft merge rules and earlier web tests) | 18 | 18 | 0 | 0 |

- Fresh and repeated integration passes executed the identical set of tests. Parameterized rows include 36 workflow transition pairs and 9 queued read and 9 queued write revocation variants.
- The Release build had 0 warnings and 0 errors with analyzers as errors.
- All 26 executable pipeline stages exited 0, including dependency review, documentation checks, M3, M4 and M5 fixtures, the three outage and recovery checks, and Trivy (HIGH and CRITICAL findings: 0 for postgres, api and web).
- Teardown proof: 0 remaining containers, volumes or integration database containers for the G5 project.
- The evidence stage exited 1 only because G5-S01 is incomplete. It reports EVIDENCE PRESENT, REVIEW REQUIRED for G5-S02 through G5-S25.

## 2. Scenario dispositions

The per-scenario mapping to executed tests, with row counts taken from the raw TRX and Playwright JSON, is in `reports/G5_EVIDENCE_INDEX.md` inside the evidence package.

| Case | Disposition |
| --- | --- |
| G5-S01 | INCOMPLETE. The [M5 local contract decision](M5_LOCAL_CONTRACT_DECISION.md) is proposed, acceptance pending. The [compliance matrix](M5_G5_S01_CONTRACT_COMPLIANCE.md) shows all six rules implemented and tested |
| G5-S02 to G5-S25 | EVIDENCE PRESENT, REVIEW REQUIRED |

No case is marked accepted.

## 3. G5-S23: unsaved edits lost on tab return

**Finding.** When a browser tab became visible again, two components reloaded: the shipment workspace called one `reload()` that cleared the open shipment, its form and the list, and the parent organization workspace cleared its selection and set loading, which unmounted the whole shipment workspace. Any unsaved edit was lost by switching tabs and returning.

**Root cause.** One reload served three different jobs (first load, filter change, refresh), and the parent treated "revalidate access" as "discard everything".

**Fix.**
- Shipment workspace: list resets, background refreshes, the open shipment and mutations have separate request serials, so no one of them can strand a loading flag. A background refresh keeps rows on screen and never clears them on failure. A newer server version of the open shipment is held aside and reported, never applied to the form. "Load current version" merges field by field through a tested module: untouched fields follow the server, edited fields keep the draft, fields the server also changed are reported as collisions, and edits that can no longer be saved (a status that is no longer reachable, a schedule locked by transit) are reported and not applied. Filter changes no longer close the panel. Closing, switching or creating while a draft exists asks for confirmation. Mutations refresh with the latest filters.
- Organization workspace: the visibility refresh is quiet. It revalidates membership, role and organization without unmounting the open workspaces, and it still clears protected state when the organization, the role or access changes, or when the service rejects access. Only a transient service outage leaves the current view.

**Tests.** Web unit tests (12 cases for the merge rules) and eight browser cases in each of Chromium, Firefox and WebKit (`shipment-draft.spec.mjs`): tab return keeps the exact values and context; an in-flight held refresh keeps the draft and typing during the refresh; a concurrent server change is held aside and merged only on request with the draft intact; a stale save keeps the exact draft and a reviewed retry succeeds once; an organization switch drops the draft; a real membership downgrade while away removes the shipment workspace and the draft; logout leaves no shipment state; archive and restore of another shipment leave a draft untouched and a normal save still works.

**Limitation.** In the headless engines used here, a tab switch never changes `document.visibilityState` and no `visibilitychange` event is delivered, including for Chromium's full browser with its window minimized. The probe output is in the evidence package (`probes/visibility-probe.txt`). The browser cases therefore bring a genuine second page to the front and drive visibility the way the application reads it (the state property plus the event). Real concurrent server changes and a real membership downgrade through the API are used where the scenario depends on server state. A headed browser was not used because it would open windows on the desktop.

## 4. G5-S21: interrupted transactions

**Finding.** The existing tests covered illegal transitions, an injected history failure and cancellation while a write waits for a lock. They did not cover an interruption after database work had started, a process death, a timeout, or a response lost after commit, which the plan requires.

**Method.** Each scenario pins the API's PostgreSQL backend after its writes and before commit, using a trigger scoped to one shipment reference that blocks on an advisory lock the test holds. The test then proves the state from `pg_locks` and `pg_stat_activity` (backend waiting on the advisory lock, write transaction id assigned, shipment row locked by the open transaction, nothing visible to other sessions) and applies the interruption. No sleep is used to establish the point. The observed values are written to the test output and recorded in the TRX. Only test-harness techniques are used; no production hook was added.

| Scenario (test) | Interruption | Verified outcome |
| --- | --- | --- |
| `TerminatedDatabaseConnectionMidTransitionRollsBackAndRecovers` | backend terminated with `pg_terminate_backend` | sanitized 503; version, status, update time and history unchanged; retry commits once; replay and skipped-version retries return 409 |
| `TerminatedDatabaseConnectionMidCreateLeavesNoShipmentOrHistory` | backend terminated during creation | no shipment or history row; reference not reserved; retry creates one of each; duplicate returns 409 |
| `TerminatedDatabaseConnectionMidArchiveLeavesShipmentActive` | backend terminated during archive | shipment still Active at version 1; retry archives once; replay 409; restore works; no history row written by archive or restore |
| `CancelledRequestMidTransitionRollsBackAndRecovers` | client cancels while the statement is blocked | the server cancels the statement and rolls back; state unchanged; retry commits once |
| `CommandTimeoutMidTransitionRollsBackWithSanitizedFailure` | enforced 6 second command timeout | sanitized 503; rolled back; retry commits once |
| `KilledApplicationProcessMidTransitionCommitsNothing` | a real API child process is killed | the database cannot see the client die, so the transaction stays open and uncommitted until released; the orphaned backend then ends and nothing commits; a new request succeeds |
| `LostResponseAfterCommitIsDistinguishableFromRollbackAndRetrySafe` | response discarded after the handler completes | unlike every case above the change is fully committed (version 2, two history rows); a read resolves the ambiguity; a blind retry returns 409 and cannot duplicate history |

Six of the seven scenarios pin a database backend and record its observed state in the TRX. The lost-response scenario does not pin a backend; it asserts the committed state and the retry outcome. Every sanitized failure body is checked for database terms, SQL, stack frames and trigger names. Every API response in these scenarios carries `Cache-Control: no-store`.

The evidence checker requires, in both integration passes, the observed interruption points and recovery lines, so a passing test without the recorded observation does not satisfy G5-S21.

## 5. Other defects found and corrected in this assignment

| Defect | Class | Correction |
| --- | --- | --- |
| Framework-generated responses such as the anonymous 401 challenge lacked `Cache-Control: no-store`, contrary to the architecture (the exception handler's 503 already carried it) | product source | `Program.cs` adds it to every `/api` response that sets no cache policy; asserted by tests |
| A shipment test injected a history failure for every shipment in the shared database, which could disturb parallel tests | test | the fault is scoped to the test's shipment |
| Browser tests assumed the fixture row is on the unfiltered first page, which stops holding as more shipments exist | test | the test searches for the fixture row |
| Draft tests would exceed the 20 logins per 5 minutes limit in one spec file | test | draft cases live in their own spec file, which gets its own API restart |
| After the rewrite, mutations refreshed the list with the filters of the render that started them | product source | latest-filter reference, found by rehearsal before the full run |

## 6. Security mutation proofs

Run in a detached worktree against the verified source (`3e57af490d9a28dd1f0c482b6d20cd44f8a9fc91`), filter `Shipment` (54 shipment integration tests), fresh database per mutation. Logs, diffs and the harness are in `mutation-proof/` in the evidence package. The first round on the earlier source, which found that list scoping was undetected, is kept in `mutation-proof/` history directories.

| Mutation | Expected | Observed |
| --- | --- | --- |
| None (control) | all pass | 54 of 54 pass |
| Tenant predicate removed from single-record lookups | fails | 1 test fails (`ExternalIdentifiersReserveArchivedValuesButPermitCrossTenantReuse`) |
| Tenant predicate removed from list queries only | survives (equivalent mutant: three redundant layers) | 54 of 54 pass, as expected |
| Tenant predicate removed everywhere | fails | 2 tests fail (`PaginationAndFiltersStayTenantScoped`, `ExternalIdentifiersReserveArchivedValuesButPermitCrossTenantReuse`) |
| Post-lock session and account revalidation removed | fails | 7 tests fail (queued read and write variants for session, account and security version, and `QueuedShipmentWriteRechecksSessionRevocation`) |
| `no-store` middleware removed | fails | 1 test fails (`AnonymousAllSixShipmentRoutesRejectAccess`) |

## 7. Storage

Controllable storage was on E:. Details are in `reports/C_DRIVE_CLEANUP_REPORT.md` in the evidence package. C: free space moved from 7,826,374,656 to 7,716,483,072 bytes (down 109,891,584) since the pre-work baseline; E: free space moved from 120,910,675,968 to 113,837,133,824 bytes (down 7,073,542,144: caches, builds, archives and Docker data). The tracked C: directories had 47 new files (27 MB) and 17 changed files, none under a path this work controls, so nothing attributable was found and nothing was deleted from C:. A wider scan of C: to locate unexplained free-space changes was not performed. The Docker Desktop data image on E: was not moved or deleted.

## 8. First verification round (run 11, `c143e67906d3a9e0c1ab5f4b259ad1e5befd2a67`)

| Suite | Total | Passed |
| --- | ---: | ---: |
| .NET unit | 138 | 138 |
| .NET contract | 12 | 12 |
| Integration, fresh | 139 | 139 |
| Integration, repeated | 139 | 139 |
| Playwright, each of Chromium, Firefox, WebKit | 39 | 39 |

All 26 executable stages passed; Trivy HIGH and CRITICAL: 0; teardown left no resources; G5-S01 incomplete. Package: `E:\HarborOps-Verification\m5\archives\G5-final-package-c143e67906d3.zip`, SHA-256 `D7386AED6F06A652FAD1653D00140330D74533E9A45941F351DB496397D7DBC8`. It was independently reviewed, which identified the three items addressed in sections 3, 4 and the compliance matrix.

Defects found and corrected in that round, with commits:

| Defect | Class | Correction |
| --- | --- | --- |
| EF context and snapshot did not compile: end-anchor text in shipment check constraints had been replaced by file content during a scripted edit | product source | `5c9f83b` |
| Integration tests did not compile (analyzer errors); SQL invariant test accepted any database error | test | `3d5af36` |
| Shipment workspace did not build (missing closing brace) | product source | `ade42f1` |
| Model snapshot differed from the live model: missing pinned indexes on `changed_by_user_id` for both shipment tables | product source | `10b40cc` |
| Atomicity fault injection used single dollar quoting; read revocation expectation treated Auditor as having lost read access | test | `0d047f7` |
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

## 9. Evidence package for the current revision

- Package: `E:\HarborOps-Verification\m5\archives\G5-final-package-3e57af490d9a.zip`
- SHA-256: `76AB98D7134E5A2482E73772C0FAA9E69EE5C02BCF3A9616889084D5D389AD89` (also in `G5-final-package-3e57af490d9a.zip.sha256`)
- Manifest: `SHA256SUMS.csv` at the root of the package (185 files). After packaging the ZIP was extracted and all 185 files re-hashed with 0 mismatches.
- Contents: original console stream, stage ledger and stage logs, TRX, Playwright JSON, traces and artifacts, Trivy JSON, migration and SQL test results, outage and recovery output, Docker service logs, teardown proof, storage records, scenario index (`reports/G5_EVIDENCE_INDEX.md`), interruption proof (`reports/G5_S21_INTERRUPTION_PROOF.md`), visibility probe output (`probes/`), mutation proofs and their harness (`mutation-proof/`, with the earlier rounds), source identity, the skipped, failed and incomplete report and the C: cleanup report.
- The package for the first verified revision, `G5-final-package-c143e67906d3.zip` (SHA-256 `D7386AED6F06A652FAD1653D00140330D74533E9A45941F351DB496397D7DBC8`), is unchanged and remains read-only.
- The pipeline's own bundle for run 13 is `G5-incomplete-20261102T044514Z-3e57af490d9a.zip` in the same directory, named incomplete because G5-S01 is pending.
- Earlier failed and superseded runs are preserved unmodified as directories in `E:\HarborOps-Verification\m5\archives` and listed in the skipped, failed and incomplete report in the package.

## 10. Known limitations and residual risks

- The evidence checker maps scenarios to tests by name and counts, and for S21 also to the recorded observations. It does not read assertion bodies, so independent review of what each scenario asserts is still required.
- Real browser visibility transitions could not be produced in the headless engines available (section 3).
- The accepted M3 reference and M4 maritime workspaces still close their edit forms when the tab becomes visible. That is accepted M3/M4 behavior outside M5 and was not changed; it is a candidate follow-up that would need its own verification.
- Browser specs keep conditional skip guards for missing fixtures. They did not trigger (0 skipped).
- Several browser failures during verification were timing races; each fix makes a precondition explicit and does not reduce assertions.
- Disk space on C: is low and outside the control of this work.

## 11. Decisions awaiting acceptance

1. Acceptance of the [M5 local contract](M5_LOCAL_CONTRACT_DECISION.md), which closes G5-S01.
2. G5 acceptance after independent review of the current evidence package.
3. Integration of `feat/m5-shipment-lifecycle` into `main`, only after the above.
