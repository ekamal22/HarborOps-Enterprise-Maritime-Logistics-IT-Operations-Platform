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
