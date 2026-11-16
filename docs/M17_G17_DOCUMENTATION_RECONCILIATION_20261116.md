# G17 Repository-Wide Documentation Reconciliation

**Recorded:** 2026-11-16
**Branch:** `feat/harborops-g17-s25-audit-6439ae`
**Baseline source:** `c2eefbfd71b1c228722e99df65afa4f0f09b7a94`
**Change type:** Documentation only. No executable source, verification test, workflow, generated dependency graph, accepted branch or protected main is modified.

## Authoritative decision

The [qualified P09 decision](M17_G17_P09_QUALIFIED_PASS_DECISION_20261116.md) records explicit agreement to the qualified performance-policy PASS and defers targeted optimization until just before G24 release readiness, or earlier if a frozen source-locked gate fails and remediation is agreed. The prior f6 FAIL and narrow memory margin remain in the record; the approved 10% pooled latency and 15% mean memory budgets and estimator remain unchanged.

The [diagnostic discrepancy](M17_G17_DIAGNOSTIC_HASH_FINDING_20261116.md) records the internal `diag-state.json` hash-order error, with original evidence preserved and prospective correction still open.

## Documentation touched and authority

31 active repository documents were aligned, spanning README, STATUS, ROADMAP, DECISIONS, ACCEPTANCE_CRITERIA, requirements, operations, security, testing, policy, milestone M17 plans and the current G17-S25 review materials. Notable reconciliation:
- The authoritative [Status](STATUS.md) records P09 QUALIFIED PASS and G17 NOT ACCEPTED, distinguishes historical f6/f7 SHA `5b1dcc41` from controlled r1c/r2/r3 SHA `8d5e2413`, and requires final source-locked verification.
- [Acceptance Criteria](ACCEPTANCE_CRITERIA.md) no longer calls the executed G17 technical campaign "planned, not executed"; it still requires all independent S25 clauses.
- [Documentation Status Guide](DOCUMENT_STATUS_GUIDE.md) now describes the controlled P09 plan as executed, not awaiting execution.
- The historic 2026-11-12 [M17 policy checklist](M17_POLICY_PROPOSAL_20261112.md) remains its original design-time snapshot with a dated current P09 decision note.
- [Release readiness](RELEASE_READINESS_TODO.md) explicitly tracks deferred P09 optimization, earlier failure-triggered remediation, separate G17 review, and the prospective diagnostic producer fix.
- The [S25 open findings](M17_G17_S25_OPEN_FINDINGS_REGISTER_20261114.md) distinguish the qualified P09 policy disposition from pending S25-A01/A02/A03 and the diagnostic hygiene finding.

Old time-specific paragraphs and failed evidence are preserved and relabeled as historical where necessary. The original pre-registered controlled protocol, source-locked artifacts, mutation audit files, and signed past-milestone decisions are untouched. `EXECUTION_GRAPH.md` is generated from `execution-graph.json` and was intentionally not edited; its dependencies and G17 to M18 blocking edge remain in effect.

## Outstanding mandatory work

1. Freeze the final G17 candidate including source, tests, fixtures and runner; execute the complete no-skip source-locked gate and preserve every attempt and manifest.
2. Complete separate independently attested G17-S25 A01 source/evidence, A02 procedural and policy, and A03 signed recommendation, and record a distinct G17 acceptance decision if justified.
3. Preserve and jointly diagnose any failed source-locked verification before applying a minimal fix and rerunning the required gate. No silent waiver or threshold relaxation.
4. Immediately before G24 release, optimize and remeasure P09 under the unchanged limits; earlier remediation takes priority if a source-locked candidate fails.
5. Fix the diagnostic state hash producer prospectively while retaining r1c/r2/r3 original evidence. Maintain M9 transport hold, M16 audit protections, default-off telemetry and no deployment authorization.

## Validation boundary

The original review branch contains a pre-existing `scripts/check-planning-docs.mjs` M11 expectation failure. This docs-only commit does not repair that unrelated script or claim that it was executed. No Node, .NET, browser, PowerShell, Docker or hosted verification was launched by this documentation reconciliation. G17 technical reports from older SHAs must not be relabeled as verification of the final branch tip.

**G17 NOT ACCEPTED; acceptance pending.**
