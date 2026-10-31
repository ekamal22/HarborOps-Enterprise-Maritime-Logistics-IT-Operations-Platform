# M1 Authentication and Session Lifecycle Verification Register

**Date:** 2026-10-31  
**Branch:** `feat/m1-authentication-session-lifecycle`  
**Pull request:** [PR #4](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/4), draft  
**Verified feature revision:** `fdefe052e4335ca8d1d287160a9859828d05f9d8`  
**Exact tested PR merge revision:** `c7fc291b9c035e731b79336f578013c0463b82b4`, merged by GitHub for testing against `main` at `fb9c04dbca13a236af0c4a7c1de33ed51ef898ef`  
**CI run:** [Verify M1 run 41143665657](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41143665657)  
**Outcome:** All required workflow steps passed. **G1 engineering verification passed; acceptance pending.**  
**Integration:** Not merged to `main`; G0 remains accepted.

## Implemented M1 work packages

| Package | Delivered and observed |
| --- | --- |
| M1-01 | D-001 approved cookie and PostgreSQL session authority; JSON/error contracts, CSRF/origin protections and negative cases |
| M1-02 | Additive `users`, `user_credentials`, `auth_sessions` and `auth_events` schema; Argon2id with per-credential salt; restricted terminal provisioning/password reset; server-authoritative session store and transactional revocation |
| M1-03 | Cookie authentication, persistent Data Protection keys, login/session/guard, logout, logout-all, self-disable, request correlation, redacted auth events, rate limiting and sanitized failure responses |
| M1-04 | React authenticated shell, Nginx and Vite same-origin `/api` routing, local Docker profile, xUnit and three-browser functional tests, disposable CI evidence and running-image scans |

## Executed verification results

| Layer | Observed result |
| --- | --- |
| .NET 10 Release build | PASS, zero errors; warnings treated as errors |
| Unit tests | **5/5 PASS**, including password policy |
| HTTP contract tests | **12/12 PASS**, including anonymous 401, CSRF rejection, source and identifier throttle boundaries |
| Real PostgreSQL integration tests, fresh | **10/10 PASS** |
| Real PostgreSQL integration tests, repeat | **10/10 PASS** |
| Frontend tests | **6/6 PASS** and Vite production build succeeded |
| Planning graph and documentation checks | PASS, 25 milestones, 126 graph nodes, 27 Markdown files checked |
| Disposable Compose stack and M0 smoke | PASS |
| M0 PostgreSQL interruption/recovery | PASS |
| Restricted synthetic account provisioning | PASS |
| Authenticated session through API restart | PASS, session remains usable |
| Session with PostgreSQL unavailable | PASS, protected endpoint denies access with 503 |
| PostgreSQL recovery | PASS, valid session resumes |
| Logout-all and saved-cookie replay | PASS, revoked cookie receives 401 |
| Playwright functional browser tests | **18/18 PASS** across Chromium, Firefox, and WebKit |
| Immutable running-image PostgreSQL, API and web Trivy scans | PASS at HIGH/CRITICAL failure threshold, all three images matched tested merge SHA |

The database tests include controlled-clock idle and absolute expiry, password reset invalidation, revoked-session denial, self-disablement through the real HTTP pipeline, invalid credentials, cookie flags, CSRF and foreign-origin rejection, and a repeatable M0 bootstrap-record-to-M1 identity upgrade in an isolated schema. The database migration snapshot check remained enabled; no pending-migration warning was suppressed.

The isolated runner used Ubuntu, .NET 10, Node 22, PostgreSQL 16, a fresh disposable integration database, `ho-verify` Docker Compose and generated synthetic account credentials. It did not access a workstation volume or customer data. The account secret was masked in CI; account passwords are not passed as CLI arguments.

## Verification history and corrections

The initial workflow iterations reported authentic failures. They were corrected and rerun rather than marked as passing:

- EF Core snapshot/provider metadata and an omitted identity event index caused the first fresh-database migration failures. Schema and snapshot were aligned without suppressing drift detection.
- The documentation checker assumed D-001 was still an open decision; the approved-decision validator was corrected.
- Two functional browser tests treated the expected anonymous-session 401 as an unexpected console failure; the assertion now recognizes that specific expected response.
- A new HTTP self-disable test disposed its request before the asynchronous send completed; the test helper was corrected.
- The final source revision passed every workflow step in run 41143665657.

## Boundaries and remaining considerations

- This is a verified **local-first engineering milestone**, not a hosted or production security certification. Non-local TLS termination, certificate/key lifecycle, key rotation, event retention, account re-enablement procedures, operational Argon2id performance benchmarking, and more extensive abuse testing require review before public deployment.
- Organization membership, tenant authorization and role-based access are M2. Integration credentials and tracking-source authentication are M7.
- Historical retained-volume compatibility remains at M6, M12, M18 and M24-03; comprehensive backup/restore belongs in M19. No existing local Docker volume was accessed as part of G1.
- All dedicated accessibility verification is reserved for M24-04, per the accepted roadmap.
- The accepted G0 M0 routes, synthetic baseline, startup and fault behavior remain covered by the M1 regression suite.
- The earlier GitHub Actions failures remain accessible in the run history. Historical success for another source revision must not substitute for future changed-code verification.

## Gate decision

G1 has executed and passed its current engineering verification workflow. **Acceptance pending.** No merge, release claim, deployment authorization, or progression to M2 is implied. After an explicit acceptance decision, integrate the approved branch and record the merge revision in [Project Status](STATUS.md). Any material source changes require fresh verification.
