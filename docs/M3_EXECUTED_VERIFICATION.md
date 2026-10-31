# M3 Executed Verification (G3)

**Date:** 2026-10-31  
**Milestone and gate:** M3 Ports and vessels reference data, gate G3  
**Verified source revision:** `b72025d35d62d0a8a45b466986a0b3bd89eebadb` on `feat/m3-port-vessel-reference-data`. Every result below was produced by one invocation of `scripts/verify-m3-local.sh` against this revision from a clean worktree. Commits after this revision change documentation only.  
**Verification method:** complete local execution on the Windows development machine with every controllable path on E:. No GitHub Actions workflow was dispatched, rerun or triggered, and every commit carries `[skip ci]`.  
**Result:** every mandatory scenario G3-S01 through G3-S25 has observed, passing evidence on the verified revision.  
**Gate state:** G3 verified locally and explicitly accepted on 2026-10-31. Accepted M3 integrated into `main` by fast-forward; M4 has not started.

The mapping of scenarios to named tests below was checked in two ways: `scripts/assert-g3-evidence.mjs` rejected a missing, failed, skipped or stale result, and every named test was then cross-checked by hand against the fresh .NET result files.

## Environment

| Item | Value |
| --- | --- |
| Operating system | Microsoft Windows 11 (10.0.26200) |
| .NET | SDK 10.0.403 from `mcr.microsoft.com/dotnet/sdk:10.0`. The host has only SDK 8.0.423 and 9.0.200, and a .NET 10 SDK was not installed because the installer writes to C: |
| Node.js and npm | v24.20.0, npm 11.19.0 |
| Docker | Engine 29.6.2, Compose v5.3.1, Docker Desktop (`linux`, `Docker Desktop`, `docker-desktop`) |
| PostgreSQL | 16.15 (hardened rootless image built from `docker/postgres/Dockerfile`, revision label equal to the verified SHA) |
| Playwright | 1.64.1 with Chromium (build 1248), Firefox (build 1555) and WebKit (build 2370) installed under E: |
| Image scanner | Trivy 0.76.1 (`aquasec/trivy:0.76.1`), vulnerability database cached on E: |
| Run window | 2026-10-31 09:03:45Z to 09:16:04Z (12 minutes 19 seconds) |

## Storage configuration (E: only)

| Location | Value |
| --- | --- |
| Verification root | `E:\HarborOps-Verification` |
| `TEMP`, `TMP`, `TMPDIR` | `E:\HarborOps-Verification\tmp` |
| `DOTNET_CLI_HOME` | `E:\HarborOps-Verification\dotnet-home` |
| `NUGET_PACKAGES`, `NUGET_HTTP_CACHE_PATH` | `E:\HarborOps-Verification\nuget\packages`, `E:\HarborOps-Verification\nuget\http-cache` |
| `NPM_CONFIG_CACHE` | `E:\HarborOps-Verification\npm-cache` |
| `PLAYWRIGHT_BROWSERS_PATH` | `E:\HarborOps-Verification\playwright-browsers` |
| `XDG_CACHE_HOME`, `GIT_TMPDIR`, `DOCKER_CONFIG` | `E:\HarborOps-Verification\xdg-cache`, `...\git-tmp`, `...\docker-config` |
| Docker data image (`HARBOROPS_DOCKER_VHDX`) | `E:\APP IDEAS\New folder\DockerDesktopWSL\disk\docker_data.vhdx` |
| Worktree | A worktree of the repository under `E:\APP IDEAS\HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform` |
| Evidence | `E:\HarborOps-Verification\evidence` (the G2 evidence was moved intact to `E:\HarborOps-Verification\archives`) |

The Docker storage preflight (`scripts/check-m3-docker-storage.mjs`) passed. It confirmed that Docker Desktop's configured location (`CustomWslDistroDir`) contains the supplied image, that the running `docker-desktop` WSL distribution is registered under that E: directory, and that the engine is the Docker Desktop Linux backend.

Residual C: activity, stated plainly rather than as a claim of zero C: I/O:

