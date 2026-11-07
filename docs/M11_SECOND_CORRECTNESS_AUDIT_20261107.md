# M11 Second-Pass Correctness Audit

**Review date:** 2026-11-07
**Isolated branch:** `feat/m11-delay-exception-lifecycle`
**Accepted baseline:** protected `main` `6ead1d076946dd96b53401265a31efc35c4a666c`
**Starting review revision:** `1aa3529e951a6fc12ec72158265923b5cb6f0e21`
**Source remediation checkpoint before this report:** `c84f605e0cddd5978a4990c86899755c8e143320`
**Prior source audit:** [M11 correctness audit](M11_CORRECTNESS_AUDIT_20261107.md)
**Disposition:** Confirmed source defects corrected on draft PR #31. Runtime correctness is not verified and G11 acceptance pending.

This is a fresh source review of the M11 implementation and its accepted M5, M9, M10 and M16 integration boundaries. It does not replace historical raw evidence or the independent G11-S25 reviewer.

## Second-pass source findings and dispositions

| ID | Severity | Confirmed source issue | Remediation | Evidence status |
| --- | --- | --- | --- | --- |
| F11-014 | High | An unchanged rule fingerprint returned before updating the evaluation checkpoint. Any later M10 projection refresh could leave the due scanner repeatedly scheduling the same input. | The signed evaluator now advances the source version, next-due and evaluated-UTC checkpoint on a semantic no-op without appending duplicate history; it also updates displayed last-evaluated time. | Source corrected; runtime and sustained-load tests NOT EXECUTED |
| F11-015 | High | Manager acknowledgment or resolution checked source revisions but not an elapsed rule deadline. A stored inactive or active state could be acted on after its due transition. | The signed action function now rejects missing, mismatched or elapsed rule-evaluation checkpoints with a retryable conflict that requires fresh evaluation. | Source corrected; PostgreSQL race tests NOT EXECUTED |
| F11-016 | High | An M10 projection could be rebuilt at the same input/correction revision while the current equipment exception still referenced the older projection snapshot. | Equipment dispositions now require current accepted M10 policy, matching counters and a projection refresh timestamp not newer than the exception evaluation checkpoint. | Source corrected; actual correction/rebuild campaign NOT EXECUTED |
| F11-017 | Medium | General API SELECT provisioning exposed M11 idempotency-replay records even though runtime code requires no direct read. | Removed direct API SELECT permission for the replay table while retaining signed SQL function access. | Source corrected; restricted database login test NOT EXECUTED |
| F11-018 | Medium | The final serializable/deadlock failure from the evaluator escaped into a generic HTTP 500 after bounded retries. | Exhausted SQLSTATE 40001/40P01 now reports a conflict without implying a committed evaluation. | Source corrected; injected contention tests NOT EXECUTED |
| F11-019 | Medium | Proof expiry was signed using the web host UTC clock while verification used PostgreSQL clock. Clock skew could reject an otherwise valid operation. | Evaluation and disposition proofs now derive their 45-second expiry from a PostgreSQL clock read; the signatures retain distinct M11 message domains. | Source corrected; cross-clock runtime test NOT EXECUTED |
| F11-020 | Medium | Adding the one-hour grace/tolerance to an accepted M5 timestamp close to year 9999 could overflow the pure rules engine. | Clamped UTC rule arithmetic to `DateTimeOffset.MaxValue` and omitted an impossible next-due time. | Source corrected; deterministic unit source added, NOT EXECUTED |
| F11-021 | Medium | The scheduled strict boundary was advanced by a full millisecond even though PostgreSQL can represent a microsecond. | Changed the strictly-after scheduler time to the first representable microsecond, preserving equality semantics. | Source corrected; deterministic unit source added, NOT EXECUTED |
| F11-022 | Medium | Manager reevaluation accepted a nonempty HTTP body when the request used chunked transfer without Content-Length. | Bounded one-byte body inspection rejects nonempty evaluation payloads independent of declared content length. | Source corrected; transport-level test still needed |
| F11-023 | Low | Concurrent UI detail requests could display whichever response arrived last, not the exception most recently selected. | Scoped detail fetches to a monotonically increasing request identity, invalidated by refresh and unmount. | Source corrected; browser test NOT EXECUTED |
| F11-024 | Medium | A source-contract assertion incorrectly double-escaped its multiline regular expression, weakening the intended no-op checkpoint check. | Corrected the expression and added assertions for source version, deadline, M10 refresh, database proof clock and bounded rule arithmetic. | Source corrected; source-contract script itself NOT EXECUTED |

## Added source verification

