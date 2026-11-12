# G15-S25 Independent Review, Round 2, 2026-11-11

**Recorded by the closure session, unedited apart from this header.** The text below is the final message of a separate read-only Claude session ("G15-S25 independent review, round 2", 55 turns) started by the closure session at the owner's instruction. Its independence is not attested here: it shares the owner and the model family with the author, and the commissioner records independence. It ran no tests; its one throwaway container (`ho-g15-review-pg`, a PL/pgSQL timing experiment) was removed. The closure session has not yet verified each finding against the source. Acceptance pending.

---

# G15-S25 independent review, round 2

**Recommendation: NOT PASS** (2026-11-11). The behaviour is sound and the evidence is intact. But three clauses are marked FULLY_TESTED without a witness that proves them, and one of those is the main round 1 fix. The fixes are small and need no redesign.

**Commit reviewed:** `3e63bd0e71bdf4a80cd8ea1d27e7cceca3c37524`. I confirmed that `git diff --name-only 3e63bd0 HEAD` lists only `README.md` and `docs/*`.

**Context and independence:**
- I'm a fresh read-only session, and I did not implement or verify this change.
- I formed my view from source, tests and raw evidence first. I read the round 1 review and the round 2 results doc afterwards.
- I did not read the sibling notes in the shared folder.
- I'm the same model family as the round 1 reviewer, so any independence beyond that is for the commissioner to record.
- I ran no tests and touched no shared database. I ran one disposable container, `ho-g15-review-pg`, for a PL/pgSQL timing experiment, and removed it.

## What checked out
- **Evidence integrity.** All 713 entries in `PACKET-SHA256.txt` verify. The archive sha256 matches the brief (`ada16acd...b060`). The `source-sha.txt` of the G15 artifacts I opened, and the mutation manifest, name `3e63bd0`. The mutation manifest source hash (`4dd0e8bd...`) equals the migration blob at 3e63bd0. The `M15ScaleLoadTests.cs` hash matches the recorded test hash.
- **17 G15 mutants.** I read every mutant log. The control passes (4 + 14 tests).
  - Each mutant is killed at the named assertion, not by a build error or a setup failure. Examples:
    - `recipient-limit-removed` fails at G15RemediationAcceptanceTests.cs:132 with "expected 0, actual 1001".
    - `receipt-cas-bypassed` fails with "expected Conflict, actual OK".
    - `cursor-lifetime-extended` fails with a lifetime of 1d 9h against a 19-20 minute range.
    - `dispatcher-timeout-not-trapped` fails with an unhandled `57014`, so the unmutated trap really does catch it.
  - All three mutants that change the C# layer (cursor binding, page limit, cursor lifetime) are killed too.
- **P12 no deletion.** There is no delete path for M15 tables in `src`, and `harborops_api` has SELECT only on receipts and nothing on the outbox.
- **`M15:EnableDelivery` and `M13:EnableIdleCleanup`.** Both default off in `Program.cs`, `compose.yaml` and `.env.example`.
- **P10 fail-closed.** `FailedOutboxIntentDuringARealM11EvaluationRollsBackTheWholeActionAndRetrySucceeds` drives a real M11 evaluate with a `CHECK(false)` on the outbox. It asserts a 5xx response, no change to exception, history or outbox rows, no leaked constraint name, and a successful retry.
- **Browser suite.** 10 of 10 tests pass on Chromium, Firefox and WebKit. Nine tests stub the notification API; only the live-event test is end-to-end.
- **Crash windows.** 6 G15 phases are `KILLED_AND_VERIFIED`, with recovery runs where applicable.
- **Scale.** At 1,000,000 receipts: unread count 77 ms, Unread filter 28 ms, Read 184 ms, Acknowledged 134 ms.
- **HTTP outage test.** It now covers list, count, detail and PATCH, checks the 15-40 s bound and the closed response body, and checks that the same version still works after recovery.

## Findings, ranked

