# M0 Engineering Verification Register

**Date:** 2026-10-30  
**Branch:** `feat/m0-engineering-baseline`  
**State:** Source implemented with additional safety and failure-path checks; full runtime verification still blocked by unavailable tooling  
**Acceptance:** Not yet eligible for acceptance

## Implemented source paths

- `HarborOps.sln` and `src/HarborOps.Domain`, `src/HarborOps.Application`, `src/HarborOps.Infrastructure`, `src/HarborOps.Api`, and `src/HarborOps.Worker`
- EF Core migration `20261030170000_InitialBootstrap` and idempotent `BootstrapSeeder`
- API routes `/api/v1/system/baseline`, `/health/live`, and `/health/ready`
- `web/` React status screen, explicit synthetic source label, loading/error states, and local HTTP origin validation
- `compose.yaml`, Dockerfiles, development environment template, scripts, and manual CI workflow
- Unit, contract, and PostgreSQL integration test projects

The worker is an idle shell, not a tracking processor. The baseline database row is synthetic infrastructure metadata, not a shipment fixture. The v2 planning [execution graph](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/docs/product-delivery-source-of-truth/docs/EXECUTION_GRAPH.md) preserves the M0-01 through M0-05 IDs and G0 gate; M1-M24 remain planned.

## Checks actually executed

| Check | Environment | Command | Outcome | Limit |
| --- | --- | --- | --- | --- |
| Node origin utility tests, isolated source copy | Debian 13, Node.js 22.16.0 | `node --test tests/*.test.mjs` | 3 passed, 0 failed | Source copied into an isolated local review directory, not run against a connected repository checkout |
| GitHub repository assessment | Connected repository | Read `main` commit/tree and all 17 planning Markdown files | 17 baseline files, no prior application code | Repository inventory only |
| GitHub branch source creation | Connected repository | Created feature branch and committed M0 files | GitHub accepted changes | Commits do not demonstrate runtime correctness |
| M0 Node origin and baseline-payload utility tests, isolated source copy | Debian 13, Node.js 22.16.0 | `node --test tests/*.test.mjs` | **6 passed, 0 failed** | Both current test files and their two utility modules were reproduced in an isolated local review directory; not a full checked-out frontend, dependency installation, TypeScript build, or browser test |

## Required checks still outstanding

| ID | Required command / scenario | Status |
| --- | --- | --- |
| V01 | `dotnet restore HarborOps.sln` | Not run, .NET SDK unavailable |
| V02 | `dotnet build HarborOps.sln -c Release --no-restore` | Not run, .NET SDK unavailable |
| V03 | `dotnet test tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj -c Release` | Not run |
| V04 | `dotnet test tests/HarborOps.ContractTests/HarborOps.ContractTests.csproj -c Release` | Not run |
| V05 | Set disposable `HARBOROPS_TEST_DB`, then `dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release` | Not run; requires PostgreSQL |
| V06 | `cd web && npm install --no-audit --no-fund && npm test && npm run build` | Utility tests completed only in isolated source copy; full npm dependency installation and frontend build not run |
| V07 | `docker compose config --quiet` | Not run, Docker unavailable |
| V08 | `bash scripts/smoke-compose.sh` after setting local `.env` | Not run, Docker unavailable |
| V09 | Open frontend in a real browser and verify database status, loading, error, refresh | Not run |
| V10 | Run `bash scripts/check-m0-degraded.sh --confirm-local-outage` on disposable local Compose; check liveness 200, readiness 503, baseline 503, then recovery to 200 | Not run; safe manual fault script committed, real Docker/PostgreSQL unavailable |
| V11 | Verify repeatable migration on a clean disposable database and inspect `bootstrap_records` | Not run |
| V12 | Run repository source/metadata, internal-link, and secret hygiene audits | Baseline static audit previously passed at 72 tracked files; new M0 changes require a repeat comprehensive check before G0. Dynamic secret scanning and clean dependency restore remain pending. |

Do not interpret any unchecked item as passed.

## M0 v2 work-package ledger

| ID | State | Recorded evidence or required follow-up |
| --- | --- | --- |
| M0-01 Assess existing M0 branch | Source review completed, gate open | Inspected core API, persistence, Compose, UI, test, and script files. Identified and addressed unsafe integration DB targeting, limited seed assertions, stale refresh responses, malformed baseline rendering, and missing controlled degradation exercise. |
| M0-02 Build and test foundations | Partial | Six frontend utility tests passed in an isolated source copy. No .NET SDK, npm dependency install, production web build, or real browser verification available. |
| M0-03 Verify migration and seed | Not verified | Integration test now restricts database name to `harborops_integration` or `harborops_test` on loopback and asserts deterministic timestamp; actual migration and data checks not executed. |
| M0-04 Verify local stack and browser | Not verified | Compose and HTTP smoke scripts exist; clean Compose startup and browser-to-database path not executed. |
| M0-05 Verify failure and hygiene | Not verified | Explicit opt-in outage script verifies baseline degradation and restoration once run; it has not been executed. Full current-revision hygiene and package audit remain pending. |
| G0 Engineering baseline gate | Open | M0-02 through M0-05 are not fully evidenced; acceptance pending only after required runtime checks are completed. |

