# M1 Authentication and Session Lifecycle Design

**Decision:** Approved architecture. D-001 closed.  
**Milestone:** M1-01 through M1-04, exit gate G1.  
**Implementation:** Accepted M1 source is integrated into `main`. [G1 verification](M1_IMPLEMENTATION_VERIFICATION.md) passed, acceptance was recorded on 2026-10-31 and PR #4 merged at `8f9fc0d506a9e1dc1d4f5acab936622df988f413`.  
**Baseline:** G0 accepted on 2026-10-31; .NET 10, EF Core 10, PostgreSQL 16, React/Vite, Nginx and Docker Compose.  
**Related:** [D-001 decision](ADR_D001_BROWSER_SESSIONS.md), [Roadmap](ROADMAP.md), [Requirements](REQUIREMENTS.md), [API Contracts](API_CONTRACTS.md), [Security](SECURITY.md), [M1 verification plan](M1_VERIFICATION_PLAN.md).

## 1. Goal and boundaries

M1 establishes human identity, secure credential verification, server-authoritative session lifecycle, logout, account disablement and a functional authenticated browser flow. It introduces no organization membership, roles, shipment access, tracking-source credentials, public registration, external SSO or production deployment. M2 subsequently delivered organization and role isolation and was accepted on 2026-10-31; M7 remains responsible for integration principals. M16/M20 review more extensive security measures later without postponing core M1 security.

