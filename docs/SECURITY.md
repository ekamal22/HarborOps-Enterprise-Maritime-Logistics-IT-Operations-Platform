# Security Model

**M11/M12 coordination approved (2026-11-08):** [Combined integration and verification plan](M11_M12_COMBINED_INTEGRATION_PLAN_20261108.md). A combined candidate must retain M11 signed SQL evaluation/disposition authority and a distinct M12 export-audit proof key, without key reuse, role widening or audit bypass. Real restricted-role SQL, live revocation and atomicity require independent runtime proofs.

**Parallel M11/M12 source status (2026-11-08):** [Source and integration register](M11_M12_PARALLEL_SOURCE_STATUS_20261108.md) records separate unmerged source candidates in draft PRs #31 and #32. M11 policy approval is blocked, both technical gate campaigns are unexecuted, and acceptance pending. Protected `main` and the M9 transport hold are unchanged.

**M12 candidate status (2026-11-08):** [Draft PR #32](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/32) contains scoped shipment search, bounded signed CSV export and source-level correctness repairs. [Latest audit](M12_CORRECTNESS_AUDIT_20261108_FIFTH_PASS.md), [implementation status](M12_IMPLEMENTATION_STATUS.md) and [G12 plan](M12_VERIFICATION_PLAN.md) distinguish authored tests from executed evidence. The fifth pass adds exclusive PostgreSQL role-test gating and CSV cancellation checks. Earlier source fixes remain in force. G12 execution and independent acceptance remain pending. Protected `main` and the M9 transport hold are unchanged.

## M12 scoped search/export security candidate, 2026-11-07

The M12 implementation remains unaccepted in draft [PR #32](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/32). All G12 runtime and security checks are NOT EXECUTED. The [correctness audit](M12_CORRECTNESS_AUDIT_20261107.md) records source remediation and the open verification obligations.

Shipment search revalidates selected organization, session, user and permitted membership inside a transaction. Protected cursor payloads bind tenant, user, login session, filter fingerprint and expiry. The CSV route requires Analyst or Manager `ExportShipmentData` permission and a same-origin browser request. Auditor shipment-read permission does not imply export. Every exported row is tenant-scoped, output capped at 5,000 rows and 10 MiB, and fixed CSV columns are escaped against spreadsheet formulas.

The M12 migration creates a distinct restricted HMAC-SHA256 verifier key table, a SECURITY DEFINER export-audit function and a unique export-event/correlation index. The HTTP runtime signs only a 45-second proof bound to actor, session, tenant, row/byte counts, filter digest and correlation. The API SQL login cannot SELECT the verifier key. M16 audit records `shipment.export_prepared` atomically before returning bytes; this is not a delivery receipt or cryptographic hash of the CSV. Neither a database-only impersonator without the signing key nor an expired/replayed proof may forge another event. Full HTTP-runtime compromise and privileged migrator/superuser access remain outside the database-only guarantee.

Use a **separate** private `HARBOROPS_M12_EXPORT_PROOF_KEY`, 32 random bytes encoded in base64, only for migrator and API. Missing key returns 503 without CSV, while malformed configured material blocks privileged key provisioning. It is never sent to the worker, web frontend, repository, logs or artifacts. Restricted-role SQL and key-rotation verification remain mandatory before G12 acceptance.


**Current integrated baseline (2026-11-07):** G0-G10 and G16 are accepted on protected `main`. The signed-correction security remediation merged in [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) at `9b6c801fde4004a4c0021ec7a6296efe28cf1544` after [G10 S01/S25 gate closure](M10_G10_GATE_CLOSURE_20261107.md). Historical reports retain their original source-locked results. The M9 transport-processing hold continues. No deployment or release is authorized.


**Current accepted baseline (2026-11-07):** G0-G10 and G16 are integrated into protected `main`; G10 remediation merged through [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28) following independently reviewed [gate closure](M10_G10_GATE_CLOSURE_20261107.md). G11-G15 and G17-G24 remain unaccepted. Sustained M9 transport-event processing remains held.

## M10 correction authority security boundary, 2026-11-07

**M10/G10 checkpoint (2026-11-07):** Fourteen M10 local policies are approved (L07/L14 revised, L06 conditional and verified). Signed restricted correction authority, same-transaction M16 audit and upgrade-safe thirteenth migration are accepted and merged via PR #28. Original G10 evidence is source-locked to `3e81b88`, correction authority to `a26d19a`, and final historical upgrade and default Compose tests to `e3002bd`. See [G10 closure](M10_G10_GATE_CLOSURE_20261107.md).

**Final merge-readiness update (2026-11-07):** Two review findings are closed in source and verified on `e3002bd9ea38a20bd4c6b12027c6ecdcdf5fb343` ([record](M10_G10_FINAL_MERGE_READINESS_20261107.md)): the accepted twelfth migration is restored unchanged and the NULL-safe projector scope function now arrives through the thirteenth migration, and the default `compose.yaml` requires `HARBOROPS_M10_CORRECTION_PROOF_KEY` for the migrator and API only. Targeted results: unit 239/239, contract 39/39, projector scope tests 3/3, change-impact integration set 126/126, default-stack correction flow 11/11, six projector mutants killed. The full integration suite, real-stack check and browser run were not repeated on this source. Independent review (G10-S25) and the S01 conformance disposition are NOT EXECUTED. Acceptance pending.

The accepted remediation removes unrestricted direct correction INSERT from `harborops_api` on `main`. Human corrections require an authenticated HTTP request, short-lived signed proof bound to actor, session, tenant, shipment, payload, generation and idempotency key, plus live Manager/session verification inside a constrained PostgreSQL function. Central M16 audit and correction effects are atomic. The migrator and API require a private proof key, not the worker or web. [G10 closure](M10_G10_GATE_CLOSURE_20261107.md) records the tested limits.

The trust boundary excludes compromise of the complete HTTP runtime or privileged database/migration identities. The new proof and grant model must be verified against real restricted SQL logins, key mismatch/rotation, signature substitution and session revocation; committed code is not security certification. Do not log, disclose or store signing key material in repository files or shareable evidence.

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

## M4 security boundary (verified and G4 accepted)

The accepted [M4 architecture](M4_ARCHITECTURE_DESIGN.md) implements typed voyage/container permissions: Analyst reads, Manager manages, and Administrator, Support and Auditor have no implicit business access. Same-tenant voyage composite foreign keys, immutable container identity, archive/version controls, CSRF/Origin, account and role revocation under queued requests, foreign UUID denial and real database outage/recovery all passed [G4 executed verification](M4_EXECUTED_VERIFICATION.md) on `600d7c787da9f9cd4a70225cb3e3c38c093c0dc6`. This is a verified local milestone security boundary, not a production certification or substitute for M20/G24 security assurance.

## M3 reference-data security boundaries (verified and accepted locally)

The accepted [M3 architecture](M3_ARCHITECTURE_DESIGN.md) provides tenant-owned port/vessel references on top of M2 sessions and organizations. Typed `ViewReferenceData` permits Analyst and Manager reads; `ManageReferenceData` permits Manager writes. Support, Administrator and Auditor have no implicit business-data access. Reads revalidate current tenant/session/role scope; writes revalidate transactionally and enforce optimistic versions, tenant ownership and identifier retention after archival. The G3 security matrix exercised foreign UUIDs, revoked/suspended memberships, CSRF/Origin denial, identifier collisions, concurrency and PostgreSQL outage/recovery with real HTTP and database tests. All 25 scenarios passed locally on `b72025d35d62d0a8a45b466986a0b3bd89eebadb` ([evidence](M3_EXECUTED_VERIFICATION.md)); G3 was explicitly accepted on 2026-10-31 and integrated into `main`. Syntax-valid UN/LOCODE/IMO/MMSI fields do not establish official registration. This is local gate evidence, not independent production security certification.

## M5 and M6 accepted security boundaries (local verification)

M5 implements typed shipment permissions, session and tenant revalidation, status history and versioned changes. M6 adds typed allocation permissions, tenant-scoped container and voyage histories, PostgreSQL interval exclusions, post-lock authorization, parent guards and immutable history. Their local gates G5 and G6 were explicitly accepted 2026-11-02. [M5 closure](M5_G5_GATE_CLOSURE.md) and [M6/G6 closure](M6_G6_GATE_CLOSURE.md) retain execution evidence and limitations. Neither gate constitutes internet-hosting or production certification.

## Accepted M7 integration-source implementation

M7 adds organization-bound opaque bearer credentials, digest-only storage, direct database revocation checks and independent tracking-event authorization. The source passed a complete local technical run (see [executed verification](M7_EXECUTED_VERIFICATION.md)), G7 was accepted 2026-11-03 and the implementation is integrated into `main`. The endpoint never acknowledges receipt without M8 durable storage. See [M7 implementation status](M7_IMPLEMENTATION_STATUS.md). G7 and D-002 Option A are accepted (2026-11-03); see [gate closure](M7_G7_GATE_CLOSURE.md).

## Transport trust model

Authenticated and state-changing routes require HTTPS, except when `Identity:AllowInsecureLoopbackHttp` is enabled for local use. In that mode the request must carry a `localhost`, `127.0.0.1` or `[::1]` Host **and** originate from an actual loopback peer or from an address listed in `Identity:TrustedLocalHttpPeers` (the Compose gateway and web proxy). A Host header alone never grants local trust, and a request with no remote address is rejected. Forwarded address and scheme headers are honoured only from addresses in `ReverseProxy:KnownProxies`, with a forward limit of one hop, and the local Nginx proxy overwrites any client-supplied `X-Forwarded-For`. The API accepts only the Hosts in `AllowedHosts`. Hosted deployments must disable the local HTTP exception, configure a key protection certificate (the service refuses to start without one), terminate TLS at a trusted proxy and set exact hosts and proxy addresses. In-memory test servers have no socket peer; the integration harness supplies one with a test-only startup filter, and the contract tests in `TransportHardeningHttpTests` cover the matrix above.

## M8 receipt-transaction security boundary (accepted G8)

[M8 architecture](M8_ARCHITECTURE_DESIGN.md) proposes revalidating the original source bearer **inside** the receipt write transaction with live organization/source/credential locks so revocation cannot race the final authorization and commit. Composite tenant/source/credential foreign keys, a unique immutable source event key, append-only receipt/event guards, post-commit-only 202 and bounded first collision metadata protect provenance and prevent false success. A proposed 10,000 Pending-item per-organization cap provides local backpressure. Every pre-acceptance rejection must preserve M7's no-side-effect contract. Tests must deliberately disable these protections and detect the resulting defect. **D-006 Option A and G8 accepted 2026-11-04 after source-locked technical execution and independent raw evidence review.** See [D-006 proposal](M8_D006_DURABILITY_PROPOSAL.md), [G8 security cases](M8_VERIFICATION_PLAN.md) and the [source-locked verification runbook](M8_G8_VERIFICATION_RUNBOOK.md).

## M7 source-authentication security boundary

The M7 implementation adds separate machine-source bearers, organization-first lifecycle locks with tracked-state refresh, a joined credential/source/organization authorization query with row locks, database immutability guards, bounded pre-authentication rate-limiter state, strict TLS/peer trust, bounded JSON and safe error envelopes. The G7 run isolated the proxy network and killed five source-security mutations (digest check, status checks, revocation, body bound, database lifecycle guard); its recorded limits include the end-to-end `499` path and the unexercised hosted TLS deployment. Human session cookies do not authorize machine intake. No event may receive a success acknowledgment until M8 durably records it. The controls are exercised by the [G7 security matrix](M7_VERIFICATION_PLAN.md) results in the [executed verification](M7_EXECUTED_VERIFICATION.md); G7 and D-002 Option A are accepted (2026-11-03); see [gate closure](M7_G7_GATE_CLOSURE.md).

## Public repository and deployment readiness

The [original repository security review](PUBLIC_REPOSITORY_SECURITY_AUDIT_2026-11-02.md) preserves its historical findings. The [remediation register](PUBLICATION_REMEDIATION_STATUS.md) and [publication safety status](PUBLICATION_SAFETY_STATUS.md) document the completed security changes, successful local verification, artifact disposition and branch governance. **The repository is public for source inspection as of 2026-11-03, but internet-facing application hosting is not cleared.** Historical Git content is visible, the LICENSE restricts reuse, and GitHub Actions remains disabled.

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

The M0 hardening checks do not replace the independently accepted M1 identity/session, M2 organization isolation or M3 reference-data authorization gates. The development Compose profile has no HTTPS termination and must not be exposed directly to the internet. M0, M1, M2 and M3 security/regression evidence is retained in the respective milestone records, including [G3 local verification](M3_EXECUTED_VERIFICATION.md). Tracking ingestion, integration secret management, comprehensive production audit records and later assurance gates remain unimplemented.

G0 was explicitly accepted on 2026-10-31 after runtime and image verification. M1 authentication and M2 organization authorization were separately verified and accepted on 2026-10-31. None of the milestone gates approves public or production deployment; later security hardening and release assessment remain required.

## M9 worker authorization and immutable processing evidence (accepted)

The accepted [M9 implementation](M9_G9_GATE_CLOSURE.md) adds tenant-scoped, lease-token/epoch-fenced work transitions, a receipt-unique immutable resolution effect, and database-generated append-only attempt events within the state-transition transaction. The restricted worker role may read relevant effect and attempt evidence, but cannot directly insert or update attempt history. The worker can operate only within active organization scope; the database guard disallows worker-initiated manual redrive.

The accepted operator routes use live membership and selected organization constraints. Redrive additionally requires a current session security version, Manager permission, CSRF validation, an attributed sanitized reason, and a bounded generation. Queue responses omit raw accepted bytes, credentials, secret digests and internal exception messages. They are marked `no-store`.

The [M9 source correctness audit](M9_CORRECTNESS_AUDIT_20261104.md) was source inspection rather than an executed penetration test. G9's restricted-role SQL attacks, revocation, tenant and takeover checks are in the [executed campaign](M9_EXECUTED_VERIFICATION.md). M16's separate [G16 closure](M16_G16_GATE_CLOSURE.md) records 544/544 independently checked combined evidence items, S07/S08/S28 supplement review, restricted API and worker grants, the F001 generation-zero forgery correction and 17/17 focused plus 286/286 full integration tests on corrected source `8ba7727`. Audit entries are append-only, visible only to a current selected-organization Auditor; the privileged migrator remains outside the database-only tamper resistance claim. Historical G8 and G9 records are unchanged.

**M9 acceptance checkpoint (2026-11-05):** The six [M9-01 contract decision](M9_LOCAL_CONTRACT_DECISION.md) choices, including the intentionally stricter M8-derived backlog admission rule and Manager-only redrive permissions, are approved with the transport-backlog operating condition. The restricted worker, security-version redrive race, tenant negative cases and 14 mutation families passed in the [source-locked technical campaign](M9_EXECUTED_VERIFICATION.md). No deployment of sustained transport-event processing is authorized until M10 or an explicitly approved backlog mechanism handles parked transport events. Independent G9-S25 review and formal G9 acceptance are recorded in [G9 closure](M9_G9_GATE_CLOSURE.md). M16 audit-read roles and the accepted M9 tracking-work roles must both survive any later combined authorization-policy change.
