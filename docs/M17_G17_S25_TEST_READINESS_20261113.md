# G17 Technical Test Readiness and S25 Preconditions

**G17-S25 review checkpoint (2026-11-14):** Local campaign c7 at `085fc666` completed 72 technical witnesses, but independent S25 returned NOT PASS because several passing tests did not fully demonstrate their required behaviors. The source candidate now contains additional retained telemetry witnesses, a dedicated malformed-context export probe, rendered-state browser tests, browser correlation verification, and a stricter non-loopback negative control. **These corrections are not yet source-locked runtime verified. G17 NOT ACCEPTED; acceptance pending.** [Remediation register](M17_G17_S25_REMEDIATION_20261114.md).

**Reconciled:** 2026-11-13
**Branch:** `feature/m17-observability-20261112`
**Scope:** 25 scenarios, 75 separately identified assertions.
**Disposition:** Technical test authoring preflight PASS on exact-source G17 campaigns; full runtime acceptance NOT VERIFIED. G17 NOT ACCEPTED; acceptance pending.
**Authoritative source of mappings:** [G17 coverage register](M17_G17_TEST_COVERAGE_20261113.json).
**Required behavior:** [G17 atomic inventory](M17_G17_ASSERTION_INVENTORY_20261112.json).

## Current technical coverage

| Assessment | Count | Evidence meaning |
| --- | ---: | --- |
| TEST_AUTHORED | 72 | A named technical test and expected behavior are mapped in source; individual runtime acceptance still requires raw passing evidence |
| PARTIAL | 0 | No partial mappings in the latest source coverage register |
| MISSING_TEST | 0 | No missing technical mappings in the latest source coverage register |
| INDEPENDENT_REVIEW | 3 | G17-S25 manual review obligations, not automated pass claims |
| **Total** | **75** | 72 technical plus 3 reviewer-only assertions |

| Scenario | Technical tests authored | Independent reviewer assertions |
| --- | ---: | ---: |
| G17-S01 | 3 | 0 |
| G17-S02 | 3 | 0 |
| G17-S03 | 3 | 0 |
| G17-S04 | 3 | 0 |
| G17-S05 | 3 | 0 |
| G17-S06 | 3 | 0 |
| G17-S07 | 3 | 0 |
| G17-S08 | 3 | 0 |
| G17-S09 | 3 | 0 |
| G17-S10 | 3 | 0 |
| G17-S11 | 3 | 0 |
| G17-S12 | 3 | 0 |
| G17-S13 | 3 | 0 |
| G17-S14 | 3 | 0 |
| G17-S15 | 3 | 0 |
| G17-S16 | 3 | 0 |
| G17-S17 | 3 | 0 |
| G17-S18 | 3 | 0 |
| G17-S19 | 3 | 0 |
| G17-S20 | 3 | 0 |
| G17-S21 | 3 | 0 |
| G17-S22 | 3 | 0 |
| G17-S23 | 3 | 0 |
| G17-S24 | 3 | 0 |
| G17-S25 | 0 | 3 |

All 72 technical assertions have authored witnesses according to the current register. The `scripts/audit-g17-test-coverage.mjs --strict` preflight and no-skip source checks passed on [run #43444477442](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43444477442), [run #43445207627](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43445207627) and [run #43445954783](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43445954783). This confirms the authoring inventory, not the runtime outcomes of all 72 assertions.

## Executed verification versus acceptance

The first two cited campaigns failed after successful preflight. Prior-source [run #43444477442](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43444477442) established 0 compile errors, 319 unit passes, 60 contract passes, 41 frontend passes and a successful frontend build, then stopped on an invalid disposable PostgreSQL host port. [Run #43445207627](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43445207627) repeated those stages and reached Prometheus rules, where a backlog recovery fixture expectation failed. Both retained FAILED evidence.

[#43470770246](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43470770246) FAILED after 53/53 scoped integration tests passed with zero skips. The next network verification could not find a running `otelcol` container, even though its expected private network attachments were recorded. It did not preserve the exit reason. A staged patch passed 21 static audit checks, three local negative tests, a collector YAML/pipeline check, and both positive and collector-crash mocked network probes before the branch advanced. Source `7f439b95f09467150027fb8635e15e1abb743ac0` adds real collector configuration validation and stronger runtime-state evidence. [#43477790029](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43477790029) is executing against that exact SHA after the no-skip preflight passed. Full G17 runtime evidence and three independent S25 assertions remain outstanding.

See the [verification recovery register](M17_G17_VERIFICATION_RECOVERY_20261113.md) for failures, corrected source revisions and next actions.

## Mandatory evidence rules

1. Match each technical assertion to exact candidate SHA, method or case identity, expected and actual behavior, timestamps and retained raw artifact. Passing an enclosing job alone is insufficient.
2. Enforce zero skipped, unreviewed, unexpected-pass, expected-failure, retry-only or flaky cases in required evidence. Preserve failed runs and their raw artifacts.
3. Inspect real restricted PostgreSQL authorization, application regressions, workers, collector faults, metrics, spans, log privacy, Prometheus alerts, three browser engines, resource bounds, recovery and retention against the full [G17 verification plan](M17_VERIFICATION_PLAN.md).
4. Independently reconcile all technical findings before G17-S25. The three G17-S25 assertions require separate source and raw-evidence review, disclosure of any policy or procedural exceptions and an explicit PASS/NOT PASS recommendation.
5. Do not mark G17 accepted, merged, deployed, or release-ready until a separate recorded decision. Protected `main`, the M9 transport hold, historical failure evidence and retained data stay unchanged.

The approval to run G17 verification is limited to the isolated, disposable feature-branch campaign. It does not authorize production monitoring, data deletion, other branch activity, or a release.

## Update 2026-11-14: executed technical verification

All 72 technical assertions were executed and individually witnessed by raw evidence in local campaign c7 at `085fc666448962ec4d133bd01bf3992f7d360191` (40 of 40 stages, ledger 72 of 72, manifest verified). The per-assertion table, evidence hashes, qualifications (owner-confirmed P09 evaluation rule, small privacy trace sample, environment notes) and the step-by-step procedure for the three independent G17-S25 assertions are in the [local verification result](M17_G17_LOCAL_VERIFICATION_RESULT_20261114.md). The independent review has not occurred and G17 is not accepted.
