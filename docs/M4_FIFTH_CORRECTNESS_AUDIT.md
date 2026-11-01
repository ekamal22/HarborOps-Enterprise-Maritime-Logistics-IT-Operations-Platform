# M4 Fifth Correctness Audit: Full Local G4 Execution

**Date:** 2026-11-01  
**Branch:** `feat/m4-voyage-container-references`  
**Starting revision:** `5b8ceeb555e0bc586cff3fc39df33aabc4841120` (remote tip after the fourth audit)  
**First all-green revision:** `f1854c3fb684d576d6b16d94d5cb44113bd25f64`. A second complete run at documentation commit `fd31958ec5017050dab58a329d65b4afb300eb60` reproduced every stage result. Later commits change documentation, the evidence mapping and the runner's archive naming only, and a third complete run on the final revision is recorded in the evidence bundle on E: (its `g4-source-sha.txt` and hash manifest name the exact revision).  
**Gate:** G4 OPEN, acceptance pending. D-003 acceptance pending.

## Result summary

Every executable stage of `scripts/verify-m4-local.sh` passed on E:-backed disposable resources at the executed revision.

| Stage | Result |
| --- | --- |
| Decision-unit tests | 4/4 passed |
| Schema-index contract | Passed |
| Preflight, Docker data image on E: | Passed |
| PostgreSQL image build | Passed |
| .NET 10 Release build | Zero warnings, zero errors |
| Unit tests | 82/82 passed |
| Contract tests | 12/12 passed |
| Fresh PostgreSQL integration | 92/92 passed |
| Repeated integration, same disposable database | 92/92 passed |
| Dependency review (NuGet advisories, npm audit) | No vulnerable packages |
| Frontend production build | Passed |
| Planning documentation and execution graph checks | Passed |
| Browsers, 12 specs, 31 tests per engine | Chromium 31/31, Firefox 31/31, WebKit 31/31 |
| M3 reference-data outage and recovery | Passed |
| M4 voyage and container outage, recovery and real role downgrade | Passed |
| Scoped recovery after PostgreSQL interruption | Passed on attempt 2 of 30 with one sanitized 503 |
| Trivy image scans (postgres, api, web) | No HIGH or CRITICAL findings |
| Evidence index | G4-S02 to G4-S25 evidence present, review required. G4-S01 INCOMPLETE solely because D-003 acceptance is pending |

The evidence checker exits non-zero by design while D-003 is pending. That exit is the only unmet item and is a decision, not a defect.

## Defects found and corrected

| ID | Class | Finding | Root cause | Correction |
| --- | --- | --- | --- | --- |
| F5-01 | Harness | A failed run left its `ho-g2-pg` database container behind, and the next run could not detect it | Git Bash rewrites a leading slash in the anchored `name=^/ho-g2-pg$` docker filter, so the takeover guard and the exit cleanup both matched nothing | `MSYS_NO_PATHCONV=1` on those two docker calls in `scripts/verify-m4-local.sh`. Verified with a throwaway container that the old filter matched nothing and the new one matches |
| F5-02 | Product | Choosing the already active Voyages or Containers tab during the first load left the list on "Loading" with Refresh disabled | The tab handler advanced the load generation, discarding the pending response, but setting the same tab changed no state, so no replacement load started | `MaritimeWorkspace.tsx` ignores a click on the active tab. A held-request browser test reproduces the defect (failed before, passes after, in three engines) |
| F5-03 | Test | Pagination test counted 27 rows instead of 25 | `ancestor::section` also matched the enclosing Secure access section, which contains the two M3 reference rows | All M4 specs use `ancestor::section[1]` |
| F5-04 | Test | The ISO container identifier test failed in the second and third engine | The identifier `CSQU3054383` was fixed, and the per-organization unique index kept it from the first engine's run. Same class as the fourth audit failure, in the browser suite | The spec constructs a new valid ISO 6346 identifier per invocation with an independent check-digit routine and derives the invalid one from it |
| F5-05 | Test | New or fixture records were not visible after create or reload in later engines | The list is ordered by record id, so once more than 25 containers exist a given record is not on page 1 | Specs locate records by search, not by position |
| F5-06 | Test | The real role revocation spec failed in Chromium and Firefox | The administrator's organization selection was not awaited, so the roster request raced the selection and returned an error body | The spec waits for the selection response, as the accepted M3 revocation spec does, and asserts roster status 200 |
| F5-07 | Test | The accepted M3 references spec failed in WebKit | It clicked the first row while the unfiltered list was still shown. The pending search reload then discarded the selection | A helper waits for the filtered response and for exactly one row before acting. The product behavior is unchanged |
| F5-08 | Harness | One failing browser spec stopped the stage and hid every later spec | `set -e` in the browser runner | The runner executes every spec in every engine and fails at the end. Failure artifacts are copied to the evidence directory |
| F5-09 | Coverage | G4-S14 had no case where account disable, security version change, organization suspension or member removal lands while a mutation is already queued | Only the administrator downgrade was exercised under queueing | `QueuedMaritimeRequestsRevalidateAccessAfterTheLockIsReleased` covers all four with a no-revocation control, against both resources. Required by the evidence checker, together with the F5-02 browser test in G4-S23 |
| F5-10 | Test | A voyage code lookup in the SQL isolation test was global | Unscoped `AnyAsync` on a fixed code | Scoped to the organization under test |
| F5-11 | Fixture | `prepare-m4-browser-fixtures.mjs` accepted an existing row with the right code but a different identity | It matched on reference code only | It now compares identifier kind, identifier, equipment type, vessel and ports |
| F5-12 | Harness | Archived evidence directories carried the wrong revision in their names | The runner named the archive of the previous run after the revision about to run | The archive is named after the revision recorded in the evidence it moves. Directories created before this fix are identified by the `g4-source-sha.txt` inside each, not by name |

