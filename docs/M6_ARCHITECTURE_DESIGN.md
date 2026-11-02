# M6 Shipment Container and Voyage Assignments: Architecture Design

**State:** Detailed design proposed, acceptance pending; implementation NOT STARTED
**Prepared:** 2026-11-02
**Planning branch:** `feat/m6-container-voyage-assignments`
**Base:** Accepted G0-G5 `main` at `344c6eb96a9af600b5843cc97c56a8449174bad8`
**Work packages:** M6-01 policy, M6-02 container links, M6-03 voyage links, M6-04 combined browser/API integration, then G6
**Authority:** [M6 local contract](M6_LOCAL_CONTRACT_DECISION.md), [roadmap](ROADMAP.md), [execution graph](execution-graph.json), [FR-005](REQUIREMENTS.md), [G6 plan](M6_VERIFICATION_PLAN.md), [accepted M4](M4_EXECUTED_VERIFICATION.md), [accepted M5](M5_G5_GATE_CLOSURE.md)

## 1. Scope, dependency and invariants

M6 turns the accepted M4 catalogs and M5 shipment records into allocation relationships while preserving their authoritative identities. It assigns a container to a shipment for an explicit UTC interval and assigns a shipment to one voyage at a time for an explicit UTC interval. Both relationships are tenant-owned and historical. They must not imply that cargo actually moved, that a container was physically loaded, or that an external voyage registry confirmed an association.

M6 does not implement vessel- or container-tracking events, independent container-to-voyage contracts, a multi-leg voyage itinerary, bookings, capacity/weight management, freight contracts, auto-ETA, M12 search/export, or M16 general audit. M7-M10 own events and derived tracking state. M24-04 remains the only dedicated accessibility assessment.

Accepted prerequisite source realities:

- M4 `voyages`: immutable `(organization_id,id)`, vessel, origin/destination ports and route; `Planned`/`Underway`/`Arrived`/`Cancelled` progress; Active/Archived record status; nullable scheduled and actual UTC; versioned archive/restore.
- M4 `containers`: immutable identifier and equipment kind, `(organization_id,id)` and archival state; versioned writes. D-003 Option B is accepted, never recalculate or rewrite ISO/synthetic identifiers in M6.
- M5 `shipments`: unique `(organization_id,id)`, immutable ports, manual workflow Draft/Booked/InTransit/Arrived/Completed/Cancelled, Active/Archived record status, versioned mutations and immutable status history. Auditor has typed shipment read-only access; Analyst and Manager may write.
- The M4/M5 stores acquire organization write lock before session/account revalidation and resource mutations. Any M6 workflow that conflicts with their archival/status mutations must obey compatible lock and transaction order.
- The accepted graph gives M6-01 two upstream gates G4 and G5. M6-02 and M6-03 may proceed in parallel after M6-01, then converge at M6-04 and G6. Do not introduce numeric-chain prerequisites.

## 2. Domain contract

### 2.1 Assignment records

| Field | ShipmentContainerAssignment | ShipmentVoyageAssignment |
| --- | --- | --- |
| `id` | Nonempty immutable UUID | Nonempty immutable UUID |
| `organizationId` | Immutable tenant | Immutable tenant |
| `shipmentId` | Immutable M5 shipment UUID | Immutable M5 shipment UUID |
| Target | Immutable `containerId` to M4 container | Immutable `voyageId` to M4 voyage |
| `startsAtUtc` | Required immutable explicit UTC | Required immutable explicit UTC |
| `endsAtUtc` | Nullable on create; set once on close | Nullable on create; set once on close |
| `version` | Starts at 1, closure produces 2 | Starts at 1, closure produces 2 |
| `createdAtUtc`, `updatedAtUtc` | Server-managed, monotonic UTC | Server-managed, monotonic UTC |
| `createdByUserId`, `closedByUserId` | Scoped actor; close actor nullable until close | Scoped actor; close actor nullable until close |

All UUID identities, association parents, start timestamp and creation provenance are immutable. No reopen or edit of an ended assignment; no DELETE. Closing only changes `ends_at_utc`, `closed_by_user_id`, `version` and `updated_at_utc` in one transaction. A created already-bounded interval is immutable with version 1 and a creation actor, not a hidden close action.

The range `[startsAtUtc, endsAtUtc)` is half-open. `endsAtUtc=null` is an unbounded future endpoint, not the literal PostgreSQL `infinity` timestamp in a DTO. Require `endsAtUtc > startsAtUtc` when non-null. A row with `startsAtUtc` in the future is still an open record if `endsAtUtc` is null, but its time-based membership begins at the specified instant. Do not call that row "physically in use" without external evidence.

