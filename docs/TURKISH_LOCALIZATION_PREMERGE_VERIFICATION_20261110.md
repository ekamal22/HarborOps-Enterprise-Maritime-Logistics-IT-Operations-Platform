# Turkish Localization Pre-Merge Compatibility and Verification

Date: 2026-11-10. This is a preliminary compatibility and verification record, not an approval.

## Integration order and boundaries

1. G13/G15 completes its independent acceptance campaign, evidence reconciliation and S25 reviews.
2. G13/G15 integrates alone into protected `main` only after unconditional approval.
3. Turkish localization is reconciled against the updated `main` in a distinct Turkish-only branch.
4. Turkish localization passes independent translation, functional, contract, browser, responsive and accessibility checks on that exact source.
5. Turkish localization merges separately following acceptance.

Do not write to `main` or any G13/G15 implementation, review, or verification branch. No Turkish work is part of the G13/G15 merge.

## Baselines and latest evidence before this remediation

- Existing Turkish feature source: `38e77d94676b2f100076074dc79243ef549a0c83`.
- Existing Turkish/G13-G15 composite source: `b77e347a6a1542e88fda22c553fd6d764ff97ec9`.
- G13/G15 accepted-source status: pending; no claimed accepted or integrated SHA.
- Run 42743803144 failed an authenticated organization-selector timing assertion; 38 frontend tests and production build passed.
- Run 42744445558 failed a KPI English date-literal assertion after 7/7 Chromium localization and 2/3 Chromium KPI cases passed; frontend tests and build passed; Firefox, WebKit and notification suites were not reached.
- The KPI formatter currently renders `Nov 01, 2026` at the user-facing English locale, whereas the browser expectation hardcodes `01 Nov 2026`. Fix the assertion to verify the exact UTC period without assuming an English month/day text order.

## Required execution sequence

Documentation update, assertion remediation, read-only cross-branch G13/G15 compatibility audit and any Turkish-only corrections, complete preflight correctness audit, then a single grouped verification dispatch at one frozen source SHA. Recording a plan or a passing partial job is not equivalent to full acceptance.

## Expected grouped evidence

Complete `web` Node test suite and TypeScript/Vite build; authenticated disposable full-stack Chromium/Firefox/WebKit suites for localization, G13 KPIs and G15 notifications; document `lang`, persistence, cross-tab behavior, Turkish percentages and state labels; 375/390/768/1440 px checks, focus and accessible-name coverage; role/organization boundaries; source-locked suite counts and no silent skips. Preserve artifact summaries without saving session cookies or secrets.

## Acceptance

Pending. The separate G13/G15 acceptance and merge must happen first. The pre-merge compatibility verification here cannot authorize localization integration into `main`.

## Current G13/G15 read-only compatibility baseline

The candidate derives from `feature/g13-g15-acceptance-closure-20261110` at `d6e02bece67ab9ab898f051a645df8e3c9d5f4f7` and keeps that branch unchanged. The current G13 implementation adds tenant-wide report admission controls, an idle KPI cleanup service, a capacity-exceeded HTTP 429 response and expanded browser coverage. Turkish localization adds user-facing translations for the fixed KPI response strings while retaining API error codes. The newest G13 browser tests are retained, including eight KPI cases and ten G15 notification cases. This preliminary candidate must be reviewed and tested independently; it is not the accepted G13/G15 merge or the final Turkish integration source.

## Added G13 localization coverage before audit closure

Two authenticated browser checks verify the newest HTTP 429 report-tenant capacity response is understandable in Turkish and that G13 ratio values render with Turkish percentage placement and decimal comma. The complete localization browser suite now requires 9 passing cases per engine. The KPI suite requires 8 and notifications require 10 per engine. These are source requirements, not executed evidence until the grouped run finishes.

## Source-locked run 42840754160 and browser isolation remediation

The 2026-11-10 grouped run [42840754160](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42840754160) failed on the Turkish pre-merge compatibility candidate `f41a63b7edc627022412346ae2aac9d0f4393f7d`. The frontend passed 39/39 tests and the production build. Chromium and Firefox each passed all 27 browser cases. WebKit passed all 9 localization cases and 7/8 KPI cases; notification tests were not reached. The real KPI report admission assertion received HTTP 429 rather than HTTP 200 in WebKit.

The G13 tenant-wide quota function permits up to five concurrent unexpired report snapshots per organization. Previously, all three engines ran against the same disposable PostgreSQL instance; real report requests used different authenticated sessions, so prior engine snapshots remained until expiry. This creates a credible inter-engine quota collision. The HTTP 429 response code itself does not establish which reporting quota code was returned, since the browser assertion recorded only the status. The remedy isolates each engine in a separately provisioned, disposable Compose project and database, preserves the G13 admission policy unchanged and runs all three required suites in each engine. Browser installations occur once, and credentials plus container volumes are destroyed after each engine. It remains essential that every real report is admitted within its isolated engine; no quota exceptions are suppressed.

The next combined campaign must execute 39+ frontend tests plus the build and all 81 browser executions with zero skips or unexpected failures. This remains a pre-merge compatibility exercise, not Turkish acceptance on updated `main`.
