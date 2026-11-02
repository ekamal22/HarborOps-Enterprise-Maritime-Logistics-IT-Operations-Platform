# G6 Shipment Assignment Verification Plan

**Status:** PLANNED, NOT EXECUTED; G6 OPEN, acceptance pending
**Prepared:** 2026-11-02
**Milestone:** M6-01 through M6-04, shipment-container and shipment-voyage interval assignments
**Branch:** `feat/m6-container-voyage-assignments`
**Accepted starting base:** `main` `344c6eb96a9af600b5843cc97c56a8449174bad8` with G0-G5 accepted
**Verification authority:** [M6 local decision](M6_LOCAL_CONTRACT_DECISION.md), [architecture](M6_ARCHITECTURE_DESIGN.md), [implementation plan](M6_IMPLEMENTATION_PLAN.md), [roadmap](ROADMAP.md), [graph](execution-graph.json), [acceptance criteria](ACCEPTANCE_CRITERIA.md)

## 1. Hard gate rules

1. Every applicable G6-S01 through G6-S25 scenario needs actual observed results on one final implementation SHA, not only authored tests, source review, screenshots or a summarized PASS claim.
2. The complete twelve-rule M6-local contract requires an explicit accepted decision. G6-S01 is incomplete until approved. G6 acceptance and merge into `main` are separate approvals.
3. Run unit, contract, fresh PostgreSQL integration, repeated integration **on the same disposable database**, historical M5-to-M6 upgrade plus rollback/reapply, all affected accepted M0-M5 suites, and Chromium/Firefox/WebKit with zero mandatory skips.
4. Require direct PostgreSQL SQL invariants, real independently scheduled writers and archived/terminal parent races. Application preflight checks are not substitutes for DB constraints.
5. Preserve failed/superseded attempts by their own SHA/run IDs, source identity, process exit statuses, raw TRX, browser JSON, SQL error text sanitized for sharing, Docker logs, image scan JSON, mutation outcomes, C:/E: storage audit and SHA-256 manifest.
6. Fail closed on existing Compose projects, volumes or integration databases. All controllable project storage is on E:. No GitHub Actions or automated hosted verification is authorized.
7. The Q1 retained-historical-volume checkpoint is a **G6-only** conditional verification. When a representative historical volume/backup is absent, explicitly record `NOT APPLICABLE`, its discovery evidence and reason. Do not invent an old volume or treat that conditional N/A as a skipped executable test. Never mutate the only retained snapshot.
8. M24-04 remains the dedicated final accessibility gate, not a G6 requirement.

## 2. G6-S01 through G6-S25 matrix

