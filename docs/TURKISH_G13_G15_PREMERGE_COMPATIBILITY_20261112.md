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
