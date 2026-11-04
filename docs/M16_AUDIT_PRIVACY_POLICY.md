# M16 Audit Privacy and Retention Policy

**Approved policy:** 2026-11-04. **Runtime enforcement:** Not yet implemented. See [M16 decisions](M16_POLICY_DECISIONS.md).

## Data collection
The audit entry contains a generated UUID, nullable tenant scope for platform identity actions, event type/schema version/outcome, server-proven actor kind and permitted actor/session/source references, actor role, resource type/ID, safe correlation ID, server UTC time and an event-specific allowlisted JSON metadata object. Supported actor categories are Human, OperatorCli, IntegrationSource and System. Each event definition must specify maximum field and metadata sizes. Never use client-provided tenant IDs or actor claims as authority.

Do not persist plaintext credentials, password hashes, session cookies/tickets, CSRF values, bearer tokens/digests, authorization headers, full request/response bodies, raw tracking payloads, unrestricted notes, personal contact details, unredacted SQL parameters or exception dumps. Avoid raw client IP and user-agent strings by default. Use bounded outcome codes, non-sensitive identifiers and changed-field names instead.

## Event coverage
Capture sensitive identity lifecycle, membership/role/status changes, creation/update/archive of business resources, credential/source lifecycle, authorized/denied audit reads and security-relevant denials. A sensitive successful business mutation and its audit entry are one transaction. A denied action never commits business effects; attempt a bounded separate denial audit when storage is available. Existing M1 auth_events and M8 immutable tracking receipts/history remain separate and authoritative for their original purposes.

## Access and response
Only an active Auditor of the selected organization can list or read its audit rows. Revalidate current scope; no Administrator override or global export. Response DTOs and filters are allowlisted, paging is bounded and foreign audit IDs must not reveal foreign existence. The eventual M12 export permission does not imply audit export.

## Retention, archival and holds
The online target is 365 days with a controlled archival step. Do not schedule deletion before archival is designed, implemented, integrity-tested and separately authorized. Preserve tenant scope and access controls in an archival manifest; legal/incident holds prevent disposal. Privileges for any future disposal operation must be segregated from API and worker runtime.

## Verification and limits
G16 must scan synthetic secret canaries across rows, HTTP responses, logs, browser traces and evidence bundles; verify event metadata allowlists, bound denial rates and test failed audit persistence. The policy does not establish regulatory certification or tamper resistance against database superusers. See [G16 verification](M16_VERIFICATION_PLAN.md).