The M0 \`GET /api/v1/system/baseline\` and \`/health/live\` and \`/health/ready\` contracts remain functional; the baseline is public synthetic metadata only. All new private M1 routes require authentication unless explicitly documented as bootstrap/login routes. No organization-scoped business endpoint is created for G1.

## 2. Selected topology

\`\`\`text
Browser (React + Vite)
      |
      | Same origin: http://localhost:5173 in isolated local development
      v
Nginx (/api/* -> internal API) or Vite dev proxy
      |
      v
ASP.NET Core 10 modular API
  - cookie authentication and authorization middleware
  - antiforgery and origin/fetch-metadata validation
  - login and password-hash concurrency limits
  - auth endpoint group and sanitized errors
      |
      +--> Application identity/session services
                 |
                 +--> PostgreSQL users, user_credentials,
                 |    auth_sessions, auth_events
                 |
                 +--> Argon2id credential verifier
      |
      +--> persistent Data Protection key ring
           in approved E-backed Docker storage
\`\`\`

Use ASP.NET Core cookie authentication to issue a cryptographically protected ticket. The ticket contains only the internal user ID, server-side session ID and security-version claim. It is **not** itself session authority: on every authenticated request, an authentication validation handler queries PostgreSQL. Accept only if the user is active, the session exists, is not revoked, has not reached either expiry and has a matching security version. Reject missing/invalid sessions and fail closed during database outages. Do not use JWTs or web-storage credentials for human browser sessions.

Keep Domain and Application independent of HTTP and EF Core. Infrastructure implements persistent repositories, hash verification and session validation. API owns cookies, CSRF, endpoint mapping, response errors and rate-limit policy. No new identity microservice, Redis dependency, hosted login provider or paid service.

## 3. D-001 decisions

| Dimension | Approved M1 design |
| --- | --- |
| Human sign-in | Controlled local accounts, email and password |
| Browser transport | Same-origin, protected HTTP-only cookie |
| Cookie authority | Server-side session row checked on every authenticated request |
| Credential protection | Salted Argon2id hash in self-describing encoded format |
| Account states | Active, Disabled |
| Sessions | Concurrent per device; logout current, logout all, revoke on disable/reset |
| Idle/absolute expiry | Proposed configurable defaults: 30 minutes / 12 hours |
| Sliding expiry | Extend inactivity deadline while never exceeding absolute expiry |
| CSRF | ASP.NET Core antiforgery token and strict browser-origin checks |
| Password provisioning | Interactive restricted local CLI; no public registration |
| Protection keys | Persistent protected key ring, on E: for workstation Docker |
| Integration bearer credentials | Not implemented in M1; M7 |
| Organizations/roles | Not implemented in M1; M2 |
| Accessibility audits | Final M24-04 only, not a G1 requirement |

The timeout and hashing work factors are approved initial **design defaults**, not observed production measurements. Benchmark work factors before non-local or production deployment; G1 accepted the tested local-first defaults.

## 4. Persistence

Extend the current \`HarborOpsDbContext\` through an additive EF Core migration. Do not replace the existing \`bootstrap_records\` model, modify the accepted M0 metadata seed or require an existing personal Docker volume for G1. Primary and foreign keys use UUIDs, timestamps use UTC \`timestamptz\`, and all new tables are schema-reviewed.

### users

- \`id UUID PRIMARY KEY\`
- \`email VARCHAR(320) NOT NULL\`
- \`normalized_email VARCHAR(320) NOT NULL UNIQUE\`
- \`display_name VARCHAR(160) NOT NULL\`
- \`status VARCHAR(16) NOT NULL\`, constrained to \`Active\` and \`Disabled\`
- \`security_version BIGINT NOT NULL DEFAULT 1\`, positive
- \`created_at_utc TIMESTAMPTZ NOT NULL\`
- \`disabled_at_utc TIMESTAMPTZ NULL\`

Normalize and validate identifiers consistently at provisioning and sign-in. Do not rely on case-insensitive database collation alone; document email normalization decisions and test Unicode/duplicate forms. Avoid email addresses in operational logs.

### user_credentials

- \`user_id UUID PRIMARY KEY REFERENCES users(id)\`
- \`password_hash TEXT NOT NULL\`, Argon2id encoded result including per-password salt and parameters
- \`hash_algorithm VARCHAR(24) NOT NULL\`, initial \`argon2id\`
- \`password_changed_at_utc TIMESTAMPTZ NOT NULL\`
- \`created_at_utc TIMESTAMPTZ NOT NULL\`

Exactly one current password credential per user. Never put plaintext, reversible passwords or generated one-time passwords in fixtures or source files. Changing credentials increments \`users.security_version\` and invalidates existing sessions in a single transaction.

### auth_sessions

- \`id UUID PRIMARY KEY\` generated with a cryptographically strong source
- \`user_id UUID NOT NULL REFERENCES users(id)\`
- \`issued_at_utc TIMESTAMPTZ NOT NULL\`
- \`last_seen_at_utc TIMESTAMPTZ NOT NULL\`
- \`idle_expires_at_utc TIMESTAMPTZ NOT NULL\`
- \`absolute_expires_at_utc TIMESTAMPTZ NOT NULL\`
- \`security_version BIGINT NOT NULL\`
- \`revoked_at_utc TIMESTAMPTZ NULL\`
- \`revocation_reason VARCHAR(40) NULL\`

Validate ordering: issue time is not later than last seen, idle expiry, or absolute expiry. Include indexes on user and active/expiry lookups. Do not extend a session beyond its absolute expiry. Stored rows are authority; ticket replay after logout cannot restore a revoked session.

### auth_events

- \`id UUID PRIMARY KEY\`
- \`user_id UUID NULL REFERENCES users(id)\`
- \`session_id UUID NULL\`, reference when applicable
- \`event_type VARCHAR(40) NOT NULL\`
- \`occurred_at_utc TIMESTAMPTZ NOT NULL\`
- \`correlation_id VARCHAR(64) NOT NULL\`
- \`result_code VARCHAR(40) NOT NULL\`
- \`source_marker VARCHAR(100) NULL\`

Record bounded, redacted login success/failure, logout, forced revocation, disablement and password-reset events. Do not log raw passwords, cookie values, CSRF tokens, password hashes, full account identifiers for unknown login attempts or arbitrary untrusted headers. Prefer a keyed and truncated source marker if abuse investigation requires grouping. Establish retention/rotation separately; this table does not replace M16's full business audit module.

## 5. Atomic state transitions

1. **Provision:** privileged local CLI verifies administrative context and target database, validates password policy, hashes password under bounded concurrency, and atomically creates user and credential. No public registration endpoint or default login seed.
2. **Login:** obtain generic credentials input; apply source and identifier rate limits; look up active user, execute a dummy hash on missing accounts to reduce username enumeration; verify stored hash; start session in PostgreSQL; issue protected cookie; write redacted event. Failed login is generic 401.
3. **Session validate:** unprotect cookie, obtain user/session identifiers, query session and account, compare account status/security version, idle/absolute expiry and revocation. Fail closed if PostgreSQL cannot be reached. Reject with 401 for invalid session, but sanitized 503 for dependency failure.
4. **Touch:** extend sliding idle expiry only under an update that does not resurrect revoked/expired sessions. Use a concurrency-safe update and cap at absolute expiry. The browser receives refreshed cookie only as required by the selected cookie middleware configuration; database expiry remains authoritative.
5. **Logout:** validate CSRF, revoke current row, clear browser cookie, ensure replay returns 401. Define idempotent behavior for already-logged-out requests without allowing CSRF bypass.
6. **Logout all:** revoke all active sessions, including the calling session, and clear caller's cookie. Other devices must fail on their next authenticated request.
7. **Disable:** require fresh password confirmation for self-disablement; atomically set Disabled, increment security version and revoke all sessions. Admin re-enable/provisioning remains a controlled CLI operation, not a publicly accessible admin API.
8. **Password reset via CLI:** validate identity out of band; update password hash, security version and revoke sessions in one transaction. A reset must never leak the new password in command args, shell history, logs or CI artifacts.

Use an injected \`TimeProvider\` for deterministic expiry tests and a scoped transaction for multi-row writes.

## 6. API contracts

Base \`/api/v1/auth\`. Inputs and outputs are bounded JSON except 204 and the cookie header. Retain M0 \`X-Correlation-ID\` and sanitized error envelopes.

| Method | Path | Authentication | CSRF | Meaning |
| --- | --- | --- | --- | --- |
| GET | /csrf | Public same-origin bootstrap | No | Mint antiforgery token; non-cacheable |
| POST | /login | Anonymous | Required | Validate credentials, create session and issue cookie |
| GET | /session | Required | No | Minimal current user/session view; no role claims |
| POST | /logout | Authenticated | Required | Revoke current session and clear cookie |
| POST | /logout-all | Authenticated | Required | Revoke all sessions and clear cookie |
| POST | /account/disable-self | Authenticated | Required | Confirm password and disable own account |
| GET | /session/guard | Required | No | Minimal protected diagnostic check |

All paths above have prefix \`/api/v1/auth\`. A \`GET /auth/csrf\` is safe only when origin restrictions and cookie settings are correct; it must not set authenticated state, and must not grant an account session.

Example \`POST /api/v1/auth/login\` request:

\`\`\`json
{"email":"operator@example.test","password":"placeholder-not-a-real-password"}
\`\`\`

Example successful \`200\` response:

\`\`\`json
{"user":{"id":"4f84a9bf-5a71-4d60-a809-12938b1a781d","email":"operator@example.test","displayName":"Operator"},"session":{"expiresAtUtc":"2026-11-01T09:00:00Z"}}
\`\`\`

The response never exposes a session secret or password hash. The example timestamp is illustrative, not a fixed expiry.

Invalid login, unknown account or disabled user share the same external \`401\` envelope:

\`\`\`json
{"code":"invalid_credentials","message":"Unable to sign in with these credentials","correlationId":"request-correlation-id"}
\`\`\`

Other stable codes: \`authentication_required\` 401, \`session_expired\` 401, \`csrf_invalid\` 400/403 (choose one consistently), \`rate_limited\` 429 with bounded Retry-After, \`dependency_unavailable\` 503 and \`internal_error\` 500. A session check must return \`401\` JSON, not a 302 login-page redirect. All protected endpoints enforce server authentication, not just a frontend routing check.

### Password policy

Start with minimum 15 characters for password-only login, at least 64-character maximum support (prefer generous UTF-8 byte cap), long passphrases, no forced character-class mixtures or routine rotation, and a locally stored common/compromised-password blocklist during provisioning. Use Argon2id with unique salts and initial 64 MiB / three iterations / one lane, benchmarked for available CPU and memory; limit concurrent expensive hash operations. Store algorithm/version/parameters in the encoded hash to permit controlled rehashing.

### Session configuration

Initial idle expiry 30 minutes, absolute expiry 12 hours, concurrent device sessions allowed, no remember-me feature. Expose a secure, host-only HTTP-only cookie. Hosted HTTPS requires the \`__Host-\` prefix, \`Secure\`, \`Path=/\`, no \`Domain\`, and reviewed \`SameSite\` behavior. The local HTTP loopback exception must use a different development-only cookie configuration and must never be enabled for a remote origin. Do not expose browser authentication on a public HTTP service.

## 7. Antiforgery, origin and proxy controls

React and the API must share a browser origin. In Docker, Nginx proxies \`/api/*\` to the internal API container; in development, Vite proxies \`/api/*\` to the API. The frontend uses relative requests with browser-managed credentials and no cross-origin bearer token handling. Keep the API's loopback diagnostic port isolated; a public deployment must use the HTTPS frontend boundary.

Use \`AddAntiforgery\` with explicit validation of all state-changing auth endpoints, including login, logout and disable-self. Fetch the initial antiforgery token from \`GET /api/v1/auth/csrf\` and submit it in \`X-CSRF-TOKEN\` alongside correct browser cookies. Re-fetch after sign-in to bind to the authenticated principal. Combine with strict trusted Origin/Referer and Fetch Metadata validation, host-only cookie scoping and non-cacheable auth responses. Do not mistake SameSite or CORS alone for a CSRF defense.

Apply correct middleware ordering: existing correlation/error processing, forwarded-header trust where configured, routing/CORS as needed, authentication, authorization, antiforgery and endpoint handlers. Exact implementation order must be verified in integration tests.

## 8. Rate limiting and account enumeration

Apply a configurable starting source limit of five failed login attempts per minute, plus separate account-identifier rate control and bounded Argon2id concurrency. Use trusted proxies only; do not blindly accept untrusted X-Forwarded-For. Unknown account and wrong-password responses use uniform outward status/body and reasonable timing equalization. Never use raw per-IP keys in logs. Login attempts must also remain bounded in body size. Security limiters must not depend on a user-provided identity being trustworthy; define deterministic partitioning and cleanup.

## 9. Key management and restart safety

ASP.NET Core Data Protection keys must persist across API container restarts. The existing API container is read-only and has only temporary storage; configure a dedicated persistent key-ring mount. On a local Windows workstation, every controlled persisted key or config must remain on the approved E: backing storage. Limit filesystem access to the API identity. Deployment-ready keys must be encrypted at rest using configured certificate-based protection or an equivalent reviewed secret mechanism. Do not commit certificates, key material, secret values or key-ring contents.

Tests must restart the API container and verify preexisting valid cookies still validate, while revoked/expired cookies remain rejected. Loss of the database cannot be interpreted as valid session, and loss of an encryption key must not silently bootstrap an insecure session.

## 10. Frontend behavior

Implement a login page, authenticated application shell, signed-in user identity, logout, logout-all, self-disable confirmation, and a minimal protected diagnostic screen. The browser state machine is \`loading\`, \`anonymous\`, \`authenticated\`, \`expired\` and \`unavailable\`. Refreshing the page restores identity through the protected \`GET /auth/session\` endpoint, never by trusting cached local state. Store no authentication credentials in localStorage/sessionStorage. Abort stale requests on logout. Show sanitized errors; navigation alone does not enforce permission.

No final accessibility audit is performed in M1. The dedicated accessibility verification gate remains M24-04, after M23 interface polish. M1 still requires correct functional behavior and security controls at implementation time.

## 11. Code organization

\`\`\`text
src/HarborOps.Domain/Identity/
  UserAccount.cs
  AccountStatus.cs
  AuthSession.cs
  AuthEvent.cs
src/HarborOps.Application/Identity/
  AuthenticationService.cs
  SessionLifecycleService.cs
  AccountLifecycleService.cs
  AuthContracts.cs
  IUserRepository.cs
  ISessionRepository.cs
  ICredentialVerifier.cs
  IAuthEventWriter.cs
src/HarborOps.Infrastructure/Identity/
  Argon2PasswordHasher.cs
  EfUserRepository.cs
  EfSessionRepository.cs
  EfAuthEventWriter.cs
  SessionValidationEvents.cs
src/HarborOps.Infrastructure/Persistence/
  HarborOpsDbContext.cs
  Migrations/20261031010000_IdentitySessions.cs
src/HarborOps.Api/Authentication/
  AuthenticationEndpoints.cs
  AuthenticationSetup.cs
  CsrfValidation.cs
  LoginRateLimitPolicy.cs
  SessionCookieOptions.cs
web/src/auth/
  AuthProvider.tsx
  authClient.ts
  LoginPage.tsx
  AuthenticatedShell.tsx
  SessionControls.tsx
tests/HarborOps.UnitTests/Identity/
tests/HarborOps.ContractTests/Identity/
tests/HarborOps.IntegrationTests/Identity/
tests/browser/specs/auth.spec.mjs
\`\`\`

The path list above is the original design outline. Actual implementation files are enumerated in [M1 Verification](M1_IMPLEMENTATION_VERIFICATION.md) and retain the accepted M0 module boundaries. A restricted local CLI mode may live in the existing API project. Its exact command name and validated target database checks must be selected and documented during implementation.

## 12. Delivery and verification sequence

- **M1-01:** Record D-001 as accepted, finalize API schema, cookie/same-origin policy, password defaults, threat and negative-case matrix.
- **M1-02:** Add additive migration, domain/application abstractions, Argon2id verifier, controlled CLI provisioning, session persistence/revocation, clock-driven unit tests. Verify a clean disposable database and a disposable M0-seeded database.
- **M1-03:** Configure middleware, antiforgery, session validator, rate limits, keys, endpoints and logging. Test invalid/disabled users, expiry, replay, cookie security, outage and safe errors.
- **M1-04:** Add Nginx/Vite same-origin proxy and functional React auth flow. Execute .NET, frontend, real PostgreSQL, Compose restart/outage, Chromium/Firefox/WebKit and image/dependency/secret checks. Preserve the verified G0 baseline contracts.
- **G1:** Gather exact tested revision and evidence, then record verified evidence, followed by explicit acceptance and merge. G1 was accepted and merged on 2026-10-31.

G1 needs no manual old-volume compatibility check, no early WCAG/screen-reader audit, no paid service and no real account data. Historical snapshot compatibility is at M6/M12/M18/M24-03, backup/restore at M19, accessibility at M24-04.

## 13. Explicit negative verification matrix

| Case | Expected |
| --- | --- |
| Unknown email / wrong password / disabled user | Identical outward 401; no enumeration |
| Missing login CSRF / forged token / foreign origin | Reject before mutation |
| Excessive login requests / excessive password hashing | 429 or bounded safe rejection |
| Anonymous call to protected route | JSON 401, never browser redirect |
| Logout then replay protected cookie | 401 |
| Logout all then use cookie on second device | 401 |
| Disable, reset or increment security version | All old sessions rejected |
| Idle expiry / absolute expiry | 401 without test-time sleeping |
| Valid session through restart | Remains valid if protected keys and DB persist |
| DB unavailable during session validation | Sanitized 503, never an authentication bypass |
| Protected data via browser route only | Server still denies unauthenticated API |
| Sensitive data in logs, headers, response or artifacts | Must fail security review |
| Missing/broken migration on clean database | Must fail G1 |
| Existing M0 endpoint, liveness, readiness or synthetic seed | No regression |

See [M1 verification plan](M1_VERIFICATION_PLAN.md) for the detailed evidence policy. The G0 evidence is immutable historical proof of the accepted baseline, not a substitute for running M1 tests.

## 14. Implementation selections and future review

The approved **approach** is fixed. During M1-01, settle exact hash library/package version with security and maintenance review, chosen \`csrf_invalid\` status (400 or 403), key-protection certificate provisioning and cleanup process, exact rate-limit partitions and retention, and password/email normalization edge cases. These are tunable implementation details, not reasons to reopen D-001. Never claim they were benchmarked or implemented before measured evidence exists.
