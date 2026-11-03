# M7 Tracking Contracts and Source Authentication: Implementation Plan

**State:** Implemented and technically verified locally ([executed verification](M7_EXECUTED_VERIFICATION.md)); D-002 accepted 2026-11-03 (Option A); G7 acceptance pending.
**Prepared:** 2026-11-03
**Branches:** Planning `plan/m7-tracking-contracts-source-auth`; implementation candidate `feat/m7-tracking-source-validation`
**Starting baseline:** `main` `97b67739cf1dad543bf25fa56326fb4702990f41` with G0-G6 accepted
**Decision gate:** [D-002 proposal and decision record](M7_D002_TRACKING_REFERENCE_PROPOSAL.md), accepted 2026-11-03 (Option A)
**Design:** [M7 architecture](M7_ARCHITECTURE_DESIGN.md)
**Verification:** [G7 test matrix](M7_VERIFICATION_PLAN.md)
**Dependencies:** [Execution graph](execution-graph.json), [Roadmap](ROADMAP.md), [Parallel Delivery Policy](PARALLEL_DELIVERY_POLICY.md)

## 1. Delivery contract

Implement the M7 interface as a source-authenticated contract that can validate but **cannot acknowledge receipt of events** until M8 adds durable atomic intake. After M7, a valid authorized POST to `/integrations/v1/tracking-events` returns HTTP 503 `intake_unavailable` with `Retry-After`. Do not return 202 for parsed or memory-only data. No tracking timeline, accepted event history, retry queue, dead-letter table, partner callback, browser event workspace or map is included.

Preserve existing authorization, schema and accepted M0-M6 behavior. Implement a dedicated machine-source principal rather than reusing human sessions or introducing general OAuth hosting. Core services use the existing .NET 10, EF Core 10, PostgreSQL 16 and Docker Compose stack. All test resources are disposable and E:-backed.

## 2. Milestone execution graph

| Work package | Depends on | Outputs | Complete when |
| --- | --- | --- | --- |
| **M7-01** Pin external subset and canonical contract | Accepted G3; D-002 approval (2026-11-03, Option A) closed this work package | Immutable upstream reference, schema profile, mapping register, representative allowlisted fixtures, error contract | Every supported/unsupported field and code is grounded in pinned reference, hashes recorded and decision explicitly accepted |
| **M7-02** Implement source entitlements | M7-01 | Additive source/credential migration, provisioning/rotation/revoke CLI, server-side bearer verification, source/tenant scope and limiters | Live database auth, expiry, rotation, disabled org, forged source and concurrent revoke tests pass |
| **M7-03** Implement strict payload validation | M7-01 | Canonical DTO, exact-profile JSON parser, bounded body/depth, UTC and identifier policies, supported event mapping and safe errors | Deterministic positive parse and exhaustive invalid/unsupported/over-limit negative matrix pass |
| **M7-04** Verify integration contracts | M7-02 and M7-03 | HTTP composition, 503 no-ack behavior, full G7 matrix, migration/tenant regression, evidence and docs | All G7 required cases executed and recorded against one source SHA; no critical unresolved defects |
| **G7** Acceptance gate | All four M7 work packages | Independent local evidence review, explicit decision and approval | Acceptance recorded separately from technical PASS and merge authorization |

M7-02 and M7-03 may proceed in parallel after M7-01 is frozen. Use different branches/worktrees for parallel tasks and reconcile generated EF migrations into one canonical order before M7-04 integration. The original source baseline and accepted M6 migration must not be mutated.

## 3. M7-01: contract and reference freeze

1. Fetch the exact public DCSA TNT 2.2.0 reference OpenAPI or normative schema from a reviewed official source. Record immutable upstream identity, version, hash, license and retrieval date. If inaccessible, stop D-002 acceptance and do not substitute a vendor schema without recording the variance. Outcome: the hosted SwaggerHub definitions were retrieved on 2026-11-03 and pinned as local snapshots; no tagged release exists.
2. Record an explicit supported subset by family, event code, classifier, mandatory reference fields and timestamp representation; maintain a table for unsupported family/code/field combinations.
3. Freeze `harborops-tracking-v1` wire DTO, `tnt-2.2-harborops-v1` profile name and transport/response error contract. Keep upstream and internal models separate.
4. Create checked-in JSON Schema or equivalent validator schema and curated synthetic positive/negative fixtures under `contracts/tracking/v1/` and `tests/fixtures/tracking/`. Include licensing/attribution for any redistributed upstream material; write original synthetic examples when rights are uncertain.
5. Resolve whether the proposed default event-date policy (2000-01-01 UTC to now + 24 hours) and the candidate first event codes precisely match the pinned reference and the desired business scope. Resolved: the first event codes and the classifier are confirmed in the pinned upstream enumerations, and the event-date window is a HarborOps profile rule that the D-002 decision did not change.
6. D-002 was accepted on 2026-11-03 (Option A) and the implementation was reconciled against the pinned reference; the supported subset is unchanged.

