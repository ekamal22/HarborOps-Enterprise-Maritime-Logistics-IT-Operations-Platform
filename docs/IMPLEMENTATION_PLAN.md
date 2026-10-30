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

The separate branch `feat/m0-engineering-baseline` contains an implemented M0 baseline with historical local verification evidence: .NET solution and test projects, PostgreSQL migration and deterministic seed, read-only API, worker shell, React frontend and Docker Compose. Build, migration, Compose, browser and outage/recovery checks were recorded against `4fb34eb`, but later cold-database testing exposed a test-state dependency on that same revision. G0 was reopened, **acceptance pending**. The correction and follow-up verification requirements are in [hardening PR #3](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/3). See the linked register for the exact environment, test results and limitations.

Review evidence for M0-01 through M0-05 and G0 before recording explicit acceptance. Do not claim M1 has started or is accepted. Execution graph v2 splits the former M1-M6 bundles into M1-M24, with narrowly scoped dependencies and acceptance gates; do not carry old milestone IDs forward without explicit remapping. The current M0 check ledger is on the [feature branch](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feat/m0-engineering-baseline/docs/M0_VERIFICATION.md).

## Local development storage

All controlled persistent project files, caches, container data and verification artifacts must stay on E:. Follow [Local Storage Policy](LOCAL_STORAGE_POLICY.md); use C: only for unavoidable temporary Windows activity.

## Change management

If scope changes, update the PRD and atomic requirements. If milestone gates change, update the roadmap and acceptance criteria. If ordering changes, edit the JSON graph and regenerate the Markdown diagram with `node scripts/render-execution-graph.mjs`. Update status only with observed results. Avoid duplicate tables of milestone states or competing task sequences here.
