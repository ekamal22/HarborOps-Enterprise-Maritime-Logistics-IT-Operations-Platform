# M5 Source Correctness Audit and Verification Gaps

**Date:** 2026-11-01  
**Branch:** `feat/m5-shipment-lifecycle`  
**Accepted base:** `main` at `18d00e6710097e4e31a87190be8edb65337d1b50`  
**Assessment:** Substantial M5 source authored and committed. No successful .NET 10 build, real PostgreSQL migration, HTTP integration or browser execution is available from this environment. G5 OPEN, acceptance pending.

## Source and behavior inventory

- `ShipmentPolicy` defines canonical reference codes, optional external reference, explicit UTC, status graph, and time validation. A unit test checks the proposed transition and role matrices.
- `ShipmentRecord` and `ShipmentStatusChange` are mapped in the live EF Core model and migration snapshot. The M5 migration creates `shipments` and `shipment_status_changes` with organization/port composite foreign keys, tenant-local reserved identity, field/time checks, immutable update constraints and database-managed workflow history. This migration has **not** been executed here.
- `ShipmentStore` implements selected-organization and active-session checks, Analyst/Manager writes, Auditor view-only access, ordered list/detail/history, exact-version updates, archive/restore and atomic creation/status transition persistence.
- Six protected HTTP routes implement the planned M5 contract, with content type/body limits, CSRF/Origin, error envelopes and no-store headers.
- React shipment list/detail/create/edit/archive/status history is wired for Analyst/Manager and the scoped Auditor read-only view. M6 assignment creation is intentionally absent.
- Unit, PostgreSQL integration, historic upgrade, SQL-guard, role/tenant, adverse payload, queued-revocation, fault-injection and Chromium/Firefox/WebKit browser tests have been authored. They have not yet executed.
- A dedicated M5 E:-rooted script, schema contract precheck, fixture preparer, outage checker and 25-case evidence index were added. The checker reports evidence for review, never acceptance.

## Source-level corrections applied during this implementation

1. No-op patch handling now evaluates all changed fields rather than prematurely rejecting a valid combined edit.
2. M0-M4 historical migration assertions were reconciled with the new additive sixth migration; the M4-only upgrade case remains pinned to the M4 migration.
3. The cloned verification runner originally referenced nonexistent G5 decision/index scripts. It now calls the retained, present G4 decision and index checks, then the new M5 schema contract check.
4. The shipment-specific PostgreSQL outage stage and its evidence requirement were added.
5. G5 script modules were syntax-checked in the available JavaScript runtime. The live and snapshot shipment mapping blocks were compared and found textually identical, and the SQL migration's expected index/constraint/trigger names were checked in source.
6. Internal shipment mutation actor identity was excluded from the public shipment record serializer, while remaining mapped for database persistence.

## Mandatory execution and coverage gaps

| Area | Current status | Required next action |
| --- | --- | --- |
| .NET Release compilation with warnings as errors | NOT RUN | Build at final SHA with .NET 10 SDK |
| EF Core snapshot correctness on runtime model | NOT RUN | Execute `MigrationSnapshotTests.ModelSnapshot_MatchesCurrentEntityModel` |
| Fresh/historical PostgreSQL migrations | NOT RUN | Apply M0-M5 on disposable PostgreSQL; upgrade seeded M4, rollback, reapply and inspect SQL |
| SQL trigger and manual history integrity | NOT RUN | Execute direct SQL bypass and fault-injection tests; verify PostgreSQL trigger depth, transaction rollback and uniqueness |
| Full role/verb/session and queued-revocation matrix | INCOMPLETE COVERAGE | Expand to all six routes and five roles with multiple queued revocation kinds; prove interleaving order |
| Three-engine React/browser execution | NOT RUN | Run every declared spec in Chromium, Firefox, WebKit, including delayed-read and conflict cases |
| Full accepted M0-M4 regressions | NOT RUN | Execute unit, contract, fresh and repeat integration and existing browsers on one SHA |
| Outage/recovery, dependency and image security scans | NOT RUN | Run disposable Postgres/API/web services, faults, Trivy and cleanup |
| G5-S01 local contract decision | OPEN | Record approval explicitly; do not infer G5 acceptance |
| G5-S01 through G5-S25 independent evidence | NOT RUN | Preserve raw TRX, browser JSON, stage logs, source identity and SHA-256 archive on E: |
| Dedicated Compose project isolation | PARTIAL | The current runner uses a separate E: M5 root but invokes shared `ho-verify`/G2 stage infrastructure; verify single-run isolation and improve project naming before executing alongside other milestones |
| C: temporary cleanup and storage measurements | NOT RUN | Measure local drives and remove only positively attributable short-lived C: files; no destructive blanket cleaning |

The G5 evidence checker references several named test classes; its current name-to-scenario mapping is **not evidence of complete subcase coverage**. Review the requirements in `M5_VERIFICATION_PLAN.md` independently, including assertion strength, test failure mechanisms, race observation and direct SQL mutations.

## Correctness conclusion

M5 has a committed implementation across domain, persistence, API, UI and initial regression suites, but **end-to-end correctness has not been established**. Some mandatory adversarial subcases and full runner isolation still require additional work. The milestone must not be labeled verified, accepted, merged or ready for G5 acceptance solely on this source audit.

No GitHub Actions were triggered. No accepted M0-M4 migration files or `main` branch refs were changed. Full local verification must run on the current final feature-branch SHA using E:-backed disposable resources, with independent review and explicit acceptance required before integration.
