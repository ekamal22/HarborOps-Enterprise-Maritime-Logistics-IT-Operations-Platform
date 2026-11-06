# G10 Independent Raw-Evidence Review

**Review date:** 2026-11-06. **Result:** The submitted raw evidence supports G10 technical verification on frozen runtime `3e81b884b6a4cc782f84c58510793666786c5796`. **G10 acceptance pending.** This is not a gate decision, merge authorization, release or deployment authorization.

**Repository baseline:** `main` at `5edf0035a1fb9d854cba00673eb8fdf1f3033b0c` includes M10 source from merged PR #23 and accepted M0-M9 and M16/G16. G10 corrections and test extensions are retained on `feat/harborops-m10-g16-verify-f80276`; they have not been merged to `main`. Changes after the frozen SHA are tests, the evidence reparse tool and documentation, not runtime code.

## Package integrity and evidence scope

- Inspected `g10-consolidated-evidence-20261106-email.zip`; SHA-256 `07928dcc03011c045c074226b9478d25b31af235cc9d670f40814c446c2d29e0`.
- **500 of 500 files** in `PACKAGE_MANIFEST.sha256` matched their SHA-256 hashes. Original attempt manifests and the included raw stage and test evidence were reconciled.
- The ZIP contains sanitized source-locked evidence for A1 through A7, including stage ledgers, test-result files, browser JSON, mutation results, scan reports, migration outputs and the preserved failed attempts. Disposable credentials and private browser traces are excluded. Email-safe harness names add `.txt` without changing contents.
- The separate original tar archive, `g10-consolidated-evidence-20261106.tar.gz`, remains on the originating E: drive. Its reported SHA-256 is `ac82259a7f65ff0b2ff6e5eabf61482a545e139afc8c12e4a0deff2afc7e1e51`; the **submitted ZIP**, not the separate tar, was independently inspected.

## Reconciled campaign

| Attempt | Evidence and result |
| --- | --- |
| A1 original | 239/239 unit, 39/39 contract, 18/18 web, 302/302 integration twice (second on fresh database), 74/74 M16 audit HTTP, 14/14 audit outage, 27/27 M10 stack; original browser attempt stopped after one WebKit failure in an M6 specification. |
| A2 interrupted | Shutdown interrupted browser supplementation; partial evidence retained, not counted as a completed browser PASS. |
| A3 browser supplement | All three engines passed 66/66 each, 198/198 total. The original failing WebKit case passed once. |
| A4 mutation supplement | Clean control, all 28 mutants killed, zero survivors. |
| A5 S17 | Correction reason and replacement validation, 1/1 final test passed; earlier setup and limiter failures preserved. |
| A6 S05/S11/S12 | DEPA/ARRI parking, archived shipment and suspended organization, 3/3 final targeted tests passed. Two setup failures preserved. |
| A7 S03/S21 | Genuine populated upgrade from pre-M10 11 migrations to frozen M10 12 migrations, with 12 original-table fingerprints retained, plus four after-commit process-termination and recovery scenarios. First harness failure retained and corrected rerun passed. |

The submitted isolated performance evidence measured 12,001 events across 121 pages, p95 page latency 12.6 ms and rebuild 760 ms. Four image scan reports show no HIGH or CRITICAL vulnerabilities. These are measured test results, not production service-level guarantees.

## Five previously disputed scenarios

- **S03 supported:** Pre-M10 source `25e8902` populated an eleven-migration database; the frozen-source migrator upgraded the retained volume to twelve migrations. Four effects backfilled to four timeline entries, with twelve accepted tables' fingerprints unchanged. The populated sample was small; large-volume populated upgrade was not measured.
- **S05 supported:** DEPA and ARRI both remained parked, with no effect, timeline entry, application or input revision change.
- **S11 supported:** Archived shipment M10 reads and writes denied access; history was retained and visible again after restoration.
- **S12 supported:** Suspended organization denied M10 reads and mutations and paused worker/projector work until reactivation. The test accepted several safe refusal status codes rather than one exact HTTP status.
- **S21 supported:** API, worker and projector container SIGKILL after committed operations followed by consistent recovery and replay without duplicates. Host power-loss and PostgreSQL crash recovery were not executed.

No full G10 campaign rerun is justified solely to repeat these supported cases. Preserve original failures and source-locked supplement provenance.

## Policy and gate blockers

**G10-S01 BLOCKED:** Fourteen M10-local policy decisions (M10-L01 through M10-L14) are proposed, not accepted. Two require special attention before approval:

1. **L07 correction auditing:** M10 stores corrections in its own append-only ledger, with no M16 audit row. The accepted [M16-D03](M16_POLICY_DECISIONS.md) and [audit policy](M16_AUDIT_PRIVACY_POLICY.md) require sensitive business mutations and corresponding audit evidence to commit atomically. Explicitly determine whether the M10 ledger is an acceptable scoped equivalent or whether atomic M16 audit capture is mandatory. Do not assume compatibility.
2. **L14 direct database correction grants:** The API database identity has INSERT on `shipment_tracking_corrections`. An allowed scoped direct SQL correction can supply actor identity without the HTTP layer's Manager/session check; the trigger checks scope and generation but not live Manager membership. This is a database-credential threat-model boundary, not proof of remote exploitability. Explicitly approve the trust model or restrict the write path and conduct targeted verification.

**G10-S25 NOT EXECUTED as a complete gate:** This independent raw-evidence review has occurred, but the S01 policy decisions and formal gate acceptance are still pending. The technical tally remains **23 PASS, S01 BLOCKED, S25 NOT EXECUTED**.

**Residual risk:** A1 recorded one failed legacy M6 WebKit browser test; A3 passed the matching case once. The original private trace is not in the sanitized package, so a claimed click-race root cause cannot be independently established here. The 10,000 unresolved-work cap relies on accepted M8/M9 tests, not a new exact-boundary G10 test.

The M9 hold on sustained transport-event processing remains mandatory. M10 does not resolve parked transport traffic, change the unresolved cap or authorize production deployment. Previously accepted G16 evidence is unchanged.

## Next decisions

1. Approve, modify or reject M10-L01 through M10-L14, especially the L07 and L14 security-policy boundaries.
2. If policy decisions require runtime changes, verify only affected areas and source-lock any new evidence.
3. Record a separate explicit G10 gate decision after reviewing remaining risks.
4. Obtain separate authorization before merging the corrected verification branch, publishing or deploying.

See [executed G10 verification](M10_EXECUTED_VERIFICATION.md), [implementation register](M10_IMPLEMENTATION_STATUS.md), [project status](STATUS.md) and [G16 closure](M16_G16_GATE_CLOSURE.md).
