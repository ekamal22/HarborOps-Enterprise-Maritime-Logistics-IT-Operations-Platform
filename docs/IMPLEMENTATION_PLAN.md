# Implementation Notes

**Status:** Supporting engineering guidance. This file is not an independent roadmap or authoritative dependency ledger.

The [Product Requirements Document](PRD.md) defines user outcomes and release boundaries. The [Delivery Roadmap](ROADMAP.md) owns M0-M24 scope, sequencing, and gates. The [Execution Graph](EXECUTION_GRAPH.md), generated from [execution-graph.json](execution-graph.json), owns work-package IDs and dependency edges. [Status](STATUS.md) alone owns what is actually implemented and verified.

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

The accepted M0 implementation is integrated into `main`. It includes .NET 10, PostgreSQL 16, a read-only baseline API, deterministic database seed, an idle worker shell, a React status UI, Docker Compose, and executable test suites. The corrected source passed [GitHub Actions run #2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41134743879) at `fec7429415fede121db8a4a34f0df72cd4fac609`: 1 unit, 10 contract, 5 fresh plus 5 repeat integration, 6 web and 12 functional browser tests, Compose outage/recovery, and PostgreSQL/API/web vulnerability scans. **G0 accepted** on 2026-10-31. No M1 code has started. See [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md).

G0 evidence is recorded and accepted. D-001 is now approved as the M1 browser cookie and server-session architecture. M1 has passed verification, G1 was accepted on 2026-10-31 and PR #4 was merged into `main` at `8f9fc0d506a9e1dc1d4f5acab936622df988f413`. M2 is next; G2 remains open. Execution graph v2 splits the former M1-M6 bundles into M1-M24, with narrowly scoped dependencies and acceptance gates; do not carry old milestone IDs forward without explicit remapping. The current M0 verification record is [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md) on `main`; [M0 Verification](M0_VERIFICATION.md) preserves the earlier historical run.

## M1 implementation work packages (completed, G1 accepted)

Authoritative design: [M1 Authentication Design](M1_AUTHENTICATION_DESIGN.md), [D-001](ADR_D001_BROWSER_SESSIONS.md), [G1 Verification](M1_VERIFICATION_PLAN.md). See [executed M1 verification](M1_IMPLEMENTATION_VERIFICATION.md) for actual implementation and runtime evidence. Approval of this implementation remains a separate gate.

| Package | Detailed deliverable | Completion evidence |
| --- | --- | --- |
| M1-01 | Finalize identity API, D-001 cookie/CSRF/same-origin decision, hash/expiry defaults, negative matrix and security boundary | Approved design and testable JSON/error contracts; unresolved implementation parameters recorded |
| M1-02 | Add `users`, `user_credentials`, `auth_sessions`, `auth_events` through an additive EF Core migration, Argon2id hashing, server-session repository and restricted CLI provisioning | Fresh and M0-seeded disposable migration, password verification, expiry/revocation and transactional state tests |
| M1-03 | Integrate ASP.NET Core authentication, Data Protection key ring, antiforgery, rate limiting, login/session/logout/logout-all/disable-self endpoints and sanitized errors | API contract and negative security tests, instant revocation, restart/outage and no-secret-leak evidence |
| M1-04 | Configure Nginx/Vite same-origin `/api` proxy, React login/session shell, three-browser functional tests and source-specific GitHub Actions evidence | Full G1 matrix, preserved M0 contracts and image scans at a fixed SHA |

M1-02 and M1-03 may proceed after M1-01; M1-04 integrates both. M1 did not introduce organization authorization. Its G1 verification, acceptance and merge are complete. M2 adds organization authorization on a separate branch; M2 verification and acceptance remain separate. Database compatibility with existing retained local volumes is not a G1 requirement. All formal accessibility testing remains the final M24-04 milestone.

## M2 implementation source (committed on feature branch; unverified)

The detailed [M2 architecture](M2_ARCHITECTURE_DESIGN.md), [M2 implementation sequence](M2_IMPLEMENTATION_PLAN.md), and [G2 local test specification](M2_VERIFICATION_PLAN.md) govern this milestone. G1 is accepted. M2-01 through M2-03 source is committed on a feature branch, with [implementation evidence inventory](M2_IMPLEMENTATION_STATUS.md); M2-04's initial [GitHub suite](M2_EXECUTED_VERIFICATION.md) passed, but its full negative test specification remains incomplete. G2 open. Do not begin M3 based on source alone.

| Package | Planned change | Gate evidence |
| --- | --- | --- |
| M2-01 | Additive organization/membership schema, fixed roles, restricted bootstrap and nullable server-side session selection | Fresh and M1-to-M2 migration, constraints, fixture and bootstrap tests |
| M2-02 | Deny-by-default organization authorization guard, scoped profile/membership APIs, optimistic versioning, last-admin transaction safety | Wrong-role, foreign-ID, concurrency, CSRF and outage tests |
| M2-03 | Session-owned selected organization, context endpoints and functional browser switcher | Multi-session, differing-role, removal, suspension and state-refresh tests |
| M2-04 | Execute G2 two-tenant matrix and M0/M1 regressions, record exact revision and outcomes | Local unit, real PostgreSQL, HTTP, browser and operational evidence |

**Execution restriction:** Do not use GitHub Actions, modify workflows, dispatch runs, or rely on remote CI until the restriction is explicitly lifted. Required verification must be executed locally with disposable resources under the E-drive storage policy.

## Local development storage

All controlled persistent project files, caches, container data and verification artifacts must stay on E:. Follow [Local Storage Policy](LOCAL_STORAGE_POLICY.md); use C: only for unavoidable temporary Windows activity.

## Change management

If scope changes, update the PRD and atomic requirements. If milestone gates change, update the roadmap and acceptance criteria. If ordering changes, edit the JSON graph and regenerate the Markdown diagram with `node scripts/render-execution-graph.mjs`. Update status only with observed results. Avoid duplicate tables of milestone states or competing task sequences here.
