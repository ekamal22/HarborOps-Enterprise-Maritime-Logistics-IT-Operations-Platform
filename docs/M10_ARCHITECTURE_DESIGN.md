# M10 Architecture: Event Timeline and Deterministic Projection Rebuild

**State:** Proposed architecture, not implemented, executed or accepted.  
**Prepared:** 2026-11-05  
**Entry:** Accepted G9. **Exit:** G10, after M10-01 through M10-04 and independent acceptance.  
**Authoritative scope:** [Roadmap](ROADMAP.md), [execution graph](execution-graph.json), [FR-009 / FR-002 / NFR-004 / NFR-007](REQUIREMENTS.md), [Domain Model](DOMAIN_MODEL.md), [M9 contract](M9_LOCAL_CONTRACT_DECISION.md), [M9 closure](M9_G9_GATE_CLOSURE.md).

## 1. Baseline and non-negotiable boundaries

M7 authenticates and validates the HarborOps tracking profile. M8 atomically commits immutable `TrackingInboxReceipt` and `TrackingAcceptedEvent` before returning 202. M9 separately leases `TrackingInboxWorkItem` and commits a unique immutable `TrackingProcessingEffect` for unambiguous equipment-to-container-to-shipment resolution. M9's processing does **not** project shipment tracking status or ETA. M9 parks unknown and opaque transport references in `NeedsReconciliation`. A receipt acknowledgment is not proof of resolution or projection.

Preserve unchanged:
- M8 source/organization/profile/source-event uniqueness, payload, replay/collision semantics and accepted-event immutability.
- M9 receipt-unique resolution effect, fencing, operator redrive, append-only attempts, tenant-aware historical assignments and the 10,000 total unresolved-work cap.
- M5 `ShipmentRecord.WorkflowStatus` and `ShipmentStatusChange` as **manual business workflow**, separate from any M10 **observed tracking state**. Tracking data cannot silently drive M5 lifecycle transitions.
- G9's sustained transport-processing hold until a separately approved and verified backlog-management path exists. M10 planning or G10 acceptance alone does **not** lift it.
- M16's independence. Audit-read permission `ViewAuditEvents`, migrations and worker-role policy require coordinated integration if M16 later merges. No G16 acceptance is inferred.

## 2. Data sources and event identity

The accepted model exposes `TrackingAcceptedEvent` (Id, ReceiptId, OrganizationId, SourceId, EventFamily, EventCode, Classifier, OccurredAtUtc, EquipmentReference, TransportReference, RawPayloadUtf8 and CanonicalPayloadJson), `TrackingInboxReceipt` (source-specific event ID, profile, digest and ReceivedAtUtc), and M9 `TrackingProcessingEffect` (one per resolved receipt, OrganizationId, SourceId, ResolutionKind, ResolutionPolicy, ContainerId, ShipmentId and ResolvedAtUtc).

**Eligibility:** A shipment timeline includes only same-organization events with a valid immutable M9 effect linking to that shipment; the original accepted record remains available to authorized operational investigation even if not yet resolved. Unknown, foreign, ambiguous and parked transport events are **not** guessed onto a shipment. Preserve accepted-but-unresolved visibility in existing authorized `tracking-work` operations.

**Ordering contract proposed for M10-01:** Sort by `OccurredAtUtc` ascending (UTC instant), then `ReceivedAtUtc`, `SourceId`, receipt's `SourceEventId` (ordinal), `ReceiptId` and event `Id` as stable final tie breakers. The current M7 profile has **no source sequence field**; never claim sequence-based ordering. Require explicit conflict indication when distinct same-time events imply incompatible observations; ordering resolves presentation and replay determinism, not causality. Repeated or late arrivals preserve occurrence and receipt times.

## 3. New read model and immutable corrections

**Proposed additive M10 tables** (names subject to M10-01 contract freeze):
- `shipment_tracking_projections`: composite organization/shipment identity, observed equipment state (`Unknown`, `EquipmentIn`, `EquipmentOut` or `Conflict`), last effective event ID/time, nullable observed ETA, rule/policy version, monotonically changing materialization revision, canonical input digest, refreshed-at UTC and lag metadata. ETA remains **null / unavailable** under the currently supported GTIN/GTOT + opaque ARRI/DEPA payload; do not fabricate time, location or operational arrival.
- `shipment_tracking_corrections`: append-only, tenant-scoped record linking an accepted event to an explicit superseding accepted event or a documented void of its projection effect; actor, reason, UTC, correction generation and correlation ID. No raw accepted payload mutation, no cross-tenant or cross-shipment link, no arbitrary synthetic replacement event. A deterministic active correction chain must reject cycles and contradictory concurrent replacements.
- `shipment_projection_applications` (or equivalent): durable, receipt-unique evidence that an M9 resolved effect has been considered by a defined projection policy, with organization, shipment, policy version and applied-at UTC. Its uniqueness and reconciliation rules prevent both lost and double application. This is a *derived* bookkeeping table, not a second M9 business effect.

A projection may be rebuilt from the complete eligible immutable history plus append-only correction chain and policy version. The input digest is a canonical digest of the ordered effective receipt/event IDs, relevant correction IDs and policy identifier. Equal inputs must yield equal derived state and digest; rerunning an identical rebuild must **not** increment the materialization revision or create duplicate application evidence.

