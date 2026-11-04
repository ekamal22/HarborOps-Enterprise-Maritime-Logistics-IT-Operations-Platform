# G9 Verification Plan: Idempotent Worker and Reconciliation

**Execution status:** The G9 technical campaign was executed on `7665bc419618bb587873169b1dff8772e6a68e22` and its results are recorded in the [executed verification record](M9_EXECUTED_VERIFICATION.md). Every executable scenario passed; **G9-S01 is blocked by contract acceptance and G9-S25 independent review is not executed. G9 acceptance pending.** The scenario list below is the original plan and is unchanged.

**Status:** Specification with an executed technical record (see above). G9 acceptance pending.  
**Prepared:** 2026-11-04  
**Accepted baseline:** G0-G8 on protected `main` at `7b9e19d730e3a09c5e1184262460dfeac9319e3d`.  
**Work packages:** M9-01 through M9-04 in [execution graph](execution-graph.json).  
**Contracts:** [M9 architecture](M9_ARCHITECTURE_DESIGN.md), [M9 local contract proposal](M9_LOCAL_CONTRACT_PROPOSAL.md), [M9 implementation plan](M9_IMPLEMENTATION_PLAN.md) and accepted [G8 gate closure](M8_G8_GATE_CLOSURE.md).

## Gate principles

Every executable scenario must be run against the same source SHA on a disposable E:-backed environment. Do not infer passing status from test definitions or a previous G8 result. Use real PostgreSQL concurrency and failure injection, not only mocks. Retain actual TRX, SQL transaction witnesses, source/lease identities, mutation kills and sanitized logs.

Preserve **all** M0-G8 accepted behavior. Original M8 accepted receipts and events must never be rewritten to achieve a worker PASS; M8 post-commit-only HTTP 202, same-key replay/collision, tenant/source/credential reauthorization and intake quota all require regression proof.

G9 has a separate acceptance step. M9-01 local contract approval and a technical PASS do not authorize a merge. The final case G9-S25 requires independent evidence review and explicit subsequent acceptance. Dedicated accessibility audits remain reserved for M24-04, not G9.

## G9-S01 through G9-S25

| Case | Requirement | Minimum observed evidence |
| --- | --- | --- |
| **G9-S01** | Accepted M9-01 local state/lease/capacity/reconciliation contract | Versioned decision record and approved state machine; no implicit change to D-006 |
| **G9-S02** | Additive M0-M9 migration and M8 populated upgrade | Fresh schema, populated M8 rows retained unchanged, Pending backfill and safe empty rollback, EF snapshot parity |
| **G9-S03** | Scoped SQL integrity and work transition guards | Direct-SQL UPDATE/DELETE attacks on M8 immutable tables fail; invalid state/tenant/token changes fail; a Processed row cannot commit without exactly one receipt effect; no orphan effects |
| **G9-S04** | Unique immutable effect and attempt event constraints | Concurrent/duplicate effect insert impossible; append-only attempt events; correct composite FKs and unique keys |
| **G9-S05** | Atomic due claim and fair polling | Due/undue indexes, one transaction claim, no double claim, bounded organization round-robin |
| **G9-S06** | Two live worker processes race for one receipt | Actual overlapping PostgreSQL transactions, exactly one current lease token and epoch; losing worker cannot finalize |
| **G9-S07** | Multiple tenants and sources remain isolated | Same external key processed independently in two organizations, no foreign association or read; independent organization progress |
| **G9-S08** | Lease renewal and clock boundaries | Heartbeat extends valid token before DB expiry; at/after expiry and wrong token/epoch always rejected |
| **G9-S09** | Expired lease recovery and stale-worker fencing | Old process pauses beyond expiry, new claimant takes over, stale finalize fails; one effect and one terminal outcome |
| **G9-S10** | Crash immediately after claim commit | Kill worker, restart after expiry; receipt retained, retry within budget, no lost event or duplicate effect |
| **G9-S11** | Crash during processing/effect before COMMIT | Kill after effect insert but before finalize commit; transaction rolls back atomically, next worker commits exactly once |
| **G9-S12** | COMMIT acknowledgment ambiguity | Real database-wire interruption around finalization COMMIT, pre/post commit branches, retry reconstructs correct effect and state |
| **G9-S13** | Duplicate delivered event does not repeat business resolution | Original M8 receipt replay 2x/10x/20-way concurrent plus processing restart, one unique resolution effect |
| **G9-S14** | Equipment reference resolution is time/tenant safe | Unique active container and time-valid M6 assignment resolves; archived, missing, ambiguous, out-of-window or foreign ID does not |
| **G9-S15** | Transport reference and unknown references are not guessed | Opaque transport token not silently cast to voyage, unknown event enters NeedsReconciliation without false shipment link |
| **G9-S16** | Deterministic finite retry schedule | Retry base/max/jitter boundaries, attempt increments at claims, exactly configured maximum then DeadLetter, no busy loop |
| **G9-S17** | Dead-letter and reconstruction evidence | Permanent error and exhausted retries produce inspectable tenant-scoped record; original event bytes/receipt unchanged |
| **G9-S18** | Authorized redrive is bounded and auditable | Explicit permission/reason, new generation, no Processed/active lease redrive, immutable attempt trail and finite redrive ceiling |
| **G9-S19** | Tenant/operator security | Cross-org direct ID/list/read/redrive denied; unauthorized roles denied; no raw payload, bearer or SQL error in responses/logs |
| **G9-S20** | Exact 10,000 unresolved-backlog cap under concurrent worker movement | 9,999/10,000/10,001 and parallel ingress/claim/finalize across sources; Leased/RetryWaiting/NeedsReconciliation still count; resolved terminal frees capacity |
| **G9-S21** | Organization suspension and resumed processing | Suspended org not processed despite due work; accepted history preserved; resumed org proceeds once; no source-credential reissuance |
| **G9-S22** | Full affected G0-G8 functional/security regression | .NET unit/contract/integration twice, M8 ingress and existing browser journeys, EF snapshot, compose outage/recovery; no skipped negative cases |
| **G9-S23** | Adversarial mutation effectiveness | Clean control passes; targeted mutations to unique effect, lease token, epoch, expiry, organization lock, retry cap, tenant filter, transition guard, backlog predicate and redrive auth fail intended tests |
| **G9-S24** | Dependency, image, operational and evidence safety | NuGet/npm advisories, source-matched exact-image HIGH/CRITICAL scans, bounded logs and readiness, named resource teardown, no secrets and E:-backed immutable evidence |
| **G9-S25** | Independent source-locked review and explicit acceptance | Reviewer checks raw stage results, hashes, attempts, PostgreSQL races, 25-case register, contract decision and residual limitations; acceptance separately recorded |