## Implementation corrections added in this review

- PostgreSQL integration tests fail closed when `HARBOROPS_TEST_DB` targets a non-loopback host or an unexpected database name, preventing accidental migration of ordinary project data.
- Persistence tests assert stable synthetic timestamp and repeated migration/seed behavior.
- Frontend baseline payloads are validated for required fields, source label, dataset version and UTC timestamp before rendering.
- Frontend refreshes cancel superseded requests, preventing stale responses from overwriting newer states; error alerts use an accessible alert role.
- `scripts/check-m0-degraded.sh` requires the explicit `--confirm-local-outage` flag, validates the local Compose project, checks healthy HTTP states, stops PostgreSQL, verifies degraded responses, restores service with an EXIT trap, and checks recovery.
- The idle worker's milestone message now points to M9 rather than the superseded v1 M2.

## Expected behavior and failure checks

1. First Compose startup waits for a healthy PostgreSQL service, applies the migration and seed in the one-shot migrator, starts the API, and serves the web shell.
2. `/health/live` returns minimal process status regardless of database readiness.
3. `/health/ready` returns HTTP 503 for unavailable database or missing bootstrap row.
4. The baseline endpoint uses a real query against PostgreSQL; the UI displays data marked `synthetic`.
5. If the database is inaccessible, the status UI presents a visible error, not a false ready state.
6. The idle worker shell does not affect M0 API behavior because event processing is not implemented or enabled in Compose.

These are expected outcomes derived from source code, not observed results.

## Test environment required

- .NET 8 SDK capable of building the pinned projects and NuGet package references
- PostgreSQL 16 with a disposable integration-test database
- Docker Engine with Compose to validate complete container startup
- Node.js 22 and npm for frontend installation, tests, and build
- A browser for actual user-interface observation

No paid cloud accounts or feeds are necessary. `.env` is local and untracked. GitHub workflow `Verify M0` is configured for manual triggering only, but a workflow-dispatch file must exist on the repository default branch before GitHub permits the usual manual run. Until then, use the local scripts or an explicitly authorized external runner. Review CI quota before any later workflow run.

## Next verification sequence

1. Execute `bash scripts/verify-local.sh` with `HARBOROPS_TEST_DB` set to a disposable PostgreSQL database, .NET, Node.js, npm, Docker and a valid local `.env` available. The script now fails explicitly if a required tool or integration database setting is missing; it does not silently skip mandatory checks.
2. Resolve build, package restore, migration, or test failures without skipping checks. Use a dedicated loopback PostgreSQL database named `harborops_integration` or `harborops_test`.
3. Run `bash scripts/smoke-compose.sh` on a clean local Compose volume.
4. On a disposable local Compose stack, deliberately run `bash scripts/check-m0-degraded.sh --confirm-local-outage`. This temporarily stops PostgreSQL and restores it. Never run it against a shared or production stack. Preserve exact HTTP codes and redacted logs.
5. Verify browser loading/error and rapid repeated refresh states using a real browser. The Node utility test results alone do not verify UI rendering.
6. Record commands, environment, failures, evidence paths, and exact commit.
7. Only then move the M0 ledger to verified and `acceptance pending`.

## Known limitations and risks

- No application compile, Compose startup, PostgreSQL migration, browser, or full frontend production build evidence exists yet.
- No shipment/tenant, event ingestion, analytics, monitoring, audit, or incident functionality exists.
- The baseline record seeds only one deterministic metadata row. Full synthetic operational fixtures begin later.
- API supports local HTTP only in Compose, with no HTTPS termination or hosted hardening.
- `global.json`, package versions, container image tags, and frontend dependencies need a successful clean restore and security review before M0 verification. There is no committed `web/package-lock.json`, so frontend transitive dependencies are not yet locked; M0 must not be marked reproducible until a real npm install produces and verifies a lockfile and build. Use `npm ci` for subsequent clean verification.
- .NET 8 support ends on 2026-11-10; schedule a deliberate .NET 10 migration with dependency and compatibility tests rather than treating the existing framework as a long-lived supported base.
- M0 tests do not yet exercise a real browser or the degraded-dependency path. A script and guarded controls exist, but have not been run.
- The full operational backup/restore rehearsal is planned for M19 under the v2 execution graph.

## Status rule

An implemented code path is not a verified runtime behavior. Do not mark M0 accepted unless all required gates pass and acceptance is explicitly confirmed.
