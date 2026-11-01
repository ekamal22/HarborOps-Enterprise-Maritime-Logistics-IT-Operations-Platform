# M4 Implementation Status

**Branch:** `feat/m4-voyage-container-references`  
**Base:** Accepted M0-M3 on `main`; G3 accepted  
**State:** M4-01 through M4-04 implemented, independently verified and accepted on 2026-11-01, integrated into `main`.
**Decision:** D-003 Option B accepted (ISO 6346:2022, freight U, tagged synthetic identifiers).
**Verification:** Final tested revision `600d7c787da9f9cd4a70225cb3e3c38c093c0dc6`; [executed G4 evidence](M4_EXECUTED_VERIFICATION.md).
**Integration:** Fast-forward into `main`, with later documentation-only acceptance commits.

## Scope committed

| Work package | Implemented source | Verification status |
| --- | --- | --- |
| M4-01 | Maritime policy for ISO checksum, synthetic IDs, equipment types, voyage time/progression; new M4 permissions | D-003 Option B accepted; domain and checksum tests passed |
| M4-02 | `VoyageRecord`, tenant-composite vessel/port foreign keys, UTC planned/actual windows, progress, scoped API, optimistic version, archive/restore | Executed at `f1854c3`: zero-warning build, 92/92 fresh and 92/92 repeated integration, voyage browser workflows in three engines, outage and recovery. Acceptance pending |
| M4-03 | `ContainerRecord`, ISO and synthetic validation, PostgreSQL checksum function, format/uniqueness checks, scoped API and archive/restore | Executed at `f1854c3`: fresh and repeated integration 92/92 including the per-invocation identifier correction, container browser workflows in three engines. Acceptance pending |
| M4-04 | `MaritimeWorkspace` React UI, selected organization integration, parent selection, filter/cursor, three-engine browser specs, E:-scoped runner and evidence index | Executed at `f1854c3`: production build and 31/31 browser tests in each of Chromium, Firefox and WebKit. Acceptance pending |

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

## Implemented tests and executed local verification

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
- The original G4 evidence index correctly recorded the then-pending D-003 policy as incomplete. D-003 Option B and G4 were subsequently explicitly accepted. See [executed verification](M4_EXECUTED_VERIFICATION.md); the historical index remains unchanged.

## Accepted G4 closure

All executable G4 stages passed on `600d7c787da9f9cd4a70225cb3e3c38c093c0dc6`; the evidence-stage nonzero exit at execution time was solely the pending D-003 policy decision, subsequently approved. The independent review verified source provenance, 149 listed hashes, fresh/repeated .NET results, browser engines, operational recovery, scans and cleanup. The [executed verification record](M4_EXECUTED_VERIFICATION.md) is authoritative for the gate closure.

No M5 shipment state was added to M4. A future independently integrated M5 schema requires combined regression before M6 work. Retain historical failed run notes below as investigation history, not current blockers.

## Fifth G4 local execution (2026-11-01)

At `f1854c3fb684d576d6b16d94d5cb44113bd25f64` the complete runner passed: decision-unit 4/4, schema-index, preflight, PostgreSQL image, zero-warning Release build, unit 82/82, contract 12/12, fresh integration 92/92, repeated integration 92/92, dependency review, web build, documentation checks, 31/31 browser tests in each of Chromium, Firefox and WebKit, M3 and M4 outage and recovery, scoped recovery and Trivy scans of the postgres, api and web images with no HIGH or CRITICAL findings. The evidence index lists G4-S02 to G4-S25 with evidence present for review. G4-S01 is incomplete only because D-003 acceptance is pending.

Corrections made, with root causes and mutation checks, are in the [fifth correctness audit](M4_FIFTH_CORRECTNESS_AUDIT.md): a stranded list after choosing the active tab during the first load (product), a container cleanup filter defeated by Git Bash path conversion, a runner that hid later browser failures, browser specs that reused fixed identifiers or relied on list position, and a new queued revocation case set for G4-S14.

## Third G4 local execution and repeatability correction (2026-11-01)

At `bbb672a5b0ab094c1ae2c21d50ec5faf0101cbee`, decision-unit 4/4, schema-index, preflight, PostgreSQL image, clean .NET Release build, unit 82/82, contract 12/12, and fresh integration 87/87 passed. Repeated integration on the same disposable database passed 86/87. The sole failure was `MaritimeGateCoverageTests.IdenticalContainerIdentifierIsAllowedInDifferentOrganizations`: hardcoded `SYN-BOX-SHAREDG4` accumulated four matching rows from two runs, violating a test assertion expecting two. The test now generates a valid unique identifier per invocation but intentionally reuses that identifier across exactly two distinct fixture organizations; the selected tenant's API search must still return exactly its own record. See [fourth correctness audit](M4_FOURTH_CORRECTNESS_AUDIT.md).

The corrected source has not been executed locally. Stages after `dotnet`, including web, browser, outage/recovery, scans and final G4 evidence, remain unexecuted for this run. G4 OPEN, D-003 pending, acceptance pending.

## Second G4 local execution and schema correction (2026-11-01)

The next E:-backed run at `fa46c86fd6e1328d54d9d37da190a8e8d42e8e9d` passed 4 decision-unit checks, preflight, rootless PostgreSQL image build, a warning-free .NET Release build, all 82 unit tests and all 12 contract tests. Both 87-case integration executions failed 86 cases at migration setup due to `PendingModelChangesWarning`. The model comparison identified exactly three omitted voyage composite-FK indexes in the M4 snapshot.

The live model and migration snapshot now explicitly map the same three indexes with the exact physical names already created by the M4 SQL migration. The local runner now executes `schema-index` before .NET checks, and G4-S04/S25 require it. See [third correctness audit](M4_THIRD_CORRECTNESS_AUDIT.md) for evidence, changes and remaining checks. No corrected-SHA runtime execution or G4 acceptance is claimed.

## First G4 local execution attempt (2026-11-01)

The E:-backed local run at `7d0dd3e60049d5e831dd1cee1493cb6a2d32f74c` completed the four decision-unit tests, environment preflight, and rootless PostgreSQL image build. The `dotnet` stage aborted with exit code 11 after two xUnit analyzer errors in `MaritimeRevocationInterleavingTests.cs`: `xUnit2020` at line 31 and `xUnit1030` at line 93. The errors were corrected by replacing `Assert.True(false, message)` with `Assert.Fail(message)` and removing `ConfigureAwait(false)` from the test cleanup. This source correction was committed as `34257e295888a249522574fe958f9e5d76a154de`.

The run stopped before full .NET build success, PostgreSQL integration execution, browser runs, recovery checks, scans, and G4 evidence indexing. Its raw console and stage logs remain in the local `E:\\HarborOps-Verification\\evidence` and `logs` directories. A complete fresh verification at the corrected final SHA is still required. G4 remains OPEN, acceptance pending.
