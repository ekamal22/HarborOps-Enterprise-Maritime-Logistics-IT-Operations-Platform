# M18 Proposed HTTP and UI Contract Examples

**Prepared:** 2026-11-18  
**Status:** Design examples only. Not implemented, approved, exercised or an endpoint guarantee.  
**Authority:** [M18-P01 to P20](M18_POLICY_PROPOSAL_20261118.md) must be explicitly decided; see [transaction/security contract](M18_SECURITY_AND_TRANSACTION_CONTRACT.md).

These payloads remain the review baseline for the M18-02 source candidate and must be reconciled with implementation and tests. Field spelling, operation-ID transport, status and exact bounds are **proposed** and may be changed through M18-01 policy/contract approval. They are not observations of a live API.

## 1. Global request and response rules

- Routes: `/api/v1/organizations/{organizationId}/incidents` under existing authenticated same-origin session. The selected active organization and required new typed permission are validated at every route, not inferred from URL ownership.
- Request mutation: HTTPS for hosted environments; local insecure loopback only where existing M1 trust policy explicitly permits. Use existing anti-CSRF bootstrap, exact accepted Origin/Fetch Metadata rules and `X-HarborOps-CSRF` for mutation. CORS preflight or `Referer` does not bypass CSRF.
- Proposed operation header: `X-HarborOps-Operation-Id: <UUID>` on every create or mutation. Requires an active authenticated actor and selected organization, and is unique for that actor within that organization. A create reserves the key before a new incident ID is allocated.
- Proposed `expectedVersion`: required on every mutation after creation; integer at least 1. If stale, return 409 without state change, new history or new M16 audit record. Preserve the caller's draft on the browser.
- Response `correlationId` comes from the existing server-controlled request scope. Client-supplied trace IDs and URL filters cannot become authorizing scope.
- All incident timestamps are persisted as UTC. User-entered observation time and server commit time remain separate; never silently backdate a transition or actor action.
- No endpoint accepts arbitrary `state`, `actorUserId`, `organizationId`, `version`, `auditMetadata`, `traceUrl`, `sqlText` or `role` as a mutation authority.

## 2. Incident creation with no preexisting incident ID

```http
POST /api/v1/organizations/{selected-org-id}/incidents
Content-Type: application/json
X-HarborOps-CSRF: <session-CSRF-value>
X-HarborOps-Operation-Id: 7d203f24-b58e-4c49-a44a-944a426726c5
```

```json
{
  "title": "API readiness degraded",
  "serviceCode": "api",
  "severity": "SEV-2",
  "detectedAtUtc": "2026-11-18T10:20:00Z",
  "impact": {
    "category": "service_unavailable",
    "scope": "current_organization_report",
    "verification": "reported_not_confirmed"
  },
  "source": {
    "kind": "manual_observation"
  }
}
```

Proposed Support/Admin success: `201 Created`, `Location` set to the new scoped incident URL, a newly generated incident ID and version 1; server UTC creation and `state: "New"`. A Manager with approved limited-create permission receives only a **summary tier** DTO, never the internal technical notes/evidence. No automatic event is created from merely viewing an M17 alert.

```json
{
  "id": "18f49b8e-1c57-45d3-9ab5-d36d918c2651",
  "reference": "INC-000001",
  "state": "New",
  "severity": "SEV-2",
  "serviceCode": "api",
  "summary": "API readiness degraded",
  "version": 1,
  "createdAtUtc": "2026-11-18T10:20:03Z",
  "correlationId": "safe-server-generated-value"
}
```

A duplicate request with the same organization, authenticated actor, operation key, operation kind and canonical body must not create a second incident. The response may carry the original outcome with a safe replay marker; the response body must be filtered against **current** permissions before it is replayed. Same key with modified body or kind: 409 conflict. Reuse after logout/revocation: no old data disclosure, and never an authentication bypass.

## 3. Explicit transition and evidence

```http
POST /api/v1/organizations/{selected-org-id}/incidents/{incident-id}/transitions
X-HarborOps-CSRF: <session-CSRF-value>
X-HarborOps-Operation-Id: efed3a3d-0083-4b03-98ad-f07f3f21129e
Content-Type: application/json
```

```json
{
  "expectedVersion": 4,
  "toState": "Monitoring",
  "reasonCode": "service_recovery_under_observation",
  "evidenceIds": ["43b6b5f5-9f68-4b43-95ec-1d33bd77ae03"]
}
```

Proposed positive result: sanitized updated projection and server-confirmed new version plus immutable event ID. Reject unsupported transitions, missing required evidence, duplicate or stale version, mismatched tenant-evidence links or revoked actor with a safe error. A 409 must not provide protected latest technical fields to a caller who now lacks detail-read permission.

Resolution and closure are different commands. Proposal for `Monitoring -> Resolved`: fresh recovery observation, relevant integrity check, root-cause classification or approved unknown reason, and corrective-action register disposition. Proposal for `Resolved -> Closed`: independent eligible confirmer for SEV-1/SEV-2 or an expressly approved and audit-recorded exception. A ticket with no second authorized responder stays `Resolved`, not automatically Closed. An already Closed incident must be reopened through a distinct versioned command, never a generic status patch.

## 4. Corrective actions and notes

