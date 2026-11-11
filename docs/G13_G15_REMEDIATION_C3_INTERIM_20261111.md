# G13 and G15 Round 2 Remediation and Campaign c3, Interim Record, 2026-11-11

**Status:** Interim. Campaign c3 is not complete: one job failed and has not been rerun. Independent S25 round 3 has not been performed. G13 and G15 acceptance decisions: **acceptance pending**. This record is not a gate decision, not a merge authorization and not a GitHub Actions result.

**Frozen source:** `4e7b9e22aacef25f7f60e2295938ec6cf79bdadd` on `feature/g13-g15-acceptance-closure-20261110` and `verify/g13-g15-evidence-closure-20261109`. It supersedes `3e63bd0` (campaign c2, [results](G13_G15_LOCAL_CLOSURE_RESULTS_C2_20261111.md)), which round 2 reviewed with NOT PASS for both gates ([G13](G13_S25_INDEPENDENT_REVIEW_ROUND2_20261111.md), [G15](G15_S25_INDEPENDENT_REVIEW_ROUND2_20261111.md)). Commits after `4e7b9e2` must change documentation only.

## What changed since `3e63bd0`

| Area | Change |
| --- | --- |
| G13 admission | Early per-tenant admission: a build takes a build slot only if the tenant still has capacity (four live snapshots minus those already committed). Every other builder is refused at once with 429 `report_tenant_capacity_exceeded`, before the heavy member statement. The authoritative late quota check under the tenant lock remains. New function `m13_tenant_live_reports`. Limits of four per tenant and two per user are unchanged |
| G13 measured | Realistic skewed 85,000 shipment tenant, eight distinct builders: four complete (17.7 to 19.6 s), four are refused in about 123 ms |
| G13 witnesses | Direct privileged-reader isolation across two tenants; six users at once; quota lock order; client cancellation releases the slot; late quota decision under a stale early check; planner and timeout settings checked on a pooled, no-reset host; first recorded event semantics; completed shipment without a proven plan; closed voyage assignment; UTC day and week buckets under non-UTC session time zones; full role matrix including Support; foreign and missing tenants answered identically; no-store, CSRF and same-origin; log hygiene; content security policy with an engine control |
| G15 dispatcher | Each intent is delivered by its own statement with its own limit (`M15:DeliveryEventTimeoutSeconds`, default 15 s), applied by the application and not by a database role setting. A poll continues past a timed-out intent. The function reports intents claimed through a transaction-local setting |
| G15 unread count | Exact up to 1,000 and flagged as capped beyond it, covered at 999, 1000, 1001 and at 1,000,000 rows; the browser shows `1000+` and never a plus for an exact count |
| G15 witnesses | Both dispatchers claim work; a second dispatcher skips a locked intent; different-user HTTP readers see only their own receipts; default-off wiring on a started host; the downgrade guard; the archived-target negative branch; fanout eligibility for inactive users and the closed role set; KPI separation from notifications |
| Web | The workflow distribution bar is drawn as SVG; a source guard test forbids inline styles. WebKit raises `style-src-attr` events for a bare native `<select>`; the browser assertion accepts that only when a control page reproduces it in the same run |
| Mutants | G13 41 (12 new), security 4, G15 25 (8 new). A dev run found three G13 survivors; two were retargeted to the new late quota test and one to the pooled planner test, and all three now die |
| Assertions | Clause-level mapping: every automated assertion lists only the witnesses that prove its own clause (tests, named job steps, named mutants, named browser cases). Six assertions were added; the inventory is 263 |
| Operations | [Quarantine recovery procedure and reporting capacity limits](OPERATIONS.md) |

## Campaign c3 on `4e7b9e2` (local mode, not GitHub Actions)

| Result | Detail |
| --- | --- |
| Passed, 0 failed commands | source (including the web inline style guard), dependency scan, security mutations 4 of 4, G13 mutations 41 of 41, regressions, G13 faults, database, browser (all three engines on the first attempt), scale, G15 scale, fan-out, concurrency, crashes |
| TRX | 571 passed, 0 failed, 0 skipped |
| **Failed** | G15 mutations: mutant `outbox-claim-skip-locked-removed` timed out after 400 s and was recorded INVALID. The other 24 mutants were killed and the clean control passed |

The mutant removes `SKIP LOCKED` from the claim query. It was killed in dev, and in three of four manual reruns, but in one rerun and in the campaign the test did not finish. A hung run showed a `harborops_api` session idle in a transaction after its call returned. Twenty further runs with tracing did not reproduce it. The cause is unknown. It appeared when three mutation jobs ran at once. Planned next step: rerun the G15 mutation job alone at the same source, keep the failed attempt in `superseded`, rerun the ledger. If it hangs again it is treated as a test defect, which changes the source and requires a new freeze and campaign.

## Not done yet

The c3 ledger passes only after that rerun; no packet or archive exists for c3; the G13-S20 scope decision for your approval (supported workloads versus intentional 413 refusals, ceiling unchanged at 500,000, the realistic 170,000 shipment requirement not claimed as met) is not written; fresh independent S25 reviews have not been requested.

## Residual risks that remain genuine

The scanned-row bound of the unread count is reasoned, not measured, for a backlog the user can no longer see. The 413 refusal of an oversized tenant still costs about 19 seconds per builder. A request cancelled during commit can briefly strand a snapshot. Some crash windows remain surrogates (raw administrator statements or hand-signed scopes). Fixture realism has no M10 equipment conflicts or corrections in the large tenants (the reference tests cover them).