| Scenario | Mandatory observed verification |
| --- | --- |
| **G6-S01** | Complete twelve-rule M6-local contract accepted explicitly; exact interval, route, archive, role, provenance and linked-voyage semantics correspond to actual code/schema without a new global D-number |
| **G6-S02** | UTC and half-open interval boundary matrix: Z/zero offset accepted, non-UTC/control/oversized rejected; null end, adjacent, zero length, reversed, historical, future timestamps; no inferred clock status |
| **G6-S03** | Container exclusivity: conflicting `(tenant,container)` ranges fail with actual SQL GiST exclusion; adjacent/other-container/other-tenant controls succeed; closed intervals still participate |
| **G6-S04** | Voyage association exclusivity: overlapping `(tenant,shipment)` intervals fail, adjacent succeed, many shipments may share a voyage; no duplicate unbounded open association |
| **G6-S05** | Composite tenant FK constraints: real other-tenant shipment/container/voyage UUIDs, missing and archived parents, forged org IDs rejected by service and independent SQL; valid own parents accepted |
| **G6-S06** | Voyage compatibility: route UUID equality including reverse-route rejection; inactive/Arrived/Cancelled voyages rejected for new association; known disjoint complete planned windows rejected; null-partial windows not fabricated |
| **G6-S07** | Immutable assignment identity/provenance: reject UPDATE of ids, tenant, start and actor, second closure/reopen, invalid version jumps, hard DELETE, direct-SQL history tampering, with positive close control |
| **G6-S08** | Fresh M0-M6 migration: indexes, composite FKs, GiST extensions/operator classes, exclusion constraints and guard triggers exist; EF model has no pending changes; no duplicate migration IDs |
| **G6-S09** | Seeded accepted M5-to-M6 upgrade, isolated rollback to M5 and reapply: user/session, org selection, M3 ports, M4 voyages/containers, M5 shipments and status history unchanged; newly added M6 rows removed only on disposable rollback |
| **G6-S10** | Q1 historical-snapshot checkpoint: safe discovery and representative read-only copy restore/migrate/re-read on E:, or documented `NOT APPLICABLE` when no representative snapshot exists; original never modified |
| **G6-S11** | Fixture correctness: two tenants, all five roles, same tenant-local refs, valid route-matched shipment/voyage pairs, multiple containers, past/future/open/closed assignments, fixture replay with no duplicate rows |
| **G6-S12** | Five-role two-permission matrix on every list/detail/create/close endpoint: Analyst and Manager manage; Auditor scoped read-only without generic M4 access; Administrator/Support denied by default |
| **G6-S13** | Authentication and session invalidation across all routes: anonymous, revoked/idle/absolute-expired, disabled account, changed security version, suspended org, no selection, removed membership, changed selected org |
| **G6-S14** | Tenant and IDOR: foreign valid assignment UUID, shipment UUID, container UUID, voyage UUID, foreign cursor and cross-tenant labels never exposed by list/detail/create/close; sanitized status and no existence leak |
| **G6-S15** | Strict HTTP body and CSRF/Origin: duplicate/unknown JSON keys, wrong MIME, oversized/malformed body, forbidden identity fields, missing/bad versions, cross-origin mutation, stable error codes and no-store/correlation IDs |
| **G6-S16** | Container allocation journey: create open/bounded, scoped history, exact-version closure, overlapping/duplicate retry 409, adjacent successor allowed, archived history still readable, no M5 workflow side effects |
| **G6-S17** | Voyage allocation journey: create, route/time validation, close and sequential reassignment, multiple shipments per voyage, no automatic M4 progress or M5 shipment-status updates |
| **G6-S18** | Parent lifecycle and direct SQL races: shipment/container/voyage archive vs open link; shipment completion/cancellation; voyage arrival/cancellation; link close vs archive; no inconsistent commit under interleavings or lock inversions |
| **G6-S19** | Concurrent writes: two writers same container, two voyages same shipment, stale closure, duplicate submit, queued role/session/organization revocation; exactly allowed winner commits, no ghost or partial row |
| **G6-S20** | Fault atomicity: injected SQL errors, cancelled and timed-out requests, database outage and interrupted transaction; no partial link or version; retry and post-commit lost-response semantics differentiated |
| **G6-S21** | Chromium complete browser journey: Analyst shipment/container/voyage attach/close/reassign, Manager control, Auditor read-only, conflict notice, parent guard and M5 unsaved-edit preservation |
| **G6-S22** | Firefox and WebKit complete journey, two tenants, role downgrade, tab return, concurrent edit, delayed list, stale response, logout; no missing/conditional skipped mandatory specs |
| **G6-S23** | Accepted M0-M5 full regressions: M1 sessions, M2 selected-org/roles, M3 parents, M4 catalog/version/route/identifier, M5 manual workflow/history and existing Playwright specs, fresh and repeat suites |
| **G6-S24** | Security proofs: effective tenant-filter and post-lock-revalidation mutation tests fail, GiST exclusion removal makes overlap tests fail, parent guard removal fails archive race; clean control passes; dependency and image scans report zero HIGH/CRITICAL |
| **G6-S25** | Operational bundle: Release warnings-as-errors, all stage exit statuses, PostgreSQL/API/browser outage and recovery, exact source and image IDs, owned-resource teardown, C:/E: storage accounting, raw original console/JSON/TRX, immutable archives, 185-style SHA-256 file checks and independent review |

S01 and S10 have decision/conditional semantics; never make them silent executable-test skips. The exact automated test names and their fixture matrix must be entered into the future `G6_EVIDENCE_INDEX.md` only after tests are authored and executed. Every S02-S25 required executable subcase must have direct assertions, not just name-based mapping.

## 3. Required test inventory

### Pure unit

- Interval parser: mandatory explicit UTC, null endpoint, strict `start < end`, adjacency, overlapping/open-ended intervals and forbidden edits.
- Voyage route equality and planned-window compatibility, including unknown bounds and reversed port identity.
- Typed role matrix, no implicit Administrator/Support privilege, Auditor narrowed view.
- Parent terminal/archival policy and repeated closure behavior.
- Domain methods must reject zero GUID, non-canonical status, incorrect fields and unintended auto-transitions.

### PostgreSQL and migration

- Fresh migration from empty accepted baseline, current EF/snapshot comparison and independent catalog checks for raw SQL exclusions and triggers.
- Historical M5 schema with representative shipment transition rows, voyages, containers, selected sessions and immutable IDs; migrate to M6, validate preservation, disposable rollback/reapply.
- Direct UPDATE/DELETE to forge association records; direct cross-org FK insert; invalid interval; concurrent direct SQL overlap from two connections; disable/remove exclusion mutation in isolated clone and confirm named tests fail.
- Execute parent archive/status transition from SQL while a link insert waits on a lock, and vice versa; check resulting relational invariants from a third session.
- Serialize use of database-wide test triggers and cloned schemas so parallel test methods cannot perturb each other. Never use an externally owned database.

### HTTP and concurrency

