# Cross-browser and accessibility verification

**Scope:** M0 foundation only. These tests do not certify M1-M24 interfaces.

## Automated browser verification

The browser project in `tests/browser` uses a pinned Playwright test runner and defines Chromium, Firefox and WebKit projects. It tests live synthetic baseline rendering, local-origin requests, keyboard refresh, a controlled HTTP 503 interface and mobile overflow.

Run from an E-drive PowerShell terminal after starting a disposable HarborOps Compose stack:

```powershell
# First run: generate the reproducible browser dependency lockfile
powershell -NoProfile -File scripts/verify-browser.ps1 -GenerateLockfile -InstallBrowsers

# Review and commit tests/browser/package-lock.json, then rerun
powershell -NoProfile -File scripts/verify-browser.ps1
```

The script puts npm cache, browser binaries, temporary data and test artifacts on E:. Do not run its browser download phase if E: has insufficient space. Check the Docker Desktop/WSL disk image remains E-backed. It requires the frontend and API on the M0 loopback ports.

**Current state:** tests and runner are implemented but **not yet executed** against the updated .NET 10 and hardened PostgreSQL branch. A generated lockfile is required before this phase can count as reproducible evidence.

## Assistive-technology verification

Automated accessible-name and focus checks are not a substitute for a real screen reader. Perform and record these manual checks on the actual running application:

1. Start Windows Narrator or NVDA on a disposable browser test profile, with screenshots/recordings saved only on E:.
2. Navigate to `http://localhost:5173` using keyboard only.
3. Confirm the page's headings convey the purpose and the System baseline section can be reached.
4. Confirm the Refresh status control is announced with its accessible name.
5. Activate refresh by keyboard and confirm the loading state and ready state are understandable.
6. Simulate an HTTP 503 using a disposable local test environment and confirm the error is announced by the live region.
7. Verify the error remains understandable without colors and the focus position remains useful after an update.
8. Verify 375px mobile layout and 200% browser zoom do not obscure the Refresh control or status.
9. Check text and interactive contrast under the selected color scheme with a measured method.
10. Record the screen reader version, browser version, exact run revision, pass/fail, evidence and any remediation.

For a finding that fails, fix the UI and rerun both the automated browser suite and the relevant screen-reader check.

## Framework and database verification

.NET 10 and the non-root PostgreSQL derived image require new verification evidence. Execute:

- Clean `dotnet restore`, `dotnet build` and all three .NET test suites.
- EF Core 10 migration snapshot comparison, fresh migration/seed twice, and historical volume startup with checksum/row validation.
- `docker compose build postgres api migrator web` and a fresh-volume Compose run.
- `bash scripts/smoke-compose.sh`, including the non-root Postgres helper check.
- Controlled outage and recovery on a disposable Compose project.
- Fresh Trivy DB scan of the **final derived** `harborops-postgres:16.15-rootless` image, plus API and web.
- Compare PostgreSQL 16 data schema and existing-volume access with previous M0 evidence.

Do not delete the existing E-backed `harborops-postgres` data volume to achieve startup. Use a separate disposable project/volume for clean initialization first, and take a known-good backup before testing an existing-volume migration. Do not treat removing `gosu` from the final filesystem as proof of a clean image scan.

## Evidence and acceptance rule

The earlier code revision `4fb34eb` remains the last fully verified M0 code baseline. This change branch has new runtime, storage and test dependencies, so it is **implementation pending re-verification**. Keep the revised verification status separate from the earlier G0 record. No automatic acceptance or production deployment is implied.
