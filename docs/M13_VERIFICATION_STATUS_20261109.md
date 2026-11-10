# M13 Verification Campaign Status

**Source-locked successor checkpoint, 2026-11-10:** [G13/G15 workflow 42727554951](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed 15/15 jobs on `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75` (541 tests passed, 0 failed/skipped). The separate atomic audit found 224 technical witnesses, 3 missing, 7 policy-review and 8 independent-review assertions; G13 and G15 acceptance remain pending. [Current detailed status](G13_G15_VERIFIED_EVIDENCE_20261110.md). Earlier source-specific reports below remain historical.

**Historical execution register:** The original tests, failures and source SHAs below are preserved without retroactive changes. The later 2026-11-10 technical campaign is recorded in the current source-locked evidence page.

**Opened:** 2026-11-09
**Branch:** `plan/m13-operational-kpi-dashboards-20261108`
**Policy:** D-005 Option A, `m13-kpi-v1`
**Feature candidate:** Implemented source, API, migration, dashboard and verification code documented in [implementation register](M13_IMPLEMENTATION_STATUS_20261109.md).
**Execution environment:** GitHub Actions only, restricted to the M13 implementation branch.
**Evidence state:** The original targeted run [42533241124](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42533241124) PASSED; later M13 technical work and independent supplements, plus two green combined workflows, are indexed by [expanded M13 evidence](M13_G13_EXPANDED_VERIFICATION_20261109.md) and [combined integration evidence](M13_M15_COMBINED_VERIFICATION_20261109.md). Full G13 scenario reconciliation and acceptance remain pending.

## Scope

The [independent M13 verification workflow](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/plan/m13-operational-kpi-dashboards-20261108/.github/workflows/verify-m13.yml) has source/build, disposable restricted PostgreSQL and three-engine browser jobs. The required [G13 scenario matrix](M13_VERIFICATION_PLAN.md) also identifies full source/fixture reconciliation, large-tenant performance, negative authorization and mutation evidence, plus independent S25 review.

That feature-branch workflow is a technical campaign, not automatic milestone acceptance. Record actual source SHA and raw failures when runs are available. Do not suppress or reinterpret failures, skip conditions or absent jobs. G13 remains NOT ACCEPTED until a separate explicit decision.

No production database, accepted main branch, M15 notification branch, merge, deployment or M9 backlog restriction is modified by starting verification.

## Initial source-locked run, retained failure

Run [42532384768](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42532384768), source `eb385ed362ab8e110ef28ac185be496acc13f69a`: source job PASS (warnings-as-errors solution build, unit, contract, React build and web tests). Database job FAIL with three M13 restricted-role scenarios failing: plain API-role `FOR SHARE` reads required forbidden table UPDATE privileges. Migration snapshot and accepted-base upgrade checks passed. Browser job FAIL during M5 fixture preparation because `.verification/evidence` was missing, before any Playwright scenarios executed. The raw GitHub logs and TRX artifact are the authoritative failed evidence.

Remediation preserves least privilege by introducing narrowly scoped migration-owned row-lock functions and granting only their execution to the API role. Browser fixture setup now creates its evidence directory. These changes are not evidence of runtime PASS until a later source-locked run confirms them. G13-S02 through S25 remain outstanding as applicable.

## Second source-locked run, retained failure

Run [42532775669](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42532775669), source `1f5bd07ca9dabcb7bdab75d7047ee627204dcf5e`: source job PASS; restricted database suite 5 PASS / 1 FAIL. The M13 runtime, Auditor grant and M11 rule scenarios passed under the restricted API role. The one failure was a populated non-default-schema migration upgrade, where SQL-language `m13_can_manage` prematurely resolved a schema-local role function using the public search path (PostgreSQL 42883). Changing it to PL/pgSQL defers function name binding until the migration installs the schema-specific search path.

The browser job loaded all disposable fixtures. Chromium M13 scenarios 3/3 PASS; Firefox 0/3 PASS at the shared post-login organization workspace assertion, and WebKit NOT EXECUTED because the script stopped on failure. The per-engine runtime restart and readiness gate were added following the precedent of the accepted M11/M12 browser campaign. Do not promote these remediations to PASS until the next frozen run provides successful output.

## Third source-locked campaign: focused PASS

Run [42533241124](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42533241124) checked the exact implementation SHA `28c4a913e2880cdec26508cd0456616801cd40b3`. **All three jobs completed successfully.**

| Job | Verified results | Scope |
| --- | --- | --- |
| source | .NET Release warnings-as-errors build with 0 errors and warnings; **304/304 unit**, **52/52 contract** tests; React production build and web tests | Focused source, domain, API and web checks |
| database | Exclusive disposable PostgreSQL, restricted API login, migration and HTTP checks: **6/6 selected tests PASS** | Targeted M13 runtime, grants/revocation, migration snapshot, historical upgrade |
| browsers | Chromium **3/3**, Firefox **3/3**, WebKit **3/3**, totaling **9/9 M13 browser journeys PASS** | Tested Manager, ungranted Auditor and Administrator paths with the isolated fixture |

The job logs and the source, database and browser artifacts are retained on the linked run. The two historical failed runs above remain historical failures; their results do not change retroactively.

## Expanded execution and remaining acceptance work

The initial focused technical campaign was followed by a 12+2 structural fixture subset, [170,000-shipment scale PASS](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42538812959), [nine source mutations PASS](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42537538212), [two SQL policy mutations PASS](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539385498), [fault recovery PASS](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42537768249), and [latest targeted M13 PASS](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539385476) on their individual frozen SHAs. The [expanded combined technical workflow](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541135825) and [targeted combined workflow](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541135788) passed on `6b05d84bf2f01741b182669c2dd17e39e63d9dda`. Historical failed runs remain recorded above.

The full K01-K14 approved 12+2 risk and occurrence fixture, concurrent authorization and snapshot races, process crash before/after commit, explicit throughput budgets, final branch synchronization, G13-S01 through S24 reconciliation and independent S25 still require completion. No G13 acceptance, merge, deployment or production migration is authorized.
