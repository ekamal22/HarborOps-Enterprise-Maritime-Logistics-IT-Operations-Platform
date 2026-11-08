# M11/M12 Targeted Local Acceptance Evidence Closure

**Post-acceptance status (2026-11-08):** The combined M11/M12 source passed independent evidence reconciliation and targeted hosted N01 confirmation. G11 and G12 were formally accepted and merged into `main` via [PR #33](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/33). See [gate closure](M11_M12_G11_G12_GATE_CLOSURE_20261108.md) for accepted policy, S25 review and M9 restrictions. Earlier runbook, governance and candidate-stage statements are preserved as dated historical checkpoints, not current blockers. No deployment or M9 hold release.

**Date:** 2026-11-08  
**Branch:** `feat/m11-m12-combined-integration`  
**Application verification SHA:** `f56d017d9b2619be56fe533f73ba1d11f63974c0`  
**Accepted baseline:** protected `main` at `6ead1d076946dd96b53401265a31efc35c4a666c`  
**Status:** Executed 2026-11-08 (see [local closure results](M11_M12_LOCAL_CLOSURE_RESULTS_20261108.md)); the plan text below is retained as written. Subsequent separate S25 decisions and gate acceptance are recorded in the formal gate closure.

## Rules and prerequisites

Use an existing clean E:-backed checkout; inspect the local working-tree status before creating an isolated worktree. E: holds durable repository clones, Docker/WSL data, package caches, browser reports, PostgreSQL data and retained verification evidence. Unavoidable temporary C: activity is permitted. Preserve existing volumes and private evidence. Create distinct disposable databases/containers with exclusive server ownership for tests changing PostgreSQL logins. Verify exact source SHA, migration hashes, test names, source provenance and credentials without logging secrets.

No GitHub Actions, deployment, production migrations, force-pushes or `main` merge. Run focused verification locally, not another unchanged full 341-case regression suite. If an implementation fix is needed, record a new SHA and execute change-impact checks before attributing results to that new revision. Commit and push coherent verified corrections to the authorized feature branch with `[skip ci]` messages. Do not silently alter established gate decisions or historical failures.

## Stage 1. Minimum preflight

1. Confirm local `HEAD`, branch, clean/dirty state, working-tree preservation and accepted baseline ancestry. Capture `git status --short`, `git rev-parse HEAD`, runtime tool versions, database identities and evidence root.
2. Run only inexpensive source/build and existing targeted tests affected by changes. The completed [final integration run](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42443065884) already passed 341/341 at the frozen application SHA.
3. Record the hash of each executable script and any database migration used. Keep raw logs and TRX/JUnit under a new immutable E:-backed evidence directory.

## Stage 2. G11 exact residuals

- **G11-S08, bounded provenance:** Using the live accepted-event correction machinery, inject three or more conflicting containers, same-timestamp GTIN/GTOT pairs, late and out-of-order receipts, multiple replacement corrections and superseded observations. Rebuild and reevaluate twice. Assert exact deterministic first witness, total count, true truncation, immutable selected accepted event/source/receipt/correction IDs, no unscoped SQL exposure, and no duplicate history. Preserve fixture IDs and SQL/HTTP assertions.
- **G11-S14/S18/S19:** Revocation/logout/org suspension at the Manager transaction boundary, concurrent disposition/rearchive/source drift, and deliberately rejected M16 audit write must fail closed with no partial exception, history or audit commit.
- **G11-S20/S21:** Time-only due scan after a missed scheduled checkpoint; stop/restart the evaluator and API; inject database outage and process termination before and after commit. Verify exact recovery, idempotency and no skipped/multiplied occurrence. Record UTC instants and local database outcomes.
- **G11-S13/S17/S24 remainder:** Fill gaps in Support/Auditor HTTP+SQL role matrix, same-key response replay under changed actor/body and targeted negative authorization mutants. Reuse successful earlier scope tests when still applicable.

## Stage 3. G12 exact residuals

- **G12-S20, mandatory measured query plans:** Seed representative isolated multi-tenant data volumes and selectivity distributions, including up to 5,000 export matches and sufficiently large nonmatching populations. Capture the **exact SQL emitted by search and export** with parameters safely redacted, then execute `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` for common text, status, port, voyage, UTC window, keyset page and export cases. Retain actual planner choices, row estimates versus actual rows, time, buffers, indexes and any sequential scans that are unacceptable for the fixture. Do not substitute a proposed index or `EXPLAIN` syntax check for measured results. Reject execution over production data.
- **G12-S09/S13:** Change, insert, archive and revoke records between live keyset pages. Verify no cross-tenant leaks, incorrect cursor reuse or false immutable-snapshot promise. Revoke/session-switch during in-flight search and export.
- **G12-S17/S19:** Test exactly 5,000 permitted rows, 5,001 rejected rows, 10 MiB minus/at/plus boundary, 15-second database statement timeout, full 25-second internal budget and 30-second proxy envelope. Test browser abort, multi-instance same-user rate and in-flight export restrictions, cancellation without partial CSV or export-prepared audit.
- **G12-S18:** Test forged/replayed/expired proof fields, missing/mismatched/rotated verifier keys, wrong runtime PostgreSQL role, audit outage or rejected write, transactional rollback and downgrade refusal after a retained authorized export.
- **G12-S15/S24 remainder:** End-to-end CSV Unicode/CRLF/formula-injection behavior, zero prohibited fields, remaining effective authorization/filter/limit mutants and complete scan/evidence safety controls.
- **G12-S21:** Preserve the documented NOT APPLICABLE decision for a Q2 2026 repository-owned production snapshot unless a real external historical safe copy is supplied. Do not repeat the already passed populated 13-to-15 migration without cause.

## Stage 4. Raw evidence closure

Capture source SHA, exact commands, environment/dependency version, fixture and isolated server identity, timestamps and durations, raw .NET TRX, raw browser JSON, SQL authority and EXPLAIN JSON, fault traces, mutation build/control/kill reasons, security scan JSON, explicit failure/skip/retry records, and safety/redaction checks.

Generate a SHA-256 manifest listing every expected report path. Independently re-read and hash every file, parse test outcome counts, reject missing/skipped or mismatched source records, and archive the raw results without credential leakage. A GitHub Actions summary or logged TRX hash alone is not sufficient.

Reconcile every G11-S01..S24 and G12-S01..S24 against the [G11 evidence matrix](M11_G11_EVIDENCE_MATRIX_20261108.md) and [G12 evidence matrix](M12_G12_EVIDENCE_MATRIX_20261108.md). Every mandatory case must have explicit PASS, FAIL, BLOCKED, NOT EXECUTED or evidenced NOT APPLICABLE with traceable evidence.

Obtain **separate independent G11-S25 and G12-S25 raw-evidence reviews**, then separately request gate acceptance. No review or recommendation itself authorizes protected-main merge, release, deployment or M9 hold clearance. Parked ARRI/DEPA, unresolved work cap 10,000, default-off M10 projector and null ETA remain unchanged.
