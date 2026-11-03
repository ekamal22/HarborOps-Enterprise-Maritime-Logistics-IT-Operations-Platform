# M7 Tracking Contracts and Source Authentication: Architecture

**State:** Implementation candidate committed on a separate feature branch; D-002 and G7 acceptance pending. No runtime verification has been executed.
**Prepared:** 2026-11-03
**Planning branch:** `plan/m7-tracking-contracts-source-auth`. **Implementation branch:** `feat/m7-tracking-source-validation`
**Accepted base:** `main` at `97b67739cf1dad543bf25fa56326fb4702990f41`, G0-G6 accepted
**Work packages:** M7-01 reference contract, M7-02 source entitlements, M7-03 payload validation, M7-04 integration contract verification; G7 follows explicit acceptance
**Authorities:** [Roadmap](ROADMAP.md), [execution graph](execution-graph.json), [D-002 proposal](M7_D002_TRACKING_REFERENCE_PROPOSAL.md), [FR-002/006/007 and NFR-002/003/006/007](REQUIREMENTS.md), [API contracts](API_CONTRACTS.md), [Security](SECURITY.md), [G7 plan](M7_VERIFICATION_PLAN.md), [accepted G6](M6_G6_GATE_CLOSURE.md).

## 1. Scope and safe completion boundary

M7 establishes an organization-scoped integration-source identity, provisioned and revocable machine credentials, a narrowly versioned tracking-event JSON contract, strictly bounded parsing and semantic validation, stable API error behavior, and locally executable negative security contracts.

**M7 does not accept or persist tracking events.** The M8 gate owns the transactional inbox receipt, immutable provenance, collision and deduplication storage, and source-specific event key uniqueness. M9 owns worker claims, retry and dead-letter processing. M10 owns temporal projections and corrections. M7 must never return HTTP 200/201/202 for an event if no durable receipt was written. A valid and authenticated intake request returns a structured HTTP 503 `intake_unavailable` with `Retry-After` until M8 integrates and verifies the durable receipt transaction. Invalid requests are rejected before that response. The source and validation path can be verified without claiming delivery.

The HTTP endpoint is a HarborOps-specific adapter route, not an unmodified DCSA publisher interface. A proposed public DCSA 2.2.0 mapping profile is recorded in [D-002](M7_D002_TRACKING_REFERENCE_PROPOSAL.md); formal D-002 closure still requires approval and a pinned upstream artifact. The unverified source candidate does not constitute an accepted reference mapping. External partner credentials, paid data feeds, live AIS, dashboards, projection timelines, notification delivery, a hosted TLS deployment and M24 accessibility auditing are excluded.

## 2. Dependency architecture

```text
Accepted G3 reference-data foundation
             |
 M7-01 reference review and contract proposal
             |
  D-002 explicit acceptance and contract freeze
          /       \
 M7-02 source    M7-03 bounded
 identity        contract validation
          \       /
     M7-04 integration contracts
             |
      G7 evidence and approval
             |
       M8 contract and durable intake
```

M7-01 can prepare the pinned reference and mapping for the D-002 decision after G3. M7-02 and M7-03 may be developed independently only after M7-01 has frozen the approved schema and D-002 has been explicitly accepted. No new graph edges are required. Accepted G6 is available for regression and future M8 prerequisites but is not an artificial prerequisite for M7-01. M8 additionally needs D-006 and a durable database transaction before any successful ingestion acknowledgment.

## 3. Module boundaries and data flow

```text
Partner simulator or explicitly configured local source
    | Authorization: Bearer <opaque source credential>
    | Content-Type: application/json; UTF-8; explicit adapter version
    v
TLS / reviewed loopback-only local exception, trusted proxy, Host filtering
    v
ASP.NET Core endpoint /integrations/v1/tracking-events
    +-- correlation ID, no-store, body-size and rate limit
    +-- integration credential lookup and constant-time validation
    +-- source status, organization status and entitlement check
    +-- strict JSON shape, syntax, semantic and D-002 subset validation
    +-- canonical event DTO (memory only, zero durable effects)
    +-- HTTP 503 intake_unavailable, Retry-After until M8
    v
[Future M8] transaction -> inbox receipt -> immutable accepted event provenance
    v
[Future M9-M10] worker -> projections -> timelines
```