### 2.2 Exclusivity and coexistence

- **Container exclusivity:** For one `(organizationId,containerId)`, no two container assignment intervals may overlap, regardless of which shipments they reference. The same container may be reassigned sequentially at an exactly adjacent timestamp.
- **Voyage selection:** For one `(organizationId,shipmentId)`, no two voyage assignment intervals may overlap. Multiple different shipments can share a voyage; no capacity/space claim is modeled.
- **Shipment container multiplicity:** Different containers may be associated with one shipment simultaneously, with independent intervals.
- **Cross-tenant identity:** Identical tenant-local container reference codes in unrelated organizations do not collide; foreign UUIDs and inaccessible records must fail without leaking existence.
- **Historical associations:** When closed, rows continue to exist for history and scoped reads. They are not overwritten or physically removed.
- **Voyage information on a container:** Derive the contextual voyage by joining its shipment's voyage assignment only where the two intervals intersect. Never infer a physical movement or create a hidden direct container-voyage assignment.

### 2.3 Voyage eligibility, route and time

- New voyage associations require Active shipment, Active voyage, voyage status Planned or Underway and the same selected organization.
- M6-01 proposes strict matching `shipment.origin_port_id == voyage.origin_port_id` and `shipment.destination_port_id == voyage.destination_port_id`. It explicitly excludes multi-leg/rerouting semantics.
- When **both** sides have complete planned UTC windows, those two windows must overlap as half-open ranges. If either window is partly unknown, do not invent a bound and do not reject solely because a partial time is unknown.
- Assignment `startsAtUtc` is the explicitly entered administrative allocation instant; do not auto-fill from vessel departure or shipment status. Voyage or shipment planned times can later change only under the accepted M4/M5 rules; M6 must reject known resulting incompatibility with an open voyage association rather than silently alter that association.
- A planned association can be opened before the shipment leaves Draft; it does not force M5 transitions. Container allocation likewise does not automatically change shipment status.
- No new voyage association to Arrived or Cancelled voyages. Existing closed links remain visible after voyage progress changes.

### 2.4 Parent status and archive contract

| Parent action | No open links | One or more open related links |
| --- | --- | --- |
| Archive M5 shipment | Preserve accepted M5 behavior | Conflict, close links first |
| Cancel or complete M5 shipment | Preserve accepted manual transition rules | Conflict, close links first |
| Archive M4 container | Preserve accepted M4 behavior | Conflict if any open container assignment |
| Archive M4 voyage | Preserve accepted M4 behavior | Conflict if any open voyage assignment |
| Cancel or arrive M4 voyage | Preserve accepted M4 status rules | Conflict if an open voyage association remains |
| Restore an archived parent | Existing M4/M5 exact-version restore | Restoring does not reopen historical links |

Parent changes and link creation/closure must serialize so a race cannot yield an archived or terminal parent with an open link. Do not put this logic solely in React, an API-only read, or a preflight SELECT without locking. Add database-level guards and execute the accepted organization-first application lock protocol. Direct SQL and concurrent transaction interleavings must be exercised before asserting the protection is correct.

## 3. Authorization boundary

Propose two typed permissions in `OrganizationPermission`:

- `ViewShipmentAssignments`: Analyst, Manager and Auditor.
- `ManageShipmentAssignments`: Analyst and Manager only.

| Role | List/detail association | Create/close association | Existing M4 mutation privilege |
| --- | --- | --- | --- |
| Analyst | Yes | Yes | No |
| Manager | Yes | Yes | Yes |
| Auditor | Yes, scoped shipment projection only | No | No |
| Administrator | No implicit business read | No | Organization admin only |
| Support | No | No | No |

The assignment view must not use a generic "may view maritime references" shortcut. For Auditor, resolve reference labels inside the already scoped assignment query and never authorize access to unrelated M4 catalog endpoints. Validate active user/session, expiry/security version, selected organization, active membership and role **after** acquiring the appropriate transaction locks. Every assignment lookup filters organization and shipment; even a valid foreign assignment UUID returns sanitized 404.

## 4. PostgreSQL and EF Core migration

One additive M6 migration follows `20261101180000_ShipmentLifecycle` using a collision-free timestamp established at implementation time. Do not change any accepted migration file or its historical snapshot. Update only current `HarborOpsDbContext`, `HarborOpsDbContextModelSnapshot` and the new migration.

### 4.1 Tables and constraints

Create `shipment_container_assignments` and `shipment_voyage_assignments` with the fields described above, plus:

