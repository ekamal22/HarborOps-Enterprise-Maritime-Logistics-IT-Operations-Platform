# M11 End-to-End Implementation Plan

**Accepted implementation (2026-11-08):** M11/G11 was formally accepted and merged into `main` in [PR #33](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/33). The [G11 gate matrix](M11_G11_EVIDENCE_MATRIX_20261108.md) and [formal closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md) govern current evidence and the M11-P01..P12/P10b policy. The proposal, candidate and pending-test wording below records earlier planning checkpoints. The M11 evaluator stays opt-in and the M9 transport hold remains binding.

**Status:** Implementation completed, G11 verification accepted and source merged into `main` through PR #33 on 2026-11-08.
**Base:** accepted `main` `6ead1d076946dd96b53401265a31efc35c4a666c` (2026-11-07). **Work:** M11-01 to M11-04; exit G11.
**Architecture:** [M11 design](M11_ARCHITECTURE_DESIGN.md). **Gate:** [G11 verification specification](M11_VERIFICATION_PLAN.md). **Register:** [M11 planning status](M11_PLANNING_STATUS.md).

## Sixth-pass test dependencies

Add source-locked unit assertions that inactive rule severity is Info and active severity retains High/Warning. In a disposable fourteen-migration PostgreSQL environment, simulate stored severity drift on an existing active exception without changing its fingerprint, reevaluate to produce one versioned Reevaluated history event, and rerun to prove no duplicate. Validate due-work keyset pagination using null/non-null UUID boundaries. These checks are authored but NOT EXECUTED.

## Fifth-pass regression dependencies

M11-03 requires a new occurrence when Acknowledged/Inactive becomes Active. Verify an overdue schedule acknowledged, changed to future, reevaluated inactive, then changed back to past. Ensure the first occurrence is Resolved with AutoResolved history, the second is Open, and repeated evaluation adds nothing. Verify Open/Inactive cannot Resolve and unrelated constraint errors are not mislabeled as replay collisions. G11-S01 must decide intervening Unknown/Stale and permanent RecordedArrivalLate behavior.

## Fourth-pass implementation checkpoint

The unverified source candidate now has database-clock cutoff list cursors, separately protected append-only history keyset continuation, a scoped history API and UI Load more history action. Complete conflict-specific event lineage, true mutable-state snapshot semantics, independent SQL reader isolation and the proposed P10 recurrence choice are still open. Run actual compiled database and browser regression tests and reconcile the frozen raw evidence before any G11 gate recommendation.

## Current implementation review boundary

A provisional M11 implementation branch exists while local policy choices remain unapproved. The [second correctness audit](M11_SECOND_CORRECTNESS_AUDIT_20261107.md) documents source-level fixes to checkpoint freshness, signed authority, clock handling and HTTP/UI races. They require compiled, executed G11 evidence on a frozen SHA before any acceptance or merge recommendation.

## 1. Entry and source-lock safeguards

G10 is accepted and integrated. Confirm branch ancestry and current `main` before implementing. Reinspect `ShipmentRecord`/`ShipmentStatusChange`, `ShipmentStore`, `ShipmentTimelineStore.ReadBoundedAsync`, `TrackingTimelinePolicy`, `ShipmentTimelineEndpoints`, `OrganizationStore.ResolveAsync`, M16 audit code, the EF snapshot, migration 12/13, `compose.yaml`, the M9 hold and G10 gate closure.

Do not modify the repository merely to run a planning review. Once implementation is authorized, create an implementation branch from the accepted main revision, distinct from this planning branch. No implementation, source mutation, merge, release, deployment, GitHub Actions or transport hold removal is authorized by this document.

## 2. Contract freeze before implementation: M11-01

1. Resolve the twelve proposed [M11 local policy choices](M11_ARCHITECTURE_DESIGN.md#2-proposed-local-rule-policy-acceptance-pending). The rule semantics, provisional grace/tolerance defaults, confidence labels, eligibility, recurrence, permissions and manual resolution policy need explicit disposition. No upstream D-number is replaced.
2. Define the precise source priority: manual M5 workflow timestamps and planned schedule for schedule rules; fresh M10 equipment projection for conflict; no physical-arrival or ETA inference.
3. Freeze the rule truth table for `Open`/`Clear`/`Unknown`/`Stale`, cancellation/archive, missing schedule, exactly-equal threshold, future time, a corrected event and late-arriving input.
4. Define severity and evidence metadata, rule versioning, bounded reason text, cursor handling, source changes, and status/condition terminology.
5. Write deterministic synthetic rule examples for two tenants and every role. Capture acceptance decision separately. G11-S01 remains BLOCKED until choices are explicitly resolved.

**Deliverables:** approved local M11 contract, versioned rule examples, JSON/HTTP schemas, migration and permission design review, source-impact table. **Exit:** independent review of examples, precise rule boundaries and approved dispositions.

## 3. Work package M11-02: Evaluation engine

Implement after M11-01 contract approval. Split the change into testable units:

| Surface | Intended files or modules | Deliverable |
| --- | --- | --- |
| Domain | `src/HarborOps.Domain/Exceptions/ShipmentExceptionPolicy.cs` and immutable input/output records | Pure UTC rule evaluator with reason codes and evidence confidence; no HTTP or EF dependency |
| Persistence | `src/HarborOps.Infrastructure/Exceptions/ShipmentExceptionStore.cs` | Snapshot-consistent scoped reads, evidence fingerprint, condition upsert, CAS, unique active occurrence and bounded list/detail |
| Schema | New `src/HarborOps.Infrastructure/Persistence/Migrations/*_ShipmentExceptions.cs`; `HarborOpsDbContext.cs` and snapshot | Additive migration 14, FK/constraint/index/role checks; migration 1-13 unchanged |
| API | `src/HarborOps.Api/Exceptions/ShipmentExceptionEndpoints.cs`, `Program.cs`, DI | Authenticated tenant-scoped list/detail; fail-closed errors and no-store headers |
| Reevaluation | Bounded M11 due scanner/lease service under `src/HarborOps.Api/Exceptions/` or a scoped background service | Due-time rechecks and missed-change reconciliation independent of M9; safe on restart |
| Tests | New domain, migration, API and PostgreSQL integration suites | Truth-table, UTC boundaries, input drift, tenant security, correction/rebuild, replay and outage coverage |

Use a fixed explicit UTC instant for pure-policy tests and PostgreSQL clock time for transactional evaluation and signatures; the current source has no `TimeProvider` dependency. Do not insert an exception from raw event receipt; require accepted resolved effects and fresh materialization where tracking evidence is essential. M10 `TrackingProjectionHostedService` is disabled by default, so specify how fixtures/rebuild or an authorized read-materialization path supplies fresh input without silently enabling it in deployment.

Avoid N+1 full timeline reductions. Store indexed due checkpoints and source-version fingerprints; reevaluate bounded batches on relevant version changes and when time crosses a configured threshold. Implement backpressure, bounded retries and idle behavior. Do not weaken or consume the M9 unresolved-work cap.

**M11-02 exit:** fresh/replayed/corrected inputs produce deterministic per-rule results, no inferred transport ETA, and isolated role/tenant tests pass.

## 4. Work package M11-03: Lifecycle and audit

Implement independently of M11-02 after the approved M11-01 contract using the same agreed schema boundaries. One integration coordinator must own shared EF snapshot, API registration and migration.

1. Represent `Open`, `Acknowledged`, `Resolved` with immutable append-only transitions and an optimistic monotonic version. Acknowledged does not imply condition cleared. Manual resolve is rejected while active if M11-P10 is accepted.
2. Create exactly one active exception per tenant/shipment/rule/subject; a new occurrence is append-only and distinguishable after a prior resolution. Preserve an evidence fingerprint per change.
3. Use live selected-organization and current Manager session proof at the transaction boundary. If the API role can call direct SQL with a forged actor, require a narrow signed authority gateway with M11-specific domain-separated proof. Do not borrow an M10 correction signature.
4. Apply the existing CSRF/Origin, request limits, strict UUID idempotency and expectedVersion conventions. Payload must be small, allowlisted, and reject unknown fields.
5. Write M16 audit and M11 history in the *same transaction* for authorized human actions; roll back entirely when either fails. Exact replay returns prior outcome without duplicate audit, while conflicting replay is 409.
6. Validate cross-tenant, archived shipment, suspended organization, revoked Manager, role change, logout and stale-session races with a real restricted PostgreSQL API login.
7. Add safe UI actions and history display. An Analyst/Auditor sees only authorized read-only summaries; no administrator privilege is inferred.

**M11-03 exit:** exact-version Manager acknowledgment/allowed resolution and automated clearance are durable, idempotent, auditable and deny unauthorized or stale actors.

## 5. Work package M11-04: End-to-end integration and G11 preparation

After M11-02 and M11-03:

1. Integrate the shared migration, API, DI, scoped tests and web panel on one frozen candidate revision; no duplicate EF snapshots or independent SQL role provisioners.
2. Run corrected/late/out-of-order equipment-event fixtures through M8/M9/M10, source-locked M10 rebuild, M11 evaluation and browser display. Confirm no real transport-event processing or release of the M9 hold.
3. Exercise schedule boundary transitions with a controllable clock and no new tracking event, plus corrected input that clears/reopens one rule while preserving old evidence.
4. Run the [G11-S01 to G11-S25 matrix](M11_VERIFICATION_PLAN.md) with negative tenant, SQL role, revocation, audit failure, concurrency, restart, populated upgrade, accessibility smoke and cross-browser paths.
5. Write raw results and a machine-verifiable hash manifest on E:, record all failed/interrupted attempts and exact source revision and environment, and re-execute only tests affected by any later source change.
6. Independently review evidence provenance and source diff. S25 and G11 require separate explicit acceptance. A green runner is not an acceptance decision.
7. After acceptance and separate merge authorization, review protected-branch settings and create the required PR. Do not run GitHub Actions or enable automatic workflows. G11 acceptance never authorizes deployment.

## 6. Delivery slicing and parallelism

| Slice | Prerequisites | Coordination |
| --- | --- | --- |
| S0 Contract/policy fixtures | G10 accepted | Single M11-01 policy review, acceptance pending |
| S1 Pure domain evaluator | M11-01 | Can proceed alongside lifecycle after policy approval |
| S2 Read/persist evaluator and due scanner | S1 | Own one coordinated migration/snapshot and due schema |
| S3 Lifecycle + audit + signed authority | M11-01; shared schema agreement | Parallel with S1 in separate files, but one merge coordinator |
| S4 HTTP and UI integration | S2 and S3 stable | Combine scope, idempotency and evidence contracts |
| S5 Source-locked G11 campaign | M11-02 and M11-03 complete | Independent evidence review and explicit gate decision |

Do not run multiple mutable-DB integration campaigns concurrently. Parallel unit/static/browser jobs are allowed only with isolated ports, services, profiles and evidence directories. Use single-spec browser debugging first, then the required three-engine gate.

## 7. Proposed engineering checkpoints

- Checkpoint A: approved decisions, deterministic examples, schema and role review with no code changes hidden under the contract.
- Checkpoint B: warning-free .NET build, rule unit suite, new migration against clean and populated accepted M10/G16 baselines, matching snapshot and restrictions.
- Checkpoint C: transaction and role tests, source-change concurrency, M16 audit atomicity and replay.
- Checkpoint D: complete integrated stack, browser, restart, scan, selected effective security mutants, evidence manifest and independent review.
- Checkpoint E: G11 accepted only by explicit decision, then separately authorized merge and post-merge documentation reconciliation.

## 8. Operational and storage constraints

All controllable persistent repositories, worktrees, test PostgreSQL data, Docker/WSL layers, caches, screenshots, traces, reports and temporary build artifacts must be on E:. Use `HARBOROPS_WORKSPACE_ROOT` and verify actual storage paths. Unavoidable short-lived C: activity is allowed, but cannot become persistent project storage. No pruning, relocation or deletion without separate authorization. Use `[skip ci]` in every commit, no attribution trailers, and no GitHub Actions unless separately authorized. Preserve earlier test failures and all accepted migrations.

**Current disposition:** Full M11 delivery sequence implemented and accepted. Original staged verification tasks and decisions are documented in the [formal closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md).
