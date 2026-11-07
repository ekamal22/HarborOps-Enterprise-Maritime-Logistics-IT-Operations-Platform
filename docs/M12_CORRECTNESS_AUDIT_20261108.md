# M12 Second-Pass Source Correctness Audit

**Date:** 2026-11-08  
**Scope:** `feat/m12-shipment-search-export`, draft PR #32, against accepted `main` at `6ead1d076946dd96b53401265a31efc35c4a666c`.  
**Code checkpoint after second-pass remediation, before final documentation reconciliation:** `d3970590f34220ad328db8b6770ffb87c2011d92`.  
**Disposition:** New source defects remediated, regression cases authored, executable G12 verification NOT EXECUTED. Acceptance pending. No merge or deployment.

This is the second source audit. The [first correctness audit](M12_CORRECTNESS_AUDIT_20261107.md) remains the historical record for signed export authority, replay protection, cursor isolation, CSV defenses, and the M11 integration boundary. Earlier code/source review results do not establish executable passing tests at the current revision.

## Second-pass findings and changes

| Finding | Severity | Evidence and remediation | Current disposition |
| --- | --- | --- | --- |
| M12-B01 | High, build reliability | `ShipmentSearchHttpTests.cs` used `IWebHostBuilder.UseSetting` without the explicitly required `Microsoft.AspNetCore.Hosting` import, unlike the accepted fixture. The import is added. | Source fixed; compilation NOT EXECUTED |
| M12-B02 | High, audit lifecycle | The SQL-only M12 migration's `Down` path would remove the signing authority and anti-replay index even when immutable `shipment.export_prepared` history existed. An explicit SQL guard now rejects downgrade with SQLSTATE 23514 if any export audit history remains. No historical entries are deleted. | Source fixed; real downgrade rehearsal NOT EXECUTED |
| M12-B03 | Medium, bounded inputs | The parser capped query key count but not total URL-encoded query length. It now rejects more than 4,096 encoded characters before reading individual parameters. A negative HTTP fixture is authored. | Source fixed; HTTP test NOT EXECUTED |
| M12-B04 | High, resource safety | Database search and CSV export previously relied on provider/default timeouts. Each M12 read transaction now uses PostgreSQL `SET LOCAL statement_timeout = '15s'`. Export also enforces a 30-second linked cancellation budget, returning a sanitized 503 for an internal deadline rather than a partial CSV. The transaction must roll back on error. | Source fixed; query timeout/outage evidence NOT EXECUTED |
| M12-B05 | High, role coverage | The restricted-role HTTP fixture tested Manager, Analyst and Administrator, but not Auditor and Support end to end. The suite now provisions both additional roles in the exclusive disposable fixture and asserts Auditor search-only, Support read/export denial, and Administrator export denial. | Test source added; five-role runtime matrix NOT EXECUTED |
| M12-B06 | High, proof forgery tests | The earlier SQL restricted-role test mutated the signed actor only. It now independently mutates all nine bound proof inputs: organization, actor, session, row count, byte count, filter digest, correlation, expiry and signature. Each must be rejected before the unchanged proof succeeds once and replay is denied. | Test source added; real restricted SQL run NOT EXECUTED |
| M12-B07 | Low, frontend type safety | The advanced-filter text field key type included `workflowStatus`, which is separately restricted to a finite typed list. The text-field key is now explicitly `Exclude<keyof ShipmentAdvancedFilters, "workflowStatus">`. | Source fixed; frontend typecheck NOT EXECUTED |
| M12-B08 | Medium, migration guard testability | The new downgrade safeguard was not independently asserted by a lightweight test. `M12MigrationSafetyTests` now inspect EF migration operations for the audit-history guard before destructive statements and for verifier function/table restrictions, without changing any database. | Unit tests authored; NOT EXECUTED |

## Third-pass M5 browser-regression findings, 2026-11-08

This additional source review compared the M12 React shipment search URL with the accepted M5 Playwright specifications. All 19 browser spec files were screened for shipment endpoint interception. Two accepted regression checks still targeted only the M5 list URI and did not exercise the M12 workspace's actual `/shipments/search` read path.

| ID | Severity | Confirmed mismatch and correction | Verification |
| --- | --- | --- | --- |
| M12-C01 | Medium, organization-switch regression | In `tests/browser/specs/shipment.spec.mjs`, the deferred-response interceptor was still matching `/shipments?`, so it could not hold the new M12 search request. Updated the interceptor and path predicate to `/shipments/search?`. The original foreign-tenant response isolation assertion is retained. | Source corrected; Playwright not executed |
| M12-C02 | Medium, unsaved draft regression | In `tests/browser/specs/shipment-draft.spec.mjs`, the in-flight background-refresh test only matched M5 list/detail requests, not `/shipments/search`. Updated its predicate to cover both the new search route and existing detail reads. The test's interception signal now resolves only after an actual advanced-search list request is held, so an unrelated shipment-detail read cannot satisfy the race precondition. The editable-draft assertions are retained. | Source corrected; Playwright not executed |

