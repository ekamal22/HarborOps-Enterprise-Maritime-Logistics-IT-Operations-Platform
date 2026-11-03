# Implementation Notes

**Status:** Supporting engineering guidance. This file is not an independent roadmap or authoritative dependency ledger.

The [Product Requirements Document](PRD.md) defines user outcomes and release boundaries. The [Delivery Roadmap](ROADMAP.md) owns M0-M24 scope and verification gates, while [Parallel Delivery Policy](PARALLEL_DELIVERY_POLICY.md) governs concurrent workstreams and integration. The [Execution Graph](EXECUTION_GRAPH.md), generated from [execution-graph.json](execution-graph.json), owns work-package IDs and dependency edges. [Status](STATUS.md) alone owns what is actually implemented and verified.

## Work-package execution contract

For each work package, record:

1. Exact branch, source revision, and planned requirement IDs.
2. Existing implementation and decisions reviewed before changes.
3. Files and module boundaries affected, API/domain/migration implications.
4. Correct behavior, expected failure behavior, and permission boundaries.
5. Exact unit, database, API, browser, security, and operational tests applicable to its scope.
6. Observed results, environment, fixtures, logs, unresolved issues, and evidence locations.
7. Documentation updated and gate state. Do not equate code presence with verification.

## Technical implementation principles

- Work within the modular monolith, separate worker, and PostgreSQL baseline in [Architecture](ARCHITECTURE.md).
- Keep multi-organization authorization checks server-side from M1 onward.
- Implement event durability, replay safety, and projection correctness as explicit tests before dashboards depend on them.
- Prefer deterministic synthetic fixtures and local-only free infrastructure.
- Do not hide failing or skipped tests behind a successful script exit.
- Keep API contracts, persistence invariants, and operations runbooks aligned with actual code.
- Record accepted architectural changes in [Decisions](DECISIONS.md).

## M0 checkpoint

