# D-006 Proposal: Durable Inbox, Receipt Identity and Future Dead-Letter Model

**State:** Accepted on 2026-11-04, Option A. Historical proposal wording and pre-decision analysis are retained below; the binding acceptance is in [D-006 decision](DECISIONS.md) and [M8/G8 gate closure](M8_G8_GATE_CLOSURE.md).
**Prepared:** 2026-11-03
**Milestone:** M8-01, with binding downstream contracts for M8-02, M8-03, M9-01 and M9-03.
**Accepted baseline:** G0-G7 on `main` at `47f030f9fa4b588d589f3ae082d03b3d5b94736a`.
**Related:** [M8 architecture](M8_ARCHITECTURE_DESIGN.md), [implementation plan](M8_IMPLEMENTATION_PLAN.md), [G8 verification](M8_VERIFICATION_PLAN.md), [execution graph](execution-graph.json), [accepted G7](M7_G7_GATE_CLOSURE.md).

## Decision requested

Choose a durable acceptance, replay, bounded-retention and worker handoff contract that does not confuse a committed receipt with processed business effects. The approved M7 route can validate an event but currently returns HTTP 503 because no receipt exists. M8 must not switch to success responses unless the complete receipt, immutable event and pending work-item records commit atomically.

This decision specifies how M9 will subsequently claim, retry, reconcile and dead-letter accepted work. M8 does not implement that worker, retry scheduler or dead-letter processing.

## Alternatives

| Option | Design | Advantages | Disadvantages | Assessment |
| --- | --- | --- | --- | --- |
| **A: Transactional PostgreSQL inbox with separate immutable ledger and mutable work state** | Three transactional records for every accepted event; future worker uses the same database and separate processing/dead-letter metadata | No external broker, atomic local receipt, clear replay semantics, retained provenance, low-cost local-first runtime | Serialized admission per organization for quota, database capacity must be watched, later worker and recovery still needed | **Accepted 2026-11-04** |
| B: Build full worker queue and dead-letter transitions inside M8 | Add retry leases, worker concurrency and dead-letter execution before G8 | Fewer future migration steps | Blurs M8/M9 acceptance, expands crash-window surface and conflicts with execution graph | Reject for M8 |
| C: External managed queue or message broker as receipt authority | Acknowledge publisher after enqueue outside PostgreSQL | Potential high throughput | Distributed commit gap, new infrastructure, cost and replay ambiguity | Reject for initial local milestone |

## Accepted Option A contract (original proposal preserved)

### Source identity and duplicate definition

- Unique key: `(organization_id, source_id, profile, source_event_id)`. It is permanent, includes the M7 profile namespace and is backed by a PostgreSQL unique constraint, not an application-only lookup. A key cannot be released by archiving or deleting an event.
- Canonical digest: SHA-256 over a deterministic, versioned UTF-8 serialization of the M7 validated and normalized event fields. Field order, string encoding, UTC normalization and optional-null handling are fixed. Caller formatting, property order and equivalent UTC representations do not change the digest. Raw bytes are also preserved in an immutable accepted event.
- Same key and same canonical event: return the original committed receipt ID and received timestamp with HTTP 202; mark `duplicate: true`. No new event, receipt, work item or processing effect.
- Same key but different canonical event: return HTTP 409 `source_event_collision`, preserve the original receipt and first-seen immutable collision metadata without storing the conflicting raw request. Repeated conflicts do not create unbounded rows.
- Cross-source and cross-organization reuse of the same external event identifier is independent. It must not reveal a foreign record.

### Receipt, history, work state and acknowledgment

- A new event creates one immutable receipt, one immutable accepted event containing provenance and bounded raw accepted bytes, and one pending work-state row in one PostgreSQL transaction. A database-enforced one-to-one relationship and deferred completeness check prohibit orphaned committed receipts.
- The work-state row starts as `Pending`, with zero attempts, no lease and no processing outcome. The immutable records are not updated when M9 eventually works the event.
- HTTP 202 means only "durably received for later processing". It does not mean recognized equipment, matched shipment, processed projection or successful delivery to a downstream system. The response includes `receiptId`, `status: received`, `duplicate`, `receivedAtUtc` and `correlationId`; no payload, token or internal database reference is echoed.
- HTTP 202 is sent only after transaction commit has returned success. A database outage, rollback, unknown commit result or cancellation must never produce a 2xx. If commit outcome is uncertain, return a retryable sanitized error where the connection permits; retrying the same key reveals the original receipt if commit succeeded.

