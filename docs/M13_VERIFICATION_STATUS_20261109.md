# M13 Verification Campaign Status

**Opened:** 2026-11-09
**Branch:** `plan/m13-operational-kpi-dashboards-20261108`
**Policy:** D-005 Option A, `m13-kpi-v1`
**Feature candidate:** Implemented source, API, migration, dashboard and verification code documented in [implementation register](M13_IMPLEMENTATION_STATUS_20261109.md).
**Execution environment:** GitHub Actions only, restricted to the M13 implementation branch.
**Evidence state:** Source-locked job outcomes and raw artifacts are required. Do not claim G13 PASS based on this record.

## Scope

The [verification workflow](../.github/workflows/verify-m13.yml) has source/build, disposable restricted PostgreSQL and three-engine browser jobs. The required [G13 scenario matrix](M13_VERIFICATION_PLAN.md) also identifies full source/fixture reconciliation, large-tenant performance, negative authorization and mutation evidence, plus independent S25 review.

The workflow is a technical campaign, not automatic milestone acceptance. Record actual source SHA and raw failures when runs are available. Do not suppress or reinterpret failures, skip conditions or absent jobs. G13 remains NOT ACCEPTED until a separate explicit decision.

No production database, accepted main branch, M15 notification branch, merge, deployment or M9 backlog restriction is modified by starting verification.

## Initial source-locked run, retained failure

Run [42532384768](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42532384768), source `eb385ed362ab8e110ef28ac185be496acc13f69a`: source job PASS (warnings-as-errors solution build, unit, contract, React build and web tests). Database job FAIL with three M13 restricted-role scenarios failing: plain API-role `FOR SHARE` reads required forbidden table UPDATE privileges. Migration snapshot and accepted-base upgrade checks passed. Browser job FAIL during M5 fixture preparation because `.verification/evidence` was missing, before any Playwright scenarios executed. The raw GitHub logs and TRX artifact are the authoritative failed evidence.

Remediation preserves least privilege by introducing narrowly scoped migration-owned row-lock functions and granting only their execution to the API role. Browser fixture setup now creates its evidence directory. These changes are not evidence of runtime PASS until a later source-locked run confirms them. G13-S02 through S25 remain outstanding as applicable.
