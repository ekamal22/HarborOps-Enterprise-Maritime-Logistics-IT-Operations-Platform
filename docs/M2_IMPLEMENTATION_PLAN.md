# M2 Organizations and Role Isolation: Implementation Plan

**State:** M2-01 through M2-03 source committed and initial GitHub verification passed. Full M2-04 adversarial matrix incomplete; G2 open.  
**Entry:** G1 verified and accepted on 2026-10-31, with M1 integrated into `main`.  
**Scope authority:** [M2 architecture](M2_ARCHITECTURE_DESIGN.md), [Roadmap](ROADMAP.md), [execution graph](EXECUTION_GRAPH.md), [Requirements](REQUIREMENTS.md), [G2 verification](M2_VERIFICATION_PLAN.md).

## Delivery rules

Implement only M2-01 to M2-04. Do not modify M0/M1 applied migration history, expand to shipments/reference entities, run against the primary local PostgreSQL database or use the only retained Docker volume. GitHub Actions verification was explicitly authorized for M2 on 2026-10-31. [Run #41157387340](M2_EXECUTED_VERIFICATION.md) passed on disposable hosted runner resources. Further G2 verification must remain isolated and avoid retained workstation volumes.

This plan is the implementation contract. [M2 source register](M2_IMPLEMENTATION_STATUS.md) records the committed implementation paths. None of the source paths has been validated by executed runtime tests; documentation approval is separate from verification.

## M2-01: Model memberships and role grants

**Dependencies:** G1 accepted.  
**Deliverables:** Additive schema, typed roles and operations, restricted organization bootstrap and consistent migration snapshot.

### Files and modules

Proposed source paths:
- `src/HarborOps.Domain/Organizations/Organization.cs`, `OrganizationMembership.cs`, `OrganizationRole.cs`, `OrganizationPermission.cs`, `RolePermissions.cs`.
- `src/HarborOps.Application/Organizations/IOrganizationAuthorizationService.cs`, `OrganizationContracts.cs`.
- `src/HarborOps.Infrastructure/Organizations/OrganizationStore.cs`, `OrganizationBootstrapService.cs`.
- `src/HarborOps.Infrastructure/Persistence/HarborOpsDbContext.cs` and a new additive `20261031xxxxxx_OrganizationMemberships.cs` migration; update `HarborOpsDbContextModelSnapshot.cs`.
- `src/HarborOps.Api/Authentication/AccountCli.cs` or dedicated `OrganizationCli.cs` for separate restricted local bootstrap.

### Execution sequence

1. Introduce closed `OrganizationRole` and `OrganizationPermission` enums and the explicit matrix from the design. Keep these in Domain/Application and independent of EF and ASP.NET.
2. Implement organization slug validation, state invariants, membership state transitions and monotonic version checks as pure domain logic.
3. Map `organizations` and `organization_memberships` with unique indexes, FKs, checks, UTC timestamps and no business-data entity references. Add nullable session-selected organization FK. Preserve M0/M1 schema and previous migrations.
4. Extend infrastructure DI for organization services. Avoid singleton DbContext or per-request static authorization cache.
5. Add a separately environment-gated organization bootstrap command. Resolve a pre-existing active user by normalized email, create the organization and Administrator membership atomically, return only a safe success summary. Reject duplicate slug or unknown/disabled admin.
6. Provide deterministic two-organization test setup through isolated test fixtures only. No tenant bootstrap in the public M0 baseline seeder.
7. Add a snapshot-drift test and a clean-M0-to-M1-to-M2 migration rehearsal; verify no pending EF model changes and repeatable upgrade.

**M2-01 acceptance checks:** role/slug validation; unique membership; allowed role/status constraints; foreign-key enforcement; last-admin prerequisite; preservation of M1 sessions, credentials, auth events and baseline row; negative CLI input; no implicit account registration.

## M2-02: Enforce permission boundary

**Dependencies:** M2-01.  
**Deliverables:** Reusable deny-by-default authorization resolver, selected-organization ownership guard and protected organization/member API.

### Files and modules

- `src/HarborOps.Application/Organizations/AuthorizedOrganizationContext.cs`, `OrganizationAuthorizationService.cs` or interface.
- `src/HarborOps.Infrastructure/Organizations/EfOrganizationAuthorizationService.cs`, `OrganizationMembershipStore.cs`.
- `src/HarborOps.Api/Authorization/OrganizationAuthorizationFilter.cs`, `OrganizationApiErrors.cs`.
- `src/HarborOps.Api/Organizations/OrganizationEndpoints.cs`, `MembershipEndpoints.cs`.
- `src/HarborOps.Api/Program.cs`, `src/HarborOps.Infrastructure/DependencyInjection.cs` for registration.

### Execution sequence

1. Require an M1-authenticated `CurrentSession`; derive user/session IDs from the validated session object, not request parameters or role claims.
2. Resolve the current session's selected organization, active organization state, active membership and typed permission fresh from PostgreSQL for every scoped endpoint.
3. Make authorization return an `AuthorizedOrganizationContext` object that is impossible to instantiate from untrusted route input in endpoint handlers. Use the context's authorized ID for EF query ownership predicates.
4. Implement organization detail, editable profile, membership list/detail and create/update/soft-remove endpoints exactly as defined in [M2 architecture](M2_ARCHITECTURE_DESIGN.md).
5. For membership edits, lock organization and relevant membership rows, recheck acting Admin entitlement within the write transaction, enforce self-change and last-admin safety, and atomically update versions/selection cleanup. Do not suppress concurrent 409 conflicts.
6. Keep DTOs narrow: never return credential hashes, security version, session identifiers, secrets, target membership records from foreign organizations or global user listings.
7. Reuse M1 `ValidMutationAsync` for all state changes; ensure common correlation IDs and sanitized 401/403/404/409/503 JSON responses. No 302 redirect from API endpoints.
8. Add unit and API tests that attempt foreign organization/member IDs with all five roles, and concurrent admin edits.

**M2-02 acceptance checks:** wrong-role read/write denials, BOLA/IDOR prevention, missing selection, invalid tenant, suspended membership/org, stale version, last-admin protection, CSRF/Origin, database outage failure, and query-level organization predicates.

## M2-03: Implement organization selection

**Dependencies:** M2-01. May proceed alongside M2-02 after schema contracts are agreed.  
**Deliverables:** Session-based selected organization, authenticated context routes, a browser organization switcher and correct denied/empty states.

### Files and modules

- `src/HarborOps.Infrastructure/Identity/IdentityStore.cs` / scoped organization session service: read or atomically set nullable `auth_sessions.selected_organization_id`.
- `src/HarborOps.Api/Authentication/AuthenticationEndpoints.cs`: add select/clear endpoints without changing M1 login contracts.
- `src/HarborOps.Api/Organizations/MyOrganizationEndpoints.cs`: membership list and server-selected context.
- `web/src/auth/OrganizationSwitcher.tsx`, `web/src/auth/organizationClient.ts`, `web/src/auth/AuthPanel.tsx`, `web/src/App.tsx`.

### Execution sequence

1. Expose only the current user's active organization memberships through `GET /api/v1/me/organizations`.
2. Implement CSRF-protected `PUT/DELETE /api/v1/auth/session/organization` that checks live membership/organization state and updates the current session only.
3. Return active selection through `GET /api/v1/me/organization-context`. Clear or reject obsolete selection after revocation; preserve the logged-in user and other organization memberships.
4. Implement a browser list and selected-org display. No membership presents an explanation and no privileged action. Changing organization requests server selection and refetches the authorized context.
5. Do not derive a grant from React state, a route ID, local storage, a query parameter or a browser-provided role.
6. Handle stale tab, session expiry, suspended membership, no selection, wrong role, invalid ID and PostgreSQL outage distinctly without showing previously loaded foreign data.
7. Preserve M1 login, logout-all, password reset, disable-self and cookie restart behavior.

**M2-03 acceptance checks:** multiple memberships, differing per-org roles, two independent browser sessions, selection changes across reload, role revocation while a tab is open, empty membership, disabled org and unknown org.

## M2-04: Verify tenant isolation and prepare G2 evidence

**Dependencies:** M2-02 and M2-03.  
**Deliverables:** Complete local G2 matrix, failure analysis, corrected defects, documentation reconciliation and an explicit acceptance-pending verification record only if every required check passes.

1. Use real PostgreSQL and real HTTP requests. Include at least two organizations, at least five distinct role memberships, a user in both organizations with different roles, a user with no memberships, one disabled member, one suspended organization and two Administrators for last-admin race tests.
2. Run unit policy/state tests, contract/HTTP tests and both fresh and M1-seeded database integration suites. Run the tests again on the already migrated disposable database.
3. Execute all requested organization and member endpoints as authorized, cross-tenant, wrong-role, logged-out, revoked, forged header, forged route and malformed requests.
4. Check list/detail not-found behavior for valid foreign IDs, selected-org mismatch, stale role after concurrent change and two simultaneous admin mutations. Ensure no unauthorized response leaks a count, email or slug.
5. Exercise browser sign-in, organization selection, reload, different-role organization switch, forbidden roster action, logout and membership-revocation recovery. Chromium, Firefox and WebKit are included if installed locally; a missing required engine is BLOCKED, not PASS.
6. Recheck M0 synthetic baseline and M1 login, cookies, CSRF, expiry, logout, disabled account and PostgreSQL outage handling against a disposable local Compose project. Test a restarted API with persistent authentication keys, not a retained personal volume.
7. Perform a targeted dependency and image review with locally available tooling, record actual results and limitations. Do not silently substitute unverified statements for executed scans.
8. Write `docs/M2_IMPLEMENTATION_VERIFICATION.md` only with actual commands, source SHA, environment, fixture identifiers, case counts, PASS/FAIL/BLOCKED/NOT RUN results and residual risks. Advance G2 to `verified, acceptance pending` only after complete evidence.
9. Update [Status](STATUS.md), [Roadmap](ROADMAP.md), [Requirements](REQUIREMENTS.md), [API Contracts](API_CONTRACTS.md), [Domain Model](DOMAIN_MODEL.md), [Security](SECURITY.md), [Operations](OPERATIONS.md) and [Acceptance Criteria](ACCEPTANCE_CRITERIA.md) to reflect actual implemented behavior rather than the proposal.

Do not accept G2, merge implementation automatically or begin M3 solely because tests pass. Explicit acceptance remains a separate decision.

## Planned implementation slices and integration order

| Slice | Prerequisites | Narrow output | Local completion test |
| --- | --- | --- | --- |
| S1: Domain contracts | G1 | Roles, operations, invariants and authorization context interface | Unit policy matrix |
| S2: Schema and bootstrap | S1 | M2 migration, model snapshot, local CLI | Fresh/seeded DB integration |
| S3: Authorization gateway | S2 | Live role/organization resolver and ownership guards | API negative matrix |
| S4: Admin endpoints | S3 | Organization and membership guarded CRUD/soft-remove | BOLA, last-admin, concurrent writes |
| S5: Session selection | S2 + S3 | Server-selected org, M1 session integration | Multi-session and revoked selection |
| S6: React context | S4 + S5 | Switcher, profile, guarded member controls | Browser functional tests |
| S7: G2 evidence | All prior | Full local regression and documented findings | G2 plan, all required checks |

Each slice must retain a buildable project and migration discipline. If only partial work is committed, document which gates remain untested; do not mark a package complete based on source alone.

## Verification commands and environments

The exact executable commands are in [M2 Verification Plan](M2_VERIFICATION_PLAN.md). Use existing `HarborOps.sln`, `dotnet test`, `npm ci`, `npm test`, `npm run build`, local Playwright and an explicitly disposable PostgreSQL/Compose environment. No source-control workflow execution is authorized.

For Windows development, keep the repository in `E:\APP IDEAS\HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform` and caches, local Docker/WSL state, generated evidence and key rings under E-drive-controlled locations. Avoid global npm, NuGet, Python or Docker configuration changes without a separate instruction. Do not prune Docker volumes or remove existing data.

## Known design decisions to settle before implementation

1. Exact EF Core migration timestamp and table/property naming, while preserving existing model snapshot comparisons.
2. Whether an existing organization should expose editable slug; default is **no**, slug immutable in M2.
3. Whether a user may hold multiple roles in one organization; default is **one**, explicit single-role grant.
4. Whether membership administration uses a page cursor or offset; default is bounded deterministic cursor.
5. Fine-grained future resource permissions are deferred. Do not expand `Administrator` into a global business-data reader in M2.
6. Suspended organizations and membership removals retain history. No hard-delete API.
7. Last-active-admin concurrency is transactionally serialized on the organization row and proven in a two-writer test.
8. Local CLI scope is explicitly opt-in and limited to bootstrap. No public organization creation or implicit user provisioning.

These choices are described in the design, but the documentation does not imply a separately accepted implementation decision.

## Milestone exit

M2 ends only with G2 evidence covering the full two-organization/role matrix, verified server-side isolation, migration safety, session selection, browser flow and preserved M0/M1 behavior. **Current state: M2 architecture and work plan documented; no M2 schema, API, interface or runtime tests implemented. G2 remains open.**
