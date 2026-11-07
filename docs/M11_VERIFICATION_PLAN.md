# M11 / G11 Source-Locked Verification Specification

**Current source audit (2026-11-07):** [M11 correctness audit](M11_SECOND_CORRECTNESS_AUDIT_20261107.md) records corrected defects and unresolved verification, local-policy and security boundaries. Runtime execution is not verified and G11 acceptance pending. The accepted M9 transport hold is unchanged.

**Implementation checkpoint (2026-11-07):** A provisional source implementation exists on `feat/m11-delay-exception-lifecycle`, including additive migration 14, rule evaluation, scoped HTTP routes, guarded Manager actions, a shipment UI, and executable tests. The code has not been compiled or run against PostgreSQL in this review. M11 local policy choices remain proposed; G11 scenarios are NOT EXECUTED and acceptance pending. No merge, deployment, or transport-hold removal is authorized.

**State:** Verification plan only, with a separate provisional implementation candidate. G11-S01 remains BLOCKED on local policy approval and executable scenarios remain NOT EXECUTED. G11 acceptance pending.
**Scope:** M11 delay rules and shipment exceptions; FR-010, FR-009, FR-002, NFR-002, NFR-007. Base `6ead1d076946dd96b53401265a31efc35c4a666c`.
**References:** [architecture and proposed policies](M11_ARCHITECTURE_DESIGN.md), [delivery plan](M11_IMPLEMENTATION_PLAN.md), [G10 closure](M10_G10_GATE_CLOSURE_20261107.md), [M9 hold](M9_G9_GATE_CLOSURE.md).

## Second-pass change-impact scenarios, NOT EXECUTED

The [second source audit](M11_SECOND_CORRECTNESS_AUDIT_20261107.md) requires explicit test evidence for no-op checkpoint advancement without duplicate history, expired due-boundary action rejection, M10 projection refresh after equivalent input versions, SQLSTATE 40001/40P01 exhaustion, restricted replay SELECT, API/database clock skew, timestamp maximum and microsecond grace boundaries, nonempty chunked evaluation bodies, and frontend out-of-order detail reads. These extend G11-S02/S03/S10/S12/S17 through S22/S24. A committed test does not count as a passing scenario.

## Gate criteria and evidence discipline

No pass may be claimed from a test file or dry run. S01 records explicit policy acceptance independently of executable results. S25 is independent raw-evidence review and G11 requires a separate explicit decision. The operating hold on sustained ARRI/DEPA transport processing, the 10,000 unresolved-work cap and null M10 ETA remain mandatory. Evidence should prove the exact new revision rather than assume historical G10 verification covers edited M11 source.

| ID | Mandatory scenario | Minimum success evidence |
| --- | --- | --- |
| G11-S01 | M11 local policy decision | Explicit disposition of M11-P01 to P12, exact version and any revised choices; otherwise BLOCKED |
| G11-S02 | Clean warning-as-error compile and contract validation | Exact .NET build, frontend typecheck and schema/API contract results on frozen SHA |
| G11-S03 | Deterministic rule truth table | Equal/just-before/just-after UTC boundaries, fixed TimeProvider, repeated same-input same-output |
| G11-S04 | Arrival schedule overdue | Active booked/in-transit shipment with planned arrival past grace is labeled schedule overdue, not physically delayed |
| G11-S05 | Recorded arrival late | Explicit M5 Arrived history beyond planned deadline produces recorded-workflow late evidence with correct transition ID/time |
| G11-S06 | Departure slippage at risk | Late recorded InTransit transition + future arrival produces risk only under approved source confidence and thresholds |
| G11-S07 | Unknown/insufficient/stale input | Missing planned times, unknown tracking, stale M10 projection and absent manual evidence never invent ETA/arrival/conflict |
| G11-S08 | M10 equipment conflict | Same-container/time conflicting accepted GTIN/GTOT yields deterministic scoped conflict, bounded provenance |
| G11-S09 | Canonical event order and correction | Late, duplicate, out-of-order and superseded M10 records yield no duplicated exception and correct reevaluation |
| G11-S10 | Rebuild/correction version drift | Stale input fingerprint prevents misleading current assertions; new generation/rebuild causes exactly one required update |
| G11-S11 | Clean and populated upgrade | New additive migration 14 on fresh DB and cloned accepted-M10/G16 data, 1-13 unchanged and source rows preserved |
| G11-S12 | Cross-tenant SQL isolation | Direct restricted-role SQL, foreign UUIDs, cursor swaps and cross-org joins fail without existence disclosure |
| G11-S13 | Five-role HTTP matrix | Manager actions; Analyst/Manager/Auditor scoped reads; Administrator/Support denied; no implicit admin bypass |
| G11-S14 | Live role, session and org revocation | Revocation/logout/suspension after initial access blocks disposition at transaction boundary |
| G11-S15 | Acknowledge lifecycle | Active Manager, required reason and expected version, exactly one immutable state change and M16 audit record |
| G11-S16 | Resolve and recurrence | Clear-only resolve policy enforced; active-condition resolve denied; reappearance creates new occurrence with preserved history |
| G11-S17 | Idempotent and conflicting replay | Same actor/key/body replay byte-equivalent without extra events; changed body/key reuse conflicts; wrong actor denied |
| G11-S18 | Concurrency and cancellation | Two simultaneous evaluations or acknowledgments, source drift and cancel/rearchive races yield one consistent state |
| G11-S19 | Audit atomicity and failure | Audit DB rejection or failure rolls back acknowledgment and history; successful replay creates no duplicate central audit |
| G11-S20 | Scheduled UTC boundary and restart | Due scanner detects time-only change, missed timer/restart recovery and bounded work without full-table hot-loop |
| G11-S21 | Outage and interruption | DB/API/evaluator outage and crash-before/after-commit preserve committed state, prevent partial commit and recover |
| G11-S22 | Real local stack and browser | Docker Compose HTTP, shipment exception panel, accurate labels, keyboard interactions and authorized action in Chromium/Firefox/WebKit |
| G11-S23 | Regressions and M9 hold | Accepted M5/M8/M9/M10/M16 changed surfaces pass; ARRI/DEPA still parked, cap honored, M10 ETA null |
| G11-S24 | Security mutants, scans and evidence safety | Real restricted-role attacks, effective targeted mutants killed, zero unresolved HIGH/CRITICAL image/dependency findings and no leaked secrets |
| G11-S25 | Independent raw-evidence review | Separate reviewer validates SHA, test records, failed attempts, manifest hashes, exact dispositions and residual limitations |

