# M8 Implementation Plan: Durable Event Inbox and Immutable History

**State:** M8-02/M8-03 implementation candidate and M8-04 test/runner source committed on `feat/m8-durable-event-inbox`. D-006 acceptance pending; no executable G8 evidence or gate acceptance. See [implementation register](M8_IMPLEMENTATION_STATUS.md).
**Prepared:** 2026-11-03
**Base:** Accepted M0-G0 through M7-G7 `main` at `47f030f9fa4b588d589f3ae082d03b3d5b94736a`.
**Planning branch:** `plan/m8-durable-inbox-immutable-history`.
**Source design:** [M8 architecture](M8_ARCHITECTURE_DESIGN.md).
**Decision:** [D-006 proposed Option A](M8_D006_DURABILITY_PROPOSAL.md), acceptance pending.
**Verification:** [G8 plan](M8_VERIFICATION_PLAN.md).
**Graph:** [work-package dependencies](execution-graph.json), [roadmap](ROADMAP.md), [parallel policy](PARALLEL_DELIVERY_POLICY.md).

## 1. Goal and strict exclusions

Deliver transactional durable intake on the existing M7 route without loosening accepted source/tenant authentication, transport trust, schema validation or body limits. Every 202 must have a committed, complete inbox receipt + immutable event + Pending work state; all failed/uncommitted or ambiguous writes must fail without a false success acknowledgment.

Implement M8-01 to M8-04 and prepare complete G8 evidence. **Do not implement** M9 worker leases, retries, retry queues, dead-letter dispatch or reconciliation, M10 projections, public tracking read endpoints, dashboards, a new browser workspace, hosting, or externally sourced tracking feeds. The first invalid/unsupported event remains a rejected M7 request without a raw-payload quarantine.

## 2. Execution DAG and parallel plan

```text
G7 accepted -> M8-01 inbox and retention contract -> D-006 approval
                                        |
                          +-------------+-------------+
                          |                           |
                    M8-02 schema                M8-03 HTTP/store
                       G6 accepted                 G6 accepted
                          +-------------+-------------+
                                        |
                                   M8-04 verification
                                        |
                                G8 independent review
                                        |
                                G8 acceptance pending
```

The repository graph defines M8-02 and M8-03 after M8-01 and accepted G6; this plan adds no graph dependencies. Work on tests, pure canonicalization and schema drafts can be prepared concurrently in separate E:-backed worktrees **after** the D-006 schema/response contract is frozen. The final EF migration, shared `TrackingEndpoints` changes and source authorization entry point have a single integration owner to prevent two competing snapshots or duplicate route definitions. Keep planning separate from implementation until the required decision is recorded.

## 3. M8-01: freeze durable intake contract

**Artifacts:** [D-006 proposal](M8_D006_DURABILITY_PROPOSAL.md), [architecture](M8_ARCHITECTURE_DESIGN.md), [API contracts](API_CONTRACTS.md), [domain model](DOMAIN_MODEL.md), [event processing](EVENT_PROCESSING.md).

1. Confirm D-006 recommendation or explicitly select another option. Freeze the source key `(org,source,profile,sourceEventId)`, deterministic digest version, byte-preservation rules, 202 replay/409 conflict semantics, intake capacity, immutable retention and worker-state handoff.
2. Confirm that M8 uses existing `POST /integrations/v1/tracking-events`, the M7 `tnt-2.2-harborops-v1` profile and its accepted D-002 mapping unchanged.
3. Specify exactly which errors can perform a bounded metadata write (a valid-key collision) and ensure rejected M7 input does not create accepted-event rows.
4. Freeze the post-commit response envelope and sanitization: `receiptId`, `status`, `duplicate`, `receivedAtUtc`, `correlationId`; `Cache-Control: no-store`.
5. Define the final bearer revalidation lock ordering, connection timeout, uncertainty semantics and org-level pending cap in the same transaction as the receipt.
6. Record D-006 acceptance distinctly from M8 implementation and future G8 acceptance. Do not use a proposed contract as evidence of decision approval.

**Exit:** approved D-006 or **acceptance pending**, plus a reviewed and versioned interface/schema contract. Without approval, dependent contract implementation cannot claim a frozen design.

