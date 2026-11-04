# G8 Verification Plan: Durable Inbox and Immutable Accepted History

**State:** M8 source, the full G8 test/runner candidate, two audit-remediation rounds and source/run-locked review scripts are committed but have **not** been executed. D-006 and G8 acceptance pending. See [initial remediation](M8_CORRECTNESS_REMEDIATION.md), [second remediation](M8_SECOND_REMEDIATION_STATUS.md) and [verification runbook](M8_G8_VERIFICATION_RUNBOOK.md).
**Prepared:** 2026-11-03
**Accepted baseline:** M0-G0 through M7-G7 on `main` at `47f030f9fa4b588d589f3ae082d03b3d5b94736a`.
**Scope:** M8-01 to M8-04, no M9 worker execution or M10 projections.
**Contracts:** [M8 architecture](M8_ARCHITECTURE_DESIGN.md), [D-006 proposal](M8_D006_DURABILITY_PROPOSAL.md), [implementation plan](M8_IMPLEMENTATION_PLAN.md), [accepted M7/G7](M7_G7_GATE_CLOSURE.md).

## 1. Non-negotiable gate rules

1. D-006 must be explicitly decided before its dependent schema and HTTP contract is treated as frozen. A recommended option is not acceptance.
2. Every new 202 must be tied to a completed PostgreSQL transaction containing an immutable receipt, immutable accepted event and Pending work item; no uncommitted receipt may be acknowledged.
3. The original bearer must be revalidated within the receipt transaction after parsing, under locks compatible with M7 source lifecycle updates. An earlier authenticated source ID is not sufficient.
4. PostgreSQL uniqueness, composite organization/source/credential FKs and append-only constraints must independently block adversarial direct SQL; source-only unit tests do not satisfy G8.
5. At least one fully executed, source-locked E:-backed technical run is required, with real database concurrency and failure injection, not test-method presence or documentation claims.
6. Preserve M7's negative response and 16 KiB profile behavior, accepted G0-G7 regressions, and the no-2xx rule on all failure and ambiguous-commit paths. The new valid-event 202 is not business processing success.
7. Declare each case PASS, FAIL, BLOCKED or NOT EXECUTED based on raw proof. Missing tools, advisory data, browsers, independent review or acceptance cannot be reclassified as PASS.
8. Run tests and scans locally only on E: with isolated resources, no Actions, no persistent project-specific C: writes and no access to primary retained Docker/PostgreSQL volumes.
9. Full dedicated accessibility testing remains exclusively M24-04. Internet-facing application hosting is outside G8.
10. G8 technical verification does not close G8. Explicit acceptance is separately required before implementation merges into protected `main`.

## 2. Mandatory G8-S01 through G8-S25 matrix

| Scenario | Required executed proof |
| --- | --- |
| **G8-S01** | D-006 explicitly accepted, chosen schema/response/retention/worker handoff frozen and checked against no M7 contract expansion |
| **G8-S02** | Pure canonicalization: fixed field order, UTF-8 and UTC normalization, optional fields and digest stable for semantically identical requests |
| **G8-S03** | Fresh M0-M8 schema, composite FKs, exact unique scoped keys, checks, EF snapshot parity, immutable triggers and deferred receipt completeness |
| **G8-S04** | Populated accepted M7->M8 upgrade and disposable rollback/reapply preserve exact M0-M7 records and constraints |
| **G8-S05** | New event commits exactly one receipt, one immutable accepted event and one Pending work item with source/tenant provenance |
| **G8-S06** | 202 returned only after successful COMMIT, with original receipt identity, UTC received timestamp, no-store and correct correlation |
| **G8-S07** | Identical raw replay and 2x/10x sequential duplicates yield one event/work row and original receipt with duplicate=true |
| **G8-S08** | JSON reorder, whitespace and equivalent UTC offset spellings normalize to same canonical event; differing semantic values do not |
| **G8-S09** | Same scoped key with different event returns 409 source_event_collision; first conflict metadata bounded, original event immutable |
| **G8-S10** | 20 simultaneous same-key submissions, both identical and conflicting; unique index arbitrates one accepted event without lost writes |
| **G8-S11** | Two independent organizations and multiple sources with identical external keys remain isolated; no foreign receipt disclosure |
| **G8-S12** | Post-parse source revoke/disable/org suspension under actual lock waits denies or linearizes receipt before revocation commit |
| **G8-S13** | Credential expiry/not-before, rotation overlap and digest mismatch rechecked inside write transaction, not just initial M7 preauth |
| **G8-S14** | All M7 negative auth, transport, body, media, schema, origin, rate-limit and proxy trust paths remain fail-closed and write-free |
| **G8-S15** | Syntax-valid unknown equipment/transport reference is durable Pending but not linked to a foreign shipment or projected state |
| **G8-S16** | Pending admission cap at 9999/10000/10001, multi-source concurrent cap, capacity 503/Retry-After, duplicate resolution at cap |
| **G8-S17** | DB outage before transaction, after insert and before commit, and at COMMIT ambiguity never give false 202; same-key retry recovers |
| **G8-S18** | Cancel/timeout paths roll back when possible; uncertainty never acknowledged; no unbounded wait or leaked connection/transaction |
| **G8-S19** | API/PostgreSQL restart preserves receipt UUID, raw accepted bytes, canonical digest, work Pending state and duplicate response |
| **G8-S20** | Direct SQL UPDATE/DELETE, orphan receipt, forged org/source/credential, double event/work row and cross-tenant FKs rejected |
| **G8-S21** | No raw payload/bearer leaks in response, structured logs, evidence, browser assets or errors; safe 409/503 correlation and no-store |
| **G8-S22** | Accepted G0-G7 unit/contract/PostgreSQL and relevant three-engine browser/proxy regressions remain green |
| **G8-S23** | Mutation-resistant quota/immutability/index/reauth/ack gate under source-locked disposable database with expected failing tests |
| **G8-S24** | Exact source/image IDs, current reachable advisory inputs, HIGH/CRITICAL image scans, transport/security smoke and controlled recovery |
| **G8-S25** | E:-backed isolated run with all stage exits, scenario matrix, raw sanitized evidence, hashes, teardown and independent review |

