# M7 Tracking Contracts and Source Authentication: Implementation Register

**State:** Correctness-audit remediation candidate committed; runtime verification and acceptance pending.
**Working branch:** `feat/m7-tracking-source-validation`
**Baseline:** Accepted G0-G6 and M7 planning branch.
**G7:** Not executed or accepted.
**D-002:** Acceptance pending. The currently checked-in subset is provisional and may change after the exact upstream normative reference is pinned and reviewed.
**Deployment:** Local-only. No public hosting or successful tracking-event intake.

The [correctness remediation register](M7_CORRECTNESS_REMEDIATION.md) tracks the implementation defects, fixes and unresolved runtime checks.

## Implemented source

| Area | Files | Behavior |
| --- | --- | --- |
| Integration-source schema | `IntegrationEntities.cs`, `20261103090000_IntegrationSources.cs`, `HarborOpsDbContext.cs` and snapshot | Tenant-bound source and credential tables, composite foreign keys, constraints and indexes |
| Database lifecycle enforcement | M7 migration source and credential triggers | Immutable credential identity/digests, non-deletable records, two-overlap limit, monotonic revocation and source transitions |
| Credential lifecycle | `IntegrationSourceStore.cs`, `IntegrationSourcePolicy.cs` | Random 256-bit secrets, SHA-256 digests, one-time bearer generation, organization-first transactional locks, one-query locked bearer validation, constrained rotation, immediate revocation and disablement |
| Restricted operations | `SourceCli.cs` | Explicit local operator opt-in and interactive create/rotate, with revoke, disable and redacted inspect |
| Event validation | `TrackingEventPolicy.cs`, `harborops-tracking-v1.schema.json` | Provisional equipment and transport subset, canonical eventFamily/eventCode JSON, absolute-end identifiers, explicit UTC, source-bound identity, D-003 equipment syntax and bounded body |
| Proxy integration | `web/nginx.conf`, `web/nginx.hosted.conf.template`, `tests/browser/specs/tracking-ingress.spec.mjs` | Proxy the integration route to the API, preserve trusted forwarding and reject unauthenticated browser requests |
| Integration API | `TrackingEndpoints.cs`, `IntegrationRequestLimiter.cs`, `Program.cs` | Source-bound bearer authorization, secure transport reuse, anonymous and authenticated limits, no-store envelopes, valid event returns 503 |
| Fixtures and tests | `tests/fixtures/tracking/`, `TrackingEventPolicyTests.cs`, `TrackingSourceHttpTests.cs`, `TrackingSourceMigrationTests.cs`, `TrackingSourceSqlTests.cs`, `TrackingSourceBoundaryHttpTests.cs` | Expanded HTTP and source tests, populated M6 preservation, SQL immutability, overlap and tenant checks, concurrency and no-acknowledgment assertions |
| Historical regression compatibility | Existing migration upgrade suites | Pin M6-specific migration tests to their accepted target and extend latest-schema assertions for M7 |
| Local runner | `scripts/verify-m7-local.sh`, `scripts/check-m7-source-cli-interactive.sh` | E:-only preflight, disposable SDK/PostgreSQL, source-locked .NET tests, browser fixtures, three-engine regression, source outage/recovery, image audits and hashed teardown evidence |

## Contract boundaries

- `POST /integrations/v1/tracking-events` is a HarborOps-specific inbound interface, not the public DCSA publisher API.
- Accepted source credentials are never browser sessions and cannot select another tenant through input.
- Provisional profile: `tnt-2.2-harborops-v1`, protocol `harborops-tracking-v1`.
- Provisional supported events: Equipment GTIN/GTOT and Transport ARRI/DEPA with ACT classification; shipment, IoT, reefer and unknown variants fail closed.
- No M7 event table, durable receipt, worker processing, idempotent acknowledgment or projection exists.
- A valid authenticated event returns `503 intake_unavailable` and `Retry-After`. M8 owns durable receipt and D-006.

## Verification boundaries

The implementation has been committed and reviewed statically. The EF mapping corruption has been repaired and model/snapshot mapping equality checked. No .NET compilation, PostgreSQL migration execution, Docker/Playwright run, dependency/image scan or independent G7 evidence review is claimed here.

The local runner now schedules browser/proxy/recovery checks and full accepted-source regressions, but has not been executed. Protected interactive operator CLI issuance, decision-specific source mutations, pinned upstream fixture review, and independent evidence assessment remain outside automatic G7 closure. G7-S01 additionally requires D-002 acceptance and a pinned reference artifact.

**Required next gate:** Complete the G7-S01 through G7-S25 matrix on one exact source revision using disposable E:-backed resources, review the raw evidence, address any failures, then request explicit G7 acceptance before merging into `main`.

All prior accepted G0-G6 source and gate records remain unchanged. No GitHub Actions are authorized.
