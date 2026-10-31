# M2 Organizations and Role Isolation: Architecture Design

**Milestone:** M2, packages M2-01 through M2-04; exit gate G2  
**State:** Detailed implementation proposal, not yet implemented or accepted  
**Baseline:** M0/G0 and M1/G1 accepted on 2026-10-31. M1 is in `main` at the accepted merge lineage.  
**Requirements:** FR-002, FR-001 integration, NFR-002, NFR-003, NFR-006, NFR-007 and NFR-008  
**Related:** [Roadmap](ROADMAP.md), [Requirements](REQUIREMENTS.md), [Domain Model](DOMAIN_MODEL.md), [API Contracts](API_CONTRACTS.md), [Security](SECURITY.md), [M2 implementation plan](M2_IMPLEMENTATION_PLAN.md), [G2 verification plan](M2_VERIFICATION_PLAN.md)

## 1. Outcome and scope boundary

Deliver authenticated membership in multiple organizations, explicit organization selection, fixed role-based grants, scoped membership administration, and denied cross-organization reads and writes through the actual API and PostgreSQL database. No organization may gain access to another organization's memberships or data because a caller knows a UUID, edits a route parameter or changes a browser request.

M2 owns identity-to-organization authorization, not shipment entities. M3 introduces port/vessel reference records, M4 voyage/container records, M5 shipments and M7 integration-source principals. No public signup, invitation/email delivery, SSO, global support-data bypass, analytics/export endpoints, or dynamic role designer is included.

## 2. Accepted foundation and integration points

Current `main` contains:
- `src/HarborOps.Api/Authentication/AuthenticationSetup.cs`: protected cookie authentication and per-request database validation.
- `src/HarborOps.Api/Authentication/AuthenticationEndpoints.cs`: session information and CSRF-protected mutations.
- `src/HarborOps.Infrastructure/Identity/IdentityStore.cs`: `CurrentSession` with user ID, session ID and expiry; login, revocation and status checks.
- `src/HarborOps.Infrastructure/Persistence/HarborOpsDbContext.cs`: M0 and M1 entity mappings.
- `web/src/auth/AuthPanel.tsx`: M1 login/session interface on same-origin `/api`.
- `src/HarborOps.Api/Program.cs`: authentication/authorization middleware, correlation IDs and sanitized errors.

Keep the M1 cookie ticket limited to authenticated user/session/security version. Do not add trusted roles or a selected-organization grant to the cookie. The database remains authoritative for both authentication and authorization. Keep the public synthetic M0 baseline and health routes unchanged.

## 3. Organization and membership model

### 3.1 Organizations

`organizations` is additive, with:
- `id uuid` primary key, application-generated and immutable.
- `slug varchar(80)`, normalized to ASCII lowercase `[a-z0-9]` and internal hyphens, globally unique. Treat slug as a locator, never as proof of access.
- `display_name varchar(160)`, trimmed and nonblank.
- `status varchar(16)`, exactly `Active` or `Suspended`.
- `version bigint` initially 1, strictly positive, incremented on update.
- `created_at_utc timestamptz`, `updated_at_utc timestamptz` (UTC).

Organizations are suspended rather than hard-deleted in M2. Suspension immediately prevents organization-scoped access. No slug recycling or cross-organization migration in M2.

### 3.2 Memberships

`organization_memberships` is additive, with:
- `id uuid` primary key and unique `(organization_id, user_id)`.
- `organization_id uuid NOT NULL`, FK to `organizations(id)`, restrictive delete.
- `user_id uuid NOT NULL`, FK to existing `users(id)`, restrictive delete.
- `role varchar(24) NOT NULL`: `Analyst`, `Manager`, `Support`, `Administrator` or `Auditor`.
- `status varchar(16) NOT NULL`: `Active`, `Suspended` or `Removed`. Removal is a soft state, retaining the row for historical referential integrity. Reinstatement is explicit.
- `version bigint` initially 1, strictly positive, incremented on any role/state edit.
- `created_at_utc`, `updated_at_utc` UTC, `granted_by_user_id uuid NULL` for CLI bootstrap versus authenticated change.

