# M5/G5 Correctness Audit

**Audit baseline:** `7c85c3271e481fd460d533f08940c6bd4211d4cc`  
**Date:** 2026-11-01  
**Scope:** M5 shipment domain, PostgreSQL/EF migration, API, frontend, integration/browser test source, fixture preparers, and G5 runner/evidence checker.  
**Disposition:** G5 BLOCKED. No acceptance or merge. Findings are source-supported; no .NET SDK, PostgreSQL, Docker stack, or browser execution was available in the review environment.

## Executive assessment

The domain/schema/API/UI implementation represents substantive M5 work and is appropriately confined to a feature branch. The source-level review does not establish that the code compiles or that its migrations, triggers, endpoint transactions, role races, and browser flows operate correctly. More importantly, the G5 implementation does **not** enforce the mandatory 25-case verification matrix: several reported scenario outcomes reuse one test whose assertions cover only a fraction of the contract, and the operational evidence contract is not implemented end to end.

These findings must be remediated before running G5 for acceptance. Preserve accepted M0-M4 behavior, do not alter applied migrations, and do not run GitHub Actions without separate authorization.

## Findings

| ID | Severity | Area | Result |
| --- | --- | --- | --- |
| G5-A01 | HIGH | Scenario evidence checker | Several scenario mappings are weak or vacuous and permit false evidence-present status |
| G5-A02 | HIGH | Runner resource isolation | G5 reuses G2/G4 Compose project and fixed container names instead of a separate owned G5 project |
| G5-A03 | HIGH | Operational gate | No G5 archive/hash/verified teardown stage and insufficient checks for full G5-S25 evidence |
| G5-A04 | HIGH | Adversarial coverage | Mandatory role, authentication, tenant, SQL, revocation and conflict subcases are absent |
| G5-A05 | HIGH | Browser coverage | Three positive/limited browser specs cannot establish G5-S22/S23 workflows or races |
| G5-A06 | MEDIUM | Historical migration | M4-to-M5 upgrade fixture lacks voyage and vessel history required by G5-S05 |
| G5-A07 | MEDIUM | Deterministic fixtures | Fixture setup lacks seeded transitions, archived shipments and cross-tenant identifier controls |
| G5-A08 | MEDIUM | Evidence match semantics | Substring-based TRX test matching does not enforce every named theory case or an exact fresh/repeat case count |
| M5-P01 | MEDIUM | Port catalog recovery | A transient port catalog failure leaves creation disabled until workspace remount |
| M5-P02 | MEDIUM | Conflict UX | Conflict response triggers reload, immediately clearing the error and entered changes |
| M5-P03 | MEDIUM | Auditor detail | Shipment detail only shows port UUIDs, not the scoped display references promised in M5 architecture |

### G5-A01: Insufficient scenario-to-evidence mapping

**Source:** `scripts/assert-g5-evidence.mjs:93-125`. `S07` tests the constant `stages.includes("m5-fixtures")`; the stage ledger independently requires a successful stage, but this mapping never verifies its detailed fixture assertions. `S13` requires only a queued session-revocation test, `S20` only one same-version status race, and `S22/S23` only three UI spec titles. `S15/S16/S17/S18` reuse existing broad lifecycle tests without checking their required separate invariants.

**Failure path:** All currently mapped tests pass, but a required distinct subcase (for example downgrade while a read waits, restore-vs-edit or repeated retry) remains unexecuted; its G5 scenario still reports evidence present.

**Remediation:** Define an explicit per-subcase executable matrix. Match exact test identities, parameter rows, phase (fresh/repeat), and browser engine. Require every named test/subcase and reject missing, skipped, duplicate or failed records. Make fixture and operational scenarios evidence-driven.

### G5-A02: G5 is not a separate verification project

**Sources:** `scripts/verify-m5-local.sh:54-75,98-103,119-148`; `scripts/verify-m2-local.sh:23-24,57-99`; `scripts/run-m3-browsers-local.sh`.

Despite a separate `E:\HarborOps-Verification\m5` root, the runner and imported stages use `ho-verify`, `ho-g2-pg`, `ho-g2-sdk`, and accepted G2 stage code. The initial existence guard avoids some collisions, but a separate G5 project is not created or owned independently. The reused .NET stage force-removes `ho-g2-pg` by name.

**Failure path:** A concurrent or resumed verification process may conflict over shared resources. Evidence from a later-stage process cannot be unambiguously attributed to an exclusively G5-owned Compose project.

**Remediation:** Supply G5-specific project and container identities end to end, including imported scripts/browser/outage commands, an ownership marker and fail-closed cleanup. Verify concurrent-run and crash/restart safety, without deleting unowned volumes or containers.

### G5-A03: Operational exit cannot prove G5-S25