Place source credential and entitlement entities in `HarborOps.Infrastructure/Integrations/`; their pure validation and mapping policy in `HarborOps.Domain/Tracking/`; integration endpoint and transport guards in `HarborOps.Api/Integrations/`. Use `HarborOps.Application` for an explicit validation/identity service boundary where it avoids API-to-EF coupling. Reuse the existing DbContext, migration system, TimeProvider, scoped DI and correlation-ID envelope. Do not reuse browser cookie identity as an integration principal or grant integration credentials implicit human roles.

## 4. Source identity and tenancy

### 4.1 M7 tables, additive implementation candidate

`integration_sources`:
- `id uuid PRIMARY KEY`; `organization_id uuid NOT NULL` with restrictive FK to `organizations(id)`.
- `source_code varchar(80) NOT NULL` with explicit ASCII/lowercase normalization, unique per organization, including disabled rows.
- `display_name varchar(160) NOT NULL`; `status varchar(16) NOT NULL` in `Active,Disabled`; `version bigint NOT NULL CHECK(version > 0)`.
- `created_at_utc timestamptz`, `updated_at_utc timestamptz`, `disabled_at_utc timestamptz NULL`, server-controlled.
- Unique `(organization_id,id)` for future tenant-bound foreign keys. Index on `(organization_id,status,source_code)`. No hard-delete endpoint.

`integration_credentials`:
- `id uuid PRIMARY KEY`; `organization_id uuid NOT NULL`; `source_id uuid NOT NULL` with composite `(organization_id,source_id)` FK to `integration_sources(organization_id,id)` and `ON DELETE RESTRICT`.
- `secret_digest bytea NOT NULL`, fixed 32 bytes, unique `id` lookup; never store a bearer secret. `key_version varchar(24)` identifies digest format only.
- `not_before_utc timestamptz`, `expires_at_utc timestamptz`, `revoked_at_utc timestamptz NULL`, `created_at_utc timestamptz`, `last_used_at_utc timestamptz NULL`; `CHECK(expires_at_utc > not_before_utc)`.
- Explicit indexes `(organization_id,source_id,revoked_at_utc)`, `(expires_at_utc)`. Test fail-closed under unique-constraint and FK races.

One M7 source belongs to exactly one organization. No shared global credential or cross-organization token reuse. An organization may have several sources; use separate credentials per source and organization. Entitlements are the immutable credential-to-source/organization binding plus a dedicated `tracking_event_validate` capability. Do not store scopes supplied by callers. Fine-grained shipment subscriptions, delivery destinations and device contracts are future decisions.

### 4.2 Credential issuance and lifecycle

- Provision with a restricted local-only `--source-cli` subcommand, explicit `HARBOROPS_ALLOW_SOURCE_CLI=true` opt-in, operator privilege and environment isolation. Planned operations: `create`, `rotate`, `revoke`, `disable`, `inspect`. No public UI or account self-service route.
- Generate at least 256 bits of cryptographically secure random secret, rendered once in a protected interactive terminal, never command arguments, shell history, stdout redirected into a log, EF SQL logging or structured telemetry. Prefix the opaque bearer with non-secret credential ID and version for indexed lookup. Never derive credentials from email, passwords or human session identifiers.
- Compute SHA-256 of the independent high-entropy bearer secret, compare to the stored digest using `CryptographicOperations.FixedTimeEquals`, and distinguish no credential status in public errors. Password hashing remains Argon2id for human passwords; bearer secrets are high-entropy random keys.
- Permit at most two active credentials per source for a deliberate short rotation overlap, with configurable maximum expiry (proposed 90 days) and maximum overlap (proposed 24 hours). Issue the replacement before revoking the predecessor. Disable/revoke/expiry takes effect on the next request by direct database validation, with no positive authorization cache.
- Validate source `Active`, organization `Active`, credential window `not_before <= now < expires`, non-revocation and entitlement on every request. Fail closed on database outages and ambiguous credentials. Avoid writing `last_used_at_utc` as a mandatory per-request mutation; if updated, do so only after safe authentication and never let it imply an accepted event.
- Unknown/invalid/expired/revoked credentials return the same generic 401 and do not reveal source identity. A valid authenticated source lacking the required capability is denied with a safe 403. Untrusted/foreign resource references must not reveal existence.

### 4.3 Transport and request authentication

