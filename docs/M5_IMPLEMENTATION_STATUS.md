# M5 Shipment Lifecycle: Implementation Status

**As of:** 2026-11-01  
**Milestone:** M5-01 through M5-04; G5 evidence gate  
**Branch:** `feat/m5-shipment-lifecycle`  
**Base:** Accepted G0-G4 `main`, at `18d00e6710097e4e31a87190be8edb65337d1b50`  
**State:** Implemented and locally verified on the feature branch at `3e57af490d9a28dd1f0c482b6d20cd44f8a9fc91` (see [M5 executed verification](M5_EXECUTED_VERIFICATION.md) and the [contract compliance matrix](M5_G5_S01_CONTRACT_COMPLIANCE.md)). G5 OPEN, acceptance pending.

## Implemented source inventory

| Work package | Committed work | Remaining before G5 |
| --- | --- | --- |
| M5-01 | Shipment domain policy: references, explicit UTC, closed manual workflow and role permissions; unit tests | Compile, execute, and confirm M5-local contract decision |
| M5-02 | Additive `ShipmentLifecycle` migration, EF model/snapshot, scoped shipment API/list/detail/history, versioned archive/restore and DB-generated manual status changes | Execute actual PostgreSQL migration, enforce all direct SQL invariants, audit API response/contracts |
| M5-03 | Organization/session/user revalidation, exact version, queued read/write account, security-version, membership, role, session and organization-state tests; duplicate create, archive, restore and cancellation races | Execute, inspect and repair deterministic PostgreSQL and HTTP races on E: |
| M5-04 | React shipment workspace, typed API client, idempotent two-tenant fixtures, eight shipment browser specs, independent G5 Compose/SDK/browser/outage stages, SHA-256 archive and 25-case checker | Run/repair full browser matrix and raw E:-backed G5 pipeline; independent review pending |
| G5 | Scenario matrix and source-level audit authored | All 25 scenarios need observed execution, independent raw-evidence review, explicit acceptance |

## Key implementation paths

- `src/HarborOps.Domain/Shipments/ShipmentPolicy.cs`
- `src/HarborOps.Infrastructure/Shipments/ShipmentRecord.cs` and `ShipmentStore.cs`
- `src/HarborOps.Infrastructure/Persistence/Migrations/20261101180000_ShipmentLifecycle.cs`
- `src/HarborOps.Api/Shipments/ShipmentEndpoints.cs`
- `web/src/shipments/ShipmentWorkspace.tsx` and `shipmentClient.ts`
- `tests/HarborOps.UnitTests/ShipmentPolicyTests.cs`
- `tests/HarborOps.IntegrationTests/ShipmentHttpTests.cs`, `ShipmentBoundaryTests.cs` and `ShipmentMigrationTests.cs`
- `tests/browser/specs/shipment.spec.mjs`
- `scripts/verify-m5-local.sh`, `scripts/assert-g5-evidence.mjs`, `scripts/check-m5-schema-contract.mjs`, `scripts/check-m5-shipment-outage.mjs` and `scripts/prepare-m5-browser-fixtures.mjs`.

Existing M0-M4 migration files are unchanged. Older test expectations for the latest migration count were reconciled on this feature branch. No M6 assignments, tracking events, derived projections or CSV exports were added.

## Remediation following the M5/G5 correctness audit

All eleven original source audit findings have committed corrections, including isolated `ho-verify-g5` verification resources, explicit TRX and browser matrix requirements, pre/post storage and teardown proof, raw ZIP hashing, extensive SQL/role/tenant/concurrency tests, port-catalog recovery, conflict preservation and Auditor port descriptors. See [remediation ledger](M5_G5_REMEDIATION_STATUS.md).

These modifications were compiled and executed (history: they were source-level when first written). Executed locally at `3e57af490d9a28dd1f0c482b6d20cd44f8a9fc91`: 138 unit, 12 contract, 146 fresh and 146 repeated PostgreSQL integration tests, 18 web unit tests and 141 browser runs (47 each in Chromium, Firefox and WebKit) all passed with none skipped; Trivy HIGH/CRITICAL 0; teardown left no resources. G5-S02 to G5-S25 have evidence present for review of this revision (the earlier revision `3e57af490d9a28dd1f0c482b6d20cd44f8a9fc91` with 139 integration tests and 117 browser runs was independently reviewed). G5-S01 is incomplete until the M5 local contract is accepted.

## Source-level correctness assessment

[The M5 source audit](M5_SOURCE_CORRECTNESS_AUDIT.md) records the corrections made, observed source checks and mandatory runtime/coverage gaps. The added JavaScript modules passed syntactic parsing and the shipment EF model/snapshot blocks matched textually. That statement describes the environment of the original source review. The build, EF comparison, migrations, HTTP and browser runs, outage checks and scans have since been executed; their results are in [M5 executed verification](M5_EXECUTED_VERIFICATION.md).

The local runner expects an E:-backed verification root ending in `m5`, for example `E:\HarborOps-Verification\m5`. It refuses to take over existing disposable Docker resources. Its resources are isolated under the `ho-verify-g5` project and the `ho-g5-pg` and `ho-g5-sdk` containers; the final run left none behind. Keep all controlled persistent data on E:, with only attributable short-lived C: temporary writes and safe cleanup.

The [M5 local contract decision record](M5_LOCAL_CONTRACT_DECISION.md) remains proposed with acceptance pending. The G5-S01 evidence checker now requires its explicit Accepted state; having source code or passing unit tests alone cannot close this decision.

## Remaining decisions and governance

The proposed M5-local contract was implemented as a working baseline in response to the implementation request, but its formal decision and G5 acceptance are not recorded. See [M5 architecture](M5_ARCHITECTURE_DESIGN.md), [implementation plan](M5_IMPLEMENTATION_PLAN.md), [G5 scenarios](M5_VERIFICATION_PLAN.md) and [source audit](M5_SOURCE_CORRECTNESS_AUDIT.md). M5 source is unverified and acceptance pending; no integration into `main` or GitHub Actions execution has occurred.

The accepted graph still makes M5-01 depend on G3. M6 remains gated on both G4 and G5, with the dedicated historical-volume checkpoint at G6; formal accessibility testing stays at M24-04.
