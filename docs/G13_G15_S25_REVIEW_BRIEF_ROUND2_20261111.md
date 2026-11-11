# G13 and G15 S25 Independent Review Brief, Round 2, 2026-11-11

**Status:** Round 2 was performed on 2026-11-11 by two separate read-only sessions and returned **NOT PASS** for both gates: [G13](G13_S25_INDEPENDENT_REVIEW_ROUND2_20261111.md), [G15](G15_S25_INDEPENDENT_REVIEW_ROUND2_20261111.md). Acceptance pending. Round 1 (`5bcb7e5`) returned NOT PASS for both gates: [G13](G13_S25_INDEPENDENT_REVIEW_ROUND1_20261110.md), [G15](G15_S25_INDEPENDENT_REVIEW_ROUND1_20261110.md). Independence is established by the reviewer's context and identity, not by this document; a review run by the session that produced the evidence does not count.

This brief is for reviewers who did not implement or verify this change set. Work from source and raw evidence, not from the conclusions of the [round 2 results](G13_G15_LOCAL_CLOSURE_RESULTS_C2_20261111.md). Read-only: do not edit the repository, run migrations against any shared database, or use GitHub Actions.

## Inputs

| Input | Where |
| --- | --- |
| Tested source | commit `3e63bd0e71bdf4a80cd8ea1d27e7cceca3c37524` on `verify/g13-g15-evidence-closure-20261109` (later commits are documentation only; confirm with `git diff --name-only 3e63bd0 HEAD`) |
| Change since round 1 | `git diff 5bcb7e5 3e63bd0` (application, migration, tests, scripts) |
| Comparison base | `main` at `eb529368a2447061fc1838cd3967b0ffedae669d` |
| Raw evidence | `E:\HarborOps-Verification\g13-g15-final\evidence\c2`, or `E:\HarborOps-Verification\g13-g15-final\evidence\c2-evidence-3e63bd0.zip` (sha256 `ada16acdfb7df19f97b4c79c972f345174b3fe92080906aaaf761bda1841b060`). Verify with `PACKET-SHA256.txt` then per-artifact `sha256.txt`. `superseded/` holds failed attempts |
| Assertion list | `docs/G13_G15_ASSERTION_INVENTORY_20261109.json` (257 assertions) and `ASSERTION-MATRIX.csv` |
| Plans and policy | `docs/M13_VERIFICATION_PLAN.md`, `docs/M15_VERIFICATION_PLAN.md`, `docs/G13_G15_POLICY_DECISIONS_20261110.md`, `docs/M15_POLICY_RECOMMENDATIONS_20261109.md`, `docs/G13_G15_FORMAL_GATE_DECISIONS_20261110.md` |

## Owner decisions in force

Concurrent builders must not return 503 as normal behavior; two active reports per user inside four per tenant; idle KPI cleanup is behind `M13:EnableIdleCleanup`, default off; the member-materialization statement limit is 25 s for that statement only (global 15 s, 500,000-member ceiling, 15 s p50 and 20 s p95 targets unchanged); verification-only workflow changes are excluded from the merge; no hosted run at the merge SHA.

## What each review must examine

Source correctness; exact behavior-to-assertion mapping (read the test body and confirm it asserts the clause); the failed and recovered history (`superseded/`, `commands.tsv`, the browser attempts); security boundaries; mutation validity (does each mutant remove the specific protection it names, with a clean control); performance; migration and rollback; cross-milestone compatibility; policy compliance; artifact integrity; residual risks and merge blockers.

**G13-S25:** KPI definitions K01 to K14, D and Q metrics, the 12 plus 2 decoy fixture, tenant and Auditor scoping, snapshots, tenant quota of 4 and user quota of 2, the planner and timeout scoping in `OperationalKpiStore` (restored on success, failure and rollback), lock scope and the 28 s envelope, realistic 85,000 and 170,000 shipment evidence, the 29 source mutants and 4 SQL security mutants, crash windows, KPI browser suite, idle cleanup switch and bound of 16.

**G15-S25:** M11 history to outbox intent, scoped personal receipts and isolation, membership versioning, cursors, the 1,000-recipient cap, delivery timeout trap, bounded unread count (cap 1,000) and state filters at 1,000,000 receipts, the HTTP outage test for list, count, detail and PATCH, retry exactly once, audit rollback, the 17 G15 mutants, crash windows, notification browser suite, P10 fail-closed, P12 deletion disabled.

## Questions to answer in writing

1. For each round 1 finding, is the disposition in the round 2 results correct? List any that remain open.
2. Does every in-scope assertion have a witness that proves its exact clause? List those that do not.
3. Are the mutation witnesses valid (control passes, mutant compiles, the named test fails for the right reason)? Does a surviving class of defect exist that no mutant covers?
4. Are the planner and timeout settings correctly scoped, and can any failure path leave them changed on a pooled connection?
5. Is a 429 `report_user_capacity_exceeded` after three dashboard loads within 10 minutes acceptable, given the web client does not release snapshots?
6. Is local-mode evidence (derived job results, real SHA, no `GITHUB_*` values) acceptable for this gate? Is the browser harness reset between engines a legitimate isolation step?
7. Any security, tenant-isolation, data-loss, performance or migration risk that blocks acceptance or merge?

## Deliverable

For each gate, a dated recommendation of PASS or NOT PASS, findings ranked by severity with file and line references, and the commit SHA reviewed. Findings are recorded in this repository only after the reviewer returns them. Reviewer identity and independence are recorded by the person who commissions the review.
