# Turkish Interface Integration Acceptance, 2026-11-12

## Decision

**ACCEPTED FOR SOURCE INTEGRATION AND MERGED TO PROTECTED `main`.**

The Turkish frontend localization is integrated after the accepted G15/S25 remediation. The integration is a scoped interface enhancement, not acceptance of G23, G24 or a production release. The change does not reopen accepted G15 technical evidence or alter the existing G13 conditional reviewer status.

- [Integration PR #48](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/48), merge commit `c81e54bfef25a0ff957f27b488ab7bd420551244`.
- Exact parent `main` at integration: `55b96e3b0a1be39faf3a53a15b4e4eb680339ca3`.
- Ported localization implementation source: `b0ef077022382d1b1d0fd80c9fe3e6fee351bbbd`.
- [Successful combined workflow run #43199664246](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43199664246), exact verification SHA `d9c8635e35aa66bdf11f47da0b17501d0d0765e9`, two of two jobs successful.
- Verification SHA and integrated implementation differ only by the disposable workflow `.github/workflows/verify-turkish-g15-s25.yml`. This workflow was deliberately excluded from the main merge. All 22 application/browser-test file blobs match the passing verification source.

## Scope and compatibility

The integration changes exactly 22 `web/` and `tests/browser/` files, with no modification to backend source, notification API contract, PostgreSQL migrations, worker/dispatcher behavior, Compose deployment flags, G13 source, or G15 service controls. All 22 localization file blobs were previously tested, and none overlapped the executable changes made by the later accepted G15 S25 remediation.

The browser interface supports `en` and `tr-TR` with a language selector, explicit preference stored under `harborops.language`, first-visit browser-language fallback and same-origin cross-tab preference synchronization. Localized presentation includes authentication, organization navigation, shipping, reference data, maritime workspace, operational KPI reports and notification interactions. Translation affects labels, formatting and presentation; server-facing enum values, permission checks, shipment facts, report values, notification states and API wire contracts are unchanged.

A previous verification at the older `main` SHA did not establish compatibility with the new G15 S25 source. The new campaign corrects that boundary and includes the three added G15 browser cases, increasing notifications from 11 to 14 per engine.

## Source-locked execution evidence

| Stage | Result |
| --- | --- |
| Complete frontend Node test suite | 41 passed, 0 failed, 0 skipped |
| TypeScript and Vite production build | PASS |
| Chromium: localization, KPI, notifications | 10 + 9 + 14 = 33 passed |
| Firefox: localization, KPI, notifications | 10 + 9 + 14 = 33 passed |
| WebKit: localization, KPI, notifications | 10 + 9 + 14 = 33 passed |
| Total browser executions | **99 passed, zero skipped, unexpected, flaky or retried** |
| Workflow jobs | 2/2 successful |
| Evidence manifests | 14/14 recorded file digests valid, zero unlisted or duplicate entries |

Browser checks exercised disposable PostgreSQL and authenticated API/web stacks for every engine. They covered locale persistence, authenticated language switching, stable operational KPI behavior, and the accepted G15 browser scenarios including pagination, pending loading state and archived-target navigation. The test workflow used only disposable fixture delivery activation.

Evidence artifacts and independent digest inspection:

- `turkish-frontend-43199664246`, artifact 13934457704, SHA-256 of downloaded ZIP `b4df692c31b24322ccd07e97af16bc20f1ff0c57e3ce0195766fa43816a9932a`; four file-manifest digest entries validated.
- `turkish-browser-43199664246`, artifact 13935875606, SHA-256 of downloaded ZIP `1d7e9fbc2a8665db9c7fa5a19fc9bbc36e18f8a7808c60a84eb37a5146f51fbc`; ten file-manifest digest entries validated.
- Both archive `source-sha.txt` files match `d9c8635e35aa66bdf11f47da0b17501d0d0765e9`. All nine browser JSON result documents were inspected directly and independently matched the intended case totals, expected status, actual result status and zero retries. The frontend raw log confirms 41 PASS and the production build result.

These are source-specific test results, not a new G15 S25 gate campaign. The G15 backend was not reverified wholesale because its source, service contracts and migration had already been accepted and were not changed for localization. A complete final-product accessibility/release campaign remains outstanding under M24.

## Preserved operating and acceptance boundaries

1. G13 retains its previously recorded conditional acceptance with its reviewer-separation qualification. No general G13 retest was added.
2. G15 remains accepted with the procedural reviewer-independence exception explicitly disclosed in [the G15 decision](G15_S25_FINAL_ACCEPTANCE_20261112.md). No G15/S25 technical evidence is retroactively modified.
3. M15-P10 outbox capture remains transactionally fail-closed with its availability tradeoff.
4. M15-P12 retention periods remain provisional. Physical deletion of notification, immutable history, audit or held records is not authorized.
5. Notification delivery defaults off. M9 transport restrictions, M10 projector opt-in and M11 evaluator opt-in remain binding.
6. No production deployment, retained-volume migration, retention purge, feature activation or G24 release authorization follows from this interface merge.
7. No existing historical failed run, source-specific plan or original decision is rewritten. The verification-only workflow remains on its separate verification branch and was not introduced into `main`.

## Post-merge source disposition

GitHub confirmed PR #48 merged successfully at `c81e54bfef25a0ff957f27b488ab7bd420551244`. Its executable application delta from the predecessor `main` consists exclusively of the 22 verified localization files. Later documentation-only reconciliation commits do not represent additional executable source verification.

**Result:** Turkish interface source integration ACCEPTED and MERGED; combined 41/41 frontend and 99/99 browser checks PASS. Production release acceptance remains pending.
