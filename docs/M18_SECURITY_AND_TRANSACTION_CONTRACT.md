# M18 Security, Persistence and Transaction Contract Refinement

**Prepared:** 2026-11-18  
**Disposition:** Proposed implementation contract, NOT APPROVED and NOT IMPLEMENTED.  
**Policy dependencies:** [M18-P01 through P20](M18_POLICY_PROPOSAL_20261118.md).  
**Source basis:** protected `main` `72251f5310f24fb98322b7eb403a5e9e66e040ef`; revalidate before implementing.

This refinement resolves design ambiguity, not policy authority. Every option here is a recommendation requiring the corresponding policy approval before use. The accepted [M16 audit decisions](M16_POLICY_DECISIONS.md), [M17 boundary](M17_ARCHITECTURE_DESIGN.md), and [M2 typed authorization](../src/HarborOps.Domain/Organizations/OrganizationPolicy.cs) are upstream constraints.

## 1. Critical existing-source observations

1. `OrganizationPolicy.cs` currently defines five roles and no M18 incident permissions. `OrganizationStore.ResolveAsync` consults current selected-organization scope. New incident access must not be inferred from shipment, audit, KPI or organization-management permissions.
2. M16's audit records live in `audit_entries` and the accepted implementation uses migration-owned capture triggers, restrictive database grants and transaction-local server-established actor context (`AuditActorContext.SetHumanAsync`). New M18 tables have **no automatic M16 trigger coverage** merely because they exist.
3. `AuditRoleProvisioner` is the centralized existing authority for runtime PostgreSQL grants. Add new incident grants only through an additive, reviewed migrator/provisioner path, never by widening runtime table privileges independently.
4. M17 emits finite-dimension service signals with no raw tenant identifiers in metrics or traces and exposes a private, default-off monitoring profile. Its telemetry is not incident authorization, a tenant-owned resource or proof of an individual customer's impact.
5. The current React organization workspace uses M1's authenticated session and a selected organization. M18 must integrate there, with Turkish localization and post-switch invalidation, not add an unrelated public route.

## 2. Organization ownership and systemic faults

P01 proposes every M18 incident is owned by exactly one selected organization. A systemic outage, outage affecting multiple organizations or suspected cross-organization disclosure may be observed in shared service telemetry, but **may not** create a global M18 incident or expose other affected tenant IDs.

Recommended rule: an authorized responder records the permitted local incident impact and a safe, global service-state classification, without another organization's records. If the fault requires a genuinely cross-tenant security response, keep the platform/security escalation record outside the tenant-scoped M18 data model and do not claim that M18 implements a global security incident system. Do not duplicate or link incidents across organizations automatically. Prove a Beta responder cannot learn Alpha's existence, count, note or source reference through M18 alert prefill or error behavior.

For an outage affecting the current organization, the scoped incident may describe `Affected service: api`, `Observed symptom: unavailable`, and `Observed scope: selected organization impact not independently verified`. It must not claim service telemetry proves cross-tenant impact.

## 3. Typed permission and explicit grant design

Proposed permission vocabulary: `ViewIncidentSummary`, `ViewIncidentDetail`, `CreateIncident`, `ReportIncidentImpact`, `AppendIncidentNote`, `ManageIncidentLifecycle`, `AssignIncident`, `ManageIncidentActions`, `AttachIncidentEvidence` and `ConfirmIncidentClosure`. Permission names are planning symbols, not current code.

Recommended roles:
- **Support and Administrator:** incident detail, manual creation, triage, assignment, notes, actions and transitions in their *selected active organization only*. Neither inherits M16 Auditor reading or unrestricted shipment data.
- **Manager:** sanitized summary, manual limited-impact incident reporting and a structured impact update. No internal investigation notes, raw diagnosis, technical state changes, severity changes, closure or assignment. If a Manager can create an incident but cannot read protected detail, the create response returns only the sanitized summary and reference, not an unrestricted detail DTO.
- **Auditor:** no implicit M18 read; read-only summary/detail requires a separate, recorded, expiring and revocable incident-read grant bound to `organization_id`, user and active Auditor membership. If the approved design does not include the grant mechanism, report G18-S02-A02 as BLOCKED, rather than silently treating a role as a grant.
- **Analyst:** no incident API visibility, writes or raw service diagnostics by default.

Recommended explicit grant schema (if P02 approves it): `incident_read_grants(organization_id, user_id, grant_id, status, scope, expires_at_utc, version, granted_by, granted_at_utc, revoked_at_utc)` with immutable grant event/audit history. The grant issuer is a currently active organization Administrator, acting through an audited, typed, organization-scoped operation; the grant recipient must be a currently active Auditor. A role change, organization suspension, account/session revocation or expiry renders it ineffective immediately. Do not reuse or reinterpret M13's KPI Auditor grant. The grant itself must not grant M16 audit, M17 private telemetry or export rights. The exact issuer and expiry policy remains subject to P02 approval.