- [Policy unit tests](../tests/HarborOps.UnitTests/ShipmentExceptionPolicyTests.cs) now include max-range UTC safety and first representable post-boundary time.
- [Proof unit tests](../tests/HarborOps.UnitTests/M11ProofSignerTests.cs) use a supplied synthetic database clock, assert a 45-second expiry and verify action/session digest binding.
- [PostgreSQL/HTTP integration source](../tests/HarborOps.IntegrationTests/M11ExceptionRuntimeTests.cs) checks that an unchanged result advances its evaluation checkpoint without duplicating history; that an expired rule deadline prevents Manager resolution until reevaluation; and that restricted SQL roles cannot read replay records.
- [Source contract assertions](../scripts/check-m11-contract.mjs) are expanded. Source assertions cannot prove the SQL migration actually applies, privileges are effective, the compiler accepts the branch or the HTTP fixture behaves as expected.

## Remaining blockers and non-claims

1. **All executable G11-S02 through G11-S24 remain NOT EXECUTED.** No successful .NET build, Npgsql integration, migration, HTTP, Playwright, PostgreSQL concurrency/restart, effective mutation, dependency or container-security scan has been produced in this review.
2. **G11-S01 is BLOCKED:** M11-P01 through M11-P12 are proposed choices, including provisional 60-minute thresholds, acknowledgment/clearance, role-specific redaction and missing-evidence semantics. Source implementation is not policy approval.
3. **G11-S25 NOT EXECUTED:** independent reviewer must reproduce raw-source provenance, check the exact frozen SHA and match executed evidence to each gate scenario before acceptance is considered.
4. **Conflict-specific provenance is incomplete:** the aggregate M10 equipment-conflict projection and digest are available, but complete identification of both conflict-causing source events and all affected containers is not guaranteed by a bounded M10 projection sample.
5. **Inherited API SQL-role SELECT scope remains broad** for accepted shipment data and M11 exception/history reads, even though proof and replay tables are restricted and HTTP endpoints enforce tenant and session checks. Assess whether a dedicated signed read boundary or row-level policies are required before approving G11-S12.
6. **History display retains only the first 500 events with an explicit truncation flag.** Historical rows remain immutable and stored; server-side history pagination or export is not yet implemented.
7. **Operational stability is not demonstrated:** the optional evaluator is disabled by default; its bounded keyset passes, repeated checkpoints, multiple replicas, projected-input churn, nonce expiry, time-only triggers and outage/restart behavior still need measured tests.
8. **Local runner limitation:** [focused E:-backed verification](../scripts/verify-g11-local.sh) collects development evidence only. It is not the complete source-locked G11 test and independent review campaign.

## Mandatory verification order

1. Freeze the implementation branch and accepted `main` baseline. Record source SHA, branch, clean tree, runtime tool versions and the disposable E:-backed PostgreSQL environment.
2. Execute a clean .NET 10 warning-as-error build and policy/unit tests, static source contract, frontend typecheck, graph generator `--check` and planning-doc assertions.
3. Apply migration 14 to new and populated accepted M10/G16 database copies. Run restricted-role SQL, missing key, cross-tenant, session-revocation, unique replay, origin/CSRF, cancellation, serializable contention and audit-failure controls.
4. Run G11 rule fixtures including GTIN/GTOT conflicts, late and superseded corrections, stale materialization, empty schedule, terminal manual workflow, UTC microsecond boundaries, year-9999 edges and projection refresh without changed fingerprint.
5. Run the bounded evaluator under sustained due and no-op inputs, a real process interruption and restart, plus a no-ETA and parked M9 ARRI/DEPA regression.
6. Execute Chromium, Firefox and WebKit shipment exception scenarios, relevant M5/M9/M10/M16 regressions, targeted mutation control and security/dependency scans. Preserve all failed attempts.
7. Hash and independently reconcile the raw output for all G11-S01 through S25. Record any accepted local policy changes and request a separate explicit gate decision.

## Preserved boundaries

- Protected `main` remains at `6ead1d076946dd96b53401265a31efc35c4a666c`. PR #31 remains draft and unmerged.
- No GitHub Actions were triggered or enabled, and no merge, deploy, release, production enablement or destructive cleanup was performed.
- M9's sustained transport-event processing hold, parked ARRI/DEPA work and 10,000 unresolved-work cap remain unchanged. M10 ETA stays null.
- Earlier gate closures and raw verification records have not been rewritten.

**Final assessment:** The second source audit has corrected the identified source defects. It cannot certify executable correctness or G11 readiness. **G11 acceptance pending.**
