# M18 Planning Status and Authorization Register

**Recorded:** 2026-11-18  
**Scope:** M18 IT incident lifecycle and remediation, gate G18.  
**Status:** P01-P20 APPROVED; M18 SOURCE CANDIDATE EXISTS ON SEPARATE FEATURE BRANCH, NOT VERIFIED. G18 NOT EXECUTED OR ACCEPTED.  
**Base source:** protected `main` at `72251f5310f24fb98322b7eb403a5e9e66e040ef` (recheck immediately before work).  
**Planning branch:** `plan/m18-incident-lifecycle-20261118`.

## Authoritative disposition

The [canonical execution graph](execution-graph.json) already contains M18-01 through M18-04 and G18, with dependency G17 for M18-01 and G16 for M18-02, and the required Q3 historical snapshot checkpoint. **No graph node or prerequisite is changed by this planning packet.** G16 and G17 are integrated. G17 retains independently signed S25-A03/F06 reviewer-documentation debt and P09 QUALIFIED PASS, and does not authorize production deployment. The separate in-progress G17 current-state documentation reconciliation branch is not part of this M18 plan and must not be merged accidentally.

M18 must produce a durable scoped ticket lifecycle, typed actor/permission control, investigation/mitigation/root cause/remediation record, recovery-confirmed close/reopen history, and a controlled M17 observation to explicitly created incident journey. Automatic ticket creation, external paging, retained data changes and M9 transport activation are excluded.

## Planning packet

| Artifact | Content | Disposition |
| --- | --- | --- |
| [M18 local policy proposals](M18_POLICY_PROPOSAL_20261118.md) | P01-P20 decisions, roles, state/reopen/severity, privacy, audit, retention, limits, opt-in | Approved 2026-11-18; technical gate remains open |
| [M18 architecture](M18_ARCHITECTURE_DESIGN.md) | Domain/database/API/UI contract and trust boundaries | Proposed |
| [M18 implementation plan](M18_IMPLEMENTATION_PLAN.md) | M18-01..04 ordered work, file ownership, guards, stop criteria and impact regressions | Planned |
| [G18 verification plan](M18_VERIFICATION_PLAN.md) | 25-case, 75-assertion scope, source-locked staging and independent S25 | Planned, not executed |
| [G18 assertion inventory](M18_G18_ASSERTION_INVENTORY_20261118.json) | Machine-readable 25 x 3 atomic IDs and planned witnesses | 0 implemented, 0 executed |
| [M18 disposable runbook](M18_OPERATIONS_RUNBOOK.md) | Owner-isolated E: setup, fault journey, Q3 compatibility, artifact and cleanup contract | Draft, not run |

## Refinement review, 2026-11-18

The proposed [transaction and security](M18_SECURITY_AND_TRANSACTION_CONTRACT.md), [decision-to-evidence crosswalk](M18_DECISION_TO_EVIDENCE_CROSSWALK.md), [API and UI examples](M18_API_AND_UI_CONTRACT_EXAMPLES.md) refine new M16 audit capture, incident creation idempotency, cross-tenant telemetry safety, role-specific read grants, closure separation, privacy and per-assertion negative controls. The assertion inventory remains 25 scenarios/75 unique NOT_EXECUTED assertions. P01-P20 were subsequently approved on 2026-11-18. The current feature candidate and its unverified boundaries are recorded in [M18 implementation checkpoint](M18_IMPLEMENTATION_CHECKPOINT_20261118.md).

## Readiness ledger

| Milestone step | State | Next release condition |
| --- | --- | --- |
| M18-01: Define severity/state policy | APPROVED 2026-11-18 | Domain source and role contracts remain subject to technical checks |
| M18-02: Implement incident CRUD/state | CANDIDATE, UNVERIFIED | Compile, fresh/upgrade migration, authorization, audit and concurrency evidence |
| M18-03: Investigation and remediation | CANDIDATE, UNVERIFIED | Local source-locked functional, privacy and browser verification |
| M18-04: Alert-to-incident evidence journey | NOT VERIFIED | Real fault-to-human-confirmed incident journey and restoration evidence pending |
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

P01-P20 were approved on 2026-11-18 and an isolated M18 implementation candidate exists. This historical planning packet does not certify its behavior. [Current candidate status](M18_IMPLEMENTATION_CHECKPOINT_20261118.md) records open verification blockers. No GitHub Actions run, migration application, retained application activation, gate acceptance or merge to `main` is authorized by this checkpoint.

The packet is intentionally exhaustive in proposed requirements but does not claim tests, approvals or a release date that do not yet exist. Preserve M16/M17 historical failures and accepted safeguards without reinterpretation.
