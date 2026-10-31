# M2 Implementation Register

**Milestone:** M2 Organizations and role isolation  
**Branch:** `feat/m2-organization-authorization`  
**Baseline:** `a2acd64234049580c21dec49dbdd987b01030a4b` on `main`; G0 and G1 accepted  
**Implementation state:** M2-01 through M2-04 complete on the feature branch. **G2 verified locally on `fbddc1361d79b027275bb53b48133ba0dc7d5cb1`; Acceptance pending. Not merged to `main`.**  
**Verification authorization:** G2 verification is local only: the complete matrix ran on the Windows development machine with every controllable path on E:, against disposable resources. Earlier GitHub runs (the first on draft PR #5) are historical; no workflow run was started after the local-only restriction.  
**Verification:** [Executed record](M2_EXECUTED_VERIFICATION.md): 22 unit, 12 contract, 32 fresh and 32 repeated PostgreSQL integration, 6 frontend and 24 browser tests (Chromium, Firefox, WebKit), plus Compose, M0 and M1 recovery, authenticated organization outage recovery with durable revocation, restricted bootstrap denial, dependency review and clean scans of the three running images. All G2-S01 through G2-S25 scenarios passed.

## Delivered source changes

| Work package | Implemented source paths | Behavior |
| --- | --- | --- |
| M2-01 | `src/HarborOps.Domain/Organizations/OrganizationPolicy.cs`, `src/HarborOps.Infrastructure/Organizations/OrganizationEntities.cs`, `src/HarborOps.Infrastructure/Persistence/HarborOpsDbContext.cs`, migration `20261031020000_OrganizationMemberships.cs`, model snapshot | Typed roles and permission matrix, slug/name validation, additive organization/membership schema, nullable session-selected organization, unique keys and status/version constraints |
| M2-01 | `src/HarborOps.Api/Organizations/OrganizationCli.cs`, `src/HarborOps.Infrastructure/Organizations/OrganizationStore.cs` | Environment-gated local bootstrap for an existing active account and initial organization Administrator, transactionally persisted without public registration |
| M2-02 | `src/HarborOps.Infrastructure/Organizations/OrganizationStore.cs`, `src/HarborOps.Api/Organizations/OrganizationEndpoints.cs` | Live database-selected scope and role authorization, organization profile read/update, bounded member listing/detail, grant/change/soft removal, self-change restriction, version conflict and last-admin protections |
| M2-03 | `src/HarborOps.Api/Organizations/OrganizationEndpoints.cs`, `web/src/auth/organizationClient.ts`, `web/src/auth/OrganizationWorkspace.tsx`, `web/src/auth/AuthPanel.tsx` | Current-user organization list, per-session select/clear, server context read, role-aware React switcher and administrator membership/profile controls |
| M2-04 | `tests/HarborOps.UnitTests/OrganizationPolicyTests.cs`; `tests/HarborOps.IntegrationTests/` `OrganizationIsolationTests`, `OrganizationAdversarialTests`, `OrganizationAdversarialHttpTests`, `OrganizationFinalInvariantsTests`, `OrganizationHttpTests`, `OrganizationHttpLifecycleTests`, `OrganizationUpgradeAndConstraintTests`, `OrganizationBoundaryMatrixTests`, `OrganizationLogRedactionTests`, `MigrationSnapshotTests`; `tests/browser/specs/organizations.spec.mjs`, `organization-revocation.spec.mjs` | Authorization, isolation, concurrency, constraint, upgrade, boundary, redaction and browser cases; **all executed and passing in the local G2 verification** |
| M2-04 operations | `scripts/verify-m2-local.sh`, `scripts/check-m2-scoped-recovery.mjs`, `.github/workflows/verify-m2.yml` | E:-confined local runner for the G2 stages, authenticated outage and recovery check, and the (not executed after the local-only restriction) hosted workflow |
| Operator command hardening | `src/HarborOps.Api/Program.cs` | `--account-cli` and `--organization-cli` report a rejected operation with a sanitized message and exit code 1 instead of aborting the process |

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

After applying the new migration in a controlled deployment and provisioning the Administrator using the already accepted M1 account CLI, the local bootstrap command is:

```powershell
docker compose run --rm -it -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api --organization-cli bootstrap north-terminal "North Terminal" operator@example.test
```

The restricted bootstrap of two synthetic organizations was executed in the local G2 verification. It is gated by `HARBOROPS_ALLOW_ORGANIZATION_CLI=true`; no password is read or printed. Without the opt-in, for an unknown account and for a duplicate slug it exits with code 1 and writes nothing. Normal account provisioning remains a separate M1 operation. Run migration only on an intended disposable test database before applying it to retained data. The existing primary local database and volumes were not touched.

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

These characteristics are exercised by the [executed verification](M2_EXECUTED_VERIFICATION.md). Restricted operator commands (`--account-cli`, `--organization-cli`) report rejected use with a sanitized message and exit code 1 instead of aborting the process. Last-administrator self-disable denial is a typed failure returned as HTTP 409, without changing ordinary M1 disablement for non-administrators.

## Verification completed for G2

The seven verification areas below were executed locally on `fbddc1361d79b027275bb53b48133ba0dc7d5cb1` and all passed. Exact commands, counts and per-scenario evidence are in the [executed record](M2_EXECUTED_VERIFICATION.md).

1. .NET restore, Release build and all M0, M1 and M2 unit, contract and real PostgreSQL suites.
2. EF migration discovery, model snapshot drift check, fresh disposable schema migration, and an M0-to-M1-to-M2 upgrade and repeat rehearsal with retained identity and baseline data.
3. The two-organization role and ownership denial matrix through real HTTP, including guessed foreign organization and member identifiers, suspended and removed memberships, server-selected context, stale versions, same-origin and CSRF checks, malformed input and paging.
4. Concurrent administrator transactions and last-active-administrator protection with independent database contexts.
5. Frontend build and functional browser scenarios on Chromium, Firefox and WebKit, including separate sessions, role changes, selected-organization switching and stale interface recovery.
6. Disposable Compose startup, API restart, database outage and recovery (M0, M1 and M2 scoped), image revision labels, image scans and dependency review.
7. Failures were investigated, corrected and the complete run repeated on the final source revision.

**G2 verified locally; Acceptance pending.** No automatic merge to `main` or progression to M3 is authorized. Historical retained-volume compatibility remains at M6/M12/M18/M24-03, and full dedicated accessibility testing stays at M24-04.