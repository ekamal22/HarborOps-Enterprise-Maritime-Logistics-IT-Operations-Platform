# G13-S25 Independent Review, Round 3, 2026-11-11

**Recorded by the closure session, unedited apart from this header.** The text below is the final message of a separate read-only Claude session ("G13-S25 independent review, round 3", Sonnet 5.5, 50 turns) started by the closure session at the owner's instruction. Its independence is not attested here: it shares the owner and the model family with the author, and the commissioner records independence. It ran no tests or containers. The closure session has not yet verified each finding against the source. Acceptance pending.

---

**G13-S25 round 3 recommendation, 2026-11-11: NOT PASS (narrow, closable).**
Reviewed commit `4e7b9e22aacef25f7f60e2295938ec6cf79bdadd`. HEAD `3f595c1` differs from it only in `docs/` and `README.md`.

I found no security, tenant-isolation, data-loss or migration defect in the shipped G13 behaviour. The NOT PASS rests on three things:
- G13-S20 still needs the owner's scope decision.
- Several witnesses do not prove their whole clause, so those assertions are over-claimed as FULLY_TESTED.
- The admission design is described more strongly than it behaves.

## Context and independence
- I am a Sonnet 5.5 session started by the closure orchestrator. I did not write or verify this change, and I read no sibling session notes.
- I ran no tests or containers. Everything below comes from source and raw artifacts.
- I read the source diff, the new tests, the mutation script, the inventory, the TRX files and the mutant logs before I opened the c3 results and the scope decision.
- I read the G13 round 2 review early. I did not read the interim record or the G15 reviews.
- Independence is not attested. The sessions share the owner and the model family.

## Evidence integrity (verified)
- The zip hash `d0e12322...22ec` matches the brief.
- `sha256sum -c PACKET-SHA256.txt` verified all 834 entries (no failures were reported).
- All 14 `source-sha.txt` files hold `4e7b9e2`.
- The 585 TRX results are all Passed.
- The packet is assembled from runs over about eight hours: first pass 04:45-06:07Z, reruns at 12:55Z and 13:11Z. All carry the same SHA, and the superseded copies are retained.

## Findings, most severe first

**F1 (Medium): the admission code claims more than it does, and the gap is not tested.**
- The comment at `OperationalKpiStore.cs:278-284` and the results doc say heavy builds are "bounded to the tenant's remaining capacity".
- What the code enforces is at most four heavy builds per tenant, because there are four slot keys (`:364-377`).
- Slots are positional: a builder takes slots `0 ... 4-others-1`, and `others` is read once.
- Example: four builders hold slots 0-3. Two commit, so two snapshots are live. A new builder has `others=2`, and slot 0 is free. It is admitted, so three heavy builds are in flight against two free quota slots.
- The late quota check refuses the loser. It gets a 429, but only after a full materialisation of about 15-18 s on the 85k tenant. That is the cost early admission was meant to avoid.
- Two smaller effects, both ending in a retryable 429:
  - Teardown window: a builder that has committed still holds its slot and is already counted in `others`, until `pg_advisory_unlock_all` runs in the `finally` at `:180-198`.
  - A builder that will fail late (413, timeout) holds a slot and can cause a transient refusal.
- No test or mutant covers these interleavings. The slot test locks slots from outside, and the six-user test passes whether admission is early or late.

