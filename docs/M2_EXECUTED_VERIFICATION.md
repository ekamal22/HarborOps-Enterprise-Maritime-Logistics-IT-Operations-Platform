# M2 Executed Verification (G2)

**Date:** 2026-10-31  
**Milestone and gate:** M2 Organizations and role isolation, gate G2  
**Verified source revision:** `fbddc1361d79b027275bb53b48133ba0dc7d5cb1` (complete local G2 execution; later documentation and acceptance-only commits did not modify the tested implementation). Merged into `main` via [PR #5](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/5) at `1df638d5300191f6c2c6c9d201fd5f4c4ab9e562`.
**Verification method:** complete local execution on the Windows development machine, with every controllable path on E:. No GitHub Actions run was started after the local-only restriction was issued.  
**Result:** every mandatory scenario G2-S01 through G2-S25 passed on the verified revision.  
**Gate state at the time of G2:** G2 verified locally and explicitly accepted on 2026-10-31; M2 merged into `main` via PR #5. M3 was then planned. **Subsequent status:** M3 was later verified locally, explicitly accepted on 2026-10-31 and fast-forward integrated into `main`. See [G3 executed verification](M3_EXECUTED_VERIFICATION.md) and [current status](STATUS.md).

Commits after the verified revision change documentation only. The planning consistency checks were rerun on the final documentation commit (see [Documentation commits](#documentation-commits)).

## Environment

| Item | Value |
| --- | --- |
| Operating system | Microsoft Windows 11 (10.0.26200), 12 logical CPUs available to Docker, 8 GB Docker VM memory |
| .NET | SDK 10.0.403 from `mcr.microsoft.com/dotnet/sdk:10.0` (the host has only SDK 8.0.423 and 9.0.200; a .NET 10 SDK was deliberately not installed because the installer writes to C:) |
| Node.js and npm | v24.20.0, npm 11.19.0 |
| Docker | Engine 29.6.2, Compose v5.3.1, Docker Desktop with its WSL distribution and image store on E: |
| PostgreSQL | 16.15 (hardened rootless image built from `docker/postgres/Dockerfile`) |
| Playwright | 1.64.1 with Chromium (build 1248), Firefox (build 1555) and WebKit (build 2370), installed under E: |
| Image scanner | Trivy 0.76.1 (`aquasec/trivy:0.76.1`), vulnerability database cached on E: |
| Git | 2.39.1.windows.1 |

## Storage configuration (E: only)

All project-controllable storage was set before any build or test and is enforced by `scripts/verify-m2-local.sh`, which refuses to run if a temporary or cache location does not resolve to E:.

| Location | Value |
| --- | --- |
| `TEMP`, `TMP`, `TMPDIR` | `E:\HarborOps-Verification\tmp` |
| `DOTNET_CLI_HOME` | `E:\HarborOps-Verification\dotnet-home` |
| `NUGET_PACKAGES`, `NUGET_HTTP_CACHE_PATH` | `E:\HarborOps-Verification\nuget\packages`, `E:\HarborOps-Verification\nuget\http-cache` |
| `NPM_CONFIG_CACHE` | `E:\HarborOps-Verification\npm-cache` |
| `PLAYWRIGHT_BROWSERS_PATH` | `E:\HarborOps-Verification\playwright-browsers` |
| `XDG_CACHE_HOME`, `GIT_TMPDIR` | `E:\HarborOps-Verification\xdg-cache`, `E:\HarborOps-Verification\git-tmp` |
| `DOCKER_CONFIG` | `E:\HarborOps-Verification\docker-config` |
| Docker data and Compose volumes | Docker Desktop WSL distribution at `E:\APP IDEAS\New folder\DockerDesktopWSL` (`docker_data.vhdx`, `ext4.vhdx`); `docker info` reports `/var/lib/docker` inside that E: distribution |
| Repository and worktree | Disposable worktree within `E:\APP IDEAS\HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform` (E:-backed storage; revision and command recorded below) |
| Evidence | `E:\HarborOps-Verification\evidence` |

Residual C: activity, stated plainly rather than as a claim of zero C: I/O:

- Tool binaries (Node.js, Git, Docker CLI) are read from C:. They are installed there and were not installed or modified.
- Docker Desktop's own application logs under `C:\Users\<user>\AppData\Local\Docker\log` rotated during the session. These are written by the Docker Desktop application, outside project control.
- `C:\Users\<user>\.docker` received small updates (`daemon.json`, `windows-daemon.json`, `buildx\current`) from Docker Desktop and the buildx plugin.
- **The WSL crash reporter wrote memory dumps to `C:\Users\<user>\AppData\Local\Temp\wsl-crashes` during earlier iterations of this verification** (10 files, about 1.5 GB, are present now; the system may have rotated older ones). Cause and correction: see failure 7 below. They were not deleted because existing C: content must not be modified, and they can be removed by the user. After the correction, the final complete run created no new files there (the newest file predates the final run).

## Executed commands

All stages were executed in one invocation of the committed runner against the verified revision:

```text
bash scripts/verify-m2-local.sh preflight dotnet deps web docs stack accounts organizations session browsers recovery scan
```

Evidence log: `E:\HarborOps-Verification\evidence\final-fbddc1361d79b027275bb53b48133ba0dc7d5cb1.log` (started 07:14:43, finished 07:20:42, exit code 0). Test result files (`.trx`) are under `E:\HarborOps-Verification\evidence\trx`.

| Stage | What it runs |
| --- | --- |
| `dotnet` | Restore and Release build of `HarborOps.sln`, then unit, contract, fresh PostgreSQL integration and repeated PostgreSQL integration suites in the .NET 10 SDK image against a throwaway PostgreSQL 16.15 container (database `harborops_integration`). The Compose volumes are never used. |
| `deps` | `dotnet list package --vulnerable --include-transitive` for the whole solution; `npm audit` for `web` and `tests/browser`. |
| `web` | `npm ci`, frontend unit tests and the TypeScript and Vite production build. |
| `docs` | `render-execution-graph.mjs --check` and `check-planning-docs.mjs`. |
| `stack` | Clean disposable Compose project `ho-verify` (`down --volumes`, then build and start), `smoke-compose.sh` (M0 regression and image revision labels) and `check-m0-degraded.sh` (PostgreSQL outage and recovery). |
| `accounts`, `organizations` | Provision the synthetic accounts, bootstrap two organizations, and assert that every rejected restricted operator command exits cleanly with code 1 and writes nothing. |
| `session` | `check-m1-session-recovery.sh`: cookie survives API restart, 503 during outage, recovery, logout-all and replay denial. |
| `browsers` | Playwright, three engines, against the Compose stack. |
| `recovery` | `check-m2-scoped-recovery.mjs`: authenticated organization access across a real PostgreSQL stop and start, with durable revocation. |
| `scan` | Revision-label check and Trivy HIGH and CRITICAL scan of the three running images. |

## Suite results on the verified revision

| Suite | Result |
| --- | --- |
| Release build (0 warnings, warnings are errors) | PASS |
| Unit tests | PASS: 22 passed, 0 failed, 0 skipped |
| API contract tests | PASS: 12 passed, 0 failed, 0 skipped |
| Fresh PostgreSQL integration tests | PASS: 32 passed, 0 failed, 0 skipped |
| Repeated PostgreSQL integration tests (already migrated and seeded database) | PASS: 32 passed, 0 failed, 0 skipped |
| Frontend unit tests and production build | PASS: 6 passed; build succeeded |
| Planning graph and document consistency | PASS: 25 milestones, 126 graph nodes, 32 Markdown files |
| Compose startup and M0 regression (`smoke-compose.sh`) | PASS |
| PostgreSQL outage and recovery, M0 (`check-m0-degraded.sh`) | PASS |
| Account provisioning (7 accounts), organization bootstrap (2 organizations) | PASS |
| Restricted operator command denial (account CLI without opt-in, organization CLI without opt-in, unknown account, duplicate slug), each exit code 1 and no rows written | PASS |
| M1 session restart, outage, logout-all and replay (`check-m1-session-recovery.sh`) | PASS |
| Browser tests | PASS: 24 passed (8 specs on each of Chromium, Firefox and WebKit) |
| Authenticated organization outage, recovery and durable revocation | PASS: recovered on attempt 2 of 30, after 1 sanitized 503 |
| Running image revision labels equal the source revision (postgres, api, web) | PASS |
| Trivy HIGH and CRITICAL scan, postgres image `sha256:408d1e05...` | PASS: no findings |
| Trivy HIGH and CRITICAL scan, api image `sha256:592d3fd3...` | PASS: no findings |
| Trivy HIGH and CRITICAL scan, web image `sha256:d7e9c1a6...` | PASS: no findings |
| NuGet vulnerability listing (all projects, transitive) | PASS: none reported |
| `npm audit` (`web`, `tests/browser`) | PASS: 0 vulnerabilities each |

Scan reports: `E:\HarborOps-Verification\evidence\trivy\{postgres,api,web}.json`.

## G2-S01 through G2-S25 outcomes

Test names are `Class.Method` in `tests/HarborOps.IntegrationTests` unless another project is named. Browser specs are in `tests/browser/specs` and ran on all three engines.

| ID | Outcome | Executed evidence |
| --- | --- | --- |
| G2-S01 | PASS | `MigrationSnapshotTests` (model snapshot equals the entity model; migrations discovered in order); `BaselinePersistenceTests.MigrationAndSeed_CanRunTwiceWithoutDuplicatingRecord`; the fresh integration run migrates an empty database; the Compose migrator runs on a new volume with no pending migrations. |
| G2-S02 | PASS | `OrganizationUpgradeAndConstraintTests.RealM0M1M2UpgradePreservesLegacyAccountSessionAndBaselineOnRepeat` (real M0 to M1 to M2 upgrade in a separate database; legacy account, credential, session and baseline row keep working; repeat migration and seed leave one copy of each); `IdentitySecurityTests.M0SeedUpgradesToM1WithoutLosingBaseline`; the repeated integration run. |
| G2-S03 | PASS | `OrganizationAdversarialTests.DatabaseRejectsInvalidRoleStatusVersionAndDuplicateMembership`; `OrganizationUpgradeAndConstraintTests.InvalidForeignKeysDuplicateSlugAndDisabledBootstrapAreRejected` (SQLSTATE 23505, 23503 and 23514 asserted). |
| G2-S04 | PASS | `OrganizationAdversarialTests.BootstrapRejectsUnknownAndDuplicateAccountsWithoutPartialWrites`; the disabled-account case in `InvalidForeignKeysDuplicateSlugAndDisabledBootstrapAreRejected`; runner stage `organizations` (non-opt-in, unknown account and duplicate slug rejected with exit code 1; zero rows for the rejected slugs; two valid organizations created). |
| G2-S05 | PASS | `HarborOps.UnitTests` `OrganizationPolicyTests` (role parsing for all five roles and invalid inputs, `EveryRoleCanViewOnlyTheOrganizationSummary`, slug rules); the HTTP role matrix under G2-S12 confirms no implicit shipment or global access. |
| G2-S06 | PASS | `OrganizationBoundaryMatrixTests.AnonymousCallersAreRejectedOnEveryScopedEndpointAndVerb` (all 10 scoped endpoint and verb combinations, with and without a CSRF token, return 401); `OrganizationHttpLifecycleTests.RevokedAndDisabledCookiesCannotReadOrganizationEndpoints`; `HarborOps.ContractTests` `AuthenticationSecurityTests`; M1 session script. |
| G2-S07 | PASS | `OrganizationBoundaryMatrixTests.MissingAndForeignSelectionsDenyEveryVerbWithoutEnumeration` (membership listing works; four organization identifiers times six verbs all return 409 `organization_selection_required`). |
| G2-S08 | PASS | `OrganizationIsolationTests.TwoOrganizationsHaveIndependentSelectionsAndLiveRoleChanges`; `OrganizationHttpTests.HttpPipelineEnforcesSelectedOrganizationAndMemberRole`; the same account is Administrator in one organization and Analyst in another in `OrganizationBoundaryMatrixTests`. |
| G2-S09 | PASS | `OrganizationAdversarialHttpTests.SameAccountSessionsSelectIndependentlyAndSuspensionFailsClosed`; browser spec `organization-revocation` (separate browser contexts). |
| G2-S10 | PASS | `OrganizationBoundaryMatrixTests.MissingAndForeignSelectionsDenyEveryVerbWithoutEnumeration` (three foreign organization targets times six verbs return 404 with the same envelope, no foreign names, and a foreign selection returns the same envelope as a nonexistent one); `OrganizationHttpLifecycleTests.MalformedRequestsAndForeignIdsNeverLeakOrganizationData`. |
| G2-S11 | PASS | `OrganizationAdversarialHttpTests.AllNonAdministratorsAreDeniedAdministrationAndForeignIds` (a real foreign member identifier cannot be read, changed or removed through either organization route). |
| G2-S12 | PASS | `OrganizationAdversarialHttpTests.AllNonAdministratorsAreDeniedAdministrationAndForeignIds` (Analyst, Manager, Support and Auditor can read the organization summary and receive 403 for listing, member read, profile change, grant and removal). |
| G2-S13 | PASS | `OrganizationAdversarialHttpTests` and `OrganizationBoundaryMatrixTests` (an Administrator of A selecting A receives 404 for B on every verb). |
| G2-S14 | PASS | `OrganizationAdversarialTests.SuspensionAndLiveRoleRevocationInvalidateCurrentSessions`; `OrganizationBoundaryMatrixTests.RemovedMemberIsDeniedImmediatelyAndReinstatementRequiresAnExplicitGrant` (removal clears the selection, listing and reselection are denied, stale and blind reinstatement conflict, explicit reinstatement restores access); browser spec `organization-revocation` (the member's page clears organization data after a suspension); recovery script (removal denied after outage). |
| G2-S15 | PASS | `OrganizationAdversarialHttpTests.SameAccountSessionsSelectIndependentlyAndSuspensionFailsClosed` (selection, context, read and reselection denied for a suspended organization while the other session continues); `OrganizationAdversarialTests`. |
| G2-S16 | PASS | `IdentitySecurityTests.SelfDisableEndpointRevokesSessionAndRejectsFutureLogin`; `OrganizationFinalInvariantsTests.ConcurrentAccountDisablementCannotEraseBothAdministrators`; `OrganizationIsolationTests.MissingAndStaleMembershipCannotAlterAdministrator`; browser spec `organizations` (sole administrator self-disable denied with 409). |
| G2-S17 | PASS | `OrganizationAdversarialTests.ConcurrentAdministratorsCannotDemoteEachOtherAndLeaveZeroAdministrators` (two independent contexts); `OrganizationBoundaryMatrixTests.ConcurrentDuplicateGrantsAndMutualRemovalsLeaveOneWinnerAndAnAdministrator` (concurrent duplicate grants and concurrent mutual removals each leave exactly one winner and one active Administrator). |
| G2-S18 | PASS | `OrganizationFinalInvariantsTests.StaleWritesAndRepeatedGrantsCannotSilentlyEscalate`; the concurrent duplicate grant in `OrganizationBoundaryMatrixTests` (one 200, one 409, one membership row); stale reinstatement in `RemovedMemberIsDeniedImmediately...`. |
| G2-S19 | PASS | `OrganizationBoundaryMatrixTests.MalformedBodiesIdentifiersAndQueriesFailValidationWithoutWrites` (malformed JSON, wrong types, missing bodies, invalid role and status, version below 1, control characters, 161 and 100,000 character names, invalid identifiers on every route, invalid paging queries: all 4xx, bounded, no exception text, no writes); `OrganizationAdversarialHttpTests` (missing CSRF and foreign Origin return 403); `OrganizationHttpLifecycleTests`. |
| G2-S20 | PASS | `check-m0-degraded.sh`; `check-m1-session-recovery.sh`; `check-m2-scoped-recovery.mjs` (real PostgreSQL stop: sanitized 503 for organization routes; after restart, access recovers with the correct organization; the revoked member stays denied with 404 and 409). See [Outage recovery analysis](#outage-recovery-analysis). |
| G2-S21 | PASS | `IdentityLifecycleTests` (provision, login, revocation, disablement, password reset revokes sessions); `IdentitySecurityTests` (idle and absolute expiry cannot be revived, self-disable); M1 session script; browser spec `auth` (sign in, reload, log out, generic failure). |
| G2-S22 | PASS | Browser spec `organizations` (selection, reload, last-administrator denial, clearing the selection with the server-side clear awaited) and `organization-revocation` (two roles for one user across two organizations, empty state, role-aware controls, protected state cleared on suspension, recovery by selecting the remaining organization), on Chromium, Firefox and WebKit. |
| G2-S23 | PASS | `OrganizationLogRedactionTests.RejectedRequestsAndDependencyFailuresNeverLeakSecretsToResponsesOrLogs` (passwords, session and antiforgery cookie values, CSRF tokens, account emails, a foreign organization name and a connection secret never appear in responses or captured logs, including the logged dependency failure with an unreachable database); identical 404 envelopes for foreign and nonexistent identifiers; the recovery script checks the API service log for the synthetic credential. |
| G2-S24 | PASS | `ApiContractTests`, `ApiAgainstPostgresTests`, `BaselineJsonTests`; `smoke-compose.sh`; browser specs `baseline`, `regression` and `auth`. |
| G2-S25 | PASS | `compose.yaml`, `docker/`, every `.csproj`, `Directory.Build.props`, `global.json` and both Node package manifests and lock files are unchanged from `main` (`git diff --stat origin/main..HEAD` over those paths lists only the M1 workflow change below); no new service, port or paid dependency; Trivy and dependency results above. |

The only difference from `main` among deployment and dependency files is `.github/workflows/verify-m1.yml`, which was reduced to manual dispatch on this branch so the M2 workflow does not duplicate pull request jobs.

## Outage recovery analysis

Run #17 failed because the authenticated organization endpoint returned 503 immediately after PostgreSQL restarted although `/health/ready` already returned 200. The API log of the local reproduction shows the cause: PostgreSQL shutdown terminates every pooled connection (`57P01 terminating connection due to administrator command`), and the readiness probe, the session validation and the organization query each take a connection from the same Npgsql pool. After the restart a request that draws a connection that was already terminated fails once, the pool discards that connection, and the next request opens a fresh one. The failing request is converted to a sanitized 503 (`dependency_unavailable`, no provider text), so the behavior is fail-closed and self-healing, not a persistent defect, and no authorization decision is made from stale data.

Commit `5cc2966` made the recovery script tolerate only sanitized 503 responses for a bounded 30 attempts while still requiring HTTP 200 with the correct organization identifier. The script now also prints how many sanitized 503 responses preceded recovery. Local result on the verified revision: recovered on attempt 2 of 30 after one sanitized 503. Revocation was durable: after recovery the removed member received 404 on reselection and 409 on organization reads.

## Failure history and corrections

1. **GitHub run 17 (outage recovery 503).** Root cause above. Correction: `5cc2966`, then locally verified.
2. **GitHub run [41163803861](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41163803861) on `5cc2966`** (a manual run started during the initial audit, before the local-only restriction; it was not touched afterwards and no further runs were started). It failed in two browser tests, which skipped the recovery step and the image scans: the Chromium revocation spec asserted the administrator's select value, which is local interface state, before the server had persisted the change; the Firefox organization spec read the server selection immediately after an asynchronous clear. Both were test races, not product defects. Correction: the specs now wait for the server response.
3. **New test assertion.** `ConcurrentDuplicateGrantsAndMutualRemovals...` first expected only `Revoked` or `Conflict` for the losing administrator. The loser is correctly denied with `SelectionRequired` because the winner's removal clears the loser's session selection. The assertion was widened to the three denial outcomes. The security properties asserted (exactly one winner, one active Administrator survives) were unchanged.
4. **Harness defect: global `MSYS_NO_PATHCONV`.** The local runner exported it, which stopped Git Bash translating `/dev/null`, so the repository scripts' `curl --output /dev/null` failed (`curl: (23)`), producing a false `/health/live` timeout and a false session-stage failure. The variable is now set only on container volume commands.
5. **Harness defect: missing environment in manual reruns.** Some manual reruns omitted `HARBOROPS_DB_PASSWORD` and failed at Compose interpolation. The runner always sets it.
6. **Browser timing under load.** One Firefox run signed in successfully (API response about 1.6 s) but rendered the signed-in view about 8.5 s later while three engines shared the machine, exceeding the 10 s assertion. Correction: the sign-in helpers assert the login response status explicitly and allow a bounded 30 s for the post-login render. All other expectations keep their original timeouts.
7. **Product hardening: restricted CLI aborted on rejection.** A rejected `--account-cli` or `--organization-cli` operation threw out of top-level code, which aborts a .NET process. The WSL crash reporter then wrote a memory dump of the process to C: for each rejected command (three per run, 100 to 180 MB each). A dump of the account command could also contain the password just entered. Correction: `Program.cs` now runs both commands through a handler that prints a sanitized message and exits with code 1, and the runner asserts exit code 1 for four rejected commands. `--ulimit core=0` and a verification-only Compose override on E: were also added for every verification container. The final run created no new dump files.

## Remaining limitations

None blocking G2. Observations:

- `xunit` 2.9.2 is reported as a legacy package by `dotnet list package --deprecated` (suggested successor `xunit.v3`). It is a test-only dependency and has no advisory.
- The M2 workflow `.github/workflows/verify-m2.yml` was not changed and was not executed against the final revision. Its operator-command denial step only requires a non-zero exit; the stricter exit code 1 assertion exists only in the local runner.
- Dedicated accessibility verification is M24-04 and historical retained-volume checks are M6, M12, M18 and M24-03; neither is part of G2.
- No public deployment, production certification or production data was involved.

## Documentation commits

The verified revision contains all source, tests and scripts. Documentation commits follow it and modify only files under `docs/` and `README.md`. After the last documentation commit, `render-execution-graph.mjs --check` and `check-planning-docs.mjs` were run again. Their result is recorded in the commit history and in `E:\HarborOps-Verification\evidence\docs-final.log`.

## Earlier GitHub evidence (historical)

These runs were executed on the pull request before the local-only restriction and are retained for audit. They are not the basis of the G2 result above.

| Run | Source | Outcome |
| --- | --- | --- |
| [41157387340](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41157387340) | `65304b7351d8999ac89008828d326158bb18d3b7` | Passed: 22 unit, 12 contract, 13 and 13 integration, 6 frontend, 24 browser, image scans |
| [41158772090](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41158772090) | `f7308216f1bdab67a860114b1613f81402c6308d` | Passed: 22 unit, 12 contract, 19 and 19 integration, 6 frontend, 24 browser, image scans |
| [41162808671](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41162808671) (run 17) | pull request head before `5cc2966` | Failed at the authenticated outage recovery step; scans skipped |
| [41163803861](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41163803861) (run 18) | `5cc296697eab23de83c3395509e1c0fd5da00629` | Failed in two browser tests; recovery step and scans skipped |
