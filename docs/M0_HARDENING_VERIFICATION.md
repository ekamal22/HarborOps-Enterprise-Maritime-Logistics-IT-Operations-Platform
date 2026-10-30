# M0 Hardening Verification and Remediation Register

**Target branch:** `fix/m0-net10-postgres-browser-hardening`  
**Prior independently audited revision:** `a791561a797b57afac84e7df7f8a959890b6972c`  
**Current correction state:** Source changes committed; fresh runtime re-verification pending.  
**Gate:** Acceptance pending. The earlier `4fb34eb` record is historical and its complete G0 conclusion is reopened because a fresh-database test failed on that baseline. It does not verify the corrected branch.

## Recorded audit of the earlier hardening revision

The audit was executed on a detached E-drive verification worktree, against an isolated `ho-verify` Compose project. This table is **historical evidence from the audited revision**, not a test run of the subsequent corrections.

| Check | Recorded outcome | Constraint |
| --- | --- | --- |
| .NET 10 restore and Release build | PASS, zero warnings/errors | SDK 10.0.403 container, not an installed host SDK |
| Unit and HTTP contract tests | PASS, 1/1 and 10/10 | At audited revision |
| Integration tests on empty database | FAIL, 4/5 | A tracked bootstrap record conflicted on re-seed |
| Integration tests against already seeded database | PASS, 5/5 twice | State-dependent result did not close the fresh-database failure |
| EF migration discovery and snapshot | PASS | At audited revision |
| PostgreSQL 16 startup, fresh initialization and existing-volume copy | PASS | Original volume mounted read-only; disposable copy used |
| Derived PostgreSQL non-root execution | PASS | UID 70, no `gosu`/`su-exec`, capabilities dropped |
| Compose bootstrap and database outage/recovery | PASS | 200/200/200, 200/503/503, 200/200/200 |
| Final api/web/postgres image scan | PASS, 0 HIGH/CRITICAL | Trivy 0.58.1 and vulnerability DB refreshed 2026-10-30 |
| Chromium/Firefox/WebKit original suite | PASS, 12/12 | Temporary npm lockfile, not reproducible from Git at that revision |
| WCAG contrast on baseline UI | FAIL | Footer text 4.24:1; 4.5:1 required |
| Manual screen-reader testing | DEFERRED | Not required for M0; scheduled for M23 interface polish and accessibility |

Evidence captured on E: under `E:\APP IDEAS\harborops-m0-cache\evidence\m0-hardening`. Neither the original M0 volume nor unrelated containers were modified.

## Corrections now committed

| ID | Finding and source-level correction | Verification still required |
| --- | --- | --- |
| D1 | Integration recovery uses a **new DbContext** after raw SQL deletes the row, avoiding stale tracked entities | Execute the specific integration test on a new database and multiple complete cold runs |
| D2 | Footer color changed to `text-slate-400`; ready-state announcement uses its own status role instead of nested live regions | Rerun all browser engines, measured contrast and automated status/keyboard assertions |
| D3 | PowerShell runners resolve their own checkout or specified E-drive worktree, support expected SHA, and image scanning resolves running container IDs and source-revision labels | Run each runner on the corrected worktree and confirm stale image/branch rejection |
| D3 | Compose smoke and outage scripts require an explicit disposable `ho-verify` project and confirmation; both support an external env file | Test positive and fail-closed paths; confirm original project remains untouched |
| D4 | `tests/browser/package-lock.json` added with exact Playwright 1.64.1 registry URLs and integrity hashes cross-checked between two independently generated npm lock records | Execute `npm ci` on E: and validate lockfile consistency; no local install run on the corrected branch yet |
| D5 | Added independent three-engine regression scenarios for refresh races, external requests/console errors, four widths, footer contrast and status semantics | Run the expanded suite from committed lockfile; verify no legitimate aborts are misclassified |
| D6 | Removed redundant ASP.NET URL override; retained documented low-severity image/package concerns | Confirm runtime log warning is gone; review remaining library and image observations |

