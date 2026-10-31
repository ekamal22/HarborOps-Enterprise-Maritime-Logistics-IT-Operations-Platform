# D-001: Browser Authentication and Session Authority

**Status:** Accepted design, implemented and verified on feature branch; G1 acceptance pending.  
**Milestone:** M1, gate G1.  
**Decision record:** D-001 in [Decisions](DECISIONS.md).  
**Detailed design:** [M1 Authentication Design](M1_AUTHENTICATION_DESIGN.md).

## Context

HarborOps is an ASP.NET Core 10 modular monolith with React, PostgreSQL and a local Docker Compose runtime. M0 has no private business routes or authenticated principals. M1 must add human login, expiry, logout, disabled-account handling and immediate server-side revocation without a hosted identity dependency. M2 will add organization memberships and resource authorization; M7 will introduce authenticated integration principals.

## Decision

Use **server-authoritative ASP.NET Core cookie authentication** for human browser sessions, backed by PostgreSQL session rows validated on every authenticated request. Use Argon2id for local credentials and a persistent ASP.NET Core Data Protection key ring for ticket protection. Route browser API traffic through the frontend's same origin and use antiforgery validation plus Origin/Fetch Metadata checks for state-changing requests. Keep cookies HTTP-only and host-scoped, secure under HTTPS, and inaccessible to frontend JavaScript.

Use controlled local account provisioning and administrative reset/disable commands. Allow multiple concurrent sessions while providing immediate server-side revocation, logout all and disablement. Default to 30-minute idle and 12-hour absolute expiry, configurable and measurable before G1. Never expose raw auth cookies or session credentials in JSON.

**Do not implement browser JWTs, refresh-token rotation, public self-registration, organization/role authorization, enterprise SSO or tracking integration tokens in M1.** Integration principals with separately scoped credentials are deferred to M7. Organization isolation begins in M2.

## Alternatives considered

| Alternative | Disposition |
| --- | --- |
| JWT access and refresh tokens in browser | Rejected for M1: additional rotation/revocation and browser-storage risk with no needed interoperability |
| Cookie ticket alone without database validation | Rejected: logout and disablement need immediate centralized revocation |
| Hosted identity-as-a-service | Rejected as required baseline: violates free local-first independence |
| Separate identity microservice | Rejected: additional deployment/cost/operational burden without demonstrated need |
| PostgreSQL authority plus cookies | Selected: fits existing database and application stack and supports deterministic test and instant invalidation |

## Consequences and protections

- Database availability is required for every protected request. Failure must return sanitized 503 and deny access; no fallback to cookie-only authorization.
- A small, persistent key-ring mount is required for API restarts. Key material stays out of source and CI artifacts and on E-backed storage for local use; hosted usage needs reviewed key encryption and HTTPS.
- Authenticated requests must validate session status, expiry, revocation, account status and security version.
- Login and all state-changing auth calls need CSRF controls, strict origin checks and bounded rate limiting/password hashing.
- A minimal auth event log is required, without secrets or raw submitted credentials. M16 later adds broader business auditing.
- G1 verifies real database sessions, negative API outcomes, restart/outage, browser flow and regression. Existing-volume quarterly tests and final accessibility testing stay at their roadmap milestones.

## Status boundaries

D-001 approval establishes the architecture; M1 implementation and its executed [verification evidence](M1_IMPLEMENTATION_VERIFICATION.md) are recorded separately. G1 acceptance and merge have not occurred. D-001 can be revisited only through a separate documented decision change.
