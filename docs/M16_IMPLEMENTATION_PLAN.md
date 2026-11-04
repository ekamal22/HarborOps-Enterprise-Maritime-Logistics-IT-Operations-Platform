# M16 Implementation Work Packages

**State:** Architecture/policy approved and code candidate committed on `feat/m16-audit-access-hardening`; local G16 execution, integration and independent review pending. See [M16 implementation status](M16_IMPLEMENTATION_STATUS.md). **Dependency:** G3 accepted. **Gate:** G16 acceptance pending.

## M16-01: Audit schema and privacy
Freeze audit event taxonomy, roles, nullable actor references, metadata allowlists, retention/legal holds, read DTOs, schema keys/indexes and direct PostgreSQL privilege model. Preserve M1 auth_events and M8 permanent accepted history. Publish the [decision record](M16_POLICY_DECISIONS.md), [privacy policy](M16_AUDIT_PRIVACY_POLICY.md) and [architecture](M16_ARCHITECTURE_DESIGN.md). Review M9 branch migration and runtime-role plans before coding. Proposed policy source: `src/HarborOps.Domain/Audit/AuditEventPolicy.cs`.

## M16-02: Append-only writer, reader and endpoints
Proposed new files: `src/HarborOps.Infrastructure/Audit/AuditEntry.cs`, `AuditWriter.cs`, `AuditReader.cs`, `src/HarborOps.Api/Audit/AuditEndpoints.cs`. Add an additive migration under `src/HarborOps.Infrastructure/Persistence/Migrations/`, update `HarborOpsDbContext.cs` and `HarborOpsDbContextModelSnapshot.cs`, and register services/routes in `src/HarborOps.Api/Program.cs`. Integrate required same-transaction audit writes in IdentityStore, OrganizationStore, ReferenceStore, MaritimeStore, ShipmentStore, AssignmentStore and IntegrationSourceStore as appropriate. Build separate migration/API/worker DB grants and migration bootstrap, including explicit TRUNCATE protection. Implement GET list/detail only with bounded paging and Auditor permission.

## M16-03: Permission and security hardening
Extend `src/HarborOps.Domain/Organizations/OrganizationPolicy.cs` with Auditor-only `ViewAuditEvents`; review `OrganizationStore.cs`, `AuthenticationSetup.cs` and affected endpoints for live session/tenant checks, post-lock revocation, BOLA/BFLA and foreign UUID concealment. Keep session CSRF/Origin/transport controls. Bound metadata/query/denial volume and redact logs. Preserve existing Administrator/Support non-entitlement and M8 atomic receipt semantics.

## M16-04: Source-locked local verification
Add unit, contract, PostgreSQL integration and three-engine browser tests in existing test projects. Rehearse fresh and M8-to-M16 migration and safe historical copy, raw SQL immutable guards, privilege separation, audit/business rollback and commit uncertainty, multi-tenant role matrix, privacy canaries, outage/recovery and affected M0-M8 plus integrated M9 regression. Plan local `scripts/verify-m16-local.sh` and isolated `compose.g16.verify.yaml` using E:-backed evidence. Preserve test commands, environment, exact SHA, SQL witnesses, TRX, Playwright, sanitizer outputs and manifest hashes for independent review.

## Concurrency and merge discipline
Use an isolated M16 feature branch. Coordinate shared `HarborOpsDbContext`, snapshot, migration numbering, OrganizationPolicy, Program, DI and Compose grants with M9; no independent conflicting migration histories. M12 export does not confer audit privileges. M14/M17/M19 integrations must use safe contracts. G16 requires [all 30 scenarios](M16_VERIFICATION_PLAN.md), independent review and separate explicit acceptance before merging or declaring completion. GitHub Actions remains disabled; verification is local only.
