# G2 Organizations and Role Isolation: Verification Plan

**Status:** Initial [GitHub verification](M2_EXECUTED_VERIFICATION.md) passed. Expanded [run #41158772090](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41158772090) passed the concurrent-administrator, suspension, cross-tenant and two-session tests; some G2 cases remain.  
**Gate:** G2; requires separate acceptance after complete successful evidence  
**Prerequisites:** M2-01, M2-02, M2-03 and M2-04 as defined in the [execution graph](EXECUTION_GRAPH.md)  
**Reference:** [M2 architecture](M2_ARCHITECTURE_DESIGN.md), [implementation plan](M2_IMPLEMENTATION_PLAN.md), [Acceptance Criteria](ACCEPTANCE_CRITERIA.md), [Status](STATUS.md).

## Tooling and execution restriction

GitHub verification of M2 was explicitly requested on 2026-10-31. The repository's dedicated M2 verification workflow can execute the disposable-runner matrix through draft PR #5; the local-only approach remains available as an alternative. [Run #41157387340](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41157387340) provides successful evidence for the configured tests. Unexecuted cases are identified in [M2 results](M2_EXECUTED_VERIFICATION.md).

Use an isolated `ho-verify` Compose project or a dedicated throwaway PostgreSQL database, never the primary `harborops` project or existing local retained volume. Dispose only resources positively identified as part of that isolated project. No compatibility check against a historical user volume is due at G2; quarter-mile checkpoints remain M6, M12, M18 and M24-03. Backups and full restores remain M19. Dedicated accessibility checks remain M24-04.

## Mandatory fixture matrix

Create deterministic synthetic users and organizations under disposable test scope. Do not introduce real personal or enterprise data.

| Fixture | Organization A | Organization B | Expected distinction |
| --- | --- | --- | --- |
| `adminA` | Active Administrator | No membership | Admin for A only |
| `adminA2` | Active Administrator | No membership | Last-admin concurrency partner |
| `adminB` | No membership | Active Administrator | Admin for B only |
| `dualUser` | Active Analyst | Active Manager | Selection changes effective role |
| `supportA` | Active Support | No membership | No implicit business record access |
| `auditorB` | No membership | Active Auditor | Read-only organization metadata |
| `removedA` | Removed Analyst | No membership | Must not select A |
| `suspendedB` | No membership | Suspended Manager | Must not select B |
| `zeroUser` | No membership | No membership | Valid login, no tenant entitlement |

Add a third temporary suspended organization to verify organization-status denial. Create distinct UUIDs and membership records whose IDs can be deliberately substituted across organizations. Seed a dormant third Administrator only if necessary to prove last-admin behavior without modifying the fixture's expected ending state.

## Expanded G2 verification execution

The initial passing M2 workflow covered the core tests. New independent PostgreSQL and HTTP regression fixtures now exercise two-context administrator races, session independence, suspension, invalid database constraints, negative bootstrap, every non-Administrator role, guessed valid foreign IDs and CSRF/origin denials. The new fixtures use random account identifiers per case, a disposable integration database and no shared M1 browser account. A new GitHub run is required to establish results; these assertions are not recorded as passing until their run completes.

## Verification matrix

| ID | Required scenario | Expected observation |
| --- | --- | --- |
| G2-S01 | Clean M0 -> M1 -> M2 migration | No pending migrations; existing baseline and auth tables preserved |
| G2-S02 | Existing M1-seeded database -> M2 and repeat | Additive migration, auth cookies and synthetic baseline continue to work; repeat leaves one copy of each fixture |
| G2-S03 | DB uniqueness/check constraints | Duplicate slug/member, invalid role/status, missing FK, invalid versions rejected |
| G2-S04 | Restricted organization bootstrap | Pre-provisioned active user becomes first Admin atomically; unknown/disabled account, repeated slug and non-opt-in command fail |
| G2-S05 | Role permission unit matrix | Only defined permissions granted; Administrator does not receive implicit shipment access |
| G2-S06 | Anonymous / revoked / disabled account | Scoped endpoints return 401; M1 status and revocation still work |
| G2-S07 | No selection | Authenticated membership listing works; selected-org operations fail 409 |
| G2-S08 | Selection A and B | Active membership required; server stores selection per session; roles reflect selected membership |
| G2-S09 | Cross-session independence | Session A changing to B does not change another session's selected organization |
| G2-S10 | Foreign organization ID | Organization GET/PATCH/list and member GET/PUT/DELETE do not expose or mutate foreign records; 404 |
| G2-S11 | Foreign member ID with selected A | B's member cannot be fetched or edited via A route, regardless of guessed UUID |
| G2-S12 | Wrong-role matrix | Non-Administrator may read allowed organization summary but cannot list, invite or edit memberships or profile |
| G2-S13 | Administrator in A requesting B | No global administrator bypass; foreign ID denied |
| G2-S14 | Membership disabled/removed while selected | Next request denied immediately and selected context invalidated; browser clears stale A data |
| G2-S15 | Organization suspension | Deny selection and all scoped requests, including administrator calls |
| G2-S16 | Last administrator and self-change | Final Admin demotion/removal or M1 account self-disable rejected; Admin cannot edit their own administrator role/state |
| G2-S17 | Competing admin writes | Organization-row serialization and version predicates avoid zero Admins and lost updates |
| G2-S18 | Stale version and duplicate create | 409 conflict; no partial write or silent role escalation |
| G2-S19 | Invalid data and foreign origin | 400 validation, 403 CSRF/origin rejection, bounded response payload |
| G2-S20 | PostgreSQL stop/recovery on disposable stack | Scoped API fails closed with sanitized 503; recovery restores valid access and revoked grants stay denied |
| G2-S21 | Login, logout, password reset, disable-self, and cookie restart | Accepted M1 lifecycle remains unchanged |
| G2-S22 | React login and organization switch | Distinct roles per org, empty state, wrong-role denial, reload and stale-selection recovery |
| G2-S23 | Unauthorized enumeration and log redaction | No foreign usernames/counts/roles, credential/CSRF/cookie values or internal SQL/provider details |
| G2-S24 | M0 synthetic endpoints and current M1 browser flow | No runtime contract regression; M0 remains public synthetic-only |
| G2-S25 | Deployment/dependency review | Local-only profile maintained; no required new paid or hosted service |

## Test-layer placement

- **Pure unit tests:** role grants; slug normalization; transition rules; self-change and last-admin policy; version conflict classifier; denied-by-default checks.
- **EF Core/PostgreSQL tests:** migration and model snapshot; uniqueness; FK/check constraints; concurrent admin writes; active role revocation; selected-session cleanup; M1 data preservation.
- **API integration tests:** M1-authenticated cookie and CSRF flows; per-role membership operations; valid foreign organization/member ID probes; no-selected-org; audit-safe error codes; stale version, suspended org/member; database outage.
- **Browser functional tests:** same-origin login; organization listing/selection; two roles for one user; reload and logout; membership role change in another session; denied/empty/conflict/recovery states. Use Chromium, Firefox and WebKit if locally available. Engine installation must follow E-drive caching rules.
- **Operational regressions:** disposable Compose startup, API restart preserving cookie keys, database interruption/recovery, clean teardown of verified disposable resources only, local image/dependency checks when tooling is available.
- **Documentation consistency:** verify `docs/execution-graph.json` and `docs/EXECUTION_GRAPH.md` consistency; check internal links and milestone status; ensure G1 remains recorded as accepted.

## Local command outline

The exact commands will be finalized when the M2 tests exist. Applicable existing commands run from the E-drive repository root include:

```powershell
# Set local caches and selected disposable test database on E: first.
dotnet restore HarborOps.sln
dotnet build HarborOps.sln -c Release --no-restore
dotnet test tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj -c Release
dotnet test tests/HarborOps.ContractTests/HarborOps.ContractTests.csproj -c Release

# Requires an explicit disposable HARBOROPS_TEST_DB on loopback.
dotnet test tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj -c Release

Set-Location web
npm ci --no-audit --no-fund
npm test
npm run build
Set-Location ..

# Documentation validation only, once implementation and docs are aligned.
node scripts/render-execution-graph.mjs --check
node scripts/check-planning-docs.mjs
```

Use existing `scripts/smoke-compose.sh` and `scripts/check-m0-degraded.sh` only with their required disposable-project confirmation and correct environmental safeguards. M2-specific test harness additions must refuse the primary database/project and must prove the target resources are disposable before destructive operations. Never claim cross-browser, security scan or failure-injection results if a required executable is absent or a command is not run.

## Reporting format

The future `docs/M2_IMPLEMENTATION_VERIFICATION.md` should record:

| Field | Required content |
| --- | --- |
| Source | Exact commit SHA and branch |
| Environment | OS, SDK/Node versions, PostgreSQL version, browser engine versions, cache/storage location |
| Database | Disposable database name/project, starting schema state, fixture version, evidence that personal volumes were untouched |
| Executed checks | ID, exact command or automated test, start/end, observed result and artifact path |
| Result status | PASS / FAIL / BLOCKED / NOT RUN / NOT APPLICABLE, with reason |
| Negative security | Role matrix, foreign identifiers, selected-org mismatch, revocation and concurrency results |
| Regression | M0 public baseline, M1 session lifecycle, Compose recovery, frontend/browser |
| Residual issues | Severity, reproduction, blocking impact and planned correction |
| Gate | Verified with acceptance pending only if required checks actually passed; otherwise open/blocked |

Do not fabricate passing test counts or mark G2 complete based on this document. The final acceptance decision must be explicit and separate from technical verification.

## Current status

M2 feature-branch implementation source and initial regression test files are committed, as recorded in [M2 implementation status](M2_IMPLEMENTATION_STATUS.md). Expanded GitHub suites passed, including concurrent Administrator demotions, suspended organizations, session isolation and API role-denial matrices. The remaining historical-upgrade, invalid-data, authenticated-outage and browser revocation checks have been implemented. Execution and exact evidence are pending. **G2 open.**
