# M18 Local Incident Policy Proposals

**Prepared:** 2026-11-18  
**Status:** PROPOSED FOR APPROVAL, NOT IMPLEMENTED, NOT TESTED, NOT ACCEPTED  
**Baseline:** protected `main` at `72251f5310f24fb98322b7eb403a5e9e66e040ef`  
**Scope:** M18-01 through M18-04 and G18; no changes to previously accepted gate decisions.

## Decision authority and sequencing

The [roadmap](ROADMAP.md) and [execution graph](EXECUTION_GRAPH.md) require G17 before M18-01, and G16 before M18-02. Both are integrated into `main`. G17 retains its separately signed S25-A03/F06 reviewer instrument obligation and P09 QUALIFIED PASS with pre-G24 remeasurement. These are not waived by M18 planning.

The following choices are proposed defaults, not already approved policies. **Do not implement policy-dependent behavior until every affected row is explicitly resolved and recorded in this document or a dedicated approved decision.** An implementation commit, mock, test expectation, or drafted table does not itself approve a choice. Reject incompatible choices explicitly rather than silently relying on defaults.

| ID | Decision | Recommended option A | Alternative or unresolved edge | Approval status |
| --- | --- | --- | --- | --- |
| M18-P01 | Incident scope | Organization-owned incidents only; no global incident browser | Separate platform incidents would require an explicit separate boundary | PENDING |
| M18-P02 | Role permissions | Support and Administrator operate technical incidents in selected active organization; Manager has summary and structured impact reporting only; Auditor reads only under a separate revocable, expiring, organization-scoped M18 grant; Analyst denied | Different Manager/Support scopes require typed permissions and revised tests | PENDING |
| M18-P03 | State machine | Seven durable states with a separately audited `reopen` action | Persisting an eighth `Reopened` state would change the model and graph contract | PENDING |
| M18-P04 | Severity | SEV-1 through SEV-4 with reviewed impact definitions; no SLA clock | Numeric response targets, escalation routing or on-call obligations need separate approval | PENDING |
| M18-P05 | Creation trigger | Explicit authenticated human action with server-issued, finite M17 service-evidence prefill; no automatic incident | Automated alert-based creation requires separate opt-in, durable deduplication and service authority | PENDING |
| M18-P06 | Source snapshot | Copy only a server-captured finite service-status/rule snapshot, with provenance/freshness and a safe digest; no claims of tenant impact from service-level telemetry | Full telemetry payload attachment or raw Grafana link is not approved | PENDING |
| M18-P07 | Persistence | Mutable current incident projection plus append-only transition, note and action history | Hard deletion or rewriting history rejected by default | PENDING |
| M18-P08 | Audit | Required M16 append-only audit event commits atomically with every successful sensitive incident mutation | Best-effort success without audit violates accepted M16-D03 | PENDING |
| M18-P09 | Notes/privacy | Choose bounded free text with explicit residual secret-paste risk and protection controls, or structured note categories; neither may flow to audit/logs/metrics | Files, screenshots, rich HTML and unrestricted log paste are out of scope | PENDING |
| M18-P10 | Concurrency/retry | `expectedVersion` for mutation; operation key unique per selected organization and actor across create and later commands, with kind and canonical payload hash | Last-write-wins, duplicate timeline append or ambiguous success prohibited | PENDING |
| M18-P11 | Assignment | Only active same-organization Support/Administrator may own technical incident; assignee revocation blocks further action | External assignee identities or automatic assignment need separate policy | PENDING |
| M18-P12 | Retention | Never hard-delete incidents or evidence in M18; future archival/retention requires approved policy | M16 365-day audit retention is not an incident data deletion rule | PENDING |
| M18-P13 | Failure semantics | Authorization fail closed, audit write failure rolls back mutation, transient storage outage sanitized 503; telemetry export alone fails open | Business mutation must never depend on monitoring exporter availability | PENDING |
| M18-P14 | UI/i18n | Functional organization-scoped English and Turkish views through existing localization infrastructure | No English-only hidden incident paths or duplicate unsynchronized navigation | PENDING |
| M18-P15 | Cross-milestone boundary | M17 read-only safe context; M15 dispatcher unchanged; M9 transport hold unchanged; no implicit paging or self-healing | Integration into notification outbox or worker is future gated scope | PENDING |
| M18-P16 | Verified closure | Resolve only after fresh recovery/integrity evidence and action disposition; close with a second authorized actor for high severity or an expressly approved, audited exception | Empty notes or a cosmetic green dashboard cannot close an incident | PENDING |
| M18-P17 | Emergency/false-positive paths | Preserve the seven-state path; exceptional expedited resolution requires explicit typed rationale and separately approved privilege rules | Permit direct `Triage -> Resolved` only if the exception flow receives approval | PENDING |
| M18-P18 | Input/cursor limits | Defaults: page 25, maximum 100, keyset cursors protected against tampering; title up to 160, note up to 4,096, service code up to 64 bytes; all tentative | Change values only with contract tests and recorded decision | PENDING |
| M18-P19 | Feature rollout | New incident UI and APIs disabled for retained deployments until G18 approval; disposable implementation test profile explicitly opts in | A hidden feature cannot bypass endpoint authorization; production remains unauthorized | PENDING |
| M18-P20 | Q3 compatibility | Run on a disposable representative historical backup if one exists; otherwise record justified NOT APPLICABLE | Never upgrade, read-write mount or delete the only retained copy | PENDING |

