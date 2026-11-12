# HarborOps

**M17 planning checkpoint (2026-11-12):** M17 observability has an end-to-end proposed implementation and G17 verification package, including 25 unexecuted scenarios and 75 unexecuted atomic assertions. P01-P09 design policies were approved on 2026-11-12. P02/P04/P09 numerical limits remain provisional pending measurement, and P05 initial local alert rules require calibration. **G17 remains not accepted and no M17 runtime feature is installed or activated by this documentation.** [Planning status](docs/M17_PLANNING_STATUS.md) | [Architecture](docs/M17_ARCHITECTURE_DESIGN.md) | [Implementation](docs/M17_IMPLEMENTATION_PLAN.md) | [G17 matrix](docs/M17_VERIFICATION_PLAN.md).

**Turkish interface integration (2026-11-12):** Accepted and merged via [PR #48](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/48) at `c81e54bfef25a0ff957f27b488ab7bd420551244`. Source-locked [run #43199664246](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43199664246) passed 41/41 frontend and 99/99 three-browser checks. G13 conditional and G15 accepted gate dispositions remain unchanged; no G24 release or production rollout is authorized. [Integration acceptance](docs/TURKISH_G15_S25_POSTMERGE_ACCEPTANCE_20261112.md).

**Current G15 state (2026-11-12):** G15 and G15-S25 are **ACCEPTED** and merged into `main` through [PR #44](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/44) at `49934d793542ef43cfe587c54b16cfd5cce8e2f4`. Tested executable SHA `ebb28d2332ab6e7eb4264265db3ae35e8fbcbc35` passed [run #43178122017](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43178122017). Reviewer independence is an expressly disclosed acceptance exception, not an attested credential. G13 stays conditionally integrated; M15-P10 fail-closed and P12 no-deletion policies remain, delivery is default-off, and no deployment or final release is authorized. See [final decision](docs/G15_S25_FINAL_ACCEPTANCE_20261112.md) and [post-merge reconciliation](docs/G15_S25_POSTMERGE_ACCEPTANCE_RECONCILIATION_20261112.md).


## Enterprise Maritime Logistics & IT Operations Platform

HarborOps is a modular enterprise platform for shipment and container management, maritime tracking events, operational analytics, infrastructure observability, security controls, and IT incident handling.

**Status (2026-11-12):** G13 remains conditionally accepted and merged via PR #38; G15 is explicitly accepted and merged via PR #44. The reviewer-separation procedural exception remains disclosed. Production deployment and retention deletion are not authorized.

**Final merge-readiness update (2026-11-07):** Two review findings are closed in source and verified on `e3002bd9ea38a20bd4c6b12027c6ecdcdf5fb343` ([record](docs/M10_G10_FINAL_MERGE_READINESS_20261107.md)): the accepted twelfth migration is restored unchanged and the NULL-safe projector scope function now arrives through the thirteenth migration, and the default `compose.yaml` requires `HARBOROPS_M10_CORRECTION_PROOF_KEY` for the migrator and API only. Targeted results: unit 239/239, contract 39/39, projector scope tests 3/3, change-impact integration set 126/126, default-stack correction flow 11/11, six projector mutants killed. The full integration suite, real-stack check and browser run were not repeated on this source. At this intermediate source-review checkpoint, G10-S25 and S01 were not yet executed. Both were subsequently reconciled and G10 accepted on 2026-11-07; the G10 gate closure is authoritative.

**Source availability:** Public since 2026-11-03 under the [inspection-only license](LICENSE). Public source code is not a hosted service. `main` is protected. Hosted verification is restricted to separately authorized workflows; there is no standing permission to run new workflows. See [Publication Safety Status](docs/PUBLICATION_SAFETY_STATUS.md).

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

M4-M10 and M16 are accepted and integrated into `main`. The [G10 closure](docs/M10_G10_GATE_CLOSURE_20261107.md) preserves source-lock and supplemental verification limits; PR #23 delivered the original M10 source and [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) integrated its required security remediation. M9 transport-event processing remains held.

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
- Observability: structured JSON logs and current health endpoints exist; OpenTelemetry, Prometheus and Grafana are proposed for M17 and are not yet integrated
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

### M9 idempotent worker and reconciliation (accepted and merged)

M9 adds fenced PostgreSQL leases, bounded retries, receipt-unique immutable effects and scoped operator redrive. [G9 execution](docs/M9_EXECUTED_VERIFICATION.md) passed 809/809 tests and 14/14 mutation families; [browser verification](docs/M9_BROWSER_SUPPLEMENT.md) passed 174/174 executions. All six [M9-01 choices](docs/M9_LOCAL_CONTRACT_DECISION.md) and G9 were accepted on 2026-11-05, with a hold on sustained transport-event processing until M10 or another explicitly approved backlog mechanism exists. [G9 gate closure](docs/M9_G9_GATE_CLOSURE.md) records evidence review, limitations and PR #16/#18 merge. No public or production deployment authorized. M10 still owns projections.

### M16 audit logging and access hardening (accepted and merged)

M16 adds append-only transaction-coupled audit events, scoped Auditor-only read APIs, actor attribution, restricted API/worker database identities and 365-day online-retention policy with future controlled archival. The standalone G16 evidence review checked 162 files and the S07/S08/S28 supplement was subsequently reviewed as 6/6 consistent with a provenance caveat. The accepted combined G16 runtime (`79318c6`) passed 228 unit, 39 contract and 286 integration tests twice, three browser engines (61/61 each in a same-source supplement), and targeted security/upgrade/outage checks. Final F001 security correction `8ba7727` passed 17 focused and 286 full integration tests. [G16 closure](docs/M16_G16_GATE_CLOSURE.md), [combined verification](docs/M9_M16_COMBINED_VERIFICATION.md) and merged [PR #25](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/25) record the outcome. M9 worker history remains the authoritative worker audit record. No deployment or sustained transport-event processing is authorized.

### M10 event timeline and projections (G10 accepted, integrated into `main`)

The [merged M10 implementation PR #23](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/23) adds an immutable indexed timeline, shipment tracking projections, Manager-only append-only corrections, a restricted optional projector and read-only shipment observations. Its [two source audits](docs/M10_SECOND_CORRECTNESS_AUDIT.md) and [remediation record](docs/M10_SECOND_AUDIT_REMEDIATION.md) are historical reviews, not runtime acceptance. The [M10/G16 reconciliation](docs/M10_M16_SOURCE_RECONCILIATION.md) joins that candidate to accepted G16 without rewriting G16 evidence. The M10/G16 combination has a completed source-locked 12-migration, restricted-role, concurrency and three-browser G10 technical campaign with [independently reviewed evidence](docs/M10_G10_INDEPENDENT_EVIDENCE_REVIEW.md), but no G10 acceptance. Neither G10 acceptance nor production deployment is authorized, and the M9 transport-backlog restriction remains binding. The approved L07/L14 remediation has a thirteenth migration, signed correction authority, removal of direct API correction INSERT, replay protection and transaction-coupled M16 correction audit. [Implementation record](docs/M10_G10_CORRECTION_AUTHORITY_IMPLEMENTATION.md) and [targeted verification plan](docs/M10_G10_L07_L14_REMEDIATION_PLAN.md) describe the new source. Its technical verification (build, restricted-login PostgreSQL tests, real-stack check, three-engine `tracking-timeline` run and 40 security mutants) is recorded in the [verification record](docs/M10_G10_REMEDIATION_VERIFICATION_20261107.md); independent review is not executed.

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
| [API Contracts](docs/API_CONTRACTS.md) | Accepted M0-M9 and M16 HTTP routes and planned future REST resources |
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
| [M5 Architecture](docs/M5_ARCHITECTURE_DESIGN.md) | Accepted shipment domain, database and HTTP contracts |
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
| [Documentation Status Guide](docs/DOCUMENT_STATUS_GUIDE.md) | Current-state sources versus historical milestone and evidence records |
| [Pre-release Documentation Checklist](docs/RELEASE_READINESS_TODO.md) | Open M24-03 reconciliation and release evidence tasks |

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

The [original security review](docs/PUBLIC_REPOSITORY_SECURITY_AUDIT_2026-11-02.md) preserves the initial findings. The [publication remediation register](docs/PUBLICATION_REMEDIATION_STATUS.md) and [publication safety status](docs/PUBLICATION_SAFETY_STATUS.md) record merged security hardening, successful local verification, completed artifact cleanup and public source visibility. This repository is public for source inspection only; **internet-facing application hosting is not approved**. The original Git history is visible. No further GitHub Actions runs are authorized by this documentation; prior explicitly approved verification runs remain part of the evidence record.

### Current state

M0-M12/G0-G12 and M16/G16 are accepted and integrated into `main` as of 2026-11-08. The [G11/G12 gate closure](docs/M11_M12_G11_G12_GATE_CLOSURE_20261108.md) and [PR #33](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/33) record the accepted integration. M10 projection remains default-off, ETA stays null and M11 evaluation remains opt-in. The M9 10,000-item transport hold is binding. No public hosting, production migration or release is authorized.
