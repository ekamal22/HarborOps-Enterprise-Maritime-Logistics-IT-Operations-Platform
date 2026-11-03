# Tracking Event Processing

## Purpose

Transform third-party or simulated maritime tracking signals into a trustworthy, inspectable shipment timeline while preserving the original source record.

**Milestone boundary:** [M7](M7_ARCHITECTURE_DESIGN.md) establishes source authentication and strict contract validation without a durable event receipt. Valid M7 requests must return `503 intake_unavailable` until M8 implements transactional inbox persistence. This document describes the complete M7-M15 target pipeline, not functionality already implemented in M7; M8 owns immutable receipts, M9 worker processing and M10 projections.

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
