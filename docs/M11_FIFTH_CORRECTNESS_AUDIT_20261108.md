# M11 Fifth Correctness Audit and Source Remediation

**Review date:** 2026-11-08
**Isolated implementation:** `feat/m11-delay-exception-lifecycle`, draft PR #31
**Accepted source baseline:** `main` `6ead1d076946dd96b53401265a31efc35c4a666c`
**Starting candidate:** `501f14b4881d76956e584a2858a1ea6eb83dc08e`
**Fifth-pass source remediation checkpoint:** `72bd0d1bc9b4ba6edcd2731e5910c00ae4e08441`
**Earlier findings:** [first](M11_CORRECTNESS_AUDIT_20261107.md), [second](M11_SECOND_CORRECTNESS_AUDIT_20261107.md), [third](M11_THIRD_CORRECTNESS_AUDIT_20261108.md), [fourth](M11_FOURTH_CORRECTNESS_AUDIT_20261108.md)
**Verdict:** Confirmed source findings remediated as described. Runtime correctness unverified. G11 acceptance pending.

## Scope

Independently reread M11's domain rules, signed evaluation/disposition SQL, PostgreSQL schema, source snapshot and due worker, API role resolution, exception list/history pagination, frontend actions and the synthetic regression source. Compared with the accepted M5 state-transition rules, M9 transport processing hold, M10 projector lock order, M16 audit implementation, M11 provisional policies and the G11 gate specification.

This environment does not expose the E:-backed HarborOps checkout, .NET 10 SDK, PostgreSQL, Docker or browser fixture runtime. Public GitHub DNS access from the local container was unavailable, so the repository was reviewed and edited through the connected GitHub service. No .NET build, SQL migration, tests, browser fixtures, scans or source-locked independent review were executed. Source checks are not test passes.

## Fifth-pass findings

| ID | Severity | Finding | Remediation | Executable evidence |
| --- | --- | --- | --- | --- |
| F11-036 | High | A previously acknowledged exception could be confirmed inactive, then become active again. The signed evaluator reused the original Acknowledged occurrence, contrary to the proposed new-occurrence-on-recurrence contract, leaving the new risk attached to a stale acknowledgment. | If the current occurrence is Acknowledged and last condition Inactive, the signed evaluation closes it with one immutable system AutoResolved history entry, then creates the next Open occurrence with a new ID and incremented occurrence number. Unknown and Stale conditions do not prove clearance. Added an integration test with an initial overdue schedule, acknowledgment, verified clearance, recurrence and repeat evaluation to check occurrence count and history. | NOT EXECUTED; provisional policy still pending |
| F11-037 | Low | Shipment UI displayed a Resolve action for an Open/Inactive record although the signed SQL only permits a Manager to resolve an Acknowledged/Inactive occurrence. | Resolve now requires both state Acknowledged and condition Inactive. Browser fixture source checks that an Open/Inactive row offers neither Resolve nor Acknowledge. | NOT EXECUTED |
| F11-038 | Medium | The action store mapped every PostgreSQL unique-key violation to an idempotency conflict, potentially hiding unrelated integrity failures as an ordinary 409. | Classifies only `shipment_exception_replays_pkey` as a replay-uniqueness conflict; SQLSTATE PZ004, 40001 and 40P01 remain recognized transaction conflicts. Unrelated 23505 errors are not suppressed. Static source-contract checks added. | NOT EXECUTED |

## Source and contract consequences

- Existing current-incident history remains append-only. Closing the previous Acknowledged/Inactive occurrence leaves its last verified inactive condition and fingerprint intact; the resulting system transition records the reason that the cleared incident was superseded. The new active occurrence has its own UUID, optimistic version and `Opened` history event.
- The unique active occurrence index remains enforced: the previous row transitions to Resolved before inserting the next occurrence within the same signed SQL transaction. The organization and shipment locks continue to serialize competing evaluations and Manager mutations. No accepted migration or worker source was modified.
- The proposed M11-P10 lifecycle is not approved solely because a plausible implementation exists. Explicit acceptance must decide what happens if a confirmed Inactive condition is followed by Unknown/Stale before becoming Active again. The current code only recognizes direct Acknowledged/Inactive to Active recurrence. It also cannot resolve an acknowledged `RecordedArrivalLate` occurrence while the immutable historical late-arrival condition remains Active under the current clear-only resolution rule.
- Error classification now distinguishes expected replay-key collisions from unrelated constraints. The API's centralized error handler continues to treat unexpected database failures as errors, without falsely acknowledging mutation success.
- Fourth-pass history continuation and list creation cutoffs remain separate from this lifecycle change. The list cursor does not freeze mutable lifecycle states. Full history is source-implemented, not runtime-verified.

## Outstanding G11 blockers

1. **G11-S01 BLOCKED:** Decide M11-P01 through P12, especially recurrence across Unknown/Stale, final handling of immutable recorded-late-arrival facts, and whether the provisional clearance transitions are acceptable.
2. **G11-S02 through G11-S24 NOT EXECUTED:** Perform .NET compile and unit tests, clean and populated fourteen-migration upgrade, restricted PostgreSQL authority and SQL HMAC tests, source-current session races, no-op checkpoint and replay tests, M5 schedule-change recurrence tests, M10 projection corrections, end-to-end history/browser scenarios, M16 audit-failure rollback, scans and targeted M5/M9/M10/M16 regression.
3. **G11-S25 NOT EXECUTED:** Freeze the final SHA, preserve raw evidence including failures and independently reconcile gate coverage before a separate explicit acceptance decision.
4. **Conflict provenance incomplete:** M10 revision and digest alone do not identify every conflicting container and retained event. Complete G11-P03 lineage remains unresolved.
5. **Inherited SQL reader trust boundary:** API role has broad existing business-table SELECT privileges. M11 HTTP tenant checks alone are not independent database row-level read isolation.
6. **Time and concurrency semantics:** An exception-list creation cutoff does not freeze mutable state; history continuation can see newly appended records. Test concurrent status and history updates, session revocation, audit failure and evaluator restart on the exact final source.

## Verification order and restrictions

Use a clean, frozen E:-backed checkout and disposable PostgreSQL environments. Execute the focused local contract/build/SQL/browser checks with raw evidence, then the full G11 scenario matrix and relevant accepted-gate regressions. Do not treat static assertions, committed test source or this audit as a PASS. No GitHub Actions are necessary or authorized for this task.

Protected `main`, the M9 worker, accepted M10/M16 migrations and GitHub workflow configuration remain unchanged. The M9 sustained transport hold remains active: ARRI/DEPA parked, 10,000 unresolved-work-item cap binding, M10 ETA null. No merge, deployment, release or destructive cleanup occurred.

**Fifth-pass decision: G11 acceptance pending.**
