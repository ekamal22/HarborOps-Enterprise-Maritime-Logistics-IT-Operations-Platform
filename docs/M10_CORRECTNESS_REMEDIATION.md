# M10 Correctness Remediation

**Recorded:** 2026-11-05  
**Original audit:** [M10 Source Correctness Audit](M10_SOURCE_CORRECTNESS_AUDIT.md), source SHA `83d78bb7b271b96d5086044801fc503f07d8a417`.  
**Change location:** PR #23, `feat/m10-timeline-projections`.  
**Disposition:** All 11 audited findings have corresponding source corrections. **No runtime tests or migrations have been executed against the corrected SHA in this GitHub-only session.** G10 acceptance and merge remain blocked pending verified evidence.

The historical source-locked audit remains unchanged. This file records the subsequent remediation without retroactively rewriting its findings.

## Code-level remediation mapping

| Audit | Committed correction | Required verification |
| --- | --- | --- |
| C01 | Replace the separate 10,000-resolved-event rejection with `ShipmentTimelineStore.StreamAsync`. The store reduces complete history in streamed order while retaining a bounded page and per-container observation state. Pages use protected keyset positions. | Real PostgreSQL and HTTP histories above 10,000 events, repeated late arrivals, pagination and bounded memory/latency profiles. Full computation is still O(retained events) per call and per-container memory grows with distinct containers. |
| C02 | Add unique composite `(receipt_id,organization_id,shipment_id)` effect identity and matching composite FK from `shipment_projection_applications`. Applications are unique by receipt and policy version. | Mismatched but otherwise valid effect/shipment insert and cross-tenant direct SQL rejection on fresh/populated migrations. |
| C03 | Version application identities, track correction generations and last verification, periodically rescan dirty shipments, and compare every derived projection field rather than trusting the input digest alone. | Policy transition, direct correction, derived-field corruption, concurrent M9 effect COMMIT, crash/replay and unchanged rebuild revision controls. |
| C04 | Add organization-first/ship-second correction locking plus a durable per-shipment correction-generation row updated by every insert, causing conflicting direct SQL writers to fail under Repeatable Read/Serializable as well as Read Committed. | Two-connection cycle/chain tests, committed history comparisons, lock-order and timeout/race injection. |
| C05 | Refuse M10 downgrade when append-only corrections, correction heads, projection application, replay evidence or projection rows are retained. | Populated rollback blocked with byte-for-byte source and history preservation; separate empty downgrade control. |
| C06 | Align SQL event-key comparison to ASCII binary `COLLATE "C"` and the domain's ordinal comparator. Replace index-position pagination with authenticated scope-bound keysets. | Mixed-case IDs, identical timestamps, UUID tie order, repeat/late arrivals, snapshot and paging on real PostgreSQL. |
| C07 | Retain the immutable M9 container identity for each event. Reducer detects same-container contradictions and marks differing multi-container observations `Mixed` instead of a false single-container or shipment state. | Two- and many-container fixtures, replay and correction changes, independent manual M5 workflow state. |
| C08 | Remove the raw input digest from HTTP and use 20-minute authenticated encrypted shipment/organization-scoped snapshot and cursor tokens. | Tampering, expiry, cross-shipment and cross-tenant scope, no payload/digest exposure. |
| C09 | Require UUID `Idempotency-Key` for manager correction and rebuild routes; store request digest and original revision atomically; return replay outcome for matching retries; enforce a 10/minute authenticated-user rate policy. | Lost response after COMMIT, replay with same/different request, concurrent duplicate submissions, revocation and rate limit behavior. |
| C10 | Require separate `harborops_projector` PostgreSQL credentials before starting the optional projector. Provide explicit least-privilege grants and verification. Default Compose projection sweeper is off. | Actual restricted-role connection, missing/wrong credentials startup denial and negative M8/M9 mutation rights. |
| C11 | Shipment UI loads a protected projection snapshot first, then its matching event page. The API rejects stale snapshots and paged keysets. Container summary responses are limited to 50 with a count and truncation indicator. | Browser concurrency, late corrections, organization changes, stale cursor refresh and three-engine functional runs. |

## Tests and local verification authored

- `TrackingTimelinePolicyTests.cs`: canonical order, per-container mixed/conflict, correction semantics, stable digest and 10,050-event streaming reduction.
- `TrackingProjectionG10Tests.cs`: real M9 effect-to-M10 projection path, unchanged M5 workflow and M9 history, repeat rebuild, direct SQL invalid effect/shipment association, immutable correction denial, chain rejection, state corruption repair, correction idempotency replay, snapshot invalidation and concurrent direct SQL correction conflict.
- `compose.g10.verify.yaml`: isolated loopback-only stack. Projector remains off until separate approved role/credentials.
- `scripts/configure-m10-projector-db.sql`: restricted-role provision and table privilege grants, with no committed password.
- `scripts/verify-g10-local.sh`: E:-only checkout and evidence, local build/unit/contract/integration/web checks, opt-in local stack, projector privilege metadata checks, disposable browser account/organization fixtures, Chromium/Firefox/WebKit functional tests, secret cleanup and evidence manifest.

## Unresolved acceptance and operational checks

These source patches are **not evidence of a successful run**. G10-S01 correction/aggregation/cursor/authorization choices still require explicit policy review. Full G10-S01 through G10-S25 evidence remains unexecuted against the corrected source. Additional mandatory coverage includes populated M9-to-M10 upgrade and protected downgrade rehearsal, exact schema/constraint checks, real database role operation, fault injection across projection commits and outages, source-locked performance/scalability, dependency/image security scans, controlled security mutations, browser verification against a final clean SHA, all applicable G0-G9 regressions, evidence sanitization and independent raw-evidence reconciliation.

M8 receipts, accepted events and M9 effects and leased-worker contracts are not changed. The M9 10,000 **unresolved** work cap and mandatory sustained transport-event processing hold remain in force; M10's resolved-event streaming does not lift that hold. M16/G16 and its supplemental raw evidence remain independent and unaccepted. PR #21 and PR #22 documentation/planning integration remain separate.

**Decision:** Corrected source candidate only. No G10 acceptance, merge, transport traffic activation or public/production deployment authorization.
