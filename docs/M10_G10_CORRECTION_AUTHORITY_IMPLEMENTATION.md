# M10 Correction Authority and Atomic Audit Remediation

**Decision:** M10 local policies approved on 2026-11-07, with L07 and L14 modified and L06 conditional.  
**Source:** `fix/m10-g10-l07-l14-remediation`.  
**Status:** Source changes and regression tests committed. Compilation, migration execution, database security tests and G10 supplemental evidence are **NOT EXECUTED** in this review environment. G10 acceptance pending.  
**Baseline:** The branch descends from independently reviewed G10 verification code. The original A1-A7 raw evidence applies only to frozen runtime `3e81b884b6a4cc782f84c58510793666786c5796`.

## Implemented changes

- Add SQL-only migration `20261107120000_M10CorrectionAuthority` after the twelve pre-remediation migrations, bringing the new-source inventory to thirteen. Preserve original migrations and all earlier evidence.
- Create a migration-owned correction operation that requires a short-lived HMAC-SHA256 proof binding the organization, shipment, actor, session, target, replacement, expected generation, reason digest, nonce and idempotency key. The database independently recomputes the correction digest and checks live Manager membership and session state under row locks.
- Keep the 32-byte verifier secret in a restricted PostgreSQL table. The signing key is supplied separately to the HTTP service and privileged migrator, not to the ordinary API SQL role or worker/projector. The API SQL role cannot read the key or nonce table.
- Revoke ordinary `harborops_api` INSERT on `shipment_tracking_corrections`. Grant execute on the constrained operation. The API route signs only server-authenticated correction requests.
- Write one sanitized `shipment.tracking_correction_created` M16 audit event in the same transaction as the correction. Failed required audit persistence raises a dedicated error and rolls back the correction and dependent state.
- Protect correction replay acknowledgments with a trigger that requires matching signed correction evidence from the same PostgreSQL transaction. Preserve existing correction generation, immutable index and M9 effect history.
- Keep proof nonces append-only for runtime identities and block downgrade of the new security migration when nonce evidence is retained.
- Remove the unsigned `ShipmentTimelineStore.CorrectAsync` entry point. Preserve rebuild operation and authorized human correction HTTP semantics.

## Configuration

`HARBOROPS_M10_CORRECTION_PROOF_KEY` must contain a randomly generated 32-byte secret encoded as base64. Keep it outside tracked files, logs and shareable verification archives. Give it only to the migrator and HTTP runtime, through `M10CorrectionProof__Key`; do not expose it to PostgreSQL worker or projector identities.

The isolated G10 Compose file and the new combined-stack override supply the configuration. The updated G10 runner creates a disposable random key when none is provided. Never reuse database passwords as proof keys. Key rotation requires the same new value to reach the migrator and HTTP runtime. Previously signed short-lived proofs fail closed after a rotation. No key is committed to the repository.

Missing HTTP key configuration returns a sanitized HTTP 503. Missing database verifier provisioning returns a dedicated dependency failure; invalid signatures or invalid live authorization are denied. A direct SQL role cannot forge proof from a known session UUID alone.

## Updated tests and checks

- The reference integration fixture provisions an isolated signing key.
- Existing M10 correction tests use authenticated HTTP instead of privileged unsigned store access.
- Restricted-role tests require direct SQL correction INSERT denial and no verifier-key or nonce-table reads.
- New focused integration tests exercise database-credential-only signature forgery, idempotency-key substitution, forged correction replay and full rollback when required audit persistence fails.
- The integration migration inventory and upgrade/downgrade tests account for the new migration.
- G10 stack source assertions and mutation variants cover the new database boundary, and check that a successful correction adds central M16 audit evidence.
- Static contract checks assert the signed operation, restricted grants, sanitized audit and absence of a direct correction INSERT in the store.

**Required execution:** Source-lock a new local run, execute compilation and affected unit, contract, integration, restricted-role, migration/upgrade, concurrent-revocation and audit outage tests. Confirm negative proofs against real separate PostgreSQL login identities. Apply focused effective security mutations to the changed boundary and run any impacted M9/M16 regressions. Preserve original failures. Do not automatically rerun the complete accepted G10 campaign.

## Residual risks and boundaries

The signing secret is protected against compromise of only the restricted API database credentials. Compromise of the complete HTTP runtime environment remains outside that narrower guarantee. A host process or privileged database administrator can access secrets; never claim protection against privileged hosts. The existing API role retains its previously approved projection/rebuild-table privileges, and rebuild replay semantics are unchanged. Nonce-evidence growth and controlled retention/archival require an operational review before sustained production use.

The existing M9 transport-event hold, 10,000 unresolved-work cap, M16 accepted audit access and retention policy, projector default-off state and all historical source evidence remain unchanged.

**Acceptance boundary:** The source implementation has not passed a new runtime verification. Do not mark S01 implementation compliant or S25 accepted until new raw evidence is reviewed. There is no merge, release, deployment or GitHub Actions authorization.
