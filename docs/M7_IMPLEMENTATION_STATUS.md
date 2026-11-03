# M7 Tracking Contracts and Source Authentication: Implementation Register

**State:** Implementation candidate committed; verification and acceptance pending.
**Working branch:** `feat/m7-tracking-source-validation`
**Baseline:** Accepted G0-G6 and M7 planning branch.
**G7:** Not executed or accepted.
**D-002:** Acceptance pending. The currently checked-in subset is provisional and may change after the exact upstream normative reference is pinned and reviewed.
**Deployment:** Local-only. No public hosting or successful tracking-event intake.

## Implemented source

| Area | Files | Behavior |
| --- | --- | --- |
| Integration-source schema | `IntegrationEntities.cs`, `20261103090000_IntegrationSources.cs`, `HarborOpsDbContext.cs` and snapshot | Tenant-bound source and credential tables, composite foreign keys, constraints and indexes |
| Credential lifecycle | `IntegrationSourceStore.cs`, `IntegrationSourcePolicy.cs` | Random 256-bit secrets, SHA-256 digests, one-time bearer generation, active organization checks, rotation overlap, immediate revocation and disablement |
| Restricted operations | `SourceCli.cs` | Explicit local operator opt-in and interactive create/rotate, with revoke, disable and redacted inspect |
| Event validation | `TrackingEventPolicy.cs`, `harborops-tracking-v1.schema.json` | Provisional equipment and transport subset, strict JSON, identifiers, explicit UTC, D-003 equipment syntax and bounded body |
| Integration API | `TrackingEndpoints.cs`, `IntegrationRequestLimiter.cs`, `Program.cs` | Source-bound bearer authorization, secure transport reuse, anonymous and authenticated limits, no-store envelopes, valid event returns 503 |
| Fixtures and tests | `tests/fixtures/tracking/`, `TrackingEventPolicyTests.cs`, `TrackingSourceHttpTests.cs`, `TrackingSourceMigrationTests.cs` | Source and tenant cases, negative parsing, migration upgrade, concurrency and no-acknowledgment assertions |
| Local runner | `scripts/verify-m7-local.sh` | E:-only preflight, isolated database, source-locked checks, test reports, audit stages, teardown and manifest |

## Contract boundaries

- `POST /integrations/v1/tracking-events` is a HarborOps-specific inbound interface, not the public DCSA publisher API.
- Accepted source credentials are never browser sessions and cannot select another tenant through input.
- Provisional profile: `tnt-2.2-harborops-v1`, protocol `harborops-tracking-v1`.
- Provisional supported events: Equipment GTIN/GTOT and Transport ARRI/DEPA with ACT classification; shipment, IoT, reefer and unknown variants fail closed.
- No M7 event table, durable receipt, worker processing, idempotent acknowledgment or projection exists.
- A valid authenticated event returns `503 intake_unavailable` and `Retry-After`. M8 owns durable receipt and D-006.

## Verification boundaries

The implementation has been committed and its source layout reviewed. No .NET compilation, PostgreSQL migration execution, Docker/Playwright run, dependency/image scan or independent G7 evidence review is claimed here.

The focused local runner does not by itself satisfy all G7 scenarios. In particular, browser/proxy/recovery tests, operator CLI interactive verification, source mutation checks and a complete evidence package require a full isolated G7 session. G7-S01 additionally requires D-002 acceptance and a pinned reference artifact.

**Required next gate:** Complete the G7-S01 through G7-S25 matrix on one exact source revision using disposable E:-backed resources, review the raw evidence, address any failures, then request explicit G7 acceptance before merging into `main`.

All prior accepted G0-G6 source and gate records remain unchanged. No GitHub Actions are authorized.
