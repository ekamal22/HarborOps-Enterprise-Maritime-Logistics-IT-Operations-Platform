# G13-S25 Independent Review, Round 2, 2026-11-11

**Recorded by the closure session, unedited apart from this header.** The text below is the final message of a separate read-only Claude session ("G13-S25 independent review, round 2", Sonnet 5.5, 56 turns) started by the closure session at the owner's instruction. Its independence is not attested here: it shares the owner and the model family with the author, and the commissioner records independence. It ran no tests or containers; its findings are from reading source and artifacts. The closure session has not yet verified each finding against the source. Acceptance pending.

---

**G13-S25 round 2 recommendation, 2026-11-11: NOT PASS (narrow, closable).** Reviewed commit `3e63bd0e71bdf4a80cd8ea1d27e7cceca3c37524`. I confirmed HEAD `b1612b2` differs from it only in `docs/` and `README.md`.

I found no correctness, tenant-isolation, data-loss or migration defect in the shipped G13 behavior. Reading the code, I could not make any failure path leave `enable_nestloop` or `statement_timeout` changed on a pooled connection. The blockers are verification claims that overstate coverage, and thin headroom on the concurrency evidence.

## Context and independence
I am a Sonnet 5.5 session started by the closure session. I did not implement or verify this change. I formed my view from source and raw evidence first. Partway through I read the results doc's "Verification results" and "Attempts" sections, then the round 1 review. I did not read sibling session notes. I ran no tests or containers, so every finding below is from reading code and artifacts.

**Evidence integrity**
- The zip hash matches the brief.
- All 713 entries in `PACKET-SHA256.txt` verify.
- All 14 `source-sha.txt` files hold `3e63bd0`.
- No TRX contains a failed result.

## Findings, most severe first

**F1 (Medium-High): the mutation set dropped two protections that the assertion matrix still reports as fully tested.**
- `scripts/g13-mutation-campaign.py` removed `tenant-materialization-scope-removed` and replaced `k03-duplicate-status-history` with `k03-counts-arrival-events-as-completions`.
- The new K03 mutant fails on a count (Expected 3, Actual 4), so it is valid. It does not cover the `DISTINCT ON` first-event de-duplication that round 1 F2 named.
- Round 1 F2 offered two ways to close the tenant-scope gap: a mutant that survives the foreign key, or stating the FK is the control. The results doc did neither.
- G13-S24-A01 ("tenant-predicate") and S24-A03 ("duplicate-history") are therefore unwitnessed in the inventory, because S24 lists the whole `g13-mutations` job as evidence.
- No mutant covers four newer protections:
  - the tenant advisory lock: `OperationalKpiStore.cs:312`
  - the lock-then-commit ordering: `OperationalKpiStore.cs:338`
  - the `e.organization_id=p_org` filter inside the SECURITY DEFINER readers: `...TenantReportCapacity.cs:53,63`
  - the `SET LOCAL` scoping: `OperationalKpiStore.cs:286`
- The org filter matters most. Those readers bypass row security, so that `WHERE` clause is now the only per-row tenant filter. `ScopedReadersAndQuotaFunctionsRequireSignedReportingAuthority` (`M13TenantCapacityAcceptanceTests.cs:543`) discards the row counts it fetches and never asserts foreign-tenant rows are excluded.
- MemberSql's outer `organization_id=@org` join keeps the API path safe. A compromised API role calling `m13_scoped_evaluations(A)` directly is the exposed case.

**F2 (Medium-High): concurrency headroom is thin, and nothing covers more than four builders.**
- The tenant quota is checked only after the heavy materialization (`OperationalKpiStore.cs:306-331`). Any number of distinct users in one tenant can run the 9-19 s statement at once. Only the actor lock limits it, and that is one build per user.
- The 85k four-builder run took 17.3-18.6 s in the concurrency job and 21.1-22.6 s in the mutation control. Both sit against a hard 25 s cut-off: `BuildAsync` cancels everything at 25 s (`:137`).
- The 28 s `RequestEnvelope` and the `lockSeconds` clamp (`:309`) are never binding, because the 25 s token fires first.
- One build alone takes about 8.8 s and four concurrent take about 18 s. Five to eight distinct builders would plausibly reach 503 `report_timeout` for everyone. That is the behavior the owner said must not be normal, and it is untested.
- The tested 85k tenant holds 466,448 members, 93% of the 500k ceiling. It is effectively the largest tenant that can be served, so there is no margin above it.

