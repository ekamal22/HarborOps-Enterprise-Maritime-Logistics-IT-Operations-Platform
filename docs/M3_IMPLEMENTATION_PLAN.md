# M3 Ports and Vessels Reference Data: Implementation Plan

**State:** M3-01 through M3-04 implemented, verified locally and accepted at G3 on 2026-10-31; integrated into `main`.
**Entry:** G2 verified, accepted and merged into `main` through PR #5  
**Milestone packages:** M3-01, M3-02, M3-03, M3-04  
**Design authority:** [M3 architecture](M3_ARCHITECTURE_DESIGN.md), [Roadmap](ROADMAP.md), [Requirements](REQUIREMENTS.md), [G3 verification plan](M3_VERIFICATION_PLAN.md), [M2 architecture](M2_ARCHITECTURE_DESIGN.md), [Local Storage Policy](LOCAL_STORAGE_POLICY.md).

## 1. Scope control and implementation rules

Implement only two tenant-owned reference resources, ports and vessels, with validated persistence, scoped APIs and a functional browser lookup/management flow. The M3 design governs the implementation requested after planning. Do not treat committed source as verified behavior. Do not start M4, create voyage/container/shipment tables, introduce external data feeds, download a global registry as a runtime dependency, add payments, or change identity-provider architecture.

- Preserve M0's synthetic baseline and health routes and M1's authentication/session lifecycle.
- Preserve all accepted M2 organization/membership endpoints, role grants, last-administrator guards and session selection.
- Implement reference data with explicit typed operations, not an implicit Administrator or Support bypass.
- Append migrations only. No modifications to existing M0-M2 applied migrations or retained workstation PostgreSQL volume.
- No GitHub Actions dispatch, rerun, workflow-triggering push or CI-dependent acceptance. Use local-only verification with disposable resources and all controllable storage on E:.
- Use `[skip ci]` on commits. Avoid push operations that would trigger Actions, even with a commit marker, until their trigger behavior is checked.
- No claim of G3 completion or acceptance before all mandatory scenarios run on a fixed final source SHA.

## 2. Dependency order and deliverable map

```text
G2 accepted
  |
  v
M3-01 Contracts, validation, authorization policy and test fixtures
  |--------------------------------|
  v                                v
M3-02 Ports schema/service/API      M3-03 Vessels schema/service/API
  |                                |
  |--------------------------------|
                   v
M3-04 React workspace, full G3 verification and evidence
                   |
                   v
G3 verified and accepted on 2026-10-31
```

M3-02 and M3-03 may be implemented in separate feature branches after M3-01. Coordinate a single additive migration and shared auth helper before parallelization; do not independently regenerate competing EF migrations. Merge implementation work only after conflict-free integration and local verification on the combined revision.

## 3. M3-01 Specify port and vessel invariants

**Dependencies:** Accepted G2; design approved.

**Deliverables and source areas:**

- Add `src/HarborOps.Domain/References/PortReferencePolicy.cs`, `VesselReferencePolicy.cs`, `ReferenceCodePolicy.cs`, typed `ReferenceStatus` and `ReferenceSourceKind` (or equivalent well-scoped files).
- Extend `src/HarborOps.Domain/Organizations/OrganizationPolicy.cs` with `ViewReferenceData` and `ManageReferenceData`, maintaining all existing M2 semantics.
- Add `tests/HarborOps.UnitTests/ReferencePolicyTests.cs` for identifier normalization, field validation, IMO checksum, role permissions and status transitions.
- Define synthetic fixtures for two organizations and all five roles. Pin fixture seed/version and ensure no real people, organizations or verified registry claims.

**Implementation requirements:**

