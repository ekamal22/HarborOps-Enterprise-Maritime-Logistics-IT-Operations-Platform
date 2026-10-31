# M3 Implementation Register

**Milestone:** M3 Ports and vessels reference data
**Branch:** `feat/m3-port-vessel-reference-data`
**Baseline:** Accepted G0, G1 and G2 on `main`; M2 merged through PR #5
**Implementation state:** Source changes from both correctness audits committed; full runtime verification remains unexecuted
**Gate:** G3 open; G3-S01 through G3-S25 have not been confirmed against a final executable run
**Merge:** Not merged into `main`; acceptance pending only after verified evidence is complete

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

## Tests and local verification tooling authored, not yet executed

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

No compiler, PostgreSQL, browser, Compose, migration, image scan or documentation check has been executed on this source in this session. The implementation is not claimed runtime-correct or G3-ready based solely on source inspection. The [G3 verification plan](M3_VERIFICATION_PLAN.md) requires executed evidence for each G3-S01 through G3-S25 scenario, an exact final implementation SHA and a full rerun after the final code fix.

## Required local execution and acceptance

Run from a clean E:-drive worktree using an E:-backed Docker Desktop/WSL installation, configured `HARBOROPS_VERIFICATION_ROOT`, `HARBOROPS_DOCKER_VHDX` pointing to the actual active E:-backed Docker Desktop data image, E: environment caches/temp/browser output and a disposable `ho-verify` stack:

```bash
bash scripts/verify-m3-local.sh
```

The script must stop rather than use controllable C: storage. It must not run GitHub Actions. Inspect actual runner logs, migration results, PostgreSQL schema, cross-tenant denials, negative validation, browser behavior and high/critical image findings before marking any scenario PASS. Do not modify retained primary volumes.

Record actual results in `docs/M3_EXECUTED_VERIFICATION.md` only after a verified complete local execution. Failed, skipped, untested or blocked mandatory checks keep G3 open. After every required scenario passes, request explicit G3 acceptance; do not merge this branch or start M4 based on implementation alone.

## Review findings corrected during implementation

- Replaced generic UUID `CompareTo` filtering with parameterized, SQL-native UUID keyset predicates.
- Added transaction-time session-row lock to serialize session selection changes with reference writes.
- Explicitly configured EF Core concrete-table inheritance for the abstract reference base type.
- Removed a duplicated helper/brace block from the M3 EF model snapshot.
- Corrected adversarial tests to compare sanitized error codes rather than per-request correlation IDs.

These source-level corrections remain subject to G3 testing. See the [correctness audit](M3_CORRECTNESS_AUDIT.md) and [remediation register](M3_CORRECTNESS_REMEDIATION.md). The [second correctness audit](M3_SECOND_CORRECTNESS_AUDIT.md) and [second remediation register](M3_SECOND_CORRECTNESS_REMEDIATION.md) document additional corrections. No implementation defect may be marked verified from source inspection alone.