**F2 (Medium): some FULLY_TESTED witnesses do not prove their whole clause.**
- **G13-S20-A04** ("CPU, memory, temporary files, write amplification"). `M13ScaleLoadTests.cs:357,400,412` records the test process's CPU and working set, not the database server's. Temp blocks appear in the BUFFERS plans, but WAL or write amplification is recorded nowhere. Only the growth in report storage is measured.
- **G13-S23-A04** (logs hide session material). `M13SecurityEnvelopeAcceptanceTests.cs:316-318` reads `DefaultRequestHeaders["Cookie"]`. With `HandleCookies=true` the cookie lives in the handler's cookie container, so that list is always empty and the session cookie is never searched for. The CSRF token, report token, member keys, password and email are searched.
- **G13-S23-A07 / S19-A15.** The new SECURITY DEFINER function `m13_tenant_live_reports` (`20261110010000_M13TenantReportCapacity.cs`) is not in the `calls` list at `M13TenantCapacityAcceptanceTests.cs:595-601`. No test checks its authority guard, its tenant filter or its expiry filter, and no mutant covers any of them. Dropping `organization_id=p_org` would make one tenant's snapshots refuse builders in another, which is a noisy-neighbour defect the suite would not catch.
- **G13-S19-A06** ("M5 through M12 immutable evidence"). The pruning and idle-cleanup tests count only shipments, status changes and voyage assignments. M10, M11 and M12 evidence is not counted.
- **G13-S19-A04** ("interrupt expired-report cleanup"). Neither witness interrupts a cleanup.
- **G13-S19-A02** ("serialization conflicts"). The retry at `OperationalKpiStore.cs:142` is dead under READ COMMITTED, and only the timeout path is witnessed.
- **G13-S19-A03.** `G13BeforeReportCommit` is a hand-built admin transaction (`G13G15CrashRecoveryTests.cs:53-111`), not a kill of the API mid-build.
- **Whole-job or whole-campaign witnesses.** S20-A09 and S23-A07 name the whole `g13-mutations` campaign, and S22-A01 and S22-A04 name only jobs. These are scenario-level.
- **Inventory rule.** The inventory's own rule says FULLY_TESTED needs each atomic clause. These assertions should be PARTIALLY_TESTED until witnessed or reworded.

**F3 (Medium, owner decision): G13-S20 is not closed.**
- The realistic 170k requirement is unmet, and the scope decision says so.
- The decision is still awaiting the owner. Until the owner chooses A, "A with B" or C, G13-S20 cannot be called passed.
- The mapping also lets A01-A06 pass on the Draft/Cancelled reference load.

**F4 (Low-Medium): headroom is still modest.**
- The 4-builder run on the realistic 85k tenant took 18.2 s against 17.1 s in the superseded run. The 8-builder run took 16.3 s, and the 4-builder 170k refusal took 20.1 s.
- All of these sit against the fixed 25 s cut-off at `OperationalKpiStore.cs:137`, with one host and two samples each.
- "Never a timeout" in the scope decision is therefore a measured result, not a guarantee.
- There is no cross-tenant limit, so many tenants building at once could still reach 503. This is already listed as residual.

**F5 (Low): the two database-level kills prove the constraint, not the predicate.**
- `member-sql-shipment-tenant-predicate-removed` fails with FK `fk_m13_member_shipment` (23503).
- `k03-first-event-deduplication-removed` fails with PK `operational_kpi_members_pkey` (23505).
- Both surface as `ServiceUnavailable` through the `Json` helper, as the logs confirm.
- This is the "constraint is the control" option from round 1. The results doc discloses it, and the failure is fail-closed.
- No mutant covers a silent tenant-filter loss where an FK would not trip. The mutation harness records KILLED on any failing named test without checking why. I read all 41 failure texts myself.

**F6 (Low): residuals I confirm as stated.**
- The 25 s token can cancel a commit that already happened.
- A refusal costs about 20 s.
- The fixtures have no M10 conflicts or corrections.
- The provisioner GRANT fails on a partly migrated database.
- The CSP test covers `nginx.conf`. `nginx.hosted.conf.template` adds `upgrade-insecure-requests` and is not exercised.
- The WebKit control only requires at least one event from a bare `<select>`. It does not compare sources or counts.
- Session advisory locks assume no transaction-mode pooler.
- I did not check the `FileNotFoundException` that round 2 saw in a crash log.

## Answers to the eight questions

1. **Round 2 findings.**
   - Closed in substance: F1 (mutants added), F3 (S19-A07 now asserts the exact key set and message; S19-A08 compares before/after counts of shipments, status changes and assignments), F6 (wording), and F10's planner leak.
   - F10 detail: a pooled, no-reset host with a session-wide mutant now fails on "off" versus "on". Four F10 sub-items stay open: the in-transaction `SET LOCAL ... on/15s` restore (`:321-323`), the GRANT on a partial deploy, `G13BeforeReportCommit`, and the unchecked `FileNotFoundException`.
   - Closed with caveats: F2 (see Q4) and F4 (see Q2).
   - Open: F5 (owner decision pending).
   - Recorded as residual, not fixed: F7, F8 and F9.

