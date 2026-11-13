# G17 Technical Test Readiness and S25 Preconditions

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

[#43452458500](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43452458500) failed its older broad integration runner (446 passed, 49 failed, 83 skipped), and [#43452683183](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43452683183) failed its scoped G17 integration stage (50 passed, 3 failed, 0 skipped). The three remaining failures concerned two missing baseline seeds and a manual redrive generation assertion; see the recovery register for actual TRX witness identity. The corrections were committed as `fe81a763f5a4e7a8bfc6394aceea34ae1f927e60` and `b1741377b8a1f9af740a131c231a689ab6a1f6eb`. [#43462771279](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43462771279) is the latest corrected source-locked campaign on exact SHA `b1741377b8a1f9af740a131c231a689ab6a1f6eb`, queued when recorded. Full 72/72 technical evidence is not yet available, and **G17 remains 0/75 independently accepted**.

See the [verification recovery register](M17_G17_VERIFICATION_RECOVERY_20261113.md) for failures, corrected source revisions and next actions.

## Mandatory evidence rules

1. Match each technical assertion to exact candidate SHA, method or case identity, expected and actual behavior, timestamps and retained raw artifact. Passing an enclosing job alone is insufficient.
2. Enforce zero skipped, unreviewed, unexpected-pass, expected-failure, retry-only or flaky cases in required evidence. Preserve failed runs and their raw artifacts.
3. Inspect real restricted PostgreSQL authorization, application regressions, workers, collector faults, metrics, spans, log privacy, Prometheus alerts, three browser engines, resource bounds, recovery and retention against the full [G17 verification plan](M17_VERIFICATION_PLAN.md).
4. Independently reconcile all technical findings before G17-S25. The three G17-S25 assertions require separate source and raw-evidence review, disclosure of any policy or procedural exceptions and an explicit PASS/NOT PASS recommendation.
5. Do not mark G17 accepted, merged, deployed, or release-ready until a separate recorded decision. Protected `main`, the M9 transport hold, historical failure evidence and retained data stay unchanged.

The approval to run G17 verification is limited to the isolated, disposable feature-branch campaign. It does not authorize production monitoring, data deletion, other branch activity, or a release.
