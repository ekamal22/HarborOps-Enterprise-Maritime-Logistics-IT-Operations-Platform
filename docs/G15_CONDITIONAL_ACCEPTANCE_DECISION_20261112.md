# G15 conditional acceptance decision, 2026-11-12

## Decision
**CONDITIONALLY ACCEPTED for G15-only source integration.** This is not unconditional G15-S25 acceptance. Formal independent reviewer attestation was not established, and that procedural qualification is recorded explicitly. The direct conditional acceptance and integration authorization was given following review of the completed technical campaign. Previous NOT PASS reviews remain historical source-bound findings and are not rewritten.

## Exact technical evidence
Isolated branch `integration/g15-post-g13-remediation-20261112` was tested at `479616a5b7f17c494d897834a66d10b6276bb517` by [run 43139153300](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43139153300). All 15 jobs succeeded. The evidence ledger recorded 591 passing TRX cases, zero failures, zero skipped tests, 248 automated technical witnesses across G13/G15, and 48 passing technical scenario checks. The reconciliation script returned zero for integrity, scenario mapping, atomic assertion coverage and sensitive-artifact checks. G15 has 111 fully witnessed automated atomic assertions and eight manual policy/review assertions, which are not represented as automated approvals.

The 25 effective G15 source mutants were killed, with the clean control passing. In particular, `outbox-claim-skip-locked-removed` failed the explicit `G15_SKIP_LOCKED_PROGRESS_FAILURE` assertion after both dispatcher operations completed, not a connection cancellation during disposal. The 17-to-18 migration upgrade from the merged G13 baseline passed without changing accepted business rows. Restricted authorization, transaction rollback, crash/recovery, live Chromium/Firefox/WebKit notification journeys, fanout with competing dispatchers, and the one-million-receipt workload passed. At the tested scale, G15 page-load p50 was 27 ms and p95 was 46 ms, not a production SLA.

The recorded SHA-256 evidence manifests for the ledger, mutation logs, browser results, PostgreSQL tests and million-receipt run matched their archived contents. Historical failed attempts remain available on the original development branches and workflow records.

## Operating limits and risks
M15-P01 through P14 are approved with conditions. P10 fail-closed outbox rollback and recovery has passing technical witnesses. P12 retention periods are provisional, **deletion is not authorized**. Dispatch is default-off except disposable test fixtures. Historical notifications remain scoped to recipient, current membership and organization authority. Quarantined-intent operations, stale database statistics, revoked-membership backlog scan costs and production sizing are residual operational work. They are not represented as measured production guarantees.

The M9 ARRI/DEPA processing hold, 10,000 unresolved-work cap, null ETA, M10 projector default-off, M11 evaluator opt-in, immutable source history, tenant isolation and accepted G13 reporting boundaries remain mandatory.

## Merge scope and source identity
Conditional integration may contain only G15 notification implementation, its migration, browser and database tests, direct support tooling and this decision, based on the accepted G13 `main`. The runtime source, test and configuration blobs must match the passing `479616a5b7f17c494d897834a66d10b6276bb517` campaign. Any final documentation-only edits or removal of its temporary source-verification workflow do not constitute a new executable campaign and must be disclosed as such. No additional feature area is included.

**Remaining procedural exception:** independent G15-S25 reviewer identity/independence is not attested; do not claim unconditional acceptance. No production migration, deployment, retention cleanup or automatic background delivery activation is approved.
