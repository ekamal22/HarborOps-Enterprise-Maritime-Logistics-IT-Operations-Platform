# M18 Decision, Threat and G18 Evidence Traceability

**Prepared:** 2026-11-18  
**Status:** Proposed planning crosswalk. Every policy and witness below remains pending, not accepted or executed.  
**Source:** [25 x 3 machine-readable inventory](M18_G18_ASSERTION_INVENTORY_20261118.json), [architecture](M18_ARCHITECTURE_DESIGN.md), [security contract](M18_SECURITY_AND_TRANSACTION_CONTRACT.md).

## 1. What must be approved, and where that choice is tested

| Decision | Approval question and blocking edge | Primary related G18 cases |
| --- | --- | --- |
| M18-P01: Incident scope | Explicitly decide tenant-only incidents and how systemic faults are represented without cross-tenant leakage | G18-S01, G18-S02, G18-S03 |
| M18-P02: Roles and grants | Approve exact five-role truth table, Manager safe summary, Auditor grant issuer/scope/expiry/revocation | G18-S01, G18-S02, G18-S03, G18-S04, G18-S23 |
| M18-P03: States | Fix seven state names, exactly allowed transitions and reopen target | G18-S01, G18-S06, G18-S07, G18-S18 |
| M18-P04: Severity | Fix SEV meaning, unknown impact and reassessment authority | G18-S01, G18-S05, G18-S13 |
| M18-P05: Incident creation | Manual confirmation versus any additional automatic source; keep automation out unless separately approved | G18-S01, G18-S15, G18-S19, G18-S21 |
| M18-P06: M17 evidence | Finite safe service-state catalog, freshness, origin and no other-tenant impact inference | G18-S01, G18-S15, G18-S19, G18-S20 |
| M18-P07: Persistence | Immutable event/note/action snapshots, append-only guards and no cascade deletion | G18-S01, G18-S03, G18-S10, G18-S16 |
| M18-P08: M16 audit | Exact event taxonomy, migration-owned trigger/routine, transaction actor context and central grants | G18-S01, G18-S11, G18-S12 |
| M18-P09: Notes and privacy | Accept bounded free text with residual secret-paste risk or choose structured-only notes | G18-S01, G18-S14 |
| M18-P10: Concurrency and replay | Org+actor operation key for create and transitions, canonical payload hash, version CAS and revocation-safe replay | G18-S01, G18-S04, G18-S08, G18-S09, G18-S22 |
| M18-P11: Assignment | Live same-organization assignee eligibility and permissions, reassignment and revocation | G18-S01, G18-S08, G18-S13 |
| M18-P12: Retention | No hard delete, bounds and relationship to separate M16 and M17 retention | G18-S01, G18-S10, G18-S14, G18-S24 |
| M18-P13: Failure behavior | Authorization/audit fail closed, exporter fail open, ambiguous commit resolution | G18-S01, G18-S04, G18-S08, G18-S09, G18-S11, G18-S12, G18-S20 |
| M18-P14: Bilingual UI | Shared English/Turkish dictionary, permission-aware route and post-org-switch cache invalidation | G18-S01, G18-S02, G18-S23 |
| M18-P15: Neighbor boundaries | M9 held; no M15 dispatcher or M17 exporter enablement; no ticket automation | G18-S01, G18-S20 |
| M18-P16: Verified recovery | Fresh health/integrity proof; distinct closure authorizer or scoped exception | G18-S01, G18-S06, G18-S16, G18-S17, G18-S21 |
| M18-P17: Expedited and false positive | Permit or reject expedited transitions with precise authority and evidence | G18-S01, G18-S07, G18-S17, G18-S18 |
| M18-P18: Field and cursor bounds | Choose exact max sizes, TTL, keyset order and protected filter/cursor scope | G18-S01, G18-S05, G18-S22 |
| M18-P19: Activation | Explicit disposable feature enablement and deploy default-off; no production authorization | G18-S01 |
| M18-P20: Q3 compatibility | Representative historical copy vs evidence-backed NOT_APPLICABLE; never mutate retained original | G18-S01, G18-S24 |


