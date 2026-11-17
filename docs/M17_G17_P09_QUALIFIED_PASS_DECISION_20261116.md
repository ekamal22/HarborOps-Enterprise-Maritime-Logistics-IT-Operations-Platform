# G17 P09 Qualified Pass and Release Optimization Decision

**Recorded:** 2026-11-16
**Decision:** **P09 QUALIFIED PASS** for the observability-overhead policy only.
**G17:** **NOT ACCEPTED; acceptance pending.**
**Scope:** P09 performance evidence and timing of optimization. No budget, measurement procedure, source, workflow, milestone dependency or release authorization is changed.

## Evidence behind the qualification

The approved gate remains **at most 10% pooled p95 request-latency overhead for each route** across three matched pairs and **at most 15% mean paired peak-memory overhead**. Raw samples, all pairs and every failure remain on record.

| Complete run | Exact tested source | Authorized pooled p95 | Mean paired peak memory | Original result |
| --- | --- | ---: | ---: | --- |
| f6 | `5b1dcc41d5b19dcbacd66de38193dc5020964647` | +12.71% | +5.86% | FAIL |
| f7 | `5b1dcc41d5b19dcbacd66de38193dc5020964647` | +9.30% | +13.79% | PASS |
| r1c | `8d5e24137ad2943a6b2787b6472e4cea7bd9947e` | +0.76% | +12.53% | PASS |
| r2 | `8d5e24137ad2943a6b2787b6472e4cea7bd9947e` | -0.67% | +13.35% | PASS |
| r3 | `8d5e24137ad2943a6b2787b6472e4cea7bd9947e` | +2.55% | +13.51% | PASS |

Four of five complete P09 runs passed. The f6 authorized-route latency spike is not explained and is **not** discounted, overwritten or relabeled. The three controlled runs used a separate one-stage Tempo verification search-window fix, so their source SHA differs from f6/f7, although the P09 measurement script and rule did not change. A combined five-run estimate is descriptive, not a substitute for any individual run verdict. Memory remains the tighter margin; individual pairs exceeded 15% even when the approved three-pair mean passed.

References: [variance](M17_G17_S25_P09_VARIANCE_ASSESSMENT_20261115.md), [controlled results](M17_G17_S25_P09_CONTROLLED_VERIFICATION_RESULTS_20261115.md), [original raw P09 data and controlled protocol](m17-s25-p09-controlled-protocol-20261115/results/), [mutation audit](M17_G17_S25_MUTATION_SURVIVOR_CLASSIFICATION_20261115.md).

## Binding scheduling decision

1. **No additional P09 campaign merely to obtain another passing outcome.** The documented qualified PASS is acceptable for the P09 policy decision, not a waiver of any numerical budget or independent S25 review.
2. **Optimization timing:** Improve observability latency and memory overhead immediately before G24 release readiness under the existing release-validation work, rather than making optimization a new G17 milestone prerequisite.
3. **Early-failure exception:** If source-locked verification of the actual frozen candidate fails, preserve its full failure evidence, identify the failing requirement, and agree on the remedy together before code changes. Run correctness checks and repeat the applicable full source-locked gate on the corrected SHA. Do not automatically assume an unrelated test failure is P09 or lower performance limits.
4. **Pre-release evidence:** Benchmark a versioned matched telemetry-on/off reference workload against the same 10%/15% limits, disclose outliers and variability, preserve every result, and ensure telemetry privacy, fail-open business behavior, default-off operation, tenant isolation, audit and held M9 transport remain intact. A failed release gate must be remediated before release.
5. **Independent gate boundary:** S25-A01 source/evidence review, S25-A02 policy/procedural attestation and S25-A03 signed acceptance recommendation remain separately required. This P09 decision is not any of those signatures.

## Source-lock and evidence-hygiene constraints

- f6/f7 are source-locked to `5b1dcc41d5b19dcbacd66de38193dc5020964647`; r1c/r2/r3 to `8d5e24137ad2943a6b2787b6472e4cea7bd9947e`.
- The review branch was at `c2eefbfd71b1c228722e99df65afa4f0f09b7a94` at the time of this decision. Later tests, fixtures and runner-list changes require final exact-source verification; old PASS records must not be transferred to a different SHA.
- The independent review identified an internal `diag-state.json` hash-order issue in all three new diagnostic records. See [diagnostic evidence finding](M17_G17_DIAGNOSTIC_HASH_FINDING_20261116.md). Preserve the original files, correct the producer prospectively.
- **G17 NOT ACCEPTED. No main merge, deployment, release decision or reviewer signature follows from this approval.**