### Future queue and dead-letter policy

- In M8 only `Pending` is created. In M9, add explicit `Leased`, `RetryWaiting`, `Processed`, `DeadLetter` transitions, an atomic claim lease, bounded attempts/backoff, lock expiry, and an inspectable tenant-scoped dead-letter disposition. Do not silently delete or overwrite the accepted event.
- M9 worker metadata and dead-letter attempts belong in a separately mutable table or additively extended work-state schema. The immutable receipt and accepted event remain unchanged. An event not matched to a shipment/container remains durable, pending reconciliation rather than being silently rejected as a cross-tenant match.
- Unsupported/malformed M7 inputs remain 4xx with no accepted receipt or raw-payload quarantine. First-seen metadata for a source key collision is separate from an accepted event and is not a substitute for a valid receipt. Full invalid-input quarantine and operational remediation remain later scoped features.

### Retention and cost envelope

- No automatic deletion of accepted receipts, immutable events or source identity keys in M8. No retention job, credential cascade or cross-tenant purge. Retention and disposal policy requires a separate explicit future decision and must preserve replay identity, applicable evidence and recovery expectations.
- For the local-first M8 baseline, propose a configurable hard admission ceiling of **10,000 pending work items per organization**. Check it transactionally under an organization lock, count before inserting a new key, and fail closed with sanitized HTTP 503 `intake_capacity_exceeded` and `Retry-After` when at capacity. Exact duplicates and collisions are resolved against existing records before the capacity check. The number is a proposed demonstrator default, not a proven production capacity limit.
- M9 state transitions may free pending capacity without deleting immutable records. Ensure worker completion and M8 intake agree on concurrency and lock order. Storage footprint, backup policy and more elaborate retention remain M19/M21 considerations.
- Keep raw accepted payload bytes at or below M7's 16 KiB limit. No raw payload, bearer, digest-derived secret or SQL text in logs, HTTP errors or browser assets. Protect database backups and avoid public read endpoints in M8.

## Required implementation consequences

1. Preserve the accepted M7 authentication, transport, schema profile and errors for all pre-receipt rejection paths.
2. Revalidate the **original bearer** and active organization/source/credential inside the receipt transaction. A previously successful M7 authorization call is not enough; lock the organization before source and credential in a consistent order so disable/revoke cannot commit between final authorization and receipt commit.
3. Add PostgreSQL unique tenant/source keys, composite foreign keys, append-only triggers, one-to-one receipt/event/work constraints and transactional quota enforcement.
4. Store canonical digest and bounded accepted raw bytes, not unbounded parser input. Persist first collision metadata as a bounded, immutable record without changing the accepted history.
5. Test commit ambiguity, concurrent identical and conflicting deliveries, source disablement, quota, SQL bypass, restart and historical-schema preservation against disposable PostgreSQL.
6. M8 releases no polling/worker endpoints, external broker, user-accessible event viewer, projection update or acknowledgment of business completion.

## Acceptance conditions

D-006 must explicitly confirm or change the persistence model, replay/collision semantics, response contract, pending-cap default, retention and M9 handoff before M8-02/M8-03 changes that depend on them. Any changed choice requires corresponding schema, G8 test and API-contract revisions. A planned design or merged documentation branch does not constitute D-006 acceptance, G8 verification or permission to merge M8 implementation.

**Decision:** Option A accepted 2026-11-04 by [explicit acceptance](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/14#issuecomment-6633731945) after [independent evidence review](M8_G8_INDEPENDENT_EVIDENCE_REVIEW.md). A PostgreSQL unique index enforces the scoped key; M8's Pending-only checks require a separate M9 state-transition migration. The 10,000 Pending ceiling is a demonstrator default. Other proposal terms above remain binding as applicable to M8; M9 processing and production capacity are not accepted by this milestone.
