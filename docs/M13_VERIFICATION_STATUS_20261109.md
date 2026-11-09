# M13 Verification Campaign Status

**Current source status (2026-11-09):** M13 code and verification entrypoints are implemented. Independent [targeted technical checks](M13_VERIFICATION_STATUS_20261109.md), [170,000 shipment / mutation / fault evidence](M13_G13_EXPANDED_VERIFICATION_20261109.md), and [combined source-locked workflow 42541135825](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541135825) passed on their respective source SHAs. The green combined source is older than the independent M13 member SELECT RLS optimization. Full G13-S01 through S24 evidence, final source synchronization and independent S25 remain open. No acceptance, merge or deployment.

**Opened:** 2026-11-09
**Branch:** `plan/m13-operational-kpi-dashboards-20261108`
**Policy:** D-005 Option A, `m13-kpi-v1`
**Feature candidate:** Implemented source, API, migration, dashboard and verification code documented in [implementation register](M13_IMPLEMENTATION_STATUS_20261109.md).
**Execution environment:** GitHub Actions only, restricted to the M13 implementation branch.
**Evidence state:** Initial and expanded technical verification, including combined full browser/DB PASS, are recorded at their frozen SHAs. M13 targeted source/real-role database/three-engine browser PASS on [42539385476](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539385476) at `8175a28ac188b98f5b4cb3edaa7106b5d38ccb84`, with separate reference-fixture subset, 170k-scale, SQL fault, code mutation and SQL policy mutation PASS at the explicitly recorded SHAs. Expanded combined verification is still failing and G13 acceptance remains pending. [Expanded run ledger](M13_G13_EXPANDED_VERIFICATION_20261109.md).

## Scope

The [verification workflow](../.github/workflows/verify-m13.yml) has source/build, disposable restricted PostgreSQL and three-engine browser jobs. The required [G13 scenario matrix](M13_VERIFICATION_PLAN.md) also identifies full source/fixture reconciliation, large-tenant performance, negative authorization and mutation evidence, plus independent S25 review.

The workflow is a technical campaign, not automatic milestone acceptance. Record actual source SHA and raw failures when runs are available. Do not suppress or reinterpret failures, skip conditions or absent jobs. G13 remains NOT ACCEPTED until a separate explicit decision.

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

## Expanded execution after the focused campaign

The focused PASS above was followed by completed 12+2 structural fixture subset, 170,000-shipment bounded-report performance, nine source mutations, two SQL security policy mutations, and focused SQL timeout/cancellation recovery. Run identifiers, performance samples, failed attempts and exact source boundaries are preserved in [G13 expanded evidence](M13_G13_EXPANDED_VERIFICATION_20261109.md). The latest M13 targeted run [42539385476](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539385476) passed on `8175a28ac188b98f5b4cb3edaa7106b5d38ccb84`.

The expanded combined M13/M15 workflow [42539272790](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539272790) **FAILED**, even though the narrower combined workflow [42539272811](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42539272811) passed. This is not a contradiction: the expanded campaign exercises 17-migration historical assertions and additional M15 notification browser scenarios. Combined remediation on the integration branch remains subject to a subsequent green source-locked run.

## Remaining acceptance work

The structural 12+2 subset is not the approved complete rule/occurrence ledger. G13 also needs full concurrent and skewed performance review with approved thresholds, process-crash and after-commit recovery, comprehensive multi-tenant and session-revocation concurrency, source-locked M13/M15 integration against both latest feature branches, complete S01-S24 evidence reconciliation, and independent G13-S25. Treat unresolved cases as outstanding or NOT EXECUTED, never PASS by analogy.

**Disposition:** No G13 acceptance, merge, deployment, production migration or operational-policy change is authorized.
