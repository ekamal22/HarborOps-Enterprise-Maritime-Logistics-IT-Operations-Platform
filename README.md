# HarborOps
## Enterprise Maritime Logistics & IT Operations Platform

HarborOps is a modular enterprise platform for shipment and container management, maritime tracking events, operational analytics, infrastructure observability, security controls, and IT incident handling.

**Status:** The hardened M0 code passed the complete GitHub Actions verification at revision `fec7429415fede121db8a4a34f0df72cd4fac609` ([run #2](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/41134743879)). **G0 verified, acceptance pending.** The earlier cold-database regression is resolved. Shipment, tracking, analytics and incident capabilities remain planned. See [Project Status](docs/STATUS.md).

HarborOps is designed to run locally with free, self-hosted development tools. Optional hosted demonstrations must remain within explicitly approved free tiers, credits, and operating limits.

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
- API: ASP.NET Core on .NET 10, C#, OpenAPI
- Data: PostgreSQL, Entity Framework Core
- Processing: .NET background worker with durable processing state
- Local runtime: Docker Compose; reverse proxy optional
- Observability: OpenTelemetry, Prometheus, Grafana, structured logs
- Validation: xUnit, integration tests, Playwright, k6, OWASP ZAP
- Hosting: local-first; optional student-credit cloud environment and static demonstration

A modular monolith plus a background worker is the initial architecture. Distributed services, live commercial feeds, and continuous paid infrastructure are not prerequisites.

### Delivery sequence

The planning branch `docs/product-delivery-source-of-truth` defines the revised 25-milestone execution sequence (M0-M24) with a separate G0-G24 gate for each milestone. Review the [granular roadmap](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/docs/product-delivery-source-of-truth/docs/ROADMAP.md) and [execution graph](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/docs/product-delivery-source-of-truth/docs/EXECUTION_GRAPH.md).

The old M1-M6 milestone IDs were superseded by the granular plan. M0-01 through M0-05 and G0 retain their meaning. This branch implements the M0 engineering foundation only. All subsequent business capabilities are planned.

### Mandatory E-drive storage

All persistent HarborOps development data must stay on E:, including the working tree at `E:\\APP IDEAS\\HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform`, caches and M0 evidence under `E:\\APP IDEAS\\harborops-m0-cache`, and Docker Desktop/WSL data and volumes. See [Local Storage Policy](docs/LOCAL_STORAGE_POLICY.md) for PowerShell session setup and preflight checks. C: is reserved for unavoidable short-lived Windows temporary writes only. Do not create new persistent project storage on C:.

### Pending M0 hardening verification

The initial hardening audit demonstrated .NET 10 compilation, database startup and copied-volume compatibility, clean final image scans and 12 browser passes, but also exposed a 4/5 fresh-database failure and a 4.24:1 contrast issue. Fixes are now committed. The corrected code **passed** cold-state tests, fresh disposable database startup, image scans and the functional browser matrix at the verified revision. Existing local Docker volume compatibility is not a G0 requirement; lightweight compatibility checkpoints belong to M6, M12, M18 and M24-03, with the full recovery/migration rehearsal at M19. Review [Hardening Verification](docs/M0_HARDENING_VERIFICATION.md) before considering this revision verified. The browser runner stores npm cache and browser binaries on E: and uses a committed `tests/browser/package-lock.json` that still requires a fresh `npm ci` check.

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
| [Project Status](docs/STATUS.md) | Implemented, pending, and verified work |
| [Local Storage Policy](docs/LOCAL_STORAGE_POLICY.md) | E-drive workspace, caches, Docker and evidence constraints |
| [M0 Verification](docs/M0_VERIFICATION.md) | Previous baseline's executed test evidence |
| [M0 Hardening Verification](docs/M0_HARDENING_VERIFICATION.md) | Passed .NET 10, fresh PostgreSQL, Docker recovery, functional browser and image verification |
| [Product Scope](docs/PRODUCT_SCOPE.md) | Goals, non-goals, personas, and constraints |
| [Requirements](docs/REQUIREMENTS.md) | Functional and non-functional requirements |
| [Architecture](docs/ARCHITECTURE.md) | System boundaries, components, and design rules |
| [Domain Model](docs/DOMAIN_MODEL.md) | Entities, relationships, and invariants |
| [API Contracts](docs/API_CONTRACTS.md) | Proposed REST resources, security, and compatibility |
| [Event Processing](docs/EVENT_PROCESSING.md) | Tracking workflow, failure modes, and reconciliation |
| [Security](docs/SECURITY.md) | Threat model and security acceptance gates |
| [Operations](docs/OPERATIONS.md) | Monitoring, incidents, backups, and recovery |
| [Testing Strategy](docs/TEST_STRATEGY.md) | Test layers, fixtures, and quality gates |
| [Implementation Plan](docs/IMPLEMENTATION_PLAN.md) | Milestones, dependencies, and work packages |
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

The hardened M0 foundation passed the regression and image checks in GitHub Actions run #2. **G0 is verified, acceptance pending** until explicitly accepted. Do not treat later roadmap capabilities as implemented. See [Project Status](docs/STATUS.md) and [M0 Verification](docs/M0_VERIFICATION.md).
