# M11 Delay Rules and Shipment Exceptions: Architecture

**Final M11/M12 technical verification (2026-11-08):** [Run reconciliation](M11_M12_VERIFIED_COVERAGE_RECONCILIATION_20261108.md) confirms **341/341 integration PASS** on `f56d017` with no skips, plus 40/40 impacted, G11 19/19, G12 4/4, three-engine browser 36/36, effective mutants 5/5, SQL read isolation and security scans. [G11/G12 acceptance sufficiency review](M11_M12_ACCEPTANCE_REVIEW_20261108.md), [G11 matrix](M11_G11_EVIDENCE_MATRIX_20261108.md) and [G12 matrix](M12_G12_EVIDENCE_MATRIX_20261108.md) record exact remaining evidence gaps. Independent S25 reviews and acceptance pending. Older 340-case and 'in progress' notes below describe historical checkpoints, not current execution.

**Executed verification reconciliation (2026-11-08):** [M11/M12 source-locked evidence](M11_M12_VERIFIED_COVERAGE_RECONCILIATION_20261108.md) establishes 340/340 earlier full integration, 40/40 updated impacted regressions including populated 13-to-15 migration, G11 19/19, G12 4/4, 36/36 browser tests, 5/5 killed mutants, restricted SQL isolation and security scans on individually identified commits. The updated source gate passed 291/291 unit and 46/46 contract tests. Final full rerun `42443065884` is still in progress. Gate matrices, independent S25 reviews, and acceptance remain pending. Historical source-only statements below are not current test results.

**Current verification policy (2026-11-08):** [M11/M12 verification operations](M11_M12_VERIFICATION_OPERATIONS_20261108.md) now requires manual, coordinated, source-locked workflows instead of automatic push-triggered campaigns. [Run-by-run progress](M11_M12_VERIFICATION_PROGRESS_20261108.md) is the current verification checkpoint; older reports and source-only notes below are historical. G11/G12 independent S25 review and acceptance remain pending. Protected `main`, the M9 transport hold, and deployment restrictions are unchanged.

**Latest hosted verification (2026-11-08):** [Executed M11/M12 run register](M11_M12_HOSTED_VERIFICATION_20261108.md) records a successful .NET and frontend source gate, G11 18/18 scoped PostgreSQL tests, G12 2/2 exclusive PostgreSQL tests, 36/36 impacted integration regressions, and direct SQL isolation of exception, history and evaluation tables with forged-scope denial. Updated source/dependency and runtime-image security scans passed. The three-engine browser rerun passed 36/36 tests, with 12/12 each in Chromium, Firefox and WebKit, including M11 exception journeys, M12 search/export and targeted M5 regressions. G11-S08 conflict-specific provenance remains open; M12 local policy decisions and independent G11/G12 S25 reviews remain pending. No G11/G12 acceptance, merge or deployment is authorized. Historical source-only notes below are retained as previous checkpoints.

**Historical combined candidate and policy checkpoint (before hosted verification):** The isolated `feat/m11-m12-combined-integration` branch contains both source candidates and shared API, React, SQL role and migration reconciliations. [M11 policy decisions](M11_LOCAL_CONTRACT_DECISION_20261108.md) now establish the rule thresholds and lifecycle contract, including verified clearance through Unknown/Stale and reviewed active `RecordedArrivalLate` resolution. Earlier no-combined-source and M11-policy-pending descriptions below are pre-integration history. G11 and G12 technical execution, independent evidence review and acceptance remain pending; source integration and authored tests are not PASS.

**M11/M12 coordination approved (2026-11-08):** [Combined integration and verification plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md). Future combined source must preserve M11 signed SQL evaluation, exception history, default-off reevaluator, unknown/stale semantics and Manager actions while coexisting with M12 search/export and its distinct proof key. Existing recurrence, resolution, SQL SELECT and pagination concerns remain open.

**Historical M11 source audit (before hosted verification):** [Sixth correctness audit](M11_SIXTH_CORRECTNESS_AUDIT_20261108.md) documents informational severity on inactive outcomes, versioned rule-metadata reconciliation and typed due-work continuation. The source changes and new tests are unexecuted. G11-S01 BLOCKED, G11-S02 through S25 NOT EXECUTED, acceptance pending. The M9 transport-processing hold remains in force.

**Implementation checkpoint (2026-11-07):** A provisional source implementation exists on `feat/m11-delay-exception-lifecycle`, including additive migration 14, rule evaluation, scoped HTTP routes, guarded Manager actions, a shipment UI, and executable tests. The code has not been compiled or run against PostgreSQL in this review. M11 local policy choices remain proposed; G11-S01 is BLOCKED on policy approval; G11-S02 through G11-S25 remain NOT EXECUTED and acceptance pending. No merge, deployment, or transport-hold removal is authorized.

