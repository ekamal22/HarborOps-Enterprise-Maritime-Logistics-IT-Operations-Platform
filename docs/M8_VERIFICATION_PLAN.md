# G8 Verification Plan: Durable Inbox and Immutable Accepted History

**State:** Executable source-level tests, fault hooks, isolated runner and stricter evidence reconciliation authored. None executed. D-006 acceptance pending; G8 acceptance pending. See [remediation register](M8_CORRECTNESS_REMEDIATION.md).
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

Run controlled security mutations that intentionally remove: (1) the scoped unique key, (2) final receipt-transaction reauthorization, (3) append-only SQL guard, (4) post-commit-only acknowledgment, (5) capacity enforcement, and (6) one of the required event/work inserts. Each mutant must fail the **intended** test; clean control must pass. Detect equivalent mutants and avoid treating an unrelated compiler failure as security efficacy.

## 4. Local execution order and resources

1. Preflight exact source SHA, clean worktree, E: storage paths, Docker/WSL location, environment versions, accepted baseline and D-006 decision. Refuse primary `harborops` project and unknown retained resources.
2. Cheap gates: planning/link/schema checks, pure canonical unit tests, .NET release build with warnings as errors, contract tests and EF migration consistency. Fail fast before Docker/browser-heavy stages.
3. Disposable PostgreSQL: full fresh M0-M8 migration/seed, populated M7->M8 upgrade, repeat integration suite, direct SQL immutability and FK attacks, concurrency, outage, restart and uncertainty windows.
4. Security mutation suite in separate isolated source exports and databases, with read-only control and mutation result manifests. Do not mutate the working tree or accepted historical scripts.
5. Build web and run relevant current browser/proxy tests in Chromium, Firefox and WebKit, including revised tracking ingress 202/409/4xx checks and accepted sessions/organizations/shipments/assignments flows.
6. Dependency and supply-chain checks from reachable NuGet/npm advisory sources and three immutable exact-image HIGH/CRITICAL scans. Missing data is a failure/blocker, not a clean result.
7. Controlled teardown removes only the named M8 test containers, volumes, images and network. Confirm unrelated and historical Docker resources unchanged. Package sanitized raw evidence with hashes and independent review.

Suggested future runner `scripts/verify-m8-local.sh` must use `HARBOROPS_VERIFICATION_ROOT=E:\\HarborOps-Verification\\m8` (or an E:-backed equivalent), an isolated `ho-verify-g8` Compose project and distinct ports/subnet from M7/default Compose. The runner and its stage layout are implementation deliverables, not existing files. Use cheap checks first. Do not run GitHub Actions or access the retained historical PostgreSQL volume outside its milestone checkpoints.

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
| BLOCKED | G8-S01 depends on D-006 acceptance; other implementation cases await code and runtime execution |
| NOT EXECUTED | G8-S02 through G8-S25 |
