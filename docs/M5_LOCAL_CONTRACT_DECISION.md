# M5 Shipment Working Contract: Decision Record

**State:** Accepted
**Milestone:** M5-01; explicit G5-S01 prerequisite
**Recorded:** 2026-11-01
**Accepted:** 2026-11-02 by explicit approval of all six contract rules and G5-S01
**Implementation branch:** `feat/m5-shipment-lifecycle`

The complete six-rule contract below was explicitly accepted on 2026-11-02. G5-S01 and the separate full G5 gate were accepted following independent evidence review. See [gate closure](M5_G5_GATE_CLOSURE.md).

## Accepted contract

1. Shipment workflow: `Draft -> Booked -> InTransit -> Arrived -> Completed`, with cancellation from Draft, Booked or InTransit. Completed and Cancelled are terminal. Manual workflow status is distinct from tracking-derived status.
2. M5 shipments require distinct active same-organization M3 origin/destination port UUIDs, with nullable explicit UTC planned windows. M6 alone owns container and voyage assignment intervals.
3. Analyst and Manager have typed shipment read/write permissions. Auditor has typed shipment read-only permission, without M3/M4 reference access. Administrator and Support have no implicit shipment access.
4. Immutable shipment UUID, organization, tenant-local reference, optional normalized external reference, port route and source. Archive retains uniqueness across archived records and is independent of workflow progress. Exact positive version required for all mutations after creation.
5. Each successful creation/manual workflow transition records an immutable status change atomically in PostgreSQL. It is not the later accepted tracking event history or generalized M16 audit.
6. M5 provides bounded list/detail/filter and basic search. Advanced shipment export and rich search are M12; tracking-derived status is M9/M10.

**Decision authority:** All six rules explicitly approved on 2026-11-02. No new global D-number or graph dependency is introduced.

**Compliance audit:** The [contract compliance matrix](M5_G5_S01_CONTRACT_COMPLIANCE.md) maps all six accepted rules to implementation and raw verification evidence.

**Verification impact:** Run 13 recorded G5-S01 incomplete at execution time, before the contract was accepted. The explicit 2026-11-02 approval closes S01. The raw run remains unchanged; full G5 was separately accepted after independent review.