- Free space on C: changed from 4,422,340,608 bytes to 4,368,510,976 bytes during the session (about 54 MB). This is attributed to operating-system activity; the project directed none of it and it is not separately measured.
- A read-only listing of `%LOCALAPPDATA%\Temp\wsl-crashes` shows no memory dump created during this session. The newest dumps pre-date the first test run of this session. Existing dumps were not touched.
- Tool binaries (Node.js, Git, Docker CLI) are read from C:, and Docker Desktop writes its own application logs there.

Docker is shared with unrelated projects. No unrelated container, image or volume was inspected beyond listing names, and none was modified. The primary `harborops_harborops-postgres` volume and the historical `ho-verify-oldcopy` volume were present before and after the run and were not accessed. Only the labeled disposable `ho-verify` Compose project and the labeled `ho-g2-pg` container are created and removed by the runner. After the run no `ho-verify` container or volume remained.

## Executed command

```text
HARBOROPS_VERIFICATION_ROOT=E:\HarborOps-Verification (plus the E: cache variables above)
HARBOROPS_DOCKER_VHDX=E:\APP IDEAS\New folder\DockerDesktopWSL\disk\docker_data.vhdx
bash scripts/verify-m3-local.sh
```

All 17 stages exited 0 and are recorded with the source SHA in `E:\HarborOps-Verification\evidence\g3-stages.tsv`: preflight, postgres-image, dotnet, deps, web, docs, stack, accounts, organizations, session, setup-fixtures, fixtures, browsers, reference-outage, recovery, scan, evidence. The index `G3_EXECUTION_INDEX.md` reports no missing evidence for any scenario.

## Results by suite

| Suite | Result |
| --- | --- |
| Release build of `HarborOps.sln` | Succeeded, 0 warnings, 0 errors |
| Unit tests | 50 of 50 passed |
| Contract tests | 12 of 12 passed |
| PostgreSQL integration, fresh database | 56 of 56 passed |
| PostgreSQL integration, repeated against the migrated and seeded database | 56 of 56 passed |
| Web unit tests | 6 of 6 passed |
| Web production build (`tsc -b`, `vite build`) | Succeeded |
| Browser, Chromium | 19 of 19 passed across 9 specs, 0 skipped, 0 flaky |
| Browser, Firefox | 19 of 19 passed across 9 specs, 0 skipped, 0 flaky |
| Browser, WebKit | 19 of 19 passed across 9 specs, 0 skipped, 0 flaky |
| NuGet vulnerable-package report | None in any of the 8 projects |
| `npm audit` (`web`, `tests/browser`) | 0 vulnerabilities in each |
| Trivy HIGH and CRITICAL, `postgres`, `api`, `web` running images | 0 findings in each |
| `render-execution-graph.mjs --check` and `check-planning-docs.mjs` | Passed: 25 milestones, 126 graph nodes, 40 Markdown files |

The 118 .NET results (50 unit, 12 contract, 56 integration) are in `E:\HarborOps-Verification\evidence\trx`. The browser run restarts the API container before every spec file, so each spec starts from a fresh API process. The browser reports are in `E:\HarborOps-Verification\evidence\playwright`.

Migration history on the disposable database, in order: `20261030170000_InitialBootstrap`, `20261031010000_IdentitySessions`, `20261031020000_OrganizationMemberships`, `20261031110000_PortVesselReferences`. `OrganizationUpgradeAndConstraintTests` now asserts these four identifiers exactly.

Running images: the `postgres`, `api` and `web` images used by Compose each carry the `org.opencontainers.image.revision` label `b72025d35d62d0a8a45b466986a0b3bd89eebadb`, which the scan stage asserts before scanning.

## G3 scenario results

All tests are in `tests/HarborOps.UnitTests`, `tests/HarborOps.ContractTests` or `tests/HarborOps.IntegrationTests` unless a browser spec or script stage is named.

