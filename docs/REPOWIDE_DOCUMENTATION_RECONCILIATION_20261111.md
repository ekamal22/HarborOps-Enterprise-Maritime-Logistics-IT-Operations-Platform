# Repository-wide documentation reconciliation, 2026-11-11

**Scope:** Documentation-only reconciliation on `feature/g13-post-c3-remediation-20261111`. No application source, migrations, tests, workflows, original evidence archives, `main` history or deployment settings changed during this reconciliation. Documentation-only commits carry `[skip ci]`.

## Inventory and classification

The repository tree was enumerated recursively and was not truncated. The documentation inventory immediately before this index was added contained **235** documents under `docs/` plus the root README: **231 Markdown** and **4 JSON** entries. This index brings the total to **236** (232 Markdown and 4 JSON), grouped by filename class as follows:

| Documentation class | Count after this index | Treatment |
| --- | ---: | --- |
| Core/general references including README, execution graph and security documentation | 29 | Update living G13/G15 status, scope and permissions as relevant |
| G13/G15 records and review documents | 32 | Update current gate navigation; preserve dated source-locked reports, independent reviews and historical fail/pass evidence |
| M13/M15 plans and verification records | 17 | Reconcile approved KPI scope, source SHA, current outstanding G15 and combined test evidence |
| Other milestone records (G0-G12 and G16 and adjacent M-series archives) | 154 | Preserve accepted and historical status; no new implementation or gate decision is inferred |
| Repository-wide reconciliation indexes (2026-11-08 through 2026-11-11) | 4 | Preserve older source-bound indexes; this file is the current reconciliation |
| **Total** | **236** | **232 Markdown, 4 JSON** |

The full tree was classified. The **50 G13/G15-relevant current/reference files** updated during this pass were inspected before being changed. The remaining dated milestone archives and historic evidence records were kept unchanged intentionally; tree classification does not constitute a fresh line-by-line audit of every archival file.

## Exact technical and acceptance boundary

- **Protected `main`:** `eb529368a2447061fc1838cd3967b0ffedae669d`, unchanged. Accepted G0-G12 and G16 remain accepted.
- **Historical c3 source:** `4e7b9e22aacef25f7f60e2295938ec6cf79bdadd`, local technical groups passed under the former 500,000-member ceiling. G13 independent S25 round 3 subsequently returned NOT PASS. Historical figures in c3 and earlier run reports remain untouched.
- **Revised G13-S20 scope:** Option A was approved with a stricter 250,000 KPI-member ceiling per report, four live reports per tenant, two per user and a 1,000,000 live-member aggregate tenant limit. The previous realistic 170,000-shipment single-report expectation is superseded rather than claimed as satisfied. Larger data sets require separate filtered reports; no automatic total stitching or deduplication is implied.
- **Completed hosted failure:** [run 43090424080](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43090424080) at `30fd28f6d2eaab2535cd68a9139c2c70435f1898` ended FAILURE, with 10 successful jobs and failed combined database, browser, G13 mutation and live crash jobs, plus failed evidence reconciliation.
- **Intermediate hosted run:** [run 43091966161](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43091966161) at `1f4ce182950e91a2fce8f4188e1b4397dde740df` had 12 passed jobs, a failed browser job and a pending G13 mutation job at the recorded review.
- **Latest source-locked hosted run checked:** [run 43093329982](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43093329982), source `067e1ab57f33fcabefae55727265499d86daf7ae`. Preflight, the combined Chromium/Firefox/WebKit browser suite, process-kill recovery, security, G13 fault and concurrency, G15 million-receipt and fanout and M10-M12 combined regressions passed. The G13 mutation, G15 mutation, scale and combined M13/M15 database jobs were still running; its final evidence ledger had not started when recorded.
- **Acceptance:** G13 NOT ACCEPTED; G15 NOT ACCEPTED. The G15 round 3 positive independent decision was not established in reviewed repository records. The current source requires one complete successful packet, exact-assertion reconciliation, independent S25 reviews and explicit gate approvals. Do not combine partial passes from different SHAs into a full PASS.

See [hosted run register](G13_G15_POST_C3_HOSTED_RUN_REGISTER_20261111.md) for each run's failure classification and current next checks, [post-c3 remediation](G13_POST_C3_REMEDIATION_20261111.md) for source scope, and [G13-S20 decision](G13_S20_SCOPE_DECISION_20261111.md) for the approved capacity policy.

## Documentation reconciled in this pass

1. **Root and living project state:** `README.md`, `STATUS.md`, `ROADMAP.md`, `RELEASE_READINESS_TODO.md`, `DOCUMENT_STATUS_GUIDE.md`, `ACCEPTANCE_CRITERIA.md`, `REQUIREMENTS.md`, `TEST_STRATEGY.md` and `IMPLEMENTATION_PLAN.md`.
2. **Operating and product contracts:** `DECISIONS.md`, `API_CONTRACTS.md`, `OPERATIONS.md`, `ARCHITECTURE.md`, `SECURITY.md`, `PRODUCT_SCOPE.md`, `PRD.md`, `DOMAIN_MODEL.md`, `COST_AND_DEPLOYMENT.md` and `PARALLEL_DELIVERY_POLICY.md`.
3. **M13/M15 plans:** updated architecture, implementation, planning, verification and policy files, plus the prior combined integration register, to link their historical numbers to the current source-bound results.
4. **G13/G15 acceptance surfaces:** updated the scenario contract, atomic coverage, combined campaign, verification, policy, former acceptance-candidate and evidence-reconciliation references, and the approved 250k scope record.
5. **Current-status references:** added the [post-c3 Actions run register](G13_G15_POST_C3_HOSTED_RUN_REGISTER_20261111.md) and revised [remediation status](G13_POST_C3_REMEDIATION_20261111.md) so a reader does not mistake the old no-campaign statement for current reality.

## Deliberately preserved sources

- Earlier c1/c2/c3 local results, historical hosted run and source-check logs, independent S25 round 1/2/3 findings, technical reviewer packets, formal NOT ACCEPTED decisions, migration snapshots and mutation evidence.
- The atomic assertion inventory JSON and scenario checker source remain unmodified by this documentation pass. Its candidate witnesses still require source-locked execution before any unqualified `FULLY_TESTED` claim.
- M10/M11/M12 and G0-G12/G16 historical acceptance documentation and the execution-graph JSON. A G13/G15 document refresh does not reopen or modify accepted baselines.
- Turkish localization work and branches. The integration order remains G13/G15 acceptance, authorized merge into `main`, separate Turkish reconciliation and regression, then separately authorized Turkish merge.

## Rules and remaining actions

GitHub Actions authorization applies only to the approved disposable post-c3 verification campaign and its preflight-gated technical packet. It does not authorize unrelated workflows or changes to application default-off services. Retention deletion remains disabled, M9's transport backlog hold remains binding, and no deployment or protected-main move is permitted.

Before declaring either gate accepted: capture completed statuses from [run 43093329982](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43093329982), reconcile all source-locked technical artifacts and security checks, perform independent G13/G15 S25 reviews, record two positive decisions if justified, and obtain separate integration authorization. Documentation-only edits are not technical verification.

**Disposition:** Repository-wide documentation synchronized to the observed post-c3 campaign state. Historical records preserved. G13/G15 acceptance pending.
