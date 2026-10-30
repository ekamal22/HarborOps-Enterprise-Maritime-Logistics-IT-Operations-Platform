# M0 Engineering Verification Register

**Date:** 2026-10-30  
**Branch:** `feat/m0-engineering-baseline`  
**State:** Implementation in progress; full verification blocked by unavailable runtime tooling  
**Acceptance:** Not yet eligible for acceptance

## Implemented source paths

- `HarborOps.sln` and `src/HarborOps.Domain`, `src/HarborOps.Application`, `src/HarborOps.Infrastructure`, `src/HarborOps.Api`, and `src/HarborOps.Worker`
- EF Core migration `20261030170000_InitialBootstrap` and idempotent `BootstrapSeeder`
- API routes `/api/v1/system/baseline`, `/health/live`, and `/health/ready`
- `web/` React status screen, explicit synthetic source label, loading/error states, and local HTTP origin validation
- `compose.yaml`, Dockerfiles, development environment template, scripts, and manual CI workflow
- Unit, contract, and PostgreSQL integration test projects

The worker is an idle shell, not a tracking processor. The baseline database row is synthetic infrastructure metadata, not a shipment fixture.

## Checks actually executed

| Check | Environment | Command | Outcome | Limit |
| --- | --- | --- | --- | --- |
| Node origin utility tests, isolated source copy | Debian 13, Node.js 22.16.0 | `node --test tests/*.test.mjs` | 3 passed, 0 failed | Source copied into an isolated local review directory, not run against a connected repository checkout |
| GitHub repository assessment | Connected repository | Read `main` commit/tree and all 17 planning Markdown files | 17 baseline files, no prior application code | Repository inventory only |
| GitHub branch source creation | Connected repository | Created feature branch and committed M0 files | GitHub accepted changes | Commits do not demonstrate runtime correctness |

## Required checks still outstanding

| ID | Required command / scenario | Status |
| --- | --- | --- |
| V01 | `dotnet restore HarborOps.sln` | Not run, .NET SDK unavailable |
| V02 | `dotnet build HarborOps.sln -c Release --no-restore` | Not run, .NET SDK unavailable |
| V03 | `dotnet test tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj -c Release` | Not run |
| V04 | `dotnet test tests/HarborOps.ContractTests/HarborOps.ContractTests.csproj -c Release` | Not run |
| V05 | Set disposable `HARBOROPS_TEST_DB`, then `dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release` | Not run; requires PostgreSQL |
| V06 | `cd web && npm install --no-audit --no-fund && npm test && npm run build` | Not run in complete frontend dependency environment |
| V07 | `docker compose config --quiet` | Not run, Docker unavailable |
| V08 | `bash scripts/smoke-compose.sh` after setting local `.env` | Not run, Docker unavailable |
| V09 | Open frontend in a real browser and verify database status, loading, error, refresh | Not run |
| V10 | Isolate the test database after startup; check liveness HTTP 200 and readiness HTTP 503; restore connectivity | Not run |
| V11 | Verify repeatable migration on a clean disposable database and inspect `bootstrap_records` | Not run |
| V12 | Run repository source/metadata, internal-link, and secret hygiene audits | Completed: 72 tracked files inspected, Markdown relative links resolved, no prohibited attribution language or em dashes found; commit messages and identity reviewed. Dynamic secret scanning remains a future security gate. |

Do not interpret any unchecked item as passed.

## Expected behavior and failure checks

1. First Compose startup waits for a healthy PostgreSQL service, applies the migration and seed in the one-shot migrator, starts the API, and serves the web shell.
2. `/health/live` returns minimal process status regardless of database readiness.
3. `/health/ready` returns HTTP 503 for unavailable database or missing bootstrap row.
4. The baseline endpoint uses a real query against PostgreSQL; the UI displays data marked `synthetic`.
5. If the database is inaccessible, the status UI presents a visible error, not a false ready state.
6. Stopping the worker shell does not affect M0 API behavior because event processing is not implemented or enabled.

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
2. Resolve build, package restore, migration, or test failures without skipping checks.
3. Run `bash scripts/smoke-compose.sh` on a clean local Compose volume.
4. Exercise failure and recovery paths; preserve exact HTTP codes and logs without secrets.
5. Verify browser loading/error and refresh states using a real browser.
6. Record commands, environment, failures, evidence paths, and exact commit.
7. Only then move the M0 ledger to verified and `acceptance pending`.

## Known limitations and risks

- No application compile, Compose startup, PostgreSQL migration, browser, or full build evidence exists yet.
- No shipment/tenant, event ingestion, analytics, monitoring, audit, or incident functionality exists.
- The baseline record seeds only one deterministic metadata row. Full synthetic operational fixtures begin later.
- API supports local HTTP only in Compose, with no HTTPS termination or hosted hardening.
- `global.json`, package versions, container image tags, and frontend dependencies need a successful clean restore and security review before M0 verification.
- M0 tests do not yet exercise a real browser or the degraded-dependency path.
- An operational backup/restore rehearsal is planned for M5.

## Status rule

An implemented code path is not a verified runtime behavior. Do not mark M0 accepted unless all required gates pass and acceptance is explicitly confirmed.
