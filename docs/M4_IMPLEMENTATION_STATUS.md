# M4 Implementation Status

**Branch:** `feat/m4-voyage-container-references`  
**Base:** Accepted M0-M3 on `main`; G3 accepted  
**State:** M4 application source, expanded G4 tests and static correctness remediation committed; implementation correctness and complete G4 runtime evidence remain unverified. G4 OPEN, acceptance pending.  
**Decision:** D-003 remains a proposal. The branch temporarily implements the tagged `ISO6346`/`Synthetic` option described in [D-003 proposal](M4_D003_CONTAINER_IDENTIFIER_PROPOSAL.md), without asserting that option is accepted.  
**No merge:** M4 remains on the feature branch, not `main`.

## Scope committed

| Work package | Implemented source | Verification status |
| --- | --- | --- |
| M4-01 | Maritime policy for ISO checksum, synthetic IDs, equipment types, voyage time/progression; new M4 permissions | Proposed policy, tests authored, D-003 not accepted |
| M4-02 | `VoyageRecord`, tenant-composite vessel/port foreign keys, UTC planned/actual windows, progress, scoped API, optimistic version, archive/restore | Source committed; latest uploaded run passed the clean Release build, 82 unit, 12 contract, and all 87 fresh integration tests. Repeat suite 86/87 due to a corrected fixture isolation defect; complete G4 pending |
| M4-03 | `ContainerRecord`, ISO and synthetic validation, PostgreSQL checksum function, format/uniqueness checks, scoped API and archive/restore | Fresh integration passed. Repeat 86/87 failed because a test used the same identifier across both executions; corrected with a per-invocation identifier. Rerun pending |
| M4-04 | `MaritimeWorkspace` React UI, selected organization integration, parent selection, filter/cursor, three-engine browser specs, E:-scoped runner and evidence index | Source committed, browser execution not performed |

## Source inventory

- `src/HarborOps.Domain/Maritime/MaritimePolicy.cs`
- `src/HarborOps.Infrastructure/Maritime/MaritimeRecord.cs` and `MaritimeStore.cs`
- `src/HarborOps.Infrastructure/Persistence/HarborOpsDbContext.cs`
- `src/HarborOps.Infrastructure/Persistence/Migrations/20261101010000_VoyageContainerReferences.cs`
- `src/HarborOps.Infrastructure/Persistence/Migrations/HarborOpsDbContextModelSnapshot.cs`
- `src/HarborOps.Domain/Organizations/OrganizationPolicy.cs`
- `src/HarborOps.Infrastructure/DependencyInjection.cs`
- `src/HarborOps.Api/Maritime/MaritimeEndpoints.cs` and `Program.cs`
- `web/src/maritime/maritimeClient.ts` and `MaritimeWorkspace.tsx`
- `web/src/auth/OrganizationWorkspace.tsx`

## Tests authored and local verification partially attempted

- Unit: `MaritimePolicyTests.cs`
- Integration: `MaritimeHttpTests.cs`, `MaritimeBoundaryTests.cs`, `MaritimeMigrationTests.cs`, `MaritimeConcurrencyTests.cs`, `MaritimeGateCoverageTests.cs`, `MaritimeUpgradeAndSecurityTests.cs`, `MaritimeSqlInvariantTests.cs`, `MaritimeRevocationInterleavingTests.cs`
- Browser: `maritime.spec.mjs`, `maritime-races.spec.mjs`, `maritime-gate.spec.mjs`
- Fixture: `scripts/prepare-m4-browser-fixtures.mjs`
- Runner: `scripts/verify-m4-local.sh`, `scripts/check-m4-maritime-outage.mjs`, `scripts/assert-g4-evidence.mjs`
- Existing M0-M3 migration regression test expectations updated for the fifth migration.
- [M4 correctness audit](M4_CORRECTNESS_AUDIT.md) records eleven source findings and the 25-case unexecuted test trace.
- [Second correctness audit](M4_SECOND_CORRECTNESS_AUDIT.md) records six additional findings and their subsequent source remediations, including isolated foreign-key cases, D-003 decision gating, complete tenant-role fixtures, independent voyage-list loading and precise runtime assertions. Full .NET/PostgreSQL/browser execution remains pending.
- Source corrections prevent combined archive-and-edit SQL transitions, no-op container edits and stale page repaint, and load parent references beyond the first 100.
- [Fourth correctness audit](M4_FOURTH_CORRECTNESS_AUDIT.md) records the third execution: 87/87 fresh and 86/87 repeat integration, root cause, repeatability fix, and remaining runtime gates.
- G4 evidence index is required to fail on missing executed evidence and pending D-003, without implying acceptance.

## Outstanding blockers and limitations