These source fixes restore the intended scope of two accepted M5 regression tests on the M12 branch. They are not evidence that the tests pass. G12-S22 must run them across Chromium, Firefox and WebKit with actual M5/M12 fixtures. No GitHub Actions, merge, release or deployment is authorized by this audit.

## Architecture and accepted contract review

The M12 search route stays separate from accepted M5 list/detail/mutation routes. The baseline M5 search uses normalized literal substring matching over reference code, external reference and display name, workflow/record-status filters, and UUID-ascending keyset ordering. M12 retains those same methods and adds M6 half-open voyage intervals, route and UTC date constraints, a tenant/user/session/filter-bound protected cursor and a bounded page size. No accepted M5 source is modified.

CSV export uses a distinct `ExportShipmentData` permission, allows Analyst/Manager only under current pending M12 local policy, and denies Auditor, Administrator and Support. It operates from a repeatable-read tenant scope and emits a fixed CSV v1 allowlist with formula escaping. The independent HMAC proof is issued only in the HTTP runtime and verified by a privileged-migrator-provisioned database secret. The proof binds export scope, actor, session, row/byte counts, digest, correlation and short expiry. A unique index denies replay. It authorizes `shipment.export_prepared`, which does not mean that the HTTP transfer reached the browser.

The M12 signing key comes from `HARBOROPS_M12_EXPORT_PROOF_KEY` in the untracked local secret configuration; only API and migrator receive it. With no key, export returns a sanitized 503, and malformed configured key material blocks provisioning. Existing M10 key material remains independent. The M9 transport backlog hold, default-off M10 projector, unresolved-work cap and null ETA are unchanged.

## Independent M11 boundary

M11 draft PR #31 adds the separate `20261107180000_ShipmentExceptions` migration; M12 adds `20261107230000_M12ShipmentExportAudit`. The two IDs are not duplicates, but their respective branch migration counts are both fourteen and the combined count would be fifteen. M11 and M12 also edit role provisioning, API startup, integration fixtures, migration snapshot tests, shipment workspace and documentation. Their combined, conflict-resolved source must be reverified on one new SHA. G11 is not a prerequisite for isolated M12 development.

## End-of-pass source reconciliation

At the final pre-report source checkpoint `3ba2846ce3f5b8629e3c5a134740518d688dd92a`, a targeted cross-file source scan checked the export signer/function version, nine HMAC binding fields, key grants and Compose secrets, anti-replay index, search query bounds, 15-second statement and 30-second export budgets, sanitized cancellation, rollback guard ordering, dedicated migration-operation tests, all five HTTP role cases, protected cursor, same-origin guard and typed filter keys. Nineteen automated text assertions matched, and the twentieth five-role case was separately confirmed after correcting the checker to use the actual lowercase local variable names. This is **20 source-consistent checks**, not 20 executed unit or integration tests.

A relative Markdown link scan of 22 relevant M12 and living documents found **zero missing linked repository file paths**. A review of the 35 commits added in this pass found every commit message included `[skip ci]` and no attribution trailer. All changes remain isolated to draft PR #32. These checks do not validate EF translation, PostgreSQL privileges, real SQL cancellation, migration rollback behavior, browser execution or deployment readiness.

## Verification and evidence classification

**Performed:** Connected repository source inspection, diff and call-site review; earlier 18 cross-file source-consistency checks from the [first audit](M12_CORRECTNESS_AUDIT_20261107.md); authored and committed source corrections and regression cases. Reviewed Vite/Nginx routing: the browser calls same-origin relative /api paths and the local/hosted proxies preserve the public Host, so M12's same-origin download check remains compatible. No hosted workflows were enabled.

**Not performed:** .NET release compile, integration or contract test execution, real PostgreSQL SQL/role/downgrade tests, web production bundle, live three-engine Playwright checks, security mutations, image scanning, M12 query-plan measurements, 5,000/5,001-row export checks, historical Q2 snapshot compatibility or independent G12-S25 raw evidence review. The connected repository environment has no checked-out .NET/PostgreSQL/Docker stack and direct GitHub network checkout is unavailable from the local execution container. These are **NOT EXECUTED**, not PASS.

G12-S01 policy acceptance, all G12 executable scenarios and G12-S25 independent review remain pending. The G12 outcome cannot be certified from source text or authored tests. The next technical campaign must use a final frozen SHA, an exclusive E:-backed disposable PostgreSQL instance for tests that reprovision shared SQL roles, bounded browser fixtures and an evidence manifest containing source hash, exact commands, raw results and every failed attempt. Do not run GitHub Actions, alter retained data, merge, or deploy without separate permission.
