# G15 S25 source-locked independent evidence review, 2026-11-12

## Disposition

**Independent technical evidence verification: PASS.**

**Formal G15-S25 unconditional acceptance: PENDING.** The evidence audit was executed separately from the automated S25 ledger, but reviewer separation from the implementation and verification authorship is not attested. The independent reviewer-identity requirement must not be implied by a successful CI run or by this evidence analysis. G15 remains conditionally accepted and integrated under the existing recorded decision.

Do not merge PR #44, enable notification dispatch, deploy or alter retained database state, or authorize physical retention cleanup on the basis of this review. Protected `main` is unchanged.

## Frozen revision and primary evidence

- Repository source: `ebb28d2332ab6e7eb4264265db3ae35e8fbcbc35`.
- Successful [G15 S25 run #43178122017](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43178122017).
- All ten workflow jobs succeeded at the same exact source SHA.
- All ten archived packages were downloaded and rechecked independently of `scripts/verify-g15-s25-remediation.py`.
- All 125 listed SHA-256 entries matched their archived bytes, with no unlisted or duplicate manifest paths.
- The final technical ledger records `TECHNICAL_PACKET_COMPLETE_REVIEW_PENDING`, not an acceptance decision.

| Evidence family | Independent finding |
| --- | --- |
| .NET raw TRX | 534 passing results, zero failed or skipped; 529 distinct result names across all files |
| G15 atomic inventory | 25 scenarios, 119 atomic clauses, 111 automated and eight manual clauses |
| Current-source method IDs | All 68 distinct inventory test methods executed with passing outcomes |
| xUnit parameter cases | All four rule-title, three event-label and eleven session-revocation cases executed and passed |
| Browser | All 14 notification cases passed separately in Chromium, Firefox and WebKit: 42 passing results |
| Browser retry and completeness | Zero skipped, unexpected, flaky or retried cases; no unexpected suite errors |
| Mutations | Passing original-source control; all 25 targeted variants killed by identified behavioral test failures rather than compile failures |
| Crash/restart | Eight killed-and-verified interruption phases; all seven applicable recovery tests passed |
| Fanout | Two dispatchers, 250 additional members and 100 readers with an exact-once result |
| Scale | One million synthetic recipient receipts, bounded unread count, stale-statistics and superseded-membership probes |
| Dependency/secret scan | Zero high/critical advisories within the scanned package scopes; no matched configured committed-secret patterns |

The million-receipt benchmark reported ten measured notification pages with p50 31 ms and p95 37 ms after statistics refresh. Stale-statistics unread-count and unread-list probes returned HTTP 200 in 85 ms and 40 ms. After superseding the membership version, the count and list returned HTTP 200 in 23 ms and 22 ms. These are disposable test observations, not production sizing limits or guarantees.

## Complete scenario review

Each automated inventory clause was reconciled against its mapped source-locked test method, browser case, individual mutant, crash phase, source/build step, job result, scan, or the explicitly labeled historical-source exception. No missing or unrecognized automated witness reference remains in G15-S01 through S24.

| G15 scenarios | Independent assessment |
| --- | --- |
| S01 | Source contracts, role/audit behavior and M13/default-off service evidence PASS. P01-P14 remain approved under their recorded conditions, including P10 and P12 |
| S02-S03 | Immutable history, delivery enablement, neutral notification labels and source-bound tests PASS |
| S04 | Recurrence, distinct occurrence identity, unchanged reevaluation and evidence-history preservation PASS. Three previously absent M11 tests are present in the new source-locked TRX |
| S05-S09 | Audience selection, personal authorization, tenant and session fencing, revocation, deduplication and receipt transitions PASS |
| S10-S13 | Restricted SQL, fanout, concurrent dispatch/read isolation, recovery and migration protection PASS in disposable configurations |
| S14 | Fail-closed M11 outbox capture, audit rollback and affected regression tests PASS. The P10 availability tradeoff remains conditional |
| S15-S20 | Indexed pagination, capped unread count, bound cursors, mutation conflict rules, audit atomicity and persisted source evidence PASS |
| S21 | Cancellation, SQL outage, per-intent dispatcher timeout, retry/quarantine and continued polling PASS with direct behavioral mutation controls |
| S22 | All 14 browser cases PASS in all three engines. Positive pagination, deliberately held loading state and real authorized archived-target detail close the three earlier browser findings |
| S23 | Affected M11/M10/M12 tests PASS at the current SHA. Historical G13 tracking/KPI behavior has an independently checked, narrowly scoped source-equivalence exception |
| S24 | All 25 effective G15 source mutations killed, with no compile-only substitution; recorded dependency checks PASS |
| S25 | The independent evidence analysis is complete, but independent reviewer identity/separation and explicit unconditional acceptance are not established. Formal S25 decision remains PENDING |

## Historical G13 compatibility exception

G15-S23-A03 references `ReplayedEquipmentReceiptsAndParkedTransportNeverInventKpiProgress`, which was not rerun in the current G15 campaign. The historical result is retained, not presented as a current-SHA test.

- Historical [run #43139153300](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43139153300), source `479616a5b7f17c494d897834a66d10b6276bb517`.
- Artifact 13909188656, `g13-tracking-source.trx`.
- Independently recalculated TRX SHA-256: `4ce60864523884bbeeaba58defdd566d430289dc5ed590a92f6323def65ad421`; the named test outcome is Passed.
- All five declared reporting, tracking and acceptance-test blobs are byte-identical between that historical SHA and the current revision.
- New source-locked migration and impacted-regression tests passed for the intervening G15 changes.

**Technical exception decision: SUPPORTED for this one unchanged G13 tracking/KPI requirement only.** It is not proof that the entire accepted G13 campaign ran again, and does not authorize any broader regression exemption.

## Failed attempts and source compatibility

Earlier workflow failures remain failures at their exact source SHAs and are not overwritten by the successful final run. In particular, run #43175855964 failed the evidence-checker self-test fixture, and run #43175941760 rejected the evidence because the checker originally misrecognized parameterized xUnit method names. Those issues were corrected with passing negative controls in the final SHA. Run #43178093053 was successful at an earlier SHA and is not substituted for the authoritative final result.

The application change remains scoped to notification feed query behavior, an additive retained-receipt index migration, guarded migration semantics, affected G15 tests and verification support. The current combined 17-to-19 migration upgrade and M13 independence witnesses passed. No receipt or source-history deletion was authorized. The fixed archived-target test now requires an authorized archived-detail HTTP 200 response, and passed.

## Policy and operational residuals

P01 through P14 had already been conditionally approved. The independent evidence audit does not convert their restrictions into unconditional production permissions:

- **P10:** source-history/outbox capture is fail-closed. Tests prove rollback and retry behavior, but the availability tradeoff remains explicitly conditional.
- **P12:** retention periods are provisional. Physical deletion and cleanup of notification receipts, source history or held records remain unauthorized.
- Delivery is default-off outside explicitly enabled disposable verification fixtures.
- Operational requeue of quarantined intents remains a separate governed procedure, not an automated recovery guarantee.
- The benchmark and crash tests do not establish arbitrary production failure recovery, retained-volume migration safety, or production latency guarantees.
- No Turkish localization integration or final G24 release approval is inferred.

## S25 procedural gate and next action

| Clause | Raw evidence assessment | Formal S25 status |
| --- | --- | --- |
| G15-S25-A01: source SHA and artifact digests | Verified directly | Reviewer separation not attested |
| G15-S25-A02: all preceding scenario assertions | 111 automated references reconciled and technical policy limitations recorded | Reviewer separation not attested |
| G15-S25-A03: failures, migration, M13 | Historical failures preserved, changed-source migration verified, M13 and G13 constraints checked | Reviewer separation not attested |
| G15-S25-A04: explicit acceptance decision | Technical evidence PASS, risks qualified | Unconditional acceptance PENDING |

**Final disposition: TECHNICAL VERIFICATION PASS; FORMAL S25 UNCONDITIONAL ACCEPTANCE NOT YET PASS.** A reviewer whose independence from implementation can be attested must sign the exact-SHA evidence and record the explicit acceptance decision. Nothing in this record modifies the existing conditional G15 integration or the protected branch policy.

The report is documentation-only and was prepared on a separate review branch from the already verified SHA. No new technical execution is claimed for the documentation revision.
