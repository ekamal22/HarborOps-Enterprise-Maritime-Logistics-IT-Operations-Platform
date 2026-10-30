# M0 Hardening Verification and Remediation Register

**Verified source branch:** `fix/m0-net10-postgres-browser-hardening`; merged into `main` through PRs #3 and #1  
**Prior independently audited revision:** `a791561a797b57afac84e7df7f8a959890b6972c`  
**Current state:** Revised code verified in GitHub Actions against exact commit `fec7429415fede121db8a4a34f0df72cd4fac609`; G0 accepted on 2026-10-31.  
**Gate:** G0 verified at the identified hardening code SHA and explicitly accepted on 2026-10-31. The earlier `4fb34eb` result remains historical and was superseded after its cold-database regression.

## Current G0 execution: GitHub Actions

- **Workflow:** [Verify M0, run #2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41134743879)
- **Evidence artifact:** [m0-verification-41134743879](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41134743879/artifacts/13016026538) (GitHub retention policy applies)
- **Source revision executed:** `fec7429415fede121db8a4a34f0df72cd4fac609`
- **Environment:** GitHub-hosted `ubuntu-latest`, .NET 10, Node 22, isolated `ho-verify` Docker Compose project and fresh non-root PostgreSQL 16.
- **Result:** **SUCCESS**, with every required job step passing and no skipped core verification stages. No local E-drive volume was accessed.
- **Build:** Release build succeeded, **0 warnings, 0 errors**.
- **Tests:** .NET unit **1/1**, contract **10/10**, fresh PostgreSQL integration **5/5**, repeat seeded integration **5/5**, frontend **6/6**, functional Playwright **12/12** across Chromium, Firefox and WebKit.
- **Runtime:** Disposable PostgreSQL creation, migrator, API and frontend startup, health/synthetic baseline smoke checks, and database stop/restart with 200/503/503 then 200/200/200 recovery all **PASS**.
- **Images:** Trivy 0.76.1 scanned immutable running PostgreSQL, API and web image IDs. Each `HIGH,CRITICAL --exit-code 1` scan passed with no high/critical vulnerability findings reported. Source labels matched the tested commit.
- **Cleanup:** Isolated GitHub runner containers and test volume were removed on successful completion.
- **Run #1:** [Failed first attempt](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41134236402) due exclusively to an ambiguous Playwright text selector. That selector was corrected and all browser cases passed in run #2.
- **Scope exceptions:** Existing local historical Docker volumes are not part of G0 and are reserved for quarterly milestones M6, M12, M18 and M24-03 (with full migration/backup recovery at M19). Accessibility testing is exclusively a final M24-04 gate. Hosted/production readiness and M1-M24 capabilities are not implied.

**Decision:** G0 **verified and accepted** on 2026-10-31. The accepted code was merged through PRs #3 and #1 into `main`. This does not authorize production deployment or future milestone acceptance.

## Recorded audit of the earlier hardening revision

The historical audit was executed on a detached E-drive verification worktree, against an isolated `ho-verify` Compose project. This table is **historical evidence from the audited revision**, not a test run of the subsequent corrections.

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
| Historical WCAG contrast observation | FAIL on audited revision | Footer previously measured 4.24:1; corrected styling is committed, final accessibility testing belongs to M24-04 |
| Accessibility audit | DEFERRED | All accessibility testing, automated and manual, belongs exclusively to M24-04 |

Evidence captured on E: under `E:\APP IDEAS\harborops-m0-cache\evidence\m0-hardening`. Neither the original M0 volume nor unrelated containers were modified.

## Corrected findings and verification disposition

| ID | Finding and source-level correction | Observed verification or deferred follow-up |
| --- | --- | --- |
| D1 | Integration recovery uses a **new DbContext** after raw SQL deletes the row, avoiding stale tracked entities | PASS: 5/5 on a fresh database and 5/5 on a seeded rerun in GitHub Actions run #2 |
| D2 | Footer color changed to `text-slate-400`; ready-state announcement uses its own status role instead of nested live regions | PASS: 12/12 functional browser cases in run #2. Full accessibility testing remains deferred to M24-04 |
| D3 | PowerShell runners resolve their own checkout or specified E-drive worktree, support expected SHA, and image scanning resolves running container IDs and source-revision labels | PASS: CI checked the exact source revision and running image labels. Standalone Windows PowerShell runner behavior remains unverified and is not a G0 requirement |
| D3 | Compose smoke and outage scripts require an explicit disposable `ho-verify` project and confirmation; both support an external env file | PASS: isolated `ho-verify` Compose smoke and database stop/recovery executed. The local retained volume was not accessed. Additional Windows-specific negative-path tests are not G0 requirements |
| D4 | `tests/browser/package-lock.json` added with exact Playwright 1.64.1 registry URLs and integrity hashes cross-checked between two independently generated npm lock records | PASS: `npm ci` from committed lockfile on the disposable GitHub runner. E-drive local install is unnecessary for G0 |
| D5 | Added three-engine functional browser regressions for refresh races, external requests and console errors; isolated accessibility specs in a separate opt-in M24-only suite | PASS: functional Chromium, Firefox and WebKit tests, 12/12 in run #2. Accessibility remains M24-04 |
| D6 | Removed redundant ASP.NET URL override; retained documented low-severity image/package concerns | The successful CI build reported zero warnings and zero errors. Test-only library and image hygiene remain future maintenance observations, not additional G0 blockers |

Unchanged low-severity concerns: test-only xUnit 2.x is marked legacy; a GSSAPI library warning was observed during migrator startup; the web service still uses the stock nginx capability profile; base image tags and `apk upgrade` can change downstream package contents between builds. Do not change these speculatively without verification and an explicit security assessment. The Trivy 0.76.1 container performed the current G0 image scans successfully in run #2.

## Historical-volume compatibility: quarterly roadmap checkpoints

**G0 does not require checking the existing local Docker volume.** Only a fresh disposable PostgreSQL instance, migrations, repeatable synthetic seed, API access and controlled outage/recovery are mandatory. An earlier audit also exercised a disposable copy of a historical volume; that result is retained as optional historical evidence, not a recurring G0 obligation.

Quarter-mile compatibility checkpoints are **M6 (Q1), M12 (Q2), M18 (Q3) and M24-03 (Q4)**. At each checkpoint, use a disposable clone of a representative historical snapshot. If no retained snapshot exists, record NOT APPLICABLE. These are delivery milestones, not a time-based automation. **M19** retains the comprehensive backup/restore and upgrade rehearsal.

When an actual breaking schema, major PostgreSQL or data-storage format change will be applied to preserved data, a one-off dry run against a safe copy remains mandatory before deploying that specific change. Never modify the only copy of an existing volume.

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

### Final-release-only accessibility testing

**No accessibility testing is required for G0-G23.** The M0 Playwright command runs functional browser tests only, not contrast, keyboard, focus, accessible naming, screen-reader, zoom, responsive accessibility or WCAG assertions. Accessibility test cases are segregated in `tests/browser/release/` and not discovered by the default `tests/browser/playwright.config.mjs`. They may be run only during **M24-04**, the final release work package, using `npm run test:final-accessibility` and the separate release configuration. M23 is interface polish, not accessibility validation. The full M24 audit will cover the finished product and include manual assistive-technology assessment.

## GitHub Actions as the M0 verification environment

A GitHub-hosted Ubuntu runner completed the required M0 checks against the exact source commit in run #2 and published verification evidence. It must execute clean .NET 10 restore/build and all tests against an empty PostgreSQL database, migration and repeatability checks, Compose startup, controlled database outage/recovery, a current vulnerability scan of the exact running images, locked frontend and browser dependency installation, and Chromium/Firefox/WebKit functional browser checks only. It does not require accessibility tests at G0. The current `.github/workflows/verify-m0.yml` covers the complete G0 verification scope and passed on run #2.

The workflow ran through a narrow pull-request trigger for the workflow-file update and passed. Its current trigger does not automatically consume minutes on unrelated changes. Future revisions require their own verification run; the historical passing SHA must not be reused as new-code evidence. GitHub-hosted runner storage is temporary remote storage, not the workstation C: drive. The E-drive policy remains mandatory for local work.

All accessibility verification, automated or manual, is deferred to final M24-04 and is explicitly excluded from M0 acceptance.

## Required acceptance evidence for corrected branch

1. Exact corrected branch HEAD, source diff, environment and E-drive locations.
2. Clean .NET 10 restore/build and all unit, contract and **fresh-database** integration tests.
3. EF Core 10 snapshot/migration and repeatable seed evidence.
4. Disposable **fresh-volume** PostgreSQL startup, schema migration and deterministic seed checks. Existing-volume compatibility is **not** a G0 requirement and is deferred to quarterly milestone checkpoints.
5. Source-labeled Compose build, successful smoke run and isolated database outage/recovery.
6. Trivy 0.76.1 or newer scan of actual running PostgreSQL, API and web images, with current DB metadata and observed critical/high counts.
7. Committed-lockfile `npm ci`, frontend build, and expanded Chromium/Firefox/WebKit results.
8. Functional browser results for data display, refresh, API failure and recovery in Chromium, Firefox and WebKit. Accessibility checks are not G0 requirements and are deferred entirely to M24-04.
9. Hygiene, warnings, remaining vulnerability disposition, exact logs and result matrix.

**Status:** G0 verified at `fec7429415fede121db8a4a34f0df72cd4fac609` and **accepted** on 2026-10-31. PRs #3 and #1 are merged to `main`. Public-deployment authorization is not implied. M1 remains unstarted.
