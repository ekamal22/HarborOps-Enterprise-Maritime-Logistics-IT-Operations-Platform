# M7 Tracking Contracts and Source Authentication: Implementation Register

**State:** M7 and G7 explicitly accepted on 2026-11-03 and integrated into `main` through PR #10 and PR #11. D-002 Option A accepted. See [G7 closure](M7_G7_GATE_CLOSURE.md).
**Working branch:** `feat/m7-tracking-source-validation`
**Baseline:** Accepted G0-G6 and M7 planning branch.
**G7:** Accepted 2026-11-03 after one source-locked run and three verified supplements; original pre-decision outcomes preserved in [executed verification](M7_EXECUTED_VERIFICATION.md).
**D-002:** Accepted 2026-11-03 (Option A). The checked-in subset is the accepted subset, pinned to the [Track & Trace 2.2.0 reference snapshots](../third_party/dcsa-track-and-trace/README.md), which are not official immutable DCSA releases.
**Deployment:** Local-only. No public hosting or successful tracking-event intake.

The [correctness remediation register](M7_CORRECTNESS_REMEDIATION.md) and [second correctness audit](M7_SECOND_CORRECTNESS_AUDIT.md) track implementation defects, corrections and unresolved runtime evidence.

## Implemented source

| Area | Files | Behavior |
| --- | --- | --- |
| Integration-source schema | `IntegrationEntities.cs`, `20261103090000_IntegrationSources.cs`, `HarborOpsDbContext.cs` and snapshot | Tenant-bound source and credential tables, composite foreign keys, constraints and indexes |
| Database lifecycle enforcement | M7 migration source and credential triggers | Immutable credential identity/digests, non-deletable records, two-overlap limit, monotonic revocation and source transitions |
| Credential lifecycle | `IntegrationSourceStore.cs`, `IntegrationSourcePolicy.cs` | Random 256-bit secrets, SHA-256 digests, one-time bearer generation, organization-first transactional locks, tracked-state refresh, locked bearer validation, coherent created/not-before windows, constrained rotation, immediate revocation and disablement |
| Restricted operations | `SourceCli.cs` | Explicit local operator opt-in and interactive create/rotate, with revoke, disable and redacted inspect |
| Event validation | `TrackingEventPolicy.cs`, `harborops-tracking-v1.schema.json` | Accepted equipment and transport subset, canonical eventFamily/eventCode JSON, absolute-end identifiers, explicit UTC, source-bound identity, D-003 equipment syntax and bounded body |
| Proxy integration | `web/nginx.conf`, `web/nginx.hosted.conf.template`, `tests/browser/specs/tracking-ingress.spec.mjs` | Proxy the integration route to the API, preserve trusted forwarding and reject unauthenticated browser requests |
| Integration API | `TrackingEndpoints.cs`, `IntegrationRequestLimiter.cs`, `Program.cs` | Source-bound bearer authorization, secure transport reuse, anonymous and authenticated limits, no-store envelopes, valid event returns 503 |
| Fixtures and tests | `tests/fixtures/tracking/`, `TrackingEventPolicyTests.cs`, `TrackingSourceHttpTests.cs`, `TrackingSourceMigrationTests.cs`, `TrackingSourceSqlTests.cs`, `TrackingSourceBoundaryHttpTests.cs`, `TrackingLifecycleRefreshTests.cs`, `TrackingRequestLimiterTests.cs` | Expanded HTTP and source tests, populated M6 preservation, SQL immutability, overlap and tenant checks, concurrency and no-acknowledgment assertions |
| Historical regression compatibility | Existing migration upgrade suites | Pin M6-specific migration tests to their accepted target and extend latest-schema assertions for M7 |
| Local runner | `scripts/verify-m7-local.sh`, `scripts/check-m7-source-cli-interactive.sh` | E:-only preflight, disposable SDK/PostgreSQL, source-locked .NET tests, five security mutations, isolated G7 network, browser fixtures, three-engine regression, source outage/recovery, image audits and hashed teardown evidence |

## Contract boundaries

- `POST /integrations/v1/tracking-events` is a HarborOps-specific inbound interface, not the public DCSA publisher API.
- Accepted source credentials are never browser sessions and cannot select another tenant through input.
- Profile: `tnt-2.2-harborops-v1`, protocol `harborops-tracking-v1`, HarborOps-specific and not DCSA conformant. Events in the DCSA wire format cannot be consumed directly; a translation layer is required.
- Supported events (accepted under D-002): Equipment GTIN/GTOT and Transport ARRI/DEPA with ACT classification; shipment, IoT, reefer and unknown variants fail closed.
- No M7 event table, durable receipt, worker processing, idempotent acknowledgment or projection exists.
- A valid authenticated event returns `503 intake_unavailable` and `Retry-After`. M8 owns durable receipt and D-006.

## Verification boundaries

Development runs on the E:-backed workstation found and corrected the defects listed in the [remediation register](M7_CORRECTNESS_REMEDIATION.md). They are not gate evidence. The gate evidence is the single complete run on `7b87f3250aba677b1f4aeef4350803d9ca7b6880` recorded in the [executed verification](M7_EXECUTED_VERIFICATION.md), with supplements for G7-S06 and G7-S25.

The local runner schedules the full gate: source-locked .NET build and tests with a repeat integration run, five controlled security mutations in a disposable E:-backed export, dependency advisories, web build, an isolated G7 network, browser fixtures, three-engine regression, source outage and recovery, exact-image scans, a per-scenario evidence summary and hashed teardown evidence. The protected interactive operator CLI lifecycle was performed separately by the operator and verified against the database (G7-S06 supplement). The D-002 reference snapshots were pinned, license-reviewed and checked in a separate supplement (G7-S01 and G7-S17); see the [executed verification](M7_EXECUTED_VERIFICATION.md).

**Next milestone:** G7 is closed. M8-01 inbox contract work is eligible, but D-006, durable intake implementation, G8 verification and acceptance remain pending. G7 retains the HTTP 503 barrier until M8 has an atomic receipt.

All prior accepted G0-G6 source and gate records remain unchanged by M7 integration. No GitHub Actions are authorized.
