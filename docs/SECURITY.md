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

## Accepted M1 session and credential security implementation

D-001 is implemented on `main` and G1 was verified and accepted on 2026-10-31, with [G1 verification evidence](M1_IMPLEMENTATION_VERIFICATION.md). PR #4 merged successfully. The runtime is not approved for public or production deployment. See [D-001](ADR_D001_BROWSER_SESSIONS.md), [M1 architecture](M1_AUTHENTICATION_DESIGN.md) and [G1 tests](M1_VERIFICATION_PLAN.md).

- **Human login:** controlled local account provisioning, Argon2id password hashing with unique salts (initial benchmark target: 64 MiB, three iterations, one lane), minimum 15-character passphrases and local compromised/common-password checks. Restrict expensive hash concurrency and never store cleartext credentials.
- **Browser:** ASP.NET Core protected HTTP-only cookie, same-origin React/API proxy, SameSite reviewed, HTTPS/Secure for remote usage. Local loopback HTTP is a separate development-only configuration, never an insecure remote exception. No browser JWT/refresh-token storage.
- **Session authority:** validate user/account status, server session row, idle/absolute expiry, revocation and security version on every protected request. An unavailable database causes sanitized 503, never cookie-only access. Proposed defaults are 30-minute idle and 12-hour absolute lifetime, configurable and tested before G1.
- **CSRF and misuse:** antiforgery token bootstrap, token rotation after login, explicit validation for JSON state-changing requests, Origin/Fetch Metadata checks, bounded request bodies, source/identifier rate limits and generic invalid-login responses.
- **Lifecycle:** revoke immediately on logout, logout-all, administrative password reset or account disablement; do not expose privileged admin HTTP routes before M2. A protected M1 diagnostic route proves back-end denial.
- **Keys:** persist ASP.NET Core Data Protection keys on restricted E-backed Docker storage. Configure key encryption at rest for deployable environments and test API restart continuity.
- **Events and confidentiality:** bounded `auth_events` with correlation/result codes but no plaintext passwords, hashes, cookies, CSRF tokens or raw account identifiers for unknown logins. G1 tests verify redaction.
- **Boundary:** identity and authentication are M1. Organization memberships/roles are M2; integration credentials are M7; complete accessibility testing remains M24-04.

## M2 organization authorization security implementation (verified locally; G2 accepted)

The [M2 architecture](M2_ARCHITECTURE_DESIGN.md) introduces live PostgreSQL membership and role checks on each scoped call, server-session organization selection, a fixed permission matrix, and organization ownership predicates for every organization/member read and write. A selected organization is context, **not** a grant. Changing a role, membership status or organization status revokes access on the next protected request. No support or administrator role implies an automatic global read of business data.

M2 must prevent cross-tenant object ID guessing, wrong-role member administration, self-escalation, stale-role decisions and removal or account disablement of the final active Administrator during concurrent writes. Use transaction locks for sensitive member mutations, sanitized 401/403/404/409/503 errors and M1 CSRF/Origin controls for mutations. The [G2 local verification plan](M2_VERIFICATION_PLAN.md) requires a two-organization and five-role negative matrix. M2 source includes these controls, and the local G2 verification exercised each of them, including competing administrator changes and suspended-organization access ([executed record](M2_EXECUTED_VERIFICATION.md)). [Source inventory](M2_IMPLEMENTATION_STATUS.md).

G2 security checks were executed locally against disposable resources and passed on `fbddc1361d79b027275bb53b48133ba0dc7d5cb1`: object-level and function-level authorization on every organization endpoint and verb, foreign organization and member identifier substitution, wrong-role denial for all five roles, stale-permission and revocation checks, session isolation, concurrent administrator races, CSRF and Origin rejection, sanitized 4xx and 503 responses, response and log redaction, durable revocation across a PostgreSQL outage, and clean HIGH and CRITICAL image scans. Restricted operator commands (`--account-cli`, `--organization-cli`) now reject invalid use with a sanitized message and exit code 1 instead of aborting the process, so no memory dump of a process that handled a password is produced. The primary workstation database and historical Docker volumes were not touched. See the [executed verification record](M2_EXECUTED_VERIFICATION.md). No production security approval is implied.

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

## Verified M0 PostgreSQL hardening

The M0 implementation on `main` builds a PostgreSQL 16.15 Alpine image without `gosu` or `su-exec`, runs the server under the built-in `postgres` account (UID 70) and restricts container privileges. An isolated audit at revision `a791561a` verified non-root startup and 0 critical/high findings in the final derived image using Trivy 0.58.1. The accepted M0 source passed a new Trivy 0.76.1 scan of the exact running PostgreSQL, API and web image IDs during GitHub Actions run #2 at `fec7429415fede121db8a4a34f0df72cd4fac609`. Future image changes still require new scans. G0 scans and tests passed in GitHub Actions run #2. Repeat them after relevant image or runtime changes. Existing-volume compatibility is deferred to the quarter-mile M6, M12, M18 and M24-03 checkpoints; M19 covers full backup/restore. Before a breaking change is applied to preserved data, rehearse it against a disposable copy. See [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md).

## M0 scoped security posture

Only minimal process health and synthetic baseline metadata endpoints are exposed. The browser does not authenticate, and no business records or authorization claims are implemented. Local Compose ports bind to loopback; PostgreSQL uses a password read from an untracked `.env` file. The API uses a limited CORS origin list, redacted error envelopes, and JSON console logging.

The M0 checks do not substitute for M1 authentication or M2 organization isolation. The configured Compose profile has no HTTPS termination, should not be exposed to the internet, and received M0-specific local runtime checks, dependency advisories, secret/port checks and container-image scanning at code revision `4fb34eb`. This does not constitute a complete production security assessment. The previous M0 image scan showed 1 critical and 25 high findings in the upstream `gosu` helper; the hardened images passed fresh immutable image-ID scans in GitHub Actions run #2. M1 authentication controls are implemented and accepted; M2 organization authorization is implemented in unverified feature-branch source but is not accepted. Tracking ingestion, integration secrets, business authorization matrices and full audit records remain unimplemented.

G0 was explicitly accepted on 2026-10-31 after runtime and image verification. M1 authentication and M2 organization authorization were separately verified and accepted on 2026-10-31. None of the milestone gates approves public or production deployment; later security hardening and release assessment remain required.
