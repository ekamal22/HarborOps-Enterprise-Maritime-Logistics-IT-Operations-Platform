# M12 Fourth-Pass Correctness Audit

**Date:** 2026-11-08  
**Scope:** Draft PR #32 on `feat/m12-shipment-search-export` against accepted `main` at `6ead1d076946dd96b53401265a31efc35c4a666c`.  
**Outcome:** Source-level defects remediated; build, PostgreSQL, browser, load and security execution remain **NOT EXECUTED**. G12 policy decisions, independent evidence review and acceptance pending.

## Audit baseline and change boundaries

Read the current M12 domain search/filter/CSV policy, authorization mapping, EF query and transaction service, protected cursor codec and API endpoints, migration and audit verifier, API role grants, authentication/rate controls, proxy configurations, React search/export client, relevant M12 unit/contract/HTTP and M5 browser tests, and the preceding three source audits. Confirmed that M12's `20261107230000_M12ShipmentExportAudit` and M11's `20261107180000_ShipmentExceptions` have different IDs but need an independent combined migration/snapshot/role/UI/test reconciliation. The accepted M5 list API is preserved, M9 ARRI/DEPA transport hold continues, and the M10 projector remains default-off.

No local .NET SDK, Docker, PostgreSQL, database volume, or executing browser fixture is available in this source-editing environment. The source checkout could not be fetched into the local execution container. This audit **cannot** certify runtime end-to-end correctness, and authored tests are not accepted as passing test results.

## New findings and source corrections

| ID | Severity | Finding | Source remediation | Evidence status |
| --- | --- | --- | --- | --- |
| M12-D01 | Medium, build compatibility | The export-audit database command used the legacy `ExecuteSqlInterpolatedAsync` API. The project treats warnings as errors, and current EF Core guidance prefers `ExecuteSqlAsync` for parameterized interpolation. | Replaced the single M12 audit command with `ExecuteSqlAsync`, retaining the parameterized `FormattableString`, transaction, cancellation token, and signed audit semantics. | Source corrected. Actual .NET 10 build NOT EXECUTED. |
| M12-D02 | High, export error contract | Both local and hosted Nginx `/api/` proxy locations have a `proxy_read_timeout` of 30 seconds, equal to the prior 30-second export preparation deadline. With no response margin, cleanup or error serialization could reach the proxy timeout first. | Bounded export preparation to 25 seconds, preserving the 15-second per-SQL-statement timeout, linked client cancellation and fail-closed audit transaction. | Source corrected. No proxy/network/timeout simulation performed. |
| M12-D03 | Medium, search resource bounds | Search applied a 15-second timeout to each SQL statement but no total request budget. Multiple authorization and query statements could cumulatively exceed the 30-second API proxy timeout. | Added a 25-second linked total search budget covering transaction, live scope and query. The endpoint maps internal cancellation to sanitized HTTP 503 and leaves actual client aborts untouched. | Source corrected. No real slow-query or proxy run performed. |

The two 25-second limits are deliberate candidate operating defaults, not evidence that the reverse proxy will always return a 503. Proxy load, connection establishment, cancellation/rollback, application queueing and network behavior must be tested with controlled delays. The default proxy configuration is unchanged.

## Rechecked security and data contracts

- **Tenant and user scope:** M12 search/export requests revalidate selected organization, live session, user security version, and current membership. Search is allowed for Analyst, Manager and Auditor; CSV export is restricted to Analyst/Manager. Organization, shipment and voyage-assignment predicates retain tenant restrictions.
- **Cursor integrity:** The Data Protection token binds organization, user, login session, filter fingerprint, UUID continuation, reference time and 15-minute expiry. Later pages are live reads, not a frozen multi-request snapshot. No cursor bypass was identified in source inspection.
- **SQL audit:** The migration-owned HMAC verifier key is read inside a SECURITY DEFINER function. The function validates all proof fields, active session and membership; replaying its correlation is blocked by a unique audit index. The restricted HTTP SQL role is denied SELECT on the verifier table. M16 records `shipment.export_prepared`, not proven delivery.
- **CSV boundaries:** The fixed 12-column UTF-8 view uses formula defanging and escaped quotes, with 5,000-row and 10-MiB caps. A successful audit must commit before the file result is returned. Abort, DB error, missing key or failed audit must not return a partial CSV.
- **Browser and accepted-route compatibility:** The two M5 browser race tests were updated in the third pass to intercept the M12 React `/shipments/search` URL, including a strict delayed-list precondition. M5's old list API remains available for API clients. M12 export uses same-origin browser requests and aborts on workspace teardown.
- **Timeouts and errors:** Search and export now use 15-second SQL statement limits with 25-second total budgets. Internal budget cancellations produce sanitized 503; client cancellation remains distinct. Nginx still uses 30-second read timeouts. Rate limit remains 10/min per principal and one in-flight export per API process.

These are reviewed implementation claims. They are **not** yet confirmed with PostgreSQL role tests, HTTP traffic, race execution, or historical upgrade data.

## Required G12 verification on the frozen final source

1. **Build gate:** .NET 10 Release build with warnings-as-errors, unit/contract suites and frontend production build. Specifically confirm the `ExecuteSqlAsync` replacement compiles with the locked EF Core version.
2. **Database gate:** On exclusive disposable PostgreSQL, migrate the accepted historical baseline to M12, provision restricted roles and a separate 32-byte audit key, check SQL function and table grants, HMAC field mutations, replay, key mismatch and rotation, absent key, audit insert outage, transaction rollback and downgrade guard.
3. **Query and resource gate:** Actual tenant/non-tenant/role matrices; UUID cursors, UTC windows, M6 association boundaries, query plans, 5,000/5,001-row rejection, output caps, 15-second SQL statement timeout, 25-second total search/export budgets, client cancellation and the 30-second proxy envelope. Verify 503 vs proxy failure with measured time, not only static configuration.
4. **Browser gate:** Chromium, Firefox and WebKit against a seeded restricted-runtime M12 fixture, including advanced search, export/download, role controls, stale responses across organization changes and unsaved M5 draft preservation during background refresh.
5. **Integration/regression gate:** Relevant accepted M2/M5/M6/M10/M16 behavior, M9 hold, M11 shared-file/migration discovery risks, security mutation tests, dependency/image scans and safe-copy Q2 historical snapshot. Do not use an existing retained volume for destructive tests.
6. **Evidence and governance:** Source-locked command/result/exit-code capture, redacted raw artifacts and manifest on E:, independent G12-S25 review, explicit M12-P01..P16 disposition and separate G12 acceptance decision. No Actions, merge, deployment or transport-hold release.

## Audit disposition

New source findings M12-D01..D03 are corrected on the feature branch. The prior A/B/C findings and their status remain recorded in [first](M12_CORRECTNESS_AUDIT_20261107.md) and [second/third](M12_CORRECTNESS_AUDIT_20261108.md) historical audit reports. They are not retroactively marked runtime PASS. Current branch is an **unaccepted candidate**; merge and release readiness remain pending.

No GitHub Actions, merge, deployment, production migration, destructive database operation, retained-volume cleanup, or changes to accepted main were performed.
