# Domain Model and Invariants

## Entities

| Entity | Key fields | Notes |
| --- | --- | --- |
| Organization | Id, name, status | Security and reporting boundary |
| User | Id, identity fields, status | May belong to multiple organizations through membership |
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
