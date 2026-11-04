# M16 Implementation Status

**As of:** 2026-11-04  
**Branch:** `feat/m16-audit-access-hardening`, based on approved M16 documentation at `6c14c78a3d08ad4289b55a13da41883ee8edca7a`.  
**State:** Implementation candidate committed, not locally executed or independently verified.  
**Decision:** M16-D01 through M16-D05 approved. **G16:** Acceptance pending.

## M16-01: Audit schema and privacy

The five decisions, privacy contract, architecture, implementation plan and 30-case G16 matrix are in [M16 policy](M16_POLICY_DECISIONS.md), [privacy](M16_AUDIT_PRIVACY_POLICY.md), [architecture](M16_ARCHITECTURE_DESIGN.md), [implementation plan](M16_IMPLEMENTATION_PLAN.md) and [G16 plan](M16_VERIFICATION_PLAN.md). These policy decisions have been accepted, but implementation proof remains open.

## M16-02: Candidate audit storage and API

- Additive `20261104180000_AuditEntries` PostgreSQL migration creates `audit_entries`, tenant-scoped chronological indexes, typed redacted metadata, UPDATE/DELETE row guards and a separate TRUNCATE statement guard.
- Transactional PostgreSQL audit capture triggers attach audit rows to sensitive identity, organization, reference, maritime, shipment, assignment and integration-source state changes. A failed append is intended to fail the mutation in the same transaction. M1 identity events and M8 durable tracking receipts/history remain separate.
- The EF model and snapshot define the matching audit read entity. Transaction-local actor attribution is installed for authenticated business mutations and restricted operator actions; request middleware supplies a safe correlation ID.
- Read-only GET list/detail routes provide current selected-organization Auditor permission checks, bounded pagination, non-sensitive DTOs, denial-audit attempts and read-audit records. No audit export or HTTP mutation route exists.
- The migration entry point provisions distinct migration, API and worker roles. API uses a restricted `harborops_api` credential in the local Compose configuration. Worker privileges are conditional on presence of M9's migration and must be reviewed with its source before integration.

## M16-03: Candidate security boundary changes

`OrganizationPermission.ViewAuditEvents` is granted only to Auditor. Query and audit-recording paths revalidate live account, session, organization and membership state. Session selection and foreign organization IDs are not authority. Reads use a 120-per-minute limiter per authenticated user, with bounded page size and no audit write/export route. PostgreSQL guards and restricted runtime grants are intended to protect append-only rows from ordinary runtime accounts. Database superusers and table-owning migrators remain outside the database-only immutability guarantee.

## M16-04: Verification assets committed, execution pending

- Unit tests: `tests/HarborOps.UnitTests/AuditPermissionTests.cs`.
- PostgreSQL/API integration: `tests/HarborOps.IntegrationTests/AuditIntegrityTests.cs`, `AuditMigrationTests.cs` and `AuditHttpTests.cs`.
- Existing migration/snapshot expectations reconciled for the new additive M16 migration.
- Browser: `tests/browser/specs/audit.spec.mjs` plus the existing browser regression inventory.
- E:-backed execution: `scripts/verify-m16-local.sh`, `scripts/run-m16-browsers-local.sh`, `compose.g16.verify.yaml`; separate Compose project, ports and disposable storage with preserved raw evidence, SHA manifest and owned-resource cleanup.

**No actual local build, PostgreSQL execution, browser execution, vulnerability scan, failure injection campaign or independent raw-evidence review has been performed in this environment.** The new tests have not been observed passing. The runner's success would establish only its executed stages; all G16-S01 through G16-S30 still need a scenario-by-scenario evidence reconciliation, including any unexercised manual/adversarial requirements. Do not mark G16 PASS from code presence.

## Integration and remaining gate work

1. Execute the clean .NET build and all unit/contract/integration tests on a dedicated E:-backed source-locked environment. Fix defects and rerun before asserting technical completion.
2. Independently validate the PostgreSQL role/trigger matrix, including TRUNCATE, direct SQL, session/role races, denial persistence outages, secret canaries and migration behavior. Capture observable results per G16 scenario.
3. Integrate with the M9 worker branch under a single coordinated EF migration/snapshot and least-privilege grant plan. Reverify M9 effects and M8 immutable history on the combined revision.
4. Exercise all three browser engines and affected M0-M8 regressions. Check local Compose and restored-DB behavior; preserve E:-rooted logs, TRX and checksums.
5. Independently review raw G16 evidence before requesting explicit G16 acceptance. No branch merge, production approval or hosting permission is implied.

The 365-day online retention target is a policy only. Automated archival/purge is not implemented and no deletion authority has been granted. External cryptographic anchoring remains deferred under M16-D05.
