# API Contracts

**G13/G15 current checkpoint (2026-11-12):** [Run 43122895445](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43122895445) is IN PROGRESS on source `c6d2dc86e30913b8d67a011131f9980422e4c197`; preflight, browser, crash recovery, G13 rollback/concurrency, G15 load/fanout and security checks had passed at inspection, with longer technical jobs and the final ledger unfinished. [Run 43120072016](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43120072016) ended FAILURE after 13 technical passes because G15 mutation `outbox-claim-skip-locked-removed` timed out and was INVALID. The fixture was changed to sleep once per transaction, still subject to source-locked verification. [Run 43093329982 attempt 2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43093329982/attempts/2) passed all technical jobs but failed atomic assertion reconciliation. Results cannot be pooled across SHAs. Approved G13-S20 contract: 250,000 KPI members per report, four live reports per tenant, two per user and 1,000,000 total live members per tenant. **G13/G15 NOT ACCEPTED** pending the complete technical packet, independent S25 reviews and explicit gate decisions. No merge, deployment, Turkish integration, production activation or retention deletion. See [run register](G13_G15_POST_C3_HOSTED_RUN_REGISTER_20261111.md) and [2026-11-12 reconciliation](REPOWIDE_DOCUMENTATION_RECONCILIATION_20261112.md); older results are historical.

