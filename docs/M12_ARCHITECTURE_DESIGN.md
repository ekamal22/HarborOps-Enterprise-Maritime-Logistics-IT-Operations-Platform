# M12 Shipment Search and Authorized Export Architecture

**State:** Implementation candidate on `feat/m12-shipment-search-export`. G12 acceptance pending. Accepted base: `main` at `6ead1d076946dd96b53401265a31efc35c4a666c`. G5 accepted, G11 not required.

## Implemented interfaces

- `GET /api/v1/organizations/{organizationId}/shipments/search`: `q` (80-char trimmed case-insensitive substring), `recordStatus` (Active default/Archived/All), `workflowStatus`, `voyageId`, `originPortId`, `destinationPortId`, and paired explicit UTC `plannedDepartureFromUtc`/`plannedDepartureBeforeUtc`, `plannedArrivalFromUtc`/`plannedArrivalBeforeUtc`. Paired windows use inclusive start/exclusive end, at most 366 days. All filters combine with AND. Unknown, repeated and invalid parameters are denied.
- M6 voyage matching uses a tenant-scoped EXISTS on the association interval containing `asOfUtc`. No inferred ETA, voyage movement or container boarding.
- Search `limit` 1..100 (default25); response `{shipments,nextCursor,asOfUtc}`. Stable UUID ascending keyset. Cursor protected with the existing persisted ASP.NET Data Protection key ring, 15-minute TTL, bound to tenant, filters and reference instant. Later pages are live reads, not a transaction-spanning snapshot. Accepted M5 list and detail routes remain unchanged.
- `GET /api/v1/organizations/{organizationId}/exports/shipments`: same business filters, no paging. `ExportShipmentData` allows only Analyst/Manager, not Auditor, Administrator or Support. The API executes one tenant-scoped repeatable-read transaction, materializes at most 5,001 rows, rejects more than 5,000 or 10MiB without partial download, then records a required restricted audit event before committing.
- CSV v1 fixed columns: Shipment ID, Reference Code, External Reference, Display Name, Workflow Status, Record Status, Origin Port ID, Destination Port ID, Planned Departure UTC, Planned Arrival UTC, Created UTC, Updated UTC. UTF-8, quoted fields, CRLF, doubled embedded quotes, spreadsheet-formula apostrophe defense. No raw tracking, credentials, M16 audit data or unapproved M11 evidence. Per-principal rate limit 10/min and single in-flight export per API instance. `Content-Disposition` attachment; no-store, nosniff.
- SQL-only EF migration `20261107230000_M12ShipmentExportAudit` creates `m12_record_shipment_export`, a SECURITY DEFINER function callable by the restricted API runtime role only after migration-time provisioning. It verifies active organization/session/user/Analyst-or-Manager membership with locks and inserts `shipment.export_completed` with bounded event-specific metadata. No accepted migration 1..13 was rewritten. This signed-session-less audit function does not cryptographically bind the exported bytes, so direct API-role SQL forgery must be evaluated as a security residual, not declared impossible.

## Frontend and M11 boundary

`ShipmentWorkspace.tsx` preserves M5 local drafts and detail selection while using typed advanced filters and protected pagination; canManage Analyst/Manager sees CSV controls, Auditor does not. Session/organization changes unmount protected state and abort pending export requests.

M11 planning PR #30 proposes exception handling and migration 14. M12's isolated branch also uses migration position 14. Before integrating these branches, coordinate unique migration ordering and re-run migration/snapshot/role and shared UI/DI/permission regressions on a combined SHA. M12 neither implements nor consumes unaccepted exception rules. The accepted M9 ARRI/DEPA hold, 10,000 unresolved-work cap, default-off M10 projector and null ETA remain unchanged.

Source and tests are authored but not runtime verified. G12 acceptance pending. See [status](M12_IMPLEMENTATION_STATUS.md), [local policy choices](M12_PLANNING_STATUS.md), [G12 campaign](M12_VERIFICATION_PLAN.md).
