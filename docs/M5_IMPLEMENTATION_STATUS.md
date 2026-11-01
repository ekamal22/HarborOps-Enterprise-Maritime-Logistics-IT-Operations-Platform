# M5 Shipment Lifecycle: Planning and Implementation Status

**As of:** 2026-11-01
**Milestone:** M5, G5
**State:** Architecture and implementation planning authored on `feat/m5-shipment-lifecycle`. Implementation has **not started**. G5 remains OPEN and acceptance pending.
**Baseline:** G0-G4 accepted and integrated into `main` at `18d00e6710097e4e31a87190be8edb65337d1b50`.
**Dependencies:** M5-01 requires accepted G3. M5-02 and M5-03 follow M5-01 and can proceed in parallel; M5-04 requires both. M6 requires **both G4 and G5**.

## Approved baseline and planned scope

M4 supplies validated voyages and containers but M5 does **not** assign them to shipments. The M5 module is intended to own scoped shipment CRUD, manual lifecycle transitions, concurrency control, searchable records, archival, and append-only manual workflow status changes. Accepted M0-M4 identity, organization, port and maritime contracts remain unchanged.

| Work package | Planned deliverable | Current state |
| --- | --- | --- |
| M5-01 | Freeze shipment lifecycle, identifiers, UTC rules, role matrix and HTTP contract | Proposed, approval pending |
| M5-02 | Additive shipment/transition schema, typed scoped CRUD and strict database invariants | Not implemented |
| M5-03 | Exact-version updates, observed SQL lock/revocation races and replay safety | Not implemented |
| M5-04 | Two-tenant browser CRUD/detail/archive/history and full G5 local verification | Not implemented |
| G5 | G5-S01 to G5-S25 reviewed raw evidence and explicit milestone acceptance | NOT RUN, acceptance pending |

## Planning documents

- [M5 architecture](M5_ARCHITECTURE_DESIGN.md): bounded scope, table/field/invariant contracts, permission matrix, API, UI and M6 boundaries.
- [M5 implementation plan](M5_IMPLEMENTATION_PLAN.md): files, delivery sequence, lock-order strategy, migration coordination, tests, operational risk and completion criteria.
- [G5 verification plan](M5_VERIFICATION_PLAN.md): 25 named mandatory scenarios, expected raw outputs and acceptance conditions.
- [Execution graph](EXECUTION_GRAPH.md): authoritative dependency edges remain unchanged; planning does not imply a G5 pass.
- [Local storage policy](LOCAL_STORAGE_POLICY.md): E: for all controllable project and verification output, only attributable short-lived C: temporary writes with safe cleanup.

## Contract choices awaiting approval

The proposed M5-local contract uses a Draft/Booked/InTransit/Arrived/Completed/Cancelled manual workflow, required active same-organization origin/destination ports, nullable UTC planned windows, immutable shipment identity and reference values, a narrowly scoped Auditor read permission, and append-only manual status history. This is not a new global D-number. Publication does **not** approve these choices automatically; see [architecture section 7](M5_ARCHITECTURE_DESIGN.md#7-decisions-to-confirm-before-implementation).

## Verification and integration boundaries

No M5 migrations, implementation source, tests, fixtures, raw results, execution evidence, or acceptance record are claimed by this planning milestone. Do not create `M5_EXECUTED_VERIFICATION.md` until actual G5 verification has been completed.

No GitHub Actions or merges are authorized by these documents. Keep M0-M4 accepted implementation stable and never rewrite its applied migration history. If an independent M5 branch is integrated with other concurrent work, rerun the combined migration/auth/tenant/browser regression matrix at the resulting source SHA. Dedicated accessibility testing remains at M24-04; quarterly existing-volume compatibility remains at M6/M12/M18/M24-03.