**Artifacts:** `docs/M7_D002_TRACKING_REFERENCE_PROPOSAL.md`, `docs/M7_ARCHITECTURE_DESIGN.md`, `docs/API_CONTRACTS.md`, contract schema and fixture manifest.

## 4. M7-02: source credentials and entitlement implementation

### Proposed file work

- `src/HarborOps.Infrastructure/Integrations/IntegrationEntities.cs`: source and credential entities and lifecycle state.
- `src/HarborOps.Infrastructure/Integrations/IntegrationSourceStore.cs`: tenant-bound credential issuance, validation, rotation, revocation and source disablement. Use typed result errors and cancellation tokens.
- `src/HarborOps.Domain/Tracking/IntegrationSourcePolicy.cs`: pure source/credential lifecycle rules, clock window, rotation overlap and capability policy.
- `src/HarborOps.Api/Integrations/SourceCli.cs`: restricted local-only opt-in provisioning, rotation, revocation, inspect and disable.
- `src/HarborOps.Api/Integrations/IntegrationAuthentication.cs`: strict bearer parsing, constant-time digest check, source authorization and security boundary.
- `src/HarborOps.Infrastructure/Persistence/HarborOpsDbContext.cs`: EF entities, constraints, indexes and composite FKs.
- `src/HarborOps.Infrastructure/Persistence/Migrations/<timestamp>_IntegrationSources.cs` and corresponding model snapshot: additive migration after M6.
- `src/HarborOps.Infrastructure/DependencyInjection.cs`, `src/HarborOps.Api/Program.cs`: explicit registration, middleware and `--source-cli` dispatch.
- `src/HarborOps.Api/appsettings.json` and local-only configuration: default-deny integration transport, bounded limits and trusted peer settings; no embedded secret.

### Build order

1. Implement PostgreSQL schema and model configuration including source unique keys, organization-bound composite FKs, credential lifetime checks, no hard deletion and indexes.
2. Implement cryptographically random 256-bit secrets, digests, constant-time comparison, credential version parser, duplicate-header rejection and generic 401.
3. Implement source create/rotate/revoke/disable as a restricted CLI using serializable or compatible lock and transaction discipline. Provision no default source or secret in migrations, fixtures outside tests or Compose.
4. Enforce per-request direct DB validation: credential lifetime, revocation, active organization, active source and `tracking_event_validate` capability; no fallback to browser session or cached authorizations.
5. Add rate limits for unknown callers and authenticated source IDs without trusting forwarded addresses from arbitrary peers.
6. Verify two-tenant source isolation, simultaneous rotation, old/new overlap, concurrent revoke, expired source, disabled organization, DB outage, credential redaction and cancellation.

**Definition of done:** credentials cannot be used across tenants, revoked source access fails on the next request, no secret enters logs or Git, and accepted M1/M2 authentication behavior is unchanged.

## 5. M7-03: bounded event schema and mapper

### Proposed file work

- `src/HarborOps.Domain/Tracking/TrackingEventContract.cs`: immutable canonical DTO and typed family/code/classifier enums or validated string policies.
- `src/HarborOps.Domain/Tracking/TrackingEventPolicy.cs`: deterministic identifier, UTC and variant constraints.
- `src/HarborOps.Application/Tracking/TrackingEventValidator.cs`: strict profile and semantic validation.
- `src/HarborOps.Api/Integrations/TrackingEventInput.cs`: exact JSON extraction, prohibited keys and size/depth enforcement.
- `contracts/tracking/v1/` and `tests/fixtures/tracking/`: immutable synthetic fixtures with hashes and normative mapping record.

### Build order

1. Freeze the profile and JSON shape from accepted D-002. Do not implement unknown codes before a reviewed schema mapping exists.
2. Enforce `application/json` UTF-8, one event per request, maximum 16 KiB raw body including chunked streams, max nesting depth 12, duplicate-key and unknown-field denial, bounded strings and strict profile headers.
3. Apply pure ISO/UTC parsing, time-window policy, event-family-specific required references and D-003 equipment identifier validation. Do not infer tenant from JSON or query state in this phase.
4. Distinguish syntax and supported-domain errors using stable `400`, `413`, `415` and `422` envelopes. Keep correlation IDs, no-store and payload redaction consistent with existing API.
5. Test JSON member casing, Unicode controls, malformed UTF-8, duplicate keys, oversize streaming, unsupported classifier, unknown future variants, nested arrays, null and conditional reference rules, temporal edge cases and source spoofing keys.

**Definition of done:** valid approved fixtures normalize deterministically; every invalid or unsupported case is rejected without a downstream write or payload leak.

## 6. M7-04: API composition and gate verification

### Proposed file work