A proposed corrective action is a separate tenant-owned record with state and history:

```json
{
  "title": "Verify recovery after database reconnect",
  "ownerUserId": "00000000-0000-0000-0000-000000000101",
  "targetAtUtc": "2026-11-18T14:00:00Z",
  "state": "Proposed",
  "expectedIncidentVersion": 5
}
```

`ownerUserId` is an **example** test fixture ID, not a real account. The API must check that the assignee is an active eligible member of the current organization at commit. Any action update is versioned, audited and reflected in immutable history. A Closed incident may retain separately tracked follow-up preventive tasks; the closure record must explicitly disclose their remaining ownership rather than imply completion.

Notes, if free text is approved under P09, require plain-text-only storage, bounded UTF-8 length, controlled visibility, safe output encoding and no copy to M16 audit metadata, metrics or HTTP error envelopes. This does not guarantee perfect secret detection. The approval must choose between this residual risk and structured-only observations before the feature is implemented. No HTML markup, images, URLs to internal telemetry, files or bulk exports are accepted by default.

## 5. Safe evidence snapshot, not arbitrary telemetry browsing

```json
{
  "kind": "M17Snapshot",
  "serviceCode": "api",
  "observationCode": "database_unavailable",
  "observedAtUtc": "2026-11-18T10:19:58Z",
  "capturedAtUtc": "2026-11-18T10:20:04Z",
  "status": "degraded",
  "correlationDigest": "bounded-server-generated-digest"
}
```

This is a **server-produced** schema illustration. The client should request a permitted source preview using a finite code, not provide the snapshot's privileged fields as trusted facts. The API must never accept a raw Prometheus expression, Tempo query, user/organization ID from telemetry, private exporter address or caller-supplied observation digest as the basis of authority. Global API health does not prove any particular organization's data exposure.

If signal is absent, disabled, held, stale or exporter unavailable, preserve the correct non-green classification and allow a human to create a manual incident with explicit source quality. Recovery verification needs a fresh relevant check and data-integrity proof, not an old stored green sample.

## 6. Permission and error examples

| Situation | Proposed response | Visibility requirement |
| --- | --- | --- |
| Session expired | 401 sanitized auth error | No incident data |
| Active Analyst incident lookup | 403 where role denial is safe | No existence/data disclosure |
| Foreign organization incident ID versus unknown ID | Identical 404 | No difference in identity, notes or counts |
| Manager creates allowed limited report | 201 safe summary | No technical detail or diagnostics |
| Manager requests Support-only note | 403 or same safe-hidden response per approved policy | No note metadata |
| Auditor with expired/revoked incident grant | 403 or same safe-hidden response per approved policy | No cached detail or cursor continuation |
| Stale expected version | 409 `stale_version` | No mutation, no private latest state |
| Key reused with different canonical payload | 409 `operation_key_conflict` | No duplicate event |
| Invalid state/size/cursor | 400/413 as appropriate | No stack trace, SQL or raw text |
| Rate limiter exceeded | 429 | No state or evidence mutation |
| Database or required M16 audit unavailable | 503 sanitized | No success until commit |
| M17 exporter unavailable | Incident API remains functional if DB/audit healthy | `unknown` evidence, not green |

The actual error taxonomy must match [API Contracts](API_CONTRACTS.md) before implementation. All responses use a bounded `code`, sanitized `message` and server `correlationId`. Tests must compare foreign-ID and unknown-ID results at body and status level, confirm stable pagination, and ensure errors cannot echo user note content or secret canaries.

## 7. Cursor and filter contract

Proposed list query: `state`, `severity`, `serviceCode`, `assigneeId`, `changedSinceUtc`, `cursor` and `limit`; exact accepted fields determined under P18. Default 25, maximum 100, based on accepted bounded query conventions. Use deterministic compound keyset ordering such as `updated_at_utc DESC, id DESC`. Protect cursor integrity, expiry, organization, session/user or permission-scope, selected filters and stable snapshot cutoff. A stale/cross-organization/revoked cursor must fail without revealing rows; a badly formed cursor cannot trigger unbounded scans.

No ambiguous `page=N` bypass, no arbitrary sort SQL, no unrestricted text search and no incident counts for unauthorized callers. Tests must include concurrent inserts, equal timestamps, resumed cursor after role revocation and source ID tampering.

## 8. UI example: permission-aware incident workspace

The intended view lives beneath the existing `OrganizationWorkspace`, not a public `/incidents` route. A Support user sees scoped incident list, severity, triage, owned notes/actions and explicit state-transition controls. An Administrator sees the same only in the selected organization. A Manager sees allowed summary and reports impact without private investigation details. An Auditor sees only read-only fields granted and currently valid. An Analyst does not see incident resources, and direct API requests still deny.

All user-visible strings require English and Turkish coverage through the existing dictionary. Organization switching immediately clears incident result caches, cursors, selected incident, permission flags and unsent cross-tenant drafts. A 409 refreshes authorized incident data but retains unsent draft in the same scope until the actor reconciles. Missing telemetry is shown as `Unknown` / an appropriate Turkish label, never a success indicator. All three browser engines must prove actual state/authorization behavior.

This proposal is not a substitute for final M24-04 dedicated accessibility acceptance, and does not assert any M18 runtime was created.
