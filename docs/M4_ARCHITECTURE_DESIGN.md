# M4 Voyage and Container Reference Data: Architecture Design

**Milestone:** M4, work packages M4-01 through M4-04; exit gate G4  
**Status:** Proposed architecture with initial feature-branch implementation, not runtime-verified. [Implementation status](M4_IMPLEMENTATION_STATUS.md) records code paths and blockers. G4 OPEN; acceptance pending only after executed verification.  
**Foundation:** G0-G3 accepted on `main`; parent references and authorization supplied by accepted M2/M3  
**Open decision:** [D-003 container identifier policy](M4_D003_CONTAINER_IDENTIFIER_PROPOSAL.md). Its recommendation is provisional until explicitly approved.  
**Authority:** [Roadmap](ROADMAP.md), [Graph](EXECUTION_GRAPH.md), [Parallel Delivery Policy](PARALLEL_DELIVERY_POLICY.md), [Requirements](REQUIREMENTS.md), [M3 architecture](M3_ARCHITECTURE_DESIGN.md), [M4 implementation plan](M4_IMPLEMENTATION_PLAN.md), [G4 verification plan](M4_VERIFICATION_PLAN.md).

## 1. Bounded capability and ownership

Deliver two organization-owned reference resources:

- **Voyages:** reference code, vessel, origin/destination port, optional scheduled and actual UTC windows, explicit voyage progression, search, edit, archive and restore.
- **Containers:** canonical identifier according to approved D-003, equipment type, provenance, versioned reference fields, search, archive and restore.
- **Browser:** selected-organization Voyage and Container workspace with Manager mutations and Analyst read-only visibility, fast tenant switching, pagination and reliable stale-response cleanup.

M4 must not implement M5 shipment records, M6 shipment-to-container/voyage assignments, cargo tracking, voyage/port schedules from outside providers, ETA predictions, ownership registry verification, container movement events, equipment interchange, booking, transport documents, telematics, map coordinates, or automatic real-time updates. The operational milestone is a **local reference catalog**, not a logistics execution system.

The work is additive relative to accepted M0-M3. Do not modify the existing applied migrations or their authorization behavior. No new paid, hosted or proprietary dependency.

## 2. Integration boundary with M3 and parallel M5

M4 uses the immutable UUID and organization ownership of accepted `PortReference` and `VesselReference`, not names, UN/LOCODE or IMO values, as relational identities. An origin and destination port may have the same external country code but must be separate records. Validation and display read the referenced port/vessel through current organization scope. No cross-organization copy, lookup, or universal registry fallback.

M5 independently models shipment lifecycle on accepted G3. Neither the M4 voyage nor container table may contain a shipment FK or status calculated from shipments. M6 is the sole owner of assignment intervals and overlap validation, after G4 and G5 are accepted. M4 must expose stable immutable UUIDs, normalized identity, archived-status semantics and versioned records so M6 can reference them later without rewriting M4 identifiers.

Before concurrent M4/M5 migrations are integrated, allocate a collision-free migration ID/order and run combined fresh plus M3-upgrade rehearsals. Coordinate shared permissions and API error envelopes; never make M4 introduce generic administrator bypasses or alter M5 entities.

## 3. Data models

### 3.1 VoyageReference

| Field | Proposed invariant |
| --- | --- |
| `Id` | Server-generated UUID, nonempty and immutable |
| `OrganizationId` | Required organization FK, immutable |
| `ReferenceCode` | Immutable, tenant-local uppercase 3-32 `[A-Z0-9]+(?:-[A-Z0-9]+)*`; unique across active and archived voyages |
| `VesselId` | Required UUID FK to vessel in the same organization; immutable in M4 |
| `OriginPortId`, `DestinationPortId` | Required different port UUIDs in the same organization; immutable in M4 |
| `PlannedDepartureAtUtc`, `PlannedArrivalAtUtc` | Nullable UTC instants, independently unknown; if both known, planned departure must precede planned arrival |
| `ActualDepartureAtUtc`, `ActualArrivalAtUtc` | Nullable UTC instants, independently modeled from planned times; if both known, actual departure must not follow actual arrival |
| `VoyageStatus` | `Planned`, `Underway`, `Arrived`, or `Cancelled`; no inferred status from clock time |
| `RecordStatus` | `Active` or `Archived`; independent from voyage progress |
| `SourceKind` | `UserProvided` or `Synthetic`, immutable, no official verification claim |
| `Version` | Monotonically increasing positive bigint, starting at 1 |
| `CreatedAtUtc`, `UpdatedAtUtc` | Server-supplied UTC timestamps; creation immutable, updated not earlier than creation |

**Voyage status transitions:**