## 4. M8-02: additive PostgreSQL and EF schema

**Expected paths:**

- `src/HarborOps.Infrastructure/Integrations/TrackingInboxEntities.cs` (new, proposed).
- `src/HarborOps.Infrastructure/Persistence/HarborOpsDbContext.cs` and model snapshot.
- `src/HarborOps.Infrastructure/Persistence/Migrations/<timestamp>_TrackingInboxAndHistory.cs` (new, timestamp selected in implementation).
- `tests/HarborOps.IntegrationTests/TrackingInboxMigrationTests.cs`, `TrackingInboxSqlTests.cs`, `TrackingInboxUpgradeTests.cs` (new, proposed).

**Work:**

1. Create immutable `tracking_inbox_receipts`, `tracking_accepted_events`, bounded `tracking_receipt_conflicts` and separate `tracking_inbox_work_items` (initially Pending only). Preserve M7 source/credential identities.
2. Add unique, permanent source-scoped replay key, alternate composite keys and tenant/source/credential foreign keys; index receipt chronology and pending work. Add the required credential composite alternate key only if it can be created without changing M7 behavior.
3. Add append-only SQL guards and deferred receipt completeness assertions, plus status and raw-payload bounds and exact-one-work/event constraints. Test direct SQL UPDATE/DELETE and cross-org insert attempts.
4. Model all EF entities against the SQL migration and generated model snapshot. No duplicate or malformed mapping entries, orphan alternate keys or migration drift.
5. Prove clean M0-M8 installation, populated accepted M7->M8 upgrade including M1-M7 rows, rollback/reapply on a **disposable copy only**, and no corruption or read-semantic changes to accepted M0-M7.
6. Define a forward-compatible Pending state for M9 without prematurely introducing worker claims or dead-letter behavior.

**Exit:** schema integrity in real PostgreSQL, one committed immutable receipt/event/work triple, duplicate-key enforcement and safe additive upgrade. Authoring migrations or matching unit tests alone is not G8 verification.

## 5. M8-03: transactional intake and HTTP contract

**Expected paths:**

- `src/HarborOps.Domain/Tracking/TrackingReceiptPolicy.cs` for pure canonical bytes and digest.
- `src/HarborOps.Infrastructure/Integrations/TrackingInboxStore.cs` for atomic writes/replays.
- `src/HarborOps.Infrastructure/Integrations/IntegrationSourceStore.cs` for receipt-transaction-scoped final bearer validation.
- `src/HarborOps.Api/Integrations/TrackingEndpoints.cs` to replace only the valid-event 503 branch, not weaken M7 rejection rules.
- `src/HarborOps.Infrastructure/DependencyInjection.cs` for store registration; `Program.cs` only if composition requires it.
- `tests/HarborOps.UnitTests/TrackingReceiptPolicyTests.cs` and `tests/HarborOps.IntegrationTests/TrackingInboxHttpTests.cs`, `TrackingInboxConcurrencyTests.cs`, `TrackingInboxInterruptionTests.cs`, `TrackingInboxCapacityTests.cs`.

**Work:**

1. Canonicalize the validated M7 `TrackingEvent` with fixed UTF-8 field order, culture-invariant UTC and an explicit serializer version; test equivalent formatting and distinct semantic values. SHA-256 produces the canonical digest.
2. Preserve streaming input bounds and the accepted M7 400/401/403/413/415/422/429 behavior. Do not accept an HTTP cookie as source authorization.
3. After body validation, open a short PostgreSQL transaction, lock organization before source/credential, revalidate the **original bearer** with constant-time digest, active/expiry/revocation checks and exact tenant identity. Prevent a disable/revoke commit from racing between final authorization and receipt commit.
4. Resolve existing scoped key first: identical canonical payload -> return original receipt with `duplicate=true`; different payload -> persist at most one immutable first-collision metadata row and return sanitized 409. Both paths require current authorization.
5. For a new key, enforce the pending cap under org lock, atomically insert receipt + immutable accepted event + Pending work item, and verify completeness. Commit before returning new 202.
6. Treat PostgreSQL errors, timeouts, lock failures and unknown COMMIT outcome as non-2xx, with retry advice; never automatically roll back a confirmed commit and never log token/raw payload. The safe same-key retry differentiates commit-success from rollback.
7. Do not resolve the event against a shipment, change M5/M6 history or signal a worker yet.

