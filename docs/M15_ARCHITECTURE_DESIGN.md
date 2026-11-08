# M15 Scoped In-App Notifications: Architecture Design

**State:** Proposed M15-01 contract for review. Planning only, no implementation or G15 verification.  
**Planning baseline:** Accepted main at eb529368a2447061fc1838cd3967b0ffedae669d, inspected 2026-11-08.  
**Requirements:** FR-019 (Should) and FR-002 (Must). **Entry:** accepted G11. **Exit:** separately verified and explicitly accepted G15.

## 1. Baseline authority and bounded scope

This design uses accepted M11 exception history as the authoritative event stream, M2 organization membership/session scope for recipient eligibility, M5 shipment identity, M10 corrected equipment observation for M11 conflict provenance, and M16 central audit where a notification action warrants auditing. M15 does not determine whether an underlying M11 exception is true, does not run the M11 evaluator, and does not alter M11 exception, shipment workflow, M10 projection, KPI, or tracking-event outcomes.

Reviewed source boundaries: src/HarborOps.Domain/Organizations/OrganizationPolicy.cs, src/HarborOps.Infrastructure/Organizations/OrganizationStore.cs, src/HarborOps.Infrastructure/Identity/IdentityEntities.cs, src/HarborOps.Api/Authentication/AuthenticationSetup.cs, src/HarborOps.Api/Exceptions/ShipmentExceptionEndpoints.cs, src/HarborOps.Infrastructure/Exceptions/ShipmentExceptionStore.cs, src/HarborOps.Infrastructure/Persistence/Migrations/20261107180000_ShipmentExceptions.cs, src/HarborOps.Api/Audit/AuditRoleProvisioner.cs, web/src/auth/OrganizationWorkspace.tsx, web/src/shipments/ShipmentWorkspace.tsx and web/src/shipments/ShipmentExceptionPanel.tsx.

Accepted M11 facts:

- Exception rule codes are EquipmentConflict, ArrivalScheduleOverdue, RecordedArrivalLate, DepartureSlippageRisk, under m11-rules-v1 with a 60-minute UTC demonstration tolerance.
- M11's immutable shipment_exception_history records actions Opened, Acknowledged, Resolved, AutoResolved, Reevaluated and InputUnavailable. Its parent shipment_exceptions record has occurrence, rule, condition, lifecycle, confidence and severity. The signed evaluator and Manager disposition remain M11 authorities.
- Opened may follow an acknowledged, verified-clear occurrence that is system-closed on recurrence. New occurrence identity and history row identity, rather than a mutable source fingerprint, distinguish a new notification event.
- M11's RecordedArrivalLate is a *manual recorded workflow arrival*, ArrivalScheduleOverdue means *arrival unconfirmed*, DepartureSlippageRisk is a *schedule risk*, and EquipmentConflict is a *recorded equipment observation conflict*. No class proves physical vessel movement or ETA.
- The M9 ARRI/DEPA NeedsReconciliation hold and 10,000-item cap stay in force. M10's projector defaults off, ETA is null and M11's evaluator stays opt-in.

Infrastructure alerts, IT incidents, email, push, SMS, predictive ETA, transport processing and M13 KPI computation are excluded. Any future M17/M18 source integration is a separately scoped extension, not a hidden FR-019 deliverable.

## 2. Proposed event catalog and trigger truth

| Notification type | Immutable M11 origin | Eligibility and display | Exclusions |
| --- | --- | --- | --- |
| ExceptionOpened | shipment_exception_history.action = Opened, newly created exception occurrence | Notify entitled recipients of the new occurrence and link to its shipment and exception. Rule code selects a fixed, accurate template. | Not an alert from a raw tracking receipt, reevaluation or mere missing telemetry. |
| ExceptionResolved | action = Resolved, new_state = Resolved | Inform entitled recipients that a Manager completed a recorded disposition. The underlying rule facts are unchanged. | Do not copy free-form disposition reason or actor identity. |
| ExceptionAutoResolved | action = AutoResolved, new_state = Resolved | Inform entitled recipients of system closure or supersession with an explicit historical label. | Do not claim confirmed physical arrival or rule evidence was erased. |

**Proposed default:** An Acknowledged, Reevaluated or InputUnavailable history entry creates no separate alert. They remain visible in authorized M11 detail. Material severity escalation within one occurrence is deferred because the existing immutable history does not persist a reliable prior-severity delta. Introducing it would require a separately reviewed source extension. Opening a recurrence produces a fresh Opened history UUID and fresh exception UUID, so it is distinct without duplication.

The outbox insertion must read only the allowlisted action, scoped exception ID, organization ID, shipment ID, rule code and occurrence. Display templates are server-owned, localized later if needed, and contain no arbitrary source text. Example labels: "Equipment observations conflict", "Planned arrival overdue; arrival unconfirmed", "Recorded workflow arrival was late", "Recorded workflow departure indicates schedule risk".

