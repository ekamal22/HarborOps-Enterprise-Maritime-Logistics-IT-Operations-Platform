# M18 As Implemented: Resolutions of the Contract Questions

**Date:** 2026-11-18
**Status:** Describes the source frozen for the local G18 campaign. It records how the open questions in the [security and transaction contract](M18_SECURITY_AND_TRANSACTION_CONTRACT.md) and the [API and UI examples](M18_API_AND_UI_CONTRACT_EXAMPLES.md) were resolved in code and where the implementation differs from those illustrative examples. It is not an acceptance and it does not change any approved policy.

## 1. Authorization and tenancy

- Every incident request revalidates, inside the write transaction and under share locks, the session, the user, the selected organization and the membership, then applies the typed permission. A membership suspended while a write waits on its row lock makes the write fail.
- Row-level security is enabled and forced on all eight incident tables. The policy accepts only the existing signed, short-lived M11 read scope for the selected organization. A caller-written setting is not authority.
- The runtime role receives SELECT and INSERT on all tables and UPDATE only on named mutable columns. It has no DELETE, TRUNCATE, TRIGGER or REFERENCES and cannot update identity, tenant, creator, history or operation-digest columns. The worker and projector roles receive nothing. All grants are in the central role provisioner, none in the migration.
- A database trigger repeats the transition graph, the version increment, the verified-recovery requirement for Resolved and the distinct-confirmer requirement for SEV-1 and SEV-2 closure, so a direct statement cannot bypass the application.
- Manager and Auditor responses never carry the assignee. Notes, actions and responder identities are technical detail.
- The Auditor read grant is a separate table. An active Administrator issues it to an active Auditor for at most 30 days, it expires on the server clock and can be revoked, and an Administrator can list grants. It never reaches M16 audit or M17 telemetry.

## 2. Audit

The accepted M16 capture function is unchanged. The M18 migration adds `m18_audit_capture()`. One fixed event per logical change, written in the same transaction, carrying the request correlation id and the server-resolved actor and role:

| Mutation | Events |
| --- | --- |
| create | `incident.created` |
| transition, reopen | `incident.state_changed`, `incident.reopened` |
| assign, severity, impact | `incident.assigned`, `incident.severity_changed`, `incident.impact_changed` |
| note, action created, action changed | `incident.note_added`, `incident.action_created`, `incident.action_changed` |
| manual evidence, M17 snapshot | `incident.evidence_attached` |
| recovery | `incident.recovery_verified` plus two `incident.evidence_attached` |
| grant, revoke | `incident.read_grant_granted`, `incident.read_grant_revoked` |
| create from prefill | `incident.created` plus `incident.evidence_attached` |

Metadata holds only the operation and an allowlist of changed column names. The immutable event and action-event histories are not audited a second time. If the audit insert fails the whole mutation rolls back and the API returns a sanitized 503. Denied requests write no incident audit row.

## 3. Idempotency and concurrency

The operation key is unique per organization, actor and key, carries a kind and a canonical payload digest, is reserved in the same transaction as the mutation and the audit, and is created before any incident exists. Same key with the same payload replays the current incident under the caller's current permission. A different payload or kind returns 409. A rolled-back attempt leaves no key. The browser keeps one key per identical command on an identical version and discards it only after a definitive refusal.

## 4. Recovery and closure

- Recovery is recorded only from `Monitoring`, only for `api` and `database`, and only after the server executes the existing readiness check, verifies the incident event chain and action histories are gap free, and records service-specific probe rows. Evidence must be younger than five minutes (configurable between 1 and 900 seconds) when the incident is resolved. A caller cannot supply any outcome field.
- `notifications` and `tracking_held` have no verifiable probe. Recovery for them returns the explicit code `recovery_unsupported` and those incidents cannot reach Resolved. This is the open policy question recorded in the [correctness audit](M18_CORRECTNESS_AND_TEST_AUDIT_20261118.md).
- Gates look only at records written since the incident entered the current state. `Mitigated` to `Investigating` is available for a failed mitigation and needs a new note or observation. The self-loop in the policy table is not implemented, because notes and actions already record new investigation without version churn.
- SEV-1 and SEV-2 closure needs a different active responder than the one who resolved. There is no exception path. SEV-3 and SEV-4 may be closed by the resolving actor.

## 5. M17 evidence

The server reads the finite in-process database readiness signal. `GET .../incidents/m17-prefill` returns a preview and a protected token bound to organization, user and session that expires after ten minutes. It creates nothing. Creating an incident with the token stores one `M17Snapshot` with status, observed time and a bounded digest of incident, service, status and microsecond time that can be recomputed from the stored row. A missing or stale signal is `unknown`. Tenant impact is never inferred from the signal.

## 6. Differences from the illustrative examples

| Example | Implemented |
| --- | --- |
| `reference` such as `INC-000001` | The incident UUID is the stable reference. No per-tenant counter exists. |
| nested `impact` and `source` objects | Flat `impactCode`; the source is implied by a prefill token or manual creation. |
| `Location` header on create | Not set. |
| `evidenceIds` on transitions | Evidence is attached by separate commands and gates query the evidence table. |
| `targetAtUtc` on actions | Not implemented. |
| Distinct 409 codes such as `stale_version` | One `conflict` code, plus `recovery_not_verified`, `recovery_unsupported` and `stale_or_invalid_cursor`. |
| Listing ordered by recent activity | Ordered by creation time so edits cannot hide records while paging. |
| Rate limit | 30 requests per user per minute by default. The setting `M18:RequestsPerMinute` exists so disposable tests can raise it. |

## 7. Activation

Incident routes exist only when `M18:EnableIncidents` is true and the web workspace only when `VITE_M18_ENABLED` is `true`. No compose file, appsettings file or retained deployment enables either. M9 transport, M15 delivery and M17 exporters are untouched.
