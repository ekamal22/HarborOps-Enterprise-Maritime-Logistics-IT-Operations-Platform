# M16 Audit Privacy and Retention Policy

**Approved policy:** 2026-11-04. **Runtime enforcement:** The corrected standalone M16 revision `e571157` passed the reported source-locked technical campaign, including privacy and security probes. Independent raw-evidence review and G16 acceptance pending. See [M16 decisions](M16_POLICY_DECISIONS.md) and [executed verification](M16_EXECUTED_VERIFICATION.md).

## Data collection
The audit entry contains a generated UUID, nullable tenant scope for platform identity actions, event type/schema version/outcome, server-proven actor kind and permitted actor/session/source references, actor role, resource type/ID, safe correlation ID, server UTC time and an event-specific allowlisted JSON metadata object. Supported actor categories are Human, OperatorCli, IntegrationSource and System. Each event definition must specify maximum field and metadata sizes. Never use client-provided tenant IDs or actor claims as authority.

Do not persist plaintext credentials, password hashes, session cookies/tickets, CSRF values, bearer tokens/digests, authorization headers, full request/response bodies, raw tracking payloads, unrestricted notes, personal contact details, unredacted SQL parameters or exception dumps. Avoid raw client IP and user-agent strings by default. Use bounded outcome codes, non-sensitive identifiers and changed-field names instead.

## Event coverage
Capture sensitive identity lifecycle, membership/role/status changes, creation/update/archive of business resources, credential/source lifecycle, authorized/denied audit reads and security-relevant denials. A sensitive successful business mutation and its audit entry are one transaction. A denied action never commits business effects; attempt a bounded separate denial audit when storage is available. Existing M1 auth_events and M8 immutable tracking receipts/history remain separate and authoritative for their original purposes.

## Access and response
Only an active Auditor of the selected organization can list or read its audit rows. Revalidate current scope; no Administrator override or global export. Response DTOs and filters are allowlisted, paging is bounded and foreign audit IDs must not reveal foreign existence. The eventual M12 export permission does not imply audit export.

## Retention, archival and holds
The online target is 365 days with a controlled archival step. Do not schedule deletion before archival is designed, implemented, integrity-tested and separately authorized. Preserve tenant scope and access controls in an archival manifest; legal/incident holds prevent disposal. Privileges for any future disposal operation must be segregated from API and worker runtime.


## Candidate event mapping and non-locking tenant scope
The corrective candidate stores audit event categories for membership role and status changes, integration source and credential lifecycle changes, human/operator actions and accepted source-authenticated M8 tracking events. Mutation metadata contains only the operation name and an allowlisted array of changed field names. Field values, credentials and tracking payloads remain excluded.

Audit tenant identifiers are historical facts, not live organization foreign keys. This prevents an audit append from taking an unexpected organization-row lock during concurrent authorization and revocation. The source business tables and restricted append path establish the tenant identity. Auditor reads use tenant-scoped keyset cursors rather than offsets, because every audit read itself generates an event.

These mappings were exercised in the reported standalone G16 campaign, including allowlist tests, a stale-context mutant and HTTP metadata checks. Independent review of the raw evidence remains pending. The retention, privilege and archival decisions above are unchanged.

## Verification and limits
G16 must scan synthetic secret canaries across rows, HTTP responses, logs, browser traces and evidence bundles; verify event metadata allowlists, bound denial rates and test failed audit persistence. The policy does not establish regulatory certification or tamper resistance against database superusers. See [G16 verification](M16_VERIFICATION_PLAN.md).

The G16 report records no secret canaries in the scanned results and no Playwright traces produced during the final campaign. This is not a claim of external privacy certification. The 1024-byte metadata CHECK lacks a separate executable negative probe, and no automated archival or purge is authorized.
