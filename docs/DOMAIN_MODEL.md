# Domain Model and Invariants

**M11/M12 coordination approved (2026-11-08):** [Combined integration and verification plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md). The integrated candidate must preserve the M11 exception/history model, M12 shipment search/export contracts, accepted M5 shipment invariants, M10 observation provenance and M16 audit atomicity.

**Parallel M11/M12 source status (2026-11-08):** [Source and integration register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md) records independent unmerged source candidates in draft PRs #31 and #32. M11 policy approval is blocked, both technical gate campaigns are unexecuted, and acceptance pending. Protected `main` and the M9 transport hold are unchanged.

**Current M11 source audit (2026-11-08):** [Sixth correctness audit](M11_SIXTH_CORRECTNESS_AUDIT_20261108.md) documents informational severity on inactive outcomes, versioned rule-metadata reconciliation and typed due-work continuation. The source changes and new tests are unexecuted. G11-S01 BLOCKED, G11-S02 through S25 NOT EXECUTED, acceptance pending. The M9 transport-processing hold remains in force.

**Current integrated baseline (2026-11-07):** G0-G10 and G16 are accepted on protected `main`. The M10 signed-correction remediation merged through [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) following S01/S25 independent review; see [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md). Earlier G10 pending statements are historical source-locked records. The M9 transport-processing hold remains mandatory. No deployment or release is authorized.

**Current integrated baseline (2026-11-07):** G0-G10 and G16 are accepted on protected `main`. The M10 signed-correction remediation merged through [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) following S01/S25 independent review; see [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md). Earlier G10 pending statements are historical source-locked records. The M9 transport-processing hold remains mandatory. No deployment or release is authorized.

## M11 provisional recurrence identity

A verified inactive Acknowledged occurrence is closed on direct recurrence, preserving immutable system AutoResolved history. The newly active incident receives a separate UUID and incremented occurrence under the existing partial unique index. Unknown/Stale does not establish clearance. This remains source-only and subject to M11-P10 approval.

## M11 scoped immutable-history retrieval

The underlying `shipment_exception_history` rows retain a composite scoped FK and append-only trigger. The new `HistoryPageAsync` query uses an ordered `(occurred_at_utc,id)` keyset. Protected history continuation cursors are request tokens, not stored business records; the first 500 detail events are a preview rather than an irreversible retention ceiling. Runtime verification and conflict-specific event lineage remain pending.

## M11 provisional persistence model

SQL-only migration 14 adds tenant-bound `shipment_exceptions`, append-only `shipment_exception_history`, rule-versioned `shipment_exception_evaluations`, organization-unique `shipment_exception_replays`, and bounded-expiry `shipment_exception_proof_nonces`. Input checkpoints reference accepted M10 `shipment_tracking_input_versions.input_revision`, not a nonexistent `revision` column. This branch is not a migrated or accepted database baseline.

## Entities

| Entity | Key fields | Notes |
| --- | --- | --- |
| Organization | Id, name, status | Security and reporting boundary |
| User | Id, identity fields, status | May belong to multiple organizations through membership |
| UserCredential (M1 implemented) | UserId, Argon2id encoded password hash, hash version, password changed UTC | Exactly one active credential row per local user; never plaintext |
| AuthSession (M1 implemented) | Id, UserId, issue/idle/absolute UTC, security version, revocation | Server authoritative session; many sessions per user |
| AuthEvent (M1 implemented) | Id, nullable UserId/SessionId, type, result, correlation, UTC | Bounded, redacted identity events; not the full M16 audit module |
| Membership | UserId, OrganizationId, role, state | Access is evaluated within active membership |
| Port | Id, UN/LOCODE where valid, country, name | Reference data; changes are versioned where needed |
| Vessel | Id, optional IMO/MMSI, name | External identifiers must be validated and never fabricated as verified |
| Voyage | Id, vessel, route, departure/arrival windows | Planned and actual times are distinct |
| Container | Id, identifier, type | ISO 6346 validation when using real-format IDs |
| Shipment | Id, OrganizationId, reference, status, milestones | Internal canonical shipment record |
| ShipmentContainer | ShipmentId, ContainerId, active interval | Prevents overlapping invalid assignments |
| VoyageAssignment | ShipmentId, VoyageId, active interval | Captures planned and actual associations |
| TrackingEvent | Id, organization, source, source key, type, occurred/received time, payload | Immutable accepted event |
| EventInbox | ReceiptId, idempotency fingerprint, result, error code | Durable ingestion and retry record |
| ShipmentProjection | ShipmentId, computed status, ETA, version | Derived, rebuildable read model |
| ShipmentException | Id, shipment, rule, severity, state | Investigable exception |
| Notification | Id, recipient scope, type, status | Derived from authorization-aware rules |
| AuditEntry (M16 implemented) | Id, OrganizationId, EventType, Outcome, ActorKind, ActorUserId/ActorSourceId, ResourceType/Id, CorrelationId, OccurredAtUtc, bounded Metadata | Append-only transaction-coupled record; selected-organization Auditor reads only, no global export; M9 worker history remains separate |
| ServiceIncident | Id, priority, state, title, cause, timestamps | Infrastructure and application investigations |
| IncidentActivity | Id, incident, actor, timestamp, note/type | Chronological incident history |