- Use `Authorization: Bearer` exclusively for integration endpoints. Do not recognize cookies, CSRF tokens or browser session membership as integration authorization. Cookie plus missing bearer still fails. Reject duplicate Authorization headers, unusually large values and ambiguous schemes.
- Require HTTPS for real remote peers, with forwarded scheme/address honored only from configured trusted proxies and a bounded hop count. An explicitly configured loopback-only development exception may be used if it also verifies actual loopback/trusted configured local peer and local Host; never trust `Host: localhost` alone.
- Do not enable permissive CORS. Even though CORS is a browser safeguard rather than credential authentication, reject cross-origin browser-only credentials and exclude tokens from browser bundles.
- Enforce an anonymous/invalid-source limiter by verified remote peer and a successful-source limiter by source ID, with queue length zero. Proposed baseline: anonymous 10 requests/minute/peer and authenticated 60 requests/minute/source, configurable, with `429` and retry information; rate controls do not replace tenant authorization.
- Emit only source ID, tenant ID, safe result class, duration, byte length and correlation ID after authentication; invalid credentials use generalized identifiers. No raw Authorization/Cookie headers, parsed payload values, network token fragments, upstream raw JSON or sensitive URL query contents in logs.

## 5. Internal tracking-event contract

### 5.1 Versioning and shape

- Route: `POST /integrations/v1/tracking-events`. `v1` is the HarborOps inbound protocol revision.
- Media type: `application/json`; document UTF-8 behavior and reject unsupported charset or media types with 415. Explicit `X-HarborOps-Tracking-Profile: tnt-2.2-harborops-v1` header when D-002 is accepted; unknown/missing profile rejected with stable `unsupported_tracking_profile`.
- One event per request, object root only. Proposed **16 KiB maximum raw body**, enforced while streaming even without Content-Length, maximum JSON depth 12, no duplicate keys, no unknown keys, no comments/trailing commas, no invalid UTF-8, no NaN/infinity, no Unicode control characters in identifiers.
- Candidate wire object (final names and code values require D-002 approval):

```json
{
  "sourceEventId": "DEMO-EVENT-0001",
  "externalSchemaVersion": "2.2.0",
  "eventFamily": "Equipment",
  "eventCode": "GTIN",
  "eventClassifier": "ACT",
  "occurredAtUtc": "2026-11-03T00:00:00Z",
  "equipmentReference": "SYN-BOX-ABCD",
  "transportReference": null,
  "shipmentReference": null,
  "locationCode": null
}
```

This is a HarborOps-specific illustrative fixture, not a DCSA-conformant payload. Exact DCSA code/field usage must be validated against the pinned artifact. `eventFamily` selects a strictly specified schema variant; require the corresponding reference and forbid irrelevant fields or invalid null combinations. Use a canonical normalized DTO separate from raw upstream schema fixtures, with source/tenant identity supplied by the server only.

### 5.2 Validation policy

- IDs: `sourceEventId` required, 1-128 ASCII printable characters from an allowlisted restricted alphabet; reject whitespace-only, controls and invalid normalization. Do not assume globally unique IDs. M8 will enforce scoped event identity and detect same-key/different-payload collisions.
- Timestamps: explicit UTC `Z` or `+00:00` only, strictly parsed, no locale ambiguity or leap-second invention. Proposed supported occurrence range: 2000-01-01 UTC through server time + 24 hours; this range is policy and must be tested/approved, not confused with source acceptance semantics.
- References: equipment codes must satisfy the accepted D-003 distinction between ISO6346 `U` and visibly synthetic `SYN-BOX-` identifiers when interpreted as HarborOps container references. Do not infer verified physical equipment from a syntactically valid value. Transport/shipment references use explicitly bounded syntax; ownership and association reconciliation are deferred to M8.
- Codes/classifiers: only approved profile values. Unsupported known categories and unknown future variants return a stable 422. Do not quietly coerce a code, turn an unknown event into a location update, or silently ignore extra fields.
- Scope: clients may not supply `organizationId`, `sourceId`, `tenantId` or any authoritatively derived field. The authenticated source determines organization and provenance. No M7 shipment or container lookup is required to indicate input syntax validity; M8 determines business-resource reconciliation.
- Every failure is side-effect-free on tracking domain state. Security credential provisioning and revocation are the only new persistent writes in M7.

### 5.3 Response and error contract