```text
Planned -> Underway -> Arrived
   |
   +-----> Cancelled
```

- Create as Planned, Active, with no actual timestamps. Past or future planned dates are allowed; the reference catalog does not assert real-world completion.
- `Planned -> Underway` requires actual departure and no actual arrival.
- `Underway -> Arrived` requires actual departure and actual arrival, with arrival >= departure.
- `Planned -> Cancelled` is permitted without actual timestamps. Cancelled and Arrived are terminal in M4.
- Reject skipping directly `Planned -> Arrived`, rollback to earlier progress, silent automatic transition and attempts to edit an Archived record. Restoration of Archived status is a separate record-status action, not a voyage status transition.
- From Planned, scheduled fields can be edited; after Underway, planned timestamps remain historical for schedule-vs-actual comparison, while actual departure is immutable once recorded. Actual arrival is immutable after Arrived. Do not silently erase known timestamps.
- Archive may be requested from any Active voyage status with expectedVersion; history and FKs remain. Restore uses PATCH with only `recordStatus=Active` and expectedVersion, and never changes the underlying voyage progression. Subsequent M6 workflows must explicitly handle references that were archived.
- On create and any referential reassignment attempt, vessel and ports must belong to the selected active organization and be Active. M4 deliberately prohibits changing voyage vessel/route identity later; corrections require a new voyage record, with existing record soft-archived.

The SQL CHECK for temporal consistency validates `planned_departure <= planned_arrival` only when both known (strict `<` is recommended for a physically meaningful planned window), and `actual_departure <= actual_arrival` only when both known. A second check validates voyage status/actual-time compatibility. Keep UTC as `timestamptz` and never interpret user timezone in the database layer.

### 3.2 ContainerReference

| Field | Proposed invariant |
| --- | --- |
| `Id`, `OrganizationId` | Immutable UUID and tenant owner |
| `ReferenceCode` | Immutable normalized local reference code, unique within organization |
| `ContainerIdentifier` | Immutable normalized external-format or visibly synthetic identifier, unique within organization, including archived rows |
| `IdentifierKind` | Exactly one of `ISO6346` or `Synthetic`; final approval is D-003 |
| `EquipmentType` | Required immutable catalog value: `Dry20`, `Dry40`, `HighCube40`, `Reefer20`, `Reefer40`, `Other` (tentative pending D-003 confirmation) |
| `DisplayName` | Optional 1-160 safe characters, mutable, intended as internal label |
| `RecordStatus` | `Active` or `Archived`, soft-archived, restorable |
| `SourceKind` | `UserProvided` or `Synthetic`, immutable; must align with identifier kind |
| `Version`, `CreatedAtUtc`, `UpdatedAtUtc` | Server-managed optimistic version and UTC timestamps |

ContainerReference does not store loading status, current location, cargo weight, damage state, leasing contracts or shipment/voyage assignment. Those are later scoped operational concepts. Type labels in the proposed `EquipmentType` list are a **local UI catalog**, not an ISO size/type verification service.

Recommended D-003 policy: ISO-mode `[A-Z]{3}U[0-9]{7}` plus ISO 6346 checksum; Synthetic-mode `SYN-BOX-[A-Z0-9]{4,16}`. Category J/Z is outside M4 freight-container scope. Parser must not conflate kinds or claim external registration. See the [D-003 proposal](M4_D003_CONTAINER_IDENTIFIER_PROPOSAL.md) for alternatives and decisions. **Do not implement M4-03 against the provisional policy until D-003 is explicitly closed.**

## 4. PostgreSQL schema and isolation

Add one M4-specific additive EF Core 10 migration, tentatively `20261101010000_VoyageContainerReferences`, but confirm actual migration ordering with concurrently developed branches before use. This is a proposed name, not a created file. New tables: `voyages` and `containers`. Both have required `organization_id` foreign key with `ON DELETE RESTRICT`.

