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
| M4-02 | `VoyageRecord`, tenant-composite vessel/port foreign keys, UTC planned/actual windows, progress, scoped API, optimistic version, archive/restore | Source committed, .NET/SQL/HTTP execution not performed |
| M4-03 | `ContainerRecord`, ISO and synthetic validation, PostgreSQL checksum function, format/uniqueness checks, scoped API and archive/restore | Source committed, .NET/SQL/HTTP execution not performed |
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

## Tests and local verification authored, not executed

- Unit: `MaritimePolicyTests.cs`
- Integration: `MaritimeHttpTests.cs`, `MaritimeBoundaryTests.cs`, `MaritimeMigrationTests.cs`, `MaritimeConcurrencyTests.cs`, `MaritimeGateCoverageTests.cs`, `MaritimeUpgradeAndSecurityTests.cs`, `MaritimeSqlInvariantTests.cs`, `MaritimeRevocationInterleavingTests.cs`
- Browser: `maritime.spec.mjs`, `maritime-races.spec.mjs`, `maritime-gate.spec.mjs`
- Fixture: `scripts/prepare-m4-browser-fixtures.mjs`
- Runner: `scripts/verify-m4-local.sh`, `scripts/check-m4-maritime-outage.mjs`, `scripts/assert-g4-evidence.mjs`
- Existing M0-M3 migration regression test expectations updated for the fifth migration.
- [M4 correctness audit](M4_CORRECTNESS_AUDIT.md) records eleven source findings and the 25-case unexecuted test trace.
- Source corrections prevent combined archive-and-edit SQL transitions, no-op container edits and stale page repaint, and load parent references beyond the first 100.
- G4 evidence index is required to fail on missing executed evidence and pending D-003, without implying acceptance.

## Outstanding blockers and limitations

1. **No .NET build:** Current execution environment lacks the .NET 10 SDK. Compile-time correctness of the newly added C# services, mappings, API handlers and tests has not been established.
2. **No PostgreSQL/Docker execution:** Fresh and historical upgrades, rollback, real SQL constraints, migrations and outage/recovery have not been executed. The accepted M3 databases have not been touched.
3. **No browser build/runtime:** React TypeScript compilation, actual browser workflows and concurrency are not verified. Chromium, Firefox and WebKit results have not been produced.
4. **G4 scenario coverage incomplete:** The expanded but unexecuted test suite still requires full scenario/subcase reconciliation against the 25-case [G4 verification contract](M4_VERIFICATION_PLAN.md), including comprehensive hostile-input fuzzing and every timing-sensitive browser/data race. Absolute expiry and a lock-observed queued-write test have been authored, but have not executed. Existing coverage is not equivalent to 25/25 PASS.
5. **D-003 open:** The ISO 6346 and synthetic kind policy is a provisional implementation assumption; it must be explicitly accepted or revised before G4.
6. **Parallel migrations:** Confirm the chosen migration number does not collide with an independently implemented M5 workstream before integrating.
7. **Runtime source SHA:** The final integrated revision must pass fresh/repeat M4, accepted M0-M3 regressions, three browsers, live PostgreSQL outage and security/dependency scans on disposable E:-backed storage.
8. **Snapshot model:** EF snapshot was updated by source editing and must pass the actual `MigrationSnapshotTests.ModelSnapshot_MatchesCurrentEntityModel` test. No model-snapshot correctness claim is made.
9. **Error handling and concurrency:** A static source review cannot substitute for deterministic interleaving tests, SQL lock monitoring and final row-state assertions.

## Safe next execution

On the authorized E:-backed Windows worktree, inspect [Local Storage Policy](LOCAL_STORAGE_POLICY.md), confirm the active Docker Desktop backing storage is E:, and run `bash scripts/verify-m4-local.sh` after the environment variables and disposable project are configured. Do not dispatch GitHub Actions. The runner is not certified as complete until all mandatory scenario subcases are mapped to real results, errors repaired and the entire matrix rerun on the final source SHA.

**Exit criterion:** G4-S01 through G4-S25 genuine PASS, independent raw-evidence review, D-003 accepted and explicit G4 acceptance. Until then M4 remains implementation in progress, unverified and not merged.
