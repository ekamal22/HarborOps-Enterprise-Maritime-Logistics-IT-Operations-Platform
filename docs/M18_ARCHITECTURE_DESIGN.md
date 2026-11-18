# M18 Incident Lifecycle and Remediation Architecture

**Prepared:** 2026-11-18  
**Status:** PROPOSED DESIGN, NOT IMPLEMENTED  
**Policy:** [M18 local proposals](M18_POLICY_PROPOSAL_20261118.md), approval pending.  
**Source baseline:** `72251f5310f24fb98322b7eb403a5e9e66e040ef`. Existing G16 and G17 accepted/integrated, with their recorded qualifications.

## 1. Context, accepted neighbors and trust boundaries

M18 implements FR-016 and FR-020 under FR-002, FR-014, NFR-002/003/005/006/007/008/012. It consumes existing technical observation from M17 and audit guarantees from M16; it does not own either subsystem. Original source reference points:

- `docs/OPERATIONS.md`: seven-state lifecycle, severity SEV-1..4 and recovery workflow.
- `src/HarborOps.Domain/Organizations/OrganizationPolicy.cs`: five fixed roles and typed permissions. **No M18 permission exists yet.**
- `src/HarborOps.Infrastructure/Organizations/OrganizationStore.cs`: current session-selected organization checks and live role/membership resolution.
- `src/HarborOps.Infrastructure/Persistence/HarborOpsDbContext.cs`: EF Core PostgreSQL model and schema ownership.
- `src/HarborOps.Api/Program.cs`: authenticated endpoint registration, M17 opt-in telemetry and M16 audit support.
- `src/HarborOps.Api/Audit/AuditEndpoints.cs`: scoped, read-only Auditor-only security-audit endpoint. Incident audit is not an audit-reading grant.
- `web/src/auth/OrganizationWorkspace.tsx`, `web/src/i18n/messages.mjs`: organization workspace and bilingual text.
- `docs/M17_G17_S25_ACCEPTANCE_DECISION_20261117.md`: technical PASS recommendation and integration, with separate S25 signature pending and P09 qualified.
- `docs/M16_POLICY_DECISIONS.md`: immutable auditing, required same-transaction successful mutation+audit, least-privilege runtime roles and retention restrictions.

G17 outputs metrics/alerts but no M18 ticket API. Browser access to private telemetry must not be assumed. The durable incident ledger persists **its own** sanitized snapshot so expired telemetry is not a data-loss dependency.

## 2. Component boundary

```text
Organization-scoped React workspace, English/Turkish
  -> same-origin /api/v1/organizations/{id}/incidents
     -> M1 server session and anti-CSRF
     -> M2 live selected-organization and typed M18 permission
     -> M18 endpoint and validation adapter
     -> M18 domain state machine + version/idempotency guard
     -> restricted M18 PostgreSQL store
          -> incident projection, append-only event/action history
          -> required M16 audit entry in same transaction
     <- sanitized typed response + correlation ID

Private M17 alert and health context
  -> explicitly authorized, read-only snapshot/preview adapter
  -> server sanitizer and allowlisted service/rule metadata
  -> human-confirmed create or link operation
  -> incident immutable evidence snapshot

No automatic M17 -> ticket write, M15 dispatch, M9 worker or public webhook.
```

## 3. Planned domain model and relational invariants

Names here are implementation proposals, not existing tables.

| Table | Required columns and core invariants |
| --- | --- |
| `it_incidents` | `id uuid PK`, `organization_id uuid NOT NULL`, immutable tenant-local reference, bounded title/service/impact, `severity`, `state`, assignee (nullable), `version bigint >= 1`, `detected_at_utc`, `created_at_utc`, `updated_at_utc`, `resolved_at_utc`, `closed_at_utc`, creator actor; nullable resolution/exception code |
| `it_incident_events` | `id uuid PK`, immutable `organization_id` + `incident_id`, event number, previous and next state, event kind, actor role/user/session provenance, reason code, server UTC, safe evidence reference, recorded incident version; unique `(organization_id,incident_id,event_number)` |
| `it_incident_notes` | `id uuid PK`, tenant and incident keys, actor, UTC, immutable bounded sanitized text, visibility classification and event link; no UPDATE/DELETE/TRUNCATE for runtime |
| `it_incident_actions` | `id uuid PK`, scoped incident, owner, short title, planned/observed UTC, action state Proposed/InProgress/Verified/Cancelled, immutable version and history of state changes; no hard deletion |
| `it_incident_evidence` | `id uuid PK`, tenant+incident keys, allowlisted source kind ManualObservation/M17Snapshot/RecoveryProbe, service code, rule code, observed UTC, bounded diagnostic class and correlation digest, verification status, immutable snapshot JSON with strict schema version |
| `it_incident_operation_ids` | unique `(organization_id, actor_user_id, operation_key)` for create and later commands; typed operation kind, canonical payload digest, nullable incident reference at initial reservation, committed version and safe result identity; altered replay returns 409. A new incident has no ID before create commits |