1. Freeze exactly the fields, casing, mutability, limits and normalization described in the architecture.
2. Explicitly test UN/LOCODE's five-character format, country prefix, optionality and invalid separators; do not equate a syntactically valid location code with verified registry entry or seaport function.
3. Test canonical seven-digit IMO checksum and optional `IMO` prefix handling, reject invalid checksums and malformed formats; document future eight-digit consideration separately.
4. Test nine-digit MMSI as text with preserved leading zero, and optional identifier behavior for name-only vessels.
5. Make the role matrix deny-by-default: Analyst reads; Manager reads/writes; Support, Administrator and Auditor have no implicit business-data permissions.
6. Decide and test that local reference codes are tenant-unique, immutable and retained on archival. No cross-tenant ownership transfer.
7. Write a fixture generator with stable test IDs or a stable seed and clearly synthetic labels. Do not run it against the primary Compose project.

**M3-01 exit evidence:** Pure domain tests pass, negative samples documented, the two reference contracts and role matrix are approved, and the M2 permission matrix remains unchanged.

## 4. Shared database and authorization foundation

**Expected changes:**

- `src/HarborOps.Infrastructure/References/PortReference.cs`, `VesselReference.cs` and focused repository/service files.
- `src/HarborOps.Infrastructure/Persistence/HarborOpsDbContext.cs` plus exactly one additive M3 migration and model snapshot update in `Migrations/`.
- `src/HarborOps.Infrastructure/DependencyInjection.cs` or the actual existing persistence registration file to register new services.
- Extend the existing M2 authorization layer with a reusable operation-specific scope verifier. Keep M2 policy API stable.

**Migration requirements:**

- Create `ports` and `vessels` with organization FK, immutable local reference, external identifiers, status, source kind, version, UTC timestamps.
- Create composite tenant/code unique indexes, partial unique indexes for non-null UN/LOCODE/IMO/MMSI, tenant/page index, FK and status/format/version constraints.
- Check model snapshot parity, migration discovery and no pending migrations.
- Test fresh migration and M0-seeded/M1-session/M2-membership to M3 upgrade on disposable PostgreSQL with data preservation and repeatability.
- No automatic primary database seed; fixture loading is explicitly isolated.
- Preserve historical rows and future referencing semantics using `RESTRICT`, not cascade delete.

**Authorization and concurrency requirements:**

- Every read binds the row's `organization_id` and revalidates selected current session and active permission in a single query, preventing a stale `OrganizationScope` being treated as a general read grant.
- Every write uses a database transaction, locks the organization row, revalidates current session, organization and membership, and then performs a tenant-qualified version-checked mutation.
- Database uniqueness conflicts become safe 409 responses. Unexpected provider details remain server-private and redacted.
- Respect cancellation and transaction rollback, including when an account or organization is revoked concurrently.
- The API handlers must not directly expose `HarborOpsDbContext` or accept client-supplied organization IDs as record ownership.
- Confirm existing M2 membership and organization-admin tests still pass with the extended typed permission enum.

## 5. M3-02 Implement ports persistence and endpoints

**Dependencies:** M3-01; shared schema and scope contract agreed.

**Expected source changes:** `src/HarborOps.Infrastructure/References/PortReferenceStore.cs`, `src/HarborOps.Api/References/PortEndpoints.cs`, DI and endpoint registration in `src/HarborOps.Api/Program.cs`.

**Ordered work:**

1. Build creation and update DTOs with explicit request-field allowlists. Server sets UUID, organization, version, timestamps and source kind. Disallow unknown or client-controlled ownership identifiers.
2. Add tenant-qualified create, get, list, update, soft archive and explicit restore methods. Never construct an unscoped `DbSet<PortReference>` query in an endpoint.
3. Expose `GET /api/v1/organizations/{organizationId}/ports` with `q`, `status`, `after`, `limit` and a stable keyset cursor. Default 25, maximum 100; scope checks and bounded output apply to all filters.
4. Expose `GET /ports/{portId}`, `POST /ports`, `PATCH /ports/{portId}`, `DELETE /ports/{portId}` with the same `organizationId` route prefix. List/detail require `ViewReferenceData`; writes require `ManageReferenceData` and accepted M1 CSRF/Origin rules.
5. Normalize and validate local reference, name, country code and optional UN/LOCODE. Unknown registry codes are permitted only as syntactically valid user-provided values, never asserted officially verified.
6. On create return 201, on edit 200, on successful archive 204. Distinguish 400 validation, 401 auth, 403 wrong-role, 404 foreign/missing, 409 selection/stale/duplicate and sanitized 503 dependency outage.
7. Add integration and HTTP tests for uniqueness, malformed input, ownership, version races, country mismatch, no-code records, archived read/restore, query bounds and hostile filtering.

