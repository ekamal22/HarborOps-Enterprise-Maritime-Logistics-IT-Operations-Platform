# API Contracts

## Contract status

M0 provides public synthetic status and health routes. M1 implements accepted protected authentication endpoints. M2 organization and membership routes are implemented on `main` following accepted G2 and merged PR #5, verified locally for supported endpoint, verb, role and foreign-identifier scenarios (see [executed evidence](M2_EXECUTED_VERIFICATION.md)). M3-M6 maritime, shipment and assignment APIs, M7 source authentication, M8 durable receipt intake and M9 tracking-work query/redrive are accepted on `main`. Full public OpenAPI generation remains planned. M10 timeline/projection endpoints and M16 audit-read APIs are not accepted.

## Observed M0 contract verification

At code revision `4fb34eb`, the API was tested against real PostgreSQL and through the HTTP pipeline. A local database outage produced controlled 503 responses from readiness and baseline endpoints, with liveness remaining HTTP 200 and a correlation identifier included in the baseline error/log. The tests verified the four-field synthetic status contract. The corrected .NET 10 runtime was subsequently verified in [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md), with G0 **accepted** on 2026-10-31. The `4fb34eb` check remains historical. That M0 statement is historical: identity, maritime, shipment and assignment resources were subsequently implemented and accepted under G1-G6; M7 tracking intake remains unverified and unmerged.

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
| GET | /organizations/{orgId}/exports/shipments | Scoped bounded CSV export | Explicit export permission |
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

The routes enforce current session, selected active organization, live membership and CSRF/Origin checks where applicable. Operator responses do not expose bearer material, source secrets, raw payloads or digests, and use `Cache-Control: no-store`. The accepted M8 post-commit acknowledgment contract remains unchanged. No M10 timeline API or M16 audit endpoint is implied by G9 acceptance.

## M10 candidate HTTP endpoints (not accepted)

The M10 branch registers the following tenant-scoped routes under `/api/v1/organizations/{organizationId}/shipments/{shipmentId}`, separate from the M5 manual shipment workflow:

| Method | Path suffix | Role and semantics |
| --- | --- | --- |
| GET | `/events` | Analyst, Manager and Auditor via `ViewShipmentData`; `limit=1..100`, optional protected keyset `cursor` and encrypted `snapshot` bound to organization/shipment and 20-minute expiration; current provenance page, `eventCount`, `nextCursor`, correction/materialization fields. No raw digest or payload. |
| GET | `/tracking-projection` | Same read scope; per-container `observedTrackingState` (`Unknown`, `EquipmentIn`, `EquipmentOut`, `Mixed`, `Conflict`), protected `snapshotToken`, at most 50 per-container observations, UTC provenance, nullable ETA, materialization revision and stale flag. No raw input digest. |
| POST | `/tracking-corrections` | Manager only, CSRF/Origin, live session/role recheck and mandatory UUID `Idempotency-Key`; exact target and optional same-shipment replacement, expected correction generation, sanitized reason. Append-only, rejects cycles and stale generations; duplicate request returns durable replay response. |
| POST | `/tracking-projection/rebuild` | Manager only, CSRF/Origin, live session/role recheck and mandatory UUID `Idempotency-Key`; serializable derived-field/digest rebuild, durable replay. Both M10 mutations have a 10-per-minute authenticated-user rate limit. |

Corrections do not overwrite accepted events or M9 effects. A void correction is visible as superseded even when its replacement is null. Historical event pagination invalidates stale cursors rather than silently skipping a late arrival. Status observations are **not** M5 `workflowStatus` transitions. All M10 routes and migration are implementation candidates until source-locked G10 verification and acceptance. [Implementation register](M10_IMPLEMENTATION_STATUS.md) and [correctness remediation](M10_CORRECTNESS_REMEDIATION.md).

**Deployment restriction:** Sustained transport-event processing remains disabled until a verified M10 or separately approved mechanism safely manages `NeedsReconciliation` within the 10,000 unresolved-work cap. No public or production deployment is authorized.
