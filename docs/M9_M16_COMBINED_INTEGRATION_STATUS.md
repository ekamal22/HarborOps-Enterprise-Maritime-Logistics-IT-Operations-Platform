# M9/M16 Combined Integration Candidate

**Recorded:** 2026-11-05
**Integration branch:** `integrate/m9-m16-g16-candidate-20261105`
**Base:** Accepted M9/G9 `main` at `b4f7b93f1ee8135b9a16b605ded315b3fbc036cc`
**M16 source:** `feat/m16-audit-access-hardening` at `c499741a4d291515cda4de35593048fd90c1c83e`, with standalone runtime evidence at `e5711573839b185a2dfc7d9f2c64ce4fc18d4c13`
**Status:** Combined technical verification executed on runtime source `79318c6b6126362a0b26a1452aad6d2b48fdf4b4` (a failed run plus a same-source supplement, both preserved); see the [executed verification record](M9_M16_COMBINED_VERIFICATION.md). Three corrective findings were fixed on this branch. The campaign script reparse was followed by independent review of the uploaded raw evidence (544/544 manifest entries). G16 and four Option A integration decisions were accepted on 2026-11-06 ([gate closure](M16_G16_GATE_CLOSURE.md)); PR #25 was separately authorized and merged on 2026-11-06 at `47ba55e2`, following the focused F001 gate extension.

## Source reconciliation

- Preserve M9 queue routes, worker implementation, attempt history, immutable processing effects, `TrackingWorkRead` and `TrackingWorkRedrive`.
- Add M16 tenant-scoped audit endpoints, capture triggers, audit actor context, `ViewAuditEvents` and restricted API/worker database identities.
- Combine registrations in `Program.cs` and compose all three permissions without granting Administrator implicit access to tracking work or audit records.
- Retain both SQL migrations, `20261104180000_AuditEntries` followed by `20261104190000_TrackingWorker`. Merge both EF model branches and the snapshot without losing M9 work-state/effect mappings.
- Correct historical full-head migration assertions to 11; retain intermediate stage counts and the guarded downgrade semantics. The M8 history test populates receipts before installing M16 auditing.
- The M16 migrator provisions the API and worker runtime roles when distinct runtime secrets are provided. The M9 role script is retained only as an existing standalone/manual path, not part of the combined stack startup. The migration-time provisioner was accepted as the authoritative combined-stack mechanism on 2026-11-06.
- When M9 tables exist, grant API INSERT on `tracking_work_attempt_events` for the separately authorized Manager redrive transaction. The worker still has no direct INSERT on attempt history. All M9 transition-trigger and row constraints remain in place.
- Add direct SQL negative coverage of the M16 1024-byte audit metadata CHECK and positive/negative role-grant assertions.

## Original integration risk register, resolved for G16 on 2026-11-06

The following questions were open when this implementation plan was prepared. The first three and F001 guard selection were later resolved by the four approved Option A decisions in the [G16 gate closure](M16_G16_GATE_CLOSURE.md). Other observations remain disclosed; no historical verification result is being rewritten.

1. Confirm automatic migrator provisioning as the authoritative combined role and grant path. Do not run the M9 manual role script as a second provisioning authority during the combined campaign.
2. Decide whether immutable M9 attempt/effect evidence satisfies the audit requirement for worker transitions and manual redrive, or whether M16 needs separate audit entries. Do not implement an unstated duplicate audit policy.
3. Decide whether to retain the documented privileged migrator threat-model exclusion or implement a restricted migration identity through a separate migration/security design.
4. Inventory whether any retained databases carry a pre-correction M16 migration version and decide their controlled remediation. Never reset retained databases to force a pass.
5. Explicitly disposition the late-commit audit-cursor ordering caveat and the supplemental source-attestation caveat.

## Required integrated verification

First perform no-runtime source checks, clean warning-as-error .NET build, EF discovery and snapshot parity, web unit/build and focused unit/contract tests. Fail fast and repair before resource-heavy work. Then use a disposable E:-backed PostgreSQL database for a focused migration and scoped-role smoke suite, including actual Manager redrive with the API role, actual worker lifecycle with the worker role, append-only guards, M8/M9 history preservation and Auditor-only reads.

Run a separate **accepted-M9 populated upgrade** by creating a disposable database at the accepted `main` SHA with an M9 event, receipt, work attempt and processing effect, then upgrade that same disposable database with the frozen combined candidate. Fresh combined 11-migration installation alone cannot establish compatibility with a previously applied M9 database.

After preflight is clean, run one exclusive full combined .NET integration pass, then a second independent database integration pass only if required by the G16/G9 gate contract; use a fresh disposable database or isolated schema and keep both results. Do not run competing PostgreSQL or Docker suites simultaneously. Reuse already passing unchanged M9 and M16 standalone evidence rather than reexecuting their complete historical campaigns.

For the integrated stack, layer `compose.g16.verify.yaml` with `compose.m9.worker.yaml` under a unique Compose project name, distinct loopback ports and a dedicated database volume. Start the worker only during controlled worker checks; do not enable sustained transport-event processing. Validate role readiness and least privilege, M9 redrive and work state, M16 audit access, cross-tenant denial, auth revocation, three-browser regression, database outage/recovery, fault injections and dependency/image scans. Keep targeted mutation families focused on changed permission, audit and worker integration boundaries.

Freeze the final integration source SHA before the full gate. Preserve raw TRX, Playwright JSON, SQL/HTTP probes, mutation ledger, Compose and image reports, exact runner/stage exit codes, sanitized environment metadata, teardown record and SHA-256 manifest. Independently reparse and verify evidence. Report original failures separately from corrected reruns.

## Operating and merge restrictions

- Accepted M9's sustained transport-event hold and 10,000 unresolved-item limit remain binding.
- No GitHub Actions. Use `[skip ci]` on all commits. All controllable permanent project storage is on E:. C: is limited to unavoidable cleaned temporary files.
- Isolate resources and do not disrupt or delete unrelated worktrees, volumes, images, databases or evidence.
- G16 was accepted on 2026-11-06 for the verified runtime SHA. Separately authorized PR #25 integration completed at `47ba55e2` after the F001 source-locked extension; gate acceptance alone did not authorize merger.
- Original standalone M16/G16 reports and historical accepted M9/G9 evidence remain unchanged. The independently reviewed S07/S08/S28 supplement remains a separate record, with its exact-source attestation limitation.