Indexes: unique `(organization_id,user_id)`; `(user_id,status,organization_id)` for membership listing; `(organization_id,status,role)` for admin counting. Enforce allowed role/state strings, versions, nonblank slug, and UTC columns with check constraints plus application validation. Normalize slug once in an explicit domain helper, not by locale-dependent database collation. A member has exactly one effective role per organization in M2 and can have different roles in other organizations.

### 3.3 Session selection

Add nullable `selected_organization_id uuid` to existing `auth_sessions`. It references `organizations(id)` using `ON DELETE RESTRICT`; old M1 sessions continue to deserialize and have no selected organization. Selection belongs to a server session, not the user record, cookie contents, browser local storage, or a client-supplied role. Each browser session may select a different organization.

Selection must be checked against the current active user, active organization, active membership and authenticated session. A null selection blocks organization-scoped operations without breaking login or existing M1 session routes. Every request revalidates the membership from PostgreSQL. A stale selection has no privilege and must be rejected. On membership removal/suspension, proactively clear matching selections for that user, without invalidating unrelated organization sessions or the whole login. This cleanup is a user-experience aid; revocation safety relies on live authorization, not only cleanup.

### 3.4 Administrative invariants

- Only an active `Administrator` membership in the **same selected active organization** may manage that organization's memberships and editable profile.
- A caller cannot change their own role or suspend/remove their own administrator membership through the admin API. This is a conservative M2 policy.
- The last active Administrator cannot be suspended, removed or demoted, including through concurrent requests. Serialize admin mutations on the organization row and check the current active-admin set inside the same transaction before update.
- No membership is granted merely because an email matches a domain, the user owns another organization, or the role is `Support`.
- Only existing active provisioned user IDs may receive a membership. Never create a user, account or credential as a side effect of a membership API.
- Enforce unique membership in the database; duplicate provisioning cannot silently escalate or overwrite a role.
- Organization creation and the first Administrator membership are one restricted, atomic local CLI operation. Do not add public organization registration in M2.

### 3.5 Migration and rollback

Add one incremental EF Core migration after `20261031010000_IdentitySessions` that creates the two new tables and nullable session selection. Update `HarborOpsDbContext` and its model snapshot together. Do not edit or rewrite applied M0/M1 migrations. Repeated migration must be idempotent, and an M1-seeded database must retain `users`, `user_credentials`, `auth_sessions`, `auth_events` and `bootstrap_records` unchanged except the additive nullable column. No real organization or membership belongs in the normal M0 baseline seeder. Test fixtures and CLI bootstrap use disposable databases.

Schema downgrade is not a production rollback mechanism. Prefer forward correction; a destructive `Down` is test-only and requires an explicit backup/recovery decision for retained datasets.

## 4. Authorization architecture

Define `OrganizationPermission` as a closed, typed list of operations. The role-to-permission mapping is in code, versioned and unit-tested, not in HTTP input and not expressed as arbitrary client claims.

| Permission | Analyst | Manager | Support | Administrator | Auditor |
| --- | --- | --- | --- | --- | --- |
| `organization.view` | Yes | Yes | Yes | Yes | Yes |
| `membership.view` | No | No | No | Yes | No |
| `membership.manage` | No | No | No | Yes | No |
| `organization.profile.update` | No | No | No | Yes | No |
| Future business record reads | Scoped by later resource policy | Scoped by later resource policy | No by default | No by default | Only explicit scoped grant |
| Future business writes | Scoped by later resource policy | Scoped by later resource policy | No | No by default | No |
| Future telemetry/incident operations | No by default | Limited by later policy | Scoped later | Scoped later | Explicit grant later |

The final three rows are forward constraints, **not** permissions implemented or granted by M2. There is no implicit global `Administrator` or `Support` override and no cumulative inheritance that would grant more capabilities than this table. Future shipment, dashboard, audit, export and incident policies must separately map each operation, default deny, and prove isolation before acceptance in their respective milestones.