For each endpoint, specify both permission and response visibility tier (summary, technical detail, allowed evidence) in a machine-reviewable contract. Enforce it server-side after the current session/selected-org check. Read and write paths must test permission changes between first check and data access. On writes, lock/revalidate the required membership and incident under the correct consistent lock order, and prohibit success after revocation.

## 4. Database structure, isolation and durable history

The initial schema in [architecture](M18_ARCHITECTURE_DESIGN.md) must become a reviewed migration contract before any code. Critical refinement:

| Table or feature | Required invariant |
| --- | --- |
| `it_incidents` | Tenant-owned identity and immutable `organization_id`; typed `state` and `severity`, `version` and explicit version CHECK; stable, tenant-local reference with uniqueness; no cascade deletion |
| `it_incident_events` | Composite `(organization_id,incident_id)` FK, monotonic per-incident sequence and UNIQUE constraint; immutable actor/state/event/evidence |
| `it_incident_notes` | Composite tenant FK, visibility classification, bounded text, server-originated actor and timestamp, immutable record |
| `it_incident_actions` | Explicit owner/state/version/last change; action-state change is recorded in immutable event/history, not silent overwrite |
| `it_incident_evidence` | Versioned, allowlisted safe observation schema with `observed_at_utc` and `captured_at_utc`; provenance classification and no raw trace/URL/payload |
| `it_incident_operation_ids` | Unique `(organization_id, actor_user_id, operation_key)` across create and subsequent commands, with `operation_kind`, canonical payload digest, incident reference, outcome, committed version and stable safe response identity |
| `incident_read_grants` if approved | Tenant-scoped grant, active Auditor prerequisite, expiry/version and recorded grant/revoke audit |

**Create-idempotency correction:** create has no incident ID before commit, so an operation key cannot require a preexisting incident ID. Scope it to the selected organization, authenticated actor and key, then bind the created incident to the committed operation record. Reusing that key with a different payload or operation kind returns conflict. Reusing after browser session renewal should remain safe for the same active actor, but must not override current revocation/permission checks; a stale actor cannot retrieve a formerly authorized result by replaying a key.

Canonical payload hash must be based on validated normalized command fields and an explicit versioned canonicalization algorithm, excluding transport-only headers. Hash comparisons should be constant-time where relevant. Commit identity must be known before returning success. Reserve the operation key, mutate projection, append event, append required M16 audit and store replay result in **one transaction**; a transient/ambiguous commit outcome is resolved by an authorized read of the operation record.

All tenant child FKs, unique keys and read predicates must be tested on real PostgreSQL, including attempts with a valid incident ID from another organization. Consider DB runtime direct-access limitations explicitly: an HTTP guard does not turn an overly broad database role into strong database tenant isolation. Choose either narrowly granted SQL routines with live scope predicates and valid server authority or an equivalent reviewed restrictive database boundary; do not rely solely on a browser role string.

## 5. M16 audit integration and transaction order

**Mandatory preimplementation spike:** trace the existing M16 audit trigger and provisioner contract. Do not invent a new public `AuditWriter` class or assume `AuditAccessRecorder` handles general incident mutations. The accepted M16 system uses migration-owned triggers and separate privileged actor context.

Recommended implementation:
1. M18 additive migration creates incident tables and installs explicit, migration-owned allowlisted audit trigger(s) or equivalent approved M16 audit procedure for sensitive incident changes. Every audit event category has a fixed event name, schema version, resource type, safe changed-field names and allowed enum codes. Never copy a free-text note or telemetry payload into audit metadata.
2. Begin PostgreSQL transaction, lock resource and live authorized actor/membership/organization, set `AuditActorContext.SetHumanAsync` inside the transaction, then invoke the approved incident state command. The database must distinguish trusted context from arbitrary user-supplied fields and must not infer privilege from a caller-controlled GUC alone.
3. Persist current projection, immutable event and operation key under the approved constraint checks. M16 audit capture must append before commit and fail the transaction if required audit cannot be recorded.
4. Return success only after actual commit; preserve source correlation ID in the sanitized API envelope. Required audit failure means full rollback, normally sanitized 503. An already-denied action remains denied if separately attempted denial audit fails.
5. Verify direct INSERT/UPDATE/DELETE/TRUNCATE and role/provisioner matrices on actual API and worker connections; runtime principals cannot disable guards or grant themselves DDL. Privileged migrator is outside app runtime threat boundary and is never used for normal API requests.

**Exactly-one nuance:** one *logical successful sensitive action* must have the documented one required category of audit record. If underlying triggers also audit secondary rows, enumerate those separately and assert the expected fixed set, not a blind count of one audit row regardless of mutation type. This avoids incorrectly treating multiple legitimate audit categories as a violation or silently double-counting an action.

## 6. Incident state, remediation, closure and emergency handling

