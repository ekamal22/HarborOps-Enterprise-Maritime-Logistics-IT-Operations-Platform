# G13/G15 Source-Locked Scenario Evidence Contract

**Current gate boundary (2026-11-11):** G13-S25 round 3 returned NOT PASS on the c3-tested source. G13-S20 Option A has since been approved with a 250,000-member cap per report, superseding the former single-report shipment-scale requirement. Post-c3 source remediation and its unexecuted verification witnesses are tracked in [the current status](G13_POST_C3_REMEDIATION_20261111.md). Historical pass/failure records below remain unchanged and apply only to their recorded SHAs. G13 and G15 are not accepted; the G15-S25 round 3 verdict is not recorded here. No main merge, GitHub Actions, production activation or retention deletion is authorized.

**Current review decision (2026-11-10):** The [15-job G13/G15 technical run](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415) PASSED at `15cabd8c542159753c927c2bd65b4346223e84f6`, with 543 passed TRX cases, 227/227 mapped technical assertions and source integrity PASS. The separate [G13/G15 S25 review](G13_G15_REVIEW_MATRIX_20261110.md) and [formal decision](G13_G15_FORMAL_GATE_DECISIONS_20261110.md) record G13 NOT ACCEPTED and G15 NOT ACCEPTED. G13 permanent quota/idle cleanup and G13/G15 exact negative mutation and HTTP-outage proof remain outstanding. Earlier status snapshots below are historical and apply only to their own source SHAs. No main merge, deployment, cleanup or activation.
**Current technical evidence (2026-11-10):** [15-job run 42727554951](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed at `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75`, including 541 passing TRX tests, no failures/skips, and 48 passing technical scenario contract checks. The separate 242-assertion audit has 224 technical witnesses present, 3 missing, 7 policy-review and 8 independent-review items. Full G13/G15 acceptance remains pending. [Current source-locked evidence](G13_G15_VERIFIED_EVIDENCE_20261110.md).

**Campaign branch:** `verify/g13-g15-evidence-closure-20261109`

**Last verified predecessor:** [Run 42612086368](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42612086368) passed all 15 jobs, 499 TRX tests and artifact integrity at `38bf5f3b34babbb2652bc87b6202c51172e3589c`. Those results are historical, not evidence for a modified SHA.

## New verification work

The existing integration, unit, contract, browser, mutation, scale and process-crash suites remain mandatory. Their latest 15-job technical campaign passed at the frozen source. The final job executed `scripts/check-g13-g15-scenarios.py`. This executed 48 separate technical evidence checks against exact-source TRX, browser, fault, scale, policy, security, SQL mutation and SIGKILL artifacts. All 48 technical contract checks passed. Separately, the 242-assertion audit still found three missing executed test witnesses; this conservative audit was not enforced as a full-coverage gate. Source SHA and every existing raw evidence manifest are still checked by the preceding verifier.

New retained evidence: `technical-scenarios.json`, `technical-scenarios.xml`, `technical-scenarios.md`, and their SHA-256 entries.

These are acceptance-evidence checks, not substitutes for application behavioral testing or independent source review. Source-locked passing evidence demonstrates executed coverage of the mapped assertions; it must not be interpreted as proving every qualitative clause in each scenario.

## Decisions not replaceable by tests

1. G13-S01 still requires an explicit numerical permanent report storage and bounded cleanup policy based on measured size and concurrency. The provisional 170,000 shipment and 30-report test envelope is not an operational capacity approval.
2. M15-P10's conditional fail-closed history and outbox coupling must be reviewed with the actual rollback/recovery and M11 evidence. M15-P12 permits provisional durations, not deletion.
3. G13-S25 and G15-S25 require an independent raw-artifact, source-lock and residual-risk review, then explicit acceptance. A self-authored automated summary cannot provide reviewer separation.

No main merge, deployment, production migration, retention cleanup or dispatcher activation is included.

## Launch

Use the manual [G13/G15 verification workflow](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/workflows/verify-m11-m12-combined.yml) with ref `verify/g13-g15-evidence-closure-20261109`. Its alternate entry point `verify-g13-combined-full.yml` remains source-equivalent. Only one workflow should be dispatched for a given source commit.

## G13-S20 approved scope amendment, 2026-11-11

The G13-S20 original single-report workload of a realistic 170,000-shipment tenant is superseded by the approved per-report ceiling of 250,000 KPI member rows. The concurrent and storage boundaries remain four live reports per tenant, two per user and 1,000,000 aggregate live members per tenant. The verification workload must prove successful correct reports for a 40,000-realistic-shipment tenant and an 85,000 simple-reference-shipment tenant, plus fail-closed HTTP 413 above 250,000 members. Shipment counts are illustrative fixtures rather than hard deployment limits. Multiple differently filtered reports remain separate snapshots, with no guaranteed automatic stitching or deduplication. All other technical/security clauses and independent review requirements remain binding. The revised source is not yet verified.