**Verification reference (2026-11-10):** [G13/G15 15-job technical campaign](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed at `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75` (541 TRX passed, no failures/skips, integrity PASS). The atomic evidence audit reports 224 present and 3 missing technical witnesses, plus 15 policy/independent-review items. This is not G13/G15 acceptance or merge authorization. [Current record](G13_G15_VERIFIED_EVIDENCE_20261110.md).

**Current accepted baseline (2026-11-08):** G0-G12 and G16 are accepted and integrated into `main`. The [G11/G12 gate closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md) and [project status](STATUS.md) govern current M11 delay exceptions and M12 scoped search/export behavior. Earlier source-only and verification checkpoints are historical. The M9 ARRI/DEPA processing hold and 10,000 unresolved-item limit remain binding; M10 projector default-off, ETA null, M11 evaluator opt-in. No deployment or release is authorized.

## Contract status

M0-M10 APIs, including the M10 timeline, projection and signed Manager correction routes, are accepted and integrated into `main`. M16 Auditor-only tenant-scoped audit APIs are also accepted. Original M10 PR #23 and remediation [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) establish the current contract. The M9 parked-transport hold and the projector default-off setting remain mandatory. See [G10 closure](M10_G10_GATE_CLOSURE_20261107.md).

## Observed M0 contract verification

At historical M0 revision `4fb34eb`, the API was tested against real PostgreSQL and through HTTP, including readiness 503 under database outage and liveness 200. This M0-only source was later verified on .NET 10 and accepted as G0 (see [M0 hardening evidence](M0_HARDENING_VERIFICATION.md)). M1-M9 and M16 API families were subsequently implemented and accepted; do not interpret the original M0-only scope as the present application state.

The workspace, caches and test artifacts must follow [Local Storage Policy](LOCAL_STORAGE_POLICY.md), using E: for all controlled persistent project storage. Only unavoidable short-lived Windows temporary activity on C: is permitted.

## Common conventions

- Base path: /api/v1
- Content type: application/json
- Transport: HTTPS when remotely accessible
- Authentication: approved D-001 server-authoritative protected browser cookie (M1), with machine-source credentials implemented, verified and accepted in M7 (2026-11-03). [Decision](ADR_D001_BROWSER_SESSIONS.md).
- Organization context: derived from authorized membership and explicitly selected organization
- Timestamps: ISO 8601 UTC
- Pagination: bounded limit, continuation token where useful
- Request correlation: correlation ID echoed in responses and structured logs
- Writes: explicit validation, idempotency where supported, conflict detection

## Implemented M0 endpoint

`GET /api/v1/system/baseline` reads the seeded `bootstrap_records` row in PostgreSQL. Its successful JSON object has `service`, `datasetVersion`, `seededAtUtc`, and `source` fields, where `source` is `synthetic`. No customer data is exposed.

`GET /health/live` reports process liveness without checking dependencies. `GET /health/ready` checks PostgreSQL connectivity and the baseline seed and returns HTTP 503 if unhealthy. Both return only a minimal `status` string.

The baseline endpoint returns a safe `baseline_unavailable` error when the record is missing and a `dependency_unavailable` response for recognized database failures. Unhandled errors use a sanitized `internal_error` envelope. Every API response is intended to carry `X-Correlation-ID`; this behavior is covered by contract tests and was observed against the running stack on 2026-10-30.

## Implemented M1 authentication contract (accepted on main)

The M1 route group is `/api/v1/auth`. These routes are served on `main` following accepted G1 and merged PR #4. The authoritative detailed contract and negative matrix are [M1 design](M1_AUTHENTICATION_DESIGN.md) and [G1 verification](M1_VERIFICATION_PLAN.md). The merged frontend uses same-origin `/api/` proxying in Nginx and Vite. See [M1 executed evidence](M1_IMPLEMENTATION_VERIFICATION.md).

| Method | Route | Access | Behavior |
| --- | --- | --- | --- |
| GET | `/api/v1/auth/csrf` | Public same-origin bootstrap | Issue non-cacheable antiforgery token; no session |
| POST | `/api/v1/auth/login` | Anonymous + CSRF | Verify password and account, persist session, issue HTTP-only cookie |
| GET | `/api/v1/auth/session` | Authenticated | Return bounded user and session expiry metadata |
| POST | `/api/v1/auth/logout` | Authenticated + CSRF | Revoke current session and clear cookie; 204 |
| POST | `/api/v1/auth/logout-all` | Authenticated + CSRF | Revoke all account sessions; 204 |
| POST | `/api/v1/auth/account/disable-self` | Authenticated + CSRF + password confirmation | Disable account and revoke all sessions; 204 |
| GET | `/api/v1/auth/session/guard` | Authenticated | Demonstrate server-side session enforcement |

`POST /login`: `{"email":"operator@example.test","password":"placeholder-not-a-real-password"}` (illustrative only). Successful 200 returns `user.id`, `user.email`, `user.displayName`, and `session.expiresAtUtc`. It never returns the authentication cookie value in JSON. The cookie is issued in a protected `Set-Cookie` response header. Unknown email, wrong password and disabled account return identical generic `401 invalid_credentials` envelopes. Protected anonymous calls return `401` JSON, not 302 redirects. Other safe codes include `authentication_required`, `session_expired`, `csrf_invalid`, `rate_limited`, `dependency_unavailable` and `internal_error`.

Local account provisioning and password reset are available through restricted interactive CLI commands. Self-disablement is available through the protected API. Privileged account re-enablement is not implemented in M1 and requires a separate reviewed administrative procedure. No public administrative endpoints, multi-organization `/me` membership response, bearer tokens, SSO or shipment routes are added at M1. Those remain later milestones. Preserve M0's public synthetic baseline and minimal liveness/readiness routes.

## M2 organization and context contract (implemented on `main`; G2 accepted)

The [M2 design](M2_ARCHITECTURE_DESIGN.md) and [G2 verification plan](M2_VERIFICATION_PLAN.md) define the following endpoints. They passed local G2 verification across the required method and role matrix ([executed record](M2_EXECUTED_VERIFICATION.md)) and are merged into `main` via PR #5. The endpoints use M1 protected-cookie sessions; mutations additionally require same-origin CSRF validation.

| Method | Route | Intended access |
| --- | --- | --- |
| GET | `/api/v1/me/organizations` | Current user's active organizations only |
| GET | `/api/v1/me/organization-context` | Revalidated server-side selected organization |
| PUT | `/api/v1/auth/session/organization` | Select active membership for current session |
| DELETE | `/api/v1/auth/session/organization` | Clear current session selection |
| GET | `/api/v1/organizations/{organizationId}` | Selected active organization, any member role |
| PATCH | `/api/v1/organizations/{organizationId}` | Selected organization Administrator; optimistic version |
| GET | `/api/v1/organizations/{organizationId}/memberships` | Selected organization Administrator; bounded list |
| GET | `/api/v1/organizations/{organizationId}/memberships/{userId}` | Selected organization Administrator |
| PUT | `/api/v1/organizations/{organizationId}/memberships/{userId}` | Selected organization Administrator, protected grant/state change |
| DELETE | `/api/v1/organizations/{organizationId}/memberships/{userId}` | Selected organization Administrator, soft removal and last-admin guard |

A null selected organization yields 409 `organization_selection_required` on scoped routes. Valid foreign organization or membership IDs return 404, wrong-role operations return 403, expired or missing M1 authentication returns 401, and database failure returns sanitized 503. An authenticated `POST /api/v1/auth/account/disable-self` also refuses disabling an account that is the last active Administrator for any organization, returning 409 `last_administrator` after password confirmation. A route ID, claimed role or custom header is not accepted as authorization. [M2 architecture](M2_ARCHITECTURE_DESIGN.md) defines the responses and transactions. [M2 implementation register](M2_IMPLEMENTATION_STATUS.md) records the source and tests, and the [executed record](M2_EXECUTED_VERIFICATION.md) records the passing local G2 evidence. G2 verified locally and accepted on 2026-10-31.

## M3 scoped reference-data API (verified, accepted and integrated)

The [M3 architecture](M3_ARCHITECTURE_DESIGN.md) defines ten implemented and accepted authenticated operations under `/api/v1/organizations/{organizationId}`: GET list/detail, POST create, PATCH update/restore and DELETE archive for both `ports` and `vessels`. The endpoints are implemented in `src/HarborOps.Api/References/ReferenceEndpoints.cs` and integrated into `main`. Real PostgreSQL, HTTP and three-browser G3 verification passed on `b72025d35d62d0a8a45b466986a0b3bd89eebadb` ([evidence](M3_EXECUTED_VERIFICATION.md)); G3 accepted 2026-10-31. An active selected organization and Manager/Analyst membership are required for reading; only Manager may mutate. Administrator, Support and Auditor gain no implicit business-data permissions.

Port create requires tenant-local `referenceCode`, `name`, `countryCode`, optional `unLocode`. Vessel create requires local `referenceCode` and `name`, optional `imoNumber`, `mmsi` and `flagCountryCode`. All organization IDs, UUIDs, version numbers and timestamps are server-managed. `GET` lists support bounded `q`, `status`, `limit` and `after` filters. Writes require the existing CSRF/Origin protections and optimistic expected version. DELETE soft-archives rather than physically deletes. Data access must remain tenant-bound even if the caller knows a foreign UUID. See [implementation plan](M3_IMPLEMENTATION_PLAN.md) and [G3 negative test matrix](M3_VERIFICATION_PLAN.md) for authoritative details.

## M4 implemented scoped APIs (G4 accepted)

The accepted [M4 architecture](M4_ARCHITECTURE_DESIGN.md) implements ten `/api/v1/organizations/{organizationId}/voyages` and `/containers` endpoints: GET list/detail, POST create, PATCH versioned edit/restore, DELETE versioned soft archive. Manager read/write, Analyst read-only, and default denial for Administrator, Support and Auditor are enforced, reusing accepted M1-M3 session and scope checks. Every mutation requires CSRF/Origin validation and expectedVersion. Voyage route requires same-organization `vesselId`, `originPortId` and `destinationPortId`, with separate nullable planned and actual UTC times, `voyageStatus` and `recordStatus`. Container create requires `referenceCode`, `identifierKind`, `containerIdentifier` and `equipmentType` under [D-003 Option B accepted](M4_D003_CONTAINER_IDENTIFIER_PROPOSAL.md). Lists have bounded search/filter, `limit=1..100` and UUID cursor `after`. Detailed request/response and error contracts are in [M4 Architecture](M4_ARCHITECTURE_DESIGN.md). No route exists yet.

## Accepted M5 shipment API

[M5 architecture](M5_ARCHITECTURE_DESIGN.md) specifies scoped shipment GET list/detail/status changes, POST Draft creation, PATCH exact-version edits/transitions/restore and DELETE soft archive. M5 supplies bounded search, while M12 owns advanced search and CSV. `containerIds` and `voyageId` from older sample payloads are excluded until M6 assignments. Five-role permissions, CSRF/Origin and sanitized errors require [G5](M5_VERIFICATION_PLAN.md).

The M5 shipment Minimal API implements the documented routes and was verified and accepted on 2026-11-02. See [G5 closure](M5_G5_GATE_CLOSURE.md) and [implementation status](M5_IMPLEMENTATION_STATUS.md).

## Third-pass M11 API clarifications

Exception lists use the provisional P09 rule order (`EquipmentConflict`, `RecordedArrivalLate`, `ArrivalScheduleOverdue`, `DepartureSlippageRisk`) followed by UUID, including across paged cursors. Competing evaluations or Manager dispositions return an explicit 409 when uncommitted. The signed evaluation proof binds timestamps at PostgreSQL microsecond precision under `m11-eval-v2`. Executable verification remains outstanding.

## Fourth-pass M11 list and history continuation

The scoped exception-list cursor now binds `organizationId`, optional filters, rule-ranked last ID and a PostgreSQL-clock `SnapshotAtUtc`. Rows opened after the first page are excluded, but lifecycle changes are not an immutable SQL snapshot and may affect later filtered pages. The detail response supplies `nextHistoryCursor` only when the 500-item preview is truncated. `GET /api/v1/organizations/{organizationId}/exceptions/{exceptionId}/history?limit=100&cursor=...` returns `{ items, nextCursor }` using immutable `(occurred_at_utc,id)` paging. Each token expires after 20 minutes, is bound to the selected tenant and exception, and cannot expose foreign history. Analyst reads redact `reason` and `actorUserId` on every page. These contracts were subsequently verified and accepted for G11; this earlier API note retains its original design details.

## Sixth-pass M11 rule severity and reevaluation

An Inactive `RecordedArrivalLate` signal from an on-time recorded arrival now uses Info severity, as do not-yet-overdue arrival schedules and inactive departure risks. Active recorded lateness remains High, and active schedule/departure risks remain Warning. A signed reevaluation with an identical source fingerprint now updates a still-active exception if its severity, confidence, reason or reference/threshold/next-due timestamp differs, appending one versioned Reevaluated history entry. Repeated identical metadata remains a genuine no-op. These contracts were subsequently exercised and formally approved under G11.

## Fifth-pass M11 provisional lifecycle and API behaviors

If an Acknowledged incident was explicitly evaluated Inactive and later becomes Active again, the signed evaluator now closes the previous occurrence as a system AutoResolved event and creates a separate Open occurrence with the next occurrence number. The Resolve action is allowed only for an Acknowledged/Inactive occurrence. Known replay-key uniqueness, serialization and deadlock collisions return 409; unrelated database uniqueness failures are not mislabeled as ordinary conflicts. These are unverified source contracts, and the decisions for recurrence across Unknown/Stale and a persistent RecordedArrivalLate condition remain pending.

## M11 provisional request and response contract

The scoped list returns `{ items, nextCursor }` with a short-lived protected cursor bound to organization, optional shipment and state. A tampered or foreign cursor returns 409 `stale_or_invalid_cursor`. Detail returns `{ exception, history, historyTruncated }`. The first 500 historical events are previewed with explicit truncation; a protected `nextHistoryCursor` now supports full-history continuation through the separate history route, pending runtime verification. `exception.evidence` includes bounded policy and revision context, not raw tracking payload.

Manager actions use a CSRF-protected empty evaluation `POST` or `PATCH /api/v1/organizations/{organizationId}/exceptions/{exceptionId}`, with an `Idempotency-Key` UUID header and exactly `action`, `expectedVersion` and `reason` in the body. Keys are organization-unique and conflicting reuse yields 409. Analyst detail redacts free-form human reasons and actor IDs. These are source contracts only, not verified endpoints or ETA.

## Accepted M11 exception API

The feature branch implements `GET /api/v1/organizations/{organizationId}/shipments/{shipmentId}/exceptions`, `GET /api/v1/organizations/{organizationId}/exceptions`, `GET /api/v1/organizations/{organizationId}/exceptions/{exceptionId}`, Manager-only `POST /api/v1/organizations/{organizationId}/shipments/{shipmentId}/exceptions/evaluate`, and Manager-only, idempotent `PATCH /api/v1/organizations/{organizationId}/exceptions/{exceptionId}`. List cursors are protected and bound to tenant/filter. The contracts were verified and accepted under G11 in [formal gate closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md). See [M11 architecture](M11_ARCHITECTURE_DESIGN.md) and [status](M11_IMPLEMENTATION_STATUS.md). They do not represent ETA or processed ARRI/DEPA records.

## Proposed resource map

| Method | Route | Purpose | Access |
| --- | --- | --- | --- |
| GET | /me | Current authenticated principal and memberships | Authenticated |
| GET/POST | /organizations/{orgId}/shipments | Search/create shipments | Scoped operations |
| GET/PATCH | /organizations/{orgId}/shipments/{id} | Detail/update with version check | Scoped operations |
| GET/POST | /organizations/{orgId}/containers | List/register containers | Scoped operations |
| GET/POST | /organizations/{orgId}/voyages | List/create voyages | Scoped operations |
| GET | /organizations/{orgId}/ports | Port lookup | Scoped viewer |
| GET | /organizations/{orgId}/shipments/{id}/events | Accepted timeline and provenance | Scoped viewer |
| POST | /integrations/v1/tracking-events | Source-authenticated event intake | Integration principal |
| GET | /organizations/{orgId}/exceptions | Shipment exceptions | Scoped operations |
| PATCH | /organizations/{orgId}/exceptions/{id} | Acknowledge/resolve exception | Scoped operations |
| GET | /organizations/{orgId}/analytics/overview | Documented operational KPIs | Scoped viewer |
| GET | /organizations/{orgId}/shipments/search | Accepted M12 tenant-scoped advanced search, protected UUID keyset and UTC filters | Typed shipment-read permission, G12 accepted |
| GET | /organizations/{orgId}/exports/shipments | Accepted M12 bounded CSV and signed nonreplayable preparation audit | Analyst/Manager typed export permission with verifier key, G12 accepted |
| GET/POST | /operations/incidents | Incident list/create | IT operations role |
| GET/PATCH | /operations/incidents/{id} | Incident detail and transitions | IT operations role |
| GET | /health/live | Process liveness | Public, minimal metadata |
| GET | /health/ready | Dependency readiness | Restricted or minimal public output |
| GET | /metrics | Scrape metrics | Restricted monitoring principal |

Do not trust orgId or other route parameters as authorization proof. Integration principals receive scoped source and organization entitlements, separate from human user roles.

## Representative payload fields

Shipment create:
- externalReference, originPortId, destinationPortId, plannedDepartureAt, plannedArrivalAt, containerIds, voyageId.

Tracking event:
- eventId or source-specific deduplication key, eventType, eventClassifier, equipmentReference, transportReference, occurredAt, location, sourceSystem, rawPayloadVersion.

Incident:
- title, severity, service, detectedAt, status, evidenceLinks, rootCause, mitigation, remediationTasks.

Use a formal schema and integration tests before treating these as stable public contracts.

## Error envelope

Responses should include: stable machine-readable error code, message suitable for clients, correlation ID, optional field validation details. Never expose credentials, internal stack traces, SQL statements, or unrestricted raw upstream payloads.

Suggested statuses: 400 invalid input, 401 unauthenticated, 403 forbidden, 404 not visible or absent, 409 version/state conflict, 422 domain violation, 429 rate limit, and 5xx unexpected dependency/server failures.

## Accepted M7 source-authenticated intake contract

The [M7 implementation](M7_IMPLEMENTATION_STATUS.md), exercised by the [executed verification](M7_EXECUTED_VERIFICATION.md), adds a HarborOps-specific `POST /integrations/v1/tracking-events` using opaque, tenant-bound bearer credentials, an exact tracking profile, strict JSON limits, UTC occurrence timestamps and server-derived source identity. A parsed/valid event is **not** a durable receipt. Until M8 provides an atomic inbox transaction, even an authenticated valid event must return `503 intake_unavailable` with `Retry-After`, not HTTP 200/201/202. The canonical JSON uses `eventFamily` and `eventCode` with source and tenant identity derived only from the authenticated credential. D-002 is accepted (Option A, 2026-11-03) and G7 was accepted on 2026-11-03 (see [G7 closure](M7_G7_GATE_CLOSURE.md) and [verification matrix](M7_VERIFICATION_PLAN.md)). This route is not a claim of standard DCSA publisher API conformance. The profile `tnt-2.2-harborops-v1` is derived from DCSA Track & Trace 2.2.0 but is deliberately narrower and **not DCSA conformant**: it omits the required upstream fields `eventCreatedDateTime` and `emptyIndicatorCode`, reduces `transportCall` to `transportReference`, requires UTC and accepts only Equipment `GTIN`/`GTOT`, Transport `ARRI`/`DEPA` and classifier `ACT`. Events in the DCSA wire format cannot be consumed directly; a translation layer is required. The pinned upstream definitions are local snapshots, not official immutable DCSA releases, limited to the reviewed HarborOps subset and not a complete transitive DCSA schema snapshot; see [`third_party/dcsa-track-and-trace`](../third_party/dcsa-track-and-trace/README.md).

## M8 durable receipt extension (G8 accepted)

The accepted M8 receipt path changes the prior M7-only 503 behavior **only after** an atomic transaction commits one immutable receipt, one immutable accepted event and one Pending work item. D-006 Option A and G8 were accepted 2026-11-04 after [independent evidence review](M8_G8_INDEPENDENT_EVIDENCE_REVIEW.md).

Accepted M8 behavior: new event -> 202 `{receiptId,status:"received",duplicate:false,receivedAtUtc,correlationId}`; canonically identical replay for the same tenant/source/profile/external event key -> 202 with original receipt and `duplicate:true`; same key with changed canonical event -> 409 `source_event_collision`; DB unavailability or pending cap -> 503 with retry guidance. Original M7 negative 4xx, secure transport, schema/profile, rate and 16 KiB limits remain unchanged. The original bearer must be reauthorized in the receipt transaction after parsing, and no 2xx is possible on unknown commit outcome. No public GET receipt, worker acknowledgment or shipment projection API belongs to M8. See [M8 architecture](M8_ARCHITECTURE_DESIGN.md), [D-006 proposal](M8_D006_DURABILITY_PROPOSAL.md) and [G8 plan](M8_VERIFICATION_PLAN.md).

## Compatibility and standards

DCSA Track & Trace is the reference for event terminology and adapter mapping. Record the exact specification release, supported subset, gaps, and contract-test results before making any compatibility claim. Internal event processing remains source-agnostic.

## API security gates

- No cross-organization reads, writes, enumeration, or bulk-export leakage.
- No unauthenticated ingestion or unbounded body size.
- All list queries, search filters, map responses, and error details enforce scope.
- Integration credentials are rotated and stored outside version control.
- Replay protection and duplicate-event response behavior are documented.


M4 endpoint behavior, tenant/role boundaries, optimistic versions and error envelopes passed [G4 executed verification](M4_EXECUTED_VERIFICATION.md) on `600d7c787da9f9cd4a70225cb3e3c38c093c0dc6`. D-003 Option B is accepted. No M5 shipment or M6 assignment endpoints are implied.

## M9 scoped work queue and redrive API (accepted on `main`)

The following routes are part of the accepted M9 implementation merged through [PR #18](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/18). [G9 closure](M9_G9_GATE_CLOSURE.md) and the [M9 local contract](M9_LOCAL_CONTRACT_DECISION.md) govern their behavior.

| Method | Implemented route | Permission | Response |
| --- | --- | --- | --- |
| GET | `/api/v1/organizations/{organizationId}/tracking-work` | `TrackingWorkRead`: Manager, Analyst, Auditor | Scoped status metadata; optional UUID cursor `after`, `limit` 1 to 100; `items`, `nextAfter` |
| GET | `/api/v1/organizations/{organizationId}/tracking-work/{receiptId}` | `TrackingWorkRead` | One tenant-scoped work item or `404 resource_not_found` |
| POST | `/api/v1/organizations/{organizationId}/tracking-work/{receiptId}/redrive` | `TrackingWorkRedrive`: Manager only | Sanitized `reason` (8 to 240 characters); `NeedsReconciliation` or `DeadLetter` only; at most three redrives |

The routes enforce current session, selected active organization, live membership and CSRF/Origin checks where applicable. Operator responses do not expose bearer material, source secrets, raw payloads or digests, and use `Cache-Control: no-store`. The accepted M8 post-commit acknowledgment contract remains unchanged. G9 acceptance alone did not include M16; the later independently accepted G16 implementation adds the separate audit-read endpoints below.

**Deployment restriction:** Sustained transport-event processing remains disabled until a verified M10 or separately approved mechanism safely manages `NeedsReconciliation` within the 10,000 unresolved-work cap. No public or production deployment is authorized.

## M16 audit events API (accepted on `main`)

The accepted [G16 gate closure](M16_G16_GATE_CLOSURE.md) and integrated [PR #25](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/25) establish these read-only HTTP routes. They require an authenticated, active, selected-organization **Auditor** membership and secure transport. Other roles, including Manager and Administrator, receive no implicit audit access.

| Method | Route | Contract |
| --- | --- | --- |
| GET | `/api/v1/organizations/{organizationId}/audit-events` | Bounded list with optional `page=1`, `limit` 1 to 100 and scope-bound `cursor`; returns `entries` and `nextCursor` |
| GET | `/api/v1/organizations/{organizationId}/audit-events/{auditId}` | Tenant-scoped audit entry or a sanitized missing/denied response |

Successful audit reads record their own audit-access events. Sensitive mutations and their audit entries commit in the same database transaction or fail closed. Responses use `Cache-Control: no-store`; there is no public audit write, export or global search route. The actor/source context and fixed metadata allowlist exclude secrets and raw tracking payloads. Retention policy is 365 days online followed by separately verified archival, without implemented automatic purge. M9 worker attempt/effect history remains a distinct immutable audit-of-record surface. The audit cursor retains its documented late-commit ordering caveat; see [G16 closure](M16_G16_GATE_CLOSURE.md).

## M10 tracking timeline and projection endpoints (G10 accepted and merged)

The [source-reconciled M10 candidate](M10_M16_SOURCE_RECONCILIATION.md) adds four authenticated routes under `/api/v1/organizations/{organizationId}/shipments/{shipmentId}`. These do **not** supersede the accepted M5 manual shipment workflow or the accepted M16 Auditor-only audit routes.

| Method | Suffix | Contract and roles |
| --- | --- | --- |
| GET | `/events` | Selected-organization shipment-read permission; `limit=1..100`, optional encrypted, short-lived `snapshot` and keyset `cursor`, M8/M9 provenance and bounded indexed pages. Late changes reject stale cursors. No raw payload or input digest. |
| GET | `/tracking-projection` | Same read scope; materialized per-container observed state, nullable ETA (not inferred from the current source profile), input revision token, materialization revision and stale flag. On stale inputs, observed state is `Unknown` until a rebuild. |
| POST | `/tracking-corrections` | Manager-only CSRF/Origin checks, current membership and session revalidation, UUID `Idempotency-Key`, append-only reasoned correction and generation precondition. |
| POST | `/tracking-projection/rebuild` | Manager-only CSRF/Origin checks and UUID idempotency key; serializable scope locking, atomically refreshed projection, unchanged input -> unchanged materialization revision. |

The original G10 tests verified twelve-migration routes on `3e81b88`. Subsequent accepted remediation added a signed, short-lived, scope-bound HTTP correction proof; restricted PostgreSQL verifies live Manager/session authority, writes one atomic M16 audit event and denies unauthorized replay. Correction authority was tested on `a26d19a`, and final migration/default-Compose changes on `e3002bd`. See [G10 closure](M10_G10_GATE_CLOSURE_20261107.md). Missing authority or required audit dependencies fail closed.

## M11/M12 local closure addendum (2026-11-08)

- Export and search return 403 (`organization_access_revoked` or `access_denied`) or 404 when access is revoked or the session organization changes while a request is in flight. Export retries up to three attempts internally on a serialization conflict and does not surface it as 503 unless the database remains unavailable.
- Deadlines measured locally: 15 second statement timeout and 25 second internal budget both return 503 `dependency_unavailable` with no body of CSV bytes; the budget response names the request budget.
- A worst-case 5,000-row export is about 4.9 MB, so the 10 MiB limit is enforced at the SQL function but not reachable over HTTP.