## Required fixture families

1. Two active organizations A/B with overlapping human-readable references, three scoped readers, a Manager, a wrong-role Administrator/Support, a disabled member and an expired session.
2. Active and archived/cancelled M5 shipments with null/present planned times; Booked, InTransit, Arrived, Completed transitions; versions and simultaneous updates.
3. One accepted equipment GTIN, one GTOT, contradictory same-container/same-occurred-time observations, cross-container Mixed state, late arrivals, duplicates, an explicit M10 correction and rebuilt projection.
4. An M10 projection row that is missing/stale due to input revision, policy version or correction generation. The disabled-by-default projector setting must be preserved and tested.
5. Synthetic UTC transitions on either side of the approved grace/tolerance boundaries, including exact equality, midnight UTC, leap date and DST transitions in local display only.
6. Real restricted PostgreSQL accounts, session-bound authorization proof if required, one audit write failure, one injected read dependency outage, concurrent Manager and evaluator transactions, and a missing/malformed signing key.
7. A populated accepted-M10/G16 snapshot cloned to an isolated database before migration 14. Preserve row hashes/record counts for critical accepted tables.
8. A parked DEPA and ARRI example in isolated M9 verification. They must not become processed, decrease the unresolved count through an illicit transition or feed the delay rules.

## Verification tiers and execution sequence

**Tier 1, before heavyweight runs:** schema and policy spec consistency; `node scripts/render-execution-graph.mjs --check`; `node scripts/check-planning-docs.mjs`; .NET compilation/unit/contract, frontend typecheck, migration source inspection, strict HTTP input parsing and test fixture sanity. Correct source defects first.

**Tier 2, change-impact:** fresh and populated real PostgreSQL migration, authenticated HTTP integration with real restricted roles, time transitions, correction/rebuild, concurrent CAS/replay, audit abort, outages and restart. Use isolated disposable E:-backed PostgreSQL and one mutable suite at a time.

**Tier 3, final gate:** freeze SHA, run required G11 scenario suite, three-engine browser specs, dependency/SQL role security tests, effective mutation control and mutants, image/dependency scans, evidence manifest and independent S25 review. Run noncompeting tests concurrently only if resource-isolated and preserving captured outputs. Do not repeat G10's full historical campaign unless changed risk or coverage requires it.

## Mutation and negative controls

At minimum include deliberate defects that the tests must catch: treating stale projection as fresh; inclusive instead of strict grace boundary; misusing event received time instead of occurred time; ignoring M10 corrections; treating GTIN/GTOT as arrival; allowing unknown input as clear; skipping tenant predicate; session/Manager validation bypass; unauthorized API SQL update; missing expected-version guard; missing idempotency collision check; duplicate active exception race; suppressing M16 audit on acknowledgment; commit of state after audit failure; incorrectly resolving active condition; mutating accepted M9 transport work.

A source mutation is not effective solely because a test exits nonzero: retain mutant identity, compilation, reason for failure and evidence proving the intended assertion killed it.

## Evidence manifest and final decisions

Store all evidence on an isolated E:-backed run directory and record:

- Full source SHA and clean/dirty status, parent commit, branch, migration hashes, config fingerprint without secrets and exact commands.
- Tool versions; PostgreSQL major, Docker/WSL storage verification, ports/Compose project, clock/fixtures, per-stage start/end measured durations, return codes and counts.
- Raw TRX/JUnit, selected SQL result captures without secrets, anonymized HTTP captures, browser reports by engine, scans, mutations and recovery outcomes.
- SHA-256 manifest with checksums and expected relative paths, plus preserved failed/interrupted attempts and superseding evidence.
- Matrix status for each G11-S01..S25: PASS, FAIL, BLOCKED or NOT EXECUTED. No result may be silently reclassified.
- Independent S25 report detailing residuals and explicit, dated G11 decision separately from test PASS.

**Exit rule:** G11 may be recommended for acceptance only when all mandatory scenarios are satisfied, current policy choices are approved, the evidence is independently reconciled, no unapproved security weakening exists, and explicit acceptance is still requested. No change in G11 status changes the M9 hold or authorizes merging, hosting or release.
