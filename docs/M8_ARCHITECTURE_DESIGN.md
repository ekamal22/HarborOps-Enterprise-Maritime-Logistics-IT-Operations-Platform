# M8 Architecture: Durable Event Inbox and Immutable History

**State:** Architecture implemented as a source candidate on `feat/m8-durable-event-inbox`, pending compile/runtime validation, D-006 approval and G8 acceptance. See [implementation register](M8_IMPLEMENTATION_STATUS.md). No verified M8 runtime is claimed.
**Prepared:** 2026-11-03
**Planning baseline:** Protected `main` at `47f030f9fa4b588d589f3ae082d03b3d5b94736a`, M0/G0 through M7/G7 accepted.
**Work packages:** M8-01 specification, M8-02 persistence schema, M8-03 HTTP receipt transaction, M8-04 integrated verification.
**Authorities:** [D-006 proposal](M8_D006_DURABILITY_PROPOSAL.md), [roadmap](ROADMAP.md), [execution graph](execution-graph.json), [requirements](REQUIREMENTS.md), [G8 verification](M8_VERIFICATION_PLAN.md), [accepted M7 contract](M7_ARCHITECTURE_DESIGN.md), [G7 closure](M7_G7_GATE_CLOSURE.md).

## 1. Scope and prerequisite boundary

M8 makes validated, source-authorized tracking events durably receivable with immutable provenance, permanent source-scoped replay keys and a pending-work handoff. It extends the accepted M7 endpoint `POST /integrations/v1/tracking-events` without changing the approved external event profile `tnt-2.2-harborops-v1`, its supported GTIN/GTOT/ARRI/DEPA variants or its negative security behavior.

G6 and G7 are accepted, satisfying the graph's upstream implementation prerequisites. M8-01 planning can proceed now. **D-006 remains acceptance pending**, and no dependent schema or acknowledgment implementation is authorized as accepted merely by this plan.

M9, not M8, implements worker claims, retry scheduling, dead-letter dispatch, source-resource reconciliation and effect-once business updates. M10 owns shipment status/timeline projections and corrections. M8 does not create a human UI, public events read API, hosted endpoint, provider integration or claim DCSA wire-format conformance. Final dedicated accessibility testing remains M24-04.

## 2. Component and transaction topology

```text
Source Bearer + one approved M7 JSON event
  -> M7 transport, origin, bearer, rate/body/profile validation
  -> M7 canonical TrackingEvent (organization/source from authenticated credential)
  -> M8 transactional receipt store
       begin PostgreSQL transaction
       lock organization -> source -> credential in stable order
       recheck original bearer digest, expiry, revocation, source capability and status
       lookup source-scoped event key
         identical canonical event -> reuse original receipt
         changed event -> record bounded first collision, return 409
         new key -> enforce pending capacity
                    insert immutable receipt + accepted event + Pending work item
       commit
  -> AFTER commit, HTTP 202 received or replayed
       errors/ambiguity -> non-2xx, safe retry via same key

Future M9 worker -> mutable processing state, retries, dead letters and reconciliation
Future M10      -> projections, timeline and correction representation
```

The M7 `IntegrationSourceStore.AuthenticateAsync` currently commits its own `FOR SHARE` transaction before returning only source and organization IDs. **That result is insufficient to authorize a write later in the HTTP request.** M8 must introduce a receipt-transaction-scoped revalidation path for the original bearer. Do not rely on headers or the in-memory normalized event for final entitlement.

### Lock and concurrency protocol

1. Initial M7 authentication remains useful for early rejection and rate-limit partitioning. Once the body is validated, begin the receipt transaction.
2. Acquire the organization row lock first (`FOR UPDATE` is proposed to serialize per-organization pending-cap checks), then source and credential locks in a documented order. Reload live rows while locks are held. Recompute and constant-time-verify the secret digest without logging the bearer. Validate capability, active organization/source, credential not-before/expiry/revocation and tenant/source association.
3. Source CLI revocation/disable already takes organization before source and mutates credentials. Preserve compatible lock order and test two PostgreSQL sessions with actual lock waits. The receipt transaction must commit before a conflicting revocation can commit, or fail to authorize after revocation.
4. Use the unique source-key index as the final arbiter of conflicting concurrent inserts. Avoid check-then-insert outside the transaction. Every duplicate response must verify the requester's current credential first, including a replay after revocation.
5. Prefer a short `READ COMMITTED` transaction with bounded lock and statement timeouts; guarantee rollback and explicit resource disposal on timeout, cancellation and errors. A receipt never commits with mismatched event/work rows.

