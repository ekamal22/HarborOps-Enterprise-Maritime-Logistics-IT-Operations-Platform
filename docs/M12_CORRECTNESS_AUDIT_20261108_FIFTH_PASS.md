# M12 Fifth-Pass Source Correctness Audit

**Date:** 2026-11-08  
**Branch:** `feat/m12-shipment-search-export`, draft PR #32  
**Accepted comparison:** `main` at `6ead1d076946dd96b53401265a31efc35c4a666c`  
**Disposition:** Two further source defects remediated. No new G12 scenario is claimed PASS. M12 policy and acceptance pending.

## Reviewed surfaces

Rechecked the current M12 search and CSV store, tenant/role authorization, active session and membership locking, signed export-preparation SQL and immutable audit migration, restricted API and worker grants, JWT-independent Data Protection cursor, M6 current voyage assignment, UTF-8 CSV field escaping, timeout and cancellation paths, browser shipment workspace and accepted M5 route regressions, G12 integration fixtures, reverse-proxy configuration, and the distinct unaccepted M11 migration.

Confirmed the following earlier source contracts remain unchanged: Analyst/Manager are the only CSV export roles; Auditor can search but cannot export; Administrator/Support cannot search or export; selected-organization and signed audit scope are checked in the database; a replayed audit correlation is denied; the API response is CSV only after the required export-preparation event has committed; the original M5 list/detail/mutation routes still exist; and sustained M9 transport processing remains held.

The EF Core 10 `ExecuteSqlAsync(DatabaseFacade, FormattableString, CancellationToken)` and migration `UpOperations`/`DownOperations` APIs were checked against official Microsoft documentation. xUnit v2's collection-level `DisableParallelization` and custom `FactAttribute.Skip` mechanisms were also checked against xUnit documentation. These checks confirm API availability, not that the project compiles.

## Fifth-pass findings and corrections

| ID | Severity | Source finding | Correction | Verification status |
| --- | --- | --- | --- | --- |
| M12-E01 | High, test isolation and safety | `RestrictedRuntimeExportIsScopedAndAudited` reprovisions shared PostgreSQL login roles `harborops_api` and `harborops_worker`. An ordinary integration test run could do so against any permitted local integration database, with effects on other databases in that server instance. Merely documenting an exclusive run did not prevent it. | The restricted-role case now requires `HARBOROPS_G12_EXCLUSIVE_POSTGRES=1` before any connection or provisioning. A custom xUnit v2 Fact attribute reports it as **SKIPPED**, not PASS, when the marker is absent, and a method-level preflight checks again at execution. An xUnit collection with `DisableParallelization=true` prevents concurrent tests in the same assembly. The test database guard still restricts the target to local `harborops_integration` or `harborops_test`. | Source fixed. Isolated PostgreSQL role test NOT EXECUTED; external server isolation remains an operator obligation. |
| M12-E02 | Medium, deadline observability | CSV serialization and UTF-8 conversion were synchronous and had no cancellation checks. The 25-second linked deadline was observed during database requests but could pass unnoticed during row assembly under CPU starvation. | Each CSV row now checks the linked operation token. Cancellation is also checked immediately before and after UTF-8 encoding, prior to signed audit and transaction commit. Client abort and internal 25-second deadline continue to use the existing endpoint error distinction. | Source fixed. Timed CPU stress, client cancellation, database rollback and proxy checks NOT EXECUTED. |

## Test execution contract and explicit skip

The exclusive role test is deliberately **not** part of an unmarked general integration run. Its xUnit result must be treated as **SKIPPED/NOT EXECUTED** when `HARBOROPS_G12_EXCLUSIVE_POSTGRES` is absent. A complete G12 campaign must provide an exclusive disposable PostgreSQL server, set that variable to `1`, and capture a real execution outcome for the restricted-role scenario. The marker is an explicit operator assertion, not an automated proof of exclusivity. Neither the collection setting nor the variable protects against other test assemblies, processes or external connections. Do not run this scenario on a PostgreSQL server shared with any retained HarborOps installation.

On Windows, keep the dedicated source checkout, Docker data, key ring, test database volume and artifacts on E:. Do not touch production or the only retained local volumes.

## Remaining G12 evidence requirements

- Source-frozen .NET 10 Release build and test compilation with warnings as errors, web production typecheck/build, migrations and EF snapshot assertions.
- Exclusive real PostgreSQL role/grant/HMAC proof, replay, forged proof, missing/rotated key, rollback guard, audit outage and transaction consistency tests, including the explicitly gated case above.
- Search and export query/row/byte caps, live tenant isolation and membership/session revocation, UUID cursor binding, UTC date/voyage filters, and real timeout/cancellation/partial-output probes through the 30-second Nginx read timeout.
- Three-engine Playwright coverage for M12 search/export and accepted M5 delayed list responses and unsaved-draft preservation, with the correct `/shipments/search` interceptors.
- Relevant G5/G6/G10/G16 regressions and M9 hold, combined M11/M12 migration and DI/UI/test reconciliation on a separate source, security mutations/scans, safe historical Q2 upgrade/retention checks, source-locked raw evidence with manifest and independent G12-S25 review.
- Explicit M12-P01..P16 policy disposition and G12 acceptance after evidence review. No merge, deployment, GitHub Actions or transport-hold release is authorized.

## Environment and audit limitations

The connected repository allows source inspection and commits, but the current execution container has no .NET SDK, PostgreSQL client/server, Docker or checkout of the full repository. Direct GitHub clone is unavailable through its network. Source reviews, official API documentation and newly authored regression checks are not executable test results.

No hosted workflow, migration, SQL fixture, release build, Playwright browser, security mutation, image scan or independent G12 review was run in this pass. The previous [first](M12_CORRECTNESS_AUDIT_20261107.md), [second/third](M12_CORRECTNESS_AUDIT_20261108.md), and [fourth](M12_CORRECTNESS_AUDIT_20261108_FOURTH_PASS.md) audit records remain historical. Current M12 implementation remains an **unverified draft candidate**; acceptance pending.
