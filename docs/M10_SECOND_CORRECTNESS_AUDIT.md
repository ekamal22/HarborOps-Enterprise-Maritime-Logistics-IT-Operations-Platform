# M10 Second Source Correctness Audit

**Audit date:** 2026-11-06  
**Source revision:** `784689f483c64d19495ee8e9c1047464aaef8f32`  
**Pull request:** [PR #23](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/23), `feat/m10-timeline-projections`  
**Baseline:** accepted M9/G9 on `main`, SHA `b4f7b93f1ee8135b9a16b605ded315b3fbc036cc`  
**Disposition:** **BLOCK MERGE, G10 ACCEPTANCE AND DEPLOYMENT.** The prior 11-issue remediation register describes intended fixes but is not a passed integration result. No runtime files are changed by this audit.

## Method and limits

Compared the corrected M10 migration, reducer, streaming store, HTTP endpoints, hosted projector, role grants, browser component, new unit/integration/browser tests, verification runner, and status documents against the accepted M8/M9 persistence and G10 plan. Examined source directly at the fixed SHA, including exact line locations below. This is a **static review**: the current environment has no .NET SDK, PostgreSQL server or Docker; no C# compilation, migration execution, tests, browser campaigns or direct database failure injection were performed. Findings about privilege requirements are supported by the PostgreSQL privilege reference: https://www.postgresql.org/docs/current/ddl-priv.html.

## Critical

**G10-R2-01: The M10 migration is syntactically corrupted and prevents compilation.**  
`src/HarborOps.Infrastructure/Persistence/Migrations/20261105180000_ShipmentTrackingProjections.cs` (lines 88, 128 and 230-464).

- The correction digest check at line 88 is unterminated; at line 128 the operation-replay digest check runs into a fragment of a trigger declaration.
- The class and `Down` method close around line 230, then additional C#/SQL content continues outside the class. The file contains **four occurrences** of `protected override void Down` and repeated old table/function declarations.
- The checked-in file cannot be compiled as a valid C# migration, independently of whether the intended PostgreSQL constraints would pass.
- **Required:** Rebuild the migration from one coherent `Up` and one coherent `Down`, with balanced raw strings and intact SQL. Add compiler/static migration integrity checking before the heavier test stages. Execute fresh and populated M9-to-M10 migration, blocked populated downgrade and clean empty downgrade. Preserve the current file in Git history for evidence.

## High

**G10-R2-02: Restricted projector cannot acquire its own shipment and organization row locks.**  
`ShipmentTimelineStore.cs` lines 160-174; `scripts/configure-m10-projector-db.sql` lines 8-20; `TrackingProjectionHostedService.cs` lines 24-44.

- The store executes `SELECT ... FOR UPDATE` on `organizations` and `shipments`. The projector grant script provides only `SELECT` on those tables, not `UPDATE`.
- PostgreSQL requires an `UPDATE` privilege on at least one column for `SELECT ... FOR UPDATE` / `FOR SHARE` as well as `SELECT`. The startup privilege probe checks selected M9/M10 permissions but does **not** perform this lock operation.
- With intended restricted credentials, the projector can pass its startup probe but fail when it reaches a rebuild. Do not fix by indiscriminately granting broad table updates.
- **Required:** Design a scoped locking mechanism compatible with least privilege (for example a narrowly scoped, independently reviewed security-definer function with a pinned search path), then test actual rebuild with the real restricted role and explicit denied DML on immutable M8/M9 tables.

**G10-R2-03: The projector can starve later shipments after a persistent failure.**  
`TrackingProjectionHostedService.cs` lines 53-84; `ShipmentTimelineStore.cs` lines 382-417.

- The sweep requests the first 16 dirty shipments, ordered by organization/shipment. An exception in any `RebuildAsync` escapes the per-shipment loop into a single outer catch.
- A persistent failure for an early item can abort the rest of that batch on every sweep; the next sweep returns the same ordered dirty items. Healthy shipments after the failed item or beyond the first page can be indefinitely delayed.
- **Required:** Handle failures per shipment with durable bounded retry/quarantine/backoff or a fair checkpoint/lease queue. Add a poison-item-plus-healthy-item integration test proving healthy work proceeds and failure visibility is retained.

**G10-R2-04: HTTP keyset pages still perform full-history scans on every request.**  
`ShipmentTimelineStore.cs` lines 24-94 and `ReadAsync` lines 134-155; `ShipmentTimelineEndpoints.cs` GET handlers.

- The store iterates every event, computes the full digest, then applies the `after` keyset only in application memory. The SQL does not constrain the requested key range or impose a `LIMIT`; every page and status GET re-reads all history.
- Removing the former 10,000-row hard limit avoids one correctness failure but introduces potentially unbounded database work and repeated full sorts, with no endpoint-specific read rate limit. A shipment with many events may repeatedly time out, making timeline access unavailable.
- **Required:** Separate bounded database-level keyset retrieval from incrementally maintained or versioned projection/snapshot consistency. Define measured query, memory, CPU, timeout and concurrency thresholds. Exercise realistic large and adversarial shipments under independent user sessions.

## Medium

**G10-R2-05: Correction race verification is timing-based, not a demonstrated race.**  
`tests/HarborOps.IntegrationTests/TrackingProjectionG10Tests.cs` lines 272-329.

- The test starts two transactions, submits one write, starts the second on a background task, waits a fixed 150 ms, then commits the first transaction. There is no synchronization or database observation proving that the second writer reached the blocked point before the first committed.
- A passing test therefore does not establish that the dangerous concurrent interleaving was exercised. The test also lacks a no-race control and failure-injection comparison.
- **Required:** Use explicit synchronization and lock observation, then source-locked repeated interleaving tests across Read Committed, Repeatable Read and Serializable. Verify exact commit/rejection outcomes, retained correction generation and no lost updates.

**G10-R2-06: Operation replay records lack the immutability enforcement claimed for durable replay.**  
`20261105180000_ShipmentTrackingProjections.cs` replay table (lines 112-127) and correction-only immutable trigger (lines 193-202).

- The correction table has an update/delete guard, but the `shipment_tracking_operation_replays` table lacks a corresponding immutable-update/delete trigger or an explicit restrictive write policy in the migration.
- The API uses those records as durable idempotency evidence. Their mutability by database roles that possess ordinary DML rights weakens the documented replay invariant.
- **Required:** Choose and enforce an explicit replay retention/immutability contract, including least-privilege grants and direct SQL negative tests, without weakening the correction history guard.

**G10-R2-07: Local G10 verification script is not a complete security or failure campaign.**  
`scripts/verify-g10-local.sh` and `tests/browser/playwright.config.mjs`.

- It now runs builds, many tests, role metadata checks, browser fixtures and Chromium/Firefox/WebKit, but does not execute the G10 security mutation families, image/dependency vulnerability scans, recovery/outage probes or a sealed evidence safety/redaction review comparable to accepted G9.
- A SHA-256 manifest proves file integrity only, not that logs or Playwright artifacts contain no credentials, personal data or sensitive tokens. Playwright HTML reporting is configured under the browser worktree rather than explicitly under the isolated evidence root.
- **Required:** Add separately labeled, source-locked security, outage and evidence-safety stages. Preserve failed attempts and independently reconcile raw TRX, browser, vulnerability and mutation evidence before any gate decision.

**G10-R2-08: Browser regression assertions do not prove correction, snapshot or revocation safety.**  
`tests/browser/specs/tracking-timeline.spec.mjs` (two scenarios).

- Both new G10 browser cases cover a visible tracking panel and changing organization selection. Neither triggers an event/correction mid-pagination, asserts protected cursor error recovery, tests revoked membership during a request, or verifies that the UI cannot represent a stale projection as current.
- The updated HTTP tests cover portions of these contracts but cannot substitute for an end-to-end three-browser run of the user-facing asynchronous behavior.
- **Required:** Add controlled mutation-during-read, page-after-correction, membership revocation and authorization-switch scenarios in all supported engines, plus clear synchronization rather than fixed sleeps.

## Preserved or positively observed boundaries

- PR #23 is a draft and remains unmerged; accepted `main` is unchanged.
- The M10 code does not alter the accepted M8 receipt and M9 worker source.
- The M10 store still requires an M9 effect tying an event to the selected shipment instead of guessing a transport reference.
- Separate M5 manual workflow and M10 observed tracking are retained.
- Manager-only correction/rebuild endpoint checks, protected cursors, rate limiting, idempotency request hashing and a fail-closed projector startup probe are present in source. Their correctness is not established by source inspection alone.
- The mandatory M9 hold on sustained transport-event traffic remains binding. G10-S01 decisions and explicit G10 acceptance remain pending.

## Gate disposition and remediation order

1. **Stop merge. Fix R2-01 first** and run a clean C# build plus migration integrity checks.
2. Fix R2-02 and R2-03, then demonstrate projector rebuild and progress under a restricted real PostgreSQL identity, including permanent failure injection.
3. Resolve R2-04 with bounded paging/projection consistency and measured scale thresholds.
4. Resolve R2-05 through R2-08 and execute complete source-locked verification on the final amended SHA, including direct SQL, role/session, data integrity, three-browser, security mutation, outage and old-milestone regression gates.
5. Obtain independent raw-evidence review and explicit G10 acceptance. No public deployment, sustained transport-event activation, G10 closure or merge authorized.

**Overall:** One critical source/build defect, three high-severity runtime or availability risks and four medium-severity integrity/verification gaps identified. No executed tests are claimed by this audit.