## 3. Proposed additive PostgreSQL persistence

The source candidate now contains `20261103190000_TrackingInboxAndHistory` after accepted M7 `20261103090000_IntegrationSources`. Its four tables, constraints and EF mappings are authored but have not been compiled or tested against a PostgreSQL server. D-006 is acceptance pending; the schema remains provisional until approved.

| Table | Minimum fields | Constraints and security |
| --- | --- | --- |
| `tracking_inbox_receipts` | `id` UUID, `organization_id`, `source_id`, `credential_id`, `profile`, `source_event_id`, `canonical_digest` bytea(32), `received_at_utc`, `correlation_id` | Immutable, tenant/source/credential composite FKs, unique (`organization_id,source_id,profile,source_event_id`), alternate key for composite receipt FKs, digest length and UTC checks, no cascade deletion |
| `tracking_accepted_events` | `id`, `receipt_id`, `organization_id`, `source_id`, `event_family`, `event_code`, `classifier`, `external_schema_version`, `occurred_at_utc`, `equipment_reference` or `transport_reference`, `raw_payload_utf8` bounded bytea, `canonical_payload` jsonb | Append-only, exactly one event per receipt, composite FK to receipt, mutually exclusive reference fields, immutable normalized contract fields and raw bytes |
| `tracking_inbox_work_items` | `receipt_id`, `organization_id`, `state=Pending`, `attempt_count=0`, `created_at_utc`, `updated_at_utc` | Exactly one initial row per receipt, separate from immutable history, state constrained to Pending at G8, index (`organization_id,state,created_at_utc`). M9 extends mutable state, leases, retry and dead letters |
| `tracking_receipt_conflicts` | `id`, `receipt_id`, `organization_id`, `attempted_digest`, `first_seen_at_utc`, `correlation_id` | Optional bounded first-seen collision record per receipt, unique receipt ID, immutable, no conflicting raw payload, composite tenant FK |

**Important EF/SQL mapping detail:** Existing M7 `integration_credentials` has a global primary key but only a separate tenant/source composite foreign key. M8's receipt schema must add or verify a database-enforceable unique alternate key for `(organization_id,source_id,credential_id)`, without changing M7 credential semantics. Do not claim an EF relationship guarantees tenant scope unless the PostgreSQL migration has the matching composite key and constraint.

All four tables are created in one migration. Commit-time deferred integrity checks must require one accepted event and one Pending work item for every newly committed receipt, including direct-SQL insert attempts. PostgreSQL triggers should prohibit UPDATE/DELETE of receipts/events/conflicts; work-state transitions are separate and belong to later milestones. Indexes and triggers must be represented faithfully in EF configuration/snapshot or explicit documented SQL as appropriate. Prove migrations preserve accepted M0-M7 rows on fresh, populated upgrade and test-only rollback/reapply paths. Never perform destructive Down migration against retained data.

### Canonical identity and provenance

The canonical serializer must be a **pure, versioned policy** over the already validated M7 `TrackingEvent`: fixed property names/order, normalized UTC instant, explicit nullable-reference policy, stable UTF-8, culture-independent encoding. The digest is SHA-256 of canonical bytes. Persist the accepted raw payload once, after M7's streaming 16 KiB validation, and normalized fields for deterministic replay. Source identity, tenant identity, credential identity, receipt timestamp and correlation ID come from server state, never caller-controlled JSON. Do not treat SHA equality alone as permission to overwrite or silently replace history.

Each receipt and event has immutable UUID identity, independent `occurred_at_utc` and `received_at_utc`. An unknown but syntax-valid equipment or transport reference may be durably received; it is **not** considered linked to a shipment until later reconciliation. No cross-tenant lookup is allowed as a side effect of intake.

## 4. Versioned HTTP contract

Same route, method, media/profile header, 16 KiB input bound, secure transport, source bearer, Origin policy and M7 error envelope remain authoritative. New successful responses are conditional on a successful transaction commit and approved D-006 response contract.