| Scenario | Result | Observed evidence |
| --- | --- | --- |
| G3-S01 Port validation | PASS | `ReferencePolicyTests`: `NormalizesLocalCodes`, `RejectsInvalidLocalCodes`, `ValidatesOptionalLocodes`, `RejectsInvalidLocodes` |
| G3-S02 Vessel validation | PASS | `ReferencePolicyTests`: `AcceptsChecksumValidImo`, `RejectsInvalidImo`, `RejectsInvalidMmsi`, `OptionalImoAndMmsiAreNotMandatory` |
| G3-S03 Five-role policy | PASS | `ReferencePolicyTests.ReferencePermissionsAreExplicitAndDoNotEscalateOrganizationRoles` (unit); `ReferenceAdversarialHttpTests.SupportAndAuditorCannotUseAnyReferenceVerb` (HTTP) |
| G3-S04 Fresh M0-M3 migrations | PASS | `MigrationSnapshotTests.ModelSnapshot_MatchesCurrentEntityModel`, `Migrations_AreDiscoveredInOrder`; fresh database migrated by the integration suite |
| G3-S05 M2-to-M3 upgrade | PASS | `ReferenceHistoricalUpgradeTests.M2SeedAndAuthenticatedSessionSurviveM3UpgradeAndRepeat`: M0-M2 seeded database, authenticated session and selection survive, repeat migration safe, rollback to M2 and reapply |
| G3-S06 Constraints and immutability | PASS | `ReferenceHistoricalUpgradeTests.DirectSqlConstraintsAndIdentityTriggersRejectInvalidMutations`: ten direct SQL writes each rejected with the exact expected SQLSTATE (23514 for organization, code, provenance, version, status, country pattern, IMO checksum and MMSI; 22001 for oversize country) |
| G3-S07 Deterministic fixtures | PASS | Stage `fixtures` ran `prepare-m3-browser-fixtures.mjs`, which builds the fixtures twice and asserts identical IDs, versions and statuses and organization A/B independence |
| G3-S08 Anonymous, disabled, expired, revoked | PASS | `ReferenceBoundaryMatrixTests.EveryReferenceRouteRejectsAnonymousAndWrongRoles`; `ReferenceSessionBoundaryTests.EveryReferenceVerbRejectsInvalidatedSession` for `revoked`, `disabled` and `expired` |
| G3-S09 Missing organization selection | PASS | `ReferenceBoundaryMatrixTests.MissingSelectionAndTrueForeignRecordsCannotBeEnumerated`, `EveryReferenceVerbRequiresExplicitSelection` |
| G3-S10 Foreign organization substitution | PASS | `ReferenceBoundaryMatrixTests.EveryReferenceRouteRejectsAnonymousAndWrongRoles` substitutes organization B into every route and verb and asserts 404 without B data |
| G3-S11 Real foreign port UUID | PASS | `ReferenceAdversarialHttpTests.ForeignPortAndVesselRejectEveryRecordVerbWithoutMutation`, plus a really-inserted foreign port in `MissingSelectionAndTrueForeignRecordsCannotBeEnumerated` |
| G3-S12 Real foreign vessel UUID | PASS | `ReferenceAdversarialHttpTests.ForeignPortAndVesselRejectEveryRecordVerbWithoutMutation` |
| G3-S13 Full HTTP role matrix | PASS | `ReferenceAdversarialHttpTests.SupportAndAuditorCannotUseAnyReferenceVerb`; `ReferenceDataHttpTests.ScopedPortAndVesselLifecycleAndRoleMatrix` |
| G3-S14 Membership, account and organization changes | PASS | `ReferenceAdversarialHttpTests.SecurityDowngradeAndSuspendedOrganizationDenyReferenceReads`; `ReferenceBoundaryMatrixTests.SuspendedMembershipIsDeniedOnNextRequest`; `ReferenceSessionBoundaryTests.EveryReferenceVerbRejectsInvalidatedSession` |
| G3-S15 CSRF, Origin, hostile input | PASS | `ReferenceAdditionalContractTests.MalformedBodiesUnknownPropertiesAndOversizedJsonFailWithSanitized400`; `ReferenceSessionBoundaryTests.ForeignOriginRejectsMutationEvenWithRealCsrfToken` |
| G3-S16 Port lifecycle and contract | PASS | `ReferenceDataHttpTests.ScopedPortAndVesselLifecycleAndRoleMatrix` |
| G3-S17 Vessel lifecycle and contract | PASS | `ReferenceAdditionalContractTests.VesselFullLifecycleAndArchivedIdentifierReservation` |
| G3-S18 Uniqueness and concurrent collisions | PASS | `ReferenceConcurrencyTests.ParallelDuplicateCreatesReturnConflictWithoutDuplicateRows`, `IdenticalExternalIdentifiersAreAllowedAcrossOrganizations`; `ReferenceDataHttpTests.MigrationConstraintsEnforceTenantOwnershipAndExternalIdentifiers` |
| G3-S19 Search and pagination | PASS | `ReferenceBoundaryMatrixTests.QueryBoundsAndLiteralWildcardsProtectScopedPagination`; browser spec `reference-paging` in three engines |
| G3-S20 Concurrent mutation and authorization races | PASS | `ReferenceConcurrencyTests.ParallelUpdateAndArchiveProduceExactlyOneVersionTransition`; `ReferenceDataHttpTests.CompetingUpdatesAndImmediateRoleRevocation`; `ReferenceRevocationInterleavingTests.QueuedRoleDowngradeWinsBeforeBlockedReferenceWrite`, `QueuedSecurityVersionChangeDeniesReferenceWrite` (status and final database state) |
| G3-S21 Archive and restore | PASS | `ReferenceAdditionalContractTests.VesselFullLifecycleAndArchivedIdentifierReservation`; `ReferenceDataHttpTests.ScopedPortAndVesselLifecycleAndRoleMatrix` |
| G3-S22 Three-browser reference flow | PASS | `references.spec.mjs` (Manager create, edit, search, archive, restore for ports and vessels; Analyst read only; reload keeps selection) and `reference-paging.spec.mjs`, 19 of 19 in each of Chromium, Firefox and WebKit |
| G3-S23 Browser races and permission changes | PASS | `reference-races.spec.mjs` (delayed tenant A response after switching to B, dependency failure and recovery, logout during a held request, revocation notice, reload) and `reference-live-revocation.spec.mjs` (real Manager-to-Support downgrade through the HTTP API during a held refresh) in all three engines |
| G3-S24 M0, M1, M2 regressions and recovery | PASS | All M0-M2 .NET tests in the 118 results; stages `stack` (M0 Compose smoke and PostgreSQL outage), `session` (M1 cookie restart, outage, logout, replay), `organizations` (M2 restricted CLI denial), `recovery` (M2 scoped recovery), `reference-outage` (M3: sanitized 503 `dependency_unavailable`, recovery, then a real role downgrade returns 403 `access_denied`, no credential in API logs); browser specs `auth`, `baseline`, `organizations`, `organization-revocation`, `regression` in three engines |
| G3-S25 Build, security, operations, documentation | PASS | Release and web builds, dependency audits, image revision labels, Trivy 0 HIGH or CRITICAL on three images, Compose startup and cleanup, planning-document checks |

