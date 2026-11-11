# G13/G15 Final Raw Evidence Reconciliation

**2026-11-11 G13/G15 source boundary:** Option A authorizes at most 250,000 KPI report members and 1,000,000 active member rows per tenant, subject to four live reports per tenant and two per user. Historical local c3 results at `4e7b9e2` do not prove the revised source and the G13 S25 round 3 recommendation was NOT PASS. Hosted run [43090424080](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43090424080) ended FAILURE, [43091966161](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43091966161) had a failed browser job at inspection, and [43093329982](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43093329982) at `067e1ab57f33fcabefae55727265499d86daf7ae` had a passing browser/crash/preflight and other technical groups while longer groups and its evidence ledger were pending at the recorded check. **G13/G15 acceptance pending.** No main merge, deployment, production activation or retention deletion. The disposable post-c3 Actions verification is the only scope-specific exception to the wider workflow restriction. See [run register](G13_G15_POST_C3_HOSTED_RUN_REGISTER_20261111.md), [remediation](G13_POST_C3_REMEDIATION_20261111.md) and [approved scope](G13_S20_SCOPE_DECISION_20261111.md). Older paragraphs in this document retain their recorded historical source context.

**Reviewed:** 2026-11-10  
**Frozen implementation SHA:** `15cabd8c542159753c927c2bd65b4346223e84f6`  
**Workflow:** [42745334415](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415), completed SUCCESS, 15 of 15 jobs.  
**Formal acceptance:** PENDING the separate S25 and milestone decisions.

## Independent read-back

Each of the 15 retained ZIP archives was opened separately and all 169 manifest-listed SHA-256 file digests were recalculated. No missing files or digest differences were detected. All 14 execution artifacts with `source-sha.txt` declare the frozen SHA. The gate archive contains 8 manifest entries and an `index.json` with the same SHA. All 543 individual TRX test results were parsed: 543 Passed, 0 Failed, 0 Skipped, 0 Other. The 48 technical scenario checks passed. `assertion-audit.json` reports 227 automated assertions with technical evidence present, 7 policy items and 8 independent-review items, with no raw integrity issues. This verifies presence and provenance, not unconditional acceptance of each assertion's semantic breadth.

The previously absent assertions G13-S08-A04, G13-S09-A03 and G13-S09-A04 have exact passed TRX results on the final SHA. The methods are `M10ProjectorAndM11EvaluatorRequireExplicitOptIn` and `LateWorkerEffectDuringRebuildIsNeverLostAndMarksTheProjectionStale`. The P10 outbox failure rollback test also has an exact passed TRX record.

## Direct mutation and crash review

The G13 KPI campaign killed 18 effective variants after a passing clean control. The G13 restricted-role SQL campaign killed 4 variants after a passing clean control. The G15 notification campaign killed 10 variants after a passing clean control. All mutation manifests name the tested SHA, the individual mutant IDs, outcome and test context. Eight process-interruption probes recorded `KILLED_AND_VERIFIED`; post-commit/claim/fanout/response recovery records also have explicit passing recovery outcomes where applicable.

## Performance and role boundaries

The 170,000-shipment report benchmark passed 30 exact-source samples. Its p50 was 11,903 ms, p95 13,000 ms, maximum 13,493 ms. Initial/final report relation allocation was 6,094,848 and 1,600,331,776 bytes; peak working set 662,376,448 bytes. Restricted-application-role EXPLAIN captures exist for empty, small, medium and skewed 170,000-shipment workloads, along with a separately labeled privileged plan. The separate four-authorized-principal reporting campaign passed. Notification scale, 250-additional-recipient two-dispatcher fanout, 100 parallel reader workload and three-engine browser suites passed.

## Limits of the evidence

The current checker verifies each assertion's configured witness set, but some S24 assertions point to overall mutation campaign success rather than a specific required killed mutation. The 48 passing technical scenario contract checks cannot be silently converted into 48 complete gate scenario PASS states. The raw final gate ledger records, separately for G13 and G15, one PASS, 23 BLOCKED and one NOT EXECUTED (S25). Therefore this review rejects an automatic acceptance inference. This distinction is retained in the independent scenario matrix and gate decision.

## Archive manifest

The artifact names, source-lock values, 169 per-file hash checks and test name readback were verified against run `42745334415`. Review input is the retained GitHub Actions artifact set, not a prior summary. Historical failed attempts `42742521530` and `42744037212` remain failures at their own SHAs, both associated with subsequently corrected living-document consistency references. No earlier source receives retroactive PASS from this final run.