**M3-02 exit evidence:** Port schema/contract tests pass against real PostgreSQL and the real M1/M2 HTTP pipeline, with no M0-M2 regression.

## 6. M3-03 Implement vessels persistence and endpoints

**Dependencies:** M3-01; shared schema and scope contract agreed. May proceed alongside M3-02 once the migration and helper are coordinated.

**Expected source changes:** `src/HarborOps.Infrastructure/References/VesselReferenceStore.cs`, `src/HarborOps.Api/References/VesselEndpoints.cs`, associated migration/registration and tests.

**Ordered work:**

1. Enforce local reference-code invariants and a required vessel name, plus optional IMO, MMSI and flag country code.
2. Normalize optional `IMO` prefix into the canonical seven digits, validate checksum, reject invalid/noisy input. Treat checksum-valid as syntactically validated, not registry-confirmed.
3. Preserve MMSI as nine-digit text (including leading zeros), check length/digits and reject all-zero values. Do not infer flag from MMSI.
4. Enforce per-organization non-null IMO/MMSI uniqueness using partial indexes; allow the same registered-looking identifier in a different tenant, without sharing data.
5. Add authenticated `GET /vessels`, `GET /vessels/{vesselId}`, `POST /vessels`, `PATCH /vessels/{vesselId}` and `DELETE /vessels/{vesselId}` beneath the organization path with the same pagination/CSRF/version/error rules as ports.
6. Make archive and restore explicit and version checked. Never hard delete an immutable reference UUID or free its local code for reuse.
7. Add test cases for unique conflicts, null optional identifiers, duplicate names, leading-zero MMSIs, invalid IMO checksums, hostile inputs, role denials, foreign UUIDs and transaction races.

**M3-03 exit evidence:** Vessel schema/HTTP and adversarial tests pass, including real PostgreSQL constraints and cross-tenant isolation.

## 7. M3-04 Reference-data browser flow and full verification

**Dependencies:** Completed M3-02 and M3-03.

**Expected source changes:**

- `web/src/references/ReferenceDataWorkspace.tsx`, `PortReferences.tsx`, `VesselReferences.tsx`, `referenceClient.ts` (or equivalent cohesive grouping).
- Update `web/src/auth/OrganizationWorkspace.tsx` or its parent to host reference tabs under selected server organization.
- Add frontend tests and `tests/browser/specs/reference-data.spec.mjs`; preserve all existing M0-M2 browser specs.
- Add focused `tests/HarborOps.IntegrationTests/Reference*Tests.cs` and any needed contract tests.
- Add an E:-only disposable local M3 verification runner or extend the existing local runner with M3 stages without weakening its preflight guards.

**Ordered work:**

1. Integrate a selected-organization-aware reference workspace and clear all tenant-specific state as soon as selection changes.
2. Add bounded list/search/status filtering and detail view for both resources; render returned canonical fields only.
3. Show manager-only create/edit/archive/restore controls and hide them for read-only Analyst, while enforcing all permissions server-side.
4. Ensure 401 sign-out recovery, 403 role-change handling, 404 foreign/missing and 409 selection/stale versions, 503 outage/retry, and no stale previous-tenant rows after delayed responses.
5. Exercise functional browser scenarios in Chromium, Firefox and WebKit using disposable M2-provisioned accounts and isolated organizations.
6. Execute G3 scenarios G3-S01 through G3-S25 in [G3 plan](M3_VERIFICATION_PLAN.md), including full M0-M2 regression, fresh/repeat schema upgrade, endpoint/verb/role matrix, adversarial races, image/dependency review and disposable Compose recovery.
7. Record commands, test counts, source SHA, OS/SDK/browser/database versions, fixture IDs, evidence paths and every PASS/FAIL/BLOCKED status in `docs/M3_EXECUTED_VERIFICATION.md` only after execution.
8. Update API Contracts, Domain Model, Requirements, Security, Operations, Test Strategy, Status, Roadmap and README only with observed implemented behavior, not predictions.