Unchanged low-severity concerns: test-only xUnit 2.x is marked legacy; a GSSAPI library warning was observed during migrator startup; the web service still uses the stock nginx capability profile; base image tags and `apk upgrade` can change downstream package contents between builds. Do not change these speculatively without verification and an explicit security assessment. The scanner runner now defaults to the reviewed Trivy 0.76.1 container image; its actual new scan has not been executed.

## E-drive verification sequence

First confirm the selected repository/worktree, Git SHA and clean tracked state. Set local Docker Desktop/WSL, npm/NuGet and temporary storage to approved E-drive locations per [Local Storage Policy](LOCAL_STORAGE_POLICY.md). Do not delete existing database volumes or Docker images to free space.

### Framework

```powershell
powershell -NoProfile -File scripts/verify-dotnet10.ps1 -UseContainer
```

The command above performs restore/build using a locally available SDK image. Run all three .NET test projects against a disposable PostgreSQL instance using an appropriately configured local/container network. Capture a cold-start isolated run of the integration test that previously failed and repeat the complete suite.

### Browser

```powershell
powershell -NoProfile -File scripts/verify-browser.ps1 -InstallBrowsers
```

Use `-RepositoryPath` when testing a detached E-drive worktree, and `-ExpectedSha` to enforce a particular revision. Browser binaries and test output stay on E:. With the committed lockfile, the runner uses `npm ci`; normal verification should not regenerate dependency resolution.

### Disposable Compose smoke and outage

Provide an approved local Compose password configuration, optionally via `--env-file` referencing an E-drive secret file. Stop any other service using the M0 loopback ports before starting the isolated project.

```bash
bash scripts/smoke-compose.sh --project ho-verify --confirm-disposable
bash scripts/check-m0-degraded.sh --project ho-verify --confirm-local-outage
```

Both scripts refuse the main `harborops` project for fault testing. The smoke script builds source-revision-labeled images and validates running container ownership. Use an E-drive external env file if `.env` is not configured.

### Image scanning

```powershell
powershell -NoProfile -File scripts/verify-container-security.ps1 -Project ho-verify -UseContainerScanner -RefreshDatabase
```

This resolves **immutable image IDs from running Compose containers**, rejects mismatched source-revision labels and writes redacted evidence under the E-drive cache. A local copy of the selected scanner image is needed; no fallback to stale `latest` tags is allowed.

### M0 accessibility gate: automated only

M0 requires automated checks for text contrast (WCAG AA for small text), meaningful headings, accessible button names, keyboard activation and focus visibility, error/status roles, responsive layout and no horizontal overflow. Execute those checks through the Chromium, Firefox and WebKit browser suite. Where tests are not automated, record a limitation rather than claiming it passed. **Manual screen-reader use, spoken-output validation and manual browser zoom are not M0 acceptance requirements.** They are deferred to M23 Interface polish and accessibility when the actual business workflows and navigation are implemented. Do not block M0 solely because no screen reader is installed.

## Required acceptance evidence for corrected branch

1. Exact corrected branch HEAD, source diff, environment and E-drive locations.
2. Clean .NET 10 restore/build and all unit, contract and **fresh-database** integration tests.
3. EF Core 10 snapshot/migration and repeatable seed evidence.
4. Disposable fresh-volume and copied historical-volume PostgreSQL startup with row and schema checks.
5. Source-labeled Compose build, successful smoke run and isolated database outage/recovery.
6. Trivy 0.76.1 or newer scan of actual running PostgreSQL, API and web images, with current DB metadata and observed critical/high counts.
7. Committed-lockfile `npm ci`, frontend build, and expanded Chromium/Firefox/WebKit results.
8. Automated text contrast, accessible names, keyboard/focus, error/status roles and responsive-width results. Manual screen reader and real-device zoom are deferred to M23, not blockers for G0.
9. Hygiene, warnings, remaining vulnerability disposition, exact logs and result matrix.

**Status:** Corrections committed, full revised-branch verification pending. No milestone acceptance or public-deployment authorization is implied. M1 remains unstarted.
