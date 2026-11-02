# M6 Shipment Assignment Implementation Status

**As of:** 2026-11-02
**Milestone:** M6-01 through M6-04; G6
**Branch:** `feat/m6-container-voyage-assignments`
**Accepted base:** `main` at `344c6eb96a9af600b5843cc97c56a8449174bad8`, G0-G5 accepted
**Implementation state:** Assignment domain, additive PostgreSQL migration, API, React workspace, expanded adversarial tests, security mutation runner, historical snapshot restore and extracted-archive verification authored and committed. Runtime build, PostgreSQL migration, three-browser execution, complete G6 verification and acceptance are **NOT RUN**. No M6 integration into `main`.
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
- `AssignmentAdditionalFailureTests.cs`: after-write cancellation, command timeout and lost committed response, with explicit retry assertions.
- `AssignmentQueuedSecurityTests.cs`: lock-observed assignment read/write requests with live session, account, membership, role, organization and selected-organization changes.
- `AssignmentPermissionMatrixTests.cs`: five-role, two-resource, list/detail/create/close authorization matrix.
- `AssignmentHttpValidationMatrixTests.cs`: malformed JSON, wrong MIME, duplicate/unknown fields, CSRF/Origin, invalid closure, no-store/correlation and unchanged state.
- `AssignmentVoyageMatrixTests.cs`: shared voyage, sequential voyage reassignment, reverse route, disjoint planning and direct SQL parent lifecycle constraints.
- `AssignmentSqlTests.cs`: real alternate-tenant parent fixtures and separate behavioral exclusion and archive guard mutation controls.
- `tests/browser/specs/assignment.spec.mjs`: Chromium/Firefox/WebKit source for allocation and tenant-switch behaviors.
- Existing full-latest-migration expectations updated to seven migrations; historical M5 tests are pinned to their actual M5 version rather than being silently reinterpreted as M6.
- `scripts/verify-m6-local.sh`, stage/browser/fixture/recovery scripts, schema/source validators, snapshot restore, mutation controls, manifest/re-extracted archive integrity, and scenario evidence mapping are authored. They must execute on E: before any G6 claims.

## Correctness-remediation register

The M6 feature branch includes successive correctness remediation rounds. These are **authored source and test changes, not verified runtime passes**:

| Audit concern | Source remediation and evidence requirement |
| --- | --- |
| G6 gate hard-coded incomplete | `scripts/assert-g6-evidence.mjs` now maps 25 scenarios to actual TRX, Playwright reports, stage proofs, executed security mutants and snapshot results. G6-S01 remains incomplete until the contract is approved. |
| Composite FK proof | `AssignmentSqlTests.CompositeForeignKeysRejectCrossTenantParentsWithoutValidationTrigger` disables only the custom validation trigger transactionally, demands PostgreSQL `23503` and the exact FK constraint name, then rolls back the test transaction. |
| Browser inventory | Ten M6 specs per engine cover Analyst allocation and adjacent reassignment, organization change, Auditor, Support, visibility return, logout, independent duplicate conflicts, delayed responses, membership downgrade and switching between different shipments. Separate per-engine container fixtures prevent overlap interference. |
| Security mutation evidence | `scripts/run-g6-mutations.mjs` exports disposable source copies, builds each variant and requires baseline PASS plus named failures for tenant read, post-lock authorization, exclusion and parent guard mutations. |
| Interruption proof | `AssignmentAdditionalFailureTests.cs` adds after-write request cancellation, command timeout and post-commit response loss to the existing real connection-termination proof. |
| ZIP integrity | `scripts/finalize-g6-evidence.mjs` re-extracts the ZIP and checks every path, SHA-256 hash and byte length, with scoped temporary cleanup. |
| Browser evidence inventory | The G6 checker rejects missing/empty tests, skipped/failed attempts and inconsistent engine inventories. |
| Deterministic archive race | `AssignmentConcurrencyTests.QueuedAllocationAndParentArchiveCannotCommitInconsistentState` observes a PostgreSQL lock before releasing the parent transaction and validates the committed invariant. |
| Tab visibility | Allocation lists refresh on visibility return without wiping assignment input or the unsaved M5 shipment draft. |
| Contextual voyage display | The shipment UI intersects container/voyage allocation intervals and labels the result as administrative context, not loading evidence. |
| Second-round isolation | Shipment allocation panel is keyed by organization and shipment; browser regression holds a source shipment response while switching to another shipment. |
| FK provenance | Foreign-tenant shipment, container and voyage records are real, with each probe designed to violate exactly one named composite key. |
| Mutation effectiveness | G6 mutation runner targets isolated SQL overlap and parent archival behavior tests, not combined schema-count assertions. |
| Cancellation settlement | After request cancellation, PostgreSQL backend transaction state must settle while the barrier is still held, before durable row assertions. |
| Outage fixture | Dedicated G6-OUTAGE-BOX prevents reuse of a previously allocated M4 or browser container. |
| Snapshot resource use | Historical backup SHA-256 calculation streams rather than reading the whole backup into memory. |
| Fixture evidence | G6 fixture generator verifies two organizations, five role memberships, matching M4 voyages, past bounded and future open allocations, and idempotent replay; validator consumes the structured report. |
| HTTP and voyage matrices | Named executable tests verify five-role permissions, malformed request handling, shared voyage/adjacent reassignment and direct SQL terminal guards. |
| Q1 snapshot | `scripts/check-g6-snapshot.mjs` restores and migrates a representative E:-backed backup in a disposable PostgreSQL container, or records justified NOT APPLICABLE when none is present. |

