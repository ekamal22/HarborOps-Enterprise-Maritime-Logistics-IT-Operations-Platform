# M18 / G18 Disposable Verification and Evidence Runbook

**Prepared:** 2026-11-18  
**Status:** Draft procedure, NOT EXECUTED. No M18 tests, fixtures, migrations or runtime are present.  
**Scope:** M18-01 through M18-04 and the later independent G18/S25 review.  
**Restrictions:** No GitHub Actions, deployment, unapproved implementation, persistent C: writes, retained-volume modification or blanket Docker cleanup.

## 0. Preconditions and authorization

- Confirm the selected source SHA, exact Git tree, branch ancestry, no local changes and approved M18-P01..P20. G16 and G17 must remain accepted dependencies with disclosed G17 signature/P09 obligations.
- Name independent technical reviewer and separate formal acceptance authority in the final record without inventing an authorization. Prepare source/evidence access independent of campaign implementer.
- Set all project worktree, package caches, Windows TEMP, Docker/WSL storage, PostgreSQL fixtures, browser profiles, traces and evidence under explicitly configured E:-backed paths per [Local Storage Policy](LOCAL_STORAGE_POLICY.md).
- Inventory existing Compose projects and running processes without changing them. Verify unique `ho-verify-*` ownership naming, labels, ports and pre-test resource hashes.
- Lock dummy two-organization dataset, five fixed roles, multiple sessions, test-only alerts, correlation canaries, source tree, migrator runtime identities and accepted-version migrations.
- Reserve source-owned artifact directory and manifest names before any run. No code or fixture edits after freeze without restarting applicable gates on the corrected source.

## Additional required design boundary checks

Before starting the source-locked campaign, verify the [security and transaction contract](M18_SECURITY_AND_TRANSACTION_CONTRACT.md): the M18 migration installs specific M16 audit capture; creation uses an actor/organization-scoped operation key without a preexisting incident ID; Auditor incident-read grants are distinct, current and revocable; Manager creation discloses only a safe summary; service telemetry cannot reveal other tenants' impact; and closing a high-severity incident requires a real second eligible confirmer or an expressly authorized exception. The negative corpus must include copied secrets in notes, false positive closure, stale/absent monitoring, reused keys after role revocation and forged M17 evidence.

## 1. Preflight and proof-of-isolation

1. Record date UTC, OS, tool versions, running containers, requested profile, available E: space, dependency/package versions and exact source SHA.
2. Check disposable project name/ownership; fail if the target resource already exists, has missing labels or points to a retained volume.
3. Check dry-run migration SQL, exact pending migration list and API/worker/migrator grants; confirm no changes to accepted migrations.
4. Start a fresh disposable database via the approved scoped Compose profile. Seed only synthetic organization Alpha and Beta and allowlisted test incidents.
5. Verify normal default Compose does not start private M17 monitoring or new incident pollers automatically. Existing M9/M15/default-off conditions must remain unchanged.
6. Preserve stdout/stderr and exact exit codes even if preflight fails. Stop before destructive commands if any ownership or storage invariant fails.

## 2. Source-locked test stages

| Stage | Proof required |
| --- | --- |
| A | Approved policy diff, source and package lock hash, schema/contract and 75-assertion inventory |
| B | Clean .NET and frontend builds, unit tests, DTO schema/enum and translation contracts |
| C | Fresh and seeded PostgreSQL schema, migration order, grants, composite tenant FK, TRUNCATE/UPDATE/DELETE guards |
| D | HTTP+SQL five-role, two-org and revocation matrix; CSRF/Origin, selected-org and forged session |
| E | Seven states, invalid transitions, reassignment, reopen, immutable history, version competition, idempotent retry |
| F | M16 atomic audit rollback, note/evidence privacy and duplicate/denied mutations |
| G | M17 controlled alert capture with human confirmation, collector absence and expired snapshot interpretation |
| H | End-to-end recovery journey with real health/integrity evidence and follow-up corrective actions |
| I | Chromium, Firefox, WebKit flows in English and Turkish, stale-version recovery and organization switching |
| J | Q3 safe-copy historic snapshot or evidence-backed NOT APPLICABLE, plus accepted impacted regressions |
| K | Effective fault mutations, evidence hashes, no required skips and cleanup ownership |
| L | Independent source/evidence S25 review and explicit gate decision, separate from technical campaign |

Do not substitute a mocked data-layer response for a required real PostgreSQL/HTTP/browser check. When a case has distinct layers, record each layer separately and preserve failure evidence. An inconclusive or unexecuted assertion remains NOT EXECUTED/BLOCKED, not PASS.

