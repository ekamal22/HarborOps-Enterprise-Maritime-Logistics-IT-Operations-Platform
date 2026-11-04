# M16 Audit Logging and Access Hardening Architecture

**Status:** Approved design and M16-D01 to M16-D05, 2026-11-04. No implementation, tests, deployment or G16 acceptance is implied. **Entry:** G3 accepted.

## Baseline
The .NET 10 modular API uses EF Core 10 and PostgreSQL 16, server-authoritative sessions and typed organization permissions. M1 auth_events provide limited identity events, not comprehensive audit. M8 receipt/accepted-event tables are immutable operational history, not general audit. Do not replace either or alter accepted M8 source keys. M9 introduces worker claim/retry/effect tracking and must coordinate schema and privileges.

## Proposed audit schema
Add `audit_entries` with immutable `id uuid`, nullable `organization_id` for platform identity actions only, `event_type`, `event_schema_version`, `outcome`, `actor_kind`, nullable actor user/session/integration source IDs, `actor_role`, `resource_type` and `resource_id`, `correlation_id`, server `occurred_at_utc`, and size-bounded allowlisted `metadata jsonb`. The tenant identifier is a historical, immutable audit scope and intentionally does not use a live foreign key to organizations: such a key would acquire a conflicting FOR KEY SHARE lock during audited writes. Parent tenant identity is enforced on the source business tables and by the restricted audit append path. Index tenant scope and `(occurred_at_utc DESC,id DESC)`; use organization-scoped `(occurred_at_utc,id)` keyset cursors, not offsets, because audit reads themselves append records. Do not derive authorization from audit metadata.

## Transaction and integrity
Use typed internal AuditWriter and tenant-scoped AuditReader. Sensitive mutations and their required audit insert use the same transaction and lock ordering. Audit insert failures roll back the business mutation; never return success on unknown commit outcome. Denial-audit writes run separately and cannot authorize an already denied request. Enforce UPDATE/DELETE immutability with database permissions and guards, and enforce TRUNCATE using revoked privilege and/or a separate statement-level trigger. Runtime DB roles must neither own the audit table nor disable triggers or run DDL. The database-only threat boundary excludes PostgreSQL superusers or privileged schema owners. Do not claim cryptographic anchoring.

## Permissions and read API
Add a typed `ViewAuditEvents` permission only for active Auditor membership in the session-selected organization. Preserve all other roles' existing business permissions. Planned read-only routes:
- `GET /api/v1/organizations/{organizationId}/audit-events`: tenant-scoped stable ordering, default 25 and maximum 100 rows per page, opaque nextCursor for subsequent pages, allowlisted filters/DTOs. Numeric page > 1 is rejected to prevent offset drift.
- `GET /api/v1/organizations/{organizationId}/audit-events/{auditId}`: scoped detail, foreign resource invisibility.

No create/update/delete/export API or global audit browser is in scope. Audit access itself is a security event. Preserve live membership revalidation, CSRF/Origin and reverse-proxy trust controls where applicable, sanitized errors and bounded input/query costs.

## Database identity split
Separate migration, API and worker credentials using least-privilege grants. The current Compose shared account requires safe bootstrap and compatible migration planning. Rehearse on fresh/accepted-M8 and disposable historical data. M9 worker writes only its intended work/effect tables, not arbitrary audit updates. Coordinate shared EF model snapshot, dependency injection, security policy and migration ordering with all parallel branches.

## Parallel and deferred scope
M12 export is separately permissioned and cannot export audit logs implicitly. M14 map and M17 telemetry may correlate only safe identifiers. M19 backup/restore must cover audit constraints. M18 incident CRUD depends on G16 under the existing graph. External cryptographic anchoring, global audit search, automatic purge and hosted certification are deferred. See [approved policy](M16_POLICY_DECISIONS.md), [privacy](M16_AUDIT_PRIVACY_POLICY.md), [work plan](M16_IMPLEMENTATION_PLAN.md) and [G16 tests](M16_VERIFICATION_PLAN.md).