- Eight new routes across five roles and two tenants with real session/cookie/CSRF context.
- Positive creates/closes, duplicate and stale expectedVersion, non-overlap, missing foreign UUID and malformed body matrix.
- Parent archive/completion/cancellation guards and subsequent valid actions after close.
- Deterministic queued reads and writes followed by session revoke, organization selection change, membership downgrade and suspension.
- Post-lock revalidation security mutation probes. Fault injection must prove that a request reached the intended database operation, not infer it from wall-clock delays.
- Outage returns sanitized dependency-unavailable 503 and recovers without losing prior committed history.

### Chromium, Firefox and WebKit

The committed G6-specific Playwright inventory contains ten per-engine scenarios, including permission downgrade, scoped read-only views, conflict recovery, delayed cross-organization responses, logout and visibility revalidation. These scenarios have not yet been executed.

- End-to-end workflow from real authenticated Analyst tenant selection to shipment creation, container attach and voyage attach, view and close.
- Second tenant same reference names, foreign UUID denial, logout/role change clearing selected assignment rows.
- Cancelled/stale in-flight read, double-click/duplicate request, visible optimistic-conflict handling, port/voyage catalog retry and parent archived/terminal error presentation.
- Independent M5 shipment draft preservation across assignment refresh and browser tab return.
- All previously accepted M0-M5 browser specs retained, no conditional guard triggered. Dedicated accessibility audit remains deferred.

## 4. Local G6 pipeline and ownership

The E:-backed runner `scripts/verify-m6-local.sh`, stage and three-browser launchers, `scripts/assert-g6-evidence.mjs`, `scripts/check-g6-schema-contract.mjs`, `scripts/check-m6-assignment-outage.mjs`, `scripts/run-g6-mutations.mjs`, `scripts/check-g6-snapshot.mjs`, and `scripts/finalize-g6-evidence.mjs` are **authored and committed, NOT EXECUTED**. The evidence checker requires nonempty TRX suites, complete per-browser inventories, four killed mutations, the conditional historical-backup disposition, the exact source SHA, and complete owned-resource teardown. A technical PASS never constitutes independent acceptance.

- Windows checkout: `E:\APP IDEAS\HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform`.
- Verification root: `E:\HarborOps-Verification\m6`; `evidence`, `archives`, `cache`, browser profiles and scratch beneath it.
- Docker Compose project: `ho-verify-g6`; isolated `ho-g6-pg`, `ho-g6-sdk` and labelled containers/volumes.
- All controllable persistent and temporary project caches, NuGet, npm, browsers, Docker image data, logs and evidence remain on E:. C: only unavoidable attributable temporary activity, with safe narrow cleanup after preservation; no broad scan/cleanup.
- Preflight must confirm branch and clean worktree, exact SHA, no existing owned G6 resources, selected Docker Desktop E:-backed data storage and free space; do not touch other projects.
- Capture exact date, process exit code, raw service logs, source SHA, migration IDs, PostgreSQL version, image digests, strict build, test names and assertion outputs, stage ledger, storage before/after, teardown and manifest.
- Run independent Q1 snapshot stage once at G6, not every routine development/test invocation. A representative historical snapshot may be absent; record reasoned `NOT APPLICABLE`.
- Final G6 validator checks each scenario, per-engine JSON, no skipped required tests, complete TRX, exact SHA, owned teardown, storage and hashes. If S01 is pending, it may package incomplete technical evidence but must not claim gate PASS or acceptance.
- Every failed run is retained separately with its source revision. No old G5 evidence is overwritten.
- Explicitly **forbid GitHub Actions** and use `[skip ci]` on every commit.
- Stage order is fail-fast: static checks, image build, a warnings-as-errors compile gate, dependency review, web build, planning documents, the .NET suites, the security mutations and the Q1 snapshot checkpoint run before the Compose browser and outage stages. Every stage row records the source SHA, exit status and duration in seconds.
- Targeted rehearsals (`scripts/dev-m6-targeted.sh`, `scripts/dev-m6-browsers.sh`) use disposable resources, write under `dev/` and are never accepted as gate evidence. The gate itself always runs every mandatory stage once on one pinned SHA.
- The Q1 checkpoint can restore a retained historical Docker volume when `HARBOROPS_G6_HISTORICAL_VOLUME` names it. The source volume is mounted read-only only, its file fingerprint is taken before and after, and the copy, container and volume are disposable.

## 5. Exit and acceptance

The complete test matrix must execute on a final pinned M6 source revision; any correction requires full rerun on a new revision. Publish raw evidence archive and standalone SHA-256 checksum, with independent review of actual assertions, SQL concurrency/proofs and S01/S10 dispositions. Do not label S01 Accepted until the M6-local contract is expressly approved. Technical evidence and acceptance are separate. Do not merge M6 into `main` without explicit G6 and integration approval.

**Current disposition:** M6 implementation and corrective test/harness source are committed on the feature branch; no .NET, PostgreSQL, browser, snapshot or complete G6 local execution has run in this session. G6-S01 through G6-S25 remain NOT RUN, with the twelve-rule contract awaiting explicit approval. See [M6 implementation status](M6_IMPLEMENTATION_STATUS.md).