## Effectiveness of the corrected tests (mutation checks)

Each mutation was applied temporarily to `MaritimeStore.cs`, tested against a disposable database in two consecutive passes, and reverted.

| Mutation | Tests expected to fail | Observed |
| --- | --- | --- |
| Remove the organization filter from the record query | `IdenticalContainerIdentifierIsAllowedInDifferentOrganizations`, `ForeignParentAndForeignRecordIdsDoNotLeak`, `ExistingForeignVoyageUuidCannotBeReadOrMutated` | All three failed in both passes |
| Remove the in-transaction account status and security version recheck | The `account_disabled` and `security_version` cases | Exactly those two failed. The other three cases passed |

## Review of the corrected container identifier test

| Check | Result |
| --- | --- |
| A new valid synthetic identifier per invocation | Confirmed |
| Both fixture organizations receive that identifier within the invocation | Confirmed |
| Exactly two rows, in two distinct organizations, each version 1 | Confirmed |
| The selected organization's search returns only its own row | Confirmed |
| Rows from earlier executions cannot be counted | Confirmed, repeated integration 92/92 |
| Detects a missing tenant filter | Confirmed by the first mutation |

A search of the remaining integration suite found no other global count or fixed shared identifier that survives between executions. Upgrade tests use per-test schemas. Every other fixed identifier is scoped to an organization created for that invocation.

## Audit dispositions with no defect found

| Area | Disposition |
| --- | --- |
| Migration and snapshot consistency | `MigrationSnapshotTests.ModelSnapshot_MatchesCurrentEntityModel` passed in both integration passes. Fresh, repeat and rollback upgrade from seeded M3 data passed |
| Composite foreign keys, route, version and archive triggers | Direct SQL rejection cases passed. No gap found in the trigger logic |
| Session and authorization invalidation | Five invalidation kinds across all ten routes passed. Queued revocation now covered (F5-09) |
| Voyage lifecycle | Transition graph, actual-time immutability and schedule rules agree between policy code, store and triggers |
| Optimistic concurrency | Competing updates, duplicate creation and parent archival races serialize with one valid outcome |
| Browser stale-data protection | Generation and token guards found sound apart from F5-02 |
| Evidence checker | Stage names in a scenario row are only checked for membership, but every expected stage is separately required to have run with exit 0, so no scenario can pass on a failed stage |

## Remaining decisions

D-003 and G4 acceptance are explicit decisions and are not claimed here. M4 is not merged. No GitHub Actions were run.

## Subsequent acceptance disposition (2026-11-01)

The final source revision `600d7c787da9f9cd4a70225cb3e3c38c093c0dc6` and the full G4 archive were independently reviewed. All 149 file hashes in the supplied manifest matched; unit, contract, fresh/repeated integration, three browser engines, outage/recovery, image security scans and teardown evidence supported G4 closure. D-003 Option B and G4 were explicitly accepted on 2026-11-01. See [M4 executed verification](M4_EXECUTED_VERIFICATION.md). The earlier `G4 OPEN` statements above accurately describe the audit's pre-decision state and are preserved as history. M4 has since been fast-forward integrated into `main` without a GitHub Actions run.
