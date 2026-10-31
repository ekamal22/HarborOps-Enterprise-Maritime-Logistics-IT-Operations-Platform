# M2 GitHub Verification Results

**Date:** 2026-10-31  
**Branch:** `feat/m2-organization-authorization`, draft [PR #5](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/5)  
**Verified branch revision:** `65304b7351d8999ac89008828d326158bb18d3b7`  
**GitHub runner checkout revision:** `af059c80146e1ec300a502b3eadf6c516818622a` (PR merge ref)  
**Workflow:** [Verify M2 run #41157387340](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41157387340)  
**Run result:** **SUCCESS**, completed 2026-10-31 at 01:57:38 UTC  
**Gate state:** G2 open. Automated verification passed, but the full G2-S01 through G2-S25 adversarial specification has not been exercised. Acceptance not recorded.  
**Integration:** The M2 branch is not merged into `main`.

## Executed verification

| Check | Observed result | Evidence |
| --- | --- | --- |
| .NET 10 restore and Release solution build | PASS | Run #41157387340, Build .NET solution |
| Unit tests | PASS: 22 passed, 0 failed, 0 skipped | Unit tests job step |
| API contract tests | PASS: 12 passed, 0 failed, 0 skipped | API contract tests job step |
| Fresh real PostgreSQL integration | PASS: 13 passed, 0 failed, 0 skipped | Fresh PostgreSQL integration tests |
| Repeat real PostgreSQL integration | PASS: 13 passed, 0 failed, 0 skipped | Seeded PostgreSQL repeat tests |
| Frontend unit tests | PASS: 6 passed, 0 failed | Frontend tests and build |
| Locked frontend production build | PASS | Frontend tests and build |
| Planning graph and links | PASS | Planning document consistency |
| Disposable Compose startup and public M0 regression | PASS | Disposable Compose startup and M0 regression |
| PostgreSQL dependency outage and recovery | PASS | Dependency outage/recovery regression |
| Separate synthetic M1 and M2 account provisioning | PASS | Provision disposable browser account and Provision separate M2 browser identity |
| Local organization bootstrap | PASS | Bootstrap synthetic organization for M2 browser verification |
| M1 session restart, outage, logout-all and replay | PASS | Persistent session restart, outage and replay |
| Chromium, Firefox and WebKit functional browser tests | PASS: 24 passed | Cross-browser M0, M1 and M2 functional tests |
| PostgreSQL, API and web image vulnerability scans | PASS: no scan exit-code failure for HIGH/CRITICAL | Scan revision-labeled running images |
| Revision-label checks | PASS | Scan revision-labeled running images |
| Evidence artifact and disposable-resource cleanup | PASS | Upload verification evidence; Remove disposable runner resources |

The runner used disposable integration PostgreSQL and Docker Compose resources, not retained workstation databases or local historical Docker volumes. No dedicated accessibility testing was run, consistent with M24-04. The runner recorded diagnostic evidence with seven-day artifact retention. No public deployment or production security certification is implied.

## Corrections during verification

Earlier runs remain accessible for audit. They were not counted as passing:
- [41156345694](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41156345694): test container startup referenced inconsistent M1/M2 container names.
- [41156571932](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41156571932): .NET compilation exposed duplicated trailing SQL in M2 migration.
- [41156665541](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41156665541): strict role unit check rejected a parser that allowed leading whitespace.
- [41156754172](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41156754172): contract tests exposed an unannotated DELETE JSON body in endpoint binding.
- [41156878431](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41156878431): WebKit organization tests hit the intentional per-account login attempt limit when M1 and M2 shared a test user.
- [41157351445](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41157351445): superseded and cancelled after correcting the separate browser fixture command.

The final run includes the corrected M2 schema, role parser, DELETE binding, separated synthetic browser identity and the accepted M0/M1 regressions.

## Remaining G2-specific evidence

The executed suite does not cover every negative case required by the [G2 verification plan](M2_VERIFICATION_PLAN.md). No check may be marked PASS without its own test assertion or recorded runtime evidence.

| G2 check IDs | Status | Required follow-up |
| --- | --- | --- |
| G2-S01, S02 | PARTIAL | Add explicit existing M1-account/session-to-M2 upgrade and repeat-migration preservation fixtures |
| G2-S03 | NOT RUN | Direct PostgreSQL invalid role/status/version, duplicate slug/member and FK rejection |
| G2-S04 | PARTIAL | Negative local bootstrap cases for disabled/unknown account and duplicate slug |
| G2-S05 | PASS | All five declared roles evaluated by unit policy assertions |
| G2-S06 | PARTIAL | Full disabled account and revoked-cookie matrix on the new M2 routes |
| G2-S07, S08 | PASS | No selection, successful selection and differing roles across two organizations |
| G2-S09 | NOT RUN | Independent selections in two simultaneous sessions for one user |
| G2-S10 | PARTIAL | Foreign organization ID denial on every read and mutation verb |
| G2-S11 | NOT RUN | Existing foreign membership user ID denial on selected A member endpoint |
| G2-S12 | PARTIAL | Explicit HTTP denial for Analyst, Manager, Support and Auditor across all administrative operations |
| G2-S13 | PASS | Administrator of A denied foreign organization B by HTTP |
| G2-S14 | PARTIAL | Removal and selection clearing checked; suspension and concurrent revocation not fully exercised |
| G2-S15 | NOT RUN | Suspended organization denial across selection and administrative operations |
| G2-S16 | PARTIAL | Sole administrator account self-disable tested; competing last-admin role/removal transitions not exercised |
| G2-S17 | NOT RUN | Two-context concurrent membership administration and last-admin invariant |
| G2-S18 | PARTIAL | Stale version conflict checked; duplicate-create collision needs direct negative assertion |
| G2-S19 | PARTIAL | Foreign Origin denial checked; broaden invalid role/body/CSRF cases |
| G2-S20 | PARTIAL | M0/M1 database outage/recovery tested; specifically scoped organization access during outage untested |
| G2-S21 | PASS | Accepted M1 lifecycle regressions executed |
| G2-S22 | PARTIAL | Browser selection/reload/clear and administrator controls verified; cross-role switch and stale-revocation UI remain untested |
| G2-S23 | PARTIAL | Foreign endpoint denial checked; M2-specific enumeration and log redaction matrix incomplete |
| G2-S24 | PASS | Public synthetic M0 endpoint and M1 browser regressions passed |
| G2-S25 | PARTIAL | Running image scans and local Compose passed; full deployment/security review remains separate |

The next work is targeted additional G2 test implementation and a fresh on-GitHub run after those tests exist. A successful workflow is evidence for the assertions it executed, not proof of unimplemented adversarial cases. G2 remains open until the mandatory matrix is complete. Any later acceptance requires a separate explicit decision.

## Verification scope and workflow policy

GitHub Actions verification was expressly requested for M2 on 2026-10-31. The repository includes a dedicated `verify-m2.yml` workflow with M0/M1 regression coverage, and the M1 workflow is manual-only on this feature branch to avoid duplicate PR jobs. The documentation-only reconciliation commit uses `[skip ci]` so the passing source revision remains the last executed verification. M0/G0 and M1/G1 accepted status remains unchanged. No M3 implementation was started.