**Sources:** `scripts/verify-m5-local.sh:64-83,143-150`; `scripts/assert-g5-evidence.mjs:23-35,84-90,121`; `docs/M5_VERIFICATION_PLAN.md:61-75`.

Teardown occurs in the EXIT trap **after** the evidence checker. There is no post-cleanup verification stage, archive creation, SHA-256 manifest/digest, full original console bundle or before/after attributable C: storage check. `S25` maps to `scanned` only. The dependency scanner's separate stage must be retained and independently verified, not inferred from Trivy.

**Failure path:** Image scans pass while leftover containers/volumes or missing hashes, outage artifacts or storage evidence go unnoticed by the scenario gate.

**Remediation:** Split execution and post-teardown verification, verify zero G5-owned resources after teardown, capture stage ledger/complete logs/TRX/Playwright/scan and mutation evidence, generate E:-resident SHA-256 file and archive manifests and record attributable storage measurements. Make each required artifact gate-explicit.

### G5-A04: Missing mandatory adversarial subcases

**Sources:** `tests/HarborOps.IntegrationTests/ShipmentBoundaryTests.cs`; `ShipmentHttpTests.cs`; `docs/M5_VERIFICATION_PLAN.md:23-46`.

The current suite includes 4 shipment HTTP tests, 8 boundary methods (one theory), one migration test, and 4 unit test methods (two theories). It does not cover all mandated G5 assertions. Examples include:

- S06: independent direct-SQL negative and positive controls for cross-tenant existing port, invalid enum, invalid windows, immutable fields, version jumps, history INSERT/UPDATE/DELETE and successful valid update.
- S08/S09/S10: anonymous, missing selection, switched selection in a queued request, and the full six-route foreign-real-UUID matrix.
- S11/S12: real foreign valid parent, same-port denial, and exact five-role, six-route matrix with nonmatching organization and role changes.
- S13: queued reads and writes across account disable, security-version bump, membership removal/downgrade, suspension, session revocation, and no-revocation control.
- S14: malformed, duplicate, over-limit and wrong-content-type bodies, invalid versions and correlated sanitized failures.
- S15-S21: complete external-reference uniqueness across tenants and archived rows, status-neutral edit/history, transition/archive and restore/edit races, replay/duplicate creates, and fault atomicity across timeout/crash/dependency interruptions.

**Remediation:** Add table-driven HTTP, live PostgreSQL, fault and deterministic lock-observed adversarial cases. Use direct SQL controls that distinguish the target invariant from unrelated constraints. Run every test against a fresh and repeated disposable database.

### G5-A05: Browser verification is incomplete

**Sources:** `tests/browser/specs/shipment.spec.mjs:14-73`; `scripts/assert-g5-evidence.mjs:116-118`.

Three browser specs cover an Analyst create/Booked/archive/restore flow, an Auditor read-only check, and a Manager tenant switch. They do not cover Manager CRUD, all terminal transitions, conflict/denial/retry UI, status-history counts, held requests, logout races, simultaneous mutation, degraded/recovered parent catalog, stale pagination and selected item invalidation.

**Remediation:** Add independent deterministic Chromium, Firefox and WebKit test cases for the complete G5-S22/S23 matrix, including network holds and release ordering. Require each case per browser by exact name and result, without a skipped branch.

### G5-A06: Historical M4 fixture lacks accepted voyage/vessel records

**Sources:** `tests/HarborOps.IntegrationTests/ShipmentMigrationTests.cs:23-107`; `docs/M5_VERIFICATION_PLAN.md:27`.

The seeded M4 fixture creates two ports, one container and an authenticated user/selected organization, but no accepted vessel or voyage records. It therefore cannot establish that the M4 voyage graph survives M5 upgrade, rollback and reapply. The checks also do not compare a detailed before/after migration metadata set.

**Remediation:** Seed a valid M4 vessel and voyage with linked ports, multiple container identities, selected session and archived/control rows; assert full before/after identity, version, parent links and migration metadata across upgrade/rollback/reapply.

### G5-A07: Fixture design does not satisfy G5-S07

**Sources:** `scripts/prepare-m5-browser-fixtures.mjs`; `docs/M5_VERIFICATION_PLAN.md:29`.

The fixture preparer creates one Draft shipment per tenant and checks its id/version on repeated calls. It relies on M4 for role setup and does not seed archived shipments, historical transitions, duplicate local codes across tenants or explicit distinct external reference checks.

**Remediation:** Build a complete two-tenant/five-role, status-varied dataset and compare durable before/after row counts, IDs, versions, reference codes and histories after repeated preparation.

### G5-A08: Test-name matching is too permissive

**Source:** `scripts/assert-g5-evidence.mjs:42-59`.