## Failure history and corrections

The first execution against the committed source did not pass. Each failure was diagnosed to a root cause and repaired without weakening a requirement. Product defects and harness defects are distinguished.

| # | Finding | Class | Correction |
| --- | --- | --- | --- |
| 1 | Every PostgreSQL integration test (56) and two contract tests failed: the EF Core model mapped the shared `ReferenceRecord` base class as a table-per-concrete-type hierarchy root and then configured keys on the derived types, which EF rejects at model build. The API could not start its database context. | Product defect | `ReferenceRecord` is now excluded from the model (`modelBuilder.Ignore<ReferenceRecord>()`) in the context and in the migration snapshot, so `PortReference` and `VesselReference` map their inherited properties directly. |
| 2 | `RealM0M1M2UpgradePreservesLegacyAccountSessionAndBaselineOnRepeat` expected three applied migrations. M3 adds a fourth. | Test defect | The assertion now lists the four exact migration identifiers in order. |
| 3 | `EveryReferenceVerbRejectsInvalidatedSession(expired)` violated the session time constraint because `AddTicks(1)` (100 ns) rounds to the same PostgreSQL microsecond as the issue time. | Test defect | The expiry is now one millisecond after issue, which is representable and already in the past. |
| 4 | `DirectSqlConstraintsAndIdentityTriggersRejectInvalidMutations` allowed a short SQLSTATE list that omitted 22001 for a three-letter country code. | Test defect | Each statement now asserts its own exact SQLSTATE, and a lowercase-digit country code case was added (23514). The assertion is stricter than before. |
| 5 | `reference-live-revocation.spec.mjs` called the organization-scoped roster API as the administrator before the UI's server-side organization selection had completed (409). | Test defect | The spec waits for the selection response and asserts 200 first. |
| 6 | The same spec created a fixed `REVOCATION-PORT` record, so any rerun against the same fixtures conflicted with 409. | Test defect | The record code and name are unique per run. |
| 7 | `revocation during a reference refresh clears protected rows` failed in all three engines: after a 403 `organization_access_revoked` the reference workspace set its alert and then asked the parent to clear the protected tree, which removed the alert, so users saw no explanation. | Product defect | The parent now receives the safe message from the child, clears protected state and shows the message. Automatic reloads after authorization loss are limited to one per 10 seconds, which also prevents a request loop if denial persists for a still-listed organization. |
| 8 | `scripts/check-m3-docker-storage.mjs` rejected this machine because the installed Docker Desktop records its WSL location as `CustomWslDistroDir`, not `diskImageLocation`, and because the verification-only E: `DOCKER_CONFIG` has no named `desktop-linux` context. The reference outage script had the same context assertion. | Harness defect | Both accept either setting, require the running `docker-desktop` distribution to be registered under the configured E: directory, and, when the named context is absent, require `DOCKER_HOST` to be pinned to the Docker Desktop Linux engine pipe and the engine to identify as Docker Desktop on Linux. |

