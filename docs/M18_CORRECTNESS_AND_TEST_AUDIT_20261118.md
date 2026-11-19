# M18 Independent Correctness and Test Audit

**Date:** 2026-11-18
**Scope:** M18-01 through M18-04 candidate source at `6a03d492142eb159c0da9468667b067da6df3e6c`, reviewed against the approved policy register M18-P01 to P20, the security and transaction contract and the 25-scenario G18 plan.
**Status:** Technical audit record. It is not an independent S25 review and not a gate acceptance.

## 1. Method

The candidate was read in full (domain policy, store, endpoints, migration, role provisioner, frontend, localization and its two unit-test files) and then exercised, not assumed. Every claim below that a behaviour is correct rests on a test that was executed against a real disposable PostgreSQL server, the real restricted `harborops_api` role, the real HTTP pipeline or a real browser. Where an earlier attempt failed, its log is retained next to the passing one.

## 2. Findings in the candidate

| ID | Severity | Finding | Root cause | Correction | Witness |
| --- | --- | --- | --- | --- | --- |
| F-M18-01 | Blocker | The candidate did not compile. `IncidentStore.More.cs` had an unbalanced `await using(` in the read-grant insert (CS1026). | Hand edit never built; the checkpoint said so. | Restructured the statement. | `build-01.log` (failure), `build-02.log` onward |
| F-M18-02 | High | The M16 audit taxonomy was the accepted generic capture function, which stores only `<table>.<operation>` and an empty changed-field list for tables it does not know. The contract requires a fixed event vocabulary and a changed-field allowlist, and one logical event per mutation. | Triggers were attached to new tables without a migration-owned taxonomy. | Added `m18_audit_capture()` in the migration: fixed event names, allowlisted column names only, no values, one event per logical mutation, request correlation id and actor context carried. The accepted M16 function is untouched. | `M18DatabaseTests.EachSensitiveMutationAppends...`, mutation `remove-required-audit`, `leak-note-into-audit` |
| F-M18-03 | High | Incident state, version, closure actors and the operation digest could be rewritten by any statement from the runtime role because table-level UPDATE was granted and only application code enforced the transition rules. | No database-side guard, broad grants. | Added `m18_incident_guard()` (immutable identity, version +1, legal state pairs, verified-recovery and distinct-confirmer rules) and column-level UPDATE grants through the central provisioner. | `M18DatabaseTests.ValidSignedScope...`, `RuntimeRoleHolds...`, mutation `drop-database-transition-guard` |
| F-M18-04 | High | Recovery verification ran `SELECT 1`, then inserted two "healthy" probe rows for any service, and the integrity probe only compared event counts. | Placeholder probe. | Recovery now requires the live readiness check, an event-chain and action-history continuity check, service-specific probe codes (`api_readiness_healthy`, `database_readiness_healthy`), a configurable freshness window (default five minutes) and refuses services with no verifiable probe with the explicit code `recovery_unsupported`. | `M18EvidenceTests.Recovery...`, `ForgedFlags...`, `RecoveryEvidenceExpires...`, mutation `skip-recovery-proof` |
| F-M18-05 | Medium | Replaying a Manager create key after the incident was assigned returned the assignee. Auditor list and detail summaries also carried it. | Redaction was applied on only some paths. | One `Redact` rule applied on every Manager and Auditor path. | `M18SecurityTests.ManagerSees...` |
| F-M18-06 | Medium | A note containing `Authorization: Bearer <token>` was accepted. | The marker needed eight characters directly after the colon. | Broader best-effort markers (bearer, basic, cloud key, token prefix, JWT shape). Residual risk stays disclosed under P09. | `M18IncidentPolicyTests`, `M18SecurityTests.ScriptTitles...` |
| F-M18-07 | Medium | Keyset paging used `updated_at_utc`, so an incident edited while a client was paging moved out of the snapshot window and never appeared on a later page. | Mutable sort key. | Paging orders by immutable creation time with a new index. | `M18WorkflowTests.ListAndHistoryPagination...` |
| F-M18-08 | Medium | Audit rows for incident mutations carried the correlation id `database`, not the request id. | The correlation parameter was never passed. | A scoped correlation carrier feeds the transaction-local actor context. | `M18DatabaseTests.EachSensitiveMutation...` |
| F-M18-09 | Medium | No server-issued M17 prefill existed, so the "alert to human confirmed incident" path (P05) was not implemented and `correlation_digest` was never populated. | Missing feature. | `GET .../m17-prefill` returns a finite, server-protected preview bound to organization, user and session; create accepts only that token; evidence digest is recomputable from stored fields. | `M18EvidenceTests.Prefill...`, `EvidenceDigestCanBeRecomputed...` |
| F-M18-10 | Medium | Browser retries of any command other than create drew a fresh operation key, so a lost response could apply the command twice. | Key generated inside each client call. | One key per identical command on an identical version, dropped only on a definitive refusal. | web test `retries reuse an operation key...`, `M18ResilienceTests.Ambiguous...` |
| F-M18-11 | Medium | Mitigation and monitoring gates accepted records from any earlier cycle; the approved `Mitigated -> Investigating` failed-mitigation path was absent. | Existence checks without a cycle boundary; table row not implemented. | Gates look only at records after entering the current state; failed mitigation needs a new note or observation. | `M18LifecycleTests.FailedMitigation...` |
| F-M18-12 | Low | Incidents for `notifications` and `tracking_held` could never reach Resolved and the API reported a generic conflict. | No verifiable probe exists for them. | Explicit `recovery_unsupported` error and localized message. The underlying policy gap is reported in section 5. | `M18EvidenceTests.Recovery...` |
| F-M18-13 | Low | No way to list or revoke Auditor read grants from the product. | Missing endpoint and UI. | Administrator list endpoint and a revoke control. | `M18ContractTests.AdministratorSees...`, browser roles test |
| F-M18-14 | Low | Turkish text was missing for evidence kinds and several error codes; service codes appeared as raw tokens; stale-cursor errors left a dead button. | Incomplete catalog. | Catalog and UI corrected; dedicated catalog tests added. | `incidents.i18n.test.mjs` |
| F-M18-15 | Low | Eleven accepted migration tests hardcoded the head migration count (19). | Adding a migration changes the count. | Updated to 20 (and the pending-migration count 6 to 7). No assertion was weakened. | neighbour regression, shard A |
| F-M18-16 | Low | `MigrationSnapshotTests.Migrations_AreDiscoveredInOrder` lists every migration by name and did not include the M18 migration. Found only by the full neighbour regression, not by the M18 suites. | Same coupling as F-M18-15, missed by the count fix. | Appended the M18 migration to the expected list. | neighbour regression, shard A |

