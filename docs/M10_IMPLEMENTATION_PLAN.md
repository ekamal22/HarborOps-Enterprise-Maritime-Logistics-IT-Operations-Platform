# M10 Implementation Plan: Shipment Timeline and Projection Rebuild

**State:** Planned only; no implementation or G10 verification claimed.  
**Baseline:** Accepted M0-M9/G0-G9 on protected `main` (2026-11-05).  
**Canonical graph:** `M10-01` depends on `G9`; `M10-02` and `M10-03` depend on `M10-01`; `M10-04` depends on both. G10 closes only after all four.  
**Design:** [M10 Architecture](M10_ARCHITECTURE_DESIGN.md); [Verification Plan](M10_VERIFICATION_PLAN.md); [M9 Contract](M9_LOCAL_CONTRACT_DECISION.md).

## Preflight, isolation and contract freeze

1. Start a clean dedicated E:-backed worktree from accepted `main`; record exact SHA and `git status`. Do not modify retained worktrees, Compose instances, databases, keyrings or evidence. Use separate `ho-verify-g10-<suffix>` Compose projects and project-specific E:-backed caches/build/test roots.
2. Verify accepted M9 schema, `TrackingAcceptedEvent`, `TrackingInboxReceipt`, `TrackingProcessingEffect`, `TrackingWorkAttemptEvent`, M5 shipment and M6 assignment mappings against the real source. Do not assume a source-sequence field or transport-voyage mapping that does not exist.
3. Record an **M10-local contract decision** covering deterministic order/ties, equipment observation semantics, correction authorization and append-only model, projection consistency/rebuild policy, nullable ETA, and disposition of the G9 transport-backlog hold. Mark each choice proposed until explicitly approved. If a choice affects M9 behavior, require its own approved cross-milestone change and G9 regression.
4. Agree on migration naming and ownership before touching EF snapshot. Coordinate separately with the unmerged M16 audit branch; preserve `20261104180000_AuditEntries` ordering before accepted `20261104190000_TrackingWorker` in any eventual combined history. Neither M10 nor M16 may silently replace the other's migrations or permission bindings.

## M10-01: Specify deterministic ordering and correction policy

**Depends on:** G9 accepted.  
**Deliverables:** Frozen documented event/order contract, correction policy, status semantics, explicit unknown/conflict behavior, canonical digest specification, timestamps, source-identity fixture map and reviewable decision record.

- Define ordering keys from actual M8/M9 columns: event occurrence UTC, receipt UTC, source ID, source-specific event key, receipt ID, accepted event ID. No nonexistent source sequence.
- Define tie cases, contradictory events, replay identity, late commits, archived shipments, prior assignments and source revocation without retroactive deletion.
- Limit projected tracking observation to `Unknown`, `EquipmentIn`, `EquipmentOut` and `Conflict` under the current supported payload. ETA is unknown unless a **separately validated** policy can derive it. Preserve manual M5 `WorkflowStatus`.
- Specify append-only correction records referencing real accepted events, void/supersede behavior, actor reason, generation and anti-cycle/anti-duplication checks; document Manager-only mutation decision and security review.
- Explicitly classify transport-reference mapping and backlog-release policy as **pending** until approved and independently verified.

**Done when:** M10-01 contract and fixtures are reviewed; every behavior has unambiguous expected outputs. This is not G10 acceptance.

## M10-02: Implement accepted, scoped timeline APIs

**Depends on:** M10-01.  
**Primary paths proposed:**
- `src/HarborOps.Api/Shipments/ShipmentTimelineEndpoints.cs` (new)
- `src/HarborOps.Infrastructure/Shipments/ShipmentTimelineStore.cs` (new)
- `src/HarborOps.Api/Program.cs` (register only approved routes)
- `src/HarborOps.Domain/Tracking/TrackingTimelinePolicy.cs` (pure ordering, conflict and cursor contract)
- `docs/API_CONTRACTS.md`, focused unit and integration tests.

**Actions:**
1. Query only same-organization, M9-resolved accepted events and approved correction overlays for a shipment. Never present unresolved/foreign events as shipment history or expose `RawPayloadUtf8`, canonical payload, digest or credentials.
2. Implement `GET /api/v1/organizations/{organizationId}/shipments/{shipmentId}/events` with stable keyset pagination, bounded page size, opaque scope-bound snapshot cursor and explicit cursor-stale behavior. Require current selected organization, active session and `ViewShipmentData`.
3. Add `GET .../tracking-projection` only after the projection schema/contract is ready. Provide separate `observedTrackingState` versus manual `workflowStatus`, policy version, UTC provenance and unknown/stale markers.
4. Return existing sanitized error envelope, correlation ID and `Cache-Control: no-store`. Maintain object-level 404/403 semantics without tenant enumeration.
5. Include direct SQL, HTTP and three-browser smoke tests of authorized timeline view if UI is added. Do not add dedicated accessibility tests before M24-04.

