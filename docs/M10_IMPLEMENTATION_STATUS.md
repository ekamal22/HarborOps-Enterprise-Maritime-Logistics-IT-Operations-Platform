# M10 Event Timeline and Projection Rebuild: Implementation Register

**Current integrated baseline (2026-11-07):** G0-G10 and G16 are accepted and integrated into protected `main`. The [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md) records S01 and S25 acceptance; the M10 security remediation merged through [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) at `9b6c801fde4004a4c0021ec7a6296efe28cf1544`. Historical source-locked reports retain their earlier blocked or pending dispositions. The M9 transport-processing hold remains in force. No deployment or release is authorized.


**As of:** 2026-11-05  
**Branch:** `feat/m10-timeline-projections` (originally branched from accepted G9 SHA `b4f7b93`; now reconciled with the accepted G16 and PR #27 documentation baseline `25e89026737063294a32bbd081ced400d19c6b3d` by merge `82c5d6eec34469ac1813d99270b4108788c9435d`). [Integration reconciliation](M10_M16_SOURCE_RECONCILIATION.md) records the merge and remaining verification.  
**State (updated 2026-11-07):** Original M10 source merged via PR #23; final security remediation, signed correction authority and upgrade-safe thirteenth migration merged via [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28). G10-S01 and S25 reviewed, G10 accepted and integrated into protected `main`. Verified source `e3002bd9ea38a20bd4c6b12027c6ecdcdf5fb343`; see [gate closure](M10_G10_GATE_CLOSURE_20261107.md). This does not authorize release or the M9 transport-hold removal.

**Planning:** [PR #22](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/22) is an independent open documentation PR; its M10 architecture, work packages and G10 scenario specification remain proposed, not accepted decisions.

## Current correction-authority checkpoint, 2026-11-07

[New-source implementation](M10_G10_CORRECTION_AUTHORITY_IMPLEMENTATION.md) is accepted and merged with thirteen additive migrations, signed Manager corrections, atomic M16 audit persistence and restricted database grants. The original 23 PASS evidence remains locked to `3e81b88`; correction-authority verification applies to `a26d19a`, with final upgrade/Compose verification on `e3002bd`. See [G10 closure](M10_G10_GATE_CLOSURE_20261107.md).

**Final merge-readiness update (2026-11-07):** Two review findings are closed in source and verified on `e3002bd9ea38a20bd4c6b12027c6ecdcdf5fb343` ([record](M10_G10_FINAL_MERGE_READINESS_20261107.md)): the accepted twelfth migration is restored unchanged and the NULL-safe projector scope function now arrives through the thirteenth migration, and the default `compose.yaml` requires `HARBOROPS_M10_CORRECTION_PROOF_KEY` for the migrator and API only. Targeted results: unit 239/239, contract 39/39, projector scope tests 3/3, change-impact integration set 126/126, default-stack correction flow 11/11, six projector mutants killed. The full integration suite, real-stack check and browser run were not repeated on this source. Independent review (G10-S25) and the S01 conformance disposition are NOT EXECUTED. Acceptance pending.

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
| New unit/integration test execution | Executed on `a26d19a`: unit 239/239, contract 39/39, integration 312/312 twice |
| `node scripts/check-planning-docs.mjs`, graph and script tests on this branch | Executed (pass) |
| Migration fresh/upgrade and schema checks | Executed: fresh, populated twelve-to-thirteen, downgrade guards |
| Browser and outage/security campaigns | Executed: `tracking-timeline` x 3 engines, audit HTTP and outage, image scans, 40/40 mutants killed. The other 17 browser specs: NOT EXECUTED |
| G10 independent evidence review | COMPLETED, S25 PASS on 2026-11-07; source-locked evidence and supplemental review reconciled |
| C01-C11 code-level remediation mapping | Committed; runtime proof recorded for the changed boundary |
| Explicit G10 acceptance and merge | ACCEPTED 2026-11-07; merged through PR #28 at `9b6c801fde4004a4c0021ec7a6296efe28cf1544` |

**Next action:** Proceed with G11 or other unlocked work according to the [roadmap](ROADMAP.md) and [execution graph](EXECUTION_GRAPH.md). Preserve the accepted M9 transport-processing hold. Do not treat G10 acceptance as production-release or deployment authorization.

## Superseding policy update, 2026-11-07

M10-L01 to M10-L14 policy selection is approved, with L07 requiring atomic M16 correction audit, L14 requiring constrained database correction authority with unforgeable actor/session provenance, and L06 conditional on the L14 guarantee. The [G10 L07/L14 remediation plan](M10_G10_L07_L14_REMEDIATION_PLAN.md) records exact requirements, proposed execution stages and negative tests. Prior statements above about pending policy selection describe earlier historical status, not the current decision.

**New-source technical tests have been executed; independent review and acceptance remain pending.** Earlier G10 A1-A7 raw evidence independently supports the frozen source `3e81b884b6a4cc782f84c58510793666786c5796`, not the security changes, which are covered by the [new-source verification record](M10_G10_REMEDIATION_VERIFICATION_20261107.md). Preserve all evidence and independently review that package. G10 acceptance, merging, release and deployment remain pending or unauthorized as applicable.

## Correction authority remediation source update, 2026-11-07

The approved L07/L14 remediation code is now committed on `fix/m10-g10-l07-l14-remediation`. It adds the thirteenth SQL-only migration, signed and server-authenticated correction operation, migration-owned M16 audit insert, protection against direct correction INSERT and forged correction replay, and targeted regression tests. The original frozen-source G10 evidence still applies only to `3e81b88`.

See [implementation and verification boundary](M10_G10_CORRECTION_AUTHORITY_IMPLEMENTATION.md). Compilation, PostgreSQL migration execution, source-locked integration, failure injection, security mutations and the three-engine `tracking-timeline` browser run were executed (see the record). The other 17 browser specs and the independent review were not. G10 acceptance and merge remain pending.
