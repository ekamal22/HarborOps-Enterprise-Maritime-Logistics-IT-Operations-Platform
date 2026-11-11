# G13-S20 Scope Decision, 2026-11-11

**2026-11-11 G13/G15 source boundary:** Option A authorizes at most 250,000 KPI report members and 1,000,000 active member rows per tenant, subject to four live reports per tenant and two per user. Historical local c3 results at `4e7b9e2` do not prove the revised source and the G13 S25 round 3 recommendation was NOT PASS. Hosted run [43090424080](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43090424080) ended FAILURE, [43091966161](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43091966161) had a failed browser job at inspection, and [43093329982](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43093329982) at `067e1ab57f33fcabefae55727265499d86daf7ae` had a passing browser/crash/preflight and other technical groups while longer groups and its evidence ledger were pending at the recorded check. **G13/G15 acceptance pending.** No main merge, deployment, production activation or retention deletion. The disposable post-c3 Actions verification is the only scope-specific exception to the wider workflow restriction. See [run register](G13_G15_POST_C3_HOSTED_RUN_REGISTER_20261111.md), [remediation](G13_POST_C3_REMEDIATION_20261111.md) and [approved scope](G13_S20_SCOPE_DECISION_20261111.md). Older paragraphs in this document retain their recorded historical source context.

**Effective decision, 2026-11-11: APPROVED.** Option A is adopted with a stricter **250,000-member ceiling per report**. The existing four-live-reports-per-tenant and two-live-reports-per-user limits are unchanged. Aggregate live-member capacity is therefore 1,000,000 per tenant. A report exceeding 250,000 members must fail closed with HTTP 413 without publishing partial results. Larger tenants may generate multiple independently filtered reports, subject to the normal report quotas and 10-minute expiry. No automatic report stitching or combined-report accuracy guarantee is implied.

A realistic shipment mix previously produced about 5.5 KPI members per shipment. That suggests around 45,000 realistic shipments per report, but this is an estimate, not a fixed shipment quota. The replacement source targets a 40,000-shipment realistic success fixture and an 85,000-shipment simple reference fixture. Neither has completed a new source-locked campaign yet. The original 170,000 realistic-shipment single-report requirement is explicitly superseded, not claimed as fulfilled.

**Release status:** Scope policy approved; implementation and fresh technical verification pending; G13 acceptance and merging remain pending. The campaign c3 results below document historical source `4e7b9e2` under the former 500,000-member ceiling, not the revised implementation.

## Historical c3 proposal and measured evidence

**Historical status at the c3 proposal:** Approval had not yet been recorded. The original G13-S20 requirement, a realistic skewed tenant of **170,000 shipments** served under the approved latency budgets, is **not satisfied** by the current source and this record does not claim it is. The 500,000-member report ceiling is unchanged.

Measured at source `4e7b9e22aacef25f7f60e2295938ec6cf79bdadd`, campaign c3 ([results](G13_G15_LOCAL_CLOSURE_RESULTS_C3_20261111.md)), restricted API role, disposable PostgreSQL 16.15, approved budgets p50 15 s and p95 20 s.

## What the evidence shows

| Workload | Members | Result |
| --- | ---: | --- |
| Reference 170,000 shipments, Draft and Cancelled only, no history | 450,006 | Served. p50 8,988 ms, p95 9,670 ms (30 samples) |
| Realistic skewed 85,000 shipments (history, evaluations, exceptions, archived rows) | 466,448 (93 percent of the ceiling) | Served. p50 8,293 ms, p95 8,821 ms, max 9,241 ms. Four simultaneous builders: all HTTP 200 in 18.2 s. Eight distinct builders: four HTTP 200 in 15.0 to 16.3 s, four HTTP 429 in 122 ms |
| Realistic skewed 170,000 shipments | more than 500,000 | **Refused** by design with HTTP 413 `report_capacity_exceeded`, in 19.9 to 20.1 s per builder |

A realistic shipment produces about 5.5 report members, so the ceiling serves up to about **91,000** realistic shipments. The reference load produces about 2.6 members per shipment and fits to about 190,000.

## Proposed scope, for approval

**Supported:** any tenant whose report fits in 500,000 members. Verified points: 170,000 simple (Draft and Cancelled) shipments and 85,000 realistic shipments, four simultaneous builders, the approved budgets met, no 503. In practice, with a realistic mix, **about 90,000 shipments per tenant**.

**Intentionally refused:** a tenant above 500,000 members, for example a realistic 170,000 shipment tenant. It receives HTTP 413 `report_capacity_exceeded` with the message "Report is too large. Narrow the filters", for every builder, never a timeout. The user narrows the filters (period, origin, destination, voyage, record source) to fit. The refusal currently costs about 20 seconds of work.

**Concurrency:** at most four reports per tenant at once and two per user; extra builders receive an immediate 429.

## Options

| Option | What it means | Effort and risk |
| --- | --- | --- |
| A. Accept the scope above | G13-S20 is closed for tenants up to about 90,000 realistic shipments; larger tenants must narrow filters. Update the plan text so S20 names this scope | None. Recommended for this gate |
| B. A plus a cheap refusal | Keep the ceiling; count members before the heavy statement so an oversized tenant is refused in well under a second | A small change, but it needs a new frozen source and campaign |
| C. Raise the ceiling to serve realistic 170,000 | About 0.93 million members; materialization time scales roughly with members, so a single build would need about 17 s and four simultaneous builds more than 30 s, which breaks the 28 s envelope and the 20 s p95 | Real engineering (incremental materialization, indexes or a different storage design) and full re-verification. Not recommended inside this gate |

## Recommendation

A for this gate, with B recorded as a follow-up, and C only if the product genuinely needs full realistic 170,000 shipment reports. Reviewers have already noted the 93 percent fill of the 85,000 shipment test, so a tenant of 90,000 realistic shipments is near the edge.

## Decision requested

Please state one of: **A**, **A with B scheduled**, or **C**. Until then G13-S20 is reported as supported for the scope above only, and **not** as satisfying the original 170,000 realistic shipment requirement.