## Required negative and SQL adversarial cases

- Insert forged effect for another tenant or a different receipt with the same external publisher event key. Uniqueness and scoped FKs must prevent cross-tenant or duplicate effects.
- Update/delete accepted receipt, original event or conflict record after M9 upgrade. Immutable M8 triggers must still reject it.
- Force illegal `Processed -> Leased`, `DeadLetter -> Processed`, `Pending -> Processed` without an effect and any work-row delete. SQL guards and application tests must reject these direct writes.
- Issue heartbeats/completions with old epoch, expired lease, wrong token, wrong organization, prior redrive generation and a token from a different worker. All fail closed and produce no effect.
- Hold an organization lock while the API performs intake and the worker finalizes. Demonstrate a finite lock wait, correct cap and no deadlock; do not rely on sleeps as proof of overlap.
- Kill either worker on both sides of the transaction COMMIT boundary. Confirm a failed or ambiguous acknowledgment does not produce false success and recovery reads durable state.
- Write a valid but unknown Equipment event, an opaque Transport event and a time-outside-interval event. No guessed associations, false process success or cross-tenant data.
- Attempt operator redrive of Processed, active Leased, foreign tenant and over-ceiling generation; ensure no privilege bypass through broad administrative roles.
- Attempt downgrade with processed/attempt/effect records; fail without data loss. Empty disposable database downgrade may succeed.
- Inject secrets, publisher body, malicious error text and SQL-like payload into exception/log scenarios; ensure sanitized outputs and archive scanners catch leaks.

## Mutations and coverage accounting

Require a clean unmodified control and a non-equivalent target mutation for each of the following families: (1) effect uniqueness removed, (2) token predicate removed, (3) epoch predicate removed, (4) lease expiry ignored, (5) two-worker claim protection removed, (6) organization capacity lock removed, (7) unprocessed state omitted from count, (8) retry budget bypassed, (9) unauthorized redrive allowed, (10) tenant resolver filter removed, (11) work transition SQL guard disabled, (12) post-commit effect acknowledgment weakened. Each must be killed by its intended negative test rather than compilation errors or an unrelated failure. Exact executable mutant scripts and kill set must be implemented and reviewed under M9-04, not assumed to exist now.

If any mutant is equivalent, record an explicit replacement and source proof; never count an equivalent mutation as a kill. Run mutations in source-isolated disposable copies after cheap checks and full core correctness pass. Do not modify accepted `main`.

## Efficient full verification execution

1. **Cheap gates:** clean SHA, script syntax, graph/doc checks, typed config validation, pure retry and lease policy unit tests, Release build with warnings as errors, EF snapshot and advisories.
2. **Database correctness:** isolated disposable PostgreSQL, M8-to-M9 populated upgrade, two full integration passes, direct SQL guard/FK tests, real overlapping worker processes, clock/lease expiry, quota and outage controls.
3. **Service functionality:** isolated `ho-verify-g9` Compose stack and real worker with API and operator interface, full affected G0-G8 regressions, three browser engines where shared browser or operator UI behavior is affected, and exact source image scans.
4. **Mutations:** twelve targeted worker/security variants plus clean control and intended failures.
5. **Finalize:** teardown named G9 resources only, final log scan, SHA-256 per-file manifest, immutable run-unique archive, archive integrity rerun, source/run-bound technical PASS/FAIL and formal acceptance pending record.

Preserve failed attempts and do not repeatedly run a full suite while known cheap checks fail. After every source change to a candidate, rerun the affected focused test, then the complete source-locked final suite. Do not suppress flaky cases with silent retries. No GitHub Actions and no permanent project storage on C:. Unavoidable task-created temporary C: files must be inventoried and safely removed; E: cache/evidence retained until acceptance and explicit cleanup authorization.

## Evidence and decisions

The final report must include exact source SHA and clean worktree, stage exit codes, full test counts, per-scenario evidence links, worker race barrier logs, lease-token/epoch redaction proof, effects and attempts reconciled to original receipts, mutation control and kills, scan/image identities, final Docker inventory, C: temporary cleanup, and archive/manifest hashes. Separate source execution from external independent review.

**Current outcome:** G9-S02 through G9-S24 passed technically on the source revision above; G9-S01 is BLOCKED (M9-01 local contract acceptance pending) and G9-S25 is NOT EXECUTED (independent evidence review pending). No M9 gate is closed and nothing is accepted.