The accepted M0 implementation is integrated into `main`. It includes .NET 10, PostgreSQL 16, a read-only baseline API, deterministic database seed, an idle worker shell, a React status UI, Docker Compose, and executable test suites. The corrected source passed [GitHub Actions run #2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41134743879) at `fec7429415fede121db8a4a34f0df72cd4fac609`: 1 unit, 10 contract, 5 fresh plus 5 repeat integration, 6 web and 12 functional browser tests, Compose outage/recovery, and PostgreSQL/API/web vulnerability scans. **G0 accepted** on 2026-10-31. This paragraph preserves the M0 checkpoint; M1-M6 have since been accepted and integrated into `main`. See [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md).

G0 evidence is recorded and accepted. D-001 is now approved as the M1 browser cookie and server-session architecture. M1 has passed verification, G1 was accepted on 2026-10-31 and PR #4 was merged into `main` at `8f9fc0d506a9e1dc1d4f5acab936622df988f413`. M2 is verified locally and G2 was accepted on 2026-10-31. Execution graph v2 split the former M1-M6 bundles into M1-M24; graph v3.0 preserves those scoped packages and gates while replacing the automatic sequential chain with explicit parallel technical prerequisites. Do not carry old milestone IDs forward without remapping. The current M0 verification record is [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md) on `main`; [M0 Verification](M0_VERIFICATION.md) preserves the earlier historical run.

## M1 implementation work packages (completed, G1 accepted)

Authoritative design: [M1 Authentication Design](M1_AUTHENTICATION_DESIGN.md), [D-001](ADR_D001_BROWSER_SESSIONS.md), [G1 Verification](M1_VERIFICATION_PLAN.md). See [executed M1 verification](M1_IMPLEMENTATION_VERIFICATION.md) for actual implementation and runtime evidence. Approval of this implementation remains a separate gate.

| Package | Detailed deliverable | Completion evidence |
| --- | --- | --- |
| M1-01 | Finalize identity API, D-001 cookie/CSRF/same-origin decision, hash/expiry defaults, negative matrix and security boundary | Approved design and testable JSON/error contracts; unresolved implementation parameters recorded |
| M1-02 | Add `users`, `user_credentials`, `auth_sessions`, `auth_events` through an additive EF Core migration, Argon2id hashing, server-session repository and restricted CLI provisioning | Fresh and M0-seeded disposable migration, password verification, expiry/revocation and transactional state tests |
| M1-03 | Integrate ASP.NET Core authentication, Data Protection key ring, antiforgery, rate limiting, login/session/logout/logout-all/disable-self endpoints and sanitized errors | API contract and negative security tests, instant revocation, restart/outage and no-secret-leak evidence |
| M1-04 | Configure Nginx/Vite same-origin `/api` proxy, React login/session shell, three-browser functional tests and source-specific GitHub Actions evidence | Full G1 matrix, preserved M0 contracts and image scans at a fixed SHA |

M1-02 and M1-03 followed M1-01; M1-04 integrated both. M1 did not introduce organization authorization; M2 delivered it and is accepted and integrated into `main` via PR #5. Database compatibility with retained local volumes is not a G1/G2 requirement; designated later milestones own those checks. Dedicated accessibility verification remains M24-04.

## M2 implementation (verified locally; G2 accepted)

The [M2 architecture](M2_ARCHITECTURE_DESIGN.md), [M2 implementation plan](M2_IMPLEMENTATION_PLAN.md) and [G2 local verification plan](M2_VERIFICATION_PLAN.md) governed the completed M2-01 through M2-04 implementation. The full G2 matrix passed locally on `fbddc1361d79b027275bb53b48133ba0dc7d5cb1` ([executed record](M2_EXECUTED_VERIFICATION.md)); G2 was accepted on 2026-10-31 and M2 merged into `main` via PR #5 at `1df638d5300191f6c2c6c9d201fd5f4c4ab9e562`. M3 was subsequently built, fully verified at G3 and integrated into `main` after explicit acceptance.

| Package | Planned change | Gate evidence |
| --- | --- | --- |
| M2-01 | Additive organization/membership schema, fixed roles, restricted bootstrap and nullable server-side session selection | Fresh and M1-to-M2 migration, constraints, fixture and bootstrap tests |
| M2-02 | Deny-by-default organization authorization guard, scoped profile/membership APIs, optimistic versioning, last-admin transaction safety | Wrong-role, foreign-ID, concurrency, CSRF and outage tests |
| M2-03 | Session-owned selected organization, context endpoints and functional browser switcher | Multi-session, differing-role, removal, suspension and state-refresh tests |
| M2-04 | Execute G2 two-tenant matrix and M0/M1 regressions, record exact revision and outcomes | Local unit, real PostgreSQL, HTTP, browser and operational evidence |

**Execution restriction:** Do not use GitHub Actions, modify workflows, dispatch runs, or rely on remote CI until the restriction is explicitly lifted. Required verification must be executed locally with disposable resources under the E-drive storage policy.

## M3 implementation (G3 accepted and merged)

The [M3 architecture](M3_ARCHITECTURE_DESIGN.md), [implementation plan](M3_IMPLEMENTATION_PLAN.md) and [G3 verification plan](M3_VERIFICATION_PLAN.md) define four bounded work packages: M3-01 freezes identifiers, tenant ownership, role permissions and fixtures; M3-02 adds port persistence and scoped endpoints; M3-03 adds vessel persistence and scoped endpoints; M3-04 delivers a browser reference workspace and executes G3-S01 to G3-S25. After the shared M3-01 contract, port and vessel work may proceed in parallel only with a coordinated additive migration. Implementation source and tests are committed, as recorded in [M3 implementation register](M3_IMPLEMENTATION_STATUS.md). G3-S01 to G3-S25 all passed in one complete local run on `b72025d35d62d0a8a45b466986a0b3bd89eebadb` ([executed verification](M3_EXECUTED_VERIFICATION.md)); G3 was explicitly accepted on 2026-10-31, and the implementation is integrated into `main`. Preserve accepted M0-M3 behavior and do not use GitHub Actions.


## M4 accepted implementation

M4-01 through M4-04 were implemented and G4 was explicitly accepted on 2026-11-01 after D-003 Option B approval and independent review of [executed evidence](M4_EXECUTED_VERIFICATION.md). The accepted implementation includes tenant-scoped voyages/containers, additive PostgreSQL migration, React workspaces and all three browser engines. The final tested implementation SHA was `600d7c787da9f9cd4a70225cb3e3c38c093c0dc6`. M5 shipment lifecycle and M6 allocation history were subsequently verified, accepted on 2026-11-02 and integrated into `main`.

## M5 and M6 accepted implementation

The [M5 architecture](M5_ARCHITECTURE_DESIGN.md), [implementation plan](M5_IMPLEMENTATION_PLAN.md) and [G5 closure](M5_G5_GATE_CLOSURE.md) record the accepted shipment lifecycle. The [M6 architecture](M6_ARCHITECTURE_DESIGN.md), [implementation plan](M6_IMPLEMENTATION_PLAN.md) and [G6 closure](M6_G6_GATE_CLOSURE.md) record accepted shipment-container and shipment-voyage association histories. Both milestones are integrated into `main`; G5 and G6 were accepted on 2026-11-02. Preserve their authorization, interval and concurrency rules in later implementation.

## M7 proposed tracking contract implementation

The [M7 architecture](M7_ARCHITECTURE_DESIGN.md), [implementation plan](M7_IMPLEMENTATION_PLAN.md), [D-002 proposal and decision record](M7_D002_TRACKING_REFERENCE_PROPOSAL.md) and [G7 matrix](M7_VERIFICATION_PLAN.md) define the tracking-source contract scope. D-002 was accepted on 2026-11-03 (Option A) with pinned local reference snapshots. The M7 implementation is committed on `feat/m7-tracking-source-validation` and technically verified locally (see [executed verification](M7_EXECUTED_VERIFICATION.md)); G7 acceptance is pending. See [implementation register](M7_IMPLEMENTATION_STATUS.md). M8, not M7, supplies durable intake and receipt acknowledgment.

## Concurrent work-package implementation

The canonical graph v3.0 defines technical dependencies at package level rather than forcing milestone-number order. Read the specific `depends_on` edges before allocating work. The accepted G0-G6 baseline may support several parallel streams, provided any mandatory D-IDs are resolved before affected implementation.

- Begin independent work in separate named milestone branches and E:-backed worktrees; verify the base revision and explicit prerequisite acceptances.
- Agree on shared route, permission, domain DTO, authentication and validation contracts before simultaneous integration. Never merge incompatible assumptions behind individually passing tests.
- Coordinate all new EF Core/PostgreSQL migrations into one ordered, additive history. Rehearse both fresh and relevant accepted M0-M6 historical upgrades after branches converge.
- Require local scoped evidence for each package; before gate closure, integrate upstream changes and rerun affected M0-M6 and cross-stream regressions at one final SHA.
- Verified evidence is not explicit acceptance. Merge only after the specific gate is accepted; milestone numbering does not authorize another stream or merge.
- G24 must revalidate the final integrated code, including security, recovery, performance, quarter checkpoints and the final M24-04 accessibility audit.

See the [Parallel Delivery Policy](PARALLEL_DELIVERY_POLICY.md) for ownership of shared contracts and detailed integration safeguards.

## Local development storage

All controlled persistent project files, caches, container data and verification artifacts must stay on E:. Follow [Local Storage Policy](LOCAL_STORAGE_POLICY.md); use C: only for unavoidable temporary Windows activity.

## Change management

If scope changes, update the PRD and atomic requirements. If milestone gates change, update the roadmap and acceptance criteria. If ordering changes, edit the JSON graph and regenerate the Markdown diagram with `node scripts/render-execution-graph.mjs`. Update status only with observed results. Avoid duplicate tables of milestone states or competing task sequences here.
