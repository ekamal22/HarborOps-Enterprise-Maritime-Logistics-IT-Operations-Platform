# M1 G1 Verification Plan

**State:** Verification executed, G1 accepted on 2026-10-31 and M1 merged into `main`. [Execution record](M1_IMPLEMENTATION_VERIFICATION.md).  
**Gate:** G1 verified and accepted. [Source verification](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41143665657) and [documentation revision verification](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41144356898) passed.  
**Source:** [M1 Authentication Design](M1_AUTHENTICATION_DESIGN.md), [Acceptance Criteria](ACCEPTANCE_CRITERIA.md), [Roadmap](ROADMAP.md).

## Verification environment

Historical G1 verification used GitHub Actions on disposable Ubuntu runners as the evidence environment. This section records that completed approach and does not authorize any new workflow executions. It reused the M0 .NET 10/Node/Compose and image-scan sequence, plus isolated PostgreSQL test databases. Start from a fresh database, and separately rehearse M0-to-M1 additive migration on a disposable M0-seeded database. No access to existing local E-drive Docker volumes is required. Do not use real user credentials or private data. Local persistent evidence and keys remain E-backed per [Local Storage Policy](LOCAL_STORAGE_POLICY.md).

For the historical G1 evidence, a workflow trigger had to run on the tested branch/commit, publish redacted evidence and use narrow triggers to avoid unnecessary Actions minutes. Build/test and final image scan failures must appear separately; do not hide a security failure when an independent browser test fails.

## Mandatory test coverage

| Area | Test evidence |
| --- | --- |
| Compile | Release .NET 10 build and frontend build with pinned dependencies |
| Schema | Fresh M1 migration, M0-to-M1 upgrade in disposable DB, migration idempotence/snapshot match |
| Credentials | New account, normalized email uniqueness, Argon2id salt/verify, wrong password, invalid/disabled account, credential reset |
| Expiry | Injected clock for idle/absolute expiry, sliding-window cap and expired-session rejection |
| Revocation | Current logout, logout-all, account disablement, password reset and old-ticket replay |
| Cookie and keys | HttpOnly, SameSite, host/path, HTTPS Secure, local-only exception and persistence across API restart |
| Browser origin | Nginx and Vite same-origin /api proxy, no localStorage token, API 401 not 302 |
| CSRF | Bootstrap/rotation after login; missing, forged, foreign-origin and cross-site mutation rejects |
| Throttle | Failed login source/identifier limits, hashing concurrency bound, generic response/timing controls |
| HTTP contracts | Response DTO allowlists, 200/204/400-or-403/401/429/503, error envelope and correlation ID |
| DB outage | Deny protected access on database loss, sanitized 503 and correct recovery |
| Browser | Chromium, Firefox and WebKit: login, reload/session restoration, logout, expiration and error recovery |
| Logging and scan | No secrets/cookies/hash/token in logs or artifacts, dependency/image scans and explicit exception disposition |
| M0 regression | Public synthetic baseline, readiness/liveness, seed and Compose recovery remain functional |

Automated accessibility audits, screen-reader checks, contrast and WCAG verification are **not** G1 checks; these run at final M24-04. Historical-volume compatibility is at M6, M12, M18 and M24-03, and M19 owns complete backup/restore rehearsal.

## Fixture boundaries

Create synthetic users only during isolated test setup, using a non-committed test secret mechanism and an ephemeral PostgreSQL instance. Ensure no test credentials, issued cookie values, tokens or password hashes are uploaded. Use an injected clock instead of real 30-minute waits. Use deterministic session/account fixtures with distinct simulated devices, redacted correlation IDs and explicit transaction cleanup.

## Evidence and acceptance

Capture branch and code SHA, database engine/schema revision, CLI command versions, hash-library version and measured settings, API/browser results, security failures or advisories, Compose restart and recovery state, and redacted logs. Report \`PASS\`, \`FAIL\`, \`BLOCKED\`, or \`NOT RUN\` separately for each mandatory check.

G1 required passing mandatory verification followed by explicit acceptance. Both were completed on 2026-10-31, and PR #4 was merged into `main` at `8f9fc0d506a9e1dc1d4f5acab936622df988f413`. Production deployment and future milestone acceptance remain separate decisions.
