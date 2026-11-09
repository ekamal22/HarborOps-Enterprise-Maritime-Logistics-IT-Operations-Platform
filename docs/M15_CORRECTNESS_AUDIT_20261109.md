# M15 Correctness Audit: Notification State and Browser Verification

**Date:** 2026-11-09  
**Audited application and browser source:** `26c858d39c2b893f45f695cdbee737afed6c0aa3` on `feat/m15-scoped-notifications-20261108`  
**Disposition:** Source-level remediation committed, final Actions verification pending. G15 not accepted. No merge, migration, delivery activation or deployment.

## Method and scope

Read the current feature-branch implementation and compare the M15 notification browser scenario to the previously passing integrated M13/M15 browser source. Inspect the failed GitHub Actions logs, API authorization/cursor/CSRF/idempotency path, notification store, M11 outbox trigger and M16 audit mutation, restricted-role SQL grant and RLS functions, background dispatcher control, React refresh/pagination/mutation state, disposable fixture session handling and M13 integration boundaries.

This is a targeted repository source correctness audit supported by already executed hosted evidence. It is **not** an independent G15-S25 review, complete negative mutation campaign, production load qualification or a substitute for source-locked execution of this revision.

## Evidence retained from the latest completed run

[GitHub Actions 42542518598](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42542518598) executed source `40aa049b88afbcf29d1ce40a3792524cf93ba990`.

| Job | Executed result |
| --- | --- |
| M15 source and regression checks | PASS; .NET build, **299/299 unit**, **46/46 contract** |
| M15 scoped PostgreSQL and M11 migration regressions | PASS; **26/26 selected integration** and **5/5 SQL/security boundary** cases |
| Disposable M11-to-M15 hosted notification fixture | PASS; real exception, opt-in delivery, scoped recipient/API and live link |
| Chromium notification browser spec | FAIL; **5/6 PASS**, one locator strict-mode violation |
| Firefox and WebKit notification browser specs | NOT EXECUTED; runner stopped at Chromium |
| Full G15 matrix and independent S25 | NOT CLOSED |

### F01: Ambiguous Acknowledged state assertion

**Root cause:** `tests/browser/specs/notifications.spec.mjs` selected `center.getByText("Acknowledged",{exact:false})`, matching both a filter `<option>` and the personal receipt display paragraph. The application had successfully performed its MarkRead and Acknowledge transitions. Playwright rejected the ambiguous locator before asserting the final state.

**Remediation:** Assert `center.locator("li p").filter({hasText:/\\|\\s*Acknowledged$/})` instead of globally searching the notification region. The combined M13/M15 branch previously passed this class of list-scoped assertion in Chromium, Firefox and WebKit; that PASS is historical and cannot be transferred to this source.

**Source fix:** `c5a03c426cfd2eed413813981e3f2ec709721bbc`. The change does not alter authorization, receipts, SQL or runtime rate limits.

### F02: Stale pagination lock after overlapping refresh

**Root cause:** `NotificationCenter.loadMore()` captured `serial.current`. If a manual or periodic refresh advanced the serial while page retrieval was in progress, the stale response was safely ignored, but `finally` also refused to clear `paging`. This could leave Load more disabled indefinitely until remount.

**Remediation:** Preserve the stale-response guard for data writes and errors, but always clear the pagination activity flag when the same component remains mounted. Notification center is keyed by selected organization and role in the workspace. Added a Playwright race regression that holds the cursor response, refreshes the first page, releases the stale response, and asserts Load more becomes enabled.

**Source fix:** `26c858d39c2b893f45f695cdbee737afed6c0aa3`.

## Other reviewed invariants

| Boundary | Source inspection | Executed evidence limit |
| --- | --- | --- |
| M11 immutable event origin | Outbox is created by `AFTER INSERT` on M11 history for Opened, Resolved and AutoResolved; unique history identity | Selected SQL regressions passed on `40aa049b`; no new run after UI fixes |
| Recipient authorization | Active Analyst/Manager/Auditor membership, source membership ID/version, current organization/user/session and shipment, m11-signed scoped read, RLS | Selected five security/fault cases passed; full five-role, revocation, cross-tenant and security mutation matrix not closed |
| Idempotent receipt and audit | Version check, replay result snapshot and M16 audit insertion occur in a transaction-owned SQL function; browser mutation carries CSRF and idempotency key | Runtime targeted tests passed; process-kill and post-response-loss matrix not closed |
| Dispatcher | Purpose-bound proof, bounded SKIP LOCKED batch, recipient upper bound, retry/quarantine; `M15:EnableDelivery` absent/false unless explicitly enabled | Real disposable hosted fanout passed; production capacity and crash recovery not accepted |
| Browser identity handling | Disposable session cookies stored under ignored `.verification/.private` and deleted on exit, no change to production login limiter | Fixture and five Chromium browser cases passed on frozen run; next three-engine campaign pending |
| M13 interaction | Independent M13 report member access, KPI source truth and M15 receipt state remain distinct | Combined source `6b05d84` passed both workflows, but its M13 RLS and M15 UI source differ from the newest feature branches |

## Static audit checks

Browser test and fixture JavaScript parsed without syntax errors. Source checks confirmed signed cursor binding to user/session/organization, mutation CSRF and idempotency validation, M15 receipt RLS and current membership checks, transactional acknowledgment audit, unique event-recipient row, signed bounded dispatcher, and default-off service registration. These are source inspection results, not proof of all adverse runtime outcomes.

## Required next steps

1. Source-lock the latest M15 branch and execute the full M15 source, restricted PostgreSQL and Chromium/Firefox/WebKit workflows after both fixes. Reject skipped browser engines and inspect test reports, failures and relevant evidence.
2. If the new pagination race test exposes a genuine problem, correct the implementation without weakening access or rate limits, then rerun at the new SHA.
3. Reconcile the latest M13 report-member RLS optimization and latest M15 UI source on the isolated combined branch; rerun both milestone suites on its final SHA.
4. Finish M15-P01..P14 policy approval, unresolved G15-S01..S24 security/recovery/cursor/load evidence and independent G15-S25 review before any acceptance decision.

**Preserved controls:** M9 ARRI/DEPA processing hold and 10,000 unresolved-work cap, default-off M10 projector, null ETA, opt-in M11 evaluator, default-off M15 delivery. No merge or deployment.
