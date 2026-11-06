# M10 Second-Audit Source Remediation

**Recorded:** 2026-11-06  
**Source audited:** `784689f483c64d19495ee8e9c1047464aaef8f32`  
**Independent audit:** [M10 Second Correctness Audit](M10_SECOND_CORRECTNESS_AUDIT.md)  
**Working branch:** `feat/m10-timeline-projections`, PR #23  
**State:** Eight second-audit findings have corresponding revised source and test paths. **No .NET build, PostgreSQL migration or G10 campaign has been run in the connected GitHub session.** Neither the original audit nor the first C01-C11 remediation register is rewritten or treated as passing runtime evidence.

| Finding | Revised source implementation | Runtime proof required |
| --- | --- | --- |
| R2-01: malformed migration | Replaced the corrupted file with one balanced M10 `Up` and `Down`, complete digest checks, scoped append-only timeline/correction/replay storage, protected downgrade and explicit input-version triggers. `scripts/check-m10-contract.mjs` guards structural regressions. | Compile entire .NET solution and worker; run fresh and M9-populated upgrades, schema snapshots and safe blocked/empty downgrades. |
| R2-02: projector permissions | Added the migration-time schema-pinned `m10_lock_projector_scope` security-definer function, restricted EXECUTE grant, separate projector connection, startup role/privilege verification and a local real-role lock probe. Projector no longer needs broad UPDATE grants on organizations or shipments. | Verify actual restricted-identity reconstruction, refused unauthorized table DML, session identity and cross-tenant permission resistance. |
| R2-03: poison-item starvation | Projector maintains a forward scope cursor, catches each item failure separately, continues through later dirty shipments, and wraps after exhausting the ordered set. A cursor-traversal integration candidate covers later-shipment eligibility without rebuilding the first. | Inject a persistent failing shipment before a healthy shipment, restart during sweep, ensure bounded retries and verify eventual healthy progress with retained error evidence. |
| R2-04: unbounded HTTP reads | M10 now stores immutable, provenance-validated timeline entries at the same transaction boundary as the accepted M9 effect, backfills entries during upgrade, and maintains a monotonic input revision. An indexed composite ordering supports actual DB keyset `LIMIT` pages. GET responses use a small versioned projection and never run a full reducer; a stale projection reports `Unknown`/pending, rather than claiming old state is current. Explicit rebuild still processes complete historical input. | Verify indexes and query plans with large histories, mixed-case IDs, late events, corrections and concurrent readers; benchmark rebuild CPU/memory and verify token invalidation across M9 effects and M10 corrections. |
| R2-05: race-test weakness | Concurrency test uses a second PostgreSQL backend PID and polls `pg_stat_activity.wait_event_type='Lock'` before committing the first transaction, rather than sleeping a fixed 150 ms. | Execute repeated adversarial interleavings across Read Committed, Repeatable Read and Serializable and inspect raw database outcomes. |
| R2-06: replay mutability | M10 migration adds an append-only trigger for update/delete on durable operation replay rows; direct SQL negative UPDATE and DELETE tests are authored. | Verify exact CHECK error under SQL mutation, source-locked replay after lost responses and rollback evidence retention. |
| R2-07: verification gaps | The E:-isolated G10 runner now includes source guards, database outage/restart, restricted-role lock probe, advisory and Trivy image scans, source-locked mutations, three-browser runs, disposable credential canary checks and evidence hashes. Its `run-g10-mutations.mjs` tests three non-equivalent reducer failures in clean exported source trees. | Run all stages locally; independently reconcile raw TRX, scripts, browser artifacts, outage and security evidence. A passing helper alone is not G10 acceptance. |
| R2-08: browser gaps | Added controlled stale-snapshot HTTP 409 and revoked-access 403 UI recovery cases alongside existing workspace/tenant-switch cases. Backend candidate tests also verify snapshot invalidation and Manager-only routes. | Execute Chromium, Firefox and WebKit. Add live in-flight membership revocation and source-effect mutation journeys to the complete G10 campaign rather than treating mocked response cases as proof of real concurrency. |

## Additional ordering correction

A final source review found that comparing UUIDs with .NET `Guid.CompareTo` can disagree with PostgreSQL UUID binary ordering. The timeline reducer now compares canonical 32-character UUID representations ordinally at all UUID tie-break positions. A unit test covers high-bit UUID values and the source guard checks all three comparison sites. This is **authored but unexecuted** and requires real PostgreSQL mixed-UUID keyset verification.

## Architectural and operating boundaries

The new M10 timeline entry table is a **derived immutable index**, not a new accepted event source. Each indexed row must match its M9 effect, M8 accepted event and inbox receipt. Its insert trigger is transactionally coupled to M9 effect creation; an M10 input revision is changed in the same transaction. Projected state is materialized at a specific input revision. When a revision changes before rebuild, the HTTP API deliberately returns pending/unknown observed state, a bounded provenance page and a protected version token. This replaces the earlier unbounded live-reduction-on-every-GET behavior. An explicitly authorized rebuild or a separately enabled, restricted projector advances materialization.

The indexed timeline and revision triggers now integrate with accepted M9 and G16 code; a fresh source-locked M9/M16/M10 regression campaign must verify migration order, worker-role permissions, M16 audit triggers and runtime lock behavior. **The 10,000 cap on unresolved M9 work is unchanged. M10 does not resolve parked opaque transport events, lift the transport admission hold or authorize production deployment. M16 has been accepted and merged separately; M10's compatibility requires new verification.**

## Technical verification status

- Static source guard assertions were inspected against the committed code; C# compiler and PostgreSQL parser were unavailable in this GitHub-only environment.
- All new and changed G10 unit, HTTP, SQL, browser, load, privilege and controlled-failure tests are **NOT EXECUTED**.
- The independent M10-01 policy decisions remain acceptance pending, including the explicit stale projection response contract.
- PR #21 stale-doc cleanup and PR #22 original M10 planning remain independently open; reconcile branch overlap before merge.
- G16 supplemental and combined M9/M16 evidence were reviewed and G16 accepted before this reconciliation. M10/G16 combined integration and final-SHA evidence remain pending. See [source reconciliation](M10_M16_SOURCE_RECONCILIATION.md).

**Disposition:** Source remediation candidate. PR #23 stays draft, no merge, no G10 acceptance or deployment.