**F3 (Medium): round 1 F3 is still open, though the results doc's disposition row only mentions F2/F3 mutants.**
- G13-S19-A07 ("discloses no count") is still witnessed only by `Assert.False(error.TryGetProperty("counts", ...))` (`M13TenantCapacityAcceptanceTests.cs:77`, `:375`).
- G13-S19-A08 ("without removing any business record") is still `count(shipments) > 0` (`:170-174`).
- The new idle-cleanup test does compare shipment counts before and after, so the correct pattern exists. It was not applied to A08.

**F4 (Medium): the assertion-to-witness mapping is scenario-level, not clause-level.**
- 18 of the 24 G13 scenarios with automated assertions map every assertion to a single identical witness set (S01-S07, S11-S14, S16-S18, S21, S22).
- Examples:
  - S01-A05 "bounds under load" shares four tests with S01-A02.
  - S23-A03 (CSP/CSRF/no-store) and S23-A04 (no token or ID leak in logs) point at the four SQL and forged-token tests, none of which inspects headers or logs.
  - `OperationalKpiContractTests.cs:21` does check `no-store`, but only on unauthenticated responses. It is not mapped.
- `audit-g13-g15-assertions.py --enforce-complete` proves only that the named witnesses ran and passed, as the results doc admits.

**F5 (Medium, policy): the practical tenant-size limit is not documented.**
- With the realistic mix, about 5.5 members per shipment means the 500k ceiling serves up to about 91k shipments.
- A 170k-shipment tenant gets a 413 and no dashboard. The plan's own S20 target tenant size is therefore refused.
- Round 1 measured 170k builds only on a Draft/Cancelled fixture. The policy docs still cite that earlier figure as verified at 170k.
- The owner should accept this limit explicitly.

**Low**
- **F6, quota wording:** the results doc says a user "opens the dashboard three times" and gets 429.
  - A build first deletes that session's own previous snapshot (`:234`), so three loads in one session leave one snapshot.
  - The 429 needs three or more sessions of one user within 10 minutes, as `...UserIsLimitedToTwo...` (`:372,406`) shows with its same-session refresh.
  - The web client has no specific handling for the code. The user sees the server message, with no release path.
- **F7, fixture realism:** `RealisticTenantFixture.cs` uses `evidence='{}'`, an identical fingerprint everywhere, no M10 equipment conflicts and no corrections, so K10 and the correction branches see no input. There is one tenant with no foreign rows, and `ANALYZE` was run.
- **F8, wasted work on refusals:** the 170k "quick refusal" takes about 19.6 s for each of four builders. It does the full materialization up to the 500,001-row limit before refusing.
- **F9, pooled-connection and commit behavior:**
  - Each build holds two pool connections for up to 25 s.
  - The 25 s token can cancel `CommitAsync` (`:338`) after the server has committed, orphaning a snapshot that holds a quota slot for 10 minutes.
  - The serialization retry (`:142`) is now dead code under READ COMMITTED. The test name `RepeatableReadReport...` is a misnomer.
- **F10, test-sensitivity gaps:**
  - `PlannerGuard` cannot discriminate a `SET LOCAL` to `SET` mutation, because Npgsql resets connections on return by default.
  - The in-transaction restore at `:301` is unpinned.
  - The 2 GiB storage assertion has about 10% headroom (1.93 GB used).
  - The `G13BeforeReportCommit` witness is still a hand-built admin transaction.
  - The provisioner GRANT still fails on a database lacking `20261110010000` (partial deploy).
  - `g13-g15-crashes-c2/G13AfterReportCommit.log` still contains a `FileNotFoundException`, but no TRX failed.

## Answers to the seven questions

1. **Round 1 dispositions.**
   - Closed in substance: F1 (concurrency evidence), F4 (user quota of 2, per-user across sessions) and F5 (explicit switch, default off, `Program.cs:42-50`, `compose.yaml`, tested both ways).
   - Open or partial: F2 (the K03 replacement is valid but the DISTINCT ON and tenant-scope clauses are unwitnessed), F3 (untouched), and F6's isolation-level note (now moot).
   - F1 closes through an owner policy change to a 413 at 170k, which is finding F5 above.
