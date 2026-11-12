# G15 S25 post-merge acceptance reconciliation, 2026-11-12

## Current disposition

**G15 and G15-S25: ACCEPTED and integrated into protected `main` on 2026-11-12.**

- Final merge: [PR #44](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/44), commit `49934d793542ef43cfe587c54b16cfd5cce8e2f4`.
- Explicit [G15-S25 acceptance decision](G15_S25_FINAL_ACCEPTANCE_20261112.md) precedes this merge.
- Tested executable source: `ebb28d2332ab6e7eb4264265db3ae35e8fbcbc35`, successful [run #43178122017](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43178122017), 10 of 10 jobs PASS.
- [Raw-evidence review](G15_S25_INDEPENDENT_EVIDENCE_REVIEW_20261112.md) independently verified the underlying artifact files, test outcomes, browser checks, crash recovery and mutations rather than relying on the automated gate status.
- [PR #46](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/46) integrated the source-locked evidence review before PR #44 merged.
- Historical conditional integration [PR #41](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/41) remains part of the history and is not overwritten.

## Merge-source equivalence

The exact tested source `ebb28d2332ab6e7eb4264265db3ae35e8fbcbc35` is an ancestor of the merged `main` commit. The GitHub commit-tree comparison found that all changed paths from that tested SHA to `49934d793542ef43cfe587c54b16cfd5cce8e2f4` are documentation only:

`README.md`, `docs/ACCEPTANCE_CRITERIA.md`, `docs/G15_S25_EVIDENCE_REAUDIT_20261112.md`, `docs/G15_S25_FINAL_ACCEPTANCE_20261112.md`, `docs/G15_S25_INDEPENDENT_EVIDENCE_REVIEW_20261112.md`, `docs/G15_S25_POSTMERGE_REVIEW_20261112.md`, `docs/M15_IMPLEMENTATION_STATUS.md`, `docs/M15_VERIFICATION_PLAN.md`, `docs/RELEASE_READINESS_TODO.md`, `docs/ROADMAP.md`, `docs/STATUS.md` and `docs/TEST_STRATEGY.md`.

Thus the merged `src/`, `tests/`, `web/`, schema/migrations, verification scripts and source-bound configuration match the successful run exactly. **No new test execution at merge SHA is claimed.** This reconciliation is based on source equivalence, not an invented post-merge test run. This post-merge document is a later documentation-only update.

## Acceptance evidence

- 119 G15 clauses within 25 scenarios: 111 automated assertions with technical witnesses and eight explicit policy/review decisions.
- 68 current-source distinct test methods, including 18 separate parameterized xUnit cases.
- 534 passing .NET TRX results and 42 passing browser results across Chromium, Firefox and WebKit, all from the same SHA.
- Passing original-source mutation control and 25 effective mutants killed by named behavioral failures, not compiler failures.
- Eight verified process-interruption phases and seven applicable passing recovery tests.
- Successful million-receipt synthetic test with stale-statistics and revoked-membership probes, concurrent dispatcher and reader tests, source-locked migration upgrades and affected M11 regression tests.
- Ten archived evidence packages with 125 valid manifest digest entries.
- Historical G13-S23 compatibility exception accepted narrowly on the matching historical test digest and five byte-identical source/test blobs, without rerunning accepted general G13 verification.

## Procedural qualification

The automated source-locked technical packet and separately inspected raw evidence justify the recorded technical PASS. A reviewer identity independent of implementation was not attested. The explicit G15 acceptance decision discloses and accepts that procedural exception; this record does not claim an external independent attestation. Earlier source-bound NOT PASS reviews, failed runs and reviewer warnings remain historical evidence, not retroactively changed.

## Operational boundaries unchanged

- M15-P10 remains fail-closed: notification outbox capture failure rolls back the relevant source-history transaction. The availability tradeoff is accepted and remains in force.
- M15-P12 retention periods remain provisional. Physical notification, immutable history, audit or held-record deletion is **not authorized**.
- Background notification delivery remains disabled by default. No production feature enablement, deployment, live schema migration, requeue, purge or release is authorized by acceptance alone.
- Accepted G13 reporting isolation remains unchanged. M9 transport restrictions, M10 projector opt-in, M11 evaluator opt-in and protected tenant/recipient scopes remain binding.
- Turkish localization integration is a later, separate workstream. It is not included in this merge or acceptance.
- G24 complete-product release acceptance remains open.

## Repository-wide documentation convention

The final decision is authoritative for current G15 state. Historical G15 conditional decisions, partial verification plans and earlier failed S25 reviews must retain their original source and outcome with clear historical labels. Status, roadmap, security, API, release and M15 planning documents should refer to this record rather than treating the previous pending checks as present-day blockers.

**Result: G15 ACCEPTED, merged into `main`, with disclosed reviewer-separation exception and all operational safeguards retained.**
