# Architecture

## Baseline topology

A modular monolith provides the HTTP API and transactional domain functions. A separate .NET worker handles ingestion and processing. PostgreSQL is the authoritative persistence layer. React is the user interface. Local infrastructure is orchestrated through Docker Compose.

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

The diagram is the target topology for later milestones. In M0, the code contains the API, PostgreSQL DbContext and migrator, React status interface, and an idle worker shell. Tracking ingestion, domain modules, metrics, tracing, and incidents are not implemented. Docker Compose startup has not been executed or verified.

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
- The worker must have bounded retries, idempotent handlers, and durable error records.
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

The application, worker shell, test projects, scripts, and Compose configuration have been added. Some target directories such as `infra/monitoring`, tracking adapters, and browser test suites are not yet implemented.

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

## M0 implemented boundaries

- `src/HarborOps.Api`: HTTP composition root, public minimal status endpoint, liveness and readiness, sanitized error envelope, correlation IDs, and JSON console logs
- `src/HarborOps.Application`: read-only baseline service contract
- `src/HarborOps.Domain`: immutable synthetic baseline identifiers
- `src/HarborOps.Infrastructure`: EF Core context, PostgreSQL migration, deterministic baseline seed, and read service
- `src/HarborOps.Worker`: idle host shell only; no tracking processing
- `web`: React status UI reading the API, with loading and dependency-error states
- `compose.yaml`: PostgreSQL, one-shot migrator, API, and static frontend

The M0 baseline record is infrastructure metadata, not a shipment or organization entity. The API does not run migrations while serving requests. Initial database creation and seeding occur in the one-shot migrator container. Subsequent migrations require an explicit operational check. No protected business endpoints exist at this stage.