No defect was found in: seven-state transition enforcement, five-role permission matrix, tenant isolation (list, detail, child objects, cursors), signed-scope row-level security, composite tenant foreign keys, append-only history, optimistic concurrency, idempotent replay including ambiguous commits, audit atomicity and the revocation race at commit time. Those were verified, not assumed.

## 3. Test audit

| Observation | Action |
| --- | --- |
| The candidate had two unit-test files and no PostgreSQL, HTTP or browser test. | Added 73 PostgreSQL and HTTP integration tests (including parameterised cases), a browser campaign of 4 journeys in English and Turkish across 3 engines (24 runs), 4 web catalog and retry tests, a 9-step fault journey against a real API process, 15 controlled faults, and source and boundary checks. |
| Status-code-only assertions are weak: a 403 could come from the wrong layer. | Matrix tests also assert unchanged version, event count and audit count, and compare foreign versus unknown bodies. |
| Concurrency tests must really compete. | Parallel requests with one `expectedVersion`; the revocation race holds a real row lock and proves the write is waiting on it before commit. |
| Ambiguous-commit claims need a real lost reply. | A TCP proxy cuts the connection after the server commit and before it. |
| A green test may pass for the wrong reason. | Fifteen controlled faults were applied to a frozen worktree; results in the verification record. |
| Fixtures must not make negatives trivial. | Every negative is paired with a positive on the same path. |
| Browser witnesses must inspect rendered state. | Specs assert visible text, absent data, request counts and layout, in both locales, in three engines. |

Test and harness defects found and fixed during the work (all logs retained): login helper used another suite's password; response bodies read twice; non-unique titles across reruns; brittle exact accessible names; stale-view race after an action click; stray evidence directory from an unescaped variable; source checks that matched their own pattern text; a journey script whose digest check was silently left unfixed; a mutation patch that did not compile; a journey that probed readiness through the static web proxy; and a memory-backed PostgreSQL data directory that filled up under the heavy M13 load classes and produced false failures (the heavy shard now uses a disk-backed volume).

## 4. Observations outside M18

- At 375 px width the account page above the incident workspace overflows by three pixels in WebKit with or without M18. The incident workspace itself does not overflow. Left unchanged.
- `scripts/check-planning-docs.mjs` passes on this source.

## 5. Open policy question (not decided here)

Services `notifications` and `tracking_held` have no server-executable recovery probe, so their incidents stay open at Monitoring. Closing them needs either an approved verification route or the exceptional-path privilege rules that M18-P16 and P17 reserve for a separate decision. M18 does not invent one.
