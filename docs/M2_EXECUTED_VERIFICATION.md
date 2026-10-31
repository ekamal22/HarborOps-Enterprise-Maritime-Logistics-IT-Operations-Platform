# M2 GitHub Verification Results

**Date:** 2026-10-31  
**Branch:** `feat/m2-organization-authorization`, draft [PR #5](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/5)  
**Verified branch revision:** `65304b7351d8999ac89008828d326158bb18d3b7`  
**GitHub runner checkout revision:** `af059c80146e1ec300a502b3eadf6c516818622a` (PR merge ref)  
**Workflow:** [Verify M2 run #41157387340](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41157387340)  
**Run result:** Initial run SUCCESS, followed by the expanded successful run #41158772090. Both results are preserved.  
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

## Expanded adversarial verification, 2026-10-31

**Run:** [Verify M2 #41158772090](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41158772090), completed successfully.  
**Verified branch revision:** `f7308216f1bdab67a860114b1613f81402c6308d`  
**PR merge ref checked out by runner:** `69b8f1df50543af977d475692e8029edce9661bb`  
**Result:** PASS. No failing or skipped tests.

| Check | Executed result |
| --- | --- |
| .NET Release build | PASS |
| Unit | 22/22 |
| API contracts | 12/12 |
| Fresh PostgreSQL integration | 19/19 |
| Repeated seeded PostgreSQL integration | 19/19 |
| Frontend unit | 6/6 |
| Chromium/Firefox/WebKit | 24/24 |
| Compose, dependency outage/recovery, organization bootstrap and M1 session replay | PASS |
| Three running-image HIGH/CRITICAL vulnerability scans and source-revision labels | PASS |
| Evidence artifact upload and disposable runner cleanup | PASS |

The new `OrganizationAdversarialTests` and `OrganizationAdversarialHttpTests` cover separate database-context concurrent Administrator demotion, last-active-Administrator survival, live membership role updates and suspension, active organization suspension, two independent selections for one account, cross-organization read and mutation denials, every non-Administrator role's API denials, invalid role and CSRF/origin requests, PostgreSQL invalid role/status/version/slug and duplicate membership rejection, and negative bootstrap cases.

### Earlier expanded-run failures and corrections

- [Run #41158495671](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41158495671) failed .NET compilation because xUnit analyzer xUnit2031 disallows filtering via `Where` before `Assert.Single`. Replaced with the predicate overload of `Assert.Single` without changing the concurrency assertion.
- [Run #41158605165](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41158605165) passed 17/19 fresh integration cases, including the new database/concurrency cases, but two HTTP tests failed during fixture setup. The database persists normalized emails in uppercase; those tests queried raw lowercase addresses. Replaced the lookups with `IdentityStore.NormalizeEmail`, preserving the actual authorization assertions.
- [Run #41158772090](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41158772090) passed all expanded suites. No production permission checks were relaxed to make verification pass.

**G2 gate:** The expanded high-priority adversarial cases now have passing evidence. The full 25-case G2 matrix still has some partial items, notably explicit retained M1-to-M2 upgrade rehearsal, full invalid FK/disabled bootstrap negative cases, specific scoped-database-outage behavior and stale-browser role-revocation behavior. These remain open until separately asserted. No M2 merge or G2 acceptance has occurred.

## Remaining G2-specific evidence

The executed suite does not cover every negative case required by the [G2 verification plan](M2_VERIFICATION_PLAN.md). No check may be marked PASS without its own test assertion or recorded runtime evidence.

| G2 check IDs | Status | Required follow-up |
| --- | --- | --- |
| G2-S01, S02 | PARTIAL | Add explicit existing M1-account/session-to-M2 upgrade and repeat-migration preservation fixtures |
| G2-S03 | PARTIAL | Invalid role/status/version/slug and duplicate membership rejected; explicit invalid FK and duplicate-slug SQL assertions remain |
| G2-S04 | PARTIAL | Unknown account and duplicate slug rejection passed; disabled-account and CLI opt-in negative cases remain |
| G2-S05 | PASS | All five declared roles evaluated by unit policy assertions |
| G2-S06 | PARTIAL | Full disabled account and revoked-cookie matrix on the new M2 routes |
| G2-S07, S08 | PASS | No selection, successful selection and differing roles across two organizations |
| G2-S09 | PASS | Two distinct authenticated sessions for one account select A and B independently and keep distinct roles |
| G2-S10 | PARTIAL | Foreign organization ID denial on every read and mutation verb |
| G2-S11 | PASS | Existing B administrator user ID is not disclosed through A membership GET, and B route mutations are denied |
| G2-S12 | PASS | HTTP role matrix denies Analyst, Manager, Support and Auditor list/detail/profile change/grant/remove |
| G2-S13 | PASS | Administrator of A denied foreign organization B by HTTP |
| G2-S14 | PARTIAL | Live role change and suspension/selection invalidation verified; concurrent grant revocation and browser refresh cases remain |
| G2-S15 | PASS | Active selection, existing scoped reads, membership list and profile administration denied after organization suspension |
| G2-S16 | PARTIAL | Sole-admin self-disable and competing Administrator demotions pass; concurrent account-disable versus removal remains |
| G2-S17 | PASS | Two independent EF transactions race Administrator demotions; exactly one succeeds and a final active Administrator survives |
| G2-S18 | PARTIAL | Stale version conflict checked; duplicate-create collision needs direct negative assertion |
| G2-S19 | PARTIAL | Invalid role, absent CSRF and foreign Origin denied; malformed bodies and broader resource-validation matrix remain |
| G2-S20 | PARTIAL | M0/M1 database outage/recovery tested; specifically scoped organization access during outage untested |
| G2-S21 | PASS | Accepted M1 lifecycle regressions executed |
| G2-S22 | PARTIAL | Browser selection/reload/clear and administrator controls verified; cross-role switch and stale-revocation UI remain untested |
| G2-S23 | PARTIAL | Existing foreign user ID and other-tenant route denial checked; log redaction matrix incomplete |
| G2-S24 | PASS | Public synthetic M0 endpoint and M1 browser regressions passed |
| G2-S25 | PARTIAL | Running image scans and local Compose passed; full deployment/security review remains separate |

Expanded G2 tests were implemented and passed in [run #41158772090](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41158772090). The next work is the remaining targeted G2 cases and separate gate acceptance. A successful workflow is evidence for the assertions it executed, not proof of unimplemented adversarial cases. G2 remains open until the mandatory matrix is complete. Any later acceptance requires a separate explicit decision.

## Verification scope and workflow policy

GitHub Actions verification was expressly requested for M2 on 2026-10-31. The repository includes a dedicated `verify-m2.yml` workflow with M0/M1 regression coverage, and the M1 workflow is manual-only on this feature branch to avoid duplicate PR jobs. The documentation-only reconciliation commit uses `[skip ci]` so the passing source revision remains the last executed verification. M0/G0 and M1/G1 accepted status remains unchanged. No M3 implementation was started.