## 3. Adversarial fault plan

- PostgreSQL unavailable before and during a write, commit uncertainty and restart. Ensure no false success; audit failure means no incident mutation; a separate denied-audit failure cannot grant rights.
- Two simultaneous transitions from one `expectedVersion`, repeated operation key same payload, same key altered payload, role revoked during transaction and user removed from incident's organization.
- Direct SQL unauthorized history UPDATE/DELETE/TRUNCATE, direct cross-org incident FK insertion, guessed foreign incident/evidence IDs and attempted unscoped list.
- Non-Manager attempt to modify shipment via incidental incident links, Support/Administrator attempt to read M16 Auditor audit endpoints, Manager attempt to open technical notes.
- Malformed UUID, size limit, invalid Unicode, mixed-case enums, unknown status, malicious cursor, tampered version, HTML/script note, false health proof, stale/absent M17 snapshot and malicious link.
- M17 collector down and telemetry expiry must not corrupt incident state or make a dashboard absence appear recovered.
- M9 transport remains held; background processing is never enabled to make incident demo pass.

## 4. Mandatory controlled journey

1. Log in as active, selected-org Support in Alpha; read live permitted M17 health signal in the owned disposable profile.
2. Introduce a controlled database-readiness fault with test ownership recorded. Confirm detection and safe bounded observation.
3. Prove **no incident exists yet** without explicit authorized creation.
4. Explicitly create incident with service, SEV, impact, symptoms, observation time and safe diagnostic snapshot; save server-originated incident reference.
5. Triage, assign active Alpha Support, investigate with safe observation, record mitigation and actual recovery.
6. Restore database or service. Verify independent health AND relevant data integrity checks; record actual UTC evidence, then monitor, resolve and close with approved verification/separation rules.
7. Reopen with new regression evidence, inspect immutable earlier closure, and re-resolve/re-close under the approved state policy.
8. Switch to Beta and try both guessed Alpha UUID and protected cursor. Confirm response and browser contain no Alpha incident or evidence.
9. Revoke Support role/session mid-flow and prove denial at commit. Test Manager limited and explicitly granted Auditor read-only behavior, including grant revocation.
10. Stop only the verified owned test containers, preserve raw logs/manifests and prove no retained resource changed.

## 5. Q3 historical compatibility

Read-only inventory existing snapshots/volumes. If a representative retained snapshot exists, clone it to a fresh disposable E: database under uniquely named project ownership, record before and after checksums for original, rehearse compatible schema upgrade and boot/read verification, compare critical row counts and schema permissions. Use safe synthetic records for new incidents. Destroy only the proven disposable copy after artifacts are retained. If no representative snapshot exists, write `NOT_APPLICABLE` with exact inventory command, reason and scope limitation. **Do not invent a PASS** or use the original snapshot as a test target. M19 is responsible for exhaustive backup/restore and RPO/RTO claims.

## 6. Artifact and reviewer contract

Use `evidence/g18/<source-sha>/<attempt-id>/` under the verified E: path. Retain raw source hash/tree, command matrix with exit codes and timings, complete unit/integration TRX, database SQL and migration/grant outputs, redacted HTTP responses, browser reports for three engines and two languages, sanitized incident-event/audit transaction proof, before/after retained-resource inventory, fault logs, Q3 disposition and every negative mutation result.

Machine manifest must include file path, byte length, SHA-256, stage, case/assertion IDs and classification. Reconcile actual files against expected list; reject missing, changed, unlisted required or wrong-source artifacts. Maintain `G18-S01-A01` through `G18-S25-A03` one-by-one; record PASS, FAIL, BLOCKED, NOT_EXECUTED or justified NOT_APPLICABLE where the specific contract permits it. No required case may be skipped without gate remaining open.

Independently verify sample raw traces and SQL/HTTP evidence, audit/role boundaries, source tree, P01-P20 decision signatures, replay/concurrency, three-browser evidence, Q3 applicability and all accepted regressions. The reviewer must sign a recommendation with source/evidence hashes, identity/independence, exception dispositions and PASS/NOT PASS. Only a separately explicit acceptance authorizes G18 closure and a later merge.

## 7. Safe cleanup

Stop only processes and containers labeled for the owned attempt. Record their exact IDs and termination statuses. Drop only uniquely owned disposable database/volume after artifact verification. Never `docker system prune`, never remove shared Docker images or WSL volumes, never delete historical failure logs, never overwrite original G16/G17 evidence. Preserve cleanup receipts and escalate any leaked resource without concealing it.

A finished cleanup is not test success. A passing technical run is not acceptance. This runbook is not execution authority.
