# G15-S25 post-merge evidence review, 2026-11-12

## Disposition

**G15-S25 recommendation: NOT PASS.**

The exact-source technical campaign passed and the current protected-main implementation matches the tested implementation. However, three browser-level atomic clauses are not completely witnessed by the current source-locked browser suite, and an independent S25 reviewer attestation has not been established. The automated assertion ledger must not be treated as final independent scenario acceptance.

**G15 source integration remains conditionally accepted and merged under the existing explicit exception.** This finding neither rewrites the past technical results nor grants unconditional acceptance, deployment, notification retention deletion or dispatcher activation.

## Source and evidence examined

- Frozen implementation SHA: `479616a5b7f17c494d897834a66d10b6276bb517`.
- Completed [source-locked run 43139153300](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43139153300): 15 of 15 jobs succeeded.
- Protected `main` at review: `0af0a6212e74b9206902024d5d9a9a222df2a9ab`, following G15 merge PR #41 and documentation PR #42.
- Tree comparison: all 346 tracked `src/`, `tests/`, `web/`, `compose.yaml` and `.env.example` blobs checked are identical between the verified source and current `main`. This is content equivalence, not a fresh executable run at the merge SHA.
- The retained evidence packet contains 34 TRX files with 591 passing test outcomes, zero failures or skipped outcomes, and 586 unique test names. Fifteen artifact-local SHA-256 manifests contain 265 file digest entries; every referenced file digest matched on read-back.
- G15 mutation manifest: passing clean control and 25 of 25 effective mutants killed. The removed `SKIP LOCKED` mutation failed the named `G15_SKIP_LOCKED_PROGRESS_FAILURE` assertion, after both dispatchers drained. Its preceding timeout-only attempt remains historical failure evidence.
- Three-browser suite: 11 notification tests passed on each of Chromium, Firefox and WebKit. The live M11-to-notification navigation test used the real API; remaining browser scenarios use targeted fixture routes.
- G15 volume: 1,000,000 recipient receipt rows, 10 pages and 500 unique members; page p50 27 ms, p95 46 ms, unread-count response 64 ms, Unread 25 ms, Read 117 ms, Acknowledged 113 ms. These are disposable test measurements after `ANALYZE`, not production service-level guarantees.
- Concurrent fanout: 250 additional recipients, two dispatchers, 100 readers, bounded 32 concurrent database readers, exact-once result.
- Crash evidence: six G15 process interruption phases with verified retained-state outcomes and applicable passing recovery checks.
- The ledger marks 111 G15 automated clauses technically witnessed and eight G15 policy/review clauses for manual disposition. That automated classification is challenged by the exact clause gaps below.

## Previous review findings checked

| Earlier issue | Current evidence | Review assessment |
| --- | --- | --- |
| Exact/capped unread count, including 1,000 versus 1,001 | HTTP test covers 999, 1,000, 1,001 and 1,200; browser verifies capped and exact labels; three cap mutants killed | Technically closed |
| Per-intent dispatcher timeout was not configured for the shipped service | `NotificationStore.DeliverOneAsync` sets `SET LOCAL statement_timeout` for each one-intent transaction; two slow intents and a healthy intent pass; timeout mutants killed | Technically closed |
| Unproven competing dispatcher claims and reader isolation | Both dispatchers must deliver work; an Analyst reads over HTTP while an Administrator is denied; `SKIP LOCKED` has a behavior-linked killed mutant | Technically closed |
| Default-off hosted services | Started-host test confirms delivery and cleanup absent by default and individually enabled only through explicit settings | Technically closed |
| Archived-target negative permission branch | API tests verify Manager access to archived details and denial without permission | Backend covered; browser-specific gap below |
| Source-history uniqueness, audit rollback, restricted SQL, and upgraded migration ordering | Dedicated targeted tests, mutants and populated 17-to-18 migration checks passed | Technically closed |

## Findings

### M1: G15-S22-A02 lacks a positive browser pagination witness

The requirement includes successful notification cursor continuation in the browser. The current `tests/browser/specs/notifications.spec.mjs` suite contains a cancelled pagination test that checks whether the Load more control becomes usable again, but does not exercise a successful second page appended to the visible feed. API and SQL keyset tests prove backend pagination, not the browser continuation clause. The inventory classifies A02 as `FULLY_TESTED` using notification display and personal-state browser cases that do not perform that positive continuation.

