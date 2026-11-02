# M6 Shipment Assignment Implementation Status

**As of:** 2026-11-02
**Milestone:** M6-01 through M6-04; G6
**Branch:** `feat/m6-container-voyage-assignments`
**Accepted base:** `main` at `344c6eb96a9af600b5843cc97c56a8449174bad8`, G0-G5 accepted
**Implementation state:** Assignment domain, additive PostgreSQL migration, API, React workspace and initial tests authored and committed. Runtime build, PostgreSQL migration, three-browser execution, complete G6 verification and acceptance are **NOT RUN**. No M6 integration into `main`.
**Decision state:** [M6 local contract](M6_LOCAL_CONTRACT_DECISION.md) proposed, acceptance pending; do not mark G6-S01 complete.

## Implemented source

| Area | Source |
| --- | --- |
| Domain rules | `src/HarborOps.Domain/Assignments/AssignmentPolicy.cs`: explicit UTC and half-open interval validation, overlap and route/window compatibility |
| Entities | `src/HarborOps.Infrastructure/Assignments/AssignmentRecord.cs`: tenant-owned container/voyage association history and closure provenance |
| SQL | `src/HarborOps.Infrastructure/Persistence/Migrations/20261102080000_ShipmentAssignments.cs`: additive tables, composite FKs, GiST exclusions, immutability and parent guards |
| EF | `HarborOpsDbContext.cs` and current `HarborOpsDbContextModelSnapshot.cs`: matching entity maps, required unique parent keys, associations and indexes |
| Service | `src/HarborOps.Infrastructure/Assignments/AssignmentStore.cs`: scoped list/detail, versioned create/closure, live session and role revalidation, conflict mapping |
| API | `src/HarborOps.Api/Assignments/AssignmentEndpoints.cs`: eight authenticated routes, strict JSON and CSRF, stable error responses |
| Authorization | `OrganizationPolicy.cs`: typed view permission for Analyst, Manager, Auditor; manage permission for Analyst and Manager |
| UI | `web/src/assignments/` and shipment workspace: container/voyage selection, interval entry, history, conflict notices and close action |

## Authored verification source

- `tests/HarborOps.UnitTests/AssignmentPolicyTests.cs`: UTC, interval boundaries, route matching, permissions and payload parsing.
- `tests/HarborOps.IntegrationTests/AssignmentHttpTests.cs`: create/list, overlap, archived parent, close/version, tenant and input denial.
- `AssignmentSqlTests.cs`: independent GiST overlap, immutable row, SQL archive guard and cross-tenant FK.
- `AssignmentMigrationTests.cs`: accepted M5 historical state, upgrade to M6, isolated rollback and reapply.
- `AssignmentSecurityTests.cs`: Auditor view-only, Manager/Analyst writes, revocation after membership downgrade and concurrent container allocation.
- `AssignmentConcurrencyTests.cs`: parent archive/cancellation versus allocation.
- `AssignmentInterruptionTests.cs`: PostgreSQL advisory-lock barrier, terminated backend before commit, rollback and successful retry.
- `tests/browser/specs/assignment.spec.mjs`: Chromium/Firefox/WebKit source for allocation and tenant-switch behaviors.
- Existing full-latest-migration expectations updated to seven migrations; historical M5 tests are pinned to their actual M5 version rather than being silently reinterpreted as M6.
- `scripts/verify-m6-local.sh`, related G6 stage/browser/fixture/recovery scripts, schema/source validators, snapshot checkpoint, manifest/archive generation, and evidence mapping were authored. They must be executed on E: before any G6 claims.

## Verification limits and outstanding work

**No successful local build or runtime test result exists for this revision.** The repository connector permits source changes but does not execute .NET 10, Docker Desktop, PostgreSQL 16, Windows E:-backed fixtures, Playwright browsers or the G6 local pipeline. Source authorship is not proof of passing the gate.

Before G6 acceptance, run the full G6-S01 through G6-S25 matrix on a pinned SHA, inspect raw assertions and actual SQL migrations, and fix every failure. The current evidence validator intentionally reports several uncovered gates as incomplete. The twelve-rule M6 contract has not been approved. Do not bypass or weaken the validator to report success.

Known items requiring adversarial execution or further source strengthening:
1. PostgreSQL `btree_gist` provisioning and GiST exclusion behavior under two simultaneous real connections.
2. Direct-SQL parent change and assignment insertion lock interleavings, including deadlock retry outcomes, unchanged M4/M5 behavior and incomplete transaction rollback.
3. End-to-end browser catalog fixtures, all role/tenant states, safe labels for Auditor and no stale state after session/organization changes.
4. Full SQL/EF migration and model snapshot comparison; historical upgrade/rollback; M0-M5 accepted regressions.
5. G6 interruption/recovery subcases beyond the initial backend-termination test, actual Q1 snapshot compatibility or documented NOT APPLICABLE.
6. Independent security mutation proofs against tenant scoping, exclusion constraints, parent guards and post-lock authorization.
7. Full G6 evidence mapping, raw test logs, image scans, E:/C: storage accounting and teardown integrity.

## Governance

No GitHub Actions may be dispatched or triggered. Use `[skip ci]` for commits. Build/test/container resources and archives belong on E:, with isolated `ho-verify-g6` names and strict cleanup of only owned disposable resources. Never consume persistent C: storage, rewrite M0-M5 migrations, discard historical evidence, force-push or merge the feature branch into `main` before explicit G6 acceptance and integration approval. Formal accessibility testing remains at M24-04.

**Exit:** Implementation source authored, runtime verification pending, G6 open, acceptance pending. These statements are not an assertion of full end-to-end acceptance.
