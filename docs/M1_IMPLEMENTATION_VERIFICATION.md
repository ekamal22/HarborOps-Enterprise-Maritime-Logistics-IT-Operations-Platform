# M1 Implementation and Verification Register

**Branch:** `feat/m1-authentication-session-lifecycle`  
**Scope:** M1-01 through M1-04  
**State:** Source implementation in progress; G1 not verified, acceptance pending.  
**Baseline:** G0 accepted on 2026-10-31.

## Implemented paths

- `src/HarborOps.Infrastructure/Identity/`: local account, Argon2id hash, server session, event persistence and revocation.
- `src/HarborOps.Infrastructure/Persistence/Migrations/20261031010000_IdentitySessions.cs`: additive identity migration.
- `src/HarborOps.Api/Authentication/`: protected cookie middleware, CSRF/origin rules, login/session/logout/logout-all/disable and restricted CLI.
- `web/src/auth/AuthPanel.tsx`, Nginx and Vite: same-origin browser sign-in.
- `compose.yaml`, API Dockerfile: persistent key ring, local loopback profile.
- `tests/HarborOps.IntegrationTests/IdentityLifecycleTests.cs` and `tests/browser/specs/auth.spec.mjs`: initial G1 regression coverage.
- `.github/workflows/verify-m1.yml`: disposable CI verification, revision-tagged images, current vulnerability scanning, cross-browser regression, and evidence upload.

## Verification matrix

| Area | State |
| --- | --- |
| Compile and migration snapshot | Not yet executed |
| Clean PostgreSQL and M0 to M1 upgrade | Not yet executed |
| Password hashing and provisioning | Not yet executed |
| Cookies, CSRF, origin and rate limit | Not yet executed |
| Logout, logout-all, disable and reset | Not yet executed |
| Browser Chromium/Firefox/WebKit | Not yet executed |
| Key-ring restart and database outage | Not yet executed |
| M0 regressions, Compose recovery and image scanning | Not yet executed |

No M1 tests are claimed to pass until a completed workflow or independent execution reports exact results. Existing-volume compatibility is not a G1 requirement. Full accessibility verification remains M24-04.

## Scope boundaries

Organization membership and role enforcement remain M2, tracking integration authentication remains M7. The M0 baseline is public synthetic data only. There are no real accounts, external providers, or production deployments.
