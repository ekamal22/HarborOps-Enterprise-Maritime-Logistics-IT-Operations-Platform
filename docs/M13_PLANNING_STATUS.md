# M13 Operational KPI Planning and Delivery Status

**Updated:** 2026-11-09
**Branch:** `plan/m13-operational-kpi-dashboards-20261108`
**Inbound accepted main:** `eb529368a2447061fc1838cd3967b0ffedae669d`
**D-005:** APPROVED, Option A, `m13-kpi-v1`.
**M13:** Source implementation authored and focused GitHub Actions jobs PASSED at `28c4a913e2880cdec26508cd0456616801cd40b3` ([evidence](M13_VERIFICATION_STATUS_20261109.md)). Full G13 acceptance remains pending. Nothing is merged or deployed.

## Current artifacts

- [Policy decision](M13_D005_POLICY_APPROVAL_20261108.md) and [source KPI catalog](M13_D005_KPI_DECISION_PROPOSAL.md)
- [Architecture](M13_ARCHITECTURE_DESIGN.md) and [implementation plan](M13_IMPLEMENTATION_PLAN.md)
- [Actual implementation register](M13_IMPLEMENTATION_STATUS_20261109.md)
- [G13-S01 through S25 verification matrix](M13_VERIFICATION_PLAN.md)

## Delivery status

| Package | Current disposition |
| --- | --- |
| M13-01 definitions | D-005 approved, source policy implemented. Full fixture ledger and measured bounds not yet verified. |
| M13-02 reporting backend | Source and migration implemented; targeted restricted PostgreSQL tests PASS. |
| M13-03 dashboard | Source implemented; three browser engines 9/9 PASS on target journeys. |
| M13-04 verification | Targeted source/build, unit, contract, database and browser checks PASS in run 42533241124; full G13 evidence still pending. |
| G13-S01 | POLICY APPROVED, SOURCE/FIXTURE AND MEASURED LIMIT RECONCILIATION PENDING |
| G13-S02 through S25 | PARTIAL SOURCE-LOCKED EVIDENCE, FULL EXIT CRITERIA NOT YET MET |
| G13 | NOT ACCEPTED |

The 10-minute report expiry, 500,000 row materialization ceiling and timeouts are provisional. Large tenant sizing, fault, mutation, fixture and independent evidence checks are outstanding. The implementation does not turn an unassessed M11 rule, absent M10 projection, or parked ARRI/DEPA event into a verified no-risk or physical arrival fact.

M15 uses an independent implementation branch, notification module and earlier timestamped migration `20261109010000_M15ScopedNotifications`. M13 owns only `20261109020000_M13OperationalKpis` and the analytics module. Shared migration, role, Program and UI changes still require combined integration review. Accepted main and the M9 transport hold remain unchanged.

GitHub Actions is the exclusive permitted M13 verification environment after implementation is complete. No local verification substitute is authorized. A G13 acceptance decision, merge or deployment requires separate authorization.