## 3. End-to-end data path and atomicity

1. An accepted M11 signed SQL transaction inserts an immutable history row. A new **additive M15 AFTER INSERT trigger**, owned and installed by the privileged migrator, writes one compact outbox intent for each cataloged history action into m15_notification_outbox in the **same transaction**. It does not edit accepted M11 SQL functions or prior migrations. The outbox row uses source_history_id as a unique immutable identity. A rolled-back M11 action creates no intent.
2. An independently controlled M15 dispatcher leases committed pending intents using PostgreSQL row locks with SKIP LOCKED, fenced lease epoch, bounded batch size and retry/backoff. This is a dedicated service boundary hosted by the API only after authorization, not an M9 worker, transport projector or automatic M11 evaluator. Its configuration defaults off until M15 authorization and rollout.
3. On an intent claim, the dispatcher reads the current active organization, source exception and shipment. It resolves active users and active memberships with ViewShipmentData permission and eligible role, snapshots each selected membership ID and version, and inserts recipient deliveries transactionally. Each intent fanout and completion marker commit together, with a uniqueness constraint preventing duplicates. A crash before commit retries; a crash after commit finds a completed intent. No external delivery receipts exist.
4. GETs and per-user state mutations revalidate the authenticated user, live PostgreSQL session, session-selected organization, membership role/status/version, organization state, resource membership and row recipient. They never trust request orgId, recipientId, signed cursor or notification metadata as authority.
5. A failed outbox insert rolls back the source transaction rather than quietly losing an alert. **This is a proposed reliability/security tradeoff requiring M15-P10 approval** and explicit M11 regression, lock-order, contention and failure testing. Rollout must not turn on the trigger before its database grants and rollback behavior are verified. No independent trigger or dispatcher is deployed under this planning authorization.

The source historical event is independent of whether a recipient is online. Membership selection occurs at first committed fanout, not at M11 event creation, and does not create a session. Therefore a user newly entitled before fanout could receive an earlier still-pending event; a user joining after completed fanout does not receive automatic historical backfill. This delivery-time audience rule is a policy proposal.

## 4. Recipient scope, five roles and revocation

| Role with Active membership in Active organization | May receive/view | May mark read or acknowledge personal receipt | May change M11 exception |
| --- | --- | --- | --- |
| Analyst | Yes, scoped shipment/exception alerts | Yes | No |
| Manager | Yes, scoped shipment/exception alerts | Yes | Yes, through existing M11 Manager API only |
| Auditor | Yes, read-only business alerts allowed by ViewShipmentData | Yes, personal receipt only | No |
| Administrator | No implicit shipment entitlement | No | No |
| Support | No implicit shipment entitlement | No | No |

Fanout is derived from OrganizationPolicy.Allows(role, ViewShipmentData), not a copied UI role string. Do not assume platform administrator supremacy or infer visibility from membership in another organization. No user-supplied recipient lists. An account must be active and the target shipment must exist in the same tenant. At creation/fanout, persist the membership ID, membership version and recipient user ID, not raw credentials, session IDs or role names as authority.

**Three authorization points:** (a) source creation checks accepted M11 database authority and tenant FK; (b) fanout checks live organization, account, membership and shipment at delivery; (c) every read, count and mutation checks live session selection, org status, membership and resource. If a membership is suspended, removed or changed, stored versions no longer match and prior alerts become unavailable even after reactivation. This conservative version-bound suppression prevents reappearance across a revoke/rejoin cycle. New entitled events use the new version. A separately proposed restore/backfill workflow is out of scope.

For archived or cancelled shipments, the alert is not evidence of present activity. Link only if the existing authorized shipment/exception detail permits historical read; otherwise suppress it from list/count and show a generic unavailable state to a previously rendered client. A physically absent or foreign resource never yields a working link or existence oracle. An in-flight authorization change must fail closed at the transaction boundary; if it races after response creation, the next request and deep-link target reauthorize independently.

## 5. Proposed PostgreSQL model

Implement additively only after M13 migration order coordination. Do **not** reserve a numeric migration ID in planning.

| Table | Proposed columns and constraints |
| --- | --- |
| m15_notification_outbox | id UUID PK, source_history_id UUID NOT NULL UNIQUE with M11 history FK, organization_id, shipment_id, exception_id, event_type, created_at_utc, state Pending/Leased/Completed/Retry, attempts, next_attempt_at_utc, lease_token, lease_epoch, lease_expires_at_utc, last_failure_code, completed_at_utc. Bounded safe failure codes only. Tenant-scoped composite FKs, no raw source body. |
| m15_user_notifications | id UUID PK, outbox_id FK, organization_id, recipient_user_id, membership_id, membership_version, shipment_id, exception_id, event_type, template_code, created_at_utc, read_at_utc nullable, acknowledged_at_utc nullable, version bigint positive. Unique (outbox_id,recipient_user_id); composite tenant/user FK as supported by accepted schema. |
| m15_notification_replays | organization_id, recipient_user_id, notification_id, actor_session_id, idempotency_key UUID, payload_digest, result_state/version, created_at_utc, expiry UTC. Unique scoped replay key; immutable or write-once semantics. |