Environment and operator interruptions that were not code defects: one full-runner attempt stopped before any test because Windows refused to rename the evidence directory while log-tail processes from this session's monitoring still held files open (the processes were identified by command line and stopped); a second attempt failed in the `stack` stage on a transient registry connection error (`EOF`) while resolving the .NET runtime base image, was cleaned up by the runner, and the full matrix was rerun after the base images were refreshed. No test result from an aborted attempt is used. The reported results come from the one complete passing invocation.

The set of tests actually executed stayed the same or grew: no test was removed, skipped or converted. The browser reports record 0 skipped across all three engines.

## Remaining risks and limits

- The model snapshot follows the project convention of restating the entity model in code. `ModelSnapshot_MatchesCurrentEntityModel` therefore compares two descriptions written from the same builder statements, and it does not by itself detect divergence between the hand-written migration SQL and the model. That divergence is covered by the direct SQL constraint test, the fresh and upgraded migration tests, and the full HTTP tests running against the migrated schema.
- Some browser tests simulate HTTP status codes to exercise frontend states. Real backend permission changes and a real PostgreSQL outage are covered separately (`reference-live-revocation`, `reference-outage`); simulated responses do not substitute for them.
- Trivy was run for vulnerabilities at HIGH and CRITICAL severity only, as in G2. Misconfiguration and secret scanning were not part of the contract.
- The Dockerfiles use floating base-image tags. A newer ASP.NET base image was downloaded during this session, and the scan applies to the images as built at the verified revision.
- Formal accessibility verification is intentionally out of scope (M24-04), and historical Docker-volume compatibility is intentionally out of scope (M6, M12, M18, M24-03). No historical volume or database was used.
- This is local, single-machine evidence. It is not a public or production deployment.

## Conclusion

All 25 mandatory G3 scenarios passed on `b72025d35d62d0a8a45b466986a0b3bd89eebadb`. No blocking correctness or security finding remains under the G3 acceptance contract. Independent raw-evidence review preceded explicit G3 acceptance on 2026-10-31. M3 has been fast-forward integrated into `main`. M4 is not started or approved for implementation by this record.

## Independent evidence review and acceptance

The final E:-backed run evidence was independently inspected before the G3 decision. All 63 files in the uploaded evidence archive matched their SHA-256 inventory. The raw TRX files contained 50 unit, 12 contract, 56 fresh PostgreSQL integration and 56 repeated PostgreSQL integration passes, with no failures or skips. Twenty-seven Playwright JSON reports contained 19 passes each in Chromium, Firefox and WebKit, no failures, skips or flaky tests. Three Trivy JSON reports contained zero HIGH or CRITICAL findings. All 17 stages exited 0 for the same source SHA, with no abort marker, and the disposable stack cleanup was logged. The evidence review found no blocking G3 discrepancies. The review does not establish production deployment readiness or broad vulnerability-free status outside the configured scans.

**Acceptance decision:** G3 explicitly accepted on 2026-10-31. **Integration:** M3 fast-forward integrated into `main` with `[skip ci]` commits, without executing GitHub Actions. **Next gate:** M4/G4 remains planned, not started. D-003 container identifier policy remains pending.
