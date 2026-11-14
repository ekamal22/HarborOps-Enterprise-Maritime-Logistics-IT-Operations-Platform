# G17 Source-Locked Technical Verification Green, November 14, 2026

**Executable source:** `f174eae6b2077937c7d43a8b1f7c34ecdab9b663`  
**Actions run:** [#43541279851](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43541279851)  
**Raw artifact:** `g17-raw-evidence-43541279851-1` (GitHub artifact ID `14093266896`)  
**Run conclusion:** SUCCESS. Both source/assertion preflight and full disposable acceptance campaign passed.  
**Gate disposition:** G17 NOT ACCEPTED; independent S25 review pending.

## Exact technical results

| Evidence | Outcome |
| --- | --- |
| Source-lock and negative-control preflight | PASS |
| Complete disposable campaign | 47 / 47 source-locked stages PASS |
| G17 scenario register | 25 scenario IDs represented |
| Technical assertion ledger | 72 / 72 TECHNICAL_PASS; no errors |
| Independent G17-S25 assertions | Three remain INDEPENDENT_REVIEW/PENDING |
| .NET unit | 319 passed, 0 failed, 0 skipped |
| .NET contract | 60 passed, 0 failed, 0 skipped |
| M17 integration | 54 passed, 0 failed, 0 skipped |
| Accepted regressions | 34 passed, 0 failed, 0 skipped |
| Dashboard browser | 24 expected, 0 unexpected, 0 flaky, 0 skipped |
| DB fault, real 5xx and DB alerts, recovery | PASS |
| Collector, network isolation, retention, storage faults | PASS |
| Export privacy | 40 retrieved traces, 80 exported spans, 149 metric series, PASS |
| Malformed-context-only export probe | 960 injected HTTP requests, 24 real exported traces checked, PASS |
| Worker journey | 5 matched trace pairs, 19 retained trace documents, PASS |
| P09 pooled p95 | Baseline +2.47%, authorized +6.78%, both within the 10% pooled rule |
| P09 mean paired peak memory | +2.35%, within 15% budget |

The original P09 per-pair measurements remain in the source-locked raw artifact. One pair exceeded the individual p95 budget, although the approved pooled gate passed. The independent S25 reviewer must explicitly assess the aggregation rule and the retained pair-level spread; the passing pooled result does not conceal an earlier failed attempt.

## Integrity and completeness

The campaign's `run-status.json` is `COMPLETE`, `source_verified=true`, `gate_acceptance=NOT_ACCEPTED`, and `g17_assertion_review=NOT_REVIEWED`. The manifest identifies the executable SHA, 25 scenarios, 75 total assertions, zero reviewed S25 assertions, 45 file entries and no missing required artifacts. All 43 manifest-listed files included in the GitHub artifact download match their recorded byte lengths and SHA-256 hashes. Two hidden Playwright `browser-artifacts/.last-run.json` and `cardinality-artifacts/.last-run.json` metadata files are listed and hashed in the manifest but were not included by the hosted artifact upload. The positive browser and cardinality results and their expected case identity are present. The independent reviewer must consider this packaging limitation explicitly rather than silently claiming 45 downloaded hashes checked.

The feature and verification branch heads currently contain documentation-only descendants of the executable source. No later code or runtime change is represented as part of this passing run. Protected main remains unchanged.

## Acceptance boundary

The successful technical campaign establishes the new baseline for independent G17-S25 review, not G17 approval. S25 must independently assess all 25 scenario requirements and 75 atomic assertion obligations, confirm the evidence and privacy behavior, review P09 policy compliance, resolve any remaining package/behavior questions, and record an explicit PASS or NOT PASS on the three independent review assertions. Only a separate acceptance decision can change the gate status. Older local c7 evidence and all failed GitHub Actions attempts remain untouched and traceable.

[Current project status](STATUS.md) | [S25 readiness](M17_G17_S25_TEST_READINESS_20261113.md) | [Historical recovery](M17_G17_VERIFICATION_RECOVERY_20261113.md) | [Documentation reconciliation](M17_G17_REPOWIDE_DOCUMENTATION_RECONCILIATION_20261114.md)
