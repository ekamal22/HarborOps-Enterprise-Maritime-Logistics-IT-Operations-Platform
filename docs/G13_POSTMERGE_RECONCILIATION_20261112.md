# G13 conditional post-merge reconciliation, 2026-11-12

**Later G15 status, 2026-11-12:** This is a historical G13-specific decision/reconciliation record; it remains valid for G13's conditional acceptance. G15 was subsequently accepted and merged through PR #44 at `49934d793542ef43cfe587c54b16cfd5cce8e2f4`. [Final G15-S25 acceptance](G15_S25_FINAL_ACCEPTANCE_20261112.md) and [post-merge reconciliation](G15_S25_POSTMERGE_ACCEPTANCE_RECONCILIATION_20261112.md).

## Integration decision
G13 is conditionally accepted and merged into protected `main` through [PR #38](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/38), merge commit `b2856449c8d1ab629c8b5419a5af0ebddafbb47d`. The [G13 decision](G13_S25_CONDITIONAL_ACCEPTANCE_DECISION_20261112.md) explicitly records that formal reviewer independence was not attested. This exception remains in force.

## Exact verification
Combined source `c6d2dc86e30913b8d67a011131f9980422e4c197` passed [run 43122895445](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43122895445): 15 jobs, 590 passing TRX cases, 248/248 automated technical witnesses and 48/48 technical scenario checks. The independent-review gate is not automatically closed by technical results.

G13-only source `f4881284a859ad20325dfafd3d15b715143e76b1` passed [run 43133896612](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43133896612): 311 unit tests, 52 contract tests, 18 frontend tests, 54 selected PostgreSQL integration tests, the web build and warning-free .NET compilation. The populated 17-migration upgrade and restricted SQL checks passed. Archived source and database artifacts identify that exact SHA.

Commit `6479e803c41cf52a37d02e0ed849a134334292b3` removed only the temporary G13 verification workflow after those runs. The final merge tree contains G13 and excludes all M15 notification implementation. Historical failed runs and their identities are preserved.

## Limits and follow-up
Approved G13 limits: 250,000 member rows per report, four live reports per tenant, two per user, and 1,000,000 live member rows per tenant. Default-off report cleanup remains disabled. Performance measurements are not a production SLA.

G15 remains NOT ACCEPTED. It requires a dedicated source branch reconciled with the merged G13 baseline, precise `SKIP LOCKED` behavioral mutation proof, remaining performance-risk review, its own source-locked campaign, and separate S25 disposition before merge. Notification retention deletion and deployment remain prohibited.

## Documentation and graph
Living status/navigation documents have been reconciled. The generated execution graph remains a prerequisite plan and does not convey acceptance by node presence. No dependency topology or historical evidence is rewritten.