- `voyages`: UUID PK; tenant-local immutable `reference_code`; three required tenant-safe parent UUIDs; planned/actual `timestamptz` fields; voyage status and record status; source kind; positive version; UTC created/updated; unique `(organization_id,reference_code)`; indexes `(organization_id,id)`, `(organization_id,vessel_id)`, `(organization_id,origin_port_id)`, `(organization_id,destination_port_id)`, and `(organization_id,voyage_status,id)`.
- `containers`: UUID PK; tenant-local `reference_code`; unique `(organization_id,reference_code)` and `(organization_id,container_identifier)`; identifier kind, equipment type, optional display name, record status, source kind, positive version, UTC timestamps; bounded scoped-query indexes.
- Enforce exact tenant matching for voyage parent references at the database layer, not only by C# authorization. Add `UNIQUE (organization_id,id)` on accepted M3 `ports` and `vessels` via the new additive M4 migration (existing M3 `(organization_id,id)` indexes are nonunique). Then define composite `FOREIGN KEY (organization_id,port_id/vessel_id)` against those unique keys, with RESTRICT deletion. Do not rewrite M3 applied migrations or change port/vessel ownership.
- PostgreSQL CHECK constraints enforce nonempty canonical local codes, closed status/type/kind sets, distinct route endpoints, valid time/status combinations, source-kind alignment, positive versions and `updated_at_utc >= created_at_utc`. For ISO identifiers, add a DB-level checksum implementation with pinned SQL domain tests. The optional function must have a bounded, explicit schema name and be removed in the reverse migration.
- Add immutable-field triggers for `id`, `organization_id`, `reference_code`, parent route/vessel identities, `identifier_kind`, `container_identifier`, equipment type, source kind and creation UTC. Accepted M3 trigger/function must not be modified.
- Protect archival identities with ordinary full unique indexes, not partial active-only indexes. Do not cascade-delete accepted reference rows. All queries must filter `organization_id`, with no globally readable lookup route.
- Historical M3 rows must be preserved exactly when M4 migrates. No automatic production/demo data seeding in the migration itself.

## 5. Authorization and transaction ordering

Reuse M2/M3 scoped session and organization checks. The current closed `OrganizationPermission` enum adds **proposed** `ViewVoyageData`, `ManageVoyageData`, `ViewContainerData`, `ManageContainerData`. Explicit grants are:

| Active member role | Read voyages | Write voyages | Read containers | Write containers |
| --- | --- | --- | --- | --- |
| Analyst | Yes | No | Yes | No |
| Manager | Yes | Yes | Yes | Yes |
| Support | No | No | No | No |
| Administrator | No | No | No | No |
| Auditor | No | No | No | No |

Do not infer business permission from Administrator organization-management rights. Keep existing M3 permissions intact, and make negative role tests a blocking G4 requirement.

For every read, enforce active M1 user/session, unrevoked expiry/security version, active selected organization and membership, exact required permission, and tenant filter in the same protected database access pattern as M3. Do not return foreign records, metadata, counts, parent names or authorization details.

For each write, acquire locks in a single global order: organization row first; session row; user row; membership/role revalidation; relevant parent voyage/port/vessel rows; target row. Revalidate selected organization and current security version *after* acquiring the scope locks, then apply version precondition and domain invariants in the same transaction. With M3 writers already locking the organization row, a competing parent archive and M4 voyage creation must serialize safely. A failed transaction writes nothing.

Return 404 for a true foreign or nonexistent UUID after valid own selection, 403 for insufficient role/revocation, 409 for missing selection or conflicts, 401 for anonymous/expired authentication, and sanitized 503 for database outage. Never echo input, SQL details, unrelated tenant identifiers or credentials in errors.

## 6. HTTP contracts

All below are proposed under existing `/api/v1/organizations/{organizationId}`, with authenticated selected organization; all mutations require same-origin CSRF/Origin checks. No public routes.

| Method | Route | Purpose | Permission |
| --- | --- | --- | --- |
| GET | `/voyages` | Bounded query, status/port/vessel search and UUID cursor | ViewVoyageData |
| GET | `/voyages/{voyageId}` | One scoped voyage | ViewVoyageData |
| POST | `/voyages` | Create Planned voyage | ManageVoyageData |
| PATCH | `/voyages/{voyageId}` | Versioned schedule, status/time or archive restoration | ManageVoyageData |
| DELETE | `/voyages/{voyageId}` | Versioned soft archive; 204 | ManageVoyageData |
| GET | `/containers` | Bounded query by identifier/type/status | ViewContainerData |
| GET | `/containers/{containerId}` | One scoped container | ViewContainerData |
| POST | `/containers` | Register typed container | ManageContainerData |
| PATCH | `/containers/{containerId}` | Versioned display name or archive restoration | ManageContainerData |
| DELETE | `/containers/{containerId}` | Versioned soft archive; 204 | ManageContainerData |

