# Security Model

## Verified M0 boundary and residual risk

The separate M0 feature branch records local .NET, database, container and browser verification at code revision `4fb34eb`; G0 is verified, acceptance pending. M0 has public synthetic baseline metadata only, loopback-bound container ports, basic CORS and sanitized errors. These limited checks do not demonstrate authenticated organization isolation, production hardening or future incident/event controls.

The recorded PostgreSQL image scan contains **1 critical and 25 high findings** in the upstream `gosu` helper. This is documented unresolved risk, not automatically approved by the G0 verification state. Review disposition at acceptance and rescan on image changes. Refer to the [M0 verification register](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feat/m0-engineering-baseline/docs/M0_VERIFICATION.md).

Persistent local project storage, credentials, logs, caches and container data must be E-backed per [Local Storage Policy](LOCAL_STORAGE_POLICY.md). No new persistent C-drive project usage is permitted; unavoidable short-lived OS temporary activity is the only exception.

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
