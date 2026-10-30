# M0 Engineering Verification Register

**Date of execution:** 2026-10-30
**Branch:** `feat/m0-engineering-baseline`
**Starting revision:** `16523bc1f865f19ef8a86bf1b6604c8c84207003`
**Final code revision verified:** `4fb34eb` (the commit that records this register follows it and changes documentation only)
**Historical verification:** The older `4fb34eb` result was reopened after the cold-database defect. The newer hardening implementation passed CI and G0 was accepted on 2026-10-31. All old test results below remain historical.
**Planning source:** [Roadmap](ROADMAP.md), [Acceptance Criteria](ACCEPTANCE_CRITERIA.md) and [execution-graph.json](execution-graph.json), all integrated into `main` alongside the accepted M0 code.

This register separates three states: source implemented, runtime verified, and accepted. Everything marked verified below was executed on the machine described in the next section. Raw logs were kept outside the repository because they contain local paths.

## Subsequent fresh-database finding

A later independent audit reproduced an integration-test failure on an empty `harborops_integration` database against both the upgraded code and the previously recorded .NET 8 baseline. The test reused a `DbContext` after raw SQL deleted a tracked row, so its second seed raised a duplicate tracked-key exception. Earlier successful runs on a pre-seeded database did not establish fresh-state repeatability. Consequently the historical `4fb34eb` evidence did not alone establish a full clean-database pass. The correction was retested successfully in the later hardening CI run. This register preserves the exact historical observations without treating them as sufficient acceptance evidence.

## Post-baseline hardening revision

The G0 evidence below applies specifically to **code revision `4fb34eb`**, not to the revised .NET 10, rootless PostgreSQL and expanded browser-testing implementation on `fix/m0-net10-postgres-browser-hardening`.

The corrected implementation passed the full `Verify M0` GitHub Actions run #2 at source revision `fec7429415fede121db8a4a34f0df72cd4fac609`: clean .NET build, fresh/seeded integration, Compose recovery, three functional browsers and final image scans. G0 was accepted on 2026-10-31. See [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md). Existing local volume testing is deferred to quarterly checkpoints; accessibility testing is deferred to final M24-04.

## Environment

| Item | Value |
| --- | --- |
| Operating system | Windows 11 Home 10.0.26200 |
| Repository location | `E:\APP IDEAS\HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform` (fresh clone, no copy on C) |
| .NET SDK | 8.0.423 (repository `global.json` pins 8.0.400 with `latestPatch`; SDK 9.0.200 is also installed and not selected) |
| Node.js / npm (host) | 24.20.0 / 11.19.0 |
| Node.js / npm (Docker web build) | 22.23.2 / 10.9.8 (`node:22-alpine`) |
| Docker | Engine and client 29.6.2, Compose v5.3.1, context `desktop-linux` (local named pipe) |
| PostgreSQL | 16.15 (`postgres:16-alpine`, digest `sha256:0cefaabd882e42bcb2a2f3dc56d3ce4770560ba62fd7f197f1a2e008fd5731ab`) |
| ASP.NET runtime in image | 8.0.33 on Debian 12.15 |
| nginx in image | 1.30.6 |
| Git | 2.39.1.windows.1 |
| Browser | Embedded desktop browser pane, Chrome 152.0.7977.167 engine, Windows 10.0 x64 |

**Storage.** The mandatory [Local Storage Policy](LOCAL_STORAGE_POLICY.md) now restricts all controllable persistent project storage to E:, with only unavoidable short-lived Windows temporary activity on C: permitted. At the start of M0 verification, C: had 4.9 GB free and E: 123.6 GB free. Docker Desktop stores its WSL data on E: (`CustomWslDistroDir`). NuGet, npm and .NET CLI caches were redirected to `E:\APP IDEAS\harborops-m0-cache` with `NUGET_PACKAGES`, `npm_config_cache` and `DOTNET_CLI_HOME`. Nothing was deleted or cleaned on C or E. Existing unrelated containers, volumes and networks were not touched; the only Docker objects created are the `harborops` Compose project and one disposable test database container, `harborops-test-pg`.

**Disposable integration database.** `harborops-test-pg` runs `postgres:16-alpine` on `127.0.0.1:55433` with its data directory on tmpfs, database `harborops_integration`, and a random password kept outside the repository.

