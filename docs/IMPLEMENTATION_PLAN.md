# Implementation Notes

**Status:** Supporting engineering guidance. This file is not an independent roadmap or authoritative dependency ledger.

The [Product Requirements Document](PRD.md) defines user outcomes and release boundaries. The [Delivery Roadmap](ROADMAP.md) owns M0-M6 scope, sequencing, and gates. The [Execution Graph](EXECUTION_GRAPH.md), generated from [execution-graph.json](execution-graph.json), owns work-package IDs and dependency edges. [Status](STATUS.md) alone owns what is actually implemented and verified.

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

The separate branch `feat/m0-engineering-baseline` contains an initial .NET solution, database-backed read-only status API, EF Core migration and seed, worker shell, React frontend, Compose configuration, and test foundations. Full backend compilation, live migration, Compose launch, and browser-to-database behavior remain unverified.

Complete M0 work packages and gate G0 before implementing dependent M1 features. The current M0 check ledger is on the [feature branch](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feat/m0-engineering-baseline/docs/M0_VERIFICATION.md).

## Change management

If scope changes, update the PRD and atomic requirements. If milestone gates change, update the roadmap and acceptance criteria. If ordering changes, edit the JSON graph and regenerate the Markdown diagram with `node scripts/render-execution-graph.mjs`. Update status only with observed results. Avoid duplicate tables of milestone states or competing task sequences here.
