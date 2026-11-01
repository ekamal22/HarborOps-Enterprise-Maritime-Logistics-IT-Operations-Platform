# M4 Third Correctness Audit: EF Model and Migration Alignment

**Date:** 2026-11-01
**Feature branch:** `feat/m4-voyage-container-references`
**Failed local source SHA:** `fa46c86fd6e1328d54d9d37da190a8e8d42e8e9d`
**Gate:** G4 OPEN, acceptance pending

## Observed E:-backed verification evidence

The uploaded second G4 run completed decision-unit (4/4), preflight, rootless PostgreSQL image build, .NET Release compilation (zero warnings/errors), unit tests (82/82), and HTTP contract tests (12/12). Both fresh and repeated integration batches failed 86 of 87 cases with the same `PendingModelChangesWarning` during EF migration validation. `MigrationSnapshotTests.Migrations_AreDiscoveredInOrder` was the passing integration case; `ModelSnapshot_MatchesCurrentEntityModel` identified the actual difference.

The snapshot comparison reported **three** model differences, each a `CreateIndexOperation` for `voyages`:

- `IX_voyages_organization_id_vessel_id`
- `IX_voyages_organization_id_origin_port_id`
- `IX_voyages_organization_id_destination_port_id`

These failures were one underlying migration metadata defect, not 86 independent functional regressions. No runtime integration, browser, dependency-outage or image-scan acceptance evidence was established by that run.

## Correction

The existing M4 SQL migration already creates the three equivalent composite foreign-key indexes under the repository's explicit canonical names:

- `IX_voyages_OrganizationId_VesselId`
- `IX_voyages_OrganizationId_OriginPortId`
- `IX_voyages_OrganizationId_DestinationPortId`

The live `HarborOpsDbContext` and `HarborOpsDbContextModelSnapshot` now explicitly configure all three indexes and their canonical database names. This makes the index contract independent of convention-only creation in the live EF model and aligns the three surfaces: live model, snapshot and physical migration. No new migration was added because the physical indexes were already created by M4's migration. No EF warning was suppressed.

A new `scripts/check-g4-index-contract.mjs` checks the three mappings and SQL index names. The local runner executes it as a `schema-index` stage before .NET testing. The G4 evidence index requires this stage for G4-S04 and G4-S25. `MigrationSnapshotTests.ModelSnapshot_MatchesCurrentEntityModel` and the database's own pending-model check remain mandatory.

## Correctness audit checks

| Check | Outcome |
| --- | --- |
| Three indexes appear in the live EF model with explicit database names | Source checked |
| Three indexes appear in the snapshot with matching mappings | Source checked |
| Three names match the M4 PostgreSQL migration | Source checked |
| Non-index model configuration in the live context and snapshot matches | Source checked |
| G4 evidence requires static index-contract precheck | Source checked |
| EF pending-model warning has not been suppressed | Source checked |
| Migration order and M0-M3 history retained | Source checked |
| Actual current-SHA EF drift check | **NOT RUN** |
| Fresh and repeated PostgreSQL integration tests on corrected SHA | **NOT RUN** |
| Three-engine browser, outage/recovery and security scans | **NOT RUN** |

The source audit is complete for the reported schema mismatch. Static alignment does not establish runtime PASS. If a new snapshot mismatch appears, the integration test prints model difference operation names and must be corrected in code, not ignored.

## Remaining verification

Re-run the complete E:-backed local G4 runner on a clean, updated M4 feature branch, preserving all evidence logs. A successful fresh and repeated database migration, authenticated HTTP integration, three-browser regression, outage/recovery, and image scan is required before independent evidence review.

D-003 remains an unaccepted decision. Until its explicit acceptance and a passing full G4 execution, the gate remains OPEN, acceptance pending. Do not dispatch GitHub Actions or merge to `main`.