## Result matrix

| ID | Outcome | Evidence summary |
| --- | --- | --- |
| M0-01 Assess existing M0 branch | Verified | Source inventory, static audit and defect list below; starting SHA recorded |
| M0-02 Build and test foundations | Verified | `verify-local.sh` exit 0 at `4fb34eb`: 0 warnings, 0 errors, 1 unit, 10 contract, 5 integration, 6 frontend tests passing, `npm ci` and production build clean |
| M0-03 Verify migration and seed | Verified | Migration applied to empty PostgreSQL 16.15 twice, seed idempotent, schema and row inspected with `psql`, snapshot matches model |
| M0-04 Verify local stack and browser | Verified | Clean-volume Compose start, migrator exit 0, smoke script exit 0, real browser path API to PostgreSQL observed |
| M0-05 Verify failure and hygiene | Verified | Outage script exit 0, manual outage with browser, recovery, secret scans, dependency and image scans |
| G0 (historical run) | At the time verified, acceptance pending | Older state-dependent check; superseded by the passing and accepted hardening run |

## M0-02 Build and test commands

Run at `4fb34eb` through `bash scripts/verify-local.sh` with `HARBOROPS_TEST_DB` set to the disposable database, after deleting `web/node_modules` and `web/dist`.

| Command | Result |
| --- | --- |
| `dotnet restore HarborOps.sln` | Succeeded, 8 projects |
| `dotnet build HarborOps.sln -c Release --no-restore` | Succeeded, 0 warnings, 0 errors (warnings are errors in `Directory.Build.props`) |
| `dotnet test tests/HarborOps.UnitTests/...csproj -c Release` | Passed 1, failed 0 |
| `dotnet test tests/HarborOps.ContractTests/...csproj -c Release` | Passed 10, failed 0 |
| `dotnet test tests/HarborOps.IntegrationTests/...csproj -c Release` | Passed 5, failed 0 |
| `npm ci --no-audit --no-fund` (in `web`) | Added 104 packages |
| `npm test` | 6 passed, 0 failed (also 6 passed on Node 22.23.2) |
| `npm run build` | `tsc -b` and `vite build` succeeded, 17 modules |
| `docker compose config --quiet` | Succeeded |

Contract tests host the real API pipeline with `WebApplicationFactory` and assert: liveness 200 with no database; readiness 503 and baseline 503 with an unreachable database; sanitized error body without password or host; baseline 503 when the seed row is missing; the exact four-field synthetic contract; unique 32 hex correlation identifier on every response including 404; CORS allows only the configured origin; the dependency-failure log entry carries the same correlation identifier as the response and no secret; timestamp format accepted by the frontend parser.

Integration tests assert: migrate and seed twice leaves one row with the fixed UTC timestamp and no pending migrations; the model snapshot has no differences from the current entity model; migrations are discovered in order; the API serves 200 for liveness, readiness and baseline from a real PostgreSQL database; deleting the baseline row turns readiness and baseline into 503 while liveness stays 200, and re-seeding recovers.

Integration safety guard, checked negatively: with `HARBOROPS_TEST_DB` unset, with database `postgres`, and with host `db.example.test`, the integration tests fail before opening any connection.

## M0-03 Migration and seed evidence

Applied to the empty disposable database: table `bootstrap_records` with `key varchar(80)` primary key, `dataset_version varchar(80) not null`, `seeded_at_utc timestamptz not null`; one row `harborops | m0-synthetic-v1 | 2026-10-30 00:00:00+00`; `__EFMigrationsHistory` contains only `20261030170000_InitialBootstrap` (EF 8.0.33). The same schema and row were then produced by the Compose migrator on a clean volume (second `psql` inspection).

## M0-04 Compose and browser evidence

Clean-volume run: `docker compose down -v` (own project only), `docker compose up -d --build`, then `bash scripts/smoke-compose.sh` (exit 0, "API, dependency readiness, and frontend HTTP smoke checks passed"). Migrator exited 0 before the API started; postgres reached `healthy`.

Browser observations (Chrome 152 engine, `http://localhost:5173`):