## Approval-critical contract details

P02 must resolve Manager safe-summary-only access and the exact Auditor M18 grant issuer, expiry, revocation and visibility scope. P09 must explicitly decide whether bounded free text is acceptable given that automatic secret detection is imperfect. P10 must permit idempotent incident creation **before an incident ID exists**. P16 must identify the independent closure confirmer, severity-specific separation requirements and an exception authority; a one-responder high-severity ticket cannot invent a second confirmer. P01 and P06 must keep organization-owned incidents separate from global service telemetry.

M16 audit capture is not inherited automatically by new M18 tables. M18 must add migration-owned, allowlisted audit trigger/routine coverage, transaction-local actor context and reviewed central role grants. The [security and transaction contract](M18_SECURITY_AND_TRANSACTION_CONTRACT.md) explains the proposed controls and their required [G18 assertions](M18_G18_ASSERTION_INVENTORY_20261118.json). Every choice above remains PENDING approval.

## Proposed transition policy

The existing [Operations](OPERATIONS.md) states are `New -> Triage -> Investigating -> Mitigated -> Monitoring -> Resolved -> Closed`, with reopen. Reopen is proposed as an action that creates an immutable event and enters `Investigating`, not an eighth durable state. All state names remain exactly as recorded. Only one transaction may advance an incident at any version.

| From | To | Preconditions (proposed) |
| --- | --- | --- |
| New | Triage | Authorized Support/Administrator; severity, impact, detection time, service and symptom captured |
| Triage | Investigating | Named active technical assignee and bounded triage record |
| Investigating | Mitigated | Mitigation summary, actual step or explicit no-action exception, observation reference |
| Mitigated | Monitoring | Recovery action documented; monitoring window start and target metric/probe captured |
| Monitoring | Investigating | Regression/worsening observed; reason and new evidence appended |
| Monitoring | Resolved | Recovery and integrity checks pass; root cause classified, corrective action register complete or justified exception |
| Resolved | Closed | Authorized independent confirmation, final record and closure verification evidence; any exception explicitly permitted |
| Resolved or Closed | Investigating | Explicit reopen command with reason, observed regression, actor, UTC timestamp and new revision; previous close facts immutable |
| Investigating or Mitigated | Investigating | Self-loop only for substantive new investigation or failed mitigation, never used as cosmetic version churn |

Forbid all other state transitions, including implicit skips, `Closed -> Closed`, closure without recovery verification and changing status by editing a generic incident record. If P17 is approved for exceptional false positives, specify one exact transition, evidence, two distinct actors where applicable and audit taxonomy before writing runtime code. A terminal `Closed` row remains discoverable to authorized readers and never disappears from history.

