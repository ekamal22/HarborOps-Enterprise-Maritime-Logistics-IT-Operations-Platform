# Architecture

**Current M15 source status (2026-11-09):** M15's [latest standalone Actions run](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42542518598) passed source and PostgreSQL but failed one Chromium receipt selector (5/6 passed); Firefox/WebKit were not reached. Subsequent [correctness remediation](M15_CORRECTNESS_AUDIT_20261109.md) remains unverified on a fresh SHA. The separate [combined M13/M15 run](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42541135825) passed 312 unit, 52 contract, 37 selected PostgreSQL and 27 three-engine browser checks on a different earlier source. Neither G13 nor G15 is accepted, and M15-P01 through P14 require approval. Accepted `main` remains G0-G12 and G16. No merge or deployment.


**Current accepted baseline (2026-11-08):** G0-G12 and G16 are accepted and integrated into `main`. The [G11/G12 gate closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md) and [project status](STATUS.md) govern current M11 delay exceptions and M12 scoped search/export behavior. Earlier source-only and verification checkpoints are historical. The M9 ARRI/DEPA processing hold and 10,000 unresolved-item limit remain binding; M10 projector default-off, ETA null, M11 evaluator opt-in. No deployment or release is authorized.

**Accepted implementation:** G0-G7 were integrated by 2026-11-03; M8/G8 and D-006 Option A were accepted 2026-11-04 following [independent raw evidence review](M8_G8_INDEPENDENT_EVIDENCE_REVIEW.md). Durable intake returns HTTP 202 only after the atomic PostgreSQL receipt/event/Pending work commit. M7 negative validation and source credential safeguards remain intact. M9/G9 is accepted and integrated into `main`; its separate opt-in worker was integrated from [M9 draft PR #18](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/18), with [executed G9 technical evidence](M9_EXECUTED_VERIFICATION.md), a passing [browser supplement](M9_BROWSER_SUPPLEMENT.md), and all six [M9-01 contract decision](M9_LOCAL_CONTRACT_DECISION.md) choices approved with a transport-backlog condition. The formal G9 evidence review and gate acceptance are recorded in [G9 closure](M9_G9_GATE_CLOSURE.md). See [G8 gate closure](M8_G8_GATE_CLOSURE.md).

## M10 correction authority and audit integration, 2026-11-07

**M10/G10 checkpoint (2026-11-07):** Fourteen M10 local policies are approved (L07/L14 revised, L06 conditional and verified). Signed restricted correction authority, same-transaction M16 audit and upgrade-safe thirteenth migration are accepted and merged via PR #28. Original G10 evidence is source-locked to `3e81b88`, correction authority to `a26d19a`, and final historical upgrade and default Compose tests to `e3002bd`. See [G10 closure](M10_G10_GATE_CLOSURE_20261107.md).

The accepted correction-security layer adds an SQL-only thirteenth migration after the immutable original twelve, including the NULL-safe projector-scope function replacement. HTTP proof signing is separated from the restricted PostgreSQL verifier; the correction and M16 audit event commit atomically. The optional projector remains off by default and restricted. See [gate closure](M10_G10_GATE_CLOSURE_20261107.md).

## Accepted M11 delay and exception architecture

The accepted M11 implementation extends M5 UTC schedules and M10 equipment projections with a versioned rule engine, additive exception tables, signed SQL authority, scoped API and UI, M16 atomic audit and an opt-in bounded reevaluator. The [M11 architecture](M11_ARCHITECTURE_DESIGN.md), [work packages](M11_IMPLEMENTATION_PLAN.md) and [G11 evidence matrix](M11_G11_EVIDENCE_MATRIX_20261108.md) record its accepted scope and verification. The M9 transport hold and null ETA remain.

## Parallel implementation and integration contracts

The established modular monolith remains the single shared architecture. Workstream parallelism changes planning prerequisites, not deployment topology or transaction ownership. Independent milestone branches must preserve accepted M0-M7 authentication, tenant isolation, API envelopes, persistence constraints, shipment and allocation behavior. Shared PostgreSQL schema changes require one coordinated additive migration history and integrated regression. Event processing attaches only after the shipment and tracking contracts needed by its own dependent packages are accepted. Observability, audit foundations and recovery tooling may develop alongside business features, but G24 must recheck all integrated domains. See [Parallel Delivery Policy](PARALLEL_DELIVERY_POLICY.md) and [Execution Graph](EXECUTION_GRAPH.md).

## M4 accepted modular extension

M4 remains inside the accepted ASP.NET Core modular monolith, not a separate service. Implemented `Voyages` and `Containers` domain policies, EF stores, scoped endpoints and React workspaces reuse M1 authentication, M2 tenant selection, M3 reference UUIDs and established JSON/CSRF conventions. [M4 architecture](M4_ARCHITECTURE_DESIGN.md) calls for composite same-tenant PostgreSQL FKs, transaction-scope authorization and UTC invariants; [M4 implementation](M4_IMPLEMENTATION_PLAN.md) coordinates one additive migration, including when workstreams execute in parallel. [D-003 Option B](DECISIONS.md) was explicitly approved and the M4 runtime was implemented, independently verified and accepted on 2026-11-01.

## Accepted M5 shipment and M6 allocation modules

[M5 architecture](M5_ARCHITECTURE_DESIGN.md) defines a bounded module in the .NET 10 monolith with tenant-owned shipment records, manual status history, scoped API commands and a React workspace. [M6 architecture](M6_ARCHITECTURE_DESIGN.md) adds versioned, tenant-scoped shipment-container and shipment-voyage association histories and PostgreSQL temporal integrity constraints. Both modules are integrated into `main`; G5 and G6 were accepted on 2026-11-02. Neither milestone introduces production tracking ingestion, a new service or external integrations.

## Baseline topology

A modular monolith provides the HTTP API and transactional domain functions. A separate opt-in .NET worker is implemented in accepted `main` under M9, with PostgreSQL leases, bounded retries, immutable resolution effects and scoped operator redrive. G9 was accepted on 2026-11-05; the default Compose stack still does not start the worker. PostgreSQL is the authoritative persistence layer. React is the user interface. Local infrastructure is orchestrated through Docker Compose.

    Browser (React + TypeScript)
                |
          HTTPS / reverse proxy
                |
          ASP.NET Core API
          /      |       \
     Identity  Domain    Reporting
          \      |       /
               PostgreSQL
                   ^
                   |
        .NET Tracking Event Worker
                   ^
                   |
          Simulator / Adapter

    API + Worker -> OpenTelemetry -> Prometheus/Grafana + structured logs
    Monitoring signals -> Incident workflow

The diagram includes accepted M9 and M10 infrastructure and planned downstream observability and incident features. M0 supplied the API, DbContext/migrator, status UI and initial worker; subsequent accepted milestones added identity, shipment records, tracking intake and processing, the M10 timeline/projections, and M16 audit controls. The M9 transport hold remains until separately verified cap-safe backlog handling.

## M2 organization and authorization architecture (implemented, G2 accepted, merged to `main`)

M2 adds `organizations`, `organization_memberships` and a nullable selected-organization field in server-side `auth_sessions`, with role-based grants and permission evaluation fresh from PostgreSQL for every scoped request. The server, not the browser, binds authenticated users to an active selected organization. Organization detail and membership administration are the real scoped resources used to prove cross-tenant isolation before shipment entities exist.

The [M2 architecture](M2_ARCHITECTURE_DESIGN.md) specifies role and permission semantics, last-administrator invariants, transactional locks, session selection, API responses and frontend integration. The [M2 implementation](M2_IMPLEMENTATION_PLAN.md) is integrated into `main` via PR #5 and listed in the [implementation record](M2_IMPLEMENTATION_STATUS.md). G2 passed locally on the recorded source revision ([executed verification](M2_EXECUTED_VERIFICATION.md)) and was accepted on 2026-10-31. GitHub Actions remain prohibited until explicitly restored.

## M1 authentication architecture (implemented, accepted on main)

M1 uses server-managed browser sessions within the existing ASP.NET Core modular monolith. The React browser communicates over one origin through an Nginx production proxy or Vite development proxy for `/api/*`. The API issues protected, HTTP-only session cookies after Argon2id credential verification. A session-validation component checks the `auth_sessions` row and `users` state in PostgreSQL on **every** authenticated request. A missing, revoked, expired or version-stale session is denied; dependency failure is sanitized and fails closed. ASP.NET Core Data Protection keys persist on restricted E-backed storage so valid cookies survive container restart.

The accepted M1 implementation adds identity entities and services in Infrastructure, EF Core persistence, and API cookie middleware/endpoints. Domain and Application retain the accepted M0 boundaries and have no new persistence or HTTP dependencies. Four additive tables are implemented: `users`, `user_credentials`, `auth_sessions` and `auth_events`. The public M0 synthetic status route remains unchanged. Integration principals are M7; organization roles and data scoping begin in M2, and cannot be inferred merely from a successful M1 login.

[Detailed architecture](M1_AUTHENTICATION_DESIGN.md) | [D-001 decision](ADR_D001_BROWSER_SESSIONS.md) | [G1 verification](M1_VERIFICATION_PLAN.md). This describes implemented, verified and accepted M1 behavior on `main`. See the [execution record](M1_IMPLEMENTATION_VERIFICATION.md); production deployment remains outside the M1 acceptance.

## Bounded modules

### Identity and Organizations
Users, organization memberships, scoped roles, permission policies, session control, and login events.

### Shipment Operations
Ports, voyages, vessels, containers, shipments, assignments, planned milestones, and operational edits.

### Tracking
Source adapters, inbound event inbox, validation, normalization, replay, projections, and exceptions.

### Analytics
Read-optimized queries and aggregates with reproducible definitions and drill-down references.

### IT Operations
Health data, service incidents, evidence, investigation notes, state transitions, and remediation tracking.

### Audit
Append-only records of sensitive actions and key operational state changes. No direct mutation of audit records.

## Modularity rules

- Modules communicate through explicit application interfaces, not arbitrary cross-module table writes.
- HTTP handlers call application services. Business rules reside in domain/application layers.
- External data formats terminate at adapter boundaries; internal domain types are stable and versioned.
- Queries may use dedicated read models without bypassing organization checks.
- The worker must have bounded retries, idempotent handlers, and durable error records. [Accepted M9](M9_G9_GATE_CLOSURE.md) implements PostgreSQL lease epoch/token fencing, receipt-unique immutable resolution effects, DB-generated attempt events and separately authorized reconciliation.
- Shared infrastructure abstractions must not obscure domain ownership.
- Prefer straightforward synchronous paths until measurable requirements justify additional infrastructure.

## Repository layout

    src/
      HarborOps.Api/
      HarborOps.Application/
      HarborOps.Domain/
      HarborOps.Infrastructure/
      HarborOps.Worker/
    web/
      src/
        app/
        features/
        components/
        lib/
    tests/
      HarborOps.UnitTests/
      HarborOps.IntegrationTests/
      HarborOps.ContractTests/
      e2e/
      performance/
    infra/
      compose/
      monitoring/
      cloud/
    samples/
      scenarios/
    docs/

The application, worker shell, test projects, scripts, and Compose configuration have been added. Some target directories such as `infra/monitoring`, tracking adapters, and the M0 functional browser matrix has been executed in the accepted G0 run.

## Persistence strategy

- PostgreSQL is the source of truth; EF Core migrations are forward-reviewed and tested.
- Organization ID is explicit on multi-organization data and must appear in authorized queries.
- Use unique constraints and transaction boundaries to enforce invariants, not only application checks.
- Use optimistic concurrency or explicit version checks for conflicting human edits.
- Preserve raw inbound payload, source metadata, and normalization results for traceability, subject to retention and redaction policy.
- Store accepted events separately from derived current-state projections.
- Rebuild projections through an explicit verified procedure.

## API design

- JSON over HTTPS, an /api/v1 base path, and OpenAPI documentation.
- Cursor pagination for event streams; bounded page-size limits elsewhere.
- Consistent validation responses, correlation IDs, and typed errors.
- Conditional write semantics for race-prone updates.
- No debug-only bypasses that are available in hosted environments.

## Infrastructure and deployment

Local first: PostgreSQL + API + worker + web + optional telemetry, all containerized. A hosted demonstration may use cloud student credits but must feature a budget ceiling, shutdown plan, and explicit limits.

## Architecture risks

1. Cross-organization data leakage through shared search or reporting queries.
2. Duplicate, late, or out-of-order tracking events causing false projections.
3. Tight coupling between third-party feed schemas and domain entities.
4. Silent worker failure or unbounded retry loops.
5. Dashboard figures diverging from source shipment records.
6. Uncontrolled external map, compute, or telemetry costs.

Each risk has corresponding test and operations requirements. See SECURITY.md, EVENT_PROCESSING.md, and TEST_STRATEGY.md.

## Local disk boundary

The source, build outputs, dependency caches, PostgreSQL/Docker Desktop data and test evidence must use E-drive storage per [Local Storage Policy](LOCAL_STORAGE_POLICY.md). Only unavoidable short-lived Windows temporary writes on C: are permitted.

## Verified M0 runtime modernization

The accepted M0 hardening baseline uses .NET 10 and EF Core 10 while preserving the original PostgreSQL 16 on-disk format and immutable bootstrap migration. The PostgreSQL container runs under the built-in non-root account with the `gosu` binary removed. The modernized runtime passed historical GitHub Actions verification at exact commit `fec7429415fede121db8a4a34f0df72cd4fac609`. Subsequent changes require scoped local E:-backed verification; GitHub Actions remains disabled. The detailed test matrix is in [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md).

## M0 implemented boundaries

- `src/HarborOps.Api`: HTTP composition root, public minimal status endpoint, liveness and readiness, sanitized error envelope, correlation IDs, and JSON console logs
- `src/HarborOps.Application`: read-only baseline service contract
- `src/HarborOps.Domain`: immutable synthetic baseline identifiers
- `src/HarborOps.Infrastructure`: EF Core context, PostgreSQL migration, deterministic baseline seed, and read service
- `src/HarborOps.Worker`: historically an idle M0 shell; accepted M9 adds verified, opt-in PostgreSQL claim, heartbeat, finalization and reconciliation
- `web`: React status UI reading the API, with loading and dependency-error states
- `compose.yaml`: PostgreSQL, one-shot migrator, API, and static frontend

The M0 baseline record is infrastructure metadata, not a shipment or organization entity. The API does not run migrations while serving requests. Initial database creation and seeding occur in the one-shot migrator container. Subsequent migrations require an explicit operational check. No protected business endpoints exist at this stage.

**Current accepted architecture boundary (2026-11-06):** M9's leased worker, reconciliation and operator routes are merged. M16's append-only audit capture, Auditor-only reads, restricted database roles and both additive migrations were accepted after independent combined verification and merged through [PR #25](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/25); see [G16 closure](M16_G16_GATE_CLOSURE.md). Transport events can park in `NeedsReconciliation`, so sustained transport processing remains prohibited until separately approved and verified backlog handling prevents exhaustion of the 10,000 unresolved-item cap. Neither M10 nor production deployment is accepted.
