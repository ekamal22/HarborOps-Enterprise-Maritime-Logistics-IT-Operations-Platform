# G13/G15 Evidence-Closure Correctness Audit

**Current gate boundary (2026-11-11):** G13-S25 round 3 returned NOT PASS on the c3-tested source. G13-S20 Option A has since been approved with a 250,000-member cap per report, superseding the former single-report shipment-scale requirement. Post-c3 source remediation and its unexecuted verification witnesses are tracked in [the current status](G13_POST_C3_REMEDIATION_20261111.md). Historical pass/failure records below remain unchanged and apply only to their recorded SHAs. G13 and G15 are not accepted; the G15-S25 round 3 verdict is not recorded here. No main merge, GitHub Actions, production activation or retention deletion is authorized.

**Current review decision (2026-11-10):** The [15-job G13/G15 technical run](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415) PASSED at `15cabd8c542159753c927c2bd65b4346223e84f6`, with 543 passed TRX cases, 227/227 mapped technical assertions and source integrity PASS. The separate [G13/G15 S25 review](G13_G15_REVIEW_MATRIX_20261110.md) and [formal decision](G13_G15_FORMAL_GATE_DECISIONS_20261110.md) record G13 NOT ACCEPTED and G15 NOT ACCEPTED. G13 permanent quota/idle cleanup and G13/G15 exact negative mutation and HTTP-outage proof remain outstanding. Earlier status snapshots below are historical and apply only to their own source SHAs. No main merge, deployment, cleanup or activation.
**2026-11-10 successor checkpoint:** The original design, decision or source review below remains a historical record for its stated date and source SHA. A later [G13/G15 15-job technical campaign](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed at `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75` with 541 passing TRX cases, but the 242-assertion audit still identifies 3 missing technical witnesses and 15 policy/independent-review items. See [current verification and acceptance status](G13_G15_VERIFIED_EVIDENCE_20261110.md). No main merge, release, activation or deletion authority follows from the technical result.

**Date:** 2026-11-09.
**Branch:** `verify/g13-g15-evidence-closure-20261109`.
**Audited test and workflow source:** `c6cc6f963a35d85358da447fa50d7b2163139e86`.
**Prior passing source:** `e4bf9cd1527f85f554baf623e90805d96d08ac1a`, [run 42569970541](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42569970541).
**Verdict:** SOURCE-LEVEL AUDIT COMPLETE. NEW SOURCE EXECUTION NOT VERIFIED. G13/G15 GATES NOT ACCEPTED. MAIN MERGE NOT AUTHORIZED.

## Scope and method

Reviewed the test and orchestration changes since the passing candidate: G13 restricted-role SQL plans, KPI cleanup and permission boundaries; G15 recipient, receipt, cancellation, crash/recovery and mutation tests; both manually dispatched workflow files; and the source-locked evidence ledger. The prior evidence review remains in [G13/G15 Source-Locked Evidence Review](G13_G15_SOURCE_LOCKED_EVIDENCE_REVIEW_20261109.md).

Performed source-level cross-checks of test names and filters, all fifteen workflow jobs, mutation case counts, recovery phase names and ledger expectations. Structural delimiter checks of all five changed C# test files and all five changed Python scripts found no mismatched nesting or unterminated string/comment modes. These checks are not substitutes for compilation, Python parsing by the Python interpreter, or runtime testing.

## Correctness findings and fixes

1. **Server-wide M10 provisioning contention.** The combined regression workflow originally placed M10 tests that rotate global PostgreSQL role credentials in the same test process as M11/M12. Both mirrored workflow definitions now run the selected M10 checks separately and preserve their own TRX and console output.
2. **Insufficient M15 cancellation witness.** A pre-canceled token did not prove an in-flight, SQL-blocked delivery could roll back. Added an isolated lock-and-cancel test that establishes an active blocked operation, cancels it, verifies absence of partial effects, and recovers the committed source with effect-once personal receipts. Retained the pre-canceled control.
3. **Incomplete restricted-role query-plan evidence.** G13's earlier 170k EXPLAIN used a privileged connection. Added real `harborops_api` role and session assertions, signed read scope, isolated rollback, plans for small, medium, 170k skewed and empty-result cases, and file-byte-count and SHA-256 reconciliation.
4. **Benchmark evidence contamination.** A high-volume `EXPLAIN ANALYZE` inserts rows before rollback, which can grow physical table files and distort storage deltas. Moved the 170k restricted-role plan and empty plan after the 30 measured reporting builds. The historical privileged plan remains and is explicitly distinguished from the restricted evidence.
5. **G13 report cleanup and authority coverage.** Added bounded expired-report pruning checks, foreign-tenant retention and history-preservation assertions, explicit signed Administrator and ungranted Auditor SQL denials, and SQL-grant/tenant-predicate mutants.
6. **G15 delivery and recipient coverage.** Added separate signed-recipient isolation and receipt-uniqueness controls, receipt uniqueness and scoped-identity mutants, source-event claim and pre/post-fanout SIGKILL/recovery windows, and an acknowledged response-discard recovery window.
7. **Ledger completeness.** The ledger now demands passed results for new closure-specific tests, all required crash phases, the restricted-role query-plan artifacts with matching byte sizes and digests, the KPI numerical envelope, and dependency/security scan evidence. The planned mutation campaign counts are G13 source 14, G13 SQL security 4, G15 notification 10.

## Verified source-level facts

- Both manually dispatched verification workflows are identical.
- The workflow retains exactly fifteen jobs and has no push-triggered execution.
- Mutation source lists contain the named 14, 4 and 10 variants expected by the ledger.
- Crash orchestrator declares eight explicit phases, with recovery where required.
- No changes to `main`, production configuration activation, business-data migrations or deployment were needed for these test-coverage fixes.
- The successful 15-job run remains evidence **only** for its original source SHA. Its results must not be attributed to the revised candidate.

## Execution and acceptance boundaries

**Not executed on this branch:** .NET Release build, integration and contract tests, PostgreSQL RLS and concurrency checks, source mutations, eight actual SIGKILL probes, 30-sample G13 capacity campaign, browser checks, and final evidence reconciliation. The current audit environment did not expose the required .NET and PostgreSQL runtimes. No GitHub Actions run was dispatched, and no claim of a passing revised candidate is made.

**Acceptance remains pending:** The complete 50-scenario G13/G15 matrix, final independent G13-S25 and G15-S25 review, source-locked retained raw artifacts for the new SHA, approved permanent KPI storage/capacity and cleanup policy, and any separately required operational retention/deletion authority. M15-P10's availability tradeoff and M15-P12's no-deletion boundary remain in force. Technical PASS does not itself close milestone acceptance.

## Next decision

Dispatch the manual 15-job campaign on `verify/g13-g15-evidence-closure-20261109` after reviewing its final branch SHA. Treat any failures as defects to fix in a new source-locked revision. Once the revised raw evidence passes, reconcile all scenarios and record separate S25 decisions. Only then prepare a guarded merge into `main`. Do not deploy, enable notification delivery in production, or activate cleanup as part of this verification.
