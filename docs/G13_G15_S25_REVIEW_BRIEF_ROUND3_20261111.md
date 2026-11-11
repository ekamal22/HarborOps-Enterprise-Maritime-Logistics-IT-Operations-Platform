# G13 and G15 S25 Independent Review Brief, Round 3, 2026-11-11

**Status:** Prepared for round 3 after the round 2 remediation. No round 3 review has been performed. Acceptance pending. Round 1 (`5bcb7e5`) and round 2 (`3e63bd0`) returned NOT PASS for both gates: [G13 r2](G13_S25_INDEPENDENT_REVIEW_ROUND2_20261111.md), [G15 r2](G15_S25_INDEPENDENT_REVIEW_ROUND2_20261111.md). Independence is established by the reviewer's context and identity, not by this document.

Read-only. Do not edit the repository, run migrations against a shared database, use GitHub Actions, or stop or remove any container you did not start (name yours `ho-g13-review-*` or `ho-g15-review-*`).

## Inputs

| Input | Where |
| --- | --- |
| Tested source | commit `4e7b9e22aacef25f7f60e2295938ec6cf79bdadd` on `verify/g13-g15-evidence-closure-20261109` (later commits are documentation only; confirm with `git diff --name-only 4e7b9e2 HEAD`) |
| Change since round 2 | `git diff 3e63bd0 4e7b9e2` |
| Comparison base | `main` at `eb529368a2447061fc1838cd3967b0ffedae669d` |
| Raw evidence | `E:\HarborOps-Verification\g13-g15-final\evidence\c3` or `E:\HarborOps-Verification\g13-g15-final\evidence\c3-evidence-4e7b9e2.zip` (sha256 `d0e12322162479686708570b254c4e99cdbb843ceffdd6f657a75348cc7522ec`). Verify with `PACKET-SHA256.txt` then per-artifact `sha256.txt`. `superseded/` holds the failed attempts |
| Assertion list | `docs/G13_G15_ASSERTION_INVENTORY_20261109.json` (263 assertions, clause-level witnesses) and `ASSERTION-MATRIX.csv` |
| Plans and policy | `docs/M13_VERIFICATION_PLAN.md`, `docs/M15_VERIFICATION_PLAN.md`, `docs/G13_G15_POLICY_DECISIONS_20261110.md`, `docs/M15_POLICY_RECOMMENDATIONS_20261109.md` |
| Scope decision awaiting the owner | `docs/G13_S20_SCOPE_DECISION_20261111.md` |

## Owner decisions in force

No 503 as normal behavior, four live reports per tenant and two per user (extra builders get a prompt 429), idle KPI cleanup behind `M13:EnableIdleCleanup` default off, scoped 25 s member statement (global 15 s, 500,000 member ceiling, 15 s p50 and 20 s p95 targets unchanged), verification-only workflow changes excluded from the merge, no hosted run at the merge SHA. The realistic 170,000 shipment requirement is **not** claimed as satisfied.

## What to examine

Source correctness; whether each round 2 finding is genuinely closed (do not rely on the disposition table in the [c3 results](G13_G15_LOCAL_CLOSURE_RESULTS_C3_20261111.md); read the tests); for every assertion in scope whether the named witness proves **that clause** (the mapping is now clause-level, check it); mutation validity (control clean, mutant compiles, the named test fails for the stated reason; note the two mutants killed by a database level 503); security boundaries; performance; migration and rollback; policy compliance; artifact integrity including the harness-only reruns recorded under "Attempts"; residual risks.

**G13-S25 scope:** early admission control (`OperationalKpiStore.cs` build slots, `m13_tenant_live_reports`), the late quota decision, lock and cancellation behavior, the 41 source and 4 security mutants, the new tests (`M13AdmissionControlAcceptanceTests`, `M13SecurityEnvelopeAcceptanceTests`, the first-event, plan, voyage and bucket tests), the browser content security policy assertion and its WebKit control, the S20 evidence and the scope decision.

**G15-S25 scope:** the per-intent dispatcher limit and claimed-count mechanism (`NotificationStore.DeliverPendingAsync`, `m15_deliver_pending`), the unread count contract at every boundary and in the browser, the 25 G15 mutants (including the one rerun alone), the dispatcher race and reader isolation tests, default-off wiring, the downgrade guard, the quarantine runbook, residual risks.

## Questions to answer in writing

1. Is each round 2 finding closed? List any that are not.
2. Does every in-scope assertion have a witness that proves its exact clause? List those that do not.
3. Are the mutation witnesses valid? Is there a defect class no mutant covers?
4. Can the early admission refuse a builder that capacity would allow, or admit more than four heavy builds per tenant, in any ordering? Is the late decision still authoritative?
5. Is the intermittent, unexplained hang of the skip-locked mutant test a defect in the product, in the test or in the harness? Is rerunning that job alone acceptable evidence?
6. Were the harness-only reruns (browser, G15 mutations, database and concurrency jobs with extended filters) legitimate, and is the evidence still source-locked?
7. Is the G13-S20 scope proposal honest about what is and is not satisfied?
8. Any security, tenant-isolation, data-loss, performance or migration risk that blocks acceptance or merge?

## Deliverable

For each gate, a dated recommendation of PASS or NOT PASS, findings ranked by severity with file and line references, the commit SHA reviewed, and answers to the eight questions. Return it as your final message only; findings are recorded in this repository after the reviewer returns them.