**Closure:** add a browser case that returns at least two pages, selects Load more, verifies the second page is appended once in stable order, and confirms no cross-tenant or stale-page leakage. Run that targeted case in Chromium, Firefox and WebKit at an exact implementation SHA.

### M2: G15-S22-A04 lacks an authorized archived-target browser witness

The requirement includes authorized navigation to an archived shipment through a historical notification. The live browser journey covers an active target; the unavailable-target browser case simulates HTTP 404. Neither uses an authorized archived shipment. The integration tests `CancelledAndArchivedTargetsKeepThePersonalEventAndGrantNothingNew` and `ArchivedShipmentKeepsHistoricalPersonalEventWithoutNewBusinessAuthority` establish relevant backend behavior, but do not establish the browser navigation clause.

**Closure:** test clicking a historical alert whose archived shipment remains authorized, verify the archived detail is shown without granting mutation authority, and retain the separate unavailable/unauthorized negative case.

### M3: G15-S22-A03 does not explicitly exercise the loading-state UI

The component renders a loading status while a notification request is pending, but none of the 11 browser test cases asserts that status during a deliberately held request and its removal on completion. The mapped empty/error tests are useful, but do not establish the distinct loading-state requirement.

**Closure:** hold the notification response and assert the loading status, then release it and verify the final safe state. This can be combined with the successful pagination test if both states are explicitly checked.

### P1: Independent S25 review attestation not established

G15-S25-A01 through A04 require an independent reviewer who validates the exact source, artifacts and scenario outcomes before the acceptance decision. Historical independent review round 2 gave NOT PASS at earlier source `3e63bd0e71bdf4a80cd8ea1d27e7cceca3c37524`. The current verification campaign supplies technical evidence, not an independently attested S25 decision. This review reconciles raw artifacts and source but does not claim an independently appointed or otherwise independently attested reviewer.

**Closure:** have a reviewer independent of implementation confirm the final source, raw evidence, per-assertion coverage and the corrected browser witnesses, then record their identity/attestation and explicit PASS or NOT PASS.

## Residual operational risks, not independently closed by CI

- The 1,000,000-row benchmark tested a recipient with visible rows after refreshed PostgreSQL statistics. A large revoked or superseded-membership backlog and stale or delayed autovacuum statistics have not been bounded by this result.
- Receipt and intent quarantine is visible to logs and status counts, but a governed operator requeue and remediation procedure remains incomplete. It must not be improvised in a retained environment.
- Some crash windows are exercised through disposable test-host/database surrogates; these are not proof of arbitrary production-process failures.
- M15-P10 outbox capture deliberately rolls back source history when capture fails. The availability tradeoff remains explicitly conditional.
- M15-P12 permits provisional retention periods only. Physical retention deletion, source-history cleanup and held-record deletion remain unauthorized.
- The in-place predecessor migration history was safe for this source integration only where the older M15 migration was not applied in a retained database. No production upgrade or rollback is authorized by this review.

## Review coverage by scenario family

| Scenarios | Evidence assessment |
| --- | --- |
| G15-S01 | Policy register approved subject to P10 availability condition and P12 no-deletion restriction |
| G15-S02 through S09 | Technical evidence and negative controls support the mapped behavior |
| G15-S10 through S16 | Backend authorization, deduplication, concurrency, crash and migration evidence present; surrogate/operational caveats retained |
| G15-S17 through S21 | Source-locked runtime tests and mutants support the bounded contracts in the disposable configurations |
| G15-S22 | **NOT PASS** for three incompletely witnessed browser clauses, M1 through M3 |
| G15-S23 and S24 | Source regression and mutation evidence supports the mapped technical clauses |
| G15-S25 | **NOT PASS** because independent review attestation and complete preceding browser evidence are absent |

## Required disposition

**Current technical campaign: PASS at `479616a5b7f17c494d897834a66d10b6276bb517`.**

**Full G15-S25 acceptance: NOT PASS.** This is an evidence-coverage and reviewer-attestation decision, not a finding that G15 runtime tests failed. Do not promote the existing conditional integration to unconditional acceptance. Preserve merged `main`, the prior review findings, failed campaign records and all archived evidence.

The next remediation should be limited to the three G15 browser acceptance clauses and an independent source-locked review. No additional G13 acceptance, mutation or performance campaign is required by these findings. Do not change acceptance conditions merely by changing the ledger classification.
