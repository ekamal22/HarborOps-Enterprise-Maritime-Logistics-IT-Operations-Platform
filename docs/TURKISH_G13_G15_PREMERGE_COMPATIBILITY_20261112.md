# Turkish Localization Compatibility With Current G13/G15

Date: 2026-11-12

## Integration boundary

The compatibility source is `ffde1fabb3f184ce3c7bf3a378abd979ffee3791` on `feature/g13-g15-acceptance-closure-20261110`. That branch and the G13/G15 evidence branch are read-only from this work. The protected `main` has not been modified.

This is not a final Turkish merge candidate. G13/G15 must first pass its acceptance gate and merge separately to `main`. Turkish localization must then be reconciled with that exact updated `main`, independently verified and accepted before any Turkish merge.

## Current compatibility changes

- The G13/G15 API, PostgreSQL migrations, quotas, cleanup policy, notification event records, audit and integration tests are preserved from the pinned source.
- The G13 CSP-safe vector workflow distribution bars replace Turkish dashboard inline styles.
- G15 `unreadCountCapped` is retained in the notification client response contract and in the Turkish notification UI. A capped 1000 count remains `1000+ unread` in English and is localized to `1.000+ okunmamış` in Turkish. An exact 25 remains uncapped.
- The G13 `report_user_capacity_exceeded` message receives Turkish presentation without changing the server code.
- Latest G13/G15 browser tests, including KPI CSP checks and G15 capped/un-capped counts, are retained. The KPI date expectation is locale-safe and new Turkish browser coverage exercises the capped and exact unread count across language changes.
- The latest G13 browser test `web/tests/no-inline-style.test.mjs` is retained. The new Turkish components introduce no `style={{...}}` props.

## Evidence and limits

Earlier Turkish grouped run [42852119407](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42852119407) passed 39 frontend cases and 81 browser executions, but it was source-locked to `77a6eea46fafd07a494e9fdbbe6fdedeed738102`, not this candidate. No green result is claimed for this new source until the full tests execute. The source audit is preliminary and must be reconciled with later G13/G15 changes before final integration.

## Final-stage checks after G13/G15 integration

- Run the full `web` frontend test suite and TypeScript/Vite build against the updated `main`.
- Run the complete authenticated Chromium/Firefox/WebKit suites for localization, G13 KPIs, G15 notifications, CSP, session isolation, role and tenant scoping, responsive layouts and accessibility.
- Reconcile all localization messages and dynamic source errors with the accepted API and ensure zero missing keys, skips or unexpected failures.
- Perform independent Turkish acceptance review before requesting a separate merge.

**Status:** Pre-merge compatibility candidate. Acceptance pending. No merge, deployment, or Actions dispatch.

## Source-level correctness audit, 2026-11-12

**Audited implementation:** `47138f7ba5b2cfac44da72ed779644f87cc51356`, derived from G13/G15 `ffde1fabb3f184ce3c7bf3a378abd979ffee3791`.

- The repository comparison confirms this Turkish branch is one commit ahead of the pinned G13/G15 baseline with no backend, migration, database, Compose, security, or workflow modifications. The new G13/G15 `web/tests/no-inline-style.test.mjs` and notification-count browser tests remain intact.
- The Turkish catalog contains 620 messages. Fifteen frontend TSX files contain 284 static translation references and 28 translated options with stable wire values. There are no missing static translation keys, placeholder mismatches, raw English accessibility attributes, or inline-style creation patterns in those files.
- Twenty-one existing and new localization test bodies passed in a source-backed evaluation of the current i18n implementation. The repository-wide static translation-reference test was checked separately. This source-backed assessment is not a substitute for native `npm test`.
- All 27 current fixed G13 reporting endpoint error messages have Turkish presentation; the new per-user capacity response is localized without changing its status or code.
- The unmodified latest G13/G15 browser specifications retain strict CSP checks and the capped-count test. Together with the augmented Turkish localization specification, there are 10 localization, 8 operational KPI and 11 notification scenarios per browser. All three specifications passed syntax checks, giving 87 expected browser executions across Chromium, Firefox and WebKit.
- The English capped count remains `1000+ unread`; Turkish displays `1.000+ okunmamış`, and a non-capped 25 retains no plus sign. This was checked at the translation and component source-contract level, not yet through a live browser.
- The previously passing 2026-11-10 Turkish campaign is historical evidence for an older source only. This branch has not run the full frontend build, disposable-stack integration campaign, responsive checks, or assistive-technology verification.

**Pre-merge technical state:** Code compatibility reconciled and source audit passed; full source-locked runtime verification remains pending. Latest recorded G13/G15 acceptance is still pending, and no Turkish merge is authorized.