## Model invariants

1. A shipment belongs to exactly one organization.
2. A caller may never read or mutate a shipment using only its public identifier without verifying organization scope.
3. A container cannot have incompatible overlapping active assignments.
4. A voyage's scheduled times must be coherent; incomplete schedules remain explicitly unknown.
5. Event occurrence time and event receipt time are stored independently in UTC.
6. The same source event may be delivered repeatedly but may produce at most one accepted business effect.
7. Source events do not directly mutate audited history.
8. Event corrections are represented as explicit correction/supersession events with links; never erase prior history.
9. Current shipment status is a derived projection backed by ordered accepted events and documented tie-breaking rules.
10. Unknown events do not silently advance or reverse the shipment state.
11. Audit records capture actor, organization scope, resource, action, outcome, and correlation ID, with sensitive values redacted.
12. Deleting an operational entity is restricted when history depends on it; use archival or tombstones where applicable.

## M1 identity persistence and invariants (accepted in main)

[The implemented M1 schema](M1_AUTHENTICATION_DESIGN.md) adds `users`, `user_credentials`, `auth_sessions` and `auth_events` in an additive EF Core migration. Accounts have unique normalized emails, Active/Disabled status and a monotonically increasing `security_version`. A user has at most one current password credential and can have multiple concurrent sessions.

1. Only an active account with valid Argon2id credentials can create a session.
2. An authenticated request succeeds only if its protected ticket refers to an existing active user and an unrevoked session within both idle and absolute expiries with matching security version.
3. Revocation, disablement and password reset are transactional. Disable/reset increments the security version and makes old cookies unusable immediately.
4. Expired/revoked sessions cannot be resurrected by sliding refresh or concurrent requests.
5. Security events never include secrets or raw credentials; full business audit remains M16.
6. The existing `bootstrap_records` synthetic seed remains unchanged and does not provision an account.
7. Organization and membership invariants were implemented and verified in M2. Authentication alone is not business authorization; tenant scope and role permissions remain server-enforced.

## M2 organization model (implemented, migrated and accepted on `main`)

The implemented [M2 architecture](M2_ARCHITECTURE_DESIGN.md) adds `organizations` and `organization_memberships` tables, one effective role per user/organization, independent role membership states and a per-session nullable selected-organization reference. The explicit role set is Analyst, Manager, Support, Administrator and Auditor. A user's active membership in organization A confers no permissions in B. Administrator grants are limited to the authorized organization and do not imply business-data access.

Required invariants include unique organization slug and user/organization membership, soft removal, active-organization checks, live role revalidation, versioned updates and transactional protection against loss of the last active Administrator. The accepted M1 cookie does not carry trusted organization roles. The M2 additive migration and invariants passed fresh and repeated disposable PostgreSQL verification; see [G2 executed evidence](M2_EXECUTED_VERIFICATION.md).

## M3 port and vessel reference model (verified and accepted)

