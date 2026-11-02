# M5 Shipment Lifecycle: Implementation Status

**As of:** 2026-11-02  
**Milestone:** M5-01 through M5-04; G5 evidence gate  
**Branch:** `feat/m5-shipment-lifecycle`  
**Base:** Accepted G0-G4 `main`, at `18d00e6710097e4e31a87190be8edb65337d1b50`  
**State:** M5/G5 implemented, locally verified at `3e57af490d9a28dd1f0c482b6d20cd44f8a9fc91` and explicitly accepted 2026-11-02. Fast-forward integrated into `main`; see [closure](M5_G5_GATE_CLOSURE.md).

## Implemented source inventory

| Work package | Committed work | Final disposition |
| --- | --- | --- |
| M5-01 | Shipment domain policy: references, explicit UTC, closed manual workflow and role permissions; unit tests | Accepted 2026-11-02; verified at `3e57af490d9a` |
| M5-02 | Additive `ShipmentLifecycle` migration, EF model/snapshot, scoped shipment API/list/detail/history, versioned archive/restore and DB-generated manual status changes | Accepted 2026-11-02; verified at `3e57af490d9a` |
| M5-03 | Organization/session/user revalidation, exact version, queued read/write account, security-version, membership, role, session and organization-state tests; duplicate create, archive, restore and cancellation races | Accepted 2026-11-02; verified at `3e57af490d9a` |
| M5-04 | React shipment workspace, typed API client, idempotent two-tenant fixtures, eight shipment browser specs, independent G5 Compose/SDK/browser/outage stages, SHA-256 archive and 25-case checker | Accepted 2026-11-02; verified at `3e57af490d9a` |
| G5 | Scenario matrix and source-level audit authored | Accepted 2026-11-02; verified at `3e57af490d9a` |

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

These modifications were compiled and executed (history: they were source-level when first written). Executed locally at `3e57af490d9a28dd1f0c482b6d20cd44f8a9fc91`: 138 unit, 12 contract, 146 fresh and 146 repeated PostgreSQL integration tests, 18 web unit tests and 141 browser runs (47 each in Chromium, Firefox and WebKit) all passed with none skipped; Trivy HIGH/CRITICAL 0; teardown left no resources. G5-S02 to G5-S25 have evidence present for review of this revision (the earlier revision `c143e67906d3a9e0c1ab5f4b259ad1e5befd2a67` with 139 integration tests and 117 browser runs was independently reviewed). G5-S01 and full G5 were accepted on 2026-11-02.

## Source-level correctness assessment

[The M5 source audit](M5_SOURCE_CORRECTNESS_AUDIT.md) records the corrections made, observed source checks and mandatory runtime/coverage gaps. The added JavaScript modules passed syntactic parsing and the shipment EF model/snapshot blocks matched textually. That statement describes the environment of the original source review. The build, EF comparison, migrations, HTTP and browser runs, outage checks and scans have since been executed; their results are in [M5 executed verification](M5_EXECUTED_VERIFICATION.md).

The local runner expects an E:-backed verification root ending in `m5`, for example `E:\HarborOps-Verification\m5`. It refuses to take over existing disposable Docker resources. Its resources are isolated under the `ho-verify-g5` project and the `ho-g5-pg` and `ho-g5-sdk` containers; the final run left none behind. Keep all controlled persistent data on E:, with only attributable short-lived C: temporary writes and safe cleanup.

The [M5 local contract decision](M5_LOCAL_CONTRACT_DECISION.md) was accepted on 2026-11-02, closing G5-S01. The runner's original evidence-stage exit 1 remains the historical pre-approval result.

## Acceptance and governance

The six-rule M5 local contract, G5-S01, G5-S21, G5-S23 and full G5 were explicitly accepted 2026-11-02 after independent review. M5 was fast-forward integrated into `main`, with no force-push or GitHub Actions. M6 now has its G4/G5 prerequisites accepted and still requires separate G6 evidence. Accessibility remains at M24-04. See [gate closure](M5_G5_GATE_CLOSURE.md).