`unit(token)` uses `name.includes(token)` and succeeds if it finds any matching passing test. It does not require the complete set of parameterized cases, or independently prove that the same expected tests ran in both the fresh and repeated integration passes. A single passing theory row could satisfy a scenario with other missing theory rows.

**Remediation:** Use exact `UnitTest` definitions and result IDs/parameters from TRX, with expected per-case counts and source/run/phase identity. Compare fresh/repeat result sets, including zero skips and failures.

### M5-P01: Parent port picker has no retry path

**Source:** `web/src/shipments/ShipmentWorkspace.tsx:108-122,220-244`.

`loadPorts` runs in an effect dependent on organization/permission, not on the Refresh button. On a transient 503 it sets `portError`, which disables Create. `Refresh shipments` only calls `reload`, so it never clears/retries the catalog request.

**Reproduction:** Return 503 once from a permitted `/ports` page, then restore the service and click Refresh shipments. Shipment list reloads but Create remains disabled and the stale port error persists.

**Remediation:** Track separate parent-catalog loading/retry generations. Refresh the parent catalog on command, without blocking the independent shipment list; add a held-response and recovery browser regression.

### M5-P02: Conflict notice disappears on reload

**Source:** `web/src/shipments/ShipmentWorkspace.tsx:187-190`; `reload` clears `error` and `selected`.

For a stale PATCH returning 409, the catch sets a human-readable conflict error and immediately calls `reload()`, which clears it, closes detail and discards entered form values.

**Reproduction:** Open one shipment twice, save from tab A, submit stale version from tab B. Tab B loses the conflict notice and edit context after refresh.

**Remediation:** Preserve a separate non-clearing conflict notice and/or revised item state. Explicitly show the need to review current version, and add a two-tab/browser conflict test.

### M5-P03: Read-only shipment detail lacks port descriptors

**Sources:** `docs/M5_ARCHITECTURE_DESIGN.md:100-125`; `src/HarborOps.Api/Shipments/ShipmentEndpoints.cs:112-126`; `web/src/shipments/ShipmentWorkspace.tsx:283-289`.

GET detail returns the raw shipment record with origin/destination port UUIDs only. The UI displays these UUIDs. Auditors intentionally lack permission to fetch M3 port catalogs, so they cannot resolve the names despite the architecture requiring scoped port display fields.

**Remediation:** Add a shipment-scoped, tenant-projected route descriptor DTO and render the labels in detail without widening Auditor M3 privileges. Test archived-port descriptor behavior.

## Strengths observed in source

- Shipment state transitions and immutable tenant/local identifiers are represented in both the domain policy and additive SQL constraints.
- Creation is scoped to active same-tenant distinct M3 ports, while archived references remain immutable historical links.
- The API rejects unexpected mutation fields, uses existing session/antiforgery mechanisms, requires positive versions, and serializes writes through organization/session locks.
- Creation and workflow status history are linked by PostgreSQL triggers rather than separate nontransactional API writes.
- No M6 assignment or later tracking-derived status was introduced into M5.
- M5 local contract acceptance is explicitly pending; no G5 outcome is represented as accepted.

These are source observations, not evidence of executed correctness.

## Required verification sequence

1. Resolve the documented product UI issues and all G5 checker/runner defects. Strengthen direct SQL, auth/role, race, historical upgrade, fixtures and browser tests until every G5-S01 to G5-S25 subcase has an executable and independently inspectable source-to-result mapping.
2. Run compilation, generated EF migration/snapshot checks and PostgreSQL migration/fault/rollback tests on a dedicated E:-backed G5 environment. Keep accepted M0-M4 migrations unchanged.
3. Execute all .NET unit/contract/integration fresh and repeated cases, all declared Chromium/Firefox/WebKit tests and real outage/recovery scenarios against the final implementation SHA.
4. Archive original console, stage ledger, TRX, browser JSON/traces, SQL assertions, dependency and Trivy results, Docker teardown proofs and attributable storage snapshots with SHA-256 manifests and archive digest on E:.
5. Independently review the raw evidence. G5-S01 stays incomplete until the M5-local contract is explicitly accepted. Only then evaluate G5 acceptance; no merge or GitHub Actions are authorized by this audit.

**Acceptance recommendation:** Do **not** approve G5 or merge M5 on this baseline. The findings require implementation and verification corrections, followed by a new audit and full observed execution.

## Subsequent source remediation (2026-11-01)

The eleven findings above describe the original audited revision. All have since received corrective implementation/test changes on the M5 feature branch. The full disposition and exact source paths are in [M5/G5 remediation status](M5_G5_REMEDIATION_STATUS.md). The original failure analysis is preserved here as history. No .NET/PostgreSQL/browser/Docker verification has executed, the M5-local contract is not accepted, and G5 remains OPEN.
