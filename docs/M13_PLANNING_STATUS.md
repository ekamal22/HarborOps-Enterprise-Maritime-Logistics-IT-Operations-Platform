# M13 Operational KPI Planning and Delivery Status

**M13/G13 current verification (2026-11-12):** The approved G13-S20 Option A contract is 250,000 KPI members per report, four live reports per tenant, two per user and 1,000,000 active member rows per tenant. [Current combined run 43122895445](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43122895445) on `c6d2dc86e30913b8d67a011131f9980422e4c197` remains in progress; preflight, G13 rollback/concurrency, browser, crash and security groups had passed while G13 mutations, resource scale and database regressions were still pending at the recorded checkpoint. [Run 43120072016](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43120072016) failed on G15's SKIP LOCKED mutation timeout despite 13 passing technical jobs. The current source includes a bounded fixture correction, not yet proven. [Run 43093329982 attempt 2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43093329982/attempts/2) failed exact assertion reconciliation after all technical jobs passed. Historical c3/round 3 G13 S25 review was NOT PASS. **G13 and G15 acceptance pending**, with no main merge, deployment or Turkish integration. The post-c3 workflow is the sole authorized disposable Actions scope. See [run register](G13_G15_POST_C3_HOSTED_RUN_REGISTER_20261111.md) and [current documentation index](REPOWIDE_DOCUMENTATION_RECONCILIATION_20261112.md). Earlier data is historical and bound to its recorded SHA.

**2026-11-10 final G13/G15 review:** The [final source-locked run](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415) PASSED 15/15 jobs, 543 tests and 227/227 technical assertion witnesses at `15cabd8c542159753c927c2bd65b4346223e84f6`. The separate [formal gate decision](G13_G15_FORMAL_GATE_DECISIONS_20261110.md) is G13 NOT ACCEPTED and G15 NOT ACCEPTED following [S25 reviews](G13_G15_REVIEW_MATRIX_20261110.md). G13 permanent tenant capacity/idle cleanup enforcement and exact G13/G15 mutation/HTTP-outage proof remain pending. P10 conformance is supported; P12 deletion remains disabled. All earlier dates and SHAs in this file describe historical checkpoints only. No merge or production authorization.
**Source-locked successor checkpoint, 2026-11-10:** [G13/G15 workflow 42727554951](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed 15/15 jobs on `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75` (541 tests passed, 0 failed/skipped). The separate atomic audit found 224 technical witnesses, 3 missing, 7 policy-review and 8 independent-review assertions; G13 and G15 acceptance remain pending. [Current detailed status](G13_G15_VERIFIED_EVIDENCE_20261110.md). Earlier source-specific reports below remain historical.

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
| M13-04 verification | Targeted source/build, unit, contract, database and browser checks PASS in run 42533241124; full G13 technical campaign passed in 2026-11-10 successor, but independent acceptance remains pending. |
| G13-S01 | POLICY APPROVED, SOURCE/FIXTURE AND MEASURED LIMIT RECONCILIATION PENDING |
| G13-S02 through S25 | PARTIAL SOURCE-LOCKED EVIDENCE, FULL EXIT CRITERIA NOT YET MET |
| G13 | NOT ACCEPTED |

The 10-minute report expiry, 500,000 member-row ceiling and timeouts remain provisional. A 170,000-shipment technical run, nine source mutations, two SQL-policy mutations and focused fault tests passed on their source SHAs. Complete risk/occurrence fixture reconciliation, measured concurrent budgets, crash/revocation races, final source integration and S25 independent review remain outstanding. The implementation does not turn an unassessed M11 rule, absent M10 projection, or parked ARRI/DEPA event into a verified no-risk or physical arrival fact.

M15 uses an independent implementation branch, notification module and earlier timestamped migration `20261109010000_M15ScopedNotifications`. M13 owns only `20261109020000_M13OperationalKpis` and the analytics module. Shared migration, role, Program and UI changes still require combined integration review. Accepted main and the M9 transport hold remain unchanged.

GitHub Actions is the exclusive permitted M13 verification environment after implementation is complete. No local verification substitute is authorized. A G13 acceptance decision, merge or deployment requires separate authorization.