1. **Latest full-source build passed:** The third E:-backed run at `bbb672a5b0ab094c1ae2c21d50ec5faf0101cbee` built with zero warnings and errors; the new test/runner revisions still require a clean rebuild.
2. **Integration rerun required:** PostgreSQL fresh integration passed 87/87 and repeated integration passed 86/87, failing on one fixed-identifier test. The source fix is committed but unexecuted. Live outage/recovery and remaining stages have not run.
3. **No browser build/runtime:** React TypeScript compilation, actual browser workflows and concurrency are not verified. Chromium, Firefox and WebKit results have not been produced.
4. **G4 scenario coverage incomplete:** Fresh integration passed, including the absolute-expiry and queued-write tests. The repeat run failed one test and was stopped before later stages. Full scenario/subcase reconciliation against all 25 [G4 cases](M4_VERIFICATION_PLAN.md) remains required; this is not 25/25 PASS.
5. **D-003 open:** The ISO 6346 and synthetic kind policy is a provisional implementation assumption; it must be explicitly accepted or revised before G4.
6. **Parallel migrations:** Confirm the chosen migration number does not collide with an independently implemented M5 workstream before integrating.
7. **Runtime source SHA:** The final integrated revision must pass fresh/repeat M4, accepted M0-M3 regressions, three browsers, live PostgreSQL outage and security/dependency scans on disposable E:-backed storage.
8. **Snapshot model:** The actual `MigrationSnapshotTests.ModelSnapshot_MatchesCurrentEntityModel` passed in fresh and repeated integration at `bbb672a5b0ab094c1ae2c21d50ec5faf0101cbee`. Repeat this check on the final source SHA.
9. **Error handling and concurrency:** A static source review cannot substitute for deterministic interleaving tests, SQL lock monitoring and final row-state assertions.

## Safe next execution

On the authorized E:-backed Windows worktree, inspect [Local Storage Policy](LOCAL_STORAGE_POLICY.md), confirm the active Docker Desktop backing storage is E:, and run `bash scripts/verify-m4-local.sh` after the environment variables and disposable project are configured. Do not dispatch GitHub Actions. The runner is not certified as complete until all mandatory scenario subcases are mapped to real results, errors repaired and the entire matrix rerun on the final source SHA.

**Exit criterion:** G4-S01 through G4-S25 genuine PASS, independent raw-evidence review, D-003 accepted and explicit G4 acceptance. Until then M4 remains implementation in progress, unverified and not merged.

## Third G4 local execution and repeatability correction (2026-11-01)

At `bbb672a5b0ab094c1ae2c21d50ec5faf0101cbee`, decision-unit 4/4, schema-index, preflight, PostgreSQL image, clean .NET Release build, unit 82/82, contract 12/12, and fresh integration 87/87 passed. Repeated integration on the same disposable database passed 86/87. The sole failure was `MaritimeGateCoverageTests.IdenticalContainerIdentifierIsAllowedInDifferentOrganizations`: hardcoded `SYN-BOX-SHAREDG4` accumulated four matching rows from two runs, violating a test assertion expecting two. The test now generates a valid unique identifier per invocation but intentionally reuses that identifier across exactly two distinct fixture organizations; the selected tenant's API search must still return exactly its own record. See [fourth correctness audit](M4_FOURTH_CORRECTNESS_AUDIT.md).

The corrected source has not been executed locally. Stages after `dotnet`, including web, browser, outage/recovery, scans and final G4 evidence, remain unexecuted for this run. G4 OPEN, D-003 pending, acceptance pending.

## Second G4 local execution and schema correction (2026-11-01)

The next E:-backed run at `fa46c86fd6e1328d54d9d37da190a8e8d42e8e9d` passed 4 decision-unit checks, preflight, rootless PostgreSQL image build, a warning-free .NET Release build, all 82 unit tests and all 12 contract tests. Both 87-case integration executions failed 86 cases at migration setup due to `PendingModelChangesWarning`. The model comparison identified exactly three omitted voyage composite-FK indexes in the M4 snapshot.

The live model and migration snapshot now explicitly map the same three indexes with the exact physical names already created by the M4 SQL migration. The local runner now executes `schema-index` before .NET checks, and G4-S04/S25 require it. See [third correctness audit](M4_THIRD_CORRECTNESS_AUDIT.md) for evidence, changes and remaining checks. No corrected-SHA runtime execution or G4 acceptance is claimed.

## First G4 local execution attempt (2026-11-01)

The E:-backed local run at `7d0dd3e60049d5e831dd1cee1493cb6a2d32f74c` completed the four decision-unit tests, environment preflight, and rootless PostgreSQL image build. The `dotnet` stage aborted with exit code 11 after two xUnit analyzer errors in `MaritimeRevocationInterleavingTests.cs`: `xUnit2020` at line 31 and `xUnit1030` at line 93. The errors were corrected by replacing `Assert.True(false, message)` with `Assert.Fail(message)` and removing `ConfigureAwait(false)` from the test cleanup. This source correction was committed as `34257e295888a249522574fe958f9e5d76a154de`.

The run stopped before full .NET build success, PostgreSQL integration execution, browser runs, recovery checks, scans, and G4 evidence indexing. Its raw console and stage logs remain in the local `E:\\HarborOps-Verification\\evidence` and `logs` directories. A complete fresh verification at the corrected final SHA is still required. G4 remains OPEN, acceptance pending.
