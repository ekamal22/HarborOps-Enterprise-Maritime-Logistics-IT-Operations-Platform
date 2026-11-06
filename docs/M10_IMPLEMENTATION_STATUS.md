# M10 Event Timeline and Projection Rebuild: Implementation Register

**As of:** 2026-11-05  
**Branch:** `feat/m10-timeline-projections` (based on accepted G9 `main`, starting SHA `b4f7b93f1ee8135b9a16b605ded315b3fbc036cc`).  
**State:** Source implementation candidate committed and pushed. **G10 acceptance pending. Not merged. No runtime test execution claimed.**  
**Planning:** [PR #22](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/22) is an independent open documentation PR; its M10 architecture, work packages and G10 scenario specification remain proposed, not accepted decisions.

## Implemented candidate paths

| Component | Source | Intended behavior |
| --- | --- | --- |
| Deterministic ordering and reducer | `src/HarborOps.Domain/Tracking/TrackingTimelinePolicy.cs` | UTC occurrence then receipt/source/event key/ID tie-break, explicit Conflict and correction handling, stable SHA-256 input fingerprint; unknown ETA |
| Additive M10 migration | `src/HarborOps.Infrastructure/Persistence/Migrations/20261105180000_ShipmentTrackingProjections.cs` | Scoped projections, append-only corrections, durable correction heads, idempotent operation replays and policy-versioned application records; composite effect/shipment foreign keys, serialized generations and populated-history downgrade guard |
| Timeline/rebuild store | `src/HarborOps.Infrastructure/Shipments/ShipmentTimelineStore.cs` | Complete streaming M9 effect history, per-container reducer, bounded keyset pages, policy/version drift reconciliation, serializable org-then-shipment locking and durable operation replay |
| Scoped HTTP routes | `src/HarborOps.Api/Shipments/ShipmentTimelineEndpoints.cs` and `Program.cs` | Two GET read routes with encrypted scope-bound snapshot/keyset tokens; Manager-only, CSRF-protected, rate-limited and idempotent correction and rebuild routes with live session verification |
| Optional projection updater | `src/HarborOps.Api/Shipments/TrackingProjectionHostedService.cs` | Disabled by default; dedicated restricted PostgreSQL identity with fail-closed startup, bounded work scans, policy/correction/drift reconciliation |
| Shipment tracking UI | `web/src/shipments/ShipmentTrackingTimeline.tsx`, `ShipmentWorkspace.tsx` | Authorized, read-only observed tracking status and paginated provenance; manual workflow remains separate |
| G10 local resources | `compose.g10.verify.yaml`, `scripts/verify-g10-local.sh`, `scripts/configure-m10-projector-db.sql` | Isolated E:-backed build, tests, scoped role privilege checks, browser fixtures and three-engine functional coverage; not a complete G10 campaign |
| Unit/regression tests | `tests/HarborOps.UnitTests/TrackingTimelinePolicyTests.cs`, `tests/HarborOps.IntegrationTests/TrackingProjectionG10Tests.cs` | Reducer order, replay, conflicts, void/supersede, digest, real PostgreSQL effect-to-timeline, rebuild, unchanged manual workflow and cross-tenant denials |
| Existing migration regressions | `MigrationSnapshotTests.cs`, `TrackingInboxMigrationTests.cs`, `TrackingInboxAuditSqlTests.cs`, `TrackingWorkerMigrationTests.cs`, `ReferenceHistoricalUpgradeTests.cs` | Expect additive M10 migration after accepted M9 and leave historical intermediate counts intact |

## Source boundaries

M8 receipts/events and M9 receipt-unique effects, lease fencing, work states and 10,000 unresolved-item guard are untouched. Only M9-resolved equipment observations become shipment timeline input. `GTIN`/`GTOT` yield per-container `EquipmentIn`/`EquipmentOut`, same-container `Conflict`, or mixed-container `Mixed`; no verified ETA, voyage inference or automatic M5 `WorkflowStatus` transition is claimed. Unresolved opaque ARRI/DEPA transport events remain parked, with the **mandatory sustained transport-event hold unchanged**. Background materialization does not start automatically; authorized GET derives current state live even when cached materialization is stale.

## Second correctness audit, source-locked

The [second source correctness audit](M10_SECOND_CORRECTNESS_AUDIT.md), against `784689f483c64d19495ee8e9c1047464aaef8f32`, identifies a critical malformed migration and further projector privilege, fairness and query-scaling blockers. Previous C01-C11 remediation mapping remains historical **source-only work**, not verified completion. **PR #23 must remain draft and unmerged.** No runtime testing or final G10 review has occurred.

## Correctness audit

A [source-locked correctness audit](M10_SOURCE_CORRECTNESS_AUDIT.md) of runtime revision `83d78bb7b271b96d5086044801fc503f07d8a417` identified blocking integrity, scalability and projection-reconciliation findings. [Remediation register](M10_CORRECTNESS_REMEDIATION.md) maps the eleven findings to committed source corrections. **Merge readiness remains blocked** pending executed source-locked verification and independent review. The audit is a static review and records no runtime test pass.

## Known technical and acceptance blockers

- **No execution:** .NET SDK, Docker and a reachable E:-backed PostgreSQL environment are unavailable from this GitHub-only session. No tests, migrations, browsers, security scans, failure injection or generated G10 source-locked evidence have been run here. New test files are authored, not passed.
- **G10-S01 policy approval pending:** Deterministic tie/conflict policy, correction semantics/authorization, exact projection revision/consistency and transport-map/backlog policy require explicit review. Implemented source is a candidate; the code does not approve these choices.
- **Scale and operability:** The source candidate removes the 10,000-resolved-event rejection and reduces full history through a streamed reducer with bounded keyset page memory plus per-container observation state. Reads remain O(retained events) and require verified latency/resource limits. The projector is disabled by default and requires the distinct restricted `harborops_projector` database role and separately supplied credentials.
- **Evidence:** Source-locked compile, unit/contract/integration, fresh/populated migration, role/session, direct SQL immutable corrections, race/failure windows, three-browser functionality, security mutations/scans, independent raw-evidence review, G10 acceptance and separate merge authorization are pending. The local helper does not cover all gates by itself.
- **M16 integration:** The unmerged M16 feature branch requires its S07/S08/S28 supplemental raw evidence and combined M9/M16 migrations, permissions and DB-role review. The M10 SQL-only migration follows the accepted M9 version; when M16 merges, preserve the full ordered migration history and verify snapshot/permission compatibility against the final combined SHA.
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
