# G10 M10-L07 and M10-L14 Targeted Security Remediation

**Decision date:** 2026-11-07  
**Policy disposition:** M10-L01 through M10-L14 approved, with changes to L07 and L14 and L06 conditioned on effective database authorization.  
**Execution status:** Source remediation committed (runtime candidate `3ef7a50859b5f023aa1457b373728888f6d1cba9`); new-source compile, migration, database security tests, independent review and G10 acceptance pending.  
**Baseline for this branch:** `d8ede69d2e7d153a886b09e0bfef8d7cbc32059f`, descended from frozen G10 runtime `3e81b884b6a4cc782f84c58510793666786c5796`.  
**Scope:** Corrections, audit persistence and restricted runtime database authority. No merger, deployment, release or GitHub Actions authorization.

The source-implementation checkpoint and its unexecuted test boundary are in [the implementation record](M10_G10_CORRECTION_AUTHORITY_IMPLEMENTATION.md). The gap analysis in section 2 describes the earlier frozen source, not current branch behavior.

## 1. Accepted policy boundaries

M10-L01 to L05, L08 to L13 retain their proposed semantics, including the M9 hold on transport-event processing and the 10,000 unresolved-work admission cap.

- **L06, conditional:** Preserve append-only, Manager-only, same-shipment, one-level correction semantics, optimistic generation, idempotency, current-session authorization and all existing HTTP protections. L06 is not verified against the new database trust boundary until the remediation passes.
- **L07, modified:** Every successful correction must create one tenant-scoped, privacy-allowlisted M16 audit entry in the same PostgreSQL transaction as the M10 correction. Keep the M10 correction ledger as immutable specialized evidence. Failure of required audit persistence must roll back the correction and all dependent materialization/replay effects. An idempotent replay must not duplicate the audit event.
- **L14, modified:** The restricted API role must not be able to INSERT arbitrary rows into `shipment_tracking_corrections` or impersonate another actor through direct SQL. An authenticated correction must pass a constrained database boundary that verifies live Manager/session/scope status and binding to an application-authenticated request. Worker and projector rights must remain least privilege; runtime identities must not obtain DDL or evidence-tampering authority.

These choices are approved, but source implementation and supplemental verification are outstanding. Do not mark S01 PASS merely because the policy decision has been recorded.

## 2. Source-supported gaps

1. `ShipmentTimelineStore.ExecuteAsync` verifies live Manager/session when given a nonempty session, then directly inserts a correction with the `harborops_api` role.
2. `AuditRoleProvisioner` grants the API role direct INSERT on corrections and replay/projection tables.
3. `m10_validate_correction` validates shipment, organization, effect relationships, correction chain and generation, but not live Manager or session provenance.
4. `audit_entries` and accepted M16-D03 require transactional evidence for sensitive successful business-resource mutation. M10 corrections presently create no corresponding M16 row.
5. The API role can read `auth_sessions`. A function that accepts a user ID and session ID and checks membership is not sufficient to prevent a database-credential-only attacker from reusing someone else's valid session identifiers. Transaction-local GUCs, correlation IDs, a supplied actor UUID, a claimed role or a caller-supplied session UUID are not authentication proofs.

Treat these as source-supported trust-boundary defects, not a demonstrated remote exploit.

## 3. Required implementation design

### R1. Create an additive, upgrade-safe persistence change

Prefer an additive SQL migration after the existing twelve-migration source, rather than editing already merged migrations or rewriting historical evidence. If a new migration is introduced, update only current-source migration-count assertions and operational documentation. Keep previous source-locked twelve-migration verification results described as historical results.

Rehearse upgrading both a fresh database and a populated twelve-migration database. Preserve existing M8/M9/M16 and M10 evidence, indexes, guards, scopes, and correction generations. A new cryptographic authorization store must not be readable or writable by ordinary API, worker or projector SQL identities.

### R2. Enforce correction authority at the database boundary

Revoke ordinary API INSERT on `shipment_tracking_corrections`. Implement a narrowly granted, schema-qualified, security-definer operation for authorized correction insertion.

The database operation must enforce active organization and shipment status, active Manager membership, current nonrevoked and nonexpired session, matching session user and selected organization, current security version, correction scope, generation and chain rules, and fail closed when verification fails.

It must also validate an **unforgeable proof of request authority** derived from the application's authenticated session. The proof must bind actor, session, organization, shipment, operation, complete correction payload or its canonical digest, generation, idempotency key, a short expiry and unique nonce. Prevent replay and cross-scope or cross-payload substitution. A database-credential-only attacker, even one able to query `auth_sessions`, must be unable to mint this proof.

A separate application-held signing key and a database-held verifier secret can satisfy this if provisioned securely. Any alternative must provide the same demonstrable property. Do not treat arbitrary SQL session variables or user IDs as trusted. Keep secrets out of source, logs, evidence, responses and permissive database grants. Explicitly review migration-time and startup provisioning, key rotation and invalid-key fail-closed behavior. In particular, the existing broad API SELECT grant must not expose any new verifier-key table.

The existing correction route must continue to obtain actor/session from server-side authenticated context. The database operation, audit append, projection update and idempotency record must share the existing transaction. Review direct replay-row privileges for unauthorized success-record forgery as part of the boundary.

### R3. Write the M16 audit entry atomically

Emit exactly one allowlisted successful correction event with organization scope, server-validated actor and session, safe correlation identifier and resource identity. Record only fixed operation metadata and non-sensitive changed-field names. Never store reason text, body content, session tokens, signing proofs, secrets or raw tracking payload in the M16 metadata.

