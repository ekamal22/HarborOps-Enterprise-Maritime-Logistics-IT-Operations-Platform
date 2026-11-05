# M16 Implementation Status

**As of:** 2026-11-05  
**Branch:** `feat/m16-audit-access-hardening`  
**Final standalone tested source:** `e5711573839b185a2dfc7d9f2c64ce4fc18d4c13`  
**State:** Corrected implementation candidate completed; standalone G16 source-locked technical campaign passed as reported. Independent review of the 162-file main campaign completed; **S07/S08/S28 supplemental log and JSON REVIEWED** (6/6 consistent checks; [record](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/0bf356dba78e51a6aafdeb665870ffa24789b3d9/docs/M16_G16_SUPPLEMENT_EVIDENCE_REVIEW.md)), M9 combined verification and G16 acceptance pending. No merge into `main`.  
**Policy:** M16-D01 through M16-D05 approved on 2026-11-04.  
**Verification record:** [M16 executed technical verification](M16_EXECUTED_VERIFICATION.md), with source report and scenario matrix links.

## M16-01: Approved audit schema and privacy

The five accepted decisions remain authoritative: Auditor-only selected-organization access, 365-day online retention followed by separately verified archival, atomic fail-closed sensitive mutation auditing, segregated PostgreSQL identities, and deferred external cryptographic anchoring. See [decisions](M16_POLICY_DECISIONS.md), [privacy](M16_AUDIT_PRIVACY_POLICY.md) and [architecture](M16_ARCHITECTURE_DESIGN.md). Retention automation and deletion are not implemented.

## M16-02: Implemented storage and read API

- Migration `20261104180000_AuditEntries` adds append-only `audit_entries`, tenant scope and chronological indexes, capture triggers, protected UPDATE/DELETE/TRUNCATE operations and restricted runtime grants.
- Sensitive business changes and required audits are transactionally coupled. M1 identity events and M8 immutable tracking history remain distinct. The audit table deliberately has no organization foreign key: the former FK caused a PostgreSQL lock-order inversion that is now corrected and mutation-tested.
- Actor context distinguishes Human, OperatorCli, IntegrationSource and System. Human role uses the current selected membership; accepted M8 tracking events use the integration source and do not inherit stale human context.
- Metadata is limited to `operation` and allowlisted `changedFields`, without values, secrets, raw payloads or user-provided authority.
- Read-only GET list/detail routes enforce current selected-organization Auditor access, tenant-bound keyset cursors, bounded requests, sanitized errors and audit-read/denial events. No global, HTTP write or export API exists.

## M16-03: Implemented access controls

Only `OrganizationPermission.ViewAuditEvents` for active Auditor memberships authorizes audit reads. API and worker runtime roles are separated from the migration role and cannot modify audit history or disable its guards. The authenticated read limiter is 30 requests per minute per user, and authorized audit persistence failures return sanitized 503 while forbidden access remains forbidden. A PostgreSQL superuser or privileged schema owner is outside the database-only tamper boundary.

## M16-04: Executed technical verification

The clean final runner on `e571157` reportedly passed:
- 205 unit, 39 contract and 252 integration tests, with another 252/252 integration repeat.
- 18 web-unit tests and the production web build.
- Three browser engines, 61/61 each.
- 13/13 targeted security mutations killed, with a passing control.
- 74/74 HTTP checks, 14/14 PostgreSQL outage/restart checks, and a separately reported 6/6 final-SHA supplementary probe whose log and JSON have since been **REVIEWED** (6/6 checks; final-SHA provenance from the published report only).
- Dependency and three image scans with no reported relevant vulnerabilities, plus owned-resource teardown and evidence-safety controls.

The first G16 campaign on `ef08d7c91d368f082fc97bac32acab1fdbd5cfca` failed and is preserved. Corrective commits fixed the audit FK lock inversion, cursor drift, thin actor/event metadata, browser races, runtime-role coverage, 503 handling and test scaffolding. The final campaign matrix records 30/30 standalone PASS, with a limited integrated-M9 S30 claim. See the [executed verification record](M16_EXECUTED_VERIFICATION.md).

## Parallel M9/M16 integration

A disposable local merge of M16 `e571157` and M9 `7665bc4` reportedly passed a clean combined build, 228 unit, 39 contract and 273 integration tests after correcting five hard-coded migration-count assertions. It was diagnostic only and never pushed. No combined Compose, browsers or outage verification was run. M9 has moved beyond this tested source. Before integration, establish one worker-role provisioning authority, decide if worker effects need M16 audit coverage, reconcile the EF snapshot and migration count semantics, and run integrated source-locked checks.

## Outstanding acceptance requirements

1. The main 162-file G16 campaign was independently reviewed ([record](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/docs/m9-m16-repowide-reconciliation-20261105/docs/M16_G16_INDEPENDENT_EVIDENCE_REVIEW.md)). The supplemental S07/S08/S28 log and JSON were supplied and **REVIEWED** ([record](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/0bf356dba78e51a6aafdeb665870ffa24789b3d9/docs/M16_G16_SUPPLEMENT_EVIDENCE_REVIEW.md)); preserve both SHA-256 values and account for the source-provenance caveat.
2. Resolve the migration superuser decision and implications of editing the unreleased M16 migration in place.
3. Resolve shared M9 worker-role provisioning and worker-effect audit policy, then test a coordinated combined integration revision.
4. Preserve the documented keyset caveat: a late-committing audit row with an earlier statement timestamp can fall behind an issued cursor; S24's 1024-byte metadata CHECK also lacks a dedicated negative probe.
5. Obtain explicit G16 acceptance and separate authorization before merging. No production deployment or hosted-application clearance is implied.

**Disposition:** The standalone main-campaign evidence was independently checked; supplemental S07/S08/S28 evidence is **REVIEWED** (6/6 consistent checks). Combined M9 verification and G16 acceptance pending.
