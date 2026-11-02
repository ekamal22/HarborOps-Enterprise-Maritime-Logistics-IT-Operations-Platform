# M5 Shipment Working Contract: Decision Record

**State:** Proposed implementation contract, acceptance pending
**Milestone:** M5-01; explicit G5-S01 prerequisite
**Recorded:** 2026-11-01
**Implementation branch:** `feat/m5-shipment-lifecycle`

The M5 code is authored against the provisional choices below. Implementing or committing source does not constitute formal decision acceptance, G5 acceptance, or merge authorization.

## Proposed contract

1. Shipment workflow: `Draft -> Booked -> InTransit -> Arrived -> Completed`, with cancellation from Draft, Booked or InTransit. Completed and Cancelled are terminal. Manual workflow status is distinct from tracking-derived status.
2. M5 shipments require distinct active same-organization M3 origin/destination port UUIDs, with nullable explicit UTC planned windows. M6 alone owns container and voyage assignment intervals.
3. Analyst and Manager have typed shipment read/write permissions. Auditor has typed shipment read-only permission, without M3/M4 reference access. Administrator and Support have no implicit shipment access.
4. Immutable shipment UUID, organization, tenant-local reference, optional normalized external reference, port route and source. Archive retains uniqueness across archived records and is independent of workflow progress. Exact positive version required for all mutations after creation.
5. Each successful creation/manual workflow transition records an immutable status change atomically in PostgreSQL. It is not the later accepted tracking event history or generalized M16 audit.
6. M5 provides bounded list/detail/filter and basic search. Advanced shipment export and rich search are M12; tracking-derived status is M9/M10.

**Decision authority:** Requires explicit confirmation of this local contract before G5-S01 closure. [M5 architecture](M5_ARCHITECTURE_DESIGN.md), [implementation status](M5_IMPLEMENTATION_STATUS.md), [source audit](M5_SOURCE_CORRECTNESS_AUDIT.md) and [verification plan](M5_VERIFICATION_PLAN.md) remain the authoritative technical records. No new global D-number or dependency graph edge is introduced.

**Compliance audit:** the [contract compliance matrix](M5_G5_S01_CONTRACT_COMPLIANCE.md) maps each of the six rules to source, tests and executed evidence. The audit does not change the state of this record, which remains proposed with acceptance pending.

**Verification impact:** The G5 checker must report G5-S01 incomplete until this record has an explicit accepted state. Other functional tests may execute without rewriting this historical decision record. G5 acceptance remains separate.
