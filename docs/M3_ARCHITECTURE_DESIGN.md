# M3 Ports and Vessels Reference Data: Architecture Design

**Milestone:** M3, packages M3-01 through M3-04; exit gate G3  
**State:** Architecture implemented, tested locally and accepted with G3 on 2026-10-31; M3 integrated into `main`.
**Foundation:** G0-G3 explicitly accepted on 2026-10-31; M0-M3 integrated into `main`. G4 remains open.
**Authority:** [Roadmap](ROADMAP.md), [Requirements](REQUIREMENTS.md), [Domain Model](DOMAIN_MODEL.md), [API Contracts](API_CONTRACTS.md), [M2 architecture](M2_ARCHITECTURE_DESIGN.md), [M3 implementation plan](M3_IMPLEMENTATION_PLAN.md), [G3 verification plan](M3_VERIFICATION_PLAN.md).

## 1. Bounded outcome and non-goals

Deliver persistent, validated, searchable port and vessel references within the selected organization, with working create, list, detail, update and archive flows. M3 creates the first business-data authorization boundary on top of the accepted M2 membership/session foundation. Reference records are per-organization, not universal catalog records.

M3 does not implement voyages, containers, shipments, tracking ingestion, automatic port imports, AIS position reporting, map tiles, terminal/facility subcodes, external data services, collaborative record merging, a global master registry, or public access. M4-M6 own the dependent maritime entities. No shipment events or incident records are seeded as an M3 side effect. M3 requires no paid service or network API.

## 2. Architectural choices

| Concern | Proposed contract | Reason |
| --- | --- | --- |
| Ownership | `organization_id` on every port and vessel, immutable after creation | Preserve M2 tenant boundaries and prevent reassignment attacks |
| Identity | Server-generated immutable UUID `id`; tenant-scoped immutable `reference_code` | Stable internal joins and a useful local identifier independent of external registries |
| External codes | Optional UN/LOCODE for ports; optional IMO and MMSI for vessels | Some legitimate records lack an external code; do not fabricate one |
| Authorization | Explicit `ViewReferenceData` and `ManageReferenceData` typed permissions | M2 Administrator and Support memberships have no implicit business data rights |
| Mutation | Versioned edits, transactional authorization recheck, soft archive | Prevent stale updates and loss of records that later milestones reference |
| Search | Authenticated, tenant-scoped, bounded keyset pagination and escaped text filters | No cross-tenant enumeration or unbounded catalog download |
| Validation | Pure C# policy plus PostgreSQL constraints; external existence is not asserted | Offline, deterministic testing and defense against invalid direct writes |
| Seed | Deterministic synthetic fixtures only in a disposable test/demo setup | No real registration claims, external dependencies or primary DB contamination |

These are M3 implementation choices proposed under existing ADR-002, ADR-003, ADR-004 and ADR-005, not changes to globally accepted ADRs. Design approval is not G3 acceptance.

## 3. Port domain contract

A `PortReference` has:

| Field | Type and invariant |
| --- | --- |
| `Id` | Nonempty server-generated UUID, immutable |
| `OrganizationId` | Required organization FK, immutable |
| `ReferenceCode` | Required normalized uppercase tenant-local code, 3-32 chars, pattern `[A-Z0-9]+(?:-[A-Z0-9]+)*`, immutable |
| `Name` | Required trimmed Unicode display name, 1-160 chars, no controls |
| `CountryCode` | Required uppercase two-letter code, syntactic `[A-Z]{2}`; do not claim membership in an external ISO directory without a pinned source |
| `UnLocode` | Optional canonical five-character UN/LOCODE, uppercase; format `[A-Z]{2}[A-Z2-9]{3}`; first two characters equal `CountryCode` |
| `Status` | `Active` or `Archived`, not physically deleted |
| `Version` | Required positive monotonically increasing 64-bit integer, initial 1 |
| `CreatedAtUtc`, `UpdatedAtUtc` | Server-assigned UTC timestamps, never accepted from clients |

UN/LOCODE denotes a trade/transport locality. It does not independently prove port function, terminal identity, official approval status, or geographic coordinates. An M3 record with a syntactically valid code is not marked officially verified. Do not use a terminal name as if it were a separate locality code.

Normalize `UnLocode` by removing at most a single presentation separator (space between the two-letter country and three-character location part), then uppercase; reject other embedded whitespace, punctuation, 0/1 in the location segment, and any country mismatch. If a record lacks UN/LOCODE, the required `ReferenceCode` still provides a stable tenant-local lookup. No country-code or location-directory download is necessary at G3.

## 4. Vessel domain contract

A `VesselReference` has `Id`, `OrganizationId`, `ReferenceCode`, `Name`, `Status`, `Version`, `CreatedAtUtc` and `UpdatedAtUtc` with the same rules as ports, plus:

| Field | Type and invariant |
| --- | --- |
| `ImoNumber` | Optional canonical seven-digit current-format ship IMO identifier; accept optional uppercase `IMO` presentation prefix, store seven digits only; validate check digit |
| `Mmsi` | Optional exactly nine-digit MMSI text, preserving leading zeros |
| `FlagCountryCode` | Optional two-letter uppercase country code; syntactic validation only |

