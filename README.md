# HarborOps
## Enterprise Maritime Logistics & IT Operations Platform

HarborOps is a modular enterprise platform for shipment and container management, maritime tracking events, operational analytics, infrastructure observability, security controls, and IT incident handling.

**Status:** Planning baseline on the default branch. The unmerged M0 implementation on `feat/m0-engineering-baseline` has a documented runtime verification result: G0 verified, acceptance pending. All described capabilities are requirements or targets unless evidenced in [Project Status](docs/STATUS.md).

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
- API: ASP.NET Core on .NET 8, C#, OpenAPI
- Data: PostgreSQL, Entity Framework Core
- Processing: .NET background worker with durable processing state
- Local runtime: Docker Compose; reverse proxy optional
- Observability: OpenTelemetry, Prometheus, Grafana, structured logs
- Validation: xUnit, integration tests, Playwright, k6, OWASP ZAP
- Hosting: local-first; optional student-credit cloud environment and static demonstration

A modular monolith plus a background worker is the initial architecture. Distributed services, live commercial feeds, and continuous paid infrastructure are not prerequisites.

### Delivery sequence

M0 through M24 follow the evidence-gated [Delivery Roadmap](docs/ROADMAP.md). The [Execution Graph](docs/EXECUTION_GRAPH.md) establishes 101 scoped work packages, 25 evidence gates, and explicit prerequisites. Previous seven-milestone task IDs have been superseded; see the roadmap migration table. Milestone sequencing is dependency based, not a calendar deadline. Current delivery status is recorded only in [Project Status](docs/STATUS.md).

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

### Current state

The default branch remains a documentation baseline. On the unmerged `feat/m0-engineering-baseline` branch, M0 was verified locally against code revision `4fb34eb`, including builds, migrations, Compose startup, browser behavior and fault recovery. G0 is verified, acceptance pending. No live or hosted deployment was claimed. See [Project Status](docs/STATUS.md) for evidence before treating any milestone as complete.
