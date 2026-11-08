# M13 Operational KPI Implementation Status

**Updated:** 2026-11-09
**Branch:** `plan/m13-operational-kpi-dashboards-20261108`
**Accepted baseline:** `eb529368a2447061fc1838cd3967b0ffedae669d`
**Policy:** D-005 Option A approved, `m13-kpi-v1`
**Status:** Implementation source is authored on an isolated branch; no source-locked G13 verification or acceptance is claimed. M13 is not merged or deployed.

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
- M13-01 to M13-03: code authored, verification pending.
- M13-04: verification sources and workflow authored, results pending.
- G13-S01: POLICY APPROVED, RECONCILIATION PENDING.
- G13-S02 to S25: NOT EXECUTED until source-locked evidence is obtained.
- G13: NOT ACCEPTED. No merge, deployment, or production migration authorization.

GitHub Actions is the exclusive verification environment, and failed or skipped jobs must not be described as PASS. See [G13 verification matrix](M13_VERIFICATION_PLAN.md) and [D-005 approval](M13_D005_POLICY_APPROVAL_20261108.md).