[M3 architecture](M3_ARCHITECTURE_DESIGN.md) specifies organization-owned `PortReference` and `VesselReference` tables with immutable UUID, organization ID and tenant-local `ReferenceCode`; name, optional external identifiers, Active/Archived state, provenance, optimistic version and server UTC timestamps. Port records have required country and optional matching UN/LOCODE. Vessel records have optional current-format IMO, nine-digit MMSI and flag country. Unique identifiers are enforced per organization, including archived rows. Selection and typed business-data permissions must be revalidated for every operation; an M2 Administrator has no automatic reference-data access.

M3 `ports` and `vessels` entity mappings, additive migration, scoped endpoints and frontend are integrated into `main`. All 25 G3 scenarios passed locally at `b72025d35d62d0a8a45b466986a0b3bd89eebadb`, with independent raw-evidence review and explicit acceptance on 2026-10-31. See [implementation register](M3_IMPLEMENTATION_STATUS.md), [executed verification](M3_EXECUTED_VERIFICATION.md) and [G3 specification](M3_VERIFICATION_PLAN.md). M4 now uses these immutable M3 port/vessel UUIDs and was accepted on 2026-11-01.

## M4 implemented reference models

The implemented [M4 architecture](M4_ARCHITECTURE_DESIGN.md) separates planned and actual UTC voyage timestamps, `Planned|Underway|Arrived|Cancelled` progress and `Active|Archived` record visibility. Same-tenant composite foreign keys bind vessel and port UUIDs. Canonical container identifiers and immutable kind, equipment type, source classification and version follow accepted D-003 Option B: ISO 6346:2022 freight category U or visibly tagged Synthetic. M5 shipment state and M6 assignment intervals remain outside the accepted M4 module. See [G4 executed verification](M4_EXECUTED_VERIFICATION.md).

## M5 shipment aggregate, M6 assignments and accepted M7 source credentials

[M5 architecture](M5_ARCHITECTURE_DESIGN.md) defines the implemented shipment aggregate: immutable tenant-owned shipment identifiers, two same-tenant M3 ports, nullable planned UTC windows, Draft/Booked/InTransit/Arrived/Completed/Cancelled manual progress, archive state, optimistic versions and append-only status changes. [G5](M5_G5_GATE_CLOSURE.md) was explicitly accepted on 2026-11-02. [M6 architecture](M6_ARCHITECTURE_DESIGN.md) adds organization-scoped shipment/container and shipment/voyage association histories with half-open UTC intervals, database overlap exclusions, versioned closure and parent lifecycle guards; [G6](M6_G6_GATE_CLOSURE.md) was accepted on 2026-11-02. Neither milestone asserts live tracking-event ingestion. M7 adds restricted source credentials and strict validation while returning 503 instead of acknowledging receipt. See [G7 closure](M7_G7_GATE_CLOSURE.md).

## M8 receipt and immutable event model (accepted)

Accepted M8 defines `TrackingInboxReceipt` with a permanent unique organization/source/profile/source-event key, canonical digest and provenance; `TrackingAcceptedEvent` holds immutable normalized events and bounded accepted raw bytes; `TrackingInboxWorkItem` holds separate processing state; `TrackingReceiptConflict` holds bounded collision metadata. Its additive migration, scoped constraints and append-only guards were verified, and D-006 Option A and G8 were accepted on 2026-11-04. The implementation is integrated into `main`; see [G8 gate closure](M8_G8_GATE_CLOSURE.md). M9 extends worker processing, while M10 owns timeline projections.

## Illustrative lifecycle

    Draft -> Booked -> In Transit -> Arrived -> Completed
      |       |           |            |
      +-------+-----------+------------+--> Cancelled (policy-gated)

This is an initial internal lifecycle, not an asserted industry standard. Container event types and operational shipment status must not be conflated.

## Event time and ordering

Order primarily by trusted occurrence time when source rules permit, then by source sequence if available, and finally by deterministic stable identifier. When this is insufficient to infer true state, surface a reconciliation exception rather than inventing certainty.

## Data generation

