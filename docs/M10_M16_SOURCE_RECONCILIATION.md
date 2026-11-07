# M10 / Accepted M16 Source Reconciliation

**Post-merge disposition (2026-11-07):** G10 S01/S25 passed independent review; the M10 security remediation is accepted and integrated into protected `main` through [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28). See [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md). Earlier pending or unexecuted statements below are preserved as source-locked historical checkpoints. The M9 transport-processing hold and deployment restrictions remain.

**Recorded:** 2026-11-06  
**Accepted base:** `main` at `25e89026737063294a32bbd081ced400d19c6b3d`, including PR #25 G16 integration, PR #26 post-merge reconciliation and PR #27 living-documentation updates.  
**M10 head before reconciliation:** `3159baa508f840596e1fe1981164296c1d8dffca` in draft PR #23.  
**Integration merge commit:** `82c5d6eec34469ac1813d99270b4108788c9435d`, with parents `3159baa508f840596e1fe1981164296c1d8dffca` (M10 source) and `25e89026737063294a32bbd081ced400d19c6b3d` (accepted main). The integration is on draft PR #23 only, not on protected `main`.

## Historical reconciliation scope and later update, 2026-11-07

This reconciliation records the original merged twelve-migration M10/M16 candidate and its original G10 handoff. PR #23 later merged on 2026-11-06, and technical evidence on frozen source `3e81b88` was independently reviewed. On 2026-11-07, fourteen M10-local policies were approved, modifying L07/L14 and conditioning L06. A separate unmerged security remediation at runtime candidate `3ef7a50859b5f023aa1457b373728888f6d1cba9` adds a thirteenth migration, constrained signed correction authority and atomic M16 correction audit capture. The later source has no executed new-source verification; the original evidence and granted-role descriptions below remain historical and must not be treated as present remediation grants. [Current implementation](M10_G10_CORRECTION_AUTHORITY_IMPLEMENTATION.md).

## Source reconciliation decisions

| Area | Resolution | Remaining proof |
| --- | --- | --- |
| Database migration ordering | Preserve the merged M16 audit migration `20261104180000_AuditEntries`, followed by accepted M9 `20261104190000_TrackingWorker`, then unaccepted M10 `20261105180000_ShipmentTrackingProjections`. Total full migration inventory: **12**. Preserve the M16 EF model snapshot; M10 adds SQL-only derived tables and triggers. | Fresh 12-step migration, populated G16/M9 upgrade, recorded EF snapshot check and safe rollback of empty/retained histories. |
| API bootstrapping | Use the accepted M16 `Program.cs` with audit correlation scope, audit routes and migrator-time role provisioning; add M10 store, routes and optional restricted projector registration. | Runtime boot, audit/read/role and M10 CSRF, tenant and Manager tests. |
| Rate limiting | Preserve the M16 `audit-read` limiter and M1 `login-ip`; add `m10-mutations` without disabling either. | HTTP 429 behavior and authorization recovery for both route families. |
| Privileges | Reuse M16 `AuditRoleProvisioner` for `harborops_api` and `harborops_worker`. The new M10 conditional grant gives **only** the API role correction/replay INSERT, projection/application INSERT and projection UPDATE. The worker gains no direct M10 table rights. M10 correction-head generation is changed only inside a migration-owned, schema-qualified SECURITY DEFINER trigger. | Connect with real restricted API, worker and projector identities; deny forged M8/M9 effects, forged audit, source/index/correction-head mutations. Validate direct correction authorization and replay. |
| M9 worker to M10 handoff | A migration-owned M10 AFTER-INSERT trigger reads the immutable M9 effect/event/receipt and atomically appends a provenance-validated index entry and increments an input version. Source M9 claim/finalization remains unchanged. | Test M9 worker finalization and audit capture with M10 triggers, including failure injection and lock-order races. |
| M16 audit interaction | Preserve accepted `audit_entries` table, capture triggers, Auditor-only reads, access audit and historic source hashes. M10 correction remains separately append-only and does not rewrite M16 accepted audit evidence. | Execute G16 authorization, direct SQL audit immutability, event capture and cross-tenant regressions on final M10 SHA. |
| Historical regression tests | Full-schema assertions become 12, while deliberately partial migrations and failed downgrade counts remain at their correct 8, 9, 10 or 11. Extend the combined restricted-role matrix with M10 positive and negative grants without relaxing the worker's exact privilege set. | Execute all G0-G9 and G16 integration tests on clean and populated databases. |
| Verification environment | `compose.g10.verify.yaml` has separate privileged migrator, M16 restricted API and worker credentials, independent projector role, and no default sustained worker/projector processing. Its runner creates E:-backed disposable credentials and artifacts. | Check effective Compose environment, source hash, migration-time grants, outage/restart and credential safety. |

## Explicit restrictions

- **G16 is already accepted and merged** through PR #25 and reconciled by PR #26. Do not reclassify it as pending or rewrite its historical evidence.
- **M10/G10 acceptance is pending**. This is a *source reconciliation*, not a passed .NET compile, PostgreSQL execution, role check or G10 campaign.
- The 10,000 unresolved-item **M9 transport-backlog hold remains in force**; M10 only indexes M9-resolved equipment events and does not resolve parked opaque ARRI/DEPA transport events.
- M10's correction/derived-field privileges are new and must receive a dedicated restricted-role correctness and threat-model review before acceptance. The added source checks cannot establish SQL security independently.
- Test with exclusive disposable E:-backed resources; do not activate GitHub Actions, modify accepted G16 evidence, reuse retained database volumes, merge into protected `main`, or authorize public/production deployment.

## Gate handoff

1. Run `node scripts/check-m10-m16-integration.mjs` and `node scripts/check-m10-contract.mjs` on the exact final SHA. These are static preflight checks only.
2. Compile the API/worker and execute all unit, contract and integration cases against fresh and populated 12-migration PostgreSQL databases. Preserve original failing evidence.
3. Exercise separate `harborops_api`, `harborops_worker` and `harborops_projector` identities and the M16 audit/retention and M10 correction/rebuild paths with direct SQL adversarial cases.
4. Complete G10 three-browser, concurrency, outage, performance, mutation, vulnerability and security checks, reconcile raw artifacts independently, resolve M10-S01 choices and obtain explicit G10 acceptance before a merge.

**Original handoff disposition (2026-11-06, historical):** Source-integrated candidate with G10 review pending at that time. Later source-locked L07/L14 verification, S01/S25 acceptance and security remediation were completed and integrated through [PR #28](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/28); see [G10 gate closure](M10_G10_GATE_CLOSURE_20261107.md).
