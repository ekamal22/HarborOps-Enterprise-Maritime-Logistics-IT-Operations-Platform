# M4 Executed Verification and G4 Acceptance

**Milestone:** M4, voyage and container reference data
**Decision:** D-003 Option B accepted on 2026-11-01
**G4:** Explicitly accepted on 2026-11-01 following independent evidence review
**Verified implementation SHA:** `600d7c787da9f9cd4a70225cb3e3c38c093c0dc6`
**Integration:** Fast-forward into `main`, with subsequent documentation-only acceptance commit(s)
**Environment:** Windows 11 local development, E:-backed Docker Desktop and disposable PostgreSQL 16, .NET 10, React/TypeScript, Chromium, Firefox and WebKit
**Release status:** Not a production deployment. M5 onward remains independently gated.

## Evidence provenance and integrity

The reviewed immutable execution archive is `E:\HarborOps-Verification\archives\G4-final-20261101T031757Z-600d7c787da9.zip`. Its SHA-256 is:

`AE865B87C60E0A436E8F3C881CFFD5F447F83AB734359DA44683690EE6FC4110`

The archive's source marker `g4-source-sha.txt` identifies `600d7c787da9f9cd4a70225cb3e3c38c093c0dc6`. All **149 listed file hashes** in the accompanying SHA-256 inventory were independently checked against the uploaded archive. Four .NET TRX reports, the Playwright per-spec JSON reports, the stage ledger, logs, PostgreSQL recovery records, scanner JSON and cleanup records were reviewed. The same source revision is published on the M4 branch history.

The G4 runner executed 20 functional stages with exit code 0 and one `evidence` policy stage with exit code 1. That sole policy exit was caused by **D-003 acceptance pending at the time of execution**, not by any failed executable test. G4-S02 through G4-S25 were reported as `EVIDENCE PRESENT, REVIEW REQUIRED`; G4-S01 had passed validation tests and lacked only the formal D-003 decision. Option B and formal G4 acceptance were subsequently explicitly approved on 2026-11-01. The original evidence file is retained unchanged rather than retrospectively misrepresenting that exit as zero.

No .NET, frontend, database or test implementation changes were required after the final verified revision. Acceptance documentation commits are not claimed as new runtime-tested SHAs.

## Executed results

| Check | Final evidence |
| --- | --- |
| Decision-gate regression tests | 4/4 passed |
| Schema-index, environment and rootless PostgreSQL image preflight | Passed |
| .NET 10 Release build | 0 warnings, 0 errors |
| Unit tests | 82/82 passed |
| Contract tests | 12/12 passed |
| Fresh PostgreSQL integration | 92/92 passed |
| Repeated integration on the same disposable database | 92/92 passed |
| Chromium browser tests | 31/31 passed |
| Firefox browser tests | 31/31 passed |
| WebKit browser tests | 31/31 passed |
| Frontend production build and planning documentation checks | Passed |
| M3 reference-data outage and recovery | Passed |
| M4 voyage/container outage and authorization recovery | Passed |
| Database recovery | Passed after a sanitized 503, recovered on attempt 2 of 30 |
| Trivy postgres, api and web image JSON | 0 HIGH and 0 CRITICAL findings |
| Disposal | No remaining verification containers or volumes reported |

All required M4 test assertions in the final TRX outcomes passed, including five session invalidation variants, five queued authorization/revocation variants and same-identifier cross-tenant isolation. All three browser engines had no failures or skips. The security mutation logs confirmed relevant tests fail when tenant filtering or account revalidation is deliberately removed, with the original code restored for the final runs.

## G4-S01 through G4-S25 closure

| Gate | Verified disposition |
| --- | --- |
| G4-S01 | Passed ISO parser/negative validation evidence plus separately approved D-003 Option B on 2026-11-01 |
| G4-S02 to G4-S06 | Reviewed domain, migration/snapshot, composite FK, direct SQL and trigger test evidence |
| G4-S07 to G4-S10 | Reviewed repeatable fixtures, authentication/session, selected-organization and foreign-organization boundary cases |
| G4-S11 to G4-S15 | Reviewed foreign UUID, five-role matrix, live revocation, hostile input and CSRF/Origin test evidence |
| G4-S16 to G4-S21 | Reviewed maritime lifecycle, identifier reservation, search/pagination and concurrency test evidence |
| G4-S22 to G4-S23 | Reviewed all three browser engines, tenant/session race cases and outage/recovery behavior |
| G4-S24 | Reviewed impacted accepted M0-M3 regression coverage in fresh and repeated executions |
| G4-S25 | Reviewed clean build, dependency/image scans, source identity, stage ledger, temporary storage and cleanup evidence |

**Review conclusion:** G4-S01 through G4-S25 accepted for M4 at the verified implementation SHA, with the documented post-run D-003 decision closure. This is milestone acceptance, not global release approval.

## Correctness audit and remediation

See [fifth correctness audit](M4_FIFTH_CORRECTNESS_AUDIT.md) for F5-01 through F5-12, including role-revocation tests, deterministic identifiers, active-tab loading, browser runner behavior, fixture validation and archive provenance repair. Earlier [first](M4_CORRECTNESS_AUDIT.md), [second](M4_SECOND_CORRECTNESS_AUDIT.md), [third](M4_THIRD_CORRECTNESS_AUDIT.md) and [fourth](M4_FOURTH_CORRECTNESS_AUDIT.md) audits preserve the investigation history.

The final source code and tests are unchanged by D-003 closure and acceptance documentation. There is no claim of a fresh test execution on documentation-only commits.

## Storage, cleanup and limitations

All controllable workspace files, .NET and npm caches, Playwright output, database data, Docker Desktop disk and retained verification artifacts were located on E:. The Docker data image was verified at `E:\APP IDEAS\New folder\DockerDesktopWSL\disk\docker_data.vhdx`. Storage observations reported C: free space changing from 5.97 GB to 5.86 GB over the verification period. Twenty-five additional Docker Desktop or Windows log files (approximately 21 MB) could not be conclusively attributed to this run and were **not deleted**. No new crash dumps or attributable task-created temporary C: files were identified. Do not infer a broader cleanup authorization.

The final archive's evidence-stage policy failure is recorded above; it was not erased. Historical archived runs may contain an incorrectly named archive directory from before F5-12; `g4-source-sha.txt` and the accompanying archive mapping identify their true source revisions.

## Downstream implications

M4 is an accepted module on `main`. M6 assignment implementation is still gated on **both G4 and G5**; G4 acceptance does not imply that M5/G5 is complete. If an independently developed M5 migration is integrated, the resulting combined source requires new migration, authorization, frontend and regression verification. M24-04 remains the formal accessibility testing gate. No GitHub Actions were triggered for this acceptance and no public deployment is claimed.