The machine inventory also links every case to its work package, required witness layers and a specific effective negative-control family. These mappings are proposed and can be revised through an approved M18-01 contract change before implementation. They do not imply test code exists. **All P01-P20 are currently PENDING.**

## 2. Approval order and stop conditions

**Group A, mandatory before incident-domain implementation:** P01 tenant boundary; P02 role/grants; P03 transitions; P07 append-only policy; P08 M16 audit coupling; P10 idempotency/concurrency; P13 failure semantics; P16 closure separation; P18 field/HTTP bounds. Any unresolved Group A choice prevents M18-02 from beginning.

**Group B, mandatory before investigation/browser source:** P04 severity; P06 M17 safe snapshot; P09 free-text privacy; P11 assignee eligibility; P12 incident retention; P14 bilingual UI; P17 false positive/exception process. Any unresolved Group B choice prevents the affected M18-03 behavior and associated acceptance witnesses.

**Group C, mandatory before the integrated fault journey:** P05 manual versus automatic creation; P15 neighboring services; P19 disposable activation; P20 Q3 snapshot. M18-04 cannot start a source-locked final campaign without all 20 choices resolved and the previous approved deliverables integrated.

A choice of a different option must update the state diagram, role matrix, API schemas, SQL constraints, browser copy, scenario assertions and negative control at the **same source version**. No unilateral editing of a test to match a defect is acceptable.

## 3. Full case-to-witness/control table

| Scenario | Work package | Minimum witness layers | Effective negative control | Policy dependencies |
| --- | --- | --- | --- | --- |
| G18-S01 | M18-01 | source, policy, contract | policies-inconsistent-or-unapproved | M18-P01, M18-P02, M18-P03, M18-P04, M18-P05, M18-P06, M18-P07, M18-P08, M18-P09, M18-P10, M18-P11, M18-P12, M18-P13, M18-P14, M18-P15, M18-P16, M18-P17, M18-P18, M18-P19, M18-P20 |
| G18-S02 | M18-02 | unit, http, postgres | unauthorized-role-grant | M18-P01, M18-P02, M18-P14 |
| G18-S03 | M18-02 | postgres, http | missing-org-predicate | M18-P01, M18-P02, M18-P07 |
| G18-S04 | M18-02 | http, postgres, race | revocation-after-initial-check | M18-P02, M18-P10, M18-P13 |
| G18-S05 | M18-02 | unit, http | accept-invalid-severity | M18-P04, M18-P18 |
| G18-S06 | M18-02 | unit, postgres, http | skipped-state-guard | M18-P03, M18-P16 |
| G18-S07 | M18-02 | unit, http | generic-state-overwrite | M18-P03, M18-P17 |
| G18-S08 | M18-02 | postgres, race | ignore-expected-version | M18-P10, M18-P11, M18-P13 |
| G18-S09 | M18-02 | postgres, http, race | duplicate-replay-event | M18-P10, M18-P13 |
| G18-S10 | M18-02 | postgres, grant | disable-immutability-trigger | M18-P07, M18-P12 |
| G18-S11 | M18-02 | postgres, audit, fault | skip-required-audit | M18-P08, M18-P13 |
| G18-S12 | M18-02 | postgres, audit, http | denial-audit-grants-access | M18-P08, M18-P13 |
| G18-S13 | M18-02 | postgres, http | assign-foreign-user | M18-P04, M18-P11 |
| G18-S14 | M18-03 | browser, http, privacy | copy-note-into-audit | M18-P09, M18-P12 |
| G18-S15 | M18-03 | http, privacy, telemetry | accept-raw-telemetry-url | M18-P05, M18-P06 |
| G18-S16 | M18-03 | postgres, http, browser | erase-action-history | M18-P07, M18-P16 |
| G18-S17 | M18-03 | http, postgres, fault | close-without-health-proof | M18-P16, M18-P17 |
| G18-S18 | M18-03 | unit, postgres, http | erase-previous-closure | M18-P03, M18-P17 |
| G18-S19 | M18-04 | telemetry, http, browser | auto-create-on-alert | M18-P05, M18-P06 |
| G18-S20 | M18-04 | fault, telemetry, postgres | missing-signal-is-healthy | M18-P06, M18-P13, M18-P15 |
| G18-S21 | M18-04 | fault, http, postgres | false-recovery-proof | M18-P05, M18-P16 |
| G18-S22 | M18-02 | http, contract, security | accept-tampered-cursor | M18-P10, M18-P18 |
| G18-S23 | M18-03 | browser, i18n, http | retain-previous-org-data | M18-P02, M18-P14 |
| G18-S24 | M18-04 | migration, snapshot, regression | mutate-retained-snapshot | M18-P12, M18-P20 |
| G18-S25 | G18 | independent-review, manifest, governance | fabricate-independent-signoff | M18-P01, M18-P02, M18-P03, M18-P04, M18-P05, M18-P06, M18-P07, M18-P08, M18-P09, M18-P10, M18-P11, M18-P12, M18-P13, M18-P14, M18-P15, M18-P16, M18-P17, M18-P18, M18-P19, M18-P20 |

