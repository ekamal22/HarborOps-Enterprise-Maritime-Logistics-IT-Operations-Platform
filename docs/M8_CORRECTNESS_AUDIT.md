# M8 Correctness Audit

**Audit date:** 2026-11-04  
**Audited source:** `690360de234d856911f60d142332c48b75aae50e`  
**Implementation:** `feat/m8-durable-event-inbox`, draft [PR #14](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/14)  
**Planning baseline:** `plan/m8-durable-inbox-immutable-history`, draft PR #13  
**Accepted baseline:** protected `main` at `47f030f9fa4b588d589f3ae082d03b3d5b94736a`, M0/G0 through M7/G7 accepted  
**Disposition:** **Correctness blockers found. M8 is not G8-ready. D-006 acceptance pending.**  
**Evidence classification:** Repository source inspection only. No executable .NET, PostgreSQL, Compose, Playwright, mutation or G8 raw evidence is available from this repository connection. A source-asserted invariant is not an observed test pass.

## Audit method

Reviewed the new receipt canonicalizer, tracking API endpoint, source bearer lifecycle and reauthorization, relational migration and EF/snapshot mappings, integration/unit/SQL tests, browser source, G8 Compose file, technical runner, mutation harness, evidence summarizer and governing D-006 and G8 requirements. Compared the implementation branch with its planning parent and cross-checked required scenarios with their named test methods. Historical G7 evidence was not modified or reinterpreted.

## Findings

### M8-A01 | High | Destructive migration rollback can erase accepted history

- **Location:** [tracking migration](../src/HarborOps.Infrastructure/Persistence/Migrations/20261103190000_TrackingInboxAndHistory.cs#L178-L190), [D-006 retention](M8_D006_DURABILITY_PROPOSAL.md).
- **Observed:** `Down()` unconditionally drops `tracking_receipt_conflicts`, `tracking_inbox_work_items`, `tracking_accepted_events` and `tracking_inbox_receipts`. The append-only row triggers do not prevent table drops. A normal migration downgrade can destroy committed receipt identity and immutable history, even though D-006 prohibits ordinary retention deletion and requires permanent replay keys.
- **Required correction:** Make migration downgrade fail closed when any accepted receipt exists, document an explicit safe downgrade policy, and test a populated M8 schema against an attempted downgrade. Empty, isolated disposable-schema rollback may remain supported.
- **Acceptance proof:** Populated receipt/event/work records remain byte-identical and downgrade is rejected without loss; empty test-only downgrade/reapply succeeds.

### M8-A02 | High | G8 browser origin configuration conflicts with existing security checks

- **Location:** [G8 browser runner](../scripts/run-m8-browsers-local.sh#L16-L20), [G8 Compose](../compose.g8.verify.yaml#L70-L78), [shipment-draft browser spec](../tests/browser/specs/shipment-draft.spec.mjs#L20-L30), [same-origin enforcement](../src/HarborOps.Api/Authentication/AuthenticationSetup.cs).
- **Observed:** The runner uses `http://127.0.0.1:5178`, while configured API CORS allows only `http://localhost:5178`; those are distinct origins. More decisively, the existing shipment draft spec still sends `Origin: http://localhost:5173` and creates browser contexts for that old address. The server's exact `IsSameOrigin` check rejects that origin for requests hosted on port 5178. Other tests and fixture scripts were partially, not universally, parameterized.
- **Required correction:** Choose one G8 browser origin and use it consistently in the runner, Playwright configuration, all browser specs and API origin/CORS configuration; preserve existing default-stack behavior. Search the full browser suite for hardcoded 5173/8080 origins and prove authenticated mutation operations in all three engines.
- **Acceptance proof:** Complete Chromium, Firefox and WebKit G8 runs with non-skipped existing tests and no false origin/CSRF failures.

### M8-A03 | High | Mandatory uncertain-COMMIT verification is absent

- **Location:** [G8 summarizer](../scripts/summarize-g8-evidence.mjs), [G8-S17](M8_VERIFICATION_PLAN.md), [HTTP receipt handling](../src/HarborOps.Api/Integrations/TrackingEndpoints.cs#L89-L131).
- **Observed:** G8-S17 requires `CommitUncertaintyNeverProducesSuccessAndReplayResolvesOutcome`, but no corresponding test exists on the audited branch. The current tests cover ordinary success, pre-cancellation and source-host restart, not failures after writes but before commit or an ambiguous commit acknowledgment. The summary necessarily marks S17 BLOCKED.
- **Required correction:** Add deterministic PostgreSQL fault injection around pre-commit, commit-before-client-ack and ambiguous-commit windows. Verify no unconfirmed 2xx, atomic rollback where possible, and stable same-key replay after an uncertain commit. Do not infer success from a test name or a source string search.
- **Acceptance proof:** Raw two-outcome commit experiments with exact source and database state, including dedicated repeat runs.

### M8-A04 | High | Several G8 cases can receive PASS from materially weaker tests

- **Location:** [evidence mapping](../scripts/summarize-g8-evidence.mjs), [scenario requirements](M8_VERIFICATION_PLAN.md), [M8 HTTP tests](../tests/HarborOps.IntegrationTests/TrackingInboxHttpTests.cs).
- **Observed:** The summarizer's `tested(method)` only checks whether named methods ran and passed; it does not establish the specified adversarial conditions. Material mismatches include:
  - **S08:** Requires JSON property reorder and whitespace equivalence, but the named canonical test only compares equivalent UTC spellings.
  - **S10:** Requires 20 simultaneous competing deliveries; the named conflicting-delivery test creates 16.
  - **S12:** Requires overlapping lock-wait races for revoke, disable and organization suspension; the named test revokes before the receipt begins, with no lock-wait interleaving.
  - **S13:** Requires expiry, not-before and rotation-overlap final checks; named evidence only proves wrong digest and foreign source.
  - **S16:** Requires 9,999/10,000/10,001 pending-cap boundaries and concurrent cross-source admission; the named test configures a limit of one and performs sequential requests.
  - **S18:** Requires mid-transaction cancellation and timeout failure windows; the named test cancels before opening the transaction.
  - **S20/S21:** The named SQL/security tests do not cover all forged scoped keys, double event/work insertions, structured logs and packaged evidence leakage demanded by the matrix.
- **Required correction:** Implement each specified condition as an independent executable assertion, then tighten summary evidence predicates so a narrower proxy test cannot mark the stronger case PASS. Use actual concurrency/lock barriers and SQL session observations, not sleeps alone.
- **Acceptance proof:** One-to-one G8-S02 through G8-S24 case-to-raw-evidence mapping, including real duration, connection, transaction and test identifiers.

### M8-A05 | Medium | EF uniqueness metadata does not match SQL constraint kind

- **Location:** [EF mappings](../src/HarborOps.Infrastructure/Persistence/HarborOpsDbContext.cs#L131-L154), [migration](../src/HarborOps.Infrastructure/Persistence/Migrations/20261103190000_TrackingInboxAndHistory.cs#L30-L54) and [snapshot](../src/HarborOps.Infrastructure/Persistence/Migrations/HarborOpsDbContextModelSnapshot.cs).
- **Observed:** EF declares `UQ_m8_receipt_key`, `UQ_m8_event_receipt` and `UQ_m8_conflict_receipt` with `HasIndex(...).IsUnique()`, whereas the SQL migration creates named `UNIQUE` constraints. Both enforce uniqueness in PostgreSQL, but EF's conceptual index metadata and actual constraint catalog are not equivalent for future migrations and introspection. The model-vs-snapshot test cannot detect that divergence because those two files agree with one another.
- **Required correction:** Align the migration and EF model on unique indexes versus alternate-key/unique constraints, without disturbing the explicit composite foreign-key alternate keys. Inspect `pg_constraint`, `pg_indexes` and design-time relational metadata after real migration.
- **Acceptance proof:** Model, migration, live PostgreSQL catalog and any next additive migration agree without duplicated or missing uniqueness objects.

### M8-A06 | Medium | Database failure before final receipt store lacks retry guidance

- **Location:** [initial authentication](../src/HarborOps.Api/Integrations/TrackingEndpoints.cs#L34-L49), [receipt error handling](../src/HarborOps.Api/Integrations/TrackingEndpoints.cs#L118-L130), [global dependency handler](../src/HarborOps.Api/Program.cs#L108-L124).
- **Observed:** Receipt-store database errors set `Retry-After: 60`, but the initial `IntegrationSourceStore.AuthenticateAsync` runs before that local try/catch. Provider errors can reach the global 503 `dependency_unavailable` handler, which does not set a Retry-After header. The documented M8 database-failure response requires retry guidance throughout the pre-acknowledgment path.
- **Required correction:** Add consistent safe retry guidance for source-authentication dependency failures without exposing credentials or changing M7's intentional negative authentication behavior.
- **Acceptance proof:** Real database-outage probes before authentication, before insert and around commit assert status, safe code, correlation, no-store and retry guidance.

### M8-A07 | Medium | Mutation suite does not cover all required invariants

- **Location:** [six G8 mutation variants](../scripts/run-g8-mutations.mjs#L27-L35), [mutation application](../scripts/apply-g8-mutation.mjs), [G8-S23](M8_VERIFICATION_PLAN.md).
- **Observed:** Six authored variants address final digest, collision-as-replay, missing work, premature success, receipt append-only and quota bypass. The G8 plan additionally calls for unique-key bypass, absent accepted-event write and source-status authorization failures. The summary presently treats six killed variants as sufficient for S23 regardless of these missing threats.
- **Required correction:** Add meaningful unique-key/index, missing accepted-event, source-revocation/status and tenant-FK mutants. Require failing behavioral assertions on the intended defect, not compilation failure. Retain clean control and repeat source-locked evidence.
- **Acceptance proof:** Full mutation register with every contract-critical mutation killed by a specifically named test.

### M8-A08 | Medium | No integrated decision/review completion path for blocked G8 gates

- **Location:** [scenario summary](../scripts/summarize-g8-evidence.mjs), [runner exit policy](../scripts/verify-m8-local.sh#L301-L314), [G8-S01/S25](M8_VERIFICATION_PLAN.md).
- **Observed:** S01 and S25 have unconditional acceptance/review blockers in source, and the runner exits 2 if any blocker remains. That correctly prevents premature acceptance but provides no source-locked supplement/reconciliation mechanism for a later explicit D-006 decision and independent evidence review.
- **Required correction:** Provide a separate reviewed, hash-bound gate reconciliation path that references the original raw run and supplements without changing original findings. Never auto-approve S01/S25.
- **Acceptance proof:** Decision and independent review references are checked against the exact runner SHA, scenario archive and manifest, with final gate acceptance still explicit.

### M8-A09 | Low | Invalid pending-limit configuration fails per request

- **Location:** [pending-limit configuration](../src/HarborOps.Infrastructure/Integrations/TrackingInboxStore.cs#L31-L45).
- **Observed:** A malformed `M8:PendingLimit` value throws `InvalidOperationException` only when a valid new receipt reaches the capacity check, producing a generic 500 instead of failing deployment preflight. This is fail-closed but less operable and makes configuration errors look like runtime outages.
- **Required correction:** Validate the configured 1..10,000 admission range during startup and retain guarded runtime checks.
- **Acceptance proof:** Invalid startup configuration rejects initialization before serving traffic, while valid defaults and test overrides behave as documented.

## Invariants supported by source inspection, not yet execution

- The endpoint continues M7 transport, bearer, profile, 16 KiB, origin and negative input checks before entering M8.
- The transaction explicitly locks organization state and revalidates the bearer digest, source capability/status, revocation and credential validity before applying receipt writes.
- Tenant/source/profile/external-event identity has a unique schema constraint. Canonical replay returns original receipt identity and timestamp; conflicting same-key payloads attempt one bounded first collision record.
- Receipt, event and initial Pending work insertions occur under one EF/PostgreSQL transaction; new-receipt 202 is constructed only after `CommitAsync` returns.
- Three immutable history tables have append-only row guards and a deferred trigger requires an event and work item for new receipts.
- The feature branch does not add M9 worker processing, M10 projections, a public receipt query or deployed hosting.
- These are implementation observations. They do **not** establish migration success, error-path correctness, SQL trigger behavior or race safety.

## Required next actions

1. Resolve **M8-A01 through M8-A04** before claiming an executable G8-ready implementation. Fix and retest the browser origin, destructive downgrade, commit-uncertainty coverage and under-specified evidence checks.
2. Correct **M8-A05 through M8-A09** and add a live schema-catalog comparison and robust source-locked mutation matrix.
3. Confirm D-006 Option A explicitly or revise the provisional schema and response contract. Acceptance pending until confirmed.
4. Execute a clean .NET 10 build; full fresh and repeated G0-G8 PostgreSQL/contract/unit suites; actual transaction interleavings; three browser engines; outage/restart tests; advisory and image scans on isolated E:-backed G8 resources. Preserve complete logs, TRX, SQL observations, evidence hashes and teardown proof.
5. Review the raw artifact independently, reconcile G8-S01 through G8-S25 using immutable source hashes and evidence references, and request explicit G8 acceptance. No GitHub Actions and no merge to protected `main` before closure.

**Audit conclusion:** The M8 feature branch is a substantial implementation candidate with a coherent transaction boundary, but the identified high-severity safety and verification defects prevent a correctness sign-off. Keep PR #14 in draft, D-006 acceptance pending and G8 open. No executable verification or release acceptance is claimed in this record.
