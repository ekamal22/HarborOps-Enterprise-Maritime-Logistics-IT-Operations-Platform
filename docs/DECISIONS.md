# Architecture Decisions

This document records accepted baseline decisions and outstanding decisions that affect implementation.

## Accepted baseline decisions

### ADR-001: Modular monolith with separate tracking worker
**Status:** Accepted for initial architecture.

Use one ASP.NET Core application with explicit modules and a separate .NET event worker. Do not introduce microservices solely for architectural appearance. Reconsider only if measured scaling or deployment independence justifies them.

### ADR-002: PostgreSQL as transactional source of truth
**Status:** Accepted.

Store operational records and event inbox/outbox state in PostgreSQL. Derived dashboards may use read models, but projections must be rebuildable from retained accepted events.

### ADR-003: Local-first execution
**Status:** Accepted.

Essential features must work locally without paid services or cloud credentials. Hosted infrastructure is optional, budget-controlled, and disposable.

### ADR-004: Synthetic-data baseline
**Status:** Accepted.

Use deterministic generated datasets. No proprietary company data or private shipping integrations are needed to complete core milestones. Simulated states are clearly labeled.

### ADR-005: Secure organization boundaries
**Status:** Accepted.

Every business-data path must verify an authenticated principal and explicit organization permission. Global platform roles do not imply broad access to customer records.

### ADR-006: Inbound event history is immutable
**Status:** Accepted.

Never silently replace historical accepted events. Correct data with linked correction records and rebuild projections.

## Open decisions

| ID | Decision required | Default hypothesis | Required before |
| --- | --- | --- | --- |
| D-001 | Session versus token auth | Secure server-managed session for browser; scoped tokens for integrations | M1 |
| D-002 | DCSA reference release/subset | Pin one version with a documented field mapping | M2 |
| D-003 | Container identifier format | Validate ISO 6346 where relevant; allow visibly synthetic demo values | M1 |
| D-004 | Map tile provider/license | Use compliant free source with local fallback | M3 |
| D-005 | Exact operational KPI tolerances | Versioned business rules and deterministic fixtures | M3 |
| D-006 | Error queue and worker durability detail | PostgreSQL inbox/dead-letter tables | M2 |
| D-007 | Hosting account and budget ceiling | Local only until credit and spending restrictions checked | M5 |
| D-008 | Backup retention and recovery objectives | Define measurable targets based on demo environment | M5 |

## Decision change protocol

Each material reversal records: context, alternatives, trade-offs, chosen approach, impact on requirements and tests, migration needs, and acceptance status. A change is not adopted solely because a new library or service is available.

## M0 implementation notes

- The API uses .NET 8 and an EF Core 8 PostgreSQL provider. The framework baseline should be upgraded before the end of its support lifecycle.
- Database migration and deterministic bootstrap seeding run as a one-shot Compose service before the API starts.
- The only M0 API resource is public, read-only synthetic platform metadata; authorization for operational data will be implemented in M1 after D-001 is resolved.
- The worker project is deliberately idle. No tracking event behavior is represented as delivered.
- The browser fetches an explicitly configured local API origin. The API permits CORS only for the configured local frontend origin.
- The manual-only verification workflow avoids automatically consuming private repository CI minutes.

These are scoped M0 implementation choices. D-001 through D-008 remain open on the timelines indicated above unless explicitly closed in a subsequent decision review.