A negative control must actually change the relevant program behavior, execute the mapped test, and demonstrate that the test detects the failure. Report compile-failed, equivalent, unreachable and survived mutants separately from killed mutants. A mock-only demonstration cannot replace a mandatory PostgreSQL, HTTP or browser witness.

## 4. Stable fixture and test-concurrency contract

The frozen test fixture must include Alpha and Beta organizations with all five roles; an active and revoked user per relevant permission; two active Support responders for independent closure, plus a one-responder high-severity exception negative; both active and expired Auditor incident grants if approved; a Manager who can create a safe report but cannot see technical notes; a revoked Administrator; two concurrent sessions for one actor; and a synthetic incident in each organization sharing an otherwise similar title/ID-search input.

Include known health, degraded, absent, held and stale M17 signals, safe and malicious source-rule codes, an unavailable database, audit insert failure, mismatched operation payloads and repeated create keys. All values must be synthetic and deterministic. Pin fixture script and hash alongside implementation source and preflight run. Do not mix an earlier fixture hash with later source.

Concurrency requires actual overlapping operations against PostgreSQL, not sequential optimistic-lock tests with a forced stale number. When appropriate, coordinate session barriers, acquire actual locks, and prove at most one commit plus exact event/audit count. An authorization race should exercise a role change **between** the first HTTP check and the lock/commit check. No fixture, snapshot or test-only grant may be reused from retained production data.

## 5. Evidence coverage must be checked in both directions

A planning checker must ensure each P01-P20 is present in the policy register and referenced by at least one relevant technical scenario, all 25 scenario IDs are distinct and numerically complete, every scenario has exactly three ordered assertions, and exactly the three G18-S25 assertions are reviewer-only. Every technical assertion must have a source-qualified executable witness before it may become PASS. Every identified critical threat must have a mapped negative-control result. Every expected artifact must exist, be hash-verified, belong to the frozen source and be accounted for in the final manifest. Any missing mapping is a BLOCKED plan or NOT_EXECUTED assertion, not a green percentage.

The Q3 option is intentionally narrow: only G18-S24-A01 may be NOT_APPLICABLE if absence of a representative snapshot is evidenced. S24-A02 and A03 remain required. All other required assertions must be executed or the gate cannot pass.

Formal S25-A01, A02 and A03 require a genuinely separate reviewer judgment and signed source/evidence-bound instrument. The candidate implementation may produce an unsigned review packet, but it cannot self-issue the acceptance recommendation. An independently recommended PASS still requires a distinct explicit G18 gate acceptance decision and a source-equivalent merge authorization.

## 6. Release and cross-milestone constraints

M18 does not close G17 S25-A03/F06's distinct signed instrument obligation or P09's qualified overhead budget. M18 does not approve public hosting, lift the M9 backlog hold, enable the M15 dispatcher or default-on M17 monitoring. M19 restore, M20 adversarial release security, M23 visual polish and M24-04 accessibility acceptance remain their own work. Operational recovery may occur during a real fault, but tests run only in owned disposable isolated resources.

This document is a planning adjunct. Canonical graph edges in [execution-graph.json](execution-graph.json) remain unchanged.