**Status:** Provisional M11-01 contract. The feature branch contains an unverified implementation candidate; policy and G11 acceptance pending.
**Planning base:** accepted G10/G16 `main` at `6ead1d076946dd96b53401265a31efc35c4a666c`, 2026-11-07.
**Prerequisite:** G10 accepted. The canonical work-package dependencies remain those in [execution-graph.json](execution-graph.json).
**Scope:** M11-01 rule definition; M11-02 deterministic exception evaluation; M11-03 scoped, auditable exception lifecycle; M11-04 end-to-end verification. FR-010, FR-009, FR-002, NFR-002 and NFR-007.
**Companion documents:** [implementation plan](M11_IMPLEMENTATION_PLAN.md), [G11 verification](M11_VERIFICATION_PLAN.md), [planning register](M11_PLANNING_STATUS.md).

## 1. Accepted inputs and design boundaries

M11 extends the accepted modular monolith and PostgreSQL transaction boundary. It must not reinterpret accepted M5 shipment workflow, M8 immutable receipt history, M9 worker effects, M10 corrected timeline or M16 audit permissions.

| Accepted input | Current location and semantics | M11 use |
| --- | --- | --- |
| Shipment schedule, workflow, archive flag and version | `ShipmentRecord` and `ShipmentStatusChange`, `src/HarborOps.Infrastructure/Shipments/` | Planned departure/arrival in UTC and recorded manual workflow transitions; never asserted physical position |
| Deterministic tracking read model | `ShipmentTimelineStore.ReadBoundedAsync`, `TrackingTimelinePolicy.Version = m10-equipment-v2` | Equipment observations and explicit conflict, input revision/digest, correction generation, stale flag |
| Retained accepted equipment history | `shipment_tracking_timeline_entries`, M10 corrections and M9 receipt-unique effects | Provenance and correction-sensitive evaluation; no rewrite of source evidence |
| Tenant/session authorization | `OrganizationStore.ResolveAsync`, `OrganizationPermission.ViewShipmentData` | Live selected organization and typed business-role checks |
| Central audit | M16 `audit_entries` and transaction-coupled write contract | Sensitive human dispositions must create exactly one authorized audit event in the same commit |

**Mandatory restriction:** M9 ARRI and DEPA transport events remain parked in `NeedsReconciliation`. The 10,000 unresolved-work-item cap remains enforced. M10 reports `etaUtc = null`; its observed equipment events are GTIN/GTOT, not transport-arrival evidence. M11 must not derive vessel ETA, assert physical arrival/departure or drain parked events. No production deployment or sustained transport processing is authorized.

## 2. Proposed local rule policy, acceptance pending

These are **provisional candidate choices requiring M11-01 approval**, not approved decisions. The draft feature branch implements some of them for executable scrutiny; that does not authorize G11 acceptance or merge. Unresolved decisions remain blockers, and no existing D-number is silently repurposed.

