# Project Status

**As of:** 2026-10-30
**Overall:** M0 hardening verified in GitHub Actions at `fec7429415fede121db8a4a34f0df72cd4fac609`, acceptance pending
**G0:** **verified** at corrected source revision `fec7429415fede121db8a4a34f0df72cd4fac609` by [GitHub Actions run #2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41134743879); **acceptance pending**. M1 has not started.

## Delivery ledger

This branch follows the granular M0-M24 roadmap on the [planning branch](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/docs/product-delivery-source-of-truth/docs/ROADMAP.md). Legacy v1 milestone labels in older documents are superseded and do not match v2 IDs. M0-01 through M0-05 and G0 keep their original meaning.

| Milestone | State | Evidence |
| --- | --- | --- |
| **M0 Engineering baseline verification** | G0 verified, acceptance pending | Current GitHub Actions run #2 passed 1 unit, 10 contract, 5 fresh + 5 repeat integration, 6 web and 12 functional browser tests, disposable Compose outage/recovery and three image scans. See [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md). |
| **M1-M24** | Planned | No implementation; gates G1-G24 are open |

## Hardening changes pending verification

This branch proposes an upgrade from .NET 8/EF Core 8 to .NET 10/EF Core 10, a derived PostgreSQL 16 image running as UID 70 with the upstream `gosu` binary removed, a refreshed test toolchain and a cross-browser suite (Chromium, Firefox, WebKit). An E-drive-only image scan runner and functional cross-browser checks have also been added. All accessibility tests are reserved for final M24-04.

**Corrected branch gate: verified, acceptance pending.** The earlier audit exposed a cold-database test defect and an unrelated accessibility contrast observation. The corrected fresh-database integration suite passed in GitHub Actions run #2 at `fec7429415fede121db8a4a34f0df72cd4fac609`; the entire functional browser suite, Compose recovery and final image scans also passed. Accessibility testing remains deferred to M24-04. The older `4fb34eb` result remains historical evidence only.

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

## Historical-volume checkpoint policy

No existing local Docker volume is required for G0. New-code verification uses a disposable fresh database; historical-volume compatibility is deferred to quarter-mile checkpoints M6, M12, M18 and M24-03, and comprehensive backup/restore remains M19. If there is no representative historical snapshot, report NOT APPLICABLE. Never alter the original retained data volume. Breaking database or storage format changes affecting preserved data must be rehearsal-tested on a safe copy before rollout.

## Open items before acceptance

1. Explicit G0 acceptance is still pending. The verified engineering foundation is not a production deployment.
2. Resolve decision D-001 before implementing M1 authentication and session lifecycle.
3. Do not merge draft PRs or begin M1 on the basis of automated verification alone.
4. Existing-volume compatibility checkpoints remain M6/M12/M18/M24-03; full migration and recovery are M19; accessibility verification is final M24-04.

## Mandatory storage boundary

All controlled persistent HarborOps storage stays on E:, including the repository at `E:\\APP IDEAS\\HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform`, project caches and evidence at `E:\\APP IDEAS\\harborops-m0-cache`, and E-backed Docker/WSL data. Follow [Local Storage Policy](LOCAL_STORAGE_POLICY.md). Only unavoidable short-lived Windows temporary use on C: is permitted. No storage cleanup, global migration or extra tool installation is authorized by this requirement.

The one critical and 25 high PostgreSQL `gosu` scan findings remain documented residual risk, not an explicitly approved exception. Revisit their disposition during acceptance and subsequent image updates.

## Documentation status

Specification documents remain authoritative for planned domains. [M0 Verification](M0_VERIFICATION.md) preserves the earlier baseline evidence; [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md) records the hardening audit, corrections, commands and outstanding tests.

## Next milestone

M1 (authentication and session lifecycle) requires G0 verified and decision D-001 closed, and is not started.

## Acceptance convention

Planned, in progress, implemented, verified, acceptance pending and accepted are distinct states. Do not infer verification or acceptance from a commit, source inspection or a documentation update.
