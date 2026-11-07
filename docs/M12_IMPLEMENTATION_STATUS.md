# M12 Implementation Status

**Date:** 2026-11-07. **Branch:** `feat/m12-shipment-search-export`.
**Accepted baseline:** `main` at `6ead1d076946dd96b53401265a31efc35c4a666c`.
**Disposition:** Code and test candidate committed; no G12 technical campaign executed, no independent G12 review and no gate acceptance. Nothing merged or deployed.

| Package | Candidate implementation | Gate |
| --- | --- | --- |
| M12-01 | Shared typed filter and CSV policies, protected cursor and role contract | Policy decisions pending |
| M12-02 | New tenant-scoped search route, M6 voyage and M5 port/status/date predicates | Authored, runtime unverified |
| M12-03 | Restricted Analyst/Manager CSV export, size limits, role/audit migration | Authored, runtime unverified |
| M12-04 | React UI plus new unit, contract, real-PostgreSQL HTTP and three-engine browser test sources | Authored, NOT EXECUTED |
| G12 | Source-locked campaign, Q2 snapshot, raw evidence, independent S25 and decision | NOT EXECUTED |

## Changed source and tests

Domain: `ShipmentSearchPolicy.cs`, `OrganizationPolicy.cs`.
Infrastructure: `ShipmentSearchStore.cs`, `DependencyInjection.cs`, `20261107230000_M12ShipmentExportAudit.cs`.
API: `ShipmentSearchEndpoints.cs`, `Program.cs`, `AuthenticationSetup.cs`, `AuditRoleProvisioner.cs`.
Web: `shipmentSearchClient.ts`, `ShipmentWorkspace.tsx`.
Tests: `ShipmentSearchPolicyTests.cs`, `ShipmentSearchContractTests.cs`, `ShipmentSearchHttpTests.cs`, `shipment-search.spec.mjs`, updated `MigrationSnapshotTests.cs` and event-specific M16 `AuditHardeningTests.cs`.

No .NET SDK, Docker, PostgreSQL or browser runtime is available in the connected source-editing environment. **No build, runtime test, migration, real-stack check, scan, mutation, or G12-S25 review passed or failed here because none was run.** All G12 scenarios are NOT EXECUTED. Reference-dataset p95 measurements belong to M21-02.

## Open conditions

- Explicit policy disposition M12-P01..P16 and review of Auditor export, audit grants and row/byte/rate limits.
- Run local E:-backed disposable DB and browser verification with the source frozen at the final SHA. Preserve every failure, not just green outcomes.
- Coordinate M11/M12 migration 14 collision before combined integration.
- Validate API-role direct SQL permission/forgery, role change and audit-failure atomicity; the audit function checks a claimed live session but does not have a signed per-request proof.
- Perform G12 Q2 historical-volume compatibility on a safe copy, or document NOT APPLICABLE with discovery evidence.
- Independent G12-S25 review and explicit G12 acceptance pending.

No GitHub Actions were run. The M9 transport hold, no-deployment status and accepted main remain unchanged.