- Seed users, organizations, ports, vessels, routes, voyages, containers, shipments, events, and incidents from a fixed seed.
- Create valid-looking but unmistakably synthetic identifiers; avoid real customer or employee details.
- Include on-time, delayed, missing, duplicated, out-of-order, invalid, and corrected event scenarios.
- Document counts and seed version so KPI test results can be reproduced.
- Always label simulated location traces and estimated arrival predictions.

## M0 physical schema implemented

`bootstrap_records` is a single infrastructure status table, not a shipment-domain model. The initial EF Core migration creates `key` (80-character primary key), `dataset_version` (required, 80 characters), and `seeded_at_utc` (required UTC timestamp). The deterministic row uses key `harborops` and dataset version `m0-synthetic-v1`. The migration and seed were applied to clean PostgreSQL 16.15 databases and inspected on 2026-10-30 (see [M0 Verification](M0_VERIFICATION.md)).

Organization membership and access boundaries were accepted in M2; organization-owned port/vessel references were accepted in M3. Voyage/container references were accepted in M4, shipment lifecycle in M5, assignments in M6 and tenant-bound source credentials in M7. Durable event receipts and accepted event history remain M8 work.

The four M1 identity tables and migration were exercised in isolated PostgreSQL integration tests, including an existing M0-seeded upgrade; G1 was accepted on 2026-10-31 and merged into `main`. M2 organization persistence and tenant isolation also passed local G2 verification, were accepted and were merged via PR #5. See [M1 evidence](M1_IMPLEMENTATION_VERIFICATION.md) and [M2 evidence](M2_EXECUTED_VERIFICATION.md).

## M9 tracking worker model (accepted and integrated)

Accepted M9 extends `TrackingInboxWorkItem` with `lease_token`, monotonic `lease_epoch`, database-issued expiry/availability, bounded attempts and redrive generation, sanitized error and completion/resolution fields. States are `Pending`, `Leased`, `RetryWaiting`, `NeedsReconciliation`, `Processed` and `DeadLetter`. Accepted M8 receipt/event/collision history remains immutable.

Append-only `TrackingWorkAttemptEvent` records processing transitions with actor attribution for manual redrive. A receipt-unique `TrackingProcessingEffect` represents proven resolution to one active same-organization container and one unambiguous event-time-valid shipment assignment. Unproven equipment and opaque transport references park in `NeedsReconciliation`, never in guessed successful effects.

Migration `20261104190000_TrackingWorker` and the matching EF model are integrated into `main` after G9 acceptance on 2026-11-05. M9 does not implement shipment status, ETA, timeline or rebuildable projections, which belong to M10. See the [M9 contract](M9_LOCAL_CONTRACT_DECISION.md), [evidence](M9_EXECUTED_VERIFICATION.md) and [G9 closure](M9_G9_GATE_CLOSURE.md).

**Operating condition:** M9 sustained transport-event processing remains disabled until M10 or a separately approved and verified mechanism handles the parked backlog under the 10,000 unresolved-work-item limit. M16 audit logging and access hardening was independently verified, accepted and merged on 2026-11-06; M9 attempt/effect history remains the worker audit of record. Shipment projection remains planned M10 work without accepted G10 evidence.

## M10 correction-security data model, 2026-11-07

The approved L07/L14 source remediation adds one SQL-only thirteenth migration after the original twelve-migration M10 projections. The private `m10_correction_proof_keys` and append-only `m10_correction_proof_nonces` support signed, short-lived correction authority; a restricted database function checks live Manager/session scope and validates the proof. A successful correction must also persist a scoped `audit_entries` row of type `shipment.tracking_correction_created` with privacy-allowlisted metadata in the same transaction. The original M10 corrections remain immutable and M8/M9 history is not rewritten.

M10's correction security is accepted and merged via [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) after S01/S25 review. Conditional L06 Manager semantics were verified through L14 database authorization. M10 does not compute ETA or process parked ARRI/DEPA traffic. [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md).

## Accepted M10 indexed timeline and projection model

Accepted M10 includes the SQL-only timeline entries, input-version tracking, projections, corrections, correction heads and operation replay records. Migration 13 adds restricted signed correction authority and atomic M16 audit, while delivering a historical-upgrade-safe projector scope function. See [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md).
