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

## Local storage security

Local project storage is restricted to E: under [Local Storage Policy](LOCAL_STORAGE_POLICY.md). C: is not an approved location for persistent secrets, database volumes, build caches, logs or browser evidence; only unavoidable short-lived operating-system temporary writes are permitted. Local credentials remain untracked and are not to be copied into reports.

## Proposed PostgreSQL hardening

The separate hardening branch builds a PostgreSQL 16.15 Alpine image without `gosu` or `su-exec`, runs the server under the built-in `postgres` account (UID 70) and restricts container privileges. An isolated audit at revision `a791561a` verified non-root startup and 0 critical/high findings in the final derived image using Trivy 0.58.1. The subsequent changes require a fresh scan using a current scanner; source-level mitigation alone does not verify the final revised image. A fresh Trivy scan of the final image, clean-volume initialization, existing-volume compatibility and recovery checks are mandatory. See [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md).

## M0 scoped security posture

Only minimal process health and synthetic baseline metadata endpoints are exposed. The browser does not authenticate, and no business records or authorization claims are implemented. Local Compose ports bind to loopback; PostgreSQL uses a password read from an untracked `.env` file. The API uses a limited CORS origin list, redacted error envelopes, and JSON console logging.

This is not a substitute for M1 authentication or M2 organization isolation. The configured Compose profile has no HTTPS termination, should not be exposed to the internet, and received M0-specific local runtime checks, dependency advisories, secret/port checks and container-image scanning at code revision `4fb34eb`. This does not constitute a complete production security assessment. The previous M0 image scan showed 1 critical and 25 high findings in the upstream `gosu` helper; the initial derived image passed a revision-specific scan, but the revised image still requires a fresh scan of its immutable image ID before acceptance. Tracking ingestion, integration secrets, authorization matrices, and audit records remain unimplemented.
