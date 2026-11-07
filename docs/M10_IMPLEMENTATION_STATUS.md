# M10 Event Timeline and Projection Rebuild: Implementation Register

**As of:** 2026-11-05  
**Branch:** `feat/m10-timeline-projections` (originally branched from accepted G9 SHA `b4f7b93`; now reconciled with the accepted G16 and PR #27 documentation baseline `25e89026737063294a32bbd081ced400d19c6b3d` by merge `82c5d6eec34469ac1813d99270b4108788c9435d`). [Integration reconciliation](M10_M16_SOURCE_RECONCILIATION.md) records the merge and remaining verification.  
**State (updated 2026-11-07):** PR #23 merged the original M10 source; technical verification and independent evidence review apply only to frozen runtime `3e81b88`. The fourteen M10 local policies are now approved with L07/L14 revisions and conditional L06. Corrected source is committed separately at runtime candidate `3ef7a50859b5f023aa1457b373728888f6d1cba9`, and new-source verification is NOT EXECUTED. G10 acceptance pending. Earlier source-candidate and pre-approval subsections are retained as history.  
**Planning:** [PR #22](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/22) is an independent open documentation PR; its M10 architecture, work packages and G10 scenario specification remain proposed, not accepted decisions.

## Current correction-authority checkpoint, 2026-11-07

[New-source implementation](M10_G10_CORRECTION_AUTHORITY_IMPLEMENTATION.md) is committed, with thirteen migrations, signed corrections, central M16 audit evidence and restricted direct SQL correction grants. Build and database verification are unexecuted. The previously reviewed 23 PASS matrix remains historical evidence only for `3e81b88`. S01 implementation conformance and S25 acceptance remain pending; no merge, release or deployment is authorized.

## Updated G10 review state, 2026-11-06

The [independent evidence review](M10_G10_INDEPENDENT_EVIDENCE_REVIEW.md) reconciles all seven G10 attempts, confirms the sanitized package's 500/500 matching manifest entries, and supports the technical 23 PASS dispositions including the five later focused coverage supplements. S01 remains blocked on 14 proposed policies, particularly L07 M16 audit compatibility and L14 database correction grant scope; S25 formal acceptance remains outstanding. The corrected verification branch is not merged. **G10 acceptance pending.** Historical pre-execution statements below are retained for provenance and do not describe the current verification status.

## Implemented candidate paths

| Component | Source | Intended behavior |
| --- | --- | --- |
| Deterministic ordering and reducer | `src/HarborOps.Domain/Tracking/TrackingTimelinePolicy.cs` | UTC occurrence then receipt/source/event key/ID tie-break, explicit Conflict and correction handling, stable SHA-256 input fingerprint; unknown ETA |
| Additive M10 migration | `src/HarborOps.Infrastructure/Persistence/Migrations/20261105180000_ShipmentTrackingProjections.cs` | Immutable indexed source entries, monotonic input revisions, scoped projections, correction heads, append-only correction and replay records, composite effect FKs and guarded downgrade |
| Timeline/rebuild store | `src/HarborOps.Infrastructure/Shipments/ShipmentTimelineStore.cs` | Indexed SQL keyset GET, input-versioned cached projections with explicit stale/unknown state, full historical streaming only during rebuild, serializable locking and durable operation replay |
| Scoped HTTP routes | `src/HarborOps.Api/Shipments/ShipmentTimelineEndpoints.cs` and `Program.cs` | Two GET read routes with encrypted scope-bound snapshot/keyset tokens; Manager-only, CSRF-protected, rate-limited and idempotent correction and rebuild routes with live session verification |
| Optional projection updater | `src/HarborOps.Api/Shipments/TrackingProjectionHostedService.cs` | Disabled by default; dedicated restricted PostgreSQL identity with fail-closed startup, bounded work scans, policy/correction/drift reconciliation |
| Shipment tracking UI | `web/src/shipments/ShipmentTrackingTimeline.tsx`, `ShipmentWorkspace.tsx` | Authorized, read-only observed tracking status and paginated provenance; manual workflow remains separate |
| G10 local resources | `compose.g10.verify.yaml`, `scripts/verify-g10-local.sh`, `scripts/configure-m10-projector-db.sql` | Isolated E:-backed build, tests, scoped role privilege checks, browser fixtures and three-engine functional coverage; not a complete G10 campaign |
| Unit/regression tests | `tests/HarborOps.UnitTests/TrackingTimelinePolicyTests.cs`, `tests/HarborOps.IntegrationTests/TrackingProjectionG10Tests.cs` | Reducer order, replay, conflicts, void/supersede, digest, real PostgreSQL effect-to-timeline, rebuild, unchanged manual workflow and cross-tenant denials |
| Existing migration regressions | `MigrationSnapshotTests.cs`, `TrackingInboxMigrationTests.cs`, `TrackingInboxAuditSqlTests.cs`, `TrackingWorkerMigrationTests.cs`, `ReferenceHistoricalUpgradeTests.cs` | Expect additive M10 migration after accepted M9 and leave historical intermediate counts intact |

## Source boundaries

M8 receipts/events and M9 receipt-unique effects, lease fencing, work states and 10,000 unresolved-item guard are untouched. Only M9-resolved equipment observations become shipment timeline input. `GTIN`/`GTOT` yield per-container `EquipmentIn`/`EquipmentOut`, same-container `Conflict`, or mixed-container `Mixed`; no verified ETA, voyage inference or automatic M5 `WorkflowStatus` transition is claimed. Unresolved opaque ARRI/DEPA transport events remain parked, with the **mandatory sustained transport-event hold unchanged**. Background materialization does not start automatically. Bounded authorized GETs use the stored input revision and indexed timeline entries; when the materialization revision is stale, GET reports an unknown/pending derived state rather than recalculating the entire event history.

## Second correctness audit, source-locked

The [second source correctness audit](M10_SECOND_CORRECTNESS_AUDIT.md), against `784689f483c64d19495ee8e9c1047464aaef8f32`, identified a critical malformed migration and further projector privilege, fairness and query-scaling blockers. The [follow-up remediation](M10_SECOND_AUDIT_REMEDIATION.md) records corresponding unverified source corrections. Previous C01-C11 remediation mapping remains historical **source-only work**, not verified completion. **PR #23 must remain draft and unmerged.** No runtime testing or final G10 review has occurred.

## Correctness audit

A [source-locked correctness audit](M10_SOURCE_CORRECTNESS_AUDIT.md) of runtime revision `83d78bb7b271b96d5086044801fc503f07d8a417` identified blocking integrity, scalability and projection-reconciliation findings. [Remediation register](M10_CORRECTNESS_REMEDIATION.md) maps the eleven findings to committed source corrections. **Merge readiness remains blocked** pending executed source-locked verification and independent review. The audit is a static review and records no runtime test pass.

## Known technical and acceptance blockers

- **No execution:** .NET SDK, Docker and a reachable E:-backed PostgreSQL environment are unavailable from this GitHub-only session. No tests, migrations, browsers, security scans, failure injection or generated G10 source-locked evidence have been run here. New test files are authored, not passed.
- **G10-S01 policy approval pending:** Deterministic tie/conflict policy, correction semantics/authorization, exact projection revision/consistency and transport-map/backlog policy require explicit review. Implemented source is a candidate; the code does not approve these choices.
- **Scale and operability:** The source candidate removes the 10,000-resolved-event rejection and reduces full history through a streamed reducer with bounded keyset page memory plus per-container observation state. Bounded GETs use a materialized input version and indexed timeline pages; full rebuilds remain O(retained events) and require verified performance limits. The projector is disabled by default and requires the distinct restricted `harborops_projector` database role and separately supplied credentials.
- **Evidence:** Source-locked compile, unit/contract/integration, fresh/populated migration, role/session, direct SQL immutable corrections, race/failure windows, three-browser functionality, security mutations/scans, independent raw-evidence review, G10 acceptance and separate merge authorization are pending. The local helper does not cover all gates by itself.
- **M16 integration:** G16 was accepted and merged through PR #25 and documented through PR #26. M10 now follows accepted migration order: M16 AuditEntries, M9 TrackingWorker, then M10 ShipmentTrackingProjections (12 in total). The accepted M16 EF snapshot and role provisioner are preserved; M10 adds narrowly scoped API correction/projection grants. Fresh/populated upgrades, exact restricted runtime roles, effect-trigger concurrency and G10 plus G16 regressions remain **unexecuted**. [Reconciliation register](M10_M16_SOURCE_RECONCILIATION.md).
- **Release:** PR #21 documentation reconciliation and final M24-03 documentation/source checks remain open in the separate planning stream. No public hosting or production deployment authorized.

## Verification status

| Check | Status |
| --- | --- |
| Source files committed and remotely visible | Completed |
| New unit/integration test execution | NOT EXECUTED |
| `node scripts/check-planning-docs.mjs`, graph and script tests on this branch | NOT EXECUTED |
| Migration fresh/upgrade and schema checks | NOT EXECUTED |
| Browser and outage/security campaigns | NOT EXECUTED |
| G10 independent evidence review | NOT EXECUTED |
| C01-C11 code-level remediation mapping | Committed; runtime proof pending |
| Explicit G10 acceptance and merge | PENDING |

**Next action:** Run a dedicated source-locked local G10 campaign on E:, remediate any compile/runtime defects without overwriting original evidence, resolve M10-local decisions, and obtain independent verification and explicit acceptance. No milestone gate is closed by the presence of this implementation register.

## Superseding policy update, 2026-11-07

M10-L01 to M10-L14 policy selection is approved, with L07 requiring atomic M16 correction audit, L14 requiring constrained database correction authority with unforgeable actor/session provenance, and L06 conditional on the L14 guarantee. The [G10 L07/L14 remediation plan](M10_G10_L07_L14_REMEDIATION_PLAN.md) records exact requirements, proposed execution stages and negative tests. Prior statements above about pending policy selection describe earlier historical status, not the current decision.

**Implementation and new-source tests remain pending.** Earlier G10 A1-A7 raw evidence independently supports the frozen source `3e81b884b6a4cc782f84c58510793666786c5796`, not the as-yet-unimplemented security changes. Preserve earlier evidence, run focused source-locked tests on E:, then independently review the supplemental package. G10 acceptance, merging, release and deployment remain pending or unauthorized as applicable.

## Correction authority remediation source update, 2026-11-07

The approved L07/L14 remediation code is now committed on `fix/m10-g10-l07-l14-remediation`. It adds the thirteenth SQL-only migration, signed and server-authenticated correction operation, migration-owned M16 audit insert, protection against direct correction INSERT and forged correction replay, and targeted regression tests. The original frozen-source G10 evidence still applies only to `3e81b88`.

See [implementation and verification boundary](M10_G10_CORRECTION_AUTHORITY_IMPLEMENTATION.md). Source inspection and static consistency checks have occurred, but .NET compilation, PostgreSQL migration execution, source-locked targeted integration, failure injection and browser runs have **not** been executed in this environment. Do not mark new-source verification PASS. G10 acceptance and merge remain pending.
