# M5 Shipment Lifecycle: Implementation Status

**As of:** 2026-11-01  
**Milestone:** M5-01 through M5-04; G5 evidence gate  
**Branch:** `feat/m5-shipment-lifecycle`  
**Base:** Accepted G0-G4 `main`, at `18d00e6710097e4e31a87190be8edb65337d1b50`  
**State:** Implementation source and initial tests committed on feature branch. No complete compilation/runtime verification. G5 OPEN, acceptance pending.

## Implemented source inventory

| Work package | Committed work | Remaining before G5 |
| --- | --- | --- |
| M5-01 | Shipment domain policy: references, explicit UTC, closed manual workflow and role permissions; unit tests | Compile, execute, and confirm M5-local contract decision |
| M5-02 | Additive `ShipmentLifecycle` migration, EF model/snapshot, scoped shipment API/list/detail/history, versioned archive/restore and DB-generated manual status changes | Execute actual PostgreSQL migration, enforce all direct SQL invariants, audit API response/contracts |
| M5-03 | Organization/session/user revalidation, exact version, concurrent write tests and one queued-revocation regression | Execute deterministic races; expand queued account/organization/member invalidation matrix and replays |
| M5-04 | React shipment workspace, typed API client, two-tenant fixture preparer, Chromium/Firefox/WebKit specs, M5 verification runner/outage checker and 25-case evidence index | Run/repair full browser matrix, fixture idempotency, request races and isolated G5 pipeline |
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

## Source-level correctness assessment

[The M5 source audit](M5_SOURCE_CORRECTNESS_AUDIT.md) records the corrections made, observed source checks and mandatory runtime/coverage gaps. The added JavaScript modules passed syntactic parsing and the shipment EF model/snapshot blocks matched textually. **No .NET 10 build, actual EF model comparison, database migration, HTTP run, Playwright run, outage or scan was executed** in the current environment. These are not G5 evidence.

The local runner expects an E:-backed verification root ending in `m5`, for example `E:\HarborOps-Verification\m5`. It refuses to take over existing disposable Docker resources. Review its shared `ho-verify` stage infrastructure for independent-project isolation before concurrent verification. Keep all controlled persistent data on E:, with only attributable short-lived C: temporary writes and safe cleanup.

## Remaining decisions and governance

The proposed M5-local contract was implemented as a working baseline in response to the implementation request, but its formal decision and G5 acceptance are not recorded. See [M5 architecture](M5_ARCHITECTURE_DESIGN.md), [implementation plan](M5_IMPLEMENTATION_PLAN.md), [G5 scenarios](M5_VERIFICATION_PLAN.md) and [source audit](M5_SOURCE_CORRECTNESS_AUDIT.md). M5 source is unverified and acceptance pending; no integration into `main` or GitHub Actions execution has occurred.

The accepted graph still makes M5-01 depend on G3. M6 remains gated on both G4 and G5, with the dedicated historical-volume checkpoint at G6; formal accessibility testing stays at M24-04.
