# Turkish localization verification after G13 and G15 integration

**Snapshot:** 2026-11-12.
**Base protected main:** `58426c15522ed797623e13738e836aa1d6735aed`.
**Integration branch:** `work/turkish-post-g15-integration-20261112`.
**Localization implementation commit:** `7268507e7495c2148a2673fd548c7a343795f7f4`.
**Verification branch:** `verify/turkish-post-g15-source-20261112`, created only after source-level audit.
**Merge status:** No Turkish merge authorized. Final acceptance pending the source-locked grouped run, reviewed evidence and a separate merge-readiness decision.

## Accepted-source boundary

G13 was conditionally accepted and merged in PR #38. G15 was conditionally accepted and merged in PR #41. These conditional dispositions, including formal reviewer qualification, remain unchanged. No G13/G15 backend, migration, notification dispatcher, reporting service, configuration, protected `main`, or security implementation was modified for Turkish integration.

All 21 localization-related file blobs carried from the Turkish preparation branch were checked against the earlier G13/G15 source and current `main`: each overlapping frontend file in current `main` was identical to the earlier source before applying its Turkish presentation change. The only upstream browser change was the additional G13 hosted content-security-policy test. The combined Turkish KPI browser specification preserves this test verbatim and replaces only the old English date-order assertion with a locale-safe UTC boundary expectation.

## Source-level correctness audit before the hosted run

- Git comparison from current `main`: 22 files changed, restricted to Turkish frontend, localization tests and the required G13 browser date assertion. Zero backend, migration, Compose or workflow changes in the implementation commit.
- Fifteen frontend TSX files: 284 static translation references; no missing catalog keys or hardcoded English accessibility attributes in checked text attributes.
- Catalog: 620 Turkish strings with no English/Turkish interpolation-placeholder mismatches. Twenty-eight translated options retain stable wire values.
- CSP: no inline-style creation or style attributes found in the checked frontend files. The latest G13 hosted CSP browser case, full dashboard violation probe and the no-inline-style Node test remain present.
- Source-backed execution of the translation implementation and test bodies: 21/21 passed. The separate static translation-reference requirement was scanned independently. This is **not** native `npm test` or a completed production build.
- All current fixed G13 report error messages have Turkish messages, including the personal and tenant capacity limits. G15 `unreadCountCapped` retains its API meaning: English `1000+ unread`, Turkish `1.000+ okunmamış`, uncapped `25` without a plus sign.
- The Playwright specifications passed source syntax checks. There are 10 localization, 9 G13 KPI and 11 G15 notification scenarios per engine, totaling **90 required browser executions** across Chromium, Firefox and WebKit.
- Prior Turkish grouped run [42852119407](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42852119407) passed on an older source, and cannot certify the final merged source.

## Grouped source-locked verification contract

The dedicated Turkish workflow triggers **only** on the explicitly named `verify/turkish-post-g15-source-20261112` verification branch and executes:

1. Complete frontend Node tests, TypeScript compilation and Vite production build.
2. Source SHA and browser-spec syntax checks.
3. Disposable PostgreSQL, API and web stack setup independently for Chromium, Firefox and WebKit.
4. Authenticated localization, G13 KPI/CSP and G15 notification suites in each browser, with exact case totals of 10, 9 and 11, and zero skipped, unexpected or flaky cases.
5. Per-engine synthetic session renewal, process limiter reset, disposal of isolated test volumes and credentials, plus source-locked sanitized evidence artifacts.

The M15 dispatcher is enabled only inside the disposable verification fixture. Runtime defaults, production authorization, retention deletion and release boundaries are unchanged.

## Independent G15 post-merge review boundary

The later [G15 post-merge review](G15_S25_POSTMERGE_REVIEW_20261112.md) recorded **NOT PASS** for three browser-specific acceptance witnesses: positive paginated continuation, authorized archived-target navigation, and an explicitly held loading state. Independent G15-S25 reviewer attestation also remains unresolved. G15 stays conditionally integrated and these findings are not resolved by the Turkish compatibility exercise. This workflow tests current notification behavior, but passing it does not automatically close these G15 assertion gaps or provide independent approval.

## Remaining acceptance conditions

A passing campaign must be checked against its exact test source SHA and preserved artifacts. Review the actual three-browser results and build logs, evaluate coverage and residual risks, and obtain a separate Turkish acceptance disposition before merging. A source audit or dispatched run alone does not establish merge readiness.

**Status as committed:** Compatibility implementation and source-level preflight complete. Hosted runtime verification and final acceptance pending.