Use a separate constrained trigger/function for outbox generation; no direct API INSERT into it. The dispatcher is permitted only narrowly scoped claim/fanout functions. Normal HTTP users cannot choose a recipient ID. Runtime harbo**rops_api** (actual role: harborops_api) currently receives broad SELECT grants from AuditRoleProvisioner, so M15 must explicitly REVOKE unrestricted privileges on new tables and enforce effective PostgreSQL RLS or verified SECURITY DEFINER access. Never assume HTTP role checks alone establish a SQL boundary. Source outbox, replay table and proof verifier material are not readable through ordinary API SQL. Validate RLS as the real restricted login, including adversarial SET configuration attempts.

Recommended signed, short-lived **m15-notification-v1** database proof or equally strong accepted M11-style scoped read context ties actor user, live session, selected org, operation, target, request digest, expiry and nonce to an API-held secret. The database checks live session/security version/membership/organization and target row before any SELECT or update. The migrator provisions an M15-specific verifier, distinct from M10/M12 keys; key absence or mismatch fails closed. No key is committed, logged or sent to web/M9 worker. The final SQL function signatures and privilege matrix are implementation deliverables, not approved signatures here.

Indexes to validate against measured EXPLAIN: outbox (state,next_attempt_at_utc,lease_expires_at_utc,id), unique source_history_id; notifications (organization_id,recipient_user_id,created_at_utc DESC,id DESC), partial unread by organization/recipient where read_at_utc IS NULL, (organization_id,shipment_id), unique (outbox_id,recipient_user_id); replay (organization_id,recipient_user_id,idempotency_key). FK/unique and restrictive grants must also cover deletion/retention operations. No arbitrary global notification reads.

## 6. User receipt lifecycle and source independence

- **Unread:** read_at_utc NULL, acknowledged_at_utc NULL.
- **Read:** first successful MarkRead sets read_at_utc from PostgreSQL UTC, acknowledged_at_utc NULL.
- **Acknowledged:** first successful Acknowledge sets acknowledged_at_utc and, if absent, read_at_utc in the same transaction.
- Duplicate same-key/same-payload replays return the original committed response without extra writes. Same key/different payload, stale expectedVersion or concurrent incompatible transitions return 409 with a stable error code.
- Acknowledge is terminal for the personal notification row; MarkRead after Acknowledge is an idempotent no-op, not a state regression. No mark-unread or bulk action in M15 v1.
- This is **not** M11's Manager-only Acknowledge/Resolve and must never update M11 condition, lifecycle, history, KPI, tracking projections, shipment status or audit truth.

Unread count is the count of currently accessible rows with read_at_utc NULL. It is not the number of active M11 exceptions, and disappearance after revocation does not imply business resolution.

## 7. Namespaced HTTP contract proposal

All routes use existing D-001 same-origin cookie authentication and /api/v1/organizations/{organizationId}/notifications; no external notification transport and no new unauthenticated endpoints.

| Route | Request / response | Rules |
| --- | --- | --- |
| GET /notifications | Query state=All,Unread,Read,Acknowledged (default All); limit 1..50 default 25; optional opaque cursor. Response { items, nextCursor, snapshotAtUtc }. | Live scope, recipient, membership version and resource checks; bounded result, no-store. Sort createdAtUtc DESC,id DESC. |
| GET /notifications/unread-count | Response { unreadCount, asOfUtc }. | Live filtered count; cap is only a presentation concern, never a truncated numeric claim. |
| GET /notifications/{notificationId} | Response one allowlisted item with authorized target and current personal state. | Foreign/nonexistent/hidden IDs return indistinguishable 404. |
| PATCH /notifications/{notificationId} | { action: "MarkRead" or "Acknowledge", expectedVersion: positive integer }, UUID Idempotency-Key. Response { item }. | CSRF token, same Origin, live scope, version/replay protection, 409 on racing incompatible writes. |

List items expose only notification UUID, type, fixed title/template, createdAtUtc, ruleCode, evidence classification label, severity if safely sourced, personal state and version, and authorized shipment/exception UUID navigation target. Never return source fingerprints, signed proofs, actor IDs, free-form reason, raw event payload, tenant roster or unfiltered metadata. Use current source detail for sensitive facts, not an old persisted rendered paragraph.