Insert within the same transaction as the correction, using a controlled database path; do not grant the API role unrestricted audit INSERT. Audit failure must roll back correction, input revision, projection changes, applications and idempotency replay records. Expose sanitized non-success (normally HTTP 503 for audit persistence outage), not misleading 200/409/422. A normal replay must not append a second M16 event.

Avoid changing the accepted M16 audit schema or reader contract unless demonstrably required. Maintain Auditor-only scoped visibility and permitted retention.

### R4. Remove optional authorization bypasses

Review `ShipmentTimelineStore.CorrectAsync`, which accepts no session and may bypass the live Manager check. Remove or confine it to an explicitly separated privileged test/migration path. All production human correction paths must require valid authenticated session and proof. Preserve projector-only automatic rebuild behavior without extending it to corrections.

### R5. Preserve accepted boundaries

Do not change the M9 transport hold, 10,000 unresolved cap, M8 receipts, M9 immutable effects, M16 accepted event contract, tenant isolation, no-inference ETA contract, cursor behavior, projector default-off behavior or accepted authorization/revocation semantics.

## 4. Minimum targeted verification matrix

| ID | Probe | Required result |
| --- | --- | --- |
| R01 | Authorized Manager HTTP correction on restricted API login | One correction, one M16 event, correct tenant/actor/session and consistent state |
| R02 | Idempotency replay and same-key different-payload collision | Replayed response or 409 respectively, no duplicate correction/audit |
| R03 | Analyst, former Manager, revoked/expired session, suspended organization and archived shipment | No correction, projection application, replay or successful correction-audit event |
| R04 | Direct SQL correction INSERT using real `harborops_api` credentials | Permission denied, including otherwise valid actor and generation |
| R05 | Restricted-role call to correction function with no/bad/expired/replayed proof | Denied without persisted effects |
| R06 | Restricted-role call with stolen valid actor/session IDs but no signing authority | Denied, even if those IDs are readable through `auth_sessions` |
| R07 | Proof substitution (tenant, shipment, actor, session, target, replacement, reason, generation, idempotency key) | Denied for every modified binding |
| R08 | Revocation and downgrade racing correction with real PostgreSQL lock observation | No post-revocation unauthorized commit; preserve lock ordering |
| R09 | Inject failure into required M16 audit append after correction validation | Entire transaction rolls back, sanitized non-success |
| R10 | Audit reader as authorized Auditor and unauthorized other roles/tenants | One scoped event for success, no metadata leakage or cross-tenant read |
| R11 | Real-login `harborops_api`, `harborops_worker`, `harborops_projector` grant matrix | No ordinary correction INSERT or audit INSERT; no privilege escalation; worker/projector unchanged |
| R12 | Valid and invalid correction chains, concurrency, input revisions and projection rebuild | Prior L06 and M10 correctness preserved |
| R13 | Fresh and populated 12-to-new migration upgrade, evidence fingerprints and downgrade guard | No loss or mutation of retained history; migration order valid |
| R14 | Key missing, mismatch, rotation/restart and credential leakage scans | Fail closed, safely recover after valid provisioning, no exposed secrets |
| R15 | Related M16 audit HTTP/outage, M9/worker interaction and M10 correction regressions | Pass on exact remediation source |

Use realistic negative tests against actual restricted database logins. A unit mock cannot establish R04 to R11. Add focused effective security mutations for the authorization guard, proof binding, audit rollback and restricted grants. Run browser engines only for behavior affected by these changes; do not rerun every old gate merely because source SHA changed.

## 5. Efficient execution stages and stop conditions

1. **Preflight:** Confirm exact branch/base, working tree, GitHub Actions disabled, correct E: storage, Docker/SDK availability and current credentials handling. No modifications to historical evidence.
2. **Implement:** Small scoped changes with new or updated tests. Keep original failures and source fingerprints. Every commit includes `[skip ci]`; no attribution trailers or generated markers. Do not create repository instruction files.
3. **Cheap verification:** Static contract checks, SQL review, compilation, unit and focused contract tests.
4. **Restricted integration:** R01-R14 on disposable E:-backed PostgreSQL and separate runtime identities. Resolve failures and rerun affected cases only.
5. **Regression:** R15 plus scoped new-source concurrency, targeted mutation, outage and relevant browser checks. Run heavy database-intensive stages exclusively. Preserve source SHA and raw results.
6. **Independent review:** Package safe raw evidence with SHA-256 manifests, execution ledger and original failure attempts. Verify source lock and reconcile results independently before changing any scenario disposition.
7. **Final checkpoint:** Update current-source verification docs and acceptance matrix accurately. G10 S25 and merger remain separate decisions; deployment and releases remain unauthorized.

## 6. Reporting contract

Report exact branch and source SHA, changed files, migration effect, security invariants enforced, tests executed and not executed, raw evidence paths and hashes, defects found and repaired, residual risks and acceptance blockers. Distinguish executed results from claims. No full G10 rerun unless a material source or environment change justifies it. Do not run or enable GitHub Actions. Do not merge, deploy or release.

## 7. Verification environment and evidence policy

Keep permanent repositories, worktrees, caches, Docker/WSL data, databases and evidence under E: on the Windows verification machine. Short-lived C: system use is allowed only when unavoidable and must be cleaned safely. Preserve all earlier A1-A7 source-locked evidence under its original paths. Use isolated Compose project names and volumes for new runs. Do not delete unrelated Docker resources or evidence.

**Current gate state:** Policy choices approved on 2026-11-07. New-source implementation is committed at candidate runtime `3ef7a50859b5f023aa1457b373728888f6d1cba9`, but runtime verification and new-source S01 conformance are not executed. S25 and G10 acceptance remain pending.
