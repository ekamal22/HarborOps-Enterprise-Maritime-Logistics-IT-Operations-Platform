# Tracking Event Processing

**M11/M12 coordination approved (2026-11-08):** [Combined integration and verification plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md). M11 optional reevaluation must not activate M9 transport processing. The combined G11/G12 campaign must verify parked ARRI/DEPA, the unresolved-work-item cap and null ETA on final integrated source.

**Parallel M11/M12 source status (2026-11-08):** [Source and integration register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md) records draft M11 PR #31 alongside draft M12 PR #32, with different additive migration IDs and an unresolved shared API/UI/role integration boundary. Both are source candidates, not merged or runtime-verified; G11-S01 BLOCKED, G11-S02..S25 NOT EXECUTED, G12 technical verification pending. Accepted `main` and the M9 transport hold remain unchanged.

**Current M11 source audit (2026-11-08):** [Sixth correctness audit](M11_SIXTH_CORRECTNESS_AUDIT_20261108.md) documents informational severity on inactive outcomes, versioned rule-metadata reconciliation and typed due-work continuation. The source changes and new tests are unexecuted. G11-S01 BLOCKED, G11-S02 through S25 NOT EXECUTED, acceptance pending. The M9 transport-processing hold remains in force.

**M11/M12 coordination approved (2026-11-08):** [Combined integration and verification plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md). M11 optional reevaluation must not activate M9 transport processing. The combined G11/G12 campaign must verify parked ARRI/DEPA, the unresolved-work-item cap and null ETA on final integrated source.

**Parallel M11/M12 source status (2026-11-08):** [Source and integration register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md) records independent unmerged source candidates in draft PRs #31 and #32. M11 policy approval is blocked, both technical gate campaigns are unexecuted, and acceptance pending. Protected `main` and the M9 transport hold are unchanged.

**Current integrated baseline (2026-11-07):** G0-G10 and G16 are accepted on protected `main`. The M10 signed-correction remediation merged through [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) following S01/S25 independent review; see [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md). Earlier G10 pending statements are historical source-locked records. The M9 transport-processing hold remains mandatory. No deployment or release is authorized.

**Current integrated baseline (2026-11-07):** G0-G10 and G16 are accepted on protected `main`. The M10 signed-correction remediation merged through [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) following S01/S25 independent review; see [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md). Earlier G10 pending statements are historical source-locked records. The M9 transport-processing hold remains mandatory. No deployment or release is authorized.

## M11 source-only history and list read boundaries

M11 list pagination now excludes exception occurrences created after the first page's database-clock cutoff; it cannot freeze later lifecycle updates. Append-only exception history is independently pageable without rewriting accepted tracking events, M9 worker effects, M10 projections or M16 audit records. The bounded evaluator and M9 transport hold remain unchanged. Test this concurrency behavior in the G11 campaign.

## M11 provisional classification boundary

The isolated M11 evaluator uses M5 workflow UTC records and corrected M10 equipment projections, with an opt-in bounded candidate scanner that is independent of M9 worker processing. It does not consume parked transport ARRI/DEPA events, loosen the 10,000-work cap or infer vessel ETA. Source and rule versions inform reevaluation. These behaviors require executable G11 evidence.

## Purpose

Transform third-party or simulated maritime tracking signals into a trustworthy, inspectable shipment timeline while preserving the original source record.

**Milestone boundary:** [M7](M7_ARCHITECTURE_DESIGN.md) and G7 were accepted 2026-11-03 and integrated into `main` ([closure](M7_G7_GATE_CLOSURE.md)). M7 established source authentication and strict contract validation, historically returning `503 intake_unavailable` before M8. M8 now provides accepted post-commit durable receipts and M9 provides accepted lease-fenced processing and reconciliation on `main`. M10 timeline/projection rebuild and downstream M11-M15 behavior are not yet accepted.

## M8 receipt transition (accepted G8)

[M8 architecture](M8_ARCHITECTURE_DESIGN.md) proposes one atomic acceptance transaction containing immutable receipt, immutable accepted event and separate Pending work state. The [D-006 Option A proposal](M8_D006_DURABILITY_PROPOSAL.md) defines permanent source-scoped identity, canonical replay digest, 202 only after commit and 409 for changed-payload collision. **M8, D-006 Option A and G8 were accepted 2026-11-04 after source-locked technical verification and independent evidence review.** Historical M7-only valid requests returned 503 before M8 intake was integrated. M9 later adds worker claims, retries and dead-letter behavior, and M10 projection rebuild; a receipt means stored for future processing, not business reconciliation. See [M8 planning status](M8_PLANNING_STATUS.md).

## M10 correction replay and audit contract, 2026-11-07

Accepted M10 indexes resolved equipment effects without rewriting M8/M9 history. The signed L07/L14 correction operation and transactional M16 audit evidence merged via [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28). Projector operation remains opt-in; ARRI and DEPA stay parked under the M9 10,000-item hold. See [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md).

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
