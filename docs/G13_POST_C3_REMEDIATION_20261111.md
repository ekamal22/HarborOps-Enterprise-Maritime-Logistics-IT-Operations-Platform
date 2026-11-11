# G13 post-c3 remediation status, 2026-11-11

**Live hosted verification checkpoint, 2026-11-11:** [Run 43090424080](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43090424080) completed FAILURE at `30fd28f6`. [Run 43091966161](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43091966161) has a browser failure and one pending mutation group at inspection. [Run 43093329982](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43093329982) at `067e1ab57f33fcabefae55727265499d86daf7ae` passed preflight, browser, crash, G13 concurrency/fault, G15 scale/fanout and security groups, while four technical jobs and final packet reconciliation remained pending at inspection. See [run register](G13_G15_POST_C3_HOSTED_RUN_REGISTER_20261111.md) for exact source and per-job evidence. **Acceptance pending.** This newer snapshot supersedes this page's original implementation-only status, while the underlying implementation and follow-up sections remain applicable.


**Disposable browser verification:** The source-locked post-c3 GitHub Actions campaign may enable M15 delivery only inside its isolated temporary browser fixture. The workflow's unique Compose project, teardown trap, generated test credentials and preflight dependency are enforced by the M15 contract check. Application and production defaults remain off. This is not production activation or permission to delete notification data.


**Verification authorization, 2026-11-11:** A single source-locked GitHub Actions preflight and conditional combined G13/G15 test packet on the post-c3 remediation branch are authorized. The combined technical jobs may run only if the preflight succeeds. This does not authorize acceptance, merging, production deployment, background feature activation, unrelated Actions workflows or retention deletion. This new authorization supersedes only the earlier general Actions prohibition for this narrowly scoped campaign.


**Status at first draft:** Implementation and regression coverage in progress. That no-campaign checkpoint is superseded by the hosted run register above. G13 NOT ACCEPTED; G15 NOT ACCEPTED; no merge or deployment authority.

**Baseline:** c3 tested source `4e7b9e22aacef25f7f60e2295938ec6cf79bdadd`. Round 3 G13 S25 returned NOT PASS. The original evidence and failed attempts remain preserved. This change set is isolated on `feature/g13-post-c3-remediation-20261111` and must not be treated as c3-tested source.

## Approved G13-S20 scope

Option A approved with a stricter 250,000-member per-report ceiling. The tenant holds at most four live reports, and a user at most two. Aggregate live-member ceiling is 1,000,000 per tenant. Above-limit reports return HTTP 413 and never commit a partial result. Larger data sets may be covered by independently filtered reports, without automatic aggregation or stitching. Approximately 45,000 realistic shipments per report is an estimate based on one historical fixture, not a shipment limit. The 170,000-realistic-shipment unrestricted report requirement is superseded.

The replacement scale fixtures target 85,000 simple reference shipments and 40,000 realistic shipments for success, and 170,000 realistic shipments for over-capacity refusal. The latency and request-envelope criteria remain unchanged. These fixture sizes were subsequently exercised in the hosted campaigns. The newest run's complete source-locked results were still pending at the recorded inspection.

## Source changes to validate

- Coordinated early admission under the tenant quota advisory lock now counts all four build-slot locks together with committed live snapshots. A build slot remains held until the report commits, at which point it is released under the same quota lock. The late quota check remains authoritative.
- A deterministic admission regression occupies non-positional slots while two reports are stored, proving that the next builder is refused before materialization.
- The session-log test captures a real Set-Cookie value from the authenticated login response rather than reading the usually empty default Cookie request-header collection.
- The privileged live-report reader has explicit authority, tenant isolation, expiry and excluded-report tests, plus three mutation variants.
- Cleanup evidence preservation checks now include M10 projection and correction, M11 evaluation/exception/history and M12/M16 audit rows, in addition to M5/M6 records.
- Reporting limits, reference scale fixtures, mutation witness names, scenario checks and inventory references were adjusted to the new report ceiling.

## Evidence still required

1. A warning-free .NET build and all targeted database and integration tests on the new source. This was the first-draft local environment limitation. Later hosted runs executed the new tests at their own exact source SHAs; review the run register for their outcomes.
2. A fully passing source-locked acceptance campaign and final evidence ledger, including all G13/G15 regressions, current mutation variants, 250k capacity, browser tests, storage and concurrency measurements.
3. Direct evidence for genuine API mid-build process termination and interrupted cleanup, or explicit partial classification without claiming the original clauses are proven.
4. A real serialization-conflict witness or a revised approved requirement that reflects the READ COMMITTED transaction model. The prior serialization retry was not demonstrated.
5. PostgreSQL server-side CPU and memory, temporary-file and WAL/write-amplification measurements for G13-S20-A04, rather than only the application test process. That assertion remains PARTIALLY_TESTED.
6. Exact M12/M16 and hosted CSP clause coverage where the c3 review found only whole-job or non-hosted witnesses.
7. Updated S25 findings and independent reviews after the new evidence is complete. The G15 round 3 decision is not inferred from the G13 review.

## Safeguards

Only the expressly approved disposable post-c3 GitHub Actions campaign is authorized. Do not activate report cleanup, notifications, transport processing, projectors, evaluators or deletion jobs. Preserve tenant isolation, restricted database roles, 15-second ordinary statements, scoped 25-second member materialization, 28-second request envelope, ten-minute snapshot expiry, four-report tenant cap and two-report per-user cap. Preserve all prior passing and failed evidence.

No new acceptance, main merge, Turkish localization integration or deployment is implied by these changes.
