# G17 Technical Test Readiness and S25 Preconditions

**Recorded:** 2026-11-13
**Branch:** `feature/m17-observability-20261112`
**Coverage review baseline:** `58e63c140073febfe3e7ea59ed863ada4d7f01da`
**Disposition:** IN PROGRESS. The technical test authoring phase is incomplete. G17 NOT ACCEPTED.
**Source of assertion identities:** [G17 atomic inventory](M17_G17_ASSERTION_INVENTORY_20261112.json).
**Source of coverage classifications:** [G17 test coverage register](M17_G17_TEST_COVERAGE_20261113.json).

## Precise coverage checkpoint

The mandatory inventory contains 25 scenarios with three separately identified assertions each. Three G17-S25 assertions require an independent reviewer after technical execution. The other 72 technical assertions cannot be skipped or waived by passing another scenario.

| Technical state | Count | Meaning |
| --- | ---: | --- |
| TEST_AUTHORED | 26 | A named technical witness is present in source. Not yet executed or independently accepted. |
| PARTIAL | 32 | Some behavior is covered; at least one necessary outcome or negative control is still absent. |
| MISSING_TEST | 14 | No complete named behavioral witness is present. |
| INDEPENDENT_REVIEW | 3 | Reserved for G17-S25 after raw evidence is collected. Not an automated pass. |
| **Total** | **75** | **No G17 scenario or assertion has executed, source-locked acceptance evidence.** |

**Technical readiness:** 26/72 authored, 46/72 incomplete.
**Executed G17 acceptance:** 0/25 scenarios, 0/75 assertions.
**GitHub Actions:** No G17 verification dispatch authorized while technical preflight is incomplete. The permission to dispatch applies only after all required scenarios have executable, no-skip witnesses and the preflight is satisfied.

## Scenario-by-scenario authoring state

The four columns indicate authored, partial, missing, and independent-review assertions, respectively. They are not run results.

| Scenario | Authored | Partial | Missing | Independent |
| --- | ---: | ---: | ---: | ---: |
| G17-S01 | 1 | 2 | 0 | 0 |
| G17-S02 | 3 | 0 | 0 | 0 |
| G17-S03 | 3 | 0 | 0 | 0 |
| G17-S04 | 3 | 0 | 0 | 0 |
| G17-S05 | 2 | 1 | 0 | 0 |
| G17-S06 | 2 | 1 | 0 | 0 |
| G17-S07 | 1 | 2 | 0 | 0 |
| G17-S08 | 1 | 2 | 0 | 0 |
| G17-S09 | 1 | 1 | 1 | 0 |
| G17-S10 | 1 | 2 | 0 | 0 |
| G17-S11 | 0 | 3 | 0 | 0 |
| G17-S12 | 0 | 2 | 1 | 0 |
| G17-S13 | 0 | 1 | 2 | 0 |
| G17-S14 | 2 | 1 | 0 | 0 |
| G17-S15 | 0 | 0 | 3 | 0 |
| G17-S16 | 2 | 1 | 0 | 0 |
| G17-S17 | 0 | 3 | 0 | 0 |
| G17-S18 | 1 | 2 | 0 | 0 |
| G17-S19 | 0 | 1 | 2 | 0 |
| G17-S20 | 1 | 1 | 1 | 0 |
| G17-S21 | 1 | 2 | 0 | 0 |
| G17-S22 | 0 | 0 | 3 | 0 |
| G17-S23 | 0 | 3 | 0 | 0 |
| G17-S24 | 1 | 1 | 1 | 0 |
| G17-S25 | 0 | 0 | 0 | 3 |

## Outstanding test obligations

**Missing-test assertion IDs (14):** `G17-S09-A01`, `G17-S12-A03`, `G17-S13-A02`, `G17-S13-A03`, `G17-S15-A01`, `G17-S15-A02`, `G17-S15-A03`, `G17-S19-A02`, `G17-S19-A03`, `G17-S20-A03`, `G17-S22-A01`, `G17-S22-A02`, `G17-S22-A03`, `G17-S24-A01`.

**Partially covered assertion IDs (32):** `G17-S01-A01`, `G17-S01-A02`, `G17-S05-A02`, `G17-S06-A03`, `G17-S07-A02`, `G17-S07-A03`, `G17-S08-A02`, `G17-S08-A03`, `G17-S09-A02`, `G17-S10-A02`, `G17-S10-A03`, `G17-S11-A01`, `G17-S11-A02`, `G17-S11-A03`, `G17-S12-A01`, `G17-S12-A02`, `G17-S13-A01`, `G17-S14-A02`, `G17-S16-A03`, `G17-S17-A01`, `G17-S17-A02`, `G17-S17-A03`, `G17-S18-A01`, `G17-S18-A03`, `G17-S19-A01`, `G17-S20-A02`, `G17-S21-A01`, `G17-S21-A03`, `G17-S23-A01`, `G17-S23-A02`, `G17-S23-A03`, `G17-S24-A03`.

Each of these IDs has a specific expected behavior and documented gap in the coverage register. Reuse existing integration and regression tests only when they directly prove every required outcome. A source-text match, test name, wrapper job exit code or test file's existence does not prove the behavior.

Priority remaining work includes realistic database and OTLP failure behavior; worker retry/lease and restricted reader proofs; raw trace/log privacy, audit isolation and tenant/authorization checks; process restart and cancellation; exporter queue overflow/recovery; low-traffic dashboard and alert states; active, expired and full-store retention; longer-running memory bounds; representative matched performance workloads; and a 75-assertion evidence reconciliation.

The targeted local runner is `scripts/run-g17-local.ps1`. It now invokes `scripts/audit-g17-test-coverage.mjs --strict` before restore or starting a disposable stack. This preflight must exit unsuccessfully while any technical assertion is `PARTIAL` or `MISSING_TEST`. Unit/contract, restricted PostgreSQL, worker, three-browser, alert, network, performance and evidence checks are subject to their own source-locked execution after authoring is complete.

## Independent G17-S25 review boundary

1. All 72 technical assertions must map to individually inspectable, executable witnesses with matching expected outcomes and negative controls. No scenario may be left without an explicit witness.
2. Verify actual compiled and executed results on the exact candidate SHA, including raw TRX method/case totals, Playwright results for all required engines, Prometheus transitions, OTLP privacy output, worker and PostgreSQL faults, and measured performance/retention.
3. Enforce zero skipped, silently ignored, unexpectedly passing, expected-failure, flaky or unreviewed cases. Preserve all failures and historical evidence without deletion.
4. Attribute each of the 72 technical results to the exact assertion ID and retained artifact. A suite-level green status cannot fill unmapped assertion rows.
5. After the technical results are complete, perform a separate independent S25 source and evidence review. Record any policy or procedural exception explicitly, then issue a separate PASS/NOT PASS recommendation before any G17 acceptance or merge consideration.

## Change and operational restrictions

Protected `main`, accepted G13/G15 source, M9 transport hold, historical evidence, and the default Compose stack are unchanged by this documentation. No GitHub Actions run, M17 deployment, production collector, retained-data cleanup or acceptance decision is authorized at this checkpoint. The draft [M17 PR #52](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/52) remains under verification.