| ID | Proposed contract | Reason |
| --- | --- | --- |
| M11-P01 | Rule version `m11-rules-v1`; case-stable rule identifiers `EquipmentConflict`, `ArrivalScheduleOverdue`, `RecordedArrivalLate`, `DepartureSlippageRisk`. | Deterministic storage, migration and reporting |
| M11-P02 | Only accepted, active same-organization shipments are eligible. Archived or cancelled shipments are excluded from new schedule-risk evaluation; existing exception history is preserved. | Avoid indefinite noise and cross-scope leakage |
| M11-P03 | `EquipmentConflict` requires a non-stale M10 projection with observed `Conflict` and the container/evidence identifiers actually responsible. It is not a movement or ETA assertion. | Reuse accepted M10 conflict semantics |
| M11-P04 | `ArrivalScheduleOverdue` means a configured grace interval has elapsed after planned arrival while no recorded `Arrived`/`Completed` manual workflow transition exists. Label it **schedule overdue, arrival unconfirmed**, never confirmed physical delay. | Avoid false claims from absence of tracking |
| M11-P05 | `RecordedArrivalLate` means a recorded manual transition to `Arrived` occurred strictly after planned arrival plus grace. Describe evidence as a **recorded workflow arrival**, not independently verified physical arrival. | Distinguish observed workflow evidence from missing data |
| M11-P06 | `DepartureSlippageRisk` requires a recorded `InTransit` transition strictly later than planned departure plus tolerance while scheduled arrival is still in the future. It is a conservative schedule-risk indicator, not a prediction. | Provide a defensible at-risk label without ETA |
| M11-P07 | Proposed demo defaults: arrival grace 60 minutes, departure tolerance 60 minutes, UTC instant supplied to the pure evaluator and derived from PostgreSQL for transactional signing. Values belong to a versioned policy, not caller query parameters. Equality at the boundary is not delayed. | Deterministic clock-bound tests; threshold approval pending |
| M11-P08 | A missing required schedule makes schedule rules unknown. Missing `InTransit`/`Arrived` history cannot prove recorded departure slippage or late arrival, although lack of a recorded arrival permits the separately labeled `ArrivalScheduleOverdue` condition after grace. Stale M10 materialization cannot prove equipment conflict. Never treat unknown as confirmed clear or delayed. | Explicit confidence and uncertainty |
| M11-P09 | Separate matching rule instances may coexist. Display precedence `Conflict > RecordedArrivalLate > ArrivalScheduleOverdue > DepartureSlippageRisk` is presentation only; never delete other active conditions. | Stable UI and auditable independent causes |
| M11-P10 | Proposed lifecycle: first Active detection opens; Open auto-resolves on verified Inactive; Acknowledged stays inactive for review unless the same rule becomes Active again directly after verified Inactive, in which case the previous occurrence is system-closed and a new Open occurrence is created. Unknown/Stale is not clearance. Recurrence after intervening uncertainty and disposition of immutable RecordedArrivalLate require explicit approval. | Preserve distinct incidents while policy acceptance remains pending |
| M11-P11 | Analyst, Manager and Auditor with active selected-organization shipment-read access may see scoped exception state. Only active Manager can acknowledge/resolve. Manager and Auditor can view attributable disposition history; Analyst response redacts free-form reasons and actor IDs. Administrator/Support lack implicit business access. Mutations require selected session, CSRF/Origin, expected version and a UUID replay key. | Least privilege and sensitive history boundaries; approval pending |
| M11-P12 | Sensitivity: human dispositions write one M16 central audit event atomically and append immutable M11 state history. System evaluation creates its own immutable rule history. Direct API-role SQL updates must not bypass Manager/session authorization. | Preserve G16 and G10 database defense-in-depth |

Approval may alter the proposed rules. If a rule cannot be justified against accepted evidence, omit the rule rather than fabricate confidence. Explicitly distinguish `ActiveCondition`, `ClearedCondition`, `InsufficientEvidence` and `StaleInput`.

## Sixth-pass provisional severity and fingerprint semantics

An inactive condition must not retain severity appropriate only to an active qualifying rule: recorded timely arrival and inactive schedule or departure risks use Info, while their active variants retain High or Warning. The deterministic source fingerprint intentionally excludes severity, confidence and next-due metadata, so the signed evaluator's no-op path must also compare persisted visible metadata before skipping a versioned Reevaluated event. The due scanner's nullable UUID keyset bounds are explicitly typed. These remain provisional source invariants pending execution and policy approval.

## Fifth-pass provisional recurrence contract

The signed evaluator closes a previously verified inactive Acknowledged incident and opens a new occurrence if that rule becomes active again. It records AutoResolved history and releases the partial unique index before inserting the new Open record in one SQL transaction. The Manager Resolve action is permitted only for Acknowledged/Inactive. Recurrence after Unknown/Stale and the handling of immutable recorded lateness remain undecided. See the [fifth audit](M11_FIFTH_CORRECTNESS_AUDIT_20261108.md).

## Fourth-pass pagination contracts

The provisional exception list uses `first_detected_at_utc` at or before the initial PostgreSQL-clock cutoff, kept in a separately versioned cursor. It prevents inclusion of newly opened occurrences but does not freeze mutable lifecycle fields. The detail endpoint retains a 500-row preview and exposes `nextHistoryCursor` when more rows exist. A separately protected, organization/exception-bound history route pages the append-only `occurred_at_utc,id` keyset and maintains Analyst redaction. These source capabilities remain unverified, and complete conflict-specific evidence lineage is a separate open requirement.

## Third-pass signed authority and presentation contracts

The provisional evaluation proof domain `m11-eval-v2` binds reference, threshold and due timestamps as canonical Unix microseconds; the Manager action remains `m11-action-v1`. Required SQL arguments and signatures are explicitly non-NULL. The proposed P09 list rank orders EquipmentConflict, RecordedArrivalLate, ArrivalScheduleOverdue and DepartureSlippageRisk, then UUID within the rule; the protected cursor follows the same composite key. Known evaluation uniqueness/serialization races are retried within a bounded budget. These source contracts are not yet verified or approved.

