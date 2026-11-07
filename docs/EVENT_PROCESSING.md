# Tracking Event Processing

**Current accepted baseline (2026-11-06):** M0-M9/G0-G9 and M16/G16 are accepted and integrated into protected `main`; see [G9 closure](M9_G9_GATE_CLOSURE.md) and [G16 closure](M16_G16_GATE_CLOSURE.md). M10 source was merged into `main` through [PR #23](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/23) on 2026-11-06 (merge `5edf003`); G10 acceptance is pending and its runtime verification is recorded in [the M10 executed verification](M10_EXECUTED_VERIFICATION.md). The mandatory M9 hold on sustained transport-event processing remains until a separately approved, verified backlog solution prevents exhaustion of the 10,000 unresolved-work-item limit. No deployment or public application hosting is authorized.

## Purpose

Transform third-party or simulated maritime tracking signals into a trustworthy, inspectable shipment timeline while preserving the original source record.

**Milestone boundary:** [M7](M7_ARCHITECTURE_DESIGN.md) and G7 were accepted 2026-11-03 and integrated into `main` ([closure](M7_G7_GATE_CLOSURE.md)). M7 established source authentication and strict contract validation, historically returning `503 intake_unavailable` before M8. M8 now provides accepted post-commit durable receipts and M9 provides accepted lease-fenced processing and reconciliation on `main`. M10 timeline/projection rebuild and downstream M11-M15 behavior are not yet accepted.

## M8 receipt transition (accepted G8)

[M8 architecture](M8_ARCHITECTURE_DESIGN.md) proposes one atomic acceptance transaction containing immutable receipt, immutable accepted event and separate Pending work state. The [D-006 Option A proposal](M8_D006_DURABILITY_PROPOSAL.md) defines permanent source-scoped identity, canonical replay digest, 202 only after commit and 409 for changed-payload collision. **M8, D-006 Option A and G8 were accepted 2026-11-04 after source-locked technical verification and independent evidence review.** Historical M7-only valid requests returned 503 before M8 intake was integrated. M9 later adds worker claims, retries and dead-letter behavior, and M10 projection rebuild; a receipt means stored for future processing, not business reconciliation. See [M8 planning status](M8_PLANNING_STATUS.md).

## M10 correction replay and audit contract, 2026-11-07

The merged original M10 source indexes resolved equipment effects without rewriting M8/M9 accepted histories. The separate L07/L14 remediation branch adds signed, scope-bound correction requests, a constrained PostgreSQL operation and one same-transaction M16 audit event per successful correction, while preserving append-only corrections, generation checks and replay semantics. The ordinary API role no longer has direct correction INSERT. New-source .NET/PostgreSQL verification is NOT EXECUTED, so these changes are not G10 accepted. [Implementation](M10_G10_CORRECTION_AUTHORITY_IMPLEMENTATION.md).

M10 reports null ETA and does not process parked ARRI/DEPA traffic. The M9 transport hold and 10,000 unresolved-work cap remain mandatory. ETA-oriented examples elsewhere on this page describe planned downstream behavior, not verified M10 capability.

## M9 worker transition (G9 accepted and integrated)

The [M9 architecture](M9_ARCHITECTURE_DESIGN.md) separates the M8 immutable receipt/event history from a mutable, lease-fenced work row and an append-only receipt-unique processing-resolution effect. A separate .NET worker claims due Pending/RetryWaiting or expired leases under PostgreSQL locks, classifies outcomes and commits resolution effects atomically with terminal state. Unknown equipment/transport/shipment associations remain NeedsReconciliation instead of being guessed. Bounded transient failure retries end in DeadLetter, with explicit scoped redrive and immutable attempt evidence. A worker's Processed state means durable reference resolution, not M10 timeline/projection updates.

All six [M9-01 contract decision](M9_LOCAL_CONTRACT_DECISION.md) choices were approved on 2026-11-05, with the mandatory condition that sustained transport-event processing remain disabled until M10 or another explicitly approved mechanism can manage the parked unresolved backlog within the 10,000-item limit. G9 was accepted on 2026-11-05 following the source-locked [technical verification](M9_EXECUTED_VERIFICATION.md), [browser supplement](M9_BROWSER_SUPPLEMENT.md) and independent [evidence review](M9_G9_INDEPENDENT_EVIDENCE_REVIEW.md); the worker is integrated into `main`. The M8 intake contract remains unchanged, and no production deployment is authorized. See [G9 closure](M9_G9_GATE_CLOSURE.md).

## Processing pipeline

    Source adapter / simulator
       -> authenticated intake
       -> input schema + size checks
       -> durable inbox receipt
       -> source-specific normalization
       -> identity + ownership reconciliation
       -> idempotency check
       -> immutable accepted event
       -> projection update
       -> exception evaluation
       -> authorized notification / analytics update

## Ingestion contract

Each receipt records source identity, tenant/organization entitlement, received timestamp, payload version, source event identifier where present, correlation identifier, and processing outcome.

Do not assume every upstream feed supplies globally unique IDs. Define a source-specific stable deduplication key, and record collisions for investigation.

## State transitions

| Condition | Expected result |
| --- | --- |
| New valid event | Accept once, update affected projection, return acknowledgment |
| Identical replay | Report previously accepted outcome, no duplicate business effect |
| Payload differs under same source key | Quarantine conflict, no silent overwrite |
| Unsupported event variant | Preserve safe diagnostic reference, reject/quarantine |
| Event for unknown container | Park for reconciliation with bounded retry policy |
| Event arrives late | Preserve occurrence time, recompute affected projection deterministically |
| Dependency unavailable | Retry with backoff; retain durable receipt and reason |
| Invalid organization scope | Reject and record security-relevant audit outcome |
| Explicit correction | Link to affected event and rebuild projection with traceability |

## Delivery and consistency

Assume at-least-once delivery. Exactly-once behavior is not assumed at the network boundary. Achieve effect-once semantics through durable keys, unique indexes, transactional writes, and idempotent handlers.

The worker may lag the ingestion endpoint. Expose intake status, processing lag, failure counts, and last successful projection version.

## Retry and dead-letter approach

- Separate transient from permanent failures.
- Use bounded exponential backoff with jitter.
- Stop after a defined retry budget and route persistent failures to an inspectable dead-letter/reconciliation queue.
- Provide guarded replay tooling with audit evidence and scope checks.
- Never retry indefinitely or remove failed messages without a recorded disposition.

## Projection correctness

- Prefer recomputation from accepted event history over mutable source-of-truth shortcuts.
- Use deterministic ordering and conflict rules.
- Record projection version and triggering event references.
- Derive status, last milestone, ETA indicator, and rule-based risk flags independently.
- Unknown status remains unknown; no fabricated position or certainty.

## Notification rules

Only inform recipients authorized for the affected organization/resource and subscribed to the relevant alert type. Deduplicate alerts on stable event or exception keys. Preserve notification evidence and allow acknowledgment. Avoid alerting unrelated users to third-party shipment issues.

## Required verification scenarios

1. Single event lifecycle from intake to dashboard.
2. Duplicate message delivered two and ten times.
3. Delayed and out-of-order events.
4. Correction event changing projected ETA.
5. Unknown container and invalid payload.
6. Worker restart after receipt but before projection commit.
7. Database outage and subsequent recovery.
8. Cross-organization forged event.
9. Alert deduplication and recipient authorization.
10. Rebuild projection from retained history, matching previous expected values.