2. **Does every in-scope assertion have a witness proving its exact clause?** No. See F4. The clause-exact witnesses I found are:
   - S19-A09 and A10
   - S19-A11, with the PG-semantics caveat in F10
   - S19-A12
   - S19-A13
   - S20-A08 to A10
   - S23-A06 and A07, for the authority part only
   - The ones I found weak: S19-A07, S19-A08, S23-A03, S23-A04, S24-A01 and S24-A03.
3. **Are the mutation witnesses valid?**
   - I read the failure text of the mutant logs for the tenant-quota and user-quota mutants, both authority-removed mutants, the planner and 15 s-timeout mutants, the cursor, idle-limit and K03 mutants, and all four SQL security mutants. All fail on content for the right reason.
   - The planner-guard mutant ends in 4x HTTP 503 `report_timeout` at 25.06 s, which is the intended failure.
   - The `tenant-quota-rejection-rollback-bypassed` mutant fails on the count (4 expected, 5 actual).
   - Surviving classes are listed in F1. Concurrent quota races are also uncovered, because the quota tests are sequential and the concurrent tests stop at four builders.
4. **Planner and timeout scoping.**
   - `SET LOCAL` applies inside the build transaction (`:286`, restored at `:301`). Early returns, exceptions and cancellation all dispose the transaction, which rolls back, so the settings cannot persist.
   - The value is restored to a hard-coded `on` and `15s`, not to the prior value. That is fine here.
   - The `m13_scoped_*` functions are STABLE, so they share the outer statement's snapshot.
   - The test witness (`PlannerGuard`, run after success, refusal, 503 timeout, cancel and rollback) is correct but non-discriminating (F10).
   - Lock scope is correct: the actor lock is held across the build, the tenant lock is taken only for the quota check and commit, and `pg_advisory_unlock_all` runs in `finally`. The run showed 0 advisory locks held.
   - The 28 s envelope is not binding (F2).
   - The cleanup switch defaults off, with a poll of 1-300 s.
   - The tenant quota of 4 and user quota of 2 count the in-flight row, so they allow exactly 4 and 2, and the quota functions deny unsigned, Administrator and other-tenant scopes.
5. **Is a 429 after three loads in 10 minutes acceptable?** Yes, as a product call, but the claim is imprecise (F6). The cause is three or more sessions, and loads within one session replace each other. Recommend a specific UI message and, ideally, releasing the snapshot on logout or navigation. These do not block the gate.
6. **Local-mode evidence and the browser reset.**
   - Local-mode evidence is acceptable. It carries a real SHA, derived job results, no `GITHUB_*` values, and hash-sealed superseded attempts.
   - Host hardware is stated only in the results doc, not in `environment.txt`.
   - The per-engine reset is legitimate. It runs in the external harness (`job-browser.sh`), uses `psql` on a disposable database, and records the rows removed (0, 1, 1).
   - Attempt 1's WebKit 429 confirms the product behavior (F6). Browser tests 4-8 are mocked, as in round 1, so the 429 UI path is not exercised.
7. **Blocking risks.** None for security, isolation, data loss or migration. The unlocked-quota race and unbounded heavy admission (F1, F2) are the closest. Editing migrations `20261109010000` and `20261110010000` in place is safe because neither is on `main` (`eb52936` contains no `20261109`/`20261110` files).

## Path to PASS
- Add mutants for:
  - removing `DISTINCT ON` in `events`
  - dropping `s.organization_id=@org` in MemberSql
  - dropping `e.organization_id=p_org` in both scoped readers
  - removing the tenant advisory lock
- Add a concurrent test with at least six distinct users: exactly four succeed, two get 429, and `operational_kpi_reports` holds at most four.
- Add a realistic 85k run with 6-8 distinct builders that either completes or records an explicit decision about the 503s.
- Fix S19-A07 and A08 to use before/after counts and an exact error-body key set.
- Re-map or correct the S23 and S24 evidence entries.
- Record the roughly 91k-shipment limit as an accepted limitation.
