# G13/G15 Source-Locked Scenario Evidence Contract

**Current G13/G15 coverage checkpoint (2026-11-09):** The most recent successful [15-job run #42632573516](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42632573516) verified commit `50ed2950bceaf3478172c3f9d9bf85186f874082`. New source-locked behavioral suites and the [atomic coverage inventory](G13_G15_ASSERTION_INVENTORY_20261109.json) were added later on `verify/g13-g15-evidence-closure-20261109` and have not passed GitHub Actions at that newer SHA. The 242 atomic requirements are currently classified as 214 with candidate full behavioral coverage, 13 partially covered, and 15 needing policy or independent review. Candidate means source-mapped, **not verified**. The accepted main baseline, manual reviewer S25 gates, G13 permanent storage decision, M15 P10 conditional review and M15 P12 cleanup prohibition remain unchanged. No main merge or deployment has occurred.

**Current G13/G15 assertion campaign (2026-11-09):** [The latest successful 15-job verification](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42632573516) passed against `50ed2950bceaf3478172c3f9d9bf85186f874082`. New behavioral cases and [atomic assertion audit](G13_G15_ATOMIC_COVERAGE_20261109.md) have since been committed and **are unverified at the newer HEAD**. All 242 assertions are classified (214 candidate fully tested, 13 partially tested, 15 requiring policy/independent review). M13 and M15 are implemented but G13/G15 milestone acceptance and main merge remain pending. Older checkpoints below remain historical for their stated SHAs.

**Campaign branch:** `verify/g13-g15-evidence-closure-20261109`

**Last verified predecessor:** [Run 42612086368](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42612086368) passed all 15 jobs, 499 TRX tests and artifact integrity at `38bf5f3b34babbb2652bc87b6202c51172e3589c`. Those results are historical, not evidence for a modified SHA.

## New verification work

The existing integration, unit, contract, browser, mutation, scale and process-crash suites remain mandatory. The final job additionally runs `scripts/check-g13-g15-scenarios.py`. This executes 48 separate technical evidence checks against exact-source TRX, browser, fault, scale, policy, security, SQL mutation and SIGKILL artifacts. A missing required reference or ineffective named security mutation fails the new gate. Source SHA and every existing raw evidence manifest are still checked by the preceding verifier.

New retained evidence: `technical-scenarios.json`, `technical-scenarios.xml`, `technical-scenarios.md`, and their SHA-256 entries.

These are acceptance-evidence checks, not substitutes for application behavioral testing or independent source review. Source-locked passing evidence demonstrates executed coverage of the mapped assertions; it must not be interpreted as proving every qualitative clause in each scenario.

## Decisions not replaceable by tests

1. G13-S01 still requires an explicit numerical permanent report storage and bounded cleanup policy based on measured size and concurrency. The provisional 170,000 shipment and 30-report test envelope is not an operational capacity approval.
2. M15-P10's conditional fail-closed history and outbox coupling must be reviewed with the actual rollback/recovery and M11 evidence. M15-P12 permits provisional durations, not deletion.
3. G13-S25 and G15-S25 require an independent raw-artifact, source-lock and residual-risk review, then explicit acceptance. A self-authored automated summary cannot provide reviewer separation.

No main merge, deployment, production migration, retention cleanup or dispatcher activation is included.

## Launch

Use the manual [G13/G15 verification workflow](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/workflows/verify-m11-m12-combined.yml) with ref `verify/g13-g15-evidence-closure-20261109`. Its alternate entry point `verify-g13-combined-full.yml` remains source-equivalent. Only one workflow should be dispatched for a given source commit.