1. `organization_id` FK to organizations RESTRICT.
2. Composite FK `(organization_id,shipment_id)` to `shipments(organization_id,id)` RESTRICT.
3. Composite FK `(organization_id,container_id)` to `containers(organization_id,id)` and `(organization_id,voyage_id)` to `voyages(organization_id,id)` RESTRICT. First inspect existing accepted M4 unique keys: add missing `UNIQUE(organization_id,id)` constraints in M6 only if genuinely absent, without rewriting M4.
4. CHECK `starts_at_utc < ends_at_utc` when end non-null; positive version; version 1 for an unclosed row before its first permitted closure; monotonic `updated_at_utc >= created_at_utc`; non-null creation actor.
5. FK for actor user IDs with RESTRICT. Do not cascade-delete business or history rows.
6. Bounded scoped indexes `(organization_id,shipment_id,starts_at_utc,id)`, `(organization_id,container_id,starts_at_utc,id)`, `(organization_id,voyage_id,starts_at_utc,id)` and `(organization_id,id)` as appropriate.
7. One immutable-row validation trigger per table (or one reviewed shared function) refusing identity, start, actor, version-jump, close-reopen and DELETE violations. Only null-to-valid-end closure is mutable.

### 4.2 Database-grade exclusion constraints

With PostgreSQL 16 and the `btree_gist` extension available, use native GiST exclusions to prevent write skew and concurrent range overlaps. The intended semantics are:

```sql
-- Expression shape to implement and verify on actual PostgreSQL.
CREATE EXTENSION IF NOT EXISTS btree_gist;

ALTER TABLE shipment_container_assignments ADD CONSTRAINT
  ex_m6_container_overlap EXCLUDE USING gist (
    organization_id WITH =,
    container_id WITH =,
    tstzrange(starts_at_utc, ends_at_utc, '[)') WITH &&
  );

ALTER TABLE shipment_voyage_assignments ADD CONSTRAINT
  ex_m6_shipment_voyage_overlap EXCLUDE USING gist (
    organization_id WITH =,
    shipment_id WITH =,
    tstzrange(starts_at_utc, ends_at_utc, '[)') WITH &&
  );
```

A null `ends_at_utc` means an unbounded range endpoint. Verify extension install rights and GiST UUID operator support before using this design; a missing extension or SQL conflict must fail the migration, not disable the constraint. The extension may already be shared by other schema objects, so reverse migration must not blindly drop it. EF Core's model-diff checker does **not** by itself prove raw SQL exclusion clauses: validate `pg_constraint`, `pg_index` and actual direct-SQL conflicting inserts.

### 4.3 Parent guards, lock ordering and rollback

Use additive M6-owned guards on parent shipment, container and voyage status/archive transitions. For normal API writes: lock organization first, then session and account, re-evaluate membership, lock referenced resources in a deterministic order, then mutate assignment or parent. Audit M4/M5 stores for compatible ordering before adding parent checks; use tenant-scoped queries and a shared helper only when it does not broaden any grants.

A direct-SQL parent archival statement acquires a parent row lock before its trigger executes. Consequently, a trigger that subsequently takes the organization lock can invert the normal lock order. This is a **known concurrency design hazard** requiring a concrete implementation decision and adversarial tests. Prefer an approach with explicit database-enforced consistency and measured lock behavior; treat a safely aborted deadlock as a conflict/retry failure, never as permission to leave inconsistent committed rows. G6 must prove that concurrent parent archive versus link insert, close versus archive, and direct SQL status changes cannot commit invalid combinations. If that proof fails, revise the lock/trigger design before gate closure.

The M6 migration must preserve all M0-M5 data and leave accepted triggers untouched except for additive M6 behavior. Test clean bootstrap, seeded M5 upgrade, rollback to accepted M5, and reapply on isolated disposable schemas. Never downgrade a retained database.

## 5. HTTP contract

Base route: `/api/v1/organizations/{organizationId}/shipments/{shipmentId}`, authenticated and bound to the actual selected organization.

| Verb | Path suffix | Action |
| --- | --- | --- |
| GET | `/container-assignments?after=&limit=` | Scoped bounded list; ascending stable UUID cursor, optional time filter if explicitly specified |
| GET | `/container-assignments/{assignmentId}` | Scoped assignment detail including sanitized parent label |
| POST | `/container-assignments` | Create immutable interval (target container, startsAtUtc, optional endsAtUtc); 201 |
| PATCH | `/container-assignments/{assignmentId}` | Close open interval with `expectedVersion` and `endsAtUtc`; 200 |
| GET | `/voyage-assignments?after=&limit=` | Scoped bounded voyage-link list |
| GET | `/voyage-assignments/{assignmentId}` | Scoped voyage association detail and safe route label |
| POST | `/voyage-assignments` | Create with voyageId, startsAtUtc, optional endsAtUtc; 201 |
| PATCH | `/voyage-assignments/{assignmentId}` | Close with exact version, explicit UTC end; 200 |

