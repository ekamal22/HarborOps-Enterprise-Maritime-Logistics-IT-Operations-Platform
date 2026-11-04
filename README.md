# HarborOps
## Enterprise Maritime Logistics & IT Operations Platform

HarborOps is a modular enterprise platform for shipment and container management, maritime tracking events, operational analytics, infrastructure observability, security controls, and IT incident handling.

**Status:** M0/G0 through M8/G8 accepted and integrated into `main` through 2026-11-04. A corrected M9 implementation candidate exists only on [draft PR #18](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/18); G9 verification and acceptance remain pending. M8 was merged into protected `main` through [PR #14](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/14) at `1caa3daa68bfecfd19a671aa2e19d39c17224d75` after independent review. M7 verification reconciled 25/25 scenarios through the original source-locked run and three reviewed supplements; see [G7 closure](docs/M7_G7_GATE_CLOSURE.md). The accepted M8 implementation acknowledges validated tracking receipts only after durable PostgreSQL commit. No production deployment; G9-G24 remain open.

**Source availability:** Public since 2026-11-03 under the [inspection-only license](LICENSE). Public source code is not a hosted service. GitHub Actions is disabled and `main` is protected. See [Publication Safety Status](docs/PUBLICATION_SAFETY_STATUS.md).

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

### Parallel-ready workstreams after G3

M4 maritime records, M5 shipments and M6 assignments are accepted and integrated. Independently eligible future streams include M7 tracking contracts, M16 audit foundations, M17 observability, M19 recovery, M20-01 security review and M23-01 interface planning. D-003 is accepted for M4; D-002 is accepted for M7 (Option A, 2026-11-03). See [Parallel Delivery Policy](docs/PARALLEL_DELIVERY_POLICY.md) and [Roadmap](docs/ROADMAP.md).

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

### Accepted M4 voyage and container reference data

M4 implements tenant-scoped voyage schedules and container references. D-003 Option B (ISO 6346:2022 category U or tagged synthetic identifiers) and G4 were explicitly accepted on 2026-11-01 following independent review. The final tested code revision `600d7c787da9f9cd4a70225cb3e3c38c093c0dc6` passed 82 unit, 12 contract, 92 fresh and 92 repeat PostgreSQL integration tests, and 31 browser tests in each of Chromium, Firefox and WebKit, plus outage/recovery and three clear HIGH/CRITICAL image scans. See [executed verification](docs/M4_EXECUTED_VERIFICATION.md), [architecture](docs/M4_ARCHITECTURE_DESIGN.md), [implementation record](docs/M4_IMPLEMENTATION_STATUS.md), [G4 plan](docs/M4_VERIFICATION_PLAN.md), and [D-003 decision](docs/DECISIONS.md).

### Accepted M5 shipment lifecycle

M5 supports tenant-scoped shipment create, edit, search, archive and restore, a manual workflow with immutable history, scoped permissions and exact-version concurrency. The six-rule [M5 local contract](docs/M5_LOCAL_CONTRACT_DECISION.md) and G5 were explicitly accepted 2026-11-02. Local run 13 at `3e57af490d9a28dd1f0c482b6d20cd44f8a9fc91` passed 601 test executions, including 47 browser scenarios per engine in Chromium, Firefox and WebKit, with independently reviewed evidence. See [G5 closure](docs/M5_G5_GATE_CLOSURE.md), [verification](docs/M5_EXECUTED_VERIFICATION.md) and [implementation status](docs/M5_IMPLEMENTATION_STATUS.md). M6 owns shipment assignment links.

### M6 shipment assignments (verified, accepted and integrated)

M6 implements tenant-safe shipment-container and shipment-voyage allocation history, UTC half-open intervals, PostgreSQL overlap exclusions, versioned closure, parent lifecycle guards, scoped APIs and a React allocation workspace. Source-locked verification on `a94d7cd406a93806527d28ba7d2ce2d6ba3307b6` passed 706 tests with zero failures or skips and four effective security mutations. G6-S01 was subsequently closed by approval of the complete [twelve-rule contract](docs/M6_LOCAL_CONTRACT_DECISION.md) and G6 on 2026-11-02. The original evidence assertion exit 1 remains preserved; the M0-only historical snapshot limitation is documented. See [G6 gate closure](docs/M6_G6_GATE_CLOSURE.md) and [executed verification](docs/M6_EXECUTED_VERIFICATION.md).

### Accepted M7 tracking contracts and source authentication

The [M7 architecture](docs/M7_ARCHITECTURE_DESIGN.md), [implementation plan](docs/M7_IMPLEMENTATION_PLAN.md), [D-002 reference proposal](docs/M7_D002_TRACKING_REFERENCE_PROPOSAL.md) and [G7 verification matrix](docs/M7_VERIFICATION_PLAN.md) define secure source credentials and a bounded, versioned tracking-event adapter, implemented on the feature branch `feat/m7-tracking-source-validation`. The [executed verification record](docs/M7_EXECUTED_VERIFICATION.md) documents a complete local run on `7b87f3250aba677b1f4aeef4350803d9ca7b6880`: 189 unit, 39 contract and 210 fresh plus 210 repeated PostgreSQL integration tests, 58 browser tests in each of Chromium, Firefox and WebKit, five effective security mutations and no HIGH or CRITICAL image findings. After supplements for the protected-terminal source lifecycle (G7-S06), evidence reconciliation (G7-S25) and the D-002 reference verification (G7-S01 and G7-S17), all 25 scenarios pass. D-002 is accepted as Option A: Track & Trace 2.2.0 is the reference baseline for a deliberately narrower HarborOps-specific profile that is not DCSA conformant, pinned as [local snapshots](third_party/dcsa-track-and-trace/README.md) that are not official immutable DCSA releases; DCSA-format events need a translation layer. G7 and M7 were explicitly accepted on 2026-11-03 and integrated through [PR #10](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/10) and [PR #11](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/11). No event is acknowledged before M8 durable receipt. See [G7 gate closure](docs/M7_G7_GATE_CLOSURE.md). See the [correctness remediation register](docs/M7_CORRECTNESS_REMEDIATION.md) for the defects that execution found.

### M8 durable receipt and immutable history (accepted)

[M8 architecture](docs/M8_ARCHITECTURE_DESIGN.md), [implementation plan](docs/M8_IMPLEMENTATION_PLAN.md), [D-006 proposal](docs/M8_D006_DURABILITY_PROPOSAL.md) and [G8 matrix](docs/M8_VERIFICATION_PLAN.md) describe PostgreSQL transactional receipts, immutable accepted events, source-scoped replay/collision handling and a separate Pending work queue. **The M8 implementation passed source-locked technical verification, independent sealed-evidence review and explicit D-006 Option A / G8 acceptance on 2026-11-04.** The accepted intake contract returns HTTP 202 only after a committed receipt transaction; M7 pre-receipt negative request behavior remains preserved. See [G8 gate closure](docs/M8_G8_GATE_CLOSURE.md) and [independent review](docs/M8_G8_INDEPENDENT_EVIDENCE_REVIEW.md). See [M8 implementation status](docs/M8_IMPLEMENTATION_STATUS.md), [full G8 verification runbook](docs/M8_G8_VERIFICATION_RUNBOOK.md) and [correctness remediation](docs/M8_CORRECTNESS_REMEDIATION.md), including the [second audit remediation](docs/M8_SECOND_REMEDIATION_STATUS.md). M9 owns worker claims/retries and dead-letter dispatch; M10 owns projections. See [M8 planning status](docs/M8_PLANNING_STATUS.md).

### M9 idempotent worker and reconciliation (unverified feature-branch candidate)

After accepted G8, [M9 architecture](docs/M9_ARCHITECTURE_DESIGN.md), [implementation plan](docs/M9_IMPLEMENTATION_PLAN.md), [local contract proposal](docs/M9_LOCAL_CONTRACT_PROPOSAL.md) and the [G9 verification matrix](docs/M9_VERIFICATION_PLAN.md) define a separate PostgreSQL-backed worker with expiring fenced leases, a unique immutable resolution effect per receipt, bounded retry/dead-letter processing and a scoped manual reconciliation path. M9 will replace only M8's Pending-only work-state guard through a new migration, while preserving the accepted receipt/event immutable history. Transport-to-voyage resolution and nonterminal backlog counting are explicit M9-01 choices, not assumed accepted behavior. **A corrected M9 implementation candidate is committed on [draft PR #18](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/18), not on accepted `main`.** The [implementation status](docs/M9_IMPLEMENTATION_STATUS.md) and [follow-up correctness audit](docs/M9_CORRECTNESS_AUDIT_20261104.md) document source changes and unexecuted tests. No M9 runtime verification has been performed; M9-01 contract and G9 acceptance remain pending. M10 retains shipment timelines and projections.

### Parallel dependency-based delivery

M0 through M24 follow the evidence-gated [Delivery Roadmap](docs/ROADMAP.md). The [Execution Graph](docs/EXECUTION_GRAPH.md) establishes 101 scoped work packages, 25 evidence gates, and explicit prerequisites. Previous seven-milestone task IDs have been superseded; see the roadmap migration table. Milestone IDs are not execution order: independently ready workstreams may run in parallel after their named upstream gates are accepted. The graph v3.0 defines work-package-level hard and integration dependencies; see [Parallel Delivery Policy](docs/PARALLEL_DELIVERY_POLICY.md). Current delivery status remains exclusive to [Project Status](docs/STATUS.md).

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
| [M5 Architecture](docs/M5_ARCHITECTURE_DESIGN.md) | Planned shipment domain, database and HTTP contracts |
| [M5 Implementation Plan](docs/M5_IMPLEMENTATION_PLAN.md) | Four work packages and integration strategy |
| [G5 Verification Plan](docs/M5_VERIFICATION_PLAN.md) | 25 mandatory scenarios and evidence |
| [M5 Contract Compliance](docs/M5_G5_S01_CONTRACT_COMPLIANCE.md) | Rule-by-rule audit of the accepted M5 contract and G5-S01 |
| [M5 Executed Verification](docs/M5_EXECUTED_VERIFICATION.md) | Accepted G5 execution, evidence and limitations |
| [M5/G5 Remediation](docs/M5_G5_REMEDIATION_STATUS.md) | Corrective source changes for all eleven findings, with execution update |
| [M5 Source Audit](docs/M5_SOURCE_CORRECTNESS_AUDIT.md) | Source corrections and remaining real-runtime / adversarial verification gaps |

| [M5 Planning Status](docs/M5_IMPLEMENTATION_STATUS.md) | Implemented, verified and accepted, G5 closed |
| [M6 Local Contract](docs/M6_LOCAL_CONTRACT_DECISION.md) | Accepted twelve-rule assignment contract |
| [M6 Architecture](docs/M6_ARCHITECTURE_DESIGN.md) | Shipment-to-container and shipment-to-voyage temporal association architecture |
| [M6 Implementation Plan](docs/M6_IMPLEMENTATION_PLAN.md) | M6-01 through M6-04 delivery ownership, sequencing and source inventory |
| [G6 Verification Plan](docs/M6_VERIFICATION_PLAN.md) | Accepted 25-scenario G6 gate and documented Q1 checkpoint |
| [M6 Planning Status](docs/M6_PLANNING_STATUS.md) | Completed delivery and acceptance decision |
| [M6 Implementation Status](docs/M6_IMPLEMENTATION_STATUS.md) | Accepted implementation and executed verification provenance |
| [M6 Executed Verification](docs/M6_EXECUTED_VERIFICATION.md) | Source-locked G6 executable evidence and historical snapshot limitation |
| [M6/G6 Gate Closure](docs/M6_G6_GATE_CLOSURE.md) | Explicit acceptance, G6-S01 decision and integration |
| [M4 Executed Verification](docs/M4_EXECUTED_VERIFICATION.md) | Accepted G4 evidence, D-003 closure, verified source SHA and immutable artifact hash |
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

### Public source and deployment security

The [original security review](docs/PUBLIC_REPOSITORY_SECURITY_AUDIT_2026-11-02.md) preserves the initial findings. The [publication remediation register](docs/PUBLICATION_REMEDIATION_STATUS.md) and [publication safety status](docs/PUBLICATION_SAFETY_STATUS.md) record merged security hardening, successful local verification, completed artifact cleanup and public source visibility. This repository is public for source inspection only; **internet-facing application hosting is not approved**. The original Git history is visible. GitHub Actions remains disabled; no workflow runs are authorized.

### Current state

M0-M7 are explicitly accepted and integrated into `main` as of 2026-11-03. M6 evidence is preserved in [G6 verification](docs/M6_EXECUTED_VERIFICATION.md). Durable tracking-event intake, processing, analytics and incident workflows remain scoped to later gates. The source repository is public, but the application has not been publicly hosted or deployed to production. See [Project Status](docs/STATUS.md).