## Additional provisional source invariants

An unchanged semantic result advances the evaluated-UTC and next-due checkpoint without adding duplicate immutable history. Manager dispositions require a matching, nonexpired checkpoint, and equipment dispositions require the accepted M10 projection to be no fresher than the checked input. Proof expiry comes from the PostgreSQL clock. UTC threshold arithmetic clamps safely at the maximum timestamp and schedules at the next representable microsecond. These are candidate source behaviors, not verified or approved policies; see the [second audit](M11_THIRD_CORRECTNESS_AUDIT_20261108.md).

## 3. Evaluation and provenance semantics

1. Resolve an active organization, selected session and shipment scope before exposing any data. For internal evaluation, use a dedicated least-privileged executor and validate active organization/shipment inside its transaction.
2. Acquire a consistent PostgreSQL snapshot of the shipment row/version, first qualifying workflow transition, M10 input revision, correction generation, projection policy/version, materialization revision and a database-provided UTC evaluation instant. Never combine revisions from different snapshots into one published assertion.
3. Reject stale/absent M10 materialization as current tracking evidence. Schedule-only checks may be independently evaluated from authoritative shipment/workflow input, but any result that depends on M10 must remain `StaleInput` until repaired.
4. Evaluate each policy independently. Evidence references must name source kind, source identifier, UTC time, rule version, compared threshold, actual comparison result and confidence/uncertainty label; never store an unrestricted raw payload.
5. Persist meaningful input, due-boundary or state transitions. The current shipment-level fingerprint binds organization, shipment, rule, workflow and schedule inputs, shipment version, M10 input revision/digest/correction generation, tracking policy, rule policy, condition and boundary. No per-container subject key is implemented; complete conflict-specific lineage remains a separate G11 blocker. Preserve a stable digest and bounded structured facts.
6. No event is edited to make a rule true. Corrections and rebuilds advance observed input revisions; the evaluator recomputes a new condition and appends a disposition instead of altering old evidence.
7. A successful idempotent replay returns the original response/version and does not duplicate state or audit. Same idempotency key with different actor, action or payload conflicts.
8. Supply an explicit UTC evaluation instant to the pure policy (a fixed instant in unit tests), and derive the transactional evaluation/proof time from PostgreSQL UTC. No application `TimeProvider` dependency exists in this candidate. Never compare local-formatted timestamps or host timezone defaults.

A schedule becoming overdue merely due to wall-clock advance needs scheduled reevaluation. Plan a **separate bounded M11 due-work scheduler**, not reuse or enable the M9 transport worker. On shipment schedule/workflow changes or M10 input/correction/rebuild changes, enqueue or discover affected shipments without changing accepted M5/M10 write invariants. Bounded reconciliation scans and a durable due index must cover restart, missed notifications and late commits. No unbounded periodic full-table scan or request-side full-history replay.

## 4. Persistence and concurrency design

Add a new, additive **fourteenth EF Core migration** after `20261107120000_M10CorrectionAuthority`, without editing migrations 1 through 13.

Proposed tables:

- `shipment_exception_rules` or a versioned code catalog: immutable rule identity and policy version. Do not use arbitrary runtime-entered SQL/rules.
- `shipment_exceptions`: UUID, tenant/shipment composite FK, shipment-level rule code, occurrence number, condition status, lifecycle state, severity, first detected UTC, last evaluated UTC, resolved UTC, source fingerprint, structured evidence containing rule policy, optimistic version and next evaluation UTC. No per-container subject key or separate cleared timestamp exists in migration 14.
- `shipment_exception_history`: append-only event ID, exception composite FK, UTC action, actor kind/user/session (where applicable), old/new state, source fingerprint, bounded reason and correlation; immutable trigger.
- `shipment_exception_idempotency`: same-tenant actor/action/key/payload digest and replay response; bounded records plus enforced uniqueness.
- Optional `shipment_exception_due_work`: tenant-bound checkpoint, claim/lease epoch and due time if a single indexed `next_evaluation_at_utc` on the exception table cannot cover not-yet-created conditions.

Required constraints: composite organization/shipment FKs, unique unresolved occurrence per `(organization, shipment, rule)`, positive versions, allowed state and severity enums, bounded JSON metadata, UTC columns, no cascading deletion of historical evidence, and tenant/due/keyset indexes. The SQL-only migration adds no new EF entities or snapshot model. Validate existing-volume additive upgrade and downgrade guards without rewriting earlier migrations.