Cursors use ASP.NET Data Protection with purpose specific to M15 list version 1, a 20-minute expiry and bindings to org/user/session/state/filter, initial database creation cutoff, and last (createdAtUtc,id). Newly committed rows beyond the initial cutoff do not enter later pages; mutable read/ack states and access permissions **are not a frozen snapshot**. Each page rechecks live authority. Tamper, expired, swapped and malformed cursors produce sanitized 400 invalid_cursor; access failures precede resource disclosure.

Errors follow existing JSON envelope { code, message, correlationId }, with 400 invalid_request/invalid_cursor, 401 authentication_required, 403 access_denied/organization_access_revoked/csrf_invalid, 404 notification_not_found, 409 organization_selection_required/notification_conflict/idempotency_conflict, 422 invalid_notification_transition, 429 rate_limited and 503 dependency_unavailable/notification_authority_unavailable. Preserve existing org-selection conventions, no redirect or raw SQL text; no-store on success and failure. Exact error mapping requires HTTP contract tests.

## 8. React integration and navigation

Own new web/src/notifications/NotificationCenter.tsx, notificationClient.ts and isolated presentation/contract tests. It accepts selected organization ID and role-derived permission only as a render hint; the server remains authoritative. Show unread count, refresh, timestamp, rule/evidence-safe title, state, a capped paged feed and explicit Mark read / Acknowledge personal receipt actions. Provide independently tested loading, empty, denied, revoked, archived/unavailable target, timeout, retry, pagination, stale response, rapid organization switch and offline/error states. Abort requests and clear protected rows on org switch/logout/revocation.

The current app is a selected-organization workspace, not a URL-routed shipment detail application. A notification link must call a parent-owned selection contract to open the matching shipment and its M11 exception within the existing authorized ShipmentWorkspace; no raw unvalidated URL redirect or invented route. Target opening refetches shipment/exception detail and handles archived, removed, foreign and now-forbidden targets without leaking existence. Integrate into OrganizationWorkspace only after an explicit M13 shared-shell contract review. M13 dashboard badges or KPI cards are not M15-owned edits.

Polling while the center is visible, or a refresh button, is the initial in-app delivery mechanism. Proposed 30-second visible polling with backoff, abort on hidden/unmount and no background fanout from the browser. It is not push, email, transport alerting or an infrastructure incident stream.

## 9. Audit, privacy, retention and performance policy proposals

Notification acknowledgments are security-relevant personal-state mutations. Proposed M16 event notification.receipt_acknowledged uses fixed event schema, scoped actor/tenant/notification ID and no message body or reason. The acknowledgment and required audit must commit atomically; if audit persistence is unavailable, acknowledgment fails closed. MarkRead need not emit a central audit row to avoid high-volume read noise. M11 Manager actions retain their own existing signed audit; never duplicate it. Review existing M16 event allowlist and SQL insertion path before adding any event. Denied access is sanitized and subject to existing M16 audit policy.

Proposed online retention: purge terminal/personal notification rows 90 days after createdAtUtc with minimum 30 days after acknowledgedAtUtc, subject to an absolute 180-day online limit and any separately approved legal/incident hold. These figures are **not approved**; no cleanup job may be turned on until policy and safe deletion/archival semantics are accepted. Source M11 append-only history, M16 audit and M8 evidence must never be deleted by M15 cleanup. Outbox Completed rows and replay keys need a separately bounded expiry after delivery proof and replay windows; never discard unresolved intents or break traceability.

Performance assumptions to validate, not measured results: 1,000,000 recipient rows, 100 simultaneous readers, peak 250 active entitled members per organization, burst 100 M11 history events/minute, bounded dispatcher batches <=100 intents. Use measured p50/p95 list/count/write latency, index scans and PostgreSQL locks in disposable fixtures; make no SLA promise before G15. Retry with jitter and bounded attempts, preserve dead/failing intents for operator diagnosis without exposing payloads. Do not perform unbounded full-history scans or claim exactly-once processor execution: unique DB results provide effective once-per-recipient persisted effect.

## 10. Cross-track and policy boundary

The M13 branch owns operational KPI definitions, query services and dashboards. M15 owns its source-intent delivery, receipt-state API and notification center only. M13 never derives metrics from notification counts or delivery timestamps; M15 does not change KPI truth or persist calculated KPI facts. Shared shell/navigation, authorization permission addition, Program.cs registrations, EF snapshot and additive migration ordering require explicit review of **both** implementation proposals before integration. Do not assume another workstream's signature or schema is approved.

All choices above are recommendations, not approvals. See M15_PLANNING_STATUS.md for numbered decisions, M15_IMPLEMENTATION_PLAN.md for controlled ownership and M15_VERIFICATION_PLAN.md for future G15 evidence.