No hard-delete endpoint. Strict 8 KiB JSON bound and duplicate/unknown field rejection following M5; no client-controlled actor, creation time, organization or assignment id. All mutating endpoints require accepted CSRF/Origin transport checks. Return 400 for malformed validation, 401 for unauthenticated/expired session, 403 for denied/revoked membership, 404 for absent/foreign/hidden parent id, 409 for overlap, stale version, parent lifecycle and concurrency conflicts, and sanitized 503 for PostgreSQL unavailability. Include correlation ID and `Cache-Control: no-store`. Do not invent an idempotency key without a separately reviewed protocol. Use deterministic paging, no unbounded organization-wide dumps.

**Create request example:** `{"containerId":"<UUID>","startsAtUtc":"2026-11-03T10:00:00Z","endsAtUtc":null}`. **Close request example:** `{"expectedVersion":1,"endsAtUtc":"2026-11-04T10:00:00Z"}`.

## 6. Service and client integration

Recommended files, subject to final implementation-level naming:

| Layer | Proposed implementation |
| --- | --- |
| Domain | `src/HarborOps.Domain/Assignments/AssignmentPolicy.cs` (UTC/window, parent eligibility, route and schedule compatibility) |
| Persistence | `src/HarborOps.Infrastructure/Assignments/AssignmentRecord.cs` and new entities/EF mappings |
| Store | `src/HarborOps.Infrastructure/Assignments/AssignmentStore.cs` (scoped transactional reads/writes, error mapping, parent revalidation) |
| HTTP | `src/HarborOps.Api/Assignments/AssignmentEndpoints.cs` and DI/route registration in the accepted program |
| Migration | `src/HarborOps.Infrastructure/Persistence/Migrations/<next-M6-ID>_ShipmentAssignments.cs` and current model snapshot |
| Client | `web/src/assignments/assignmentClient.ts`, `web/src/assignments/ShipmentAssignmentsPanel.tsx` integrated under the M5 shipment detail |
| Domain tests | `tests/HarborOps.UnitTests/AssignmentPolicyTests.cs` |
| DB/HTTP tests | `tests/HarborOps.IntegrationTests/AssignmentSqlMatrixTests.cs`, `AssignmentHttpTests.cs`, `AssignmentConcurrencyTests.cs`, `AssignmentMigrationTests.cs` |
| Browsers | `tests/browser/specs/assignment.spec.mjs` plus affected M4/M5 regressions |

The selected-shipment workspace gets separate **Containers** and **Voyages** association panels, each listing open and closed history, with explicit UTC start/end, validation and conflict messages. Use existing M4 catalog reads only for Analyst and Manager and separate scoped label projections for Auditor. Show planned dates as planning data, not asserted movements. A failed catalog lookup must not erase an already open shipment draft. Preserve unsaved M5 shipment edits during background refresh; any assignment mutation refreshes only its own list, and closes/opens do not auto-save or overwrite the shipment form. Organization switch, logout, role revocation and session expiry must clear every assignment cache/request. Keep functional browser validation in G6; the dedicated accessibility acceptance gate stays M24-04.

## 7. Verification, operational boundaries and residual risks

G6 must establish PostgreSQL constraints under **independent direct SQL**, two-tenant authorization, concurrent writers, M4/M5 parent state races and full three-engine browser journeys. Include failing negative controls, exact source SHA, full fresh/repeat integration TRX, M0-M5 regressions, migration upgrade/rollback, outage/recovery, dependency and HIGH/CRITICAL image scans, teardown and SHA-256 raw evidence. The [G6 plan](M6_VERIFICATION_PLAN.md) defines 25 cases, including a security-mutation control.

M6 Q1 is the first historical-volume compatibility checkpoint: discover a representative retained snapshot safely, never use the only original; clone to isolated E:-backed storage for startup, non-destructive migration and representative reads. If no representative snapshot exists, record `NOT APPLICABLE` with documented negative discovery and reason. This is **not** a per-commit Docker volume check.

Unresolved until demonstrated in implementation: trigger/organization lock inversion; actual GiST extension privileges/operator classes; route/window constraints when a voyage's planned schedule is later edited; policy for independent container/voyage historical interval intersections; native headed browser visibility events beyond M5's documented simulated visibility tests. These are concrete test requirements, not permission to silently relax constraints.

**Acceptance:** Architecture and local contract are proposed; source implementation, executed G6 evidence and G6 acceptance are all pending.
