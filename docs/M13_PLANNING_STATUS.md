# M13 Operational KPI Planning and Delivery Status

**Current G13/G15 checkpoint (2026-11-09):** [Run 42569970541](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42569970541) **PASSED 15/15 jobs** at `e4bf9cd1527f85f554baf623e90805d96d08ac1a`: 487 successful TRX cases, six successful three-engine browser suites, 23 effective mutations killed, four process-kill probes, and technical evidence integrity PASS. This does **not** close the scenario ledger: each gate has 1 PASS, 22 BLOCKED and 2 NOT EXECUTED. The [evidence review](G13_G15_SOURCE_LOCKED_EVIDENCE_REVIEW_20261109.md) documents remaining source-locked acceptance gaps and the outstanding independent S25 decisions. No merge or deployment.
**Earlier source-locked status (2026-11-09):** [Combined expanded M13/M15 run 42541135825](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541135825) and [targeted run 42541135788](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541135788) PASS at `6b05d84bf2f01741b182669c2dd17e39e63d9dda` (312 unit, 52 contract, 37 PostgreSQL and 27 browser). The newer independent M15 [42545005498](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42545005498) passed source/DB, Chromium 7/7 and Firefox 7/7, but failed WebKit 1/7 because the expected live notice was absent. This latest M15 SHA and the independent M13 report-member SELECT RLS are not included in the green combined SHA. G13/G15 acceptance pending; no merge/deployment. [Combined evidence](M13_M15_COMBINED_VERIFICATION_20261109.md).

**Updated:** 2026-11-09
**Branch:** `plan/m13-operational-kpi-dashboards-20261108`
**Inbound accepted main:** `eb529368a2447061fc1838cd3967b0ffedae669d`
**D-005:** APPROVED, Option A, `m13-kpi-v1`.
**M13:** Source implemented; focused M13, 170,000 shipment, fault and mutation technical checks plus combined integration jobs PASS on explicitly documented SHAs ([independent evidence](M13_G13_EXPANDED_VERIFICATION_20261109.md); [combined evidence](M13_M15_COMBINED_VERIFICATION_20261109.md)). Full G13 acceptance remains pending. Nothing is merged or deployed.

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

The 10-minute report expiry, 500,000 member-row ceiling and timeouts remain provisional. A 170,000-shipment technical run, nine source mutations, two SQL-policy mutations and focused fault tests passed on their source SHAs. Complete risk/occurrence fixture reconciliation, measured concurrent budgets, crash/revocation races, final source integration and S25 independent review remain outstanding. The implementation does not turn an unassessed M11 rule, absent M10 projection, or parked ARRI/DEPA event into a verified no-risk or physical arrival fact.

M15 uses an independent implementation branch, notification module and earlier timestamped migration `20261109010000_M15ScopedNotifications`. M13 owns only `20261109020000_M13OperationalKpis` and the analytics module. Shared migration, role, Program and UI changes still require combined integration review. Accepted main and the M9 transport hold remain unchanged.

GitHub Actions is the exclusive permitted M13 verification environment after implementation is complete. No local verification substitute is authorized. A G13 acceptance decision, merge or deployment requires separate authorization.
