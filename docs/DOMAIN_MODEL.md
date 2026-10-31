# Domain Model and Invariants

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
| AuditRecord | Id, actor, action, resource, timestamp, metadata | Append-only sensitive action record |
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
7. Organization and membership invariants remain M2 scope; authentication alone is not business authorization.

## M2 organization model (implemented in feature branch; migration not executed)

The proposed [M2 architecture](M2_ARCHITECTURE_DESIGN.md) adds additive `organizations` and `organization_memberships` tables, one effective role per user/organization, independent role membership states and a per-session nullable selected-organization reference. The explicit role set is Analyst, Manager, Support, Administrator and Auditor. A user's active membership in organization A confers no permissions in B. Administrator grants are limited to the authorized organization and do not imply business-data access.

Required invariants include uniqueness of organization slug and user/organization membership, soft removal, active-organization checks, live role revalidation, versioned updates and transactional protection against loss of the final active Administrator. The accepted M1 cookie does not carry trusted organization roles. These invariants are represented in feature-branch source and an additive migration, but the migration and tests have not been executed. See [M2 source register](M2_IMPLEMENTATION_STATUS.md), [implementation sequence](M2_IMPLEMENTATION_PLAN.md) and [G2 verification](M2_VERIFICATION_PLAN.md).

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

Organization membership and access boundaries are planned for M2; maritime reference entities and shipment/assignment invariants are planned for M3-M6. None of these business entities were implemented in the M0 engineering baseline.

The four M1 identity tables and migration were exercised in isolated PostgreSQL integration tests, including an existing M0-seeded schema upgrade. See [M1 verification register](M1_IMPLEMENTATION_VERIFICATION.md). G1 was accepted on 2026-10-31 and merged into `main`; M2 organization isolation remains planned.
