# G15-S25 Independent Evidence Review

**Review date:** 2026-11-10
**Review disposition:** NOT PASS pending final corrected exact-source evidence reconciliation and separate gate acceptance.
**Previously verified implementation SHA:** `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75`.
**Final closure candidate:** `15cabd8c542159753c927c2bd65b4346223e84f6`.

## Direct read-back of retained evidence

The preceding [15-job campaign](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) yielded source-matched SHA-256 ZIP manifests, 541 passing TRX outcomes, zero failures or skips, 48 passing G13/G15 technical scenario contract checks, and 224 of 227 mapped technical assertion witnesses. The retained raw material includes live restricted-role PostgreSQL checks, three browser engines, ten killed notification mutations with a passing control, one-million-row scoped feeds, dual dispatcher fanout, and eight actual process-interruption phases. These old results are historical; the new exact-source campaign must be examined separately.

## Policy and behavior reconciliation

**P10:** The approved fail-closed M11 history/outbox design remains in effect. `FailedOutboxInsertRollsBackNewImmutableHistoryInSameTransaction`, `InFlightCancellationRollsBackBlockedFanout`, `CancelledDeliveryPreservesIntentAndSubsequentRetryDeliversExactlyOnce`, and `ExhaustedFanoutRetriesQuarantineWithoutPartialReceipts` occur in the real database test suite. The retained crash manifest covers before and after history, intent-claim and fanout commits, lost response, and subsequent recovery. These observations support satisfying P10's conditional recovery and accepted M11 compatibility requirements once they are confirmed at the final tested source SHA.

**P12:** Ninety days for unread/read and 30 days for acknowledged notification rows remain provisional periods only. The current authorization does not permit physical deletion or background cleanup. Immutable M11 history, M16 audit, replay records, holds and pending delivery obligations must not be removed. P12 does not require enabling deletion for G15 acceptance, and no code or authorization change should be inferred.

**P01-P09, P11, P13-P14:** Existing approved behavior and default-off dispatcher configuration are unchanged. G15 does not grant business read permissions merely because a user has Support or Administrator privileges.

## Mandatory independent review findings

1. The preceding technical 48/48 check tally is not a final 50-scenario acceptance ledger. G15-S01 through S24 require explicit source-backed examination and a separate S25 disposition, without marking previously BLOCKED or NOT EXECUTED scenarios PASS automatically.
2. Confirm the full final run's TRX, browser, SQL authorization, source-history uniqueness, signed dispatch scope, session/membership invalidation, transaction recovery, mutation controls, audit atomicity and artifact integrity at one identical full commit SHA.
3. Preserve historical failed workflow runs and source SHAs, including their known runner or fixture errors, with no retrospective PASS inference.

## S25 completion requirements

On completion of the corrected technical campaign, perform a separate raw-artifact review of all final results, reconcile each G15 S01-S24 requirement, record explicit P10 condition disposition and P12 retention restriction, and issue an explicit written G15-S25 PASS or NOT PASS and separate gate recommendation. Until then S25 is NOT PASS. Nothing here authorizes main merge, deployment, dispatcher activation, notification deletion or production migrations.

## Additional assertion-mapping quality findings

The current G15-S24-A01 through A05 inventory entries each map to the overall `g15-mutations` campaign and an unrelated duplicate-personal-receipt database test. All ten mutants were killed, but the audit does not require a specific source-history uniqueness mutation or separately prove every stated tenant/personal-row, membership/session, signed-proof, source-history, receipt-CAS and audit-atomicity category by individual mutant ID. The latest ten-mutant manifest names a receipt-recipient uniqueness variant, which must not automatically be equated to the source-history/outbox uniqueness requirement without reviewing the actual SQL mutation.

Also, G15-S21-A05 requires a bounded, correctly scoped HTTP response after dependency outages, yet its inventory evidence points to fanout cancellation and retry tests. These may establish delivery persistence but do not, by their names or scope alone, establish HTTP dependency-outage behavior. A direct HTTP outage test or a defensible source-locked reference must be established before independently closing this clause.

These are evidence-contract review findings, not claims that production behavior is broken. Strict 227/227 witness matching is necessary but not sufficient to close them.