Proposed database restrictions: CHECK for service/role/severity/state lengths and enumeration; composite tenant FKs `(organization_id,incident_id)`; indexes for scoped list `(organization_id,state,severity,updated_at_utc,id)`, assignee, deterministic pagination, and immutable history. Do not use cascade delete for evidence or history. No raw M17 URLs, SQL text, personal contact info, cookie values, bearer tokens or free-form request bodies in audit metadata. Database grants enforce no runtime DDL and protect immutable tables from UPDATE/DELETE/TRUNCATE.

Potential action links to shipment or M11 exception are **not implicit**. A future typed integration must authorize both source resource and incident in the *same* organization at read time; no universal cross-domain foreign key or automatic business-data access. No unapproved data export.

The migration must be append-only, provisioner-compatible with the current migrator role and safe for existing databases. Creating M18 tables does not automatically install M16 audit capture. Add reviewed migration-owned trigger/routine coverage for every sensitive incident mutation, with transaction-local server-established actor context, an explicit audit taxonomy and centralized `AuditRoleProvisioner` grants. Do not assume a general-purpose runtime audit writer exists. Use new versioned migrations and EF snapshot updates without editing previously accepted migrations. Check fresh, repeat, upgrade and rollback-of-failed-transaction behavior. Existing data must remain unchanged.

## 4. Domain invariants and concurrency

- Every state change goes through an explicit command and the approved transition matrix. Generic patch cannot set `state`, `version`, `resolvedAt` or actor fields.
- Lock the incident and verify live membership, tenant ownership, permissions and `expectedVersion` inside the transaction. An earlier HTTP permission check alone is insufficient.
- Create one revision and one immutable event per successful command. Concurrency loser gets 409 with no duplicate audit, note or action. Replays with the same operation key and identical payload return the recorded result; changed payload with the same key is 409.
- No success response before commit. On ambiguous commit outcome re-read operation key; never invent a success.
- Evidence-required resolution/closure uses structured checks: health signal captured with observed-at timestamp, observed recovery, pertinent business/data integrity result, approved exception if needed, actor and UTC time. A raw green color is not sufficient.
- Reopen does not erase resolution/closure evidence or overwrite original detected time. Reopen is a new event and increments version.
- Disallow stale assignee after suspension/removal and external-organization assignment. Assignee change is versioned and audited.
- Every server-generated action time is UTC; client-provided historical detection times are validated with explicit offset and bounded range.

## 5. Proposed versioned HTTP contract

All routes below are *planned*, not implemented. Group `/api/v1/organizations/{organizationId}/incidents`, require the M1 server session; do not use browser bearer tokens. All writes require CSRF/Origin and exact content-type/schema checks.

| Verb and path | Scope / behavior | Required authorization |
| --- | --- | --- |
| `GET /` | Stable keyset list; allowlisted state, severity, service, assignee, changed-since filters; safe summary DTO | `ViewIncidentSummary` |
| `GET /{incidentId}` | Scoped detail and safe timeline envelope | `ViewIncidentDetail` or summary-only response under limited grant |
| `POST /` | Manual create; typed detection, source, impact and operation key | `CreateIncident` |
| `POST /{incidentId}/transitions` | Explicit `expectedVersion`, target state, reason/evidence, operation key | `ManageIncidentLifecycle` |
| `POST /{incidentId}/reopen` | Explicit reason/evidence/version, append new event | `ManageIncidentLifecycle` |
| `PATCH /{incidentId}/assignment` | Scoped active assignee, expectedVersion and reason | `AssignIncident` |
| `PATCH /{incidentId}/severity` | Audited severity change with new evidence | `ManageIncidentLifecycle` |
| `POST /{incidentId}/notes` | Immutable bounded, visibility-aware note | `AppendIncidentNote` |
| `POST /{incidentId}/actions` | Create corrective task, scoped owner and follow-up | `ManageIncidentActions` |
| `PATCH /{incidentId}/actions/{actionId}` | Typed action transition/version and evidence | `ManageIncidentActions` |
| `POST /{incidentId}/evidence` | Attach sanitized structured manual or M17 snapshot | `AttachIncidentEvidence` |
| `GET /{incidentId}/history` | Cursor-safe immutable event history | `ViewIncidentDetail` |