**Status boundary:** `GTIN`/`GTOT` can establish observed equipment in/out evidence only. It does not prove `Draft`, `Booked`, `InTransit`, `Arrived` or `Completed` for the M5 shipment, and does not calculate an ETA. Contradictory same-instant evidence yields `Conflict` until an explicit correction resolves it. Missing usable events yield `Unknown`.

## 4. Consistency, retries and rebuild

Do not modify M9 finalization to write an M10 projection within its fenced claim transaction. An M10 projector independently discovers all `TrackingProcessingEffect` receipts lacking matching application evidence for the current policy. Use an indexed anti-join or equivalent durable comparison, **not** a simple timestamp-only high-water mark that can skip a transaction committed late. Include bounded organization/shipment batches, backpressure, opt-in service startup and crash-safe idempotent recovery.

For a shipment materialization, acquire organization and shipment locks in the agreed order, respecting M8/M9 lock hierarchy. Recheck active organization and the authorized resolved effect set in the same database transaction, compute from a consistent snapshot, and commit projection + application rows together. Avoid a stale overwrite if a concurrent M9 effect commits. Document isolation level and deadlock/retry policy before code; prefer bounded retries and exact tests over speculative concurrency claims.

An explicit rebuild operation independently recomputes the complete eligible history in deterministic order. It compares the canonical digest and derived rows, fixes drift transactionally, and records observed old/new digest without exposing raw source payload. Rebuild must be resumable after process termination, never mutate receipt/event/effect history, and never hide unprocessed or parked work. If an incident occurs before materialization commit, durable input remains eligible for a later retry.

## 5. API and authorization proposal

Use the existing authenticated selected-organization model and the accepted `OrganizationPermission.ViewShipmentData` for reads: Analyst, Manager and Auditor. Never grant implicit access to Support or Administrator, and never infer authorization from an organization UUID.

| Proposed operation | Contract | Scope |
| --- | --- | --- |
| `GET /api/v1/organizations/{organizationId}/shipments/{shipmentId}/events` | Bounded keyset-ordered timeline, `items`, opaque `nextCursor`, `projectionRevision`, event/source provenance summary, correction/supersession flags, receipt and occurrence UTC. No raw payload, bearer, digest or secret | ViewShipmentData, live session and selected active organization |
| `GET /api/v1/organizations/{organizationId}/shipments/{shipmentId}/tracking-projection` | Observed tracking state, last event/time, nullable ETA, policy/version, digest identifier and lag/staleness flag. Clearly separate from `workflowStatus` | ViewShipmentData |
| `POST /api/v1/organizations/{organizationId}/shipments/{shipmentId}/tracking-corrections` | Proposed explicit append-only void/supersede request with expected correction generation, target receipt, optional replacement receipt and sanitized reason | Manager only, separately frozen mutation policy, CSRF/Origin and idempotency |
| `POST /api/v1/organizations/{organizationId}/shipments/{shipmentId}/tracking-projection/rebuild` | Proposed bounded administrative rebuild with optional expected revision, atomic outcome and correlation ID; no arbitrary org/receipt selection | Manager only, CSRF/Origin, bounded rate and audit evidence |

Correction/rebuild mutations are **proposals pending M10-01 authorization contract approval**. New `ManageTrackingCorrections` permission is not automatically authorized: prefer existing Manager-only `ManageShipmentData` if formal review confirms that scope, or introduce a distinct explicit permission with full M2 authorization and M16 compatibility review. No browser integration or endpoint exists yet.

Timeline cursors must be opaque, bounded, tenant/shipment/filter-bound, validated and stable against inserted late events. Use a snapshot/reference revision or reject a stale cursor with a documented restart response. No offset pagination, cross-tenant cursor reuse, unrestricted export or unsafe raw data.

## 6. Opaque transport backlog decision

M7 `Transport` ARRI/DEPA contains an opaque `TransportReference`. It is **not** a verified voyage ID. M9 intentionally parks all such records. An inferred link based only on string similarity, port or timestamp is forbidden.

M10-01 must explicitly decide whether verified transport-association and bounded backlog drain is inside the G10 scope or deferred to a separate approved mechanism. If included, require an authorized, versioned same-tenant map from external transport reference to an exact active voyage and relevant event-time-valid M6 shipment assignment, with uniqueness, time boundaries, provenance and conflict handling. Extend M9 effect resolution only through a separately tested controlled migration/contract change, then prove capacity remains bounded and no duplicate receipt effect or invalid shipment attachment results.

**Until independently verified and explicitly enabled, sustain the G9 hold regardless of G10 progress.** Do not consume, delete, mark Processed or silently deprioritize parked transport events to relieve capacity.

## 7. Known integration and risk boundaries

- M10 schema is additive after accepted M9 migration `20261104190000_TrackingWorker`. The unmerged M16 migration `20261104180000_AuditEntries` may need controlled migration-history and EF snapshot integration; never assume one branch's snapshot is authoritative in the other.
- Protect direct SQL, EF and endpoint access with same-tenant referential integrity and real PostgreSQL rejection tests. Audit any projection repair or correction as append-only operational evidence without claiming M16 audit acceptance.
- Preserve source credentials, organization selection and session revocation, active/archived record rules, M5 optimistic workflow writes, M6 half-open assignment intervals and G9 work-state policy.
- M11 exceptions, M12 advanced search/export, M13 KPIs, M15 notifications, public hosting, and all formal accessibility testing (M24-04 only) are outside G10.
- Every invariant above is **proposed** until contract review and executed source-locked G10 evidence. No silent contract approval through implementation.
