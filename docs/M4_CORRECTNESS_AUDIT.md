# M4 End-to-End Correctness Audit

**Scope:** M4 voyage and container reference management, including cross-module M0-M3 contracts.
**Branch:** feat/m4-voyage-container-references
**Baseline:** G0-G3 accepted on main.
**Review type:** Source and authored-test audit. No build, PostgreSQL, Docker, browser or GitHub Actions execution.
**Outcome:** Implemented source and expanded coverage committed. G4 remains OPEN; acceptance pending.

## Audit method

The repository tree, M4 changes relative to main, domain models, EF mappings and migration, HTTP endpoints, authorization and persistence code, browser client/workspace, existing tests, runner, evidence checker, and documented G4-S01 through G4-S25 requirements were reviewed. Assertions were added against observed gaps. No unexecuted test is reported as PASS.

## Findings and corrections

| ID | Area | Finding | Correction / disposition |
| --- | --- | --- | --- |
| CA-01 | SQL archive integrity | An Active row could be archived by direct SQL while changing another mutable field in the same statement. API ordinarily did not do this, but the trigger failed to forbid it. | M4 migration trigger now requires archive-only changes. Added direct SQL negative tests. Must execute against PostgreSQL. |
| CA-02 | Container frontend | Submitting an unchanged display name could create a no-op update and increment the optimistic version. | Changed container update payload to emit no patch for an unchanged value. Runtime verification pending. |
| CA-03 | Browser pagination | A delayed page response needed a request-token guard in addition to the data-generation guard. | Added token checks to both success and error paths, plus a browser tab-switch/pagination interleaving test. Runtime verification pending. |
| CA-04 | Voyage parent selection | The form only retrieved the first 100 active port and vessel references, excluding valid parents beyond that limit. | Introduced paginated parent retrieval with duplicate suppression, cursor-loop detection and an explicit bound rather than silent truncation. Runtime verification pending. |
| CA-05 | Session boundary matrix | Previous G4 evidence referenced M3 session tests instead of observing M4's ten routes under invalidated sessions. | Added four invalidation variants, each exercised against all ten M4 methods. |
| CA-06 | Tenant and roles | Prior tests covered selected cases but not the entire ten-route scope/role matrix. | Added anonymous, foreign organization, selected organization change, five-role method and suspended-organization cases. |
| CA-07 | List/filter correctness | Successful multi-page container and voyage filtering, literal wildcard searches, and stale page behavior were not fully represented. | Added HTTP and browser pagination and filter tests, cross-tenant row checks, and a stale-page interleaving test. |
| CA-08 | SQL and migration | Direct immutable field rules, fresh-schema migration and drift verification lacked dedicated comprehensive tests. | Added fresh isolated-schema and additional direct SQL trigger/check tests, preserving seeded historical upgrade and rollback coverage. |
| CA-09 | Request validation | Incorrect Origin with a valid antiforgery token and additional hostile payload/filter variants were not explicitly covered for both M4 resources. | Added real M4 HTTP assertions. |
| CA-10 | Foreign record probing | Foreign container archive and foreign-scope list paths lacked explicit assertions. | Added foreign DELETE and list-denial checks and preservation of the original row version/status. |
| CA-11 | Test evidence mapping | Several G4 index rows relied on proxies that did not execute M4-specific scenarios. | Updated evidence requirements to use the new named G4 tests. Evidence checker remains non-authoritative for acceptance. |

## G4 coverage trace

Each row below identifies authored evidence targets, not successful execution.