Current canonical states: `New`, `Triage`, `Investigating`, `Mitigated`, `Monitoring`, `Resolved`, `Closed`. `Reopen` is an explicit audited command returning to `Investigating`, not an eighth durable state unless P03 changes.

There are two distinct facts: **service recovered** and **ticket closed**. Recovery requires a health proof and relevant data-integrity observation with timestamp, source, freshness classification and authorized verifier. `Resolved` requires a root-cause classification (`Known`, `Undetermined` with review reason, or `FalsePositive` only if P17 permits that exception) plus recorded action disposition. `Closed` requires independent confirmation or a formally approved separation exception under P16. A `Closed` ticket with outstanding corrective action must preserve open follow-up actions and explicit ownership; closure of immediate impact is not a claim all preventive actions are complete.

Recommended P16 separation rule: SEV-1/SEV-2 closure confirmation must be by an authorized active responder different from the actor who marked `Resolved`. SEV-3/SEV-4 may follow the same default; a same-actor exception requires a distinct explicit code and recorded approval, not an invisible workaround. If only one eligible responder exists and no authorized exception is approved, the incident stays `Resolved`. Neither system-generated fields nor the UI may fabricate a second independent human. This remains a policy proposal.

Do not force the on-call responder to wait for state-machine paperwork before real-world containment. Emergency mitigation may occur operationally and be documented afterward with true observed timestamps; the durable ticket state transition must still follow the approved sequence or an explicitly approved expedited route. Never backdate a state change to create apparent process conformance.

## 7. M17 evidence adapter and freshness semantics

M17 telemetry is private **service-level** evidence, not tenant-granted diagnostic browsing. M18 must not expose arbitrary Prometheus/Tempo queries, raw spans, Grafana links, log lines, external URLs or other tenants' metadata.

Preferred adapter boundary: a server-controlled, finite catalog of service and alert codes, with a dedicated internal read of safe current health classification only. The user may select only an allowlisted service/fault category; the server provides capture time, observed time, status (`healthy`, `degraded`, `unknown`, `unavailable`, `held`, `disabled` when applicable), allowed alert rule code and safe source digest. None of these observations alone proves tenant impact. If collector is unreachable or sampling absent, capture the degraded evidence state as `unknown`, not a green result.

Store an immutable snapshot at ticket creation or evidence append; monitor storage retention must not be required to read ticket history. Validate provenance and freshness, explicitly distinguish collected time from event time and prevent replay of untrusted client-provided alert metadata. If no safe M17 reading is available, manual incident reporting remains possible using a separately marked observed symptom. Verified recovery must use a relevant, currently valid source plus data-integrity check, not the existence of a saved old metric.

Reject client-supplied full trace/context IDs when unnecessary; use a bounded server digest with origin recorded, and avoid pretending it proves M17 trace continuity by itself.

## 8. Notes, privacy, retention and export

Bounded free-text notes inherently risk a user pasting secrets, so sanitization cannot promise that no secret ever reaches the database. P09 must settle whether to accept this residual risk with strict UI warnings, size bounds, format allowlisting, server rejection of known token patterns and output escaping, or require structured note categories only. In either case, **no free-text note** is copied to audit metadata, metrics, traces, HTTP error responses or unrelated exports. Test canary injection at all sinks, but document residual limitations honestly.

No unrestricted incident export, file upload, attachment, external notification, automatic deletion or retention purge is part of M18. M16's 365-day online audit retention policy does **not** authorize incident-record deletion. Preserve incidents and evidence until a separately approved lifecycle/archival policy exists; do not silently expire on local telemetry's 24-hour/250-MiB limits. Resource budgets and growth risks are recorded for future release planning.

## 9. Observable denial and recovery semantics

- `401`: session missing/expired. `403`: authenticated but insufficient role or revoked permission where exposing denial category is safe. `404`: foreign incident/evidence and unknown resource have identical body, timing class and no existence signal. `409`: stale version, conflicting operation key, invalid transition, or required organization selection with distinct safe error codes. `503`: required DB/audit unavailable. Exact status behavior must be contract-tested against current API conventions.
- Do not expose unauthorized incident counts in dashboards, cursor metadata, search autocomplete, error codes or logs.
- Read after revocation fails, including replay of previously successful mutation responses when permission has subsequently changed.
- Telemetry collector loss is fail-open for business function, while authorization and required audit write remain fail-closed.

## 10. Required implementation review proofs

Before M18-02 begins, an implementation review must show: approved P01/P02/P03/P07/P08/P10/P13/P16/P18 choices; selected grant approach; SQL migration and role-provisioner design; audit trigger proof; concurrency and idempotency truth table; tenant/authorization invariants; and exact corresponding [G18 assertions](M18_VERIFICATION_PLAN.md). The raw source-locked G18 campaign must later exercise every material guard using real two-organization PostgreSQL and authenticated HTTP fixtures, not only mocks.

This document does not supersede the original policy proposal, create a new global decision number, close prior S25 conditions or authorize implementation.