The actual contract must enumerate status codes, field names, max sizes, examples and replay semantics before M18-02 implementation. Suggested errors: 400 invalid schema/filter, 401 invalid session, 403 forbidden/revoked, 404 foreign or nonexistent record with indistinguishable body, 409 stale version/invalid transition/selection required, 413 oversized input, 429 bounded rate-limiter, 503 data or required audit unavailable. Preserve existing error-envelope `code`, sanitized `message` and server `correlationId`. Never return SQL exceptions or sensitive evidence. Cursors must be signed/bound to organization, role/user scope, filters, cutoff and expiry, and reject tampering or session switching.

## 6. Permissions, privacy and telemetry

Add narrowly typed permissions in the OrganizationPolicy and test all five role combinations against explicit approval. If Auditor incident visibility is approved, implement a separate, revocable and expiring organization-scoped M18 read grant managed by an authorized active Administrator. It is neither M13 KPI grant nor M16 audit permission. Manager limited creation/reporting returns safe summary only, not private technical detail. Selected organization and user/session role are revalidated on every operation; writes also revalidate transactionally. Auditor visibility must use a new explicit revocable M18 grant, **not** the existing `ViewAuditEvents` grant. Administrator never acquires global tenant access by name. Manager limited rights must not accidentally expose technical logs.

M17 is a source of observations, not incident authorization. Invariant: monitoring remains default-off, private and fail-open as telemetry while authorization/audit remains fail-closed. An expired M17 metric is displayed as unknown and does not count as verified recovery. Use only sanitized correlation digests and finite service/alert identifiers in M17 metrics, traces and incident evidence. A browser link to a local Grafana dashboard is optional only if privacy, origin and permission are proven; no arbitrary alert URL or client-selected query. M17 telemetry is service-level, not tenant-owned: its status does not prove another organization's impact. Collector loss, missing signals and expired samples remain unknown, not healthy. See the [security and transaction refinement](M18_SECURITY_AND_TRANSACTION_CONTRACT.md).

## 7. Frontend and localization contract

Create an organization-scoped `web/src/incidents/` workspace integrated with `OrganizationWorkspace`; no separate login or public incident page. Minimum functional pages: list/filters with status chips and severity, detail with immutable chronology, triage form, assignment, investigating notes, mitigation/recovery checklist, corrective actions, close with evidence, reopen. Protect actions by server permission, not conditional UI alone.

Show trustworthy empty/unavailable/forbidden/conflict/loading states. Display `unknown` explicitly for unavailable M17 data; never claim green health from missing values. A stale-version 409 must refresh and preserve unsent user draft rather than silently overwriting another actor. Add **all new UI copy** to `web/src/i18n/messages.mjs` with English and Turkish translation, and verify both in all three supported browser engines. Avoid converting server enum/status keys in transit; translate display text only.

M23 owns final visual polish. M24-04 owns the dedicated accessibility acceptance audit; M18 must still preserve correct functional semantics, labelled inputs and observable state without falsely claiming an M18 accessibility sign-off.

## 8. Integration, deployment and ownership

Recommended implementation module paths: `src/HarborOps.Domain/Incidents/`, `src/HarborOps.Infrastructure/Incidents/`, `src/HarborOps.Api/Incidents/`, `web/src/incidents/` and appropriately scoped existing tests. Shared file changes to `Program.cs`, `OrganizationPolicy.cs`, `HarborOpsDbContext.cs`, EF migrations, `OrganizationWorkspace.tsx` and `messages.mjs` require an explicit diff and branch-integration review. Protect G16 audit SQL and M17 performance/telemetry boundaries. **M18 planning does not grant permission to implement, migrate, activate workers, dispatch CI, merge runtime or deploy.**

## 9. Deliverable and proof map

| M18 work package | Implementation proof | Independent exit |
| --- | --- | --- |
| M18-01 | Signed decisions, state machine, five-role matrix, severity/notes/error/retention contracts | No ambiguous policy or unreviewed privilege expansion |
| M18-02 | Tenant-isolated incident schema, endpoints, typed permissions, version-safe transactions, immutable audit | Fresh, upgrade, attack and concurrency tests |
| M18-03 | Notes, corrective action register, recovery evidence, closure/reopen and bilingual workflow | Three-browser functional proof, privacy and rollback tests |
| M18-04 | Controlled alert context -> manual incident -> investigate -> recover -> verify/close, Q3 snapshot check | Source-locked no-skip G18 matrix, reviewer report and explicit gate decision |

See [G18 verification plan](M18_VERIFICATION_PLAN.md), [assertion inventory](M18_G18_ASSERTION_INVENTORY_20261118.json), and [disposable runbook](M18_OPERATIONS_RUNBOOK.md) for the exact evidence contract.