**Done when:** M10-02 API contracts, pagination, cross-tenant denial, live role/session revocation, invalid cursor and privacy tests pass on a committed source SHA.

## M10-03: Implement versioned read models and correction/rebuild engine

**Depends on:** M10-01.  
**Primary paths proposed:**
- `src/HarborOps.Infrastructure/Persistence/Migrations/<next-id>_ShipmentTrackingProjection.cs` and `HarborOpsDbContextModelSnapshot.cs` (additive, coordinated)
- `src/HarborOps.Infrastructure/Shipments/ShipmentTrackingProjectionRecords.cs`, `ShipmentProjectionStore.cs`, `ShipmentProjectionRebuilder.cs` (new)
- `src/HarborOps.Domain/Tracking/ShipmentProjectionPolicy.cs` (deterministic pure reducer)
- `src/HarborOps.Worker/` (separate, opt-in M10 projector; do not alter M9 claim/finalization semantics)
- correction/rebuild endpoints, only after authorization decisions.

**Actions:**
1. Introduce tenant-owned projection, append-only correction and receipt-unique projection-application tables with same-organization composite foreign keys, immutable correction safeguards, bounded indexes and fresh/populated migration rehearsals.
2. Build a pure reducer over deterministic, source-locked eligible events plus corrections. Verify `GTIN`/`GTOT` observation semantics, `Conflict`/Unknown results, no fabricated ETA or M5 status changes.
3. Discover unresolved projection work by durable left anti-join or an equivalent no-skip mechanism, not by `resolved_at_utc` high-water alone. Build crash-safe, idempotent, bounded batches and policy-version rebuild markers.
4. Use a documented organization-first locking sequence, a per-shipment lock, bounded retries and atomic projection/application commit. Verify concurrency against M9 finalization and that a late committed resolution cannot be skipped.
5. Add Manager-only, CSRF/Origin protected explicit correction and bounded rebuild operations with append-only actor/reason evidence if the M10-local contract authorizes them. A rebuild of identical inputs must preserve identical digest/state without a phantom version increment.
6. Do **not** activate sustained transport processing. Implement any transport mapping/backlog drain only after a separate approved policy and source-locked M9 compatibility review.

**Done when:** Additive schema, projector, correction controls and pure reducer meet agreed contract with real PostgreSQL outage, restart, concurrent write and repeat-rebuild evidence. No G10 acceptance implied.

## M10-04: Verify rebuild, cross-milestone behavior and failure recovery

**Depends on:** M10-02 and M10-03.  
**Primary files proposed:** `tests/HarborOps.UnitTests/*Projection*`, `tests/HarborOps.IntegrationTests/*Projection*`, `tests/browser/specs/tracking-timeline.spec.mjs` if a view exists, `scripts/verify-g10-local.sh` and `docs/M10_EXECUTED_VERIFICATION.md` (all names proposed).

- Execute all [G10 scenarios](M10_VERIFICATION_PLAN.md) against fresh and populated isolated databases, exact migration history, real PostgreSQL, tenant-role matrix, accepted G0-G9 regressions, both API families, correction and replay, late-commit race, outage/restart, input checksum stability and rebuilt projection equivalence.
- Run source-locked .NET unit/contract/integration tests and browser regressions in Chromium, Firefox and WebKit if UI changed; use controlled security mutation or adversarial tests for the G10-specific isolation/idempotency protections. Include image/dependency security assessment relevant to changed artifacts.
- Verify independent M9 worker effect-once behavior, 10,000 unresolved capacity guard and parked transport events. Never infer backlog safety from a passing projection test.
- Collect sanitized commands, exact revision, environment, raw output, failure/retry records, source hash and artifact manifest. Distinguish unexecuted, failed, technical pass, independent evidence review and explicit G10 acceptance.
- Require an independent raw-evidence disposition and separate merge decision before integration. Preserve accepted evidence and avoid GitHub Actions.

## Parallel work, merge and release boundaries

M10-02 API and M10-03 projection work may advance **in parallel after M10-01**, each in its own branch, E:-backed resources and isolated test database. Shared `HarborOpsDbContext`, snapshot, `Program.cs`, organization permissions, shipment endpoints and Compose must have a single designated integration branch. The combined candidate, not independent task branches, must pass M10-04.

M16 remains a separate unaccepted branch with an unreviewed S07/S08/S28 supplement and incomplete M9 integration. Any M10/M16 overlap in migrations, permissions and database identities must be explicitly reconciled before either final merge.

**Do not claim G10 complete, release, hosted readiness or permission to lift M9's transport backlog restriction from this planning document.** See [pre-release documentation checklist](RELEASE_READINESS_TODO.md).