**M3-04 exit evidence:** Functional UI and every mandatory G3 scenario passed on the verified implementation SHA; the full G3 gate was explicitly accepted on 2026-10-31 ([executed verification](M3_EXECUTED_VERIFICATION.md)). No additional M4 work is claimed.

## 8. Verification strategy and execution boundary

Before any local verification:

- Prove working tree and temporary/cache/build/log/browser paths on E:. Confirm Docker Desktop WSL images and volumes are E:-backed; do not modify or inspect historical primary volumes as routine G3 checks.
- Check the necessary tool versions and local .NET 10 SDK container availability without installing a new toolchain on C:.
- Use a new disposable Compose project and a PostgreSQL database explicitly restricted to `harborops_integration` or `harborops_test`. Never run tests against the primary local `harborops` database.
- Perform Release build, unit, API contract, fresh PostgreSQL integration, repeated migration/seed integration, frontend tests/build, three-browser functional tests, Compose/session/recovery checks and local image scans.
- Execute all role/tenant/endpoint/verb tests and simultaneous update/archive/revocation races. A passing unit test cannot replace real DB or HTTP tests.
- Record exact failed attempts and fixes; rerun the complete G3 matrix after the final source change. Missing required tools or evidence are BLOCKED, not PASS.
- Use a documentation-only `[skip ci]` commit for acceptance-record changes. Never start GitHub Actions.

Formal accessibility testing is reserved for M24-04. Historical retained-volume compatibility occurs at M6, M12, M18 and M24-03, not G3; M19 owns backup/restore.

## 9. Risk register and mitigations

| Risk | Mitigation and verification |
| --- | --- |
| Cross-tenant ID substitution | Require server-session selection, permission and `organization_id` predicate for every query; test valid foreign IDs |
| Stale read after scope switch or revocation | Database authorization predicate and browser request generation/abort; two-session race tests |
| Privilege escalation by Administrator or Support | Closed business-data permission matrix with explicit negative HTTP tests |
| Concurrent edits/archive | Lock organization, revalidate within transaction, version check and exactly one affected row |
| Duplicate external identity | Partial unique tenant indexes, 409 response, nullable identifiers, archive does not release identities |
| Claiming official identity from syntax alone | Distinguish user-provided/synthetic provenance; no official existence claim or automatic enrichment |
| Destructive schema change | Additive migration, snapshot test, preserved M0-M2 data, safe disposable repeat upgrade |
| Unbounded search/response | Escape user queries, cap length/page, indexed UUID cursor, bounded payload |
| Invalid dependency or outdated image | Local dependency review, revision-labeled built images and high/critical Trivy scan |
| C: storage and crash dumps | E: preflight, bounded core dumps disabled, clean CLI failure, inspect only authorized paths |
| Feature scope creep | No voyage, container, shipment, mapping, AIS, integration feeds or production deployment at M3 |

## 10. Documentation deliverables and handoff

The design set consists of:

1. [M3 architecture](M3_ARCHITECTURE_DESIGN.md): schema, API, permissions, ownership, status and future seams.
2. This implementation plan: work-package files, dependency order, checkpoints and release boundaries.
3. [G3 verification plan](M3_VERIFICATION_PLAN.md): mandatory executable scenario matrix and evidence requirements.
4. [Project Status](STATUS.md) and [M3 implementation register](M3_IMPLEMENTATION_STATUS.md): implementation source is committed on a feature branch and G3 is verified locally with acceptance pending (see the [executed verification](M3_EXECUTED_VERIFICATION.md)).
5. [Roadmap](ROADMAP.md) and [Requirements](REQUIREMENTS.md): existing M3 allocation and cross-reference.

**Next decision:** Explicit G3 acceptance, which is separate from this plan and from the executed verification record. Do not conflate local verification with an accepted G3 gate or a merge.