## Proposed severity and impact contract

| Severity | Grounded example | Required response |
| --- | --- | --- |
| SEV-1 | Sustained complete platform outage or confirmed cross-organization exposure | Record immediate containment, scope, escalation owner and decision; no fabricated SLA |
| SEV-2 | Major tracking backlog or broad material reporting corruption | Prioritize diagnosis, recovery, and integrity proof |
| SEV-3 | Isolated service degradation with a functioning workaround | Scoped investigation and tracked corrective action |
| SEV-4 | Low-impact usability or diagnostic defect | Normal documented queue |

Severity is a classification of **verified impact**, not a function of a single alert value. `unknown` impact must be represented explicitly; do not silently classify data absence as healthy. A severity change is an auditable event with previous/new values, evidence and reason, never a direct unaudited field overwrite. All server times are UTC.

## Proposed permission contract

Every role requires an active user, valid server-backed session, active organization membership, matching session-selected organization and explicit typed permission. Selection never grants rights.

| Operation | Analyst | Manager | Support | Administrator | Auditor |
| --- | --- | --- | --- | --- | --- |
| List safe incident summaries | Deny | Allow within organization | Allow | Allow | Explicit read-only grant |
| Read technical notes, diagnostic snapshots and actions | Deny | Deny by default | Allow within organization | Allow within organization | Explicit read-only grant for sanitized records |
| Create manual incident from organization impact | Deny | Allow limited fields | Allow | Allow | Deny |
| Append impact observation | Deny | Allow limited, own-scoped note | Allow | Allow | Deny |
| Set severity, assign, transition, reopen | Deny | Deny | Allow | Allow | Deny |
| Verify recovery and close | Deny | Deny | Allow with separation controls | Allow with separation controls | Deny |
| Read M16 audit entries | Existing M16 policy only | Existing M16 policy only | Existing M16 policy only | Existing M16 policy only | Existing M16 Auditor policy only |

This table requires new typed `OrganizationPermission` values and transaction-time scope checks. Do not infer technical read access from `ViewShipmentData`, `ViewAuditEvents`, `ViewOperationalKpis` or Administrator organization-management powers. An Auditor's explicit incident grant must be separately designed and revocable; if not implemented in M18, the grant-dependent paths remain denied and the corresponding gate assertion is BLOCKED until approved disposition. A global Administrator bypass is never allowed.

## Evidence, confidentiality and audit

Persist only the incident's normalized service identifier, observed time, category, severity, bounded impact, permitted source kind and a safe correlation digest/reference. Do not replicate raw M17 trace bodies or arbitrary labels. Sanitize server-side and on output. Represent missing/outdated telemetry as `unknown`, not verified healthy. A sealed safe snapshot remains intelligible after M17's short telemetry retention expires.

The M16 audit entry is a separate immutable security record. For each accepted transition/assignment/severity/closure mutation, atomically commit the current state, immutable incident event and allowlisted M16 audit entry. Audit metadata must exclude free-form notes and sensitive evidence. Audit insertion failure must roll back the incident transaction and return sanitized non-success. Denial audit is best effort only for already denied requests and never converts denial into grant. No application runtime role can UPDATE/DELETE/TRUNCATE history or audit directly. Privileged migrator scope is not application authorization.

## Explicit exclusions

No auto-open incident, automatic resolution, escalations to third-party paging, email/SMS/Slack, change-management approvals, asset inventory, customer data export, file attachments, external ITSM sync, on-call SLA commitments, telemetry collector reconfiguration, new tracking worker activation, bulk incident purge or production deployment. M19 retains full backup/restore responsibility, M20 full security review, M23 final UI polish and M24-04 final dedicated accessibility acceptance.

## Approval register

| Decision IDs | Status | Decision date | Evidence |
| --- | --- | --- | --- |
| M18-P01 through M18-P20 | PENDING | Not recorded | Explicit policy selection required before implementing affected work |

Record each approved value, changes, rationale and review evidence **without** editing the original proposal out of existence. Policy approval authorizes only design/implementation within this milestone. It is not a G18 PASS, merge, release or production authorization.
