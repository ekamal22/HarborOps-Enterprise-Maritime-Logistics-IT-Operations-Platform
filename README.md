# HarborOps
## Enterprise Maritime Logistics & IT Operations Platform

HarborOps is a modular enterprise platform for shipment and container management, maritime tracking events, operational analytics, infrastructure observability, security controls, and IT incident handling.

**Status:** M0 engineering baseline merged into `main` and **G0 accepted** on 2026-10-31. The .NET 10, fresh PostgreSQL, functional browser, Compose recovery and image hardening passed [GitHub Actions run #2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41134743879) at source revision `fec7429415fede121db8a4a34f0df72cd4fac609`. M1-M24 remain planned; M1 has not started. See [Project Status](docs/STATUS.md).

HarborOps is designed to run locally with free, self-hosted development tools. Optional hosted demonstrations must remain within explicitly approved free tiers, credits, and operating limits.

### Planning source of truth

- [Product Requirements Document](docs/PRD.md) defines intended users, product scope, outcomes, and release priorities.
- [Delivery Roadmap](docs/ROADMAP.md) defines milestone deliverables, entry criteria, and evidence-based exit gates.
- [Local Storage Policy](docs/LOCAL_STORAGE_POLICY.md) requires all controlled persistent development storage on E: and limits C: use to unavoidable temporary system activity.
- [Execution Graph](docs/EXECUTION_GRAPH.md) visualizes work-package prerequisites; [graph data](docs/execution-graph.json) is authoritative for dependencies and IDs.
- [Project Status](docs/STATUS.md) alone describes implementation, verification, and acceptance state. [Requirements](docs/REQUIREMENTS.md) and [Acceptance Criteria](docs/ACCEPTANCE_CRITERIA.md) own atomic requirements and detailed checks.

Run `node scripts/render-execution-graph.mjs --check` to validate the graph against its JSON source, and `node scripts/check-planning-docs.mjs` to check milestone coverage, document links and status references. These are documentation consistency checks, not application runtime verification.

### Product objectives

- Manage shipments, containers, voyages, ports, and tracking milestones through a consistent operational model.
- Ingest, validate, deduplicate, and process tracking events through versioned interfaces informed by DCSA Track & Trace.
- Surface shipment exceptions, ETA changes, and aggregate operational health through searchable dashboards.
- Enforce authenticated, role-scoped access to operational records and record sensitive changes in audit trails.
- Expose metrics, logs, tracing, health checks, and an integrated IT incident workflow.
- Demonstrate reproducible testing, safe failure handling, recoverability, and deployment portability.

### M1 implementation branch

The feature branch `feat/m1-authentication-session-lifecycle` contains the first M1 implementation of server-authoritative cookie sessions, Argon2id passwords, local account provisioning, database identity tables and a browser sign-in shell. This is not merged into `main` or accepted until G1 verification succeeds and acceptance is recorded. The previous M0 status remains the accepted default-branch baseline.

**Local M1 accounts:** Start Compose with `docker compose up -d --build` as described below. Create an account with `docker compose run --rm -it -e HARBOROPS_ALLOW_ACCOUNT_CLI=true api --account-cli provision operator@example.test "Operator"`. The password is read privately from the terminal, never passed as a CLI argument. Reset through `--account-cli reset operator@example.test`. No public registration endpoint exists.

### Next milestone: M1 authentication design

The M1 architecture is **approved for implementation**, but no M1 source code, database migration or G1 verification has been completed. D-001 selects PostgreSQL-authoritative ASP.NET Core cookie sessions, Argon2id local credentials, a same-origin browser/API proxy, CSRF protection, immediate revocation and persistent Data Protection key storage.

- [M1 Authentication and Session Lifecycle Design](docs/M1_AUTHENTICATION_DESIGN.md): schema, endpoints, security behavior, frontend and work packages.
- [D-001 Architecture Decision](docs/ADR_D001_BROWSER_SESSIONS.md): selected model, alternatives and consequences.
- [M1 G1 Verification Plan](docs/M1_VERIFICATION_PLAN.md): API, database, browser, Compose, security and negative-case evidence.

Organization memberships and authorization remain M2. Historical-volume checks occur at quarter-mile milestones; dedicated accessibility testing remains M24-04. G0 stays accepted, while G1 is open.

### Planned modules

| Module | Scope |
| --- | --- |
| Shipment Operations | Shipments, containers, ports, voyages, assignments, milestones |
| Event Tracking | Event intake, normalization, idempotency, history, exception detection |
| Operations Dashboard | Search, maps, route state, KPIs, alerts, CSV export |
| Identity & Security | Login, role- and organization-scoped access, auditing, defensive controls |
| Infrastructure Operations | Runtime health, telemetry, diagnostics, backup and recovery |
| IT Service Desk | Incidents, priorities, root-cause analysis, remediation tracking |

### Proposed stack

- Web: React, TypeScript, Vite, Tailwind CSS, MapLibre GL JS
- API: ASP.NET Core on .NET 10, C#; OpenAPI generation is planned
- Data: PostgreSQL, Entity Framework Core
- Processing: .NET background worker with durable processing state
- Local runtime: Docker Compose; same-origin Nginx/Vite API proxy planned for M1 browser authentication
- Observability: OpenTelemetry, Prometheus, Grafana, structured logs
- Validation: xUnit, integration tests, Playwright, k6, OWASP ZAP
- Hosting: local-first; optional student-credit cloud environment and static demonstration

A modular monolith plus a background worker is the initial architecture. Distributed services, live commercial feeds, and continuous paid infrastructure are not prerequisites.

### Delivery sequence

M0 through M24 follow the evidence-gated [Delivery Roadmap](docs/ROADMAP.md). The [Execution Graph](docs/EXECUTION_GRAPH.md) establishes 101 scoped work packages, 25 evidence gates, and explicit prerequisites. Previous seven-milestone task IDs have been superseded; see the roadmap migration table. Milestone sequencing is dependency based, not a calendar deadline. Current delivery status is recorded only in [Project Status](docs/STATUS.md).

### Local M0 startup

Prerequisites: Docker Engine with Compose, Node.js 22, and .NET 10 SDK for standalone development.

1. Copy `.env.example` to `.env` and set a unique `HARBOROPS_DB_PASSWORD`.
2. Run `docker compose up -d --build` from the repository root.
3. Check `http://localhost:8080/health/live`, `http://localhost:8080/health/ready`, and `http://localhost:8080/api/v1/system/baseline`.
4. Open `http://localhost:5173` for the read-only system baseline.
5. For separate disposable verification, run `bash scripts/smoke-compose.sh --project ho-verify --confirm-disposable` with the local test password configured. This builds source-revision-labeled images and checks the isolated stack.
6. On that disposable test stack only, run `bash scripts/check-m0-degraded.sh --project ho-verify --confirm-local-outage` to test database interruption and recovery. These scripts refuse the primary `harborops` project. The script temporarily stops the PostgreSQL service and restores it on exit.
7. Use `docker compose down` to stop services. Add `-v` only to intentionally remove the local database.

The web image installs from the committed `web/package-lock.json` with `npm ci`. Run `bash scripts/verify-local.sh` with a disposable `HARBOROPS_TEST_DB` value for the full build and test sequence. Compose uses a one-shot migrator and a synthetic baseline seed, and does not run a tracking worker or ship production data. Integration tests only run against a loopback PostgreSQL database named `harborops_integration` or `harborops_test`. See [M0 Verification](docs/M0_VERIFICATION.md) for environment details, evidence and limitations.


### Documentation index

| Document | Purpose |
| --- | --- |
| [Product Requirements Document](docs/PRD.md) | Authoritative product vision, scope, and outcomes |
| [Delivery Roadmap](docs/ROADMAP.md) | Authoritative milestone plan and gates |
| [Execution Graph](docs/EXECUTION_GRAPH.md) | Generated execution dependencies and work packages |
| [Execution Graph Data](docs/execution-graph.json) | Canonical dependency edges and node IDs |
| [Project Status](docs/STATUS.md) | Authoritative implementation, verification, and acceptance state |
| [Local Storage Policy](docs/LOCAL_STORAGE_POLICY.md) | Mandatory E-drive workspace, caches, runtime data and evidence locations |
| [Product Scope](docs/PRODUCT_SCOPE.md) | Goals, non-goals, personas, and constraints |
| [Requirements](docs/REQUIREMENTS.md) | Functional and non-functional requirements |
| [Architecture](docs/ARCHITECTURE.md) | System boundaries, components, and design rules |
| [Domain Model](docs/DOMAIN_MODEL.md) | Entities, relationships, and invariants |
| [API Contracts](docs/API_CONTRACTS.md) | Proposed REST resources, security, and compatibility |
| [M1 Authentication Design](docs/M1_AUTHENTICATION_DESIGN.md) | Approved D-001 implementation blueprint; M1 not started |
| [M1 Verification Plan](docs/M1_VERIFICATION_PLAN.md) | G1 checks and evidence requirements, not executed |
| [Event Processing](docs/EVENT_PROCESSING.md) | Tracking workflow, failure modes, and reconciliation |
| [Security](docs/SECURITY.md) | Threat model and security acceptance gates |
| [Operations](docs/OPERATIONS.md) | Monitoring, incidents, backups, and recovery |
| [Testing Strategy](docs/TEST_STRATEGY.md) | Test layers, fixtures, and quality gates |
| [Implementation Notes](docs/IMPLEMENTATION_PLAN.md) | Supporting development method, not a separate roadmap |
| [Acceptance Criteria](docs/ACCEPTANCE_CRITERIA.md) | Release and milestone readiness requirements |
| [Cost and Deployment](docs/COST_AND_DEPLOYMENT.md) | Zero-cost baseline and hosted-demo constraints |
| [Demo Scenarios](docs/DEMO_SCENARIOS.md) | Reproducible product walkthroughs |
| [Decisions](docs/DECISIONS.md) | Architecture decisions and pending choices |
| [Contributing](docs/CONTRIBUTING.md) | Working conventions and documentation hygiene |

### Operating principles

1. Prefer correctness and maintainability over an unnecessarily complex feature set.
2. Use synthetic or properly licensed datasets. Mark simulated vessel positions and ETA outputs.
3. Never claim standards certification or external API compatibility before contract tests support it.
4. Never treat UI-level restrictions as a substitute for server-side authorization.
5. Keep local development free of mandatory paid services; guard hosted usage against charges.
6. Record verification evidence separately from implementation claims.
7. Keep documentation aligned with implemented behavior.

### Project boundaries

HarborOps is an independent demonstration platform. It does not claim affiliation with any shipping line, authority, port operator, or industry standards organization. Use of third-party identifiers, geographic data, map tiles, and specifications is subject to the applicable rights and service policies.

### M0 verification evidence

The [M0 Hardening Verification register](docs/M0_HARDENING_VERIFICATION.md) preserves the complete 2026-10-31 G0 evidence. The successful GitHub Actions run covered 1 unit, 10 contract, 5 fresh and 5 repeat integration, 6 web and 12 browser tests, Compose fault/recovery and three high/critical image scans. Existing local Docker volumes are not M0 gate requirements. Accessibility testing occurs only at M24-04.

### Current state

The default `main` branch contains the accepted M0 engineering foundation. G0 was verified on the hardening branch and merged via PRs #3 and #1. The code includes .NET 10, PostgreSQL 16, a read-only baseline API, deterministic seed, React status UI and Docker Compose. Full shipment, tracking, analytics and incident workflows remain planned for M1-M24. No public or production deployment is claimed. See [Project Status](docs/STATUS.md).