**Medium 1: G15-S17-A07 is not proven by its only witness.**
- The inventory claims "count exact up to 1,000 and capped beyond".
- The only witness asserts `Assert.InRange(unread, 1L, 1000L)` (M15ScaleLoadTests.cs:180). A count of 1, or a flag that is never set, passes.
- Nothing in the C#, TypeScript or browser tests mentions `unreadCountCapped` or `1000+`. Every browser mock omits the flag.
  - Cap logic: NotificationStore.cs:185-212
  - Endpoint: NotificationEndpoints.cs:207
  - Web display: NotificationCenter.tsx:64 and :149
- No mutant covers the cap: not removing the flag, not shifting `cap+1` to `cap`, not removing `LIMIT`. The one test that gets near it asserts a range.
- To close: assert exactly 1000 with `unreadCountCapped=true` at scale. Add a 1000 versus 1001 boundary case and an exact small count with the flag false. Add a browser case with `unreadCountCapped:true` that expects "1000+". Add one or two cap mutants.

**Medium 2: the `delivery_timeout` trap is reachable only if the database role sets a statement timeout. Production sets none.**
- The witness uses `ALTER ROLE harborops_api SET statement_timeout='2s'` (G15RemediationAcceptanceTests.cs:256). Nothing in `src`, the provisioner, compose or the migrations configures a dispatcher timeout.
- In production the only bound is the client `CommandTimeout = 25` (NotificationStore.cs:52), and no test exercises that path.
- I tested the semantics in a disposable PostgreSQL 16.15. A three-iteration loop with the same `WHEN OTHERS OR QUERY_CANCELED` handler, run under `statement_timeout='2s'` with a 4 s sleep per iteration, gave: `1:trapped 57014 (2.0s)  2:ok (4.0s)  3:ok (4.0s)`.
  - The timer is one-shot per top-level statement. After the first trapped event, later events in the batch run unbounded, up to the client's 25 s cancel.
- The migration comment says "one slow event can neither abort the batch nor be retried forever". That holds only under a server timeout that production does not set. A client cancel or process shutdown during the call is also counted as a `delivery_timeout` retry.
- G15-S21-A03 and S21-A06 are therefore proven for the test configuration, not for the shipped one. To close: set a bounded `SET LOCAL statement_timeout` in `DeliverPendingAsync`, or document and provision the role setting. Reword the claim to "first slow event". Add a test that uses the client-timeout path.

**Medium 3: S12-A03 and S12-A04 are still only partly proven.**
- `CompetingDispatchersDeliverSeveralIntentsExactlyOnceWhileReadersRun` sums the two dispatchers' results (G15RemediationAcceptanceTests.cs:699). It never shows that both claimed work.
  - Under repeatable read, a loser can hit `40001` and return 0 without anyone noticing.
  - No mutant removes `SKIP LOCKED` (20261109010000_M15ScopedNotifications.cs:160).
- The reader is one Manager reading through the store, not HTTP. `Assert.InRange(n, 0, intents)` is a trivial bound for a single recipient, so the comment "readers only ever observed complete per-intent receipt sets" overclaims. There is no other-user reader to prove scoping during fanout.
- To close: assert per-dispatcher claims greater than 0, add a `SKIP LOCKED` mutant, and add at least one different-user HTTP reader.

