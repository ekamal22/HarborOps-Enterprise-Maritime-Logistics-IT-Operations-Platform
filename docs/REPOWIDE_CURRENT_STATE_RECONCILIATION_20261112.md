# Repository-wide current-state reconciliation, 2026-11-12

## Authority and source

This record reconciles active project documentation against protected `main` at `757bab53e898944c1ba14481ceebdb4f18bc5842`, following [G13 PR #38](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/38) and [G13 documentation PR #39](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/39). It does not alter source, evidence, gate decisions, milestone scope or deployment permission. [Status](STATUS.md) remains the living acceptance ledger.

## Inventory and classification

The recursive source tree at the inspected baseline contained 748 entries and was not truncated. All 185 tracked Markdown and plain-text files were read, including dated milestone records and the third-party license/reference documents. The three tracked documentation JSON files were inspected: `execution-graph.json`, `M11_M12_EXECUTED_RUN_INDEX_20261108.json` and `M11_M12_LOCAL_EVIDENCE_INDEX_20261108.json`. The graph renderer and planning-document checker were also inspected for generated-file and validation boundaries. The source tree was enumerated for application, migration, workflow and configuration paths without changing them.

- Active project documents: README, STATUS, ROADMAP, DOCUMENT_STATUS_GUIDE, PRD, CONTRIBUTING, PARALLEL_DELIVERY_POLICY, TEST_STRATEGY, RELEASE_READINESS_TODO, implementation status registers and applicable product and engineering references.
- Active specifications: product scope, requirements, acceptance criteria, architecture, security, API contracts, domain model, operations, cost and deployment, implementation plan and local storage guidance.
- Historical evidence: source-specific milestone audits, test registers, local decisions, closed gate records and dated reconciliation reports. Their original source association and decisions remain intact.
- Generated documentation: EXECUTION_GRAPH.md is generated from the canonical graph and must not be edited independently of its renderer. The graph is a prerequisite plan, not an observed acceptance ledger.
- Supporting material: the third-party reference/license files and dated structured evidence indexes.

## Verified current integration position

| Stream | Disposition at inspection | Authority |
| --- | --- | --- |
| G0-G12 and G16 | Accepted and integrated in protected `main` | [Status](STATUS.md) and their individual gate closures |
| G13 | Conditionally accepted and integrated on 2026-11-12 through PR #38, merge `b2856449c8d1ab629c8b5419a5af0ebddafbb47d` | [Conditional decision](G13_S25_CONDITIONAL_ACCEPTANCE_DECISION_20261112.md), [post-merge record](G13_POSTMERGE_RECONCILIATION_20261112.md) |
| G15 | Implemented in separate [PR #40 candidate](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/40); NOT ACCEPTED, NOT MERGED | Candidate head observed as `479616a5b7f17c494d897834a66d10b6276bb517`. A passing earlier combined-source campaign is not proof of this candidate |
| Turkish localization | Separate, unmerged and deferred | Preparation branch `work/turkish-g13g15-latest-compat-20261112`, observed head `7af75aa27f8a1549ff3c648d7d02e3d6ef2ef592` |
| G24 | Open; no production authorization | [Release checklist](RELEASE_READINESS_TODO.md) |

G13's S25 technical assessment recommended PASS, but formal reviewer independence was not attested. Its conditional exception is not unconditional acceptance. The bounded report contract permits at most 250,000 member rows per report, four live reports per tenant, two per user, and 1,000,000 aggregate live member rows per tenant. Capacity refusals fail closed, snapshots expire after ten minutes and automatic idle cleanup remains disabled by default. The isolated [G13 run 43133896612](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43133896612) passed at its frozen source and did not include G15 notifications.

The combined [run 43122895445](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43122895445) passed at `c6d2dc86e30913b8d67a011131f9980422e4c197`. It does not confer G15 acceptance or verify the newly assembled PR #40 source. The `outbox-claim-skip-locked-removed` mutant still requires a clean-control and bounded forward-progress behavioral witness that cannot be satisfied by test teardown cancellation. Populated migration, security, performance, source-locked candidate evidence, independent S25 review and explicit acceptance remain necessary. Notification delivery defaults off and retention deletion is not authorized.

Earlier [Turkish run 42852119407](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42852119407) succeeded at older SHA `77a6eea46fafd07a494e9fdbbe6fdedeed738102`; it does not establish correctness of the later preparation branch or future integrated source. Order of work is fixed: G13 conditional integration completed; resolve, verify, review, accept and integrate G15 separately; then reconcile, verify, accept and integrate Turkish localization separately. Neither deferred stream is part of G13's acceptance.

## Active-document corrections

This reconciliation corrects old statements that G13 was still an open implementation or unmerged gate, that M15 had no source implementation, or that G13-G15 shared an integration decision. It refreshes current-state sections in the project overview, status ledger, roadmap, product requirements, documentation guide, contribution and parallel-delivery policy, release checklist, test strategy and G13 post-merge record. Older 2026-11-08 acceptance snapshots in the event-processing, cost, M11 and M12 implementation notes are explicitly marked as dated checkpoints. No original historical test, independent review, policy approval or failed-run evidence is rewritten.

Preserved controls include the M9 ARRI/DEPA sustained-processing hold and 10,000 unresolved-work-item cap; default-off M10 projection; null ETA without sufficient evidence; opt-in M11 evaluation; tenant, role, session and audit isolation; G13 admission limits and default-off cleanup; G15 default-off delivery and forbidden retention deletion; and no deployment. G14-G15 and G17-G24 remain unaccepted. The roadmap stays at M0-M24 with 101 work packages and 25 gates, and the canonical dependency topology is unchanged.

## Known limitation requiring separate authorization

The generated `EXECUTION_GRAPH.md` contains a 2026-11-08 gate-status checkpoint and a “Current starting point” string emitted directly by `scripts/render-execution-graph.mjs`. That dated text predates G13's conditional merge. Altering the generated view alone would make the renderer check fail; correcting the hardcoded generator text would modify an explicitly out-of-scope script. Both were therefore left intact. Read [Status](STATUS.md) for the current gate ledger. A separate permitted generator-and-renderer update is needed to remove that remaining obsolete presentation, without changing dependency data.

The hosted G15 candidate campaign was still running at the inspection checkpoint; no result from it is claimed in this document. Full local `node` graph-rendering, planning-doc checking, script tests and exhaustive automated link checking could not be executed without a local checkout and were not marked PASS. No GitHub Actions were launched.

## Completion boundary

The documented source snapshot and dated historical records remain intact. Documentation corrections were staged on a dedicated branch and do not authorize any merge, gate acceptance, retention operation, environment activation, or release. Any newer main or G15 branch movement must be reconciled before this documentation branch is merged.