Implement an `IOrganizationAuthorizationService` in Application backed by an Infrastructure EF Core resolver. API endpoint filters or explicit minimal-API authorization guards pass the actual `CurrentSession` established by M1, resolve its database-selected organization and active membership, then require a typed permission. All business services introduced in future milestones will accept a verified `AuthorizedOrganizationContext` rather than an untrusted organization ID. Do not expose a general DbContext query to a handler that skips authorization.

Every organization-owned read/write query must include a server-side ownership predicate. For member detail, require `membership.organization_id == context.OrganizationId` and the authorized role. A route UUID, member UUID or JSON organization ID is only a requested locator. Verify equality with the selected, authorized organization before any query, mutation or response. The data layer must guard ownership independently of frontend navigation.

Use freshly queried membership state per request. No membership/role claims in cookies, cross-request cached authorization decisions or client-controlled `X-Organization-ID` trust. A stale active membership snapshot must not authorize another operation after role/suspension changes. For sensitive writes, lock/validate the relevant organization and membership rows in the same transaction used for the write, and recheck authorization after locking to eliminate check-then-mutate races.

## 5. Selection contract and HTTP outcomes

Selection is a deliberate, CSRF-protected mutation using the current M1 cookie and antiforgery token. It updates only the active server session:

`PUT /api/v1/auth/session/organization` with `{"organizationId":"<uuid>"}`.

A separate `DELETE /api/v1/auth/session/organization` clears the selection. Neither route changes the cookie or membership role. Selection never creates a membership. A successful selection responds with an allowlisted summary including selected ID, name, role, and `membershipVersion`; cache control is `no-store`.

Read operations:
- `GET /api/v1/me/organizations`: enumerate only the current user's active memberships in active organizations, sorted deterministically. Each item contains ID, slug, displayName, role and membership version.
- `GET /api/v1/me/organization-context`: current session selection (or null), revalidated; no access to a formerly permitted organization after suspension/removal.
- `GET /api/v1/organizations/{organizationId}`: permitted profile fields, requires active selection and `organization.view`.
- `GET /api/v1/organizations/{organizationId}/memberships`: selected organization's roster, Administrator only, bounded page size.
- `GET /api/v1/organizations/{organizationId}/memberships/{userId}`: selected organization's member only, Administrator only. Foreign user IDs remain unresolvable even if they exist in another organization.

Mutation operations:
- `PATCH /api/v1/organizations/{organizationId}`: Administrator only; allowed `displayName`, expected `version`.
- `PUT /api/v1/organizations/{organizationId}/memberships/{userId}`: Administrator only; explicit create or update of `role` and `status`, with version precondition for an existing row. Creation must be explicit and fail on duplicate.
- `DELETE /api/v1/organizations/{organizationId}/memberships/{userId}`: soft-remove, Administrator only, requires version precondition. No physical deletion.
- Restricted `--organization-cli bootstrap SLUG DISPLAY_NAME ADMIN_EMAIL`: creates an organization and active Administrator membership for a previously provisioned active account, in a single transaction. CLI must be separately environment-gated, use no password arguments and refuse duplicate/ambiguous identities.

All mutating HTTP endpoints use the existing M1 same-origin validation and CSRF token, a bounded JSON body, and no-store responses. Use existing correlation/error envelopes without sensitive identifiers. Do not retrofit M1 login with a required organization on sign-in: zero-membership users can log in but cannot access scoped resources.

| Situation | Expected response |
| --- | --- |
| No valid M1 cookie | 401 `authentication_required` |
| Authenticated without an active selection | 409 `organization_selection_required` on scoped routes |
| Requested org or member belongs to another organization | 404 `resource_not_found`, regardless of guessed ID validity |
| Active member lacking the required operation | 403 `access_denied` |
| Suspended organization or membership, or selection invalidated | 403 `organization_access_revoked`; do not return the previous organization's data |
| Invalid UUID/body/role/state/version | 400 `invalid_request` |
| Stale version or last-admin invariant conflict | 409 `conflict` |
| Duplicate membership or slug | 409 `conflict` |
| Missing/forged CSRF or invalid origin on mutations | 403 `csrf_invalid` |
| PostgreSQL unavailable or validation cannot complete | 503 `dependency_unavailable`, never fallback access |