**Exit:** exercised real HTTP + PostgreSQL contracts with no false acknowledgment, no duplicate accepted rows and safe source revocation under contention.

## 6. M8-04: G8 implementation verification

**Expected tests and scripts:**

- `tests/HarborOps.IntegrationTests/TrackingInbox*Tests.cs`, unit/contract suites and M7 regression tests.
- `tests/browser/specs/tracking-ingress.spec.mjs` updated for 202/409 and existing invalid-event denial; three browser engines if shared routing changed.
- `scripts/verify-m8-local.sh`, `scripts/check-m8-schema-contract.mjs`, `scripts/run-g8-mutations.mjs`, `scripts/summarize-g8-evidence.mjs`, isolated M8 browser and fault/recovery scripts (all new, proposed).
- `docs/M8_IMPLEMENTATION_STATUS.md` and `docs/M8_EXECUTED_VERIFICATION.md` only when real execution evidence exists.

**Work:**

1. Execute all [G8-S01 to G8-S25](M8_VERIFICATION_PLAN.md) on one exact final implementation revision. Run the clean .NET build and cheap schema/unit/contract checks before expensive PostgreSQL, browser and image stages.
2. Run fresh and repeated integration suites with real PostgreSQL, populated M7->M8 migration/rollback in disposable databases, two-org source isolation, competing credential revocation, concurrent duplicate/collision requests, mid-transaction kill/cancel, ambiguous commit retry and restart preservation.
3. Prove controlled security mutants are killed for unique-key bypass, missing final auth, immutability bypass, premature 202, quota bypass and absent event/work row writes. A mutant surviving is a G8 blocker, not success.
4. Preserve required G0-G7 regression coverage and rerun relevant Chromium, Firefox and WebKit browser/proxy checks. Current NuGet/npm advisories and exact built-image HIGH/CRITICAL scans must report trusted data and no unresolved severe vulnerabilities. Never weaken a security control merely to make a test pass.
5. Use isolated `ho-verify-g8` Compose/project/network/image labels, separate subnet/ports and an E:-backed evidence root. Confirm all Docker, WSL, .NET, npm and browser outputs reside on E:. No GitHub Actions, persistent C: project storage, broad Docker pruning or interference with retained volumes.
6. Archive raw sanitized TRX, browser results, database/transaction proofs, source and image SHAs, mutation results, stage exits, advisory/scanner reports, storage compliance, teardown, an SHA-256 manifest and independent review. Historical results remain unmodified; no retroactive PASS relabeling.
7. Update affected architecture, API, schema, security, operations, tests and roadmap/status records. Ask for **explicit G8 acceptance** after the full independently reviewed gate passes, before merging implementation to protected `main`.

**Exit:** all mandatory G8 scenarios executed and reviewed with no blockers, G8 **acceptance pending** until a separate explicit decision.

## 7. Failure classification and stop conditions

| Severity | Example | Response |
| --- | --- | --- |
| Critical | 2xx before commit, cross-tenant receipt, invalid bearer accepted, lost accepted event or non-atomic triple | Stop implementation/gate; repair, rerun full affected suite |
| High | Revocation lock race, uniqueness breach, bypassable immutability, ambiguous replay mishandled, populated upgrade data loss | Stop G8; reproduce and remediate |
| Medium | Unsafe response fields, unbounded collision metadata, missing cap behavior, inadequate browser/proxy regression | Correct and verify before closure |
| Administrative | D-006 not accepted, skipped stage, absent evidence, incomplete independent review, unapproved hosting | Mark BLOCKED or acceptance pending; do not merge |

## 8. Handoff and constraints

The accepted M7 code remains the baseline until M8 implementation is separately approved and verified. Source implementation may progress in a new named feature branch after D-006 is decided, without GitHub Actions. No third-party queue, worker, M10 projection or final accessibility validation belongs in G8. M8 success means a durable, replay-safe **receipt**, not completed shipment processing. The next event-processing gate after accepted G8 is M9/G9; M19/G19 separately owns comprehensive backup/restore rehearsals.
