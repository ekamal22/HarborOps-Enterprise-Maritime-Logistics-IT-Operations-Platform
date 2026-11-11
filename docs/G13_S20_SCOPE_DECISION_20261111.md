# G13-S20 Scope Decision, Prepared for Owner Approval, 2026-11-11

**Status:** Awaiting the owner's decision. Nothing here is approved. The original G13-S20 requirement, a realistic skewed tenant of **170,000 shipments** served under the approved latency budgets, is **not satisfied** by the current source and this record does not claim it is. The 500,000-member report ceiling is unchanged.

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