| Check | Observed |
| --- | --- |
| Initial load | Status screen rendered from `GET http://localhost:8080/api/v1/system/baseline` 200 |
| Data and labels | Service `HarborOps`, provenance "Synthetic demonstration", dataset `m0-synthetic-v1`, seed reference `2026-10-30T00:00:00.000Z` |
| Network requests | Only the HTML, one JS bundle, one CSS file and the baseline call; no other origins |
| Manual refresh and rapid refresh | 15 clicks produced 15 requests, superseded requests aborted, final state ready with correct data, no alert |
| Keyboard | Tab reached the Refresh button with a 2 px cyan outline; Enter triggered a refresh |
| Database stopped | Alert "Service unavailable / Service returned HTTP 503" |
| Database restored | Manual refresh returned the data; no reload needed |
| Hung API (container paused) | Alert "The service did not respond in time" after the 10 s client timeout |
| Responsive layout | 375 px wide emulation, no horizontal overflow |
| Console | The only console error was the 503 resource message from the deliberate outage |

The outage, rapid refresh, keyboard, hung-API and mobile checks ran against the stack built from `ecfc77e`; the later commits changed the nginx base image (`e3af3ed`) and added one warning log (`4fb34eb`). After the final rebuild the page was loaded again and showed the same data with the same three requests. Screen reader behavior and color contrast were not measured; this register makes no accessibility claim beyond the focus ring and the `alert` and `status` roles.

## M0-05 Failure and recovery evidence

Preflight: Docker context `desktop-linux` (local pipe), Compose project `harborops`, data is the synthetic seed only, the script stops only the `postgres` service of this project and restores it from an exit trap.

| Condition | Liveness | Readiness | Baseline |
| --- | --- | --- | --- |
| Database healthy | 200 | 200 | 200 |
| Database stopped | 200 | 503 | 503 `dependency_unavailable` with correlation identifier |
| Database restored | 200 | 200 | 200 |

`bash scripts/check-m0-degraded.sh --confirm-local-outage` exited 0 three times (first on the working tree that became `ecfc77e`, then on the clean volume at `e3af3ed`, then on the final `4fb34eb` build). In the first manual outage the same codes were captured with `curl`. The final run logged `Baseline request failed because a dependency is unavailable (InvalidOperationException)` with a `CorrelationId` scope.

## Security and hygiene

| Check | Result |
| --- | --- |
| Secret patterns in tracked files | None; `.env` is ignored, only `.env.example` with a placeholder is tracked |
| Secret patterns in M0 branch history | None found (`git log -p --all -S"Password="` and keyword scan) |
| Password in container logs | 0 occurrences in api, migrator, postgres and web logs |
| Error bodies | `internal_error` and `dependency_unavailable` envelopes carry no connection details (asserted by a contract test) |
| CORS | Allowed origin echoed for `http://localhost:5173`, absent for `http://evil.example` (contract test and live curl) |
| Listening ports | Compose publishes 127.0.0.1 only: 8080, 5173, 55432 |
| NuGet advisories | `dotnet list package --vulnerable --include-transitive`: none in all 8 projects |
| NuGet deprecation | `xunit` 2.9.2 flagged Legacy (successor `xunit.v3`) in two test projects; test-only, low severity, not changed |
| npm advisories | Initially 1 high (postcss 8.5.6, four advisories); fixed by pinning 8.5.32; `npm audit` reports 0 vulnerabilities, including production only |
| Container settings | api and migrator run as UID 1654, read-only root filesystem, all capabilities dropped, `no-new-privileges`; web has `no-new-privileges`; postgres is stock |
| Image scan | Trivy 0.58.1, database updated 2026-10-30, fixable HIGH and CRITICAL only: api image 0; web image 0 after the nginx update; `postgres:16-alpine` 1 CRITICAL and 25 HIGH, all in the `gosu` helper binary's Go standard library |
| Attribution and punctuation | No tool or co-author attribution in tracked files or in any commit made for M0; no em dashes in the changes made here |

Remaining risks: (1) the `gosu` findings are upstream and the binary only drops privileges at container start; postgres is bound to loopback and local-only. This is a documented residual risk, not explicit risk acceptance; verify disposition before final G0 acceptance. Revisit when a newer 16.x image is published. (2) .NET 8 support ends 2026-11-10, 11 days after this run; the .NET 10 move should be scheduled before any hosted use. (3) The Trivy findings reflect the database on the run date. (4) The API serves HTTP only; there is no TLS or authentication yet by design. (5) The Docker .NET base tags (`8.0`) are floating, so rebuilds can change patch levels. (6) `VITE_API_BASE_URL` is fixed at web image build time, not at container start.

