# M3 Implementation Register

**Milestone:** M3 Ports and vessels reference data
**Original feature branch:** `feat/m3-port-vessel-reference-data`; accepted M3 now integrated into `main`.
**Baseline:** G0-G3 accepted on 2026-10-31; M0-M3 integrated into `main`.
**Implementation state:** Source changes from both correctness audits committed and corrected during execution; complete local verification executed on `b72025d35d62d0a8a45b466986a0b3bd89eebadb`
**Gate:** G3 verified locally and explicitly accepted on 2026-10-31. All G3-S01 through G3-S25 scenarios passed on the verified source ([executed verification](M3_EXECUTED_VERIFICATION.md)).
**Merge:** Fast-forward integration of the accepted M3 feature branch into `main` on 2026-10-31; M4 has not started.

## Implemented source inventory

| Package | Source paths | Behavior |
| --- | --- | --- |
| M3-01 | `src/HarborOps.Domain/References/ReferencePolicy.cs`, `src/HarborOps.Domain/Organizations/OrganizationPolicy.cs` | Normalized immutable tenant-local codes; country and optional UN/LOCODE syntax; current seven-digit IMO checksum; nine-digit MMSI; typed Analyst read/Manager write permissions without Administrator/Support bypass |
| M3-02 | `src/HarborOps.Infrastructure/References/ReferenceRecord.cs`, `ReferenceStore.cs`, `src/HarborOps.Api/References/ReferenceEndpoints.cs` | Port create/list/get/update/archive/restore, tenant scope, bounded search/cursor, conditional versions and explicit transaction-time authorization |
| M3-03 | Same reference store/endpoints, `src/HarborOps.Infrastructure/Persistence/HarborOpsDbContext.cs`, migration `20261031110000_PortVesselReferences.cs` and model snapshot | Vessel create/list/get/update/archive/restore, nullable IMO/MMSI/flag, per-tenant partial uniqueness, additive tables and constraints; concrete-table EF mapping |
| M3-04 | `web/src/references/referenceClient.ts`, `ReferenceDataWorkspace.tsx`, `web/src/auth/OrganizationWorkspace.tsx`, `organizationClient.ts` | React port/vessel tabs, listing, search, pagination, manager create/edit/archive/restore, selected-org context, discarded stale tenant state and permission-aware UI |

## Implementation surfaces and contracts

- `src/HarborOps.Infrastructure/DependencyInjection.cs`: registers scoped reference store.
- `src/HarborOps.Api/Program.cs`: maps reference endpoints behind M1 authorization and M2 selected-organization boundary.
- New schema `ports` and `vessels`: UUID primary key, immutable tenant-local reference, required organization FK, archived status, source classification, version and UTC timestamps, nullable external identifiers and tenant-scoped unique indexes. Migration is additive; no M0-M2 history was rewritten.
- Ten `/api/v1/organizations/{organizationId}/ports|vessels` operations: GET list/detail, POST create, PATCH update/restore, DELETE soft archive. Mutations require CSRF and Origin validation.
- User-provided external identifier syntax and checksum validation does not establish actual registration or port function. No external service or paid dependency was added.
- M4 voyages/containers, M5 shipments and later tracking/analytics are not implemented by M3.

## Tests and local verification tooling executed and accepted

- `tests/HarborOps.UnitTests/ReferencePolicyTests.cs`: role matrix, local code, UN/LOCODE, IMO checksum, MMSI and invalid fields.
- `tests/HarborOps.IntegrationTests/ReferenceDataHttpTests.cs`: real PostgreSQL API lifecycle, tenant scoping, duplicate IDs, concurrency and suspension/revocation.
- `tests/HarborOps.IntegrationTests/ReferenceBoundaryMatrixTests.cs`: full endpoint/verb role denial, foreign organization/UUID substitution, missing selection, cursor/filters, revoked membership.
- `tests/HarborOps.IntegrationTests/MigrationSnapshotTests.cs`: expects the new M3 migration and complete model snapshot.
- `tests/browser/specs/references.spec.mjs`: manager/analyst workflows in Chromium/Firefox/WebKit, reference create/edit/archive/restore and tenant switching.
- `scripts/prepare-m3-browser-fixtures.mjs`: isolated M3 organization and role fixtures, distinct from accepted G2 browser test accounts.
- `scripts/verify-m3-local.sh`: guarded E:-drive orchestration using the accepted M2 runner, disposable PostgreSQL/Compose, synthetic fixtures, all web/browser regressions, outage recovery and image scans.
- `scripts/assert-g3-evidence.mjs`: verifies the presence of successful named TRX and three-engine browser results across G3-S01 to G3-S25, without assigning acceptance.
- Additional integration suites: `ReferenceAdversarialHttpTests.cs`, `ReferenceHistoricalUpgradeTests.cs`, `ReferenceAdditionalContractTests.cs`, `ReferenceSessionBoundaryTests.cs` and `ReferenceConcurrencyTests.cs`.
- Additional browser cases: `reference-races.spec.mjs` and `reference-paging.spec.mjs`, with isolated pagination fixtures.

The source was built and run in full by `scripts/verify-m3-local.sh` at `b72025d35d62d0a8a45b466986a0b3bd89eebadb`. That execution found a persistence-model mapping defect and a frontend authorization-notice defect, which were fixed, along with several test-harness defects; see the failure history in the [executed verification](M3_EXECUTED_VERIFICATION.md). The [G3 verification plan](M3_VERIFICATION_PLAN.md) scenarios all have observed evidence on that revision.

## Reproducible local execution and acceptance history

Run from a clean E:-drive worktree using an E:-backed Docker Desktop/WSL installation, configured `HARBOROPS_VERIFICATION_ROOT`, `HARBOROPS_DOCKER_VHDX` pointing to the actual active E:-backed Docker Desktop data image, E: environment caches/temp/browser output and a disposable `ho-verify` stack:

```bash
bash scripts/verify-m3-local.sh
```

The script must stop rather than use controllable C: storage. It must not run GitHub Actions. Inspect actual runner logs, migration results, PostgreSQL schema, cross-tenant denials, negative validation, browser behavior and high/critical image findings before marking any scenario PASS. Do not modify retained primary volumes.

Actual results are recorded in [G3 executed verification](M3_EXECUTED_VERIFICATION.md). G3 was accepted on 2026-10-31 and M3 fast-forward integrated into `main`. Future changes to code, tests or runtime images require verification appropriate to their impact. M4 implementation has not started.

## Review findings corrected during implementation

- Replaced generic UUID `CompareTo` filtering with parameterized, SQL-native UUID keyset predicates.
- Added transaction-time session-row lock to serialize session selection changes with reference writes.
- Explicitly configured EF Core concrete-table inheritance for the abstract reference base type.
- Removed a duplicated helper/brace block from the M3 EF model snapshot.
- Corrected adversarial tests to compare sanitized error codes rather than per-request correlation IDs.

The original [correctness audit](M3_CORRECTNESS_AUDIT.md) and [remediation register](M3_CORRECTNESS_REMEDIATION.md), together with the [second audit](M3_SECOND_CORRECTNESS_AUDIT.md) and [second remediation register](M3_SECOND_CORRECTNESS_REMEDIATION.md), preserve historical defects and corrections. The final source was exercised in the complete local G3 verification and its raw evidence independently reviewed before acceptance.
