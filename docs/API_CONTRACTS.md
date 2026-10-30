# API Contracts

## Contract status

M0 implements only `GET /api/v1/system/baseline`, `GET /health/live`, and `GET /health/ready`. All other resource routes below remain proposed. Live OpenAPI generation, full schemas, authentication, and protected business routes are deferred.

## Common conventions

- Base path: /api/v1
- Content type: application/json
- Transport: HTTPS when remotely accessible
- Authentication: secure session or bearer token, chosen by ADR before implementation
- Organization context: derived from authorized membership and explicitly selected organization
- Timestamps: ISO 8601 UTC
- Pagination: bounded limit, continuation token where useful
- Request correlation: correlation ID echoed in responses and structured logs
- Writes: explicit validation, idempotency where supported, conflict detection

## Implemented M0 endpoint

`GET /api/v1/system/baseline` reads the seeded `bootstrap_records` row in PostgreSQL. Its successful JSON object has `service`, `datasetVersion`, `seededAtUtc`, and `source` fields, where `source` is `synthetic`. No customer data is exposed.

`GET /health/live` reports process liveness without checking dependencies. `GET /health/ready` checks PostgreSQL connectivity and the baseline seed and returns HTTP 503 if unhealthy. Both return only a minimal `status` string.

The baseline endpoint returns a safe `baseline_unavailable` error when the record is missing and a `dependency_unavailable` response for recognized database failures. Unhandled errors use a sanitized `internal_error` envelope. Every API response is intended to carry `X-Correlation-ID`; this behavior is covered by contract tests and was observed against the running stack on 2026-10-30.

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

## Compatibility and standards

DCSA Track & Trace is the reference for event terminology and adapter mapping. Record the exact specification release, supported subset, gaps, and contract-test results before making any compatibility claim. Internal event processing remains source-agnostic.

## API security gates

- No cross-organization reads, writes, enumeration, or bulk-export leakage.
- No unauthenticated ingestion or unbounded body size.
- All list queries, search filters, map responses, and error details enforce scope.
- Integration credentials are rotated and stored outside version control.
- Replay protection and duplicate-event response behavior are documented.
