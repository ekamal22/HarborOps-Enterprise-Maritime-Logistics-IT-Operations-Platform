# G13/G15 Verified Evidence and Acceptance Status, 2026-11-10

**Current disposition:** Technical campaign PASS; G13 and G15 milestone acceptance pending. This is the current status reference for M13/M15. Historical 2026-11-09 plans, failed-run tables and source audits retain their original SHA-specific results.

## Source lock and executed results

- **Verified implementation commit:** `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75` on `verify/g13-g15-evidence-closure-20261109`.
- **Main baseline:** `eb529368a2447061fc1838cd3967b0ffedae669d`, with G0-G12 and G16 accepted. This G13/G15 verification branch is not merged into `main`.
- **Preflight:** [GitHub Actions run 42726180647](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42726180647), PASS. Release build, four focused M13/M15 cross-milestone regressions, 170,000-shipment KPI scale measurement and 18 effective G13 source mutations passed.
- **Integrated verification:** [GitHub Actions run 42727554951](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951), PASS, 15 of 15 jobs. The source-locked ledger reports 541 TRX executions passed, 0 failed, 0 skipped, 0 other. Evidence integrity PASS with no integrity issues.
- **Technical scenarios:** 48 of 48 G13-S01 through S24 and G15-S01 through S24 *technical evidence contract checks* passed. This is not a recorded acceptance decision for those 48 complete scenarios.
- **Other verified areas:** restricted PostgreSQL permissions, combined M13/M15 and accepted-milestone regressions, 170,000-shipment reporting, 500,000-member fail-closed cap, four-principal reporting concurrency, 1,000,000-row notification feed, dual-dispatcher fanout, three-engine browser suites, mutation sensitivity, crash recovery before/after commits and dependency/secret inventory.
- **Artifact:** [retained scenario and assertion evidence](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951/artifacts/13722934430) includes `index.json`, `assertion-audit.json`, `matrix.md`, technical scenario results and SHA-256 manifests.

The reporting preflight's 30-sample campaign recorded p50 **13,531 ms**, p95 **14,368 ms** and maximum **14,473 ms**, under approved test envelopes of p50 at most 15,000 ms, p95 at most 20,000 ms and SQL statement timeout 15 seconds. The maximum sample leaves limited SQL timeout headroom; it is not a production SLA approval. The 500,001-member attempt was rejected without partial report publication. The limits were not relaxed to obtain PASS.

## Atomic assertion audit: passing workflow does not mean full coverage

The committed [242-assertion inventory](G13_G15_ASSERTION_INVENTORY_20261109.json) classifies 227 assertions as candidate test-mapped and 15 as policy or independent-review requirements. The *executed* [assertion audit](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951/artifacts/13722934430) gives the narrower disposition:

| Atomic assertion outcome | Count |
| --- | ---: |
| Technical evidence present | 224 |
| Technical evidence missing | 3 |
| Policy review | 7 |
| Independent review | 8 |
| **Total** | **242** |

**Outstanding technical evidence IDs:**
- `G13-S08-A04`: Default-off M10 projector and opt-in M11 evaluator. The inventory points to `M10ProjectorAndM11EvaluatorRequireExplicitOptIn`, but the run did not include a matching passed test witness.
- `G13-S09-A03`: Out-of-order GTIN/GTOT must not invent progress. The inventory requires the passed test witness `LateWorkerEffectDuringRebuildIsNeverLostAndMarksTheProjectionStale`, which the atomic verifier did not find under its required matching rule. Other G13 tracking coverage passed, but that is not a substitute for this evidence contract.
- `G13-S09-A04`: M10 correction-generation and idempotency. The same required late-worker/rebuild test witness is missing in the executed assertion audit.

These are **evidence gaps**, not evidence of three failing application behaviors. Do not relabel them PASS without a source-locked passing test and assertion-level reconciliation. The atomic verifier reports `full_automated_assertion_coverage: false` despite the successful job result, because the non-enforcing audit records these gaps explicitly. Any future complete-coverage gate must enforce them without suppressing or waiving tests.

## Full milestone scenario ledger

The run's final scenario ledger records **for each gate separately**: 1 PASS, 23 BLOCKED and 1 NOT EXECUTED. The S25 independent review is NOT EXECUTED for both G13 and G15. The technical scenario check tally (48/48) and this full acceptance ledger are different assessments; neither supersedes the other automatically.

## Approved decisions and remaining authorizations

- M13 D-005 Option A is approved. The G13 p50/p95, concurrency and capacity test envelopes are approved verification limits. Permanent reporting storage, resource quotas, retention and cleanup numerical policy still require explicit agreement.
- M15 P01-P09, P11, P13 and P14 are approved. P10 is **approved conditionally**, subject to reconciling transactional fail-closed recovery and accepted-M11 compatibility with the passing evidence. P12 has approved **provisional periods only**, with **no retention deletion authority**. Cleanup stays off.
- G13-S25 and G15-S25 need separate independent raw-artifact, source and residual-risk reviews and an explicit acceptance decision. Green GitHub Actions cannot make those approvals.
- M13/M15 remain unmerged and unactivated. No main merge, deployment, production migration, dispatcher enablement, retained data cleanup or release is authorized. The M9 transport-processing hold, 10,000 unresolved-work cap, null ETA, M10 projector default-off and M11 evaluator opt-in remain binding.

## Next actions and documentation ownership

1. Reconcile the three named atomic evidence gaps at the verified source boundary. If test execution is necessary, request an explicitly authorized source-locked verification on a new code SHA. This documentation reconciliation alone does not trigger GitHub Actions.
2. Resolve G13 permanent storage/retention numerical policy and the P10/P12 residual conditions without bypassing technical conformance.
3. Complete independent G13-S25 and G15-S25 reviews, reconcile all scenario dispositions and record explicit decisions. Only then reassess integration readiness.
4. Read [STATUS.md](STATUS.md) for the current repository state, [ROADMAP.md](ROADMAP.md) for prerequisites, and the original [G13](M13_VERIFICATION_PLAN.md) and [G15](M15_VERIFICATION_PLAN.md) matrices for full scenario definitions.

This document is a **documentation-only reconciliation after the verified source SHA**. Later Markdown-only commits do not constitute a new technical verification run.
