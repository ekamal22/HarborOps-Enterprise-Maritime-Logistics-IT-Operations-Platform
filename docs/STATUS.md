# Project Status

**As of:** 2026-10-30
**Overall:** M0 hardening corrections committed; full cold-state regression and acceptance verification pending
**G0:** reopened for verification. A later fresh-database test failed on the historical `4fb34eb` baseline and the initial .NET 10 hardening revision. Corrective code is committed but not yet retested. Acceptance pending. M1 has not started.

## Delivery ledger

This branch follows the granular M0-M24 roadmap on the [planning branch](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/docs/product-delivery-source-of-truth/docs/ROADMAP.md). Legacy v1 milestone labels in older documents are superseded and do not match v2 IDs. M0-01 through M0-05 and G0 keep their original meaning.

| Milestone | State | Evidence |
| --- | --- | --- |
| **M0 Engineering baseline verification** | G0 reopened, acceptance pending | The earlier `4fb34eb` suite passed against an already seeded test database; a later isolated cold-database run reproduced a test failure. Fix committed on the hardening branch, re-verification required. See [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md). |
| **M1-M24** | Planned | No implementation; gates G1-G24 are open |

## Hardening changes pending verification

This branch proposes an upgrade from .NET 8/EF Core 8 to .NET 10/EF Core 10, a derived PostgreSQL 16 image running as UID 70 with the upstream `gosu` binary removed, a refreshed test toolchain and a cross-browser suite (Chromium, Firefox, WebKit). A manual screen-reader procedure and an E-drive-only image scan runner have also been added.

**Corrected branch gate: re-verification pending.** The initial independent hardening audit passed build, database container startup, security scans and 12 browser checks but found a fresh-database integration failure (4/5) and a footer contrast failure. Both have source fixes; the corrected revision must be retested. Do not carry forward old G0 test counts to the new code SHA. The evidence and exact required checks are in [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md). The original `4fb34eb` test result remains historical evidence, but the later cold-database failure prevents relying on it as a complete G0 pass.

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
2. Rerun the previously failing fresh-database integration test and the full .NET 10 suite on the corrected revision.
3. Rebuild source-labeled images, repeat isolated Compose startup and recovery, and rescan the exact running image IDs with the updated scanner.
4. Run clean `npm ci` from the committed browser lockfile and execute the expanded three-browser suite and contrast checks.
5. Complete manual screen-reader and real browser-zoom testing or explicitly record the remaining coverage limitations.

## Mandatory storage boundary

All controlled persistent HarborOps storage stays on E:, including the repository at `E:\\APP IDEAS\\HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform`, project caches and evidence at `E:\\APP IDEAS\\harborops-m0-cache`, and E-backed Docker/WSL data. Follow [Local Storage Policy](LOCAL_STORAGE_POLICY.md). Only unavoidable short-lived Windows temporary use on C: is permitted. No storage cleanup, global migration or extra tool installation is authorized by this requirement.

The one critical and 25 high PostgreSQL `gosu` scan findings remain documented residual risk, not an explicitly approved exception. Revisit their disposition during acceptance and subsequent image updates.

## Documentation status

Specification documents remain authoritative for planned domains. [M0 Verification](M0_VERIFICATION.md) preserves the earlier baseline evidence; [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md) records the hardening audit, corrections, commands and outstanding tests.

## Next milestone

M1 (authentication and session lifecycle) requires G0 verified and decision D-001 closed, and is not started.

## Acceptance convention

Planned, in progress, implemented, verified, acceptance pending and accepted are distinct states. Do not infer verification or acceptance from a commit, source inspection or a documentation update.
