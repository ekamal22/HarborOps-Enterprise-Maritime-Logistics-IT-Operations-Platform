# HarborOps
## Enterprise Maritime Logistics & IT Operations Platform

HarborOps is a modular enterprise platform for shipment and container management, maritime tracking events, operational analytics, infrastructure observability, security controls, and IT incident handling.

**Status:** M0/G0 through M3/G3 are verified, explicitly accepted and integrated into `main` as of 2026-10-31. G3 passed all 25 local scenarios on `b72025d35d62d0a8a45b466986a0b3bd89eebadb`; the independent [executed verification record](docs/M3_EXECUTED_VERIFICATION.md) preserves the test results. M4 is next, with D-003 container identifier policy unresolved. G4-G24 are open. See [Project Status](docs/STATUS.md).

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

### Accepted M1 authentication and sessions

M1 is integrated into `main` with server-authoritative cookie sessions, Argon2id credentials, controlled local account provisioning, identity tables and a functional browser sign-in shell. Both M1 verification runs [41143665657](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41143665657) and [41144356898](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41144356898) passed. G1 was explicitly accepted on 2026-10-31; merged revision: `8f9fc0d506a9e1dc1d4f5acab936622df988f413`.

**Local M1 accounts:** Start Compose with `docker compose up -d --build` as described below. Create an account with `docker compose run --rm -it -e HARBOROPS_ALLOW_ACCOUNT_CLI=true api --account-cli provision operator@example.test "Operator"`. The password is read privately from the terminal, never passed as a CLI argument. Reset through `--account-cli reset operator@example.test`. No public registration endpoint exists.

### M1 acceptance evidence

The M1 architecture is implemented and its engineering verification passed in [Verify M1 run](docs/M1_IMPLEMENTATION_VERIFICATION.md). G1 is accepted and the merged `main` branch includes M0 and M1. D-001 selects PostgreSQL-authoritative ASP.NET Core cookie sessions, Argon2id local credentials, a same-origin browser/API proxy, CSRF protection, immediate revocation and persistent Data Protection key storage.

- [M1 Authentication and Session Lifecycle Design](docs/M1_AUTHENTICATION_DESIGN.md): schema, endpoints, security behavior, frontend and work packages.
- [D-001 Architecture Decision](docs/ADR_D001_BROWSER_SESSIONS.md): selected model, alternatives and consequences.
- [M1 G1 Verification Plan](docs/M1_VERIFICATION_PLAN.md): API, database, browser, Compose, security and negative-case evidence.

Organization memberships and role-scoped authorization were delivered in M2 and are now on `main`. G0, G1 and G2 are accepted. Historical-volume checks remain at quarterly milestones; dedicated accessibility testing remains M24-04.

### Accepted milestone: M2 organizations and role isolation