| Situation | HTTP | Stable code |
| --- | ---: | --- |
| Missing, malformed, invalid, expired or revoked bearer | 401 | `source_authentication_required` |
| Authenticated source without integration capability or disabled organization | 403 | `source_access_denied` |
| Unsupported media type / profile | 415 / 422 | `unsupported_media_type` / `unsupported_tracking_profile` |
| Malformed JSON, duplicate/unknown fields, invalid timestamp/identifier | 400 | `invalid_tracking_event` |
| Unsupported event family/code/classifier | 422 | `unsupported_tracking_variant` |
| Oversized or unbounded input | 413 | `payload_too_large` |
| Rate limit | 429 | `rate_limited` |
| Valid, authorized, bounded event **before M8** | 503 | `intake_unavailable` |
| Credential store/database unavailable | 503 | `dependency_unavailable` |
| Unexpected server failure | 500 | `internal_error` |

Every JSON error contains `code`, safe `message` and `correlationId`; responses have `Cache-Control: no-store`, no token echoes and no user-supplied raw payload excerpts. Set `Retry-After` on unavailable intake and throttling. A valid request's 503 must not create an event receipt, and clients must not be told to discard or mark the event delivered. M8 will replace this 503 only after durable atomic receipt and replay contracts pass.

## 6. Database, migration and concurrency

- Add one EF Core migration `IntegrationSources` after the accepted M6 migration `20261102080000_ShipmentAssignments`; do not edit previous migrations or bootstrap history. Use PostgreSQL 16-compatible columns, positive version and lifecycle checks, composite tenant FKs and unique indexes. Validate no pending EF model changes.
- Provision/rotation/revocation must be transactional: lock the organization and source in a deterministic order, recheck active organization/source, validate remaining credential count/window and version before commit, prevent concurrent rotation from bypassing the maximum active keys.
- No direct UPDATE to credential digests after issuance; rotate by inserting a new credential and revoking the predecessor. No hard deletion while the source is provisioned. Revoke old credentials without disrupting other tenants' source IDs.
- Migration rollback may remove only M7-owned tables, on a disposable database. Preserve all accepted M0-M6 rows and constraints during upgrade/rollback and reapply. No new inbox, event-history, projection, dead-letter or outbox table in M7.
- In the source verification path, credential revocation races must fail closed at the authorization boundary. M7 promises no durable event effect, so a post-validate revocation cannot cause a false acknowledgment. M8 must revalidate source authorization within the eventual receipt transaction.

## 7. Failure and threat model

- **BOLA, tenant spoofing:** source and tenant come from DB-bound credential, never HTTP body, headers or route client selection.
- **Replay:** M7 deliberately does not provide replay/idempotency acceptance. A replayed valid request also yields 503, not 202. M8 must introduce effect-once behavior under a stable source-specific key.
- **Credential theft:** TLS required off loopback, one-time secret provisioning, no token in logs, limited lifetimes, immediate revocation, independent rotation and no cookies.
- **Host and forwarded-header forgery:** reuse the existing reviewed network-peer checks; do not create a new Host-only bypass for integration routes.
- **Payload resource exhaustion:** enforce body size at ingress and parser, depth, finite strings, controlled allocation, bounded JSON and rate limits. Reject zip bombs and unsupported compressed/multipart request bodies.
- **Database outage:** 503 with correlation ID, no acceptance, no authorization fallback.
- **Side-channel data:** do not distinguish invalid from unknown/revoked bearer details or expose existence of foreign organization/related business objects.
- **Compatibility drift:** freeze exact external source version and fixtures; unsupported future fields/codes never silently mutate the contract.
- **Operational safety:** run all development and verification locally on E:, with disposable Compose/database resources, with GitHub Actions disabled and no hosting authorization.

## 8. Deferred contracts handed to M8-M10

The M7 canonical DTO must carry stable `sourceId`, `organizationId`, `sourceEventId`, external profile, event family/code/classifier, UTC occurrence, normalized references and safe provenance metadata **in memory**. M8 designs durable receipt IDs, payload digest, source-event key collision policy, atomic inbox writes, immutable accepted-event history, organization/reference reconciliation, and the durable acknowledgment format under D-006. M9 designs worker retry and dead-letter behavior; M10 designs deterministic timeline and correction semantics.

Do not introduce these storage, retry or projection features into M7 by convenience. Their downstream gates remain independent.

## 9. Acceptance boundary

M7 planning may be reviewed now; D-002 is not accepted by this architecture document. Implementation proceeds only after D-002's exact public artifact, initial subset and profile mapping are accepted. The [G7 matrix](M7_VERIFICATION_PLAN.md) requires a source-locked local run, complete negative authentication, transport, tenant, payload and upgrade checks, and explicit acceptance before merging implementation into protected `main`. No GitHub Actions run, public hosting, or publication-license change is authorized.