## Defects found and corrected

| # | Finding | Severity | Fix |
| --- | --- | --- | --- |
| 1 | No line-ending rules; on a Windows checkout with `core.autocrlf=true` the shell scripts were CRLF and cannot run under bash | Medium | `.gitattributes` (`53aee70`) |
| 2 | No frontend lockfile; postcss 8.5.6 carried a high advisory | High for build tooling | Lockfile with 129 integrity-checked registry entries and postcss 8.5.32 (`216accf`) |
| 3 | Baseline returned HTTP 500 with the database down because EF wraps the provider exception in `InvalidOperationException`; the existing filter only matched the unwrapped types. The new contract test failed before the fix | High (acceptance expectation violated) | Inner-chain check (`adbee5e`) |
| 4 | JSON logs omitted scopes, so the correlation identifier never reached any log line | Medium | `IncludeScopes = true` (`adbee5e`) |
| 5 | Dependency failures on the baseline path produced no log entry | Medium | Warning with correlation scope, type name only (`4fb34eb`) |
| 6 | Existing contract test only serialized a DTO and could not detect HTTP-level regressions | Medium | 10 pipeline-level contract tests, snapshot and API-against-PostgreSQL tests |
| 7 | A hung API left the UI loading forever | Medium | 10 s client timeout with an explicit error (`ecfc77e`) |
| 8 | Web image fell back to `npm install` without a lockfile | Medium | `npm ci` only (`ecfc77e`) |
| 9 | Hardening: unbounded 15 s connect time, no readiness time limit, client abort treated as server error, writable root filesystem and full capabilities in api containers | Low | Bounded connect and readiness time, 499 on client abort, container settings (`adbee5e`, `ecfc77e`). The abort path has no automated test |
| 10 | Web base `nginx:1.27-alpine` carried 2 critical and 43 high fixable findings on an older scanner database | High | `nginx:1.30.6-alpine` plus `apk upgrade` (`e3af3ed`); rescan 0 |

## Execution notes

- One `docker compose up -d --build` failed with a transient `EOF` fetching `mcr.microsoft.com` metadata; the immediate retry succeeded with exit 0 and no change.
- `docker pull` refreshed `postgres:16-alpine` and fetched `nginx:1.30.6-alpine` and `mcr.microsoft.com/dotnet/sdk:8.0` during the run.
- A stray local API process used for a manual test was found still running and was stopped.
- The scanner database was refreshed before the final scans; earlier scans used a cached database dated 2026-10-09.

## Known limitations

- The baseline record is infrastructure metadata. Shipments, authentication, events, dashboards and incidents are not implemented and belong to M1 and later.
- Real-browser verification used one embedded Chrome 152 engine only; Firefox, Safari and screen readers were not tested.
- The CI workflow `Verify M0` has not been run and cannot be dispatched until it exists on the default branch.
- The compose stack was verified on one Windows host with Docker Desktop.
- Local Node.js is 24.20.0; the frontend tests and the container build were additionally run on Node 22.23.2.
- Raw logs and screenshots are stored locally, not in the repository.

## Reproduce

```bash
cp .env.example .env              # set a unique HARBOROPS_DB_PASSWORD
docker run -d --name harborops-test-pg --tmpfs /var/lib/postgresql/data \
  -e POSTGRES_DB=harborops_integration -e POSTGRES_USER=harborops_test \
  -e POSTGRES_PASSWORD=<local value> -p 127.0.0.1:55433:5432 postgres:16-alpine
export HARBOROPS_TEST_DB="Host=127.0.0.1;Port=55433;Database=harborops_integration;Username=harborops_test;Password=<local value>"
bash scripts/verify-local.sh
bash scripts/smoke-compose.sh
bash scripts/check-m0-degraded.sh --confirm-local-outage
```

## Status rule

An implemented code path is not a verified runtime behavior, and a verified gate is not an accepted milestone. The historical result was superseded by the passing hardening run. G0 was explicitly accepted on 2026-10-31; M1 remains unstarted until D-001 and its milestone work begin.