Concurrency: serialize evaluation/disposition on a deterministic tenant/shipment lock order; enforce expected version and input fingerprint in the transaction, then CAS/unique constraints. Evaluate-and-create, acknowledge, automatic clearance and manual resolve races must not lose audit events or create duplicate active occurrences. An evaluation failure must never half-commit an exception without its history or required central audit. No direct insert/update/delete grant to the API DB login outside a guarded, narrow entry point. Where SQL-side identity validation cannot prove HTTP request provenance, use a short-lived session-bound signed proof with an M11-specific domain/key, following the M10 L14 design pattern. Do not reuse M10 correction proof bytes or grant its privileges to the M11 evaluator.

## 5. Public application and UI contracts, proposed

Scoped routes under `/api/v1/organizations/{organizationId}`, consistent with [API contracts](API_CONTRACTS.md) and M2/M5/M10:

| Method | Route | Contract |
| --- | --- | --- |
| GET | `/shipments/{shipmentId}/exceptions` | Bounded current-and-history summary for the visible shipment |
| GET | `/exceptions` | Tenant-scoped list with optional `state` and `shipmentId`, `limit=1..100`, protected rule-precedence keyset cursor and database creation cutoff; no `ruleCode` query filter is implemented |
| GET | `/exceptions/{exceptionId}` | Evidence summary and immutable bounded transition history |
| GET | `/exceptions/{exceptionId}/history` | Bounded `limit=1..100` continuation of immutable history using an encrypted, tenant-and-exception-bound `(occurred_at_utc,id)` cursor; Analyst reasons and actors redacted |
| PATCH | `/exceptions/{exceptionId}` | `{ "action": "Acknowledge" | "Resolve", "expectedVersion": 7, "reason": "..." }`, with UUID `Idempotency-Key`, CSRF/Origin and live Manager authority |

List/detail results include `ruleCode`, `rulePolicyVersion`, `severity`, `condition`, `state`, `reasonCode`, `evidence` (bounded, redacted), `sourceInputVersion`, `correctionGeneration`, `evaluatedAtUtc`, `materializationStale`, `nextCursor` when relevant and `version`. Expose `etaUtc: null` only if showing existing M10 projection context. No global collection, anonymous access, arbitrary external links, source credentials, secret-bearing traces or raw events.

Use existing response conventions: 401 unauthenticated, 409 missing organization selection, 403 wrong role/revocation, 404 foreign or absent resource, 400 malformed input, 409 stale version/replay collision, 422 invalid lifecycle, 503 database/authority unavailable. All responses include bounded correlation references without private detail and `Cache-Control: no-store`. A cursor is bound to tenant, query and snapshot to prevent swapped-tenant or changed-filter enumeration.

UI: add a shipment exception panel to `web/src/shipments/ShipmentWorkspace.tsx`, referencing the existing tracking timeline component without replacing its manual workflow. Render visible unknown/stale labels and concise rule explanations, source UTC timestamp, acknowledged/resolved history and accessible keyboard-friendly action controls. Do not add M15 notifications, M13 aggregate dashboards, M14 map features or predictive ETA.

## 6. Isolation, failure and upgrade requirements

- SQL tenant predicates and verified database grants are required even if HTTP checks pass. Deny cross-org ID substitution, archived/suspended organizations, revocation after initial read, and replay after session logout.
- Manual state changes must be transactionally audited under M16; audit failure rolls the action back. Reason text remains bounded and privacy-safe.
- Time-only reevaluation is idempotent after restart. A power or database outage must leave committed exceptions/history intact; only requeue unfinished evaluation work.
- Source corrections and superseded events must not create duplicated active exceptions; histories remain inspectable and the latest state reflects the corrected M10 input once it is fresh.
- A true M10 `Conflict` may coexist with schedule risk, but no `ARRI`/`DEPA` event can be treated as processed or used as proof.
- Use a disposable E:-backed clean database and a populated accepted-M10/G16 database copy when validating migration 14. Never use or mutate the only retained volume.

## 7. Ownership of downstream contracts

G11 produces explainable per-shipment exception records for later G13 KPI definitions and G15 notification recipient/deduplication rules. Those downstream milestones must consume versioned exception IDs, occurrence, evidence status and deterministic policy version. G11 does **not** implement KPI aggregation, recipient routing, map tiles, incident tickets, transport backlog remediation or deployment.

**Disposition:** Architecture proposed for implementation after M11-P01 through M11-P12 are decided. The accepted G10 source and mandatory M9 hold remain unchanged.
