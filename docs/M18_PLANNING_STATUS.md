# M18 Planning Status and Authorization Register

**Recorded:** 2026-11-18  
**Scope:** M18 IT incident lifecycle and remediation, gate G18.  
**Status:** END-TO-END PLANNING PACKET PREPARED. M18 NOT IMPLEMENTED. G18 NOT EXECUTED OR ACCEPTED.  
**Base source:** protected `main` at `72251f5310f24fb98322b7eb403a5e9e66e040ef` (recheck immediately before work).  
**Planning branch:** `plan/m18-incident-lifecycle-20261118`.

## Authoritative disposition

The [canonical execution graph](execution-graph.json) already contains M18-01 through M18-04 and G18, with dependency G17 for M18-01 and G16 for M18-02, and the required Q3 historical snapshot checkpoint. **No graph node or prerequisite is changed by this planning packet.** G16 and G17 are integrated. G17 retains independently signed S25-A03/F06 reviewer-documentation debt and P09 QUALIFIED PASS, and does not authorize production deployment. The separate in-progress G17 current-state documentation reconciliation branch is not part of this M18 plan and must not be merged accidentally.

M18 must produce a durable scoped ticket lifecycle, typed actor/permission control, investigation/mitigation/root cause/remediation record, recovery-confirmed close/reopen history, and a controlled M17 observation to explicitly created incident journey. Automatic ticket creation, external paging, retained data changes and M9 transport activation are excluded.

## Planning packet

| Artifact | Content | Disposition |
| --- | --- | --- |
| [M18 local policy proposals](M18_POLICY_PROPOSAL_20261118.md) | P01-P20 decisions, roles, state/reopen/severity, privacy, audit, retention, limits, opt-in | Proposed, all choices pending |
| [M18 architecture](M18_ARCHITECTURE_DESIGN.md) | Domain/database/API/UI contract and trust boundaries | Proposed |
| [M18 implementation plan](M18_IMPLEMENTATION_PLAN.md) | M18-01..04 ordered work, file ownership, guards, stop criteria and impact regressions | Planned |
| [G18 verification plan](M18_VERIFICATION_PLAN.md) | 25-case, 75-assertion scope, source-locked staging and independent S25 | Planned, not executed |
| [G18 assertion inventory](M18_G18_ASSERTION_INVENTORY_20261118.json) | Machine-readable 25 x 3 atomic IDs and planned witnesses | 0 implemented, 0 executed |
| [M18 disposable runbook](M18_OPERATIONS_RUNBOOK.md) | Owner-isolated E: setup, fault journey, Q3 compatibility, artifact and cleanup contract | Draft, not run |

## Readiness ledger

| Milestone step | State | Next release condition |
| --- | --- | --- |
| M18-01: Define severity/state policy | PLANNED | Explicit P01-P20 selections, revised role and transition matrix, source contract signed |
| M18-02: Implement incident CRUD/state | NOT STARTED | Approved M18-01, accepted G16, additive schema and safe source change |
| M18-03: Investigation and remediation | NOT STARTED | Approved M18-01, agreed M18-02 interfaces; isolated implementation permissible in parallel |
| M18-04: Alert-to-incident evidence journey | NOT STARTED | M18-02 and M18-03 integrated into one exact source |
| G18: Independent 25-case review | NOT EXECUTED | 72/72 technical witnesses, 3 separately reviewed S25 assertions, explicit gate decision |
| Q3 historical snapshot checkpoint | NOT EXECUTED | Safe copy or evidence-backed NOT_APPLICABLE for G18-S24-A01 |
| G18 merge / production rollout | NOT AUTHORIZED | Separate acceptance/merge decisions; production remains blocked by future G24 |

## Unresolved decisions and risk register

| ID | Risk/decision | Current control |
| --- | --- | --- |
| R18-01 | M18 role matrix could widen access or imply cross-org Administrator authority | Typed permissions, full five-role/two-org adversarial gate |
| R18-02 | Open telemetry endpoint or untrusted alert URL could leak sensitive data | Manual safe snapshot, digest, no direct exporter access |
| R18-03 | State and audit could commit inconsistently | Same-transaction M16 audit coupling with injected failure proof |
| R18-04 | Concurrent or replayed transitions could duplicate/overwrite events | Expected-version, durable operation key, immutable history |
| R18-05 | Closure based on stale/missing green signal could conceal unresolved failure | Required health and integrity evidence, explicit unknown state |
| R18-06 | Rich notes could leak in logs, audit or browser tests | Bounded sanitized text and output canary controls |
| R18-07 | Added incident tables/roles could break accepted DB migrations or grants | Additive migration, clean/upgrade/least-privilege verification |
| R18-08 | New UI could be English-only or show stale tenant data | Shared Turkish dictionary and three-browser org-switch tests |
| R18-09 | Unsafe snapshot testing could alter retained historical records | E:-backed owned copy only, NOT_APPLICABLE evidence rule |
| R18-10 | Source-locked campaign could omit S25 independence or failed evidence | Manifest, negative mutation matrix, separate signed S25 recommendation |
| R18-11 | Unresolved G17 procedural/performance caveats could be presented as closed | Explicit carry-forward of S25-A03/F06, P09 and no-release rule |
| R18-12 | Unrelated open branches could overwrite shared context | Recheck main, owned files, branch merge ancestry and conflict review |

## Planned next authorization

The first authorized *planning* step is to review and explicitly decide P01-P20. Only after recording choices may M18-01 freeze an implementation-ready contract. Each later work package requires its graph prerequisites and its own source-lock/test evidence. No GitHub Actions run, runtime implementation, migration, application activation, gate acceptance or merge to `main` is authorized by this packet alone.

The packet is intentionally exhaustive in proposed requirements but does not claim tests, approvals or a release date that do not yet exist. Preserve M16/M17 historical failures and accepted safeguards without reinterpretation.
