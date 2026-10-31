# M2 Implementation Register

**Milestone:** M2 Organizations and role isolation  
**Branch:** `feat/m2-organization-authorization`  
**Baseline:** `a2acd64234049580c21dec49dbdd987b01030a4b` on `main`; G0 and G1 accepted  
**Implementation state:** Source changes committed on the feature branch. **Not merged into `main`, not verified, G2 open.**  
**Verification authorization:** M2 verification via GitHub was explicitly requested on 2026-10-31. The M2 workflow is configured on draft PR #5; results are not recorded until a completed run is reviewed.  
**Verification:** Requested on draft PR #5. Results pending; G2 open.

## Delivered source changes

| Work package | Implemented source paths | Behavior |
| --- | --- | --- |
| M2-01 | `src/HarborOps.Domain/Organizations/OrganizationPolicy.cs`, `src/HarborOps.Infrastructure/Organizations/OrganizationEntities.cs`, `src/HarborOps.Infrastructure/Persistence/HarborOpsDbContext.cs`, migration `20261031020000_OrganizationMemberships.cs`, model snapshot | Typed roles and permission matrix, slug/name validation, additive organization/membership schema, nullable session-selected organization, unique keys and status/version constraints |
| M2-01 | `src/HarborOps.Api/Organizations/OrganizationCli.cs`, `src/HarborOps.Infrastructure/Organizations/OrganizationStore.cs` | Environment-gated local bootstrap for an existing active account and initial organization Administrator, transactionally persisted without public registration |
| M2-02 | `src/HarborOps.Infrastructure/Organizations/OrganizationStore.cs`, `src/HarborOps.Api/Organizations/OrganizationEndpoints.cs` | Live database-selected scope and role authorization, organization profile read/update, bounded member listing/detail, grant/change/soft removal, self-change restriction, version conflict and last-admin protections |
| M2-03 | `src/HarborOps.Api/Organizations/OrganizationEndpoints.cs`, `web/src/auth/organizationClient.ts`, `web/src/auth/OrganizationWorkspace.tsx`, `web/src/auth/AuthPanel.tsx` | Current-user organization list, per-session select/clear, server context read, role-aware React switcher and administrator membership/profile controls |
| Preparation for M2-04 | `tests/HarborOps.UnitTests/OrganizationPolicyTests.cs`, `tests/HarborOps.IntegrationTests/OrganizationIsolationTests.cs`, `tests/HarborOps.IntegrationTests/MigrationSnapshotTests.cs` | Added source-level authorization and isolation cases and extended migration discovery expectations; **not executed** |

Existing M0/M1 endpoints are left in place. M2 introduces no shipment or maritime entities, no cross-tenant support bypass, no integration tokens, no SSO, no paid dependencies, and no public deployment.

## Implemented HTTP interface

- `GET /api/v1/me/organizations` lists current-user active memberships.
- `GET /api/v1/me/organization-context` returns validated selected-session context or null.
- `PUT /api/v1/auth/session/organization` selects an existing active membership in the current server session.
- `DELETE /api/v1/auth/session/organization` clears that session's selection.
- `GET /api/v1/organizations/{organizationId}` returns selected organization profile.
- `PATCH /api/v1/organizations/{organizationId}` updates selected organization profile with expected version, Administrator only.
- `GET /api/v1/organizations/{organizationId}/memberships` lists selected organization members, Administrator only, bounded UUID cursor.
- `GET /api/v1/organizations/{organizationId}/memberships/{userId}` retrieves only a member of selected organization, Administrator only.
- `PUT /api/v1/organizations/{organizationId}/memberships/{userId}` creates or changes a membership with expected version for an existing row, Administrator only.
- `DELETE /api/v1/organizations/{organizationId}/memberships/{userId}` soft-removes membership with expected version, Administrator only.

Mutations use existing same-origin and antiforgery enforcement. No organization ID, role, or permission is trusted from browser storage or a cookie claim. Login without a membership remains possible, but scoped actions require an active server-side selection and live authorization.

## Operational interface

After applying the new migration in a controlled deployment and provisioning the Administrator using the already accepted M1 account CLI, the proposed local bootstrap command is:

```powershell
docker compose run --rm -it -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api --organization-cli bootstrap north-terminal "North Terminal" operator@example.test
```

This command is recorded as implemented **source**, not a demonstrated working command. It is gated by `HARBOROPS_ALLOW_ORGANIZATION_CLI=true`; no password is read or printed. Normal account provisioning remains a separate M1 operation. Run migration only on an intended disposable test database before applying it to retained data. The existing primary local database and volumes have not been touched here.

## Security and consistency provisions in source

- Fixed role-to-permission mapping with default denial for membership administration.
- One role per active organization membership; roles are not embedded in M1 session cookies.
- Selected organization is persisted on `auth_sessions`, independently per browser session.
- Request-path organization IDs are checked against current selected organization before member/object reads.
- Sensitive organization/member writes lock organization rows and revalidate an active Administrator inside the write transaction.
- Last active Administrator and administrator self-change protection, including the existing M1 self-disable action, optimistic version conflicts, soft removal and selected-session invalidation.
- No cross-organization membership listing or global business-data read grant.
- Scoped HTTP errors and request correlation, without passwords or raw session cookies in responses.
- Browser context reload and protected state clearing on changes/denials.

These are implementation characteristics, **not verified security claims**. No concurrency, authorization bypass, API contract, browser or migration test was executed in this handoff. Last-administrator self-disable denial is represented by a new typed failure and HTTP 409 response, without changing ordinary M1 disablement for non-administrators.

## Verification still required for G2

All scenarios in [G2 verification plan](M2_VERIFICATION_PLAN.md) remain unrun. Required work includes:

1. .NET restore, release build and all existing M0/M1 unit/contract/real PostgreSQL suites, plus new M2 source tests.
2. EF migration discovery, model snapshot drift check, fresh disposable schema migration, and an M1-seeded-to-M2 upgrade/repeat rehearsal with retained identity and baseline data.
3. Complete two-organization role/ownership denial matrix through real HTTP, including guessed foreign organization/member identifiers, suspended/removed memberships, server-selected context, stale versions, same-origin and CSRF.
4. Concurrent administrator transaction and last-active-admin tests with two independent database contexts.
5. Local frontend build and functional browser scenarios, including multiple sessions, role changes, selected-organization switch and stale UI recovery.
6. Safe disposable Compose startup, API restart and database outage/recovery tests, local image and dependency review, and final documentation/link review.
7. Remediate any observed failures and record exact source SHA, commands, fixture, environment and PASS/FAIL/BLOCKED/NOT RUN evidence.

**No verification was run.** G2 is neither verified nor accepted. No automatic merge to `main` or progression to M3 is authorized by this implementation commit. Historical retained-volume compatibility remains at M6/M12/M18/M24-03, and full dedicated accessibility testing stays at M24-04.