| Input/outcome | Proposed HTTP | Semantics |
| --- | --- | --- |
| New authorized, valid event, three records committed | **202** | `{receiptId,status:"received",duplicate:false,receivedAtUtc,correlationId}` |
| Same source key and canonically identical payload, valid current authorization | **202** | Original receipt and timestamp; `duplicate:true`; no extra accepted event |
| Same source key with different canonical event | **409** | `source_event_collision`; preserve original receipt; first bounded collision metadata only |
| Pre-receipt invalid profile/body/credential/transport | Existing M7 400/401/403/413/415/422/429 | Preserve accepted codes, no receipt/work/event writes |
| DB unavailable, commit failure or ambiguous commit response | **503** | `receipt_unavailable` or existing sanitized dependency code, retry guidance, never 2xx |
| Organization pending cap reached | **503** | `intake_capacity_exceeded` with `Retry-After`; no new receipt |
| Client cancellation before confirmed commit | No 2xx | Roll back if possible; if uncertain, stable-key replay resolves outcome |

Every API response must set `Cache-Control: no-store` and safe correlation. No `Location` header implying a nonexistent public GET endpoint. A 202 must not imply processing, projection or a matched shipment. M8 should keep response fields and behavior stable for M9.

## 5. Data integrity and retention boundary

- Immutable receipt and event history has **no normal UPDATE/DELETE endpoint or scheduled purge**. Once accepted, a source key remains reserved across credential rotation, source disablement and organization changes. Disablement blocks new authentication but never erases previously received events.
- The local-first proposal caps pending work at **10,000 records per organization**. Count only currently pending work rows, not all historical receipts; the cap is enforced after duplicate resolution under the same organization transaction lock. M9 must coordinate completion/dead-letter state transitions with this admission protocol.
- Invalid/unsupported events continue to be rejected without an accepted receipt or raw-payload quarantine. A conflicting *valid* event key is recorded as first-seen sanitized collision metadata. Detailed invalid-event remediation is not asserted by G8.
- No separate broker, hosted database, retention deletion, automatic backup job, public admin event query, or irreversible cleanup is introduced in M8. M19 owns more comprehensive recovery, and M21 owns performance/capacity measurements.
- Current M7 bearer and SQL/EF lifecycle guards remain intact; migration adjustments must be strictly additive.

## 6. Failure boundaries and threat coverage

- **False success:** Never write an HTTP 202 before the transaction commits all required records. Mutation tests must detect moving the ack before commit or leaving event/work rows out.
- **Lost/duplicate events:** Unique key plus transaction serialization, commit-time completeness checks and replay of ambiguous commits. Identical requests 2 or 20 times yield one immutable event.
- **Revocation races:** Reauthorize under the receipt transaction lock; stale M7 authentication results are not sufficient. Test active->disabled, revoked and suspended organization interleavings.
- **Tenant confusion:** Use bound source/organization from the credential and composite FKs. Same UUID/external key across different sources is not a lookup path for other tenants.
- **Unbounded storage or raw-data leak:** 16 KiB cap, 10,000 pending cap proposal, minimal sanitized responses/logs, no raw dumps or tokens in shareable evidence. No retention purge until separately approved.
- **Partial write or commit uncertainty:** Roll back on failures, never acknowledge unknown commit outcomes; stable replay resolves ambiguity.
- **Migration damage:** M7 sources and credentials remain intact and all accepted M0-M6 records survive populated upgrade and test-only rollback/reapply.

## 7. Integration contract for M9 and M10

M9 must operate only on **committed immutable accepted events** and a separately mutable work-state record. Work claims, retry budgets, dead-letter reason codes, lease expiry, reconciliation, worker privilege and effect-once business actions require later design and G9 evidence. Adding the future worker must not change receipt identity, raw accepted payload, canonical digest, source-bound uniqueness, or previously acknowledged HTTP responses. M10 derives timelines from retained accepted events and can rebuild deterministic projections without mutating original history.

## 8. Acceptance and execution

The required G8 evidence is specified in [M8 verification plan](M8_VERIFICATION_PLAN.md). The implementation must not be marked verified solely because the planning branch is merged. D-006 acceptance is required before applicable M8-02/M8-03 contract changes; G8 requires executed fresh and populated migrations, controlled outages, adversarial race tests, clean accepted regressions and independent raw-evidence review. Following verification, G8 remains **acceptance pending** until explicitly approved. GitHub Actions stays disabled, no persistent project writes on C: are permitted, and no production/public application hosting is authorized.