M2 is merged into `main` through [PR #5](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/5) at `1df638d5300191f6c2c6c9d201fd5f4c4ab9e562`. All 25 G2 scenarios passed locally on `fbddc1361d79b027275bb53b48133ba0dc7d5cb1` (22 unit, 12 contract, 32 fresh and 32 repeated PostgreSQL integration, 6 frontend and 24 cross-browser tests, Compose/session recovery, scoped database outage recovery and three clean image scans). See the [executed verification record](docs/M2_EXECUTED_VERIFICATION.md), [architecture](docs/M2_ARCHITECTURE_DESIGN.md), [implementation record](docs/M2_IMPLEMENTATION_STATUS.md) and [G2 plan](docs/M2_VERIFICATION_PLAN.md). G2 was accepted on 2026-10-31.

### Accepted milestone: M3 ports and vessels reference data

M3 is implemented, locally verified, explicitly accepted on 2026-10-31 and integrated into `main` by fast-forward from `feat/m3-port-vessel-reference-data`. The additive PostgreSQL migration, scoped port/vessel APIs, React reference workspace, authorization and regression coverage all passed G3-S01 through G3-S25 on `b72025d35d62d0a8a45b466986a0b3bd89eebadb`. See the [executed verification](docs/M3_EXECUTED_VERIFICATION.md), [implementation register](docs/M3_IMPLEMENTATION_STATUS.md), [architecture](docs/M3_ARCHITECTURE_DESIGN.md), [plan](docs/M3_IMPLEMENTATION_PLAN.md) and [G3 specification](docs/M3_VERIFICATION_PLAN.md).

### Next milestone: M4 voyages and containers reference data

M4 is planned and has not started. Its entry gate G3 is accepted; D-003 container identifier policy must be resolved before applicable M4 work. No M4 implementation, G4 verification or production deployment is implied. See [Roadmap](docs/ROADMAP.md) and [Decisions](docs/DECISIONS.md).

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
- Local runtime: Docker Compose; same-origin Nginx/Vite API proxy implemented in `main`
- Observability: OpenTelemetry, Prometheus, Grafana, structured logs
- Validation: xUnit, integration tests, Playwright, k6, OWASP ZAP
- Hosting: local-first; optional student-credit cloud environment and static demonstration

A modular monolith plus a background worker is the initial architecture. Distributed services, live commercial feeds, and continuous paid infrastructure are not prerequisites.

### Delivery sequence

M0 through M24 follow the evidence-gated [Delivery Roadmap](docs/ROADMAP.md). The [Execution Graph](docs/EXECUTION_GRAPH.md) establishes 101 scoped work packages, 25 evidence gates, and explicit prerequisites. Previous seven-milestone task IDs have been superseded; see the roadmap migration table. Milestone sequencing is dependency based, not a calendar deadline. Current delivery status is recorded only in [Project Status](docs/STATUS.md).

### Local M0-M3 startup

Prerequisites: Docker Engine with Compose, Node.js 22 or later, and either the .NET 10 SDK for standalone development or an E:-backed local .NET 10 SDK container.

1. Copy `.env.example` to `.env` and set a unique `HARBOROPS_DB_PASSWORD`.
2. Run `docker compose up -d --build` from the repository root.
3. Check `http://localhost:8080/health/live`, `http://localhost:8080/health/ready`, and `http://localhost:8080/api/v1/system/baseline`.
4. Open `http://localhost:5173` for the baseline, sign-in interface and M2 organization workspace. Provision a local account as described above.
5. After migrations and account provisioning, bootstrap an organization through the restricted local CLI, for example `docker compose run --rm -it -e HARBOROPS_ALLOW_ORGANIZATION_CLI=true api --organization-cli bootstrap north-terminal "North Terminal" operator@example.test`. Organization membership is required before scoped operations; no organization is created by public registration.
6. For separate disposable verification, run `bash scripts/smoke-compose.sh --project ho-verify --confirm-disposable` with the local test password configured. This builds source-revision-labeled images and checks the isolated stack.
7. On that disposable test stack only, run `bash scripts/check-m0-degraded.sh --project ho-verify --confirm-local-outage` to test database interruption and recovery. These scripts refuse the primary `harborops` project. The script temporarily stops the PostgreSQL service and restores it on exit.
8. Use `docker compose down` to stop services. Add `-v` only if you intend to remove both the local database **and** persistent authentication key-ring volumes, invalidating existing cookies.

The web image installs from the committed `web/package-lock.json` with `npm ci`. For M0 baseline checks, `scripts/verify-local.sh` remains available. The complete G2-local verification command is `bash scripts/verify-m2-local.sh preflight dotnet deps web docs stack accounts organizations session browsers recovery scan` with the required E:-only storage configuration and disposable resources; see [M2 executed evidence](docs/M2_EXECUTED_VERIFICATION.md) and [Local Storage Policy](docs/LOCAL_STORAGE_POLICY.md) before invoking it. No GitHub Actions executions are authorized. Compose uses a one-shot migrator and a synthetic baseline seed; it does not ship a tracking worker or production data. Integration tests use isolated PostgreSQL databases. Do not run destructive checks on the retained primary stack.


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
| [API Contracts](docs/API_CONTRACTS.md) | Implemented M0-M3 HTTP routes and planned future REST resources |
| [M1 Authentication Design](docs/M1_AUTHENTICATION_DESIGN.md) | Approved D-001 implementation blueprint; M1 accepted on `main` |
| [M1 Verification Plan](docs/M1_VERIFICATION_PLAN.md) | G1 checks and evidence requirements; executed M1 evidence is recorded separately |
| [M1 Verification Register](docs/M1_IMPLEMENTATION_VERIFICATION.md) | Executed M1 checks, exact revisions, G1 acceptance and merge record |
| [M2 Architecture Design](docs/M2_ARCHITECTURE_DESIGN.md) | Implemented organization, membership, role and scope model |
| [M2 Implementation Plan](docs/M2_IMPLEMENTATION_PLAN.md) | Completed M2-01 through M2-04 work packages |
| [M2 Implementation Register](docs/M2_IMPLEMENTATION_STATUS.md) | M2 source paths and verified behavior |
| [M2 G2 Verification Plan](docs/M2_VERIFICATION_PLAN.md) | All 25 mandatory G2 scenarios |
| [M2 Executed Verification](docs/M2_EXECUTED_VERIFICATION.md) | Local E:-only results and G2 acceptance record |
| [M3 Architecture Design](docs/M3_ARCHITECTURE_DESIGN.md) | Tenant-owned port/vessel models, permissions and API contracts |
| [M3 Implementation Plan](docs/M3_IMPLEMENTATION_PLAN.md) | M3-01 to M3-04 work packages and dependency sequence |
| [G3 Verification Plan](docs/M3_VERIFICATION_PLAN.md) | Required G3-S01 to G3-S25 local tests, all executed and passed |
| [M3 Implementation Register](docs/M3_IMPLEMENTATION_STATUS.md) | Accepted M3 implementation and source inventory |
| [M3 Executed Verification](docs/M3_EXECUTED_VERIFICATION.md) | Verified G3 execution evidence, failure history and acceptance record |
| [M3 Correctness Remediation](docs/M3_CORRECTNESS_REMEDIATION.md) | Historical first-audit fixes and subsequent verified G3 outcome |
| [Second M3 Audit Remediation](docs/M3_SECOND_CORRECTNESS_REMEDIATION.md) | Historical second-audit fixes and subsequent verified G3 outcome |
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

The default `main` branch contains accepted M0-M3 implementation, tests and documentation. G3 was independently reviewed and explicitly accepted on 2026-10-31 after the successful local run on `b72025d35d62d0a8a45b466986a0b3bd89eebadb`. Future voyages, containers, shipments, tracking, analytics and incident workflows remain planned for M4-M24. No public or production deployment is claimed. See [Project Status](docs/STATUS.md).