- Lists: `?q=` bounded substring, `recordStatus=Active|Archived|All`, `limit=1..100` default 25 and `after=<uuid>` strict UUID keyset; voyages also filter `vesselId`, `originPortId`, `destinationPortId`, `voyageStatus`; containers also filter `equipmentType` and `identifierKind`. Invalid filters return sanitized 400. All filtering is in SQL within tenant scope, with stable `id` order and no unbounded offset or wildcard interpretation.
- Create returns 201, complete safe entity projection and location. PATCH uses mandatory `expectedVersion>=1`, exactly one meaningful change; successful 200 returns new version. DELETE requires `{"expectedVersion":N}` and returns 204 only on a successful Active-to-Archived transition. Stale or already-archived mutation returns 409. Restore via PATCH accepts only `{"recordStatus":"Active","expectedVersion":N}` while archived.
- Reject unknown/duplicate JSON properties, unexpected fields, wrong types, body length greater than 8192 bytes, excessive nesting, invalid UTF-8 or Content-Type with existing M3 bounded request-parser conventions.
- Voyage POST requires local `referenceCode`, `vesselId`, `originPortId`, `destinationPortId`, nullable planned timestamps. Container POST requires `referenceCode`, `identifierKind`, `containerIdentifier`, `equipmentType`, optional `displayName`. The server supplies all identities, status/provenance and UTC timestamps.
- A PATCH may not alter immutable foreign keys/route, container identifier, equipment type, code, provenance or owner. No upsert, bulk import, implicit external lookup, or cross-organization transfer is provided.
- Lists and details remain readable for authorized Analysts but must hide all create/update/archive controls. Parent reference dropdowns contain only active same-organization ports/vessels and must be validated server-side regardless of what the browser sends.

## 7. React browser design

Add a Voyage and Container workspace nested under the existing M2 `OrganizationWorkspace`. Keep M3 port/vessel workspace unchanged. Suggested files: `web/src/voyages/VoyageWorkspace.tsx`, `voyageClient.ts`, `web/src/containers/ContainerWorkspace.tsx`, `containerClient.ts`.

- Organization switch clears all voyage/container rows, selection, forms, errors and cached parent options *before* requesting the new tenant. A pending request in the old tenant must never repaint after a switch.
- A 401, access-revoked 403, session invalidation or unavailable 503 must clear protected rows and management controls. The parent is notified of revoked permissions to revalidate the active session and organization.
- Keep create/edit/archive/restore state separate from listing and cursor fetching; abort/ignore stale responses on tab change, filters, visibility refresh, logout or unmount.
- Show distinct Planned, Underway, Arrived, Cancelled voyage progress and Active/Archived record status, without conflating them.
- Render missing planned/actual timestamps as `Unknown`; display UTC in data transport and use user-local UI rendering with explicit timezone context. Do not fabricate ETA or event history.
- Containers with `Synthetic` kind must display a persistent Synthetic label; an ISO-shaped identifier is not labeled externally verified.
- Filtered/paged table, detail drawer, reference selection, status transitions and meaningful empty/error/permission states are mandatory in Chromium, Firefox and WebKit functional tests. Formal accessibility testing stays exclusively at M24-04.

## 8. Synthetic fixtures and verification isolation

Use fixed namespaced synthetic organizations and two independent tenants, both with unique selected-session roles, plus Analyst, Manager, Support, Administrator and Auditor test principals. Seed active/archived voyages and both container identifier kinds using fixed reproducible UUIDs and statuses; use controlled UTC instants including a missing ETA, overnight crossing and invalid reversed-time negative fixture. Seed only on the disposable E:-backed test database via an explicit fixture tool; the normal migration and primary `harborops` project must not gain demo rows.

The G4 plan requires a fresh database, historical seeded M3 upgrade, M4 rollback/reapply, direct SQL constraints, cross-tenant real-object tests, concurrency/revocation interleavings, all five roles, three browser engines, full accepted M0-M3 regression, real database outage and image/dependency scans. Record exact implementation SHA, versions, stage exits and raw evidence in `E:\HarborOps-Verification\evidence\g4` or another verified E: path, never a C:-backed default. No Actions, public service or paid dependency.

## 9. Explicit risks and approval boundary

1. **D-003 unapproved:** Identifier kind, checksum, synthetic prefix and equipment type catalog remain proposals. M4-01 must settle the selected contract before building container persistence.
2. **Concurrent M5 migration:** Coordinate schema order and avoid silently modifying a shared migration history.
3. **Parent archival race:** M3 writes and M4 writes must serialize through the same organization lock and rely on composite DB FKs; never trust route UUID alone.
4. **Nullable times vs voyage status:** Check both C# and SQL semantics, including independently missing times and terminal states.
5. **Archive vs progress:** Separate record visibility from voyage lifecycle state, preserving historical references for M6.
6. **External semantics:** ISO-format validity and UN/LOCODE syntax never establish official registration, physical existence, position or service schedule.
7. **Evidence gate:** Architectural approval authorizes implementation design only. G4 must execute every mandatory check and obtain separate explicit acceptance before integration.

**No M4 source, migration, fixtures or verification output is claimed by this document.** Detailed work packages and acceptance sequencing appear in [M4 implementation plan](M4_IMPLEMENTATION_PLAN.md) and [G4 verification plan](M4_VERIFICATION_PLAN.md).
