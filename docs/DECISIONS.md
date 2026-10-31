# Architecture Decisions

This document records accepted baseline decisions and outstanding decisions that affect implementation.

## Local storage constraint

The mandatory [Local Storage Policy](LOCAL_STORAGE_POLICY.md) requires E-drive storage for all controllable persistent project files, caches, Docker/WSL state, database data and evidence. C-drive use is limited to unavoidable short-lived Windows temporary activity. This does not grant permission for cleanup, package migration or changes to global tool installations.

The accepted M0 baseline uses the hardened rootless PostgreSQL container and .NET 10. The prior upstream `gosu` findings were closed in the immutable-image high/critical scan performed during GitHub Actions run #2. G0 was accepted on 2026-10-31; future image changes require scanning.

## Accepted baseline decisions

### ADR-001: Modular monolith with separate tracking worker
**Status:** Accepted for initial architecture.

Use one ASP.NET Core application with explicit modules and a separate .NET event worker. Do not introduce microservices solely for architectural appearance. Reconsider only if measured scaling or deployment independence justifies them.

### ADR-002: PostgreSQL as transactional source of truth
**Status:** Accepted.

Store operational records and event inbox/outbox state in PostgreSQL. Derived dashboards may use read models, but projections must be rebuildable from retained accepted events.

### ADR-003: Local-first execution
**Status:** Accepted.

Essential features must work locally without paid services or cloud credentials. Hosted infrastructure is optional, budget-controlled, and disposable.

### ADR-004: Synthetic-data baseline
**Status:** Accepted.

Use deterministic generated datasets. No proprietary company data or private shipping integrations are needed to complete core milestones. Simulated states are clearly labeled.

### ADR-005: Secure organization boundaries
**Status:** Accepted.

Every business-data path must verify an authenticated principal and explicit organization permission. Global platform roles do not imply broad access to customer records.

### ADR-006: Inbound event history is immutable
**Status:** Accepted.

Never silently replace historical accepted events. Correct data with linked correction records and rebuild projections.

The [granular roadmap](ROADMAP.md) assigns earlier decision deadlines to narrower milestone entry points. These dates denote prerequisite boundaries, not new approvals. The original ADR-001 through ADR-006 architecture decisions remain intact.

### D-001: Human browser authentication and server-side session authority
**Status:** Accepted architecture; M1 implemented, verified and accepted on 2026-10-31, integrated into `main` via PR #4 (`8f9fc0d506a9e1dc1d4f5acab936622df988f413`).

Human browser login uses ASP.NET Core 10 cookie authentication with protected tickets, PostgreSQL-backed authoritative session validation on every authenticated request, Argon2id password hashes, antiforgery protection, same-origin React/API routing and persistent Data Protection keys. Account disablement or password reset invalidates sessions immediately. Integration-scoped tokens remain deferred to M7, while organization roles/isolation begin in M2. The approved detailed record is [D-001](ADR_D001_BROWSER_SESSIONS.md), with [M1 design](M1_AUTHENTICATION_DESIGN.md) and [G1 test plan](M1_VERIFICATION_PLAN.md). This decision approval is distinct from the [executed M1 runtime verification](M1_IMPLEMENTATION_VERIFICATION.md) and from G1 acceptance, which was explicitly recorded on 2026-10-31.

## M2 implementation choices (verified locally; not a new global decision)

[ADR-005](#adr-005-secure-organization-boundaries) requires scoped access. The [M2 architecture](M2_ARCHITECTURE_DESIGN.md) implements one role per organization membership, a fixed typed permission matrix, server-stored selection in `auth_sessions`, live authorization checks, controlled local bootstrap and transactional last-Administrator protection. These implementation-level choices are now integrated into `main` via PR #5 and do not create a new global ADR. The full G2 matrix passed locally on disposable resources ([executed record](M2_EXECUTED_VERIFICATION.md)); no retained workstation database was changed. G2 accepted on 2026-10-31.

## M3 reference-data design choices (proposal; approval pending)

The proposed [M3 architecture](M3_ARCHITECTURE_DESIGN.md) keeps each port/vessel reference owned by one organization, with an immutable UUID and tenant-local reference code, optional external identifiers, optimistic versioning and soft archival. Analysts may view reference data; Managers may manage it; M2 Support, Administrator and Auditor roles do not acquire implicit business access. These choices follow accepted ADR-002/003/004/005, but remain M3-specific implementation decisions subject to design approval. The [M3 implementation plan](M3_IMPLEMENTATION_PLAN.md) and [G3 tests](M3_VERIFICATION_PLAN.md) define the executable obligations. G3 is open, and no new globally accepted ADR is asserted.

## Open decisions

| ID | Decision required | Default hypothesis | Required before |
| --- | --- | --- | --- |
| D-002 | DCSA reference release/subset | Pin one version with a documented field mapping | M7 |
| D-003 | Container identifier format | Validate ISO 6346 where relevant; allow visibly synthetic demo values | M4 |
| D-004 | Map tile provider/license | Use compliant free source with local fallback | M14 |
| D-005 | Exact operational KPI tolerances | Versioned business rules and deterministic fixtures | M13 |
| D-006 | Error queue and worker durability detail | PostgreSQL inbox/dead-letter tables | M8 |
| D-007 | Hosting account and budget ceiling | Local only until credit and spending restrictions checked | M22 |
| D-008 | Backup retention and recovery objectives | Define measurable targets based on demo environment | M19 |

## Decision change protocol

Each material reversal records: context, alternatives, trade-offs, chosen approach, impact on requirements and tests, migration needs, and acceptance status. A change is not adopted solely because a new library or service is available.

## Accepted M0 runtime decision

The M0 baseline uses .NET 10, EF Core 10, PostgreSQL 16 with rootless container execution, a public synthetic-only read-only status endpoint and an isolated test database. Runtime verification passed on source revision `fec7429415fede121db8a4a34f0df72cd4fac609` and G0 was accepted on 2026-10-31. Authentication and session management are M1 under the accepted D-001 architecture; organization isolation remains M2. The browser remains functional-only until the final M24-04 accessibility gate.
