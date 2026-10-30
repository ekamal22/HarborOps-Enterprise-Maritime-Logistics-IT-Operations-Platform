# Project Status

**As of:** 2026-10-30
**Overall:** M0 engineering baseline implemented and runtime verified on the feature branch; acceptance pending
**G0 (last verified code `4fb34eb`):** verified, acceptance pending. The separate .NET 10 and PostgreSQL hardening branch has **no passing runtime evidence yet**. M1 has not started.

## Delivery ledger

This branch follows the granular M0-M24 roadmap on the [planning branch](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/docs/product-delivery-source-of-truth/docs/ROADMAP.md). Legacy v1 milestone labels in older documents are superseded and do not match v2 IDs. M0-01 through M0-05 and G0 keep their original meaning.

| Milestone | State | Evidence |
| --- | --- | --- |
| **M0 Engineering baseline verification** | G0 verified, acceptance pending | [M0 Verification](M0_VERIFICATION.md): code revision `4fb34eb`, 16 .NET tests and 6 web tests passing, clean-volume Compose run, browser path, database outage and recovery, dependency and image scans |
| **M1-M24** | Planned | No implementation; gates G1-G24 are open |

## Hardening changes pending verification

This branch proposes an upgrade from .NET 8/EF Core 8 to .NET 10/EF Core 10, a derived PostgreSQL 16 image running as UID 70 with the upstream `gosu` binary removed, a refreshed test toolchain and a cross-browser suite (Chromium, Firefox, WebKit). A manual screen-reader procedure and an E-drive-only image scan runner have also been added.

**New branch gate: not verified.** Do not carry forward the old G0 test counts to the new code SHA. The evidence and exact required checks are in [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md). The original M0 verification at `4fb34eb` remains historical evidence, not a pass for these changes.

## M0 source inventory

- `HarborOps.sln` with domain, application, infrastructure, API, worker and three test projects
- EF Core PostgreSQL context, baseline migration, deterministic seed and read service
- Public read-only baseline API, liveness endpoint and PostgreSQL readiness endpoint
- Structured JSON logging with correlation identifiers, local CORS policy, controlled 503 responses for database failure
- React and TypeScript status view with synthetic label, loading, error, timeout and refresh states
- Docker Compose, one-shot migrator, Dockerfiles, committed `web/package-lock.json`, scripts
- Manual GitHub workflow `Verify M0` (not run; it can be dispatched only after it exists on the default branch)

## Checks and outcomes

All results are recorded with commands, versions and revisions in [M0 Verification](M0_VERIFICATION.md).

- Clean .NET restore and Release build: 0 warnings, 0 errors.
- Unit 1, contract 10, PostgreSQL integration 5, web 6 tests passed.
- `npm ci` from the lockfile, production build, and Docker web build with `npm ci` only.
- Migration and seed on PostgreSQL 16.15, repeatable, schema inspected.
- Compose clean-volume start, smoke script, real browser load, refresh, rapid refresh, keyboard focus, mobile width.
- Database outage: liveness 200, readiness 503, baseline 503; recovery to 200 and the UI recovers on refresh.
- Ten defects corrected, including baseline HTTP 500 with the database down and missing correlation identifiers in logs.
- npm and NuGet advisories: none open. Trivy image scan: no fixable high or critical findings in the api and web images; upstream `gosu` findings remain in the postgres image.

## Open items before acceptance

1. Explicit acceptance of G0 is not recorded. Status stays acceptance pending.
2. Execute the new .NET 10 migration and regression tests; do not mark the upgraded code verified until a successful run is recorded.
3. Build and rescan the derived non-root PostgreSQL image and verify that the previous `gosu` vulnerabilities are absent from the final filesystem. Verify compatibility with fresh and existing data volumes.

## Mandatory storage boundary

All controlled persistent HarborOps storage stays on E:, including the repository at `E:\\APP IDEAS\\HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform`, project caches and evidence at `E:\\APP IDEAS\\harborops-m0-cache`, and E-backed Docker/WSL data. Follow [Local Storage Policy](LOCAL_STORAGE_POLICY.md). Only unavoidable short-lived Windows temporary use on C: is permitted. No storage cleanup, global migration or extra tool installation is authorized by this requirement.

The one critical and 25 high PostgreSQL `gosu` scan findings remain documented residual risk, not an explicitly approved exception. Revisit their disposition during acceptance and subsequent image updates.

4. Complete and record the three-browser test suite and manual screen-reader/color contrast checks before claiming those coverage gaps closed.

## Documentation status

Specification documents remain authoritative for planned domains. [M0 Verification](M0_VERIFICATION.md) is the single register for M0 evidence.

## Next milestone

M1 (authentication and session lifecycle) requires G0 verified and decision D-001 closed, and is not started.

## Acceptance convention

Planned, in progress, implemented, verified, acceptance pending and accepted are distinct states. Do not infer verification or acceptance from a commit, source inspection or a documentation update.
