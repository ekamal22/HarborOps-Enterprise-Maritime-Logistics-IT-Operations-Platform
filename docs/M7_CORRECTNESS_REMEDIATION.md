# M7 Correctness Audit Remediation

**State:** Source corrections committed, no G7 execution or acceptance recorded.
**Implementation branch:** `feat/m7-tracking-source-validation`.
**Decision boundary:** D-002 upstream artifact, checksum, license and exact mapping acceptance pending.
**Deployment boundary:** Local only. No durable tracking-event receipt or successful ingestion response until M8.

The [second correctness audit](M7_SECOND_CORRECTNESS_AUDIT.md) records follow-up fixes, including EF tracked-state refresh, migration fixtures, capped limiter partitions, future credential revocation timing, security mutations and verification-network isolation.

## Remediation register

| Finding | Implemented correction | Required execution evidence |
| --- | --- | --- |
| Malformed EF Core model and snapshot | Both model files restored from the accepted M6 baseline and the M7 entity configurations inserted once, identically in model and snapshot | Release build, EF pending-model check and fresh M7 migration |
| Credential validation and revocation race | Credential, source and organization joined in one authorization query, with PostgreSQL row-share locks and direct lifetime/revocation checks | Concurrent revoke/suspend versus authentication and controlled database interruption |
| Organization suspension versus source rotation | Create, rotate, revoke and disable lock organization before source inside a transaction | Concurrent organization-status mutation with rotation and source provisioning |
| Unbounded anonymous database probes | IP pre-authentication limiter executes before database lookup, with separate invalid-credential and source partitions | HTTP 401/429 threshold, remote peer and rate partition scenarios |
| Mutable credential identity and historical records | PostgreSQL source and credential triggers forbid deletions, identity/digest changes, revocation reversal, expiry extension and disabled-source reactivation | Direct SQL mutation probes and migration upgrade/reapply |
| No database-enforced two-key limit | Source-locked issuance trigger rejects a third overlapping credential window, including future overlap | Parallel rotation and raw SQL overlapping third-key insert |
| Canonical field inconsistency | JSON parser, schema, test fixtures and HTTP examples consistently use `eventFamily` and `eventCode` | Contract fixture parser and end-to-end HTTP tests |
| Identifier regex end matching | Use absolute string boundaries for event and transport identifiers and strict timestamp grammar | Decoded control-character, trailing newline and UTC boundary tests |
| Credential status error ambiguity | Invalid, expired and revoked tokens receive 401; a cryptographically valid token linked to a disabled source or organization is denied 403 | Negative HTTP source and organization-state cases |
| Sparse M6 upgrade coverage | Populated rehearsal preserves actual accounts, sessions, memberships, ports, vessels, voyage, container, shipment history and assignments | Exact database row snapshots across M6 to M7, rollback and reapply |
| Foreign credential test was inconclusive | Both tenants now have identical source codes and distinct credentials; cross-tenant credential revocation is explicitly denied | Two-tenant PostgreSQL and HTTP checks |
| Canonical event lacked trusted tenant identity | Validated in-memory event is bound to the authenticated database source and organization | Positive source-bound parse and spoofed-tenant rejection |
| Incomplete G7 runner | Added source-locked SDK build/test container, disposable PostgreSQL, cross-browser regression, source-specific outage, security scans, hashed evidence and resource cleanup | Real Windows E:-backed end-to-end execution and independent evidence review |

## Verification state

The authored tests and runner are not executed evidence. A source-level inspection verifies that the EF mapping and snapshot have matching M7 declarations and that checked-in JSON fixtures parse. No workstation-local .NET build, PostgreSQL migration, Docker/Playwright run, dependency scan or CLI lifecycle execution is claimed.

The G7 runner is `scripts/verify-m7-local.sh`. It requires a clean E:-backed checkout, `HARBOROPS_VERIFICATION_ROOT` on E: ending in `m7`, and an E:-backed Docker Desktop image verified by `HARBOROPS_DOCKER_VHDX`. It creates disposable resources under a unique `ho-verify-g7` Compose project and preserves evidence under the configured E: root. The source CLI interactive stage requires `HARBOROPS_G7_INTERACTIVE_CLI=1` and an unredirected private terminal. It displays source credentials once and does not record them in the evidence archive.

No GitHub Actions, retained Docker volume, retained database or C: persistent storage may be used. Only isolated G7-labeled resources can be torn down. Dedicated accessibility verification remains M24-04.

## Remaining G7 acceptance work

D-002 requires an immutable normative 2.2.0 reference, hash, license check and approved mapping. Every G7-S01 through G7-S25 scenario must receive an executed result, including concurrent mutation effectiveness and independent review of logs, coverage, image digests and E:-backed cleanup evidence. A completed technical runner is not by itself an accepted gate. G7 acceptance and any merge into `main` require an explicit subsequent decision.
