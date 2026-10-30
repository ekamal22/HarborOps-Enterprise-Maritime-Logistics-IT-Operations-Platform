# Security Model

## Security goals

Protect organizational boundaries, shipment data integrity, credentials, diagnostic information, and the availability of operational workflows.

## Primary threats

| Threat | Control |
| --- | --- |
| Broken object-level authorization | Per-resource organization checks and negative integration tests |
| Broken function-level authorization | Permission policy on every command, query, export, and administration route |
| Cross-organization analytics leakage | Scope predicates in data access and aggregate-query verification |
| Excessive data exposure | Response DTO allowlists, field filtering, export permission |
| Credential theft or replay | Secure sessions/token management, short-lived credentials, rotation |
| Malicious event ingestion | Authenticated adapters, validation, size limits, replay protections |
| SQL injection | Parameterized queries and ORM safely bound query parameters |
| Cross-site scripting | Safe rendering, encoding, and restrictive content security policy |
| CSRF | Anti-CSRF controls for cookie-based state-changing operations |
| SSRF | Restrict external destinations; no arbitrary user-supplied fetch URLs |
| Sensitive log leakage | Redaction, scoped access, retention limits |
| Supply-chain compromise | Pinned dependencies, lockfiles, vulnerability review |
| Denial of service | Rate limits, bounded requests, query ceilings, worker backpressure |
| Privilege escalation | Scoped administrative operations, audit records, least privilege |

## Identity and session controls

- User authentication is required for all business data.
- Adopt one consistent auth model and document its risks; no ad-hoc mix of insecure token storage and cookies.
- Authorization is performed server-side for individual resources, lists, exports, maps, notifications, and background jobs.
- Application administrators are not assumed entitled to business records without an explicit grant.
- Verify role changes revoke access as intended.
- Test logout, expiry, concurrent sessions, invalid credentials, and account disablement.

## Data protection

- Keep secrets in local secret stores or environment configuration excluded from source control.
- Do not commit production tokens, connection strings with passwords, real customer data, or personal details.
- Use HTTPS for hosted deployments and appropriately secure database/network boundaries.
- Validate uploaded and imported files before processing; protect against path traversal and malicious content.
- Use minimal data retention and redact diagnostic copies.
- Protect backups as rigorously as the primary database.
- Audit sensitive state changes and unsuccessful access attempts without storing secret values.

## Verification gates

- Anonymous and wrong-role requests fail for every protected route.
- Cross-organization user enumeration, object retrieval, mutation, export, search, and bulk analytics fail.
- Direct-object requests are tested with guessed and valid foreign identifiers.
- Security-sensitive state transitions are validated independently of UI restrictions.
- Threat tests include forged event source, duplicated event IDs, oversized payload, injection probes, malicious HTML, and replay.
- Static dependency and secret scanning results are triaged; no unresolved critical/high issues at release.
- Hosted security verification uses controlled environments, not third-party systems.

## Disclosure and incident handling

Record observed issues and remediation status in the repository's issue process without posting secrets or exploitable production data. Security fixes require regression tests and clear release notes.

This model is a design specification. It does not assert that controls have already been implemented or independently audited.

## M0 scoped security posture

Only minimal process health and synthetic baseline metadata endpoints are exposed. The browser does not authenticate, and no business records or authorization claims are implemented. Local Compose ports bind to loopback; PostgreSQL uses a password read from an untracked `.env` file. The API uses a limited CORS origin list, redacted error envelopes, and JSON console logging.

This is not a substitute for M1 authentication and organization isolation. The configured Compose profile has no HTTPS termination, should not be exposed to the internet, and has not received a runtime security test. Tracking ingestion, integration secrets, authorization matrices, and audit records remain unimplemented.