## Runtime-discovered defects (local execution before the full G6 run)

Local execution found the following defects that source review had not. Each was corrected on the feature branch with `[skip ci]` commits and re-verified in isolation before the full G6 run. These are development rehearsal results, not gate evidence; the gate run is recorded separately once it completes.

| Finding | Root cause | Correction |
| --- | --- | --- |
| Release build failed (EF1003) | The assignment list cursor query concatenated a table name into `FromSqlRaw` | Per-table `FromSqlInterpolated` queries in `AssignmentStore` |
| Integration test projects did not compile | An invalid `DBNull` pattern and a shadowed local named `actor` | Corrected pattern and renamed local |
| Accepted M5 test `CompositePortsAndImmutableFieldsRejectIndependentSqlMutations` failed | The M6 parent guard triggers sorted before the accepted `shipments_validate` trigger and replaced its delete rejection message | Guards renamed `zz_m6_guard_*` so the accepted validators run first; the M5 assertion is unchanged |
| `QueuedAllocationAndParentArchiveCannotCommitInconsistentState` errored in cleanup | The test rolled back a transaction it had already committed | A committed flag guards the rollback |
| Fixture generator rejected container identifiers | Synthetic identifier suffixes were longer than the 16 character limit | Short engine tags in the identifiers |
| Five assignment browser specs failed in Chromium and Firefox, five in WebKit | Direct API calls ran before the server-side organization selection and the controlled select value were visible; an unawaited promise was concatenated into a URL; a label match was ambiguous; the CSRF origin omitted the port | `selectOrganization` helper that waits for the PUT and a non-empty value, awaited lookup, exact label match, correct origin |
| Outage checks failed with ENOBUFS | The API log credential scan exceeded the default process buffer after the browser stage | Larger buffer in the outage and recovery scripts |
| Reference and maritime outage checks failed with HTTP 429 | Back-to-back checks shared account logins and exhausted the login limiter | The runner restarts the API before each outage and recovery check |
| Mutation runner could not extract its source export | GNU tar read the drive letter as a host and a backslash before `t` as an escape | Cache-relative forward-slash paths |
| Leftover disposable database after a failed build | The cleanup trap did not run under errexit | EXIT trap in the .NET stage |

Harness changes made in the same period: a warnings-as-errors compile gate and the disposable-database suites now run before the Compose stages, each stage records its duration, ownership checks cover every `ho-g6-` container, mutation failure messages and a ZIP SHA-256 sidecar are retained, and a read-only restore of a retained historical Docker volume is supported by the Q1 snapshot check. Development-only rehearsal scripts (`scripts/dev-m6-targeted.sh`, `scripts/dev-m6-browsers.sh`) write outside the evidence directory and never count as gate evidence.

## Verification limits and outstanding work

**No successful local build or runtime test result exists for this revision.** The repository connector permits source changes but does not execute .NET 10, Docker Desktop, PostgreSQL 16, Windows E:-backed fixtures, Playwright browsers or the G6 local pipeline. Source authorship is not proof of passing the gate.

Before G6 acceptance, run the full G6-S01 through G6-S25 matrix on a pinned SHA, inspect raw assertions and actual SQL migrations, and fix every failure. The current evidence validator intentionally reports several uncovered gates as incomplete. The twelve-rule M6 contract has not been approved. Do not bypass or weaken the validator to report success.

Known items requiring adversarial execution or further source strengthening:
1. PostgreSQL `btree_gist` provisioning and GiST exclusion behavior under two simultaneous real connections.
2. Direct-SQL parent change and assignment insertion lock interleavings, including deadlock retry outcomes, unchanged M4/M5 behavior and incomplete transaction rollback.
3. Execute and inspect the ten M6 browser scenarios on Chromium, Firefox and WebKit, including role downgrade, shipment identity changes, held responses and contextual data isolation; expand any uncovered mandatory journey before acceptance.
4. Full SQL/EF migration and model snapshot comparison; historical upgrade/rollback; M0-M5 accepted regressions.
5. Execute and independently review the expanded cancellation/timeout/interruption/lost-response tests and the Q1 historical snapshot, or its documented NOT APPLICABLE disposition.
6. Execute four isolated security mutations and prove their named tests fail while the unmodified control passes.
7. Full G6 evidence mapping, raw test logs, image scans, E:/C: storage accounting and teardown integrity.

## Governance

No GitHub Actions may be dispatched or triggered. Use `[skip ci]` for commits. Build/test/container resources and archives belong on E:, with isolated `ho-verify-g6` names and strict cleanup of only owned disposable resources. Never consume persistent C: storage, rewrite M0-M5 migrations, discard historical evidence, force-push or merge the feature branch into `main` before explicit G6 acceptance and integration approval. Formal accessibility testing remains at M24-04.

**Exit:** Implementation source authored, runtime verification pending, G6 open, acceptance pending. These statements are not an assertion of full end-to-end acceptance.
