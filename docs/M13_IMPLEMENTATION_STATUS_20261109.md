# M13 Operational KPI Implementation Status

**Current follow-up (2026-11-11):** G13-S25 round 3 returned NOT PASS against the c3 source `4e7b9e2`. G13-S20 Option A is approved at a new 250,000-member single-report ceiling. The post-c3 implementation and exact-clause tests are on `feature/g13-post-c3-remediation-20261111` and have not completed a new source-locked verification campaign. The previous c3 PASS cannot be applied to that source. No G15-S25 round 3 decision is recorded here; both G13 and G15 acceptance remain pending. See [current remediation](G13_POST_C3_REMEDIATION_20261111.md) and [approved scope](G13_S20_SCOPE_DECISION_20261111.md). Earlier evidence below is historical and remains specific to its recorded commit. No merges, GitHub Actions, production activation or notification deletion are authorized.

**Final G13/G15 status, 2026-11-10:** [Source-locked run 42745334415](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415) PASSED all 15 jobs at `15cabd8c542159753c927c2bd65b4346223e84f6`, with 543 passed TRX cases and 227/227 technical witnesses. The separate [policy decisions](G13_G15_POLICY_DECISIONS_20261110.md), [S25 reviews](G13_G15_REVIEW_MATRIX_20261110.md) and [formal decisions](G13_G15_FORMAL_GATE_DECISIONS_20261110.md) record **G13 NOT ACCEPTED; G15 NOT ACCEPTED**. G13 tenant storage quota/idle cleanup and exact negative mutation/HTTP-outage requirements remain. P10 is technically fulfilled; P12 deletion remains prohibited. Earlier numbers in this page are historical checkpoints, not the newest results. No main merge, deploy or activation.
**Historical 2026-11-10 predecessor source checkpoint:** [G13/G15 workflow 42727554951](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed 15/15 jobs on `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75` (541 tests passed, 0 failed/skipped). The separate atomic audit found 224 technical witnesses, 3 missing, 7 policy-review and 8 independent-review assertions; G13 and G15 acceptance remain pending. [Current detailed status](G13_G15_VERIFIED_EVIDENCE_20261110.md). Earlier source-specific reports below remain historical.

**Updated:** 2026-11-09
**Branch:** `plan/m13-operational-kpi-dashboards-20261108`
**Accepted baseline:** `eb529368a2447061fc1838cd3967b0ffedae669d`
**Policy:** D-005 Option A approved, `m13-kpi-v1`
**Status:** M13 source implemented. Last independent source-locked M13 target PASS in [42539385476](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539385476), plus independent 170,000-shipment, 9 source mutation, 2 SQL policy mutation, and fault recovery PASS on their recorded SHAs. A separate combined technical PASS exists at `6b05d84bf2f01741b182669c2dd17e39e63d9dda` but does not contain the latest report-member SELECT RLS optimization from M13. [Expanded evidence](M13_G13_EXPANDED_VERIFICATION_20261109.md). G13 acceptance pending; not merged or deployed.

## Implemented source inventory

| Area | Implementation |
| --- | --- |
| KPI catalog and UTC filters | `src/HarborOps.Domain/Reporting/OperationalKpiPolicy.cs` |
| REPEATABLE READ tenant reports, scoped membership, freshness and keyset drill | `src/HarborOps.Infrastructure/Reporting/OperationalKpiStore.cs` |
| Additive reporting schema, RLS, signed access, expiry pruning and Auditor grant procedure | `src/HarborOps.Infrastructure/Persistence/Migrations/20261109020000_M13OperationalKpis.cs` |
| Conditional restricted-role SQL grants | `src/HarborOps.Api/Audit/AuditRoleProvisioner.cs` |
| API catalog, overview, report members, Auditor grants and rate limits | `src/HarborOps.Api/Reporting/OperationalKpiEndpoints.cs` |
| Selected organization and business permission | `src/HarborOps.Domain/Organizations/OrganizationPolicy.cs` and `web/src/auth/OrganizationWorkspace.tsx` |
| Dashboard, UTC filters, risk/quality cards, trends, drill and grant controls | `web/src/analytics/` |
| Unit and contract tests | `tests/HarborOps.UnitTests/OperationalKpiPolicyTests.cs` and `tests/HarborOps.ContractTests/OperationalKpiContractTests.cs` |
| Restricted-role and source-fidelity integration tests | `tests/HarborOps.IntegrationTests/M13OperationalKpiRuntimeTests.cs` |
| Chromium, Firefox and WebKit spec | `tests/browser/specs/operational-kpis.spec.mjs` |
| GitHub Actions verification entrypoint | `.github/workflows/verify-m13.yml` |

## Behavioral contract

Fourteen KPIs K01-K14 use approved M5/M6/M10/M11 evidence. K05 and K06 measure first recorded workflow arrival with distinct strict and 60-minute tolerance. They do not claim a physical delivery event or future ETA. K07-K10 require fresh rule/source checkpoints, K11 counts current exception occurrences, K12 counts historical first openings, and K13/K14 disclose unassessed, stale or missing evidence. UTC windows are half-open with default previous seven complete UTC days and a maximum of 366 days.

Each dashboard overview uses one tenant-scoped REPEATABLE READ materialization with a protected actor/session/org/filter/version-bound report token. Drill members are read from that stored report and live access is revalidated per request. Current source detail may differ from report-time state. Auditor reporting requires an active explicit read-only grant bound to the current membership version; Administrator manages grants but cannot read KPI data by default. Support has no reporting access.

## Provisional limits

The source currently targets ten-minute token expiry, a 500,000 member-row fail-closed report ceiling, 15-second SQL statements and a 25-second report-build budget. **These are provisional implementation parameters, not demonstrated production limits.** Report builds exceeding the ceiling must roll back rather than return false partial totals. The reference twelve-shipment plus two-tenant fixture, 170,000-shipment workload, retention, multi-instance latency and security mutation evidence remain subject to G13 verification.

## Parallel and operational boundaries

M13 uses migration `20261109020000_M13OperationalKpis`; the independent M15 branch uses `20261109010000_M15ScopedNotifications`. M13 does not touch the M15 notification module. Combined EF migration ordering, API registration, React workspace and regression checks must be resolved before either is integrated into main.

G11/G12/G16 accepted status, the parked M9 ARRI/DEPA transport hold, 10,000 unresolved-work cap, M10 projector default-off, null ETA and M11 evaluator opt-in remain unchanged.

## G13 and release status

- D-005: APPROVED with safeguards.
- M13-01 through M13-03: application implementation authored; targeted checks PASSED on the frozen source.
- M13-04: combined source-locked implementation verification PASSED at the 2026-11-10 SHA. Full G13 acceptance remains incomplete because three atomic witnesses and independent G13-S25 review are not closed.
- G13-S01: D-005 policy approved, technical source/fixture verification passed in the combined campaign, but permanent storage and cleanup numerical policies and full scenario reconciliation remain pending.
- G13-S02 to S25: narrower tests have PASS evidence, but no scenario is declared fully PASS without its complete exit criteria.
- G13: NOT ACCEPTED. No merge, deployment, or production migration authorization.

GitHub Actions is the exclusive verification environment, and failed or skipped jobs must not be described as PASS. See [G13 verification matrix](M13_VERIFICATION_PLAN.md) and [D-005 approval](M13_D005_POLICY_APPROVAL_20261108.md).