| Scenario | Current authored evidence | Execution |
| --- | --- | --- |
| G4-S01 | Expanded independent ISO checksum vectors; D-003 gate in evidence checker | NOT RUN; D-003 pending |
| G4-S02 | Synthetic kind and equipment catalog unit tests; container browser workflow | NOT RUN |
| G4-S03 | Voyage UTC, schedule, progression and transition unit tests; SQL invariants | NOT RUN |
| G4-S04 | Fresh isolated M0-M4 schema, EF snapshot/drift, migration discovery | NOT RUN |
| G4-S05 | Historical M3 upgrade, repeat, rollback and reapply | NOT RUN |
| G4-S06 | Foreign tenant FK, direct immutable route/code/kind, status, window and version SQL tests | NOT RUN |
| G4-S07 | Two-tenant fixture preparation and repeatability checks | NOT RUN |
| G4-S08 | Anonymous and four invalidated-session cases applied to all ten M4 routes | NOT RUN; absolute-expiry case requires additional confirmation |
| G4-S09 | All ten routes without selected organization; server selection-change tests | NOT RUN |
| G4-S10 | Foreign organization substitution for all ten routes | NOT RUN |
| G4-S11 | Existing foreign voyage UUID read/update/delete isolation | NOT RUN |
| G4-S12 | Existing foreign container UUID read/update/delete, foreign list denial, unchanged row | NOT RUN |
| G4-S13 | Manager/Analyst/Administrator/Support/Auditor ten-route permission matrix | NOT RUN |
| G4-S14 | Membership downgrade, suspended organization, browser live revocation and outage checks | NOT RUN; deterministic queued-write ordering still needs execution review |
| G4-S15 | Bad query/body, oversize/duplicate JSON, CSRF, real foreign Origin | NOT RUN; comprehensive malformed-input fuzzing remains an enhancement |
| G4-S16 | Voyage creation, progression, archive, restore, schedule and filters | NOT RUN |
| G4-S17 | ISO and synthetic container lifecycle, immutable SQL fields, browser form recovery | NOT RUN |
| G4-S18 | Duplicate reservation, archived identifiers, two simultaneous writes | NOT RUN |
| G4-S19 | Tenant-scoped 29-row pagination, 27-row browser paging, voyage filters and literal wildcards | NOT RUN |
| G4-S20 | Concurrent version changes, parent archival race, SQL invariants | NOT RUN; deterministic lock interleaving needs runtime observation |
| G4-S21 | Archive/restoration, historical schedules, forbidden status transition and direct SQL archive-only semantics | NOT RUN |
| G4-S22 | Manager and Analyst Playwright workflows, validation recovery, pagination on three browser engines | NOT RUN |
| G4-S23 | Delayed foreign response, logout while pending, live role change, stale pagination, 503/recovery | NOT RUN |
| G4-S24 | Whole-solution M0-M4 .NET/React/browser regressions in the local runner | NOT RUN |
| G4-S25 | Clean-build/scan, provenance, E:-backed storage checks, stage index and teardown | NOT RUN; independent review required |

## Remaining correctness risks

1. M4 has no executable .NET or TypeScript build evidence at the current commit. Compile-time errors remain possible.
2. The EF snapshot was manually amended. The isolated fresh migration and model-drift tests are mandatory and have not been executed.
3. The migration's PostgreSQL functions, composite foreign keys, triggers, and rollback have not been validated by a live database. The SQL archive-only correction must be exercised on a disposable schema.
4. Concurrent administrator changes, queued writes and parent archival must be checked with controlled real database interleavings. Task.WhenAll outcomes alone are not sufficient to establish deterministic serialization in every ordering.
5. The three-browser suite has not run on Chromium, Firefox or WebKit. Assertions may require correction against actual rendered behavior.
6. D-003 remains a proposed identifier policy, not an accepted decision. It cannot be implicitly approved by passing syntax or tests.
7. Existing and newly authored tests cannot establish G4 acceptance until the full 25-scenario contract is observed at one final revision and its raw artifacts independently reviewed.
8. Parallel M5 schema work, if started separately, must be reconciled and reverified before integration.

## Required execution

Use only an authorized E:-backed checkout and disposable resources. Confirm Docker Desktop storage is actually on E:, then run the committed local verification entrypoint:

    bash scripts/verify-m4-local.sh

The runner must execute release build, fresh/repeated PostgreSQL integration, migration and snapshot tests, M0-M3 regressions, browser specs in all three engines, real database outage/recovery, image/dependency scans, provenance and cleanup. Preserve raw .trx, browser JSON, SQL, scan and stage logs. Repair every failure and rerun the complete matrix at the final SHA.

No GitHub Actions run or main-branch merge was performed for this audit. Do not advance G4 status on the basis of this source review.