## 3. Adversarial fixture and test design

Use two isolated organizations, at least two distinct integration sources per organization, active/disabled sources, rotated/revoked/expired credentials, matching and conflicting `sourceEventId` keys, approved Equipment GTIN/GTOT and Transport ARRI/DEPA payloads, and synthetic valid identifiers. Add invalid/unsupported D-002 variants, 0/16384/16385 byte requests, transport spoofing and a missing-reference event. No real customer or personal data.

Deterministically orchestrate distinct PostgreSQL connections with barriers at: after M7 preauthentication, after receipt-transaction org lock, before final credential validation, after receipt insert, after event/work inserts, immediately before COMMIT, after COMMIT sent and before response. Confirm the database outcome independently after each injected failure. A mock-only concurrency suite is not gate evidence.

Run the **11 committed, source-isolated security mutation variants** from `scripts/run-g8-mutations.mjs`: digest verification, collision-as-replay, missing work, early acknowledgment, append-only guard, capacity enforcement, missing accepted event, unique key, credential tenant FK, final revocation and source-status checks. Require an unmodified passing control and an intended failing test for each effective mutant. Detect equivalent mutants and never count a compiler failure or unrelated test as a kill.

## 4. Local execution order and resources

1. Preflight the exact feature commit, clean worktree, E:-backed paths and Docker/WSL data location; inventory unrelated Docker resources and any attributable temporary C: writes. The proposed D-006 contract remains acceptance pending, so technical testing may proceed but formal G8 closure cannot.
2. Cheap failure gates before long work: script syntax, generated execution graph, all Markdown links and status consistency, Node tracking-contract tests, .NET Release build, current advisory checks and web tests/build. Stop at a real blocker, repair it and rerun the affected cheap gates.
3. Disposable PostgreSQL: execute full fresh and repeated integration suites, populated accepted M7 upgrade/empty rollback, direct SQL attacks, live lock waits, real before/after COMMIT socket disruption, restart and complete byte-level Pending-state proof.
4. Run the isolated Compose stack, account/source fixtures, three Playwright engines, outage/recovery probes, supply-chain advisories and three exact-source image scans. Preserve all missing/failed tests, not only a subset.
5. Run the longest complete 11-mutation suite **after** the other less-expensive mandatory checks pass, with a passing control and expected regression kill for each mutant. Never substitute an unverified mutant list for execution.
6. Teardown only identified G8-owned resources, scan all final logs and outputs, produce immutable per-run archive and manifest, and run independent package safety/reconciliation. Recheck C: temporary-path attribution and delete only safe task-owned temporary files.
7. Report technical PASS/FAIL and formal acceptance separately, retain unmodified raw evidence and obtain explicit D-006 and G8 decisions later.

The existing runner is `scripts/verify-m8-local.sh`, with `HARBOROPS_VERIFICATION_ROOT` set to a **fresh** E:-backed directory ending in `/m8`, for example `E:\\HarborOps-Verification\\g8-attempt-01\\m8`. Every attempt, including the same-SHA retry, needs a new root. The isolated Compose project is `ho-verify-g8`, with dedicated ports and subnet. See the [runbook](M8_G8_VERIFICATION_RUNBOOK.md) for exact evidence handling. Do not run GitHub Actions or access retained historical PostgreSQL volumes outside their approved milestone checkpoints.

## 5. Required immutable evidence package

- Tested SHA, tree/dirty status, dependency and scanner versions, image digests, UTC run window and all stage exit codes.
- Raw TRX test results, executable source-mutation results, deterministic fixtures and 25-scenario results indexed to specific tests/queries.
- Fresh/upgrade/rollback schema and row-hash checks, SQL trigger/constraint attacks, concurrent-session wait evidence, cancellation, commit ambiguity and restart persistence results.
- Sanitized HTTP response samples proving 202 only after commit, correct 409 and 503 behavior, no token or raw payload emission, and per-engine browser reports.
- E:/C: placement checks, safe teardown and untouched unrelated resources, SHA-256 manifest for every file, final evidence archive digest and independent archive review.
- A register of all FAIL, BLOCKED, NOT EXECUTED and accepted limitations, with no retrospective alteration of the original run results.

## 6. Gate closure

No critical/high open issue, unaccepted D-006, false success acknowledgment, incomplete credential reauthorization, cross-tenant exposure, data loss, missing advisory evidence or unreviewed mandatory scenario is eligible for G8 closure. Once G8-S01 to G8-S25 are fully executed and independently reviewed, the result remains **acceptance pending** until explicitly accepted. Only then may verified M8 implementation be merged into protected `main`; M9 design and implementation remain independently gated.

### Planned scenario register

| Result | At planning time |
| --- | --- |
| PASS | 0 (no G8 run executed) |
| FAIL | 0 (no G8 run executed) |
| BLOCKED | G8-S01 depends on D-006 acceptance and G8-S25 needs independent evidence review |
| NOT EXECUTED | G8-S02 through G8-S24; G8-S25 also has no completed source-locked run |