2. **Exact-clause witnesses.**
   - Clause-exact (read against the test bodies): S19-A07, A08, A09-A13, A14 (slot test), A15 (lock test), S20-A08 to A11, S23-A01, A02, A08, A05 and A06.
   - Not clause-exact: S20-A04, S23-A04, S23-A07/S19-A15 (live-count function), S19-A02, A03, A04, A06, S20-A09, S22-A01 and A04, and the S23-A03 CSP and browser parts for the hosted config only.
   - I did not sample S21 or the rest of S22 in detail.

3. **Mutation witnesses.**
   - The 41 source and 4 security mutants all fail on the named test.
   - 39 fail on a content assertion; the exceptions are the two in F5.
   - The 15 s timeout mutant fails with a 503, which is the intended reason.
   - Uncovered defect classes:
     - `m13_tenant_live_reports` (tenant filter, expiry, authority);
     - the early `m13_user_quota` call, since the late check hides it;
     - the in-transaction planner restore;
     - slot-ordering behaviour;
     - a silent tenant-filter loss that survives the FK.

4. **Early admission.**
   - It cannot admit more than four heavy builds per tenant, because only four slot keys exist and they are DB-wide.
   - It can admit more builders than the remaining capacity (F1), and the late loser pays a full build.
   - It can transiently refuse a builder capacity would allow, during teardown or while a doomed builder holds a slot.
   - The late decision is authoritative. It takes the tenant lock after the member statement (`:332-349`), and the mutants `...lock-removed`, `...active-report-cap-bypassed` and `...rejection-rollback-bypassed` are killed on content.

5. **Skip-locked hang.**
   - I can see only the TIMEOUT in the superseded log, and an alone-rerun that killed 25 of 25.
   - It occurred only in the mutant run, where waiting on a row lock is the expected behaviour.
   - The product statement carries `statement_timeout=15s`, so a database hang of 400 s is implausible.
   - I therefore suspect the test or harness, but I cannot attribute it. It is not shown to be a product defect.
   - A rerun alone is acceptable as kill evidence because it has a clean control, the same SHA and a recorded failure.
   - The unexplained flake stays a residual. This is the G15 reviewer's decision.

6. **Harness-only reruns.**
   - The superseded concurrency job used the same commands and filters, but its filter list omitted the eight-builder test, so the rerun added coverage.
   - The superseded timings (17.1 s, 19.2 s) are consistent with the rerun.
   - The reruns are legitimate and source-locked: same SHA, retained originals, a stricter audit and the same packet checksum.
   - I did not independently inspect the database and G15 mutation reruns beyond their SHA, status and TRX results.

7. **G13-S20 scope proposal.**
   - It is honest about the main fact: the realistic 170k requirement is not satisfied, the ceiling is unchanged, and about 91k realistic shipments is the practical limit.
   - Two soft spots:
     - Option B ("small change, count members first") is asserted without evidence, because a member count is the expensive statement.
     - The C extrapolation (about 17 s single, over 30 s for four) is reasoning, not measurement.
   - The inventory marks S20-A01-A06 FULLY_TESTED from a Draft/Cancelled load. That is "reference", as the decision says, but the labels should carry the scope.

8. **Blocking risks.**
   - None for security, tenant isolation, data loss or migration.
   - Editing the two migrations in place is acceptable only because neither is on `main`. I cannot verify that no non-disposable database applied an earlier version.
   - The performance risks are the 25 s ceiling and the missing cross-tenant limit (F4).

## Path to PASS
1. The owner records the S20 decision (A, A with B, or C).
2. Fix the F1 wording to "at most four, and at most the capacity", or make admission count in-flight builds.
3. Add the missing tests and mutants:
   - authority, tenant filter and expiry tests, plus mutants, for `m13_tenant_live_reports`;
   - a log scan that reads the real cookie container;
   - M10-M12 row counts around cleanup;
   - a kill-the-API-mid-build test, or reclassify S19-A03 as partial.
4. Reclassify S20-A04 and the other F2 items as PARTIALLY_TESTED, or add database CPU, memory and WAL capture.
5. Optionally, record the reason for each KILLED in the mutation manifest.