Resolve existence-hiding checks before returning 403, avoid database provider messages in responses, and apply uniform cross-tenant denial for both list and detail endpoints. No response may return another organization's membership count, email list or status.

## 6. Concurrent changes and selection semantics

A role update takes effect on the next protected request, including a previously opened browser tab. Session A and session B may have different selected organizations for the same user. Changing A must not silently alter B; suspending a membership invalidates that org selection for all sessions of the affected user.

Admin role changes and self/last-admin protection use a transaction with a row lock on `organizations`, followed by a fresh membership read and conditional `version` update. Competing requests must produce one commit and one conflict or a valid serialized result, never leave an organization with zero active administrators.

For profile/membership writes, do not trust an authorization query completed before a concurrent role revocation. Enforce a consistent lock order: organization row, acting membership, target membership. Reads can perform a fresh authorized query without returning unauthorized data. Avoid retry loops that hide stale permission checks.

## 7. Frontend integration

Extend the existing M1 authenticated `AuthPanel` with a scoped `OrganizationSwitcher` or extract a small authenticated workspace component:
1. Load `/api/v1/me/organizations` and `/api/v1/me/organization-context` after login.
2. Show the list of memberships; display a deliberate empty state when none exist.
3. Switch with CSRF-protected `PUT /api/v1/auth/session/organization`, never by changing local storage alone.
4. Refresh authorized context after each switch, role change, logout, browser reload, unauthorized/forbidden response or membership revocation.
5. Expose a bounded organization profile; only Administrators see member roster/edit controls. These controls are a usability affordance, never an authorization decision.
6. Distinguish unauthenticated (401), no selection (409), unauthorized (403), missing/foreign record (404), stale edit (409) and temporary database outage (503). Do not reuse stale organization-specific state after a failed switch.
7. Preserve existing M0 public baseline and M1 login/session behavior. No new dashboards, shipments or maritime records in M2.

Keep the same-origin `/api` proxy, use `credentials: "same-origin"`, and do not store JWTs, role grants or trusted selected-org identifiers in local storage. Any in-memory selected-org state is display state only, and is refreshed from the server.

## 8. Threat analysis and required negative cases

- BOLA/IDOR: swap valid A and B organization IDs, member IDs and selected-org mismatch across all endpoints and HTTP verbs.
- Broken function-level authorization: Analyst, Manager, Support and Auditor may view allowed organization summary but may not read or mutate membership roster/profile.
- Forged context: sending a foreign organization ID in path, body or extra header never changes selection or grants permission.
- Revocation: a suspended/removed membership or suspended organization prevents the next scoped access without needing logout.
- Last-admin race: two concurrent demotions or removals cannot remove the final administrator.
- Multi-session: selections are session-specific, role changes propagate, revocation works even if another tab has an old UI state.
- Outage: missing database or revoked M1 cookie causes safe failure; no role or tenant context is trusted from the cookie.
- Enumeration: unknown, foreign, and disabled members are not exposed via unauthorized list/detail results.
- Tampering: malformed UUIDs, malicious role strings, stale versions, duplicate member adds, missing CSRF and foreign origins fail.
- Observability: log correlation, action/outcome and opaque IDs only as permitted; no passwords, cookies, CSRF tokens or full unauthorized membership lists.

## 9. Explicit non-goals, operational constraints and acceptance

G2 does not require tenant-owned shipment data, a dynamic RBAC editor, external login, invitation messages, a production deployment, old Docker volume compatibility, or dedicated accessibility auditing. The last two remain at M6/M12/M18/M24-03 and M24-04 respectively, with M19 owning full restore verification. The environment must remain zero-cost and local-first; controlled persistent data, caches and evidence must remain on E: under [Local Storage Policy](LOCAL_STORAGE_POLICY.md).

Do not use GitHub Actions for M2 planning, implementation or verification until the restriction is explicitly lifted. Do not create or edit workflow files, dispatch workflows or rely on remote CI status. G2 can be verified later with explicitly run local build, PostgreSQL and browser checks against isolated disposable databases. Every result must name the exact source revision, fixture and observed outcome. This design document does not mark M2 code delivered, G2 verified or G2 accepted.