For the current seven-digit IMO ship-number format, validate the final digit against the weighted sum of the first six digits using weights 7, 6, 5, 4, 3, 2, modulo 10. Do not treat a passing check digit as proof of registration or vessel identity. The IMO scheme notes that future eight-digit numbers may be introduced. Any change to accepted lengths must be a separately tested versioned policy update, not a silent parser relaxation.

MMSI is a nine-digit radio-station identifier, distinct from an IMO hull identifier. Validate digit length and reject all-zero values, but do not infer flag, registration or existence solely from digits. MMSI may change while the vessel's internal UUID and reference code remain stable. Name is not unique and can change. Allow a name-only synthetic vessel with no IMO/MMSI, identified by `ReferenceCode`.

Retain the source classification `UserProvided` or `Synthetic` on each reference record, with no `OfficiallyVerified` state in M3. Do not attach an unproven registry-confirmation badge. Any future verified source must record its publication/version and provenance before that status can exist.

## 5. PostgreSQL and migration

Add one forward-only, additive EF Core 10 migration after the accepted M2 migration. Tables: `ports` and `vessels`. Do not edit or regenerate any applied M0-M2 migration. Both tables contain a required `organization_id` FK to `organizations.id` with `RESTRICT` deletion behavior, nullable external identifiers, `source_kind`, status, version and UTC timestamps.

Required indexes and database constraints:

- Unique `(organization_id, reference_code)` across both active and archived rows within each table. Archival never frees a code for another record.
- Partial unique `(organization_id, un_locode)` on ports where UN/LOCODE is not null.
- Partial unique `(organization_id, imo_number)` and `(organization_id, mmsi)` on vessels where the identifier is not null.
- Index `(organization_id, id)` for keyset pagination and stable page order.
- FK, nonempty/format and length checks, positive version, `Active|Archived` status, `UserProvided|Synthetic` source kind, and nonempty trimmed names. Include country/UN/LOCODE prefix consistency in a DB check.
- For vessel IMO, validate its seven digits and checksum in domain policy; add a PostgreSQL check or generated safe equivalent for checksum so direct database writes cannot bypass it. Where a database check cannot express a domain rule reliably, explicitly test and document the difference before G3.
- Use deterministic UTC timestamps supplied by `TimeProvider`; no local-time dependent comparisons.

Do not silently mutate existing organization membership rows or seed live port/vessel data during `--migrate`. The existing `bootstrap_records`, identity/session and membership records must be preserved across the additive upgrade. Future M4 foreign keys must reference the immutable UUIDs, not mutable names or external codes. Hard delete is not exposed.

## 6. Access and transaction design

Add `ViewReferenceData` and `ManageReferenceData` to the closed `OrganizationPermission` enum with an explicit role matrix:

| Role | View reference data | Create/update/archive |
| --- | --- | --- |
| Analyst | Yes | No |
| Manager | Yes | Yes |
| Support | No | No |
| Administrator | No by default | No by default |
| Auditor | No by default | No |

There is no dynamic extra-grant mechanism in M3. Do not confuse organization administration with business-data authority. A later explicit grant model requires its own acceptance gate. Keep all existing M2 organization and membership permissions unchanged.

For every call, require M1 authenticated `CurrentSession`, the selected session organization, active user, active organization, active membership and the exact typed permission. `OrganizationStore.ResolveAsync` is a preliminary scope check, not permission to run unscoped queries. Each repository read must independently predicate on `organization_id`, verify current selected organization and active permission in the same database operation, and return no cross-organization rows, counts or hints.

For every write, begin a transaction, lock the target organization row first, re-read session, selected organization and membership under the same transaction, and authorize `ManageReferenceData` after locking. Only then insert/update/archive a row whose `organization_id` matches the locked scope. Do not rely on a pre-transaction `OrganizationScope` for write permission. Use version predicates (`WHERE id = ... AND organization_id = ... AND version = expectedVersion`), exactly one affected row and version increment. A stale update/archive returns 409, with no partial write. Independent tenants may reuse the same `ReferenceCode` or external identifier without sharing rows.

Select `organization_id` from the trusted server session; route `organizationId`, request bodies, cookie role text and custom headers are only locators or untrusted input. Never accept `organizationId` as a writable record property. All foreign UUIDs must receive the same sanitized 404 envelope as nonexistent UUIDs after valid own selection. Missing selection gives 409 and wrong-role access gives 403, using the accepted M2 error codes. Revoked/suspended membership must fail closed, including when changed from a different session. Database outage gives sanitized 503 and no cached tenant-specific data.

## 7. Proposed versioned HTTP contracts

All endpoints are under `/api/v1/organizations/{organizationId}` and require authenticated same-origin sessions, server selection and typed reference permissions:

| Method | Route | Purpose | Permission |
| --- | --- | --- | --- |
| GET | `/ports` | Bounded search/list | ViewReferenceData |
| GET | `/ports/{portId}` | One scoped port | ViewReferenceData |
| POST | `/ports` | Create port | ManageReferenceData |
| PATCH | `/ports/{portId}` | Versioned mutable fields/status update, including explicit restore | ManageReferenceData |
| DELETE | `/ports/{portId}` | Soft archive with version precondition, 204 | ManageReferenceData |
| GET | `/vessels` | Bounded search/list | ViewReferenceData |
| GET | `/vessels/{vesselId}` | One scoped vessel | ViewReferenceData |
| POST | `/vessels` | Create vessel | ManageReferenceData |
| PATCH | `/vessels/{vesselId}` | Versioned mutable fields/status update, including explicit restore | ManageReferenceData |
| DELETE | `/vessels/{vesselId}` | Soft archive with version precondition, 204 | ManageReferenceData |

List query: `?q=<text>&status=Active|Archived|All&limit=1..100&after=<uuid>`. Default status `Active`, limit 25, order by immutable UUID ascending with `id > after`; next cursor `nextAfter` is the last returned UUID only if an additional result exists. Search `q` is a trimmed, bounded 1-80-character substring across name/local code and external identifiers, with LIKE wildcards escaped or parameterized. No unbounded offset, wildcard injection or external search provider. A malformed filter/cursor returns 400.

Create request accepts `referenceCode`, `name` and the permitted optional port/vessel fields. The server supplies `id`, `organizationId`, timestamps, `version=1`, `status=Active`, and `sourceKind=UserProvided`; test-only seed may mark `Synthetic`. Creation returns 201 with the stored record. PATCH accepts `expectedVersion` and only documented mutable fields; `id`, `organizationId`, `referenceCode`, provenance and creation timestamp are immutable. DELETE requires a JSON body `{"expectedVersion":1}`; it archives idempotently only under the documented version policy, returning 204 on a successful transition and 409 on stale/already-archived versions. Explicit restore through PATCH requires an expected version and must preserve identifier uniqueness.

Successful reads return only needed fields and `Cache-Control: no-store`. Error objects retain `code`, `message`, `correlationId`; common codes are 400 `invalid_request`, 401 `authentication_required`, 403 `access_denied`/`csrf_invalid`, 404 `resource_not_found`, 409 `organization_selection_required`/`conflict`, 503 `dependency_unavailable`. Include `X-Correlation-ID` and enforce body-size bounds. No stack traces, SQL, sensitive identifiers, registry credentials or arbitrary internal database error messages in responses/logs.

## 8. Browser integration

Build a `ReferenceDataWorkspace` beneath the existing authenticated organization workspace, using the same server-selected organization context. Provide `Ports` and `Vessels` tabs with compact search, status filtering, paged rows, detail views and clear empty/loading/error states. The Manager role alone sees create/edit/archive/restore controls, and API permission checks still enforce all actions.

When the selected organization changes, increment an in-flight request sequence (or abort pending requests), immediately discard old port/vessel rows and detail state, fetch the new server context and then load references. Never render stale previous-tenant results during a pending switch, logout, revocation, suspension or API 503. Show a recovery action for 409 selection-required and 503 dependency failures. Do not leak foreign data into browser storage, diagnostics or screenshots. Keep M2 membership administration functional.

Functional verification runs in Chromium, Firefox and WebKit with the same M2 test authentication approach. Formal accessibility audit remains scheduled only for M24-04; M3 still uses standard labeled controls and semantic HTML without claiming an accessibility gate.

## 9. Extension seams and explicit deferrals

M4 voyage routes may reference M3 vessel/port UUIDs; M5 shipments and M6 assignment checks must verify that related records belong to the same organization and satisfy their own active/archived policy. Do not build M4 tables or external-code reconciliation in M3. Avoid cascade delete to protect future historical references.

UN/LOCODE geographic existence, function codes, approval state, IMO registry matching and MMSI assignment verification are outside M3. For later real-world enrichment, pin an approved official publication, record licensing and version, prove provenance and validate updates through a separate gate. M3 establishes syntax and uniqueness, not authoritative registry certification.

## 10. Reference material and design review

- UNECE Recommendation No. 16, UN/LOCODE structure and locality versus facility distinction: https://unlocode.unece.org/recommendation16/
- IMO Ship Identification Number Scheme, current seven-digit example and possible future extension: https://www.imo.org/en/ourwork/iiis/pages/imo-identification-number-schemes.aspx
- ITU-R M.585 maritime identities and nine-digit MMSI: https://www.itu.int/dms_pubrec/itu-r/rec/m/R-REC-M.585-9-202205-I%21%21PDF-E.pdf

**Implementation note:** The accepted port/vessel model and API are integrated into `main`, with real PostgreSQL, HTTP and three-browser testing against `b72025d35d62d0a8a45b466986a0b3bd89eebadb`. The final EF Core mapping treats `PortReference` and `VesselReference` as concrete entities and excludes `ReferenceRecord` from the entity model. All G3 scenarios passed; see [executed verification](M3_EXECUTED_VERIFICATION.md). G3 was explicitly accepted on 2026-10-31; no M4 implementation or production deployment is implied.