- `src/HarborOps.Api/Integrations/TrackingEndpoints.cs`: integration route, authentication and validation composition, response mapping and deliberate non-acknowledgment.
- `tests/HarborOps.UnitTests/TrackingEventPolicyTests.cs` and `IntegrationSourcePolicyTests.cs`.
- `tests/HarborOps.ContractTests/TrackingContractTests.cs`: authentic/forged token, Host, HTTPS, payload and response contracts.
- `tests/HarborOps.IntegrationTests/TrackingSourceSecurityTests.cs`, `TrackingSourceMigrationTests.cs`, `TrackingSourceConcurrencyTests.cs`.
- `scripts/verify-m7-local.sh`, `scripts/verify-m7-stages-local.sh`, fixture preparer and source-locked evidence checks, following approved M6 conventions without modifying accepted M6 runners.
- `docs/M7_VERIFICATION_PLAN.md`, `docs/M7_IMPLEMENTATION_STATUS.md`, `docs/TEST_STRATEGY.md` and `docs/STATUS.md` after execution.

### Runtime invariants

- No event success acknowledgment without an M8 durable receipt.
- Every failure response uses safe `code/message/correlationId`, no-store and configured rate/Retry-After headers.
- No source may select a different organization through payload, URL, header or human selected-org session.
- Existing browser routes continue to require cookie/CSRF policy; integration routes require bearer and never inherit browser auth.
- Database unavailable, revoked source, archived/suspended organization and unexpected JSON all fail closed.
- Negative tests demonstrate that no event inbox, accepted-event history, worker queue, projection, delivery or external network side effect exists in M7.

### Verification execution order

1. Preflight E: storage and exact source commit; refuse primary app volumes or unrelated Docker resources.
2. Clean .NET solution build with warnings as errors; unit and contract cases.
3. Disposable PostgreSQL fresh M0-M7 migration, then seeded accepted M6-to-M7 upgrade; verify all original M0-M6 records persist. Run rollback/reapply on a separate disposable copy if supported.
4. Direct SQL integrity, credential expiry/revocation, concurrent provisioning and tenant probes.
5. Full backend integration and accepted M0-M6 regressions affected by integration bootstrap, DI, migration and authentication; web build and existing three-browser regression if shared routing changes.
6. Compose smoke, source CLI, HTTPS/loopback/proxy, forged forwarded-header, database outage/recovery and teardown.
7. Current NuGet/npm and exact image HIGH/CRITICAL scans when images or dependencies change. Capture every status and skipped case explicitly.
8. Package raw sanitized evidence, SHA-256 inventory, test totals, source and image digests, local E: storage compliance and cleanup. Prepare for independent G7 review.

Run only locally. GitHub Actions remains disabled. A new manual-only workflow is unnecessary for this milestone and must not be created as part of planning.

## 7. Task and risk register

| Priority | Risk | Mitigation and proof |
| --- | --- | --- |
| Resolved | D-002 public artifact or subset not pinned | Pinned by SHA-256 as local snapshots and accepted 2026-11-03 (Option A); one artifact is published and three are withheld for want of a confirmed license |
| High | Event accepted before durable storage exists | Always return 503 before M8 and assert absence of event writes |
| High | Source credential cross-tenant use | Immutable organization-bound credential FK, server-derived scope, two-tenant adversarial tests |
| High | Revocation or rotation race | Lock and transaction discipline, direct DB validation, concurrent barrier cases |
| High | Header/proxy/Host spoof | Reuse verified peer trust and HTTPS policy, forged request cases |
| High | Body or JSON parser exhaustion | Bounded streamed raw bytes, depth, field counts, lengths and rate limits |
| Medium | DCSA upstream version drift | Immutable version and fixtures, separate HarborOps adapter, no conformance claim |
| Medium | Migration drift after accepted M6 | Additive source tables, snapshot diff, seeded upgrade and original-row hashes |
| Medium | Token exposure through logs/artifacts | Structured metadata only; secret-scan evidence before packaging |
| Medium | Future M8 disagreement about receipt/idempotency | Explicit handoff fields and a mandatory durable boundary before any 202/200 |
| Low | Future UI or accessibility demand expands M7 | No M7 browser feature; preserve M24-04 dedicated accessibility gate |

## 8. Planned handoff and acceptance

M7 source, schema migration, contract fixtures and tests are committed on the separate implementation branch and were executed in a source-locked local run recorded in the [executed verification](M7_EXECUTED_VERIFICATION.md). D-002 is accepted (2026-11-03, Option A). Explicit G7 acceptance is pending and is required before implementation merge.

The detailed [G7-S01 through G7-S25](M7_VERIFICATION_PLAN.md) specification is the evidence index authority. M8 remains blocked on G7, its accepted G6 prerequisite and its own D-006 decision; do not imply M8 readiness from M7 contract-only work.

Use feature branches and pull requests because `main` is protected. Do not run GitHub Actions, do not consume persistent C: storage, do not alter accepted G0-G6 evidence, and do not claim hosted application readiness.