**Low**
- **L1.** The count and list bounds are on visible rows, not scanned rows. This is reasoned from the plan shape, not measured. `m15_can_read` runs per row. A user whose old receipts fail the policy (rejoined with a new membership version, or changed role) with a large unread backlog gets a count that scans all of those rows. P12 means backlogs only grow. The scale test has one recipient with all 1,000,000 rows visible.
- **L2.** The `ANALYZE` after the bulk load (M15ScaleLoadTests.cs:112) was added because stale statistics produced a bad plan. Production depends on autovacuum timing. The author discloses this. It stays a residual risk, and I would record it.
- **L3.** S01-A05's config assertions are vacuous. `appsettings.json` contains no `M15` or `M13` keys, so `GetValue<bool>` returns false whatever the code does (G13G15FeatureDefaultAcceptanceTests.cs:32-33). The real protection is the string match on `Program.cs` and `compose.yaml`. No test starts the API and checks that the hosted service is not registered.
- **L4.** The crash windows are still partly surrogate. `G15BeforeClaimCommit` is a raw admin `SELECT ... FOR UPDATE SKIP LOCKED`. The before/after-fanout windows call `m15_deliver_pending` from the test host with a hand-signed scope. The crash script is unchanged since round 1, and the PostgreSQL semantics make the results valid.
- **L5.** There is still no operator path for a quarantined intent, including `recipient_limit`. OPERATIONS.md:278 does not describe one. The test requeues by raw admin SQL. The round 2 disposition table is silent on this round 1 point.
- **L6.** S10-A02 ("archived details only when M11 permits") is pinned for a Manager only. The negative branch is untested.
- **Informational.** The migration `20261109010000` was edited in place. This is safe only because `main` does not contain it. Confirm that no non-disposable database applied the c1 version.

## Answers to the seven questions

1. **Round 1 dispositions.**
   - Correct: M2 (the cap now has a witness and a mutant) and L1 (all 17 mutants are killed, including C#-layer ones). L3 is substantively unchanged.
   - Partly correct: M1 (bounded and measured, but see Medium 1 and L1). M3 (see Q2).
   - Open: L5 above, which is round 1's quarantine runbook point. L4 above, which is round 1's L3 crash-window point. L4 from round 1 (late-delivered events and cursor cutoffs) is not mentioned and not retested. Round 1 L6 (a `40001` makes the service sleep 15 s while work is pending) and L7 (local-mode evidence is self-reported) are unchanged.
2. **Witnesses proving each clause.** No. Not proven: S17-A07, S21-A03/A06 in the shipped configuration, S12-A03 and S12-A04. Weak: S01-A05, S10-A02. The new tests do prove S05-A04, S18-A05, S18-A03, S17-A01/A03, S14-A02/A03, S10-A01, S12-A06 and S21-A05. I did not re-audit the other roughly 100 FULLY_TESTED rows beyond the matrix mapping.
3. **Mutation validity.** Valid: control clean, 17 of 17 killed at the intended assertion. Uncovered defect classes: the unread-count cap and flag, removal of `SKIP LOCKED`, the server-side timeout setup, the default-off wiring, and (inferred from the mutant list) the `ViewShipmentData` guard and `organization_id` predicates in the C# store.
4. **Planner and timeout scoping (G13).** I did not review G13 in depth. In `OperationalKpiStore.cs:286-301` the settings are `SET LOCAL`, so they end with the transaction on every path that rolls back. I did not trace a path that catches a failure and keeps using the same transaction.
5. **The 429 after three dashboard loads (G13).** Outside G15, so I don't give a verdict. The behaviour follows from the approved limits. It is a product decision, not a technical defect.
6. **Local-mode evidence and the harness reset.**
   - Local-mode evidence is acceptable as technical evidence for G15 under the owner's decision of no hosted run, provided the record says "local" and the archive hash is anchored outside the producer's control.
   - The reset is a legitimate isolation step for the KPI suite only. It `DELETE`s `operational_kpi_reports` in the disposable database before each engine's KPI run and records the count (0, 1, 1). The notification suite only refreshes sessions. It does not weaken any G15 evidence.
7. **Blocking risks.** I found no security, tenant-isolation, data-loss or migration blocker. The acceptance blockers are Mediums 1-3. The only operational risk to record is L1 for the unread-count bound.

## Conditions to move to PASS
1. Close Medium 1: prove the exact-and-capped contract, add the browser `1000+` case, add cap mutants, and map them.
2. Close Medium 2: give the dispatcher a real timeout in production configuration, or narrow the claim and document the required role setting. Add a witness for the client-timeout path.
3. Close Medium 3: assert both dispatchers claimed work, add a `SKIP LOCKED` mutant, and add a different-user HTTP reader.
4. Record L1, L2, L5 and the migration-in-place note in the residual-risk list.
