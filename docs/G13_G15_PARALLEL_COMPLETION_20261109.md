# G13/G15 Parallel Acceptance Completion

**Latest verified technical campaign (2026-11-09):** [Run 42569970541](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42569970541) passed all 15 jobs at source `e4bf9cd1527f85f554baf623e90805d96d08ac1a`, with 487 passed TRX cases, zero failed/skipped, all six browser suites passing, and artifact integrity PASS. This is **not** full milestone acceptance. The final ledger reports G13 and G15 each 1 PASS, 22 BLOCKED and 2 NOT EXECUTED. The [source-locked evidence review](G13_G15_SOURCE_LOCKED_EVIDENCE_REVIEW_20261109.md) records outstanding plan, capacity, recovery and mutation proof. Merge, production migration, deployment and runtime activation remain pending.


**Prepared:** 2026-11-09.
**Branch:** `verify/g13-g15-parallel-closure-20261109`.
**Preserved proven source:** `ef2dd697df6c4a2dc8b53c55e7968ac37e41a745` and the nine-job [technical PASS run 42555586856](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42555586856).
**Historical result:** The eleven-job [run 42561392681](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42561392681) FAILED at `895c20650e04f165116daf8a96327907cda09bf9` (8/11 successful; two illegal G13 lifecycle fixture transitions caused G13 SQL suite and mutation-control failures; evidence ledger failed accordingly). This supersedes the former unexecuted status. See the [later final campaign](G13_G15_FINAL_VERIFICATION_20261109.md).

## Parallel verification workstreams

The registered manual GitHub Actions workflow now contains eleven jobs. The source/contract build is the prerequisite for the combined PostgreSQL, browser, G13 faults/mutations, G15 mutations and two distinct M15 load jobs. The G13 170,000-shipment scale and SQL-policy mutation jobs also run on their own disposable PostgreSQL servers. Independent work is not serialized behind unrelated test failures. The ledger executes after prerequisite artifacts arrive and fails closed on a required job failure, absent report, skipped test or checksum error.

| Workstream | Committed candidate checks | Scope boundary |
| --- | --- | --- |
| G13 source truth | Approved twelve A-shipment plus two B-decoy fixture, exact K01-K14 membership keys, four authoritative-version M11 evaluations, resolved factual risk and archive history | Synthetic SQL fixtures do not prove live M11 evaluator event causality |
| G13 snapshot races | Real admin-writer uncommitted transition, independent restricted-API reporting snapshot, committed follow-up and frozen member-set check | Not a complete process-crash or transaction-admission campaign |
| G13 mutation quality | Thirteen source variants covering workflow statuses, arrivals, coverage, current M11 rule risk, equipment freshness, exception lifecycle and opening history; two SQL read-policy attacks | Effective kills must be proven by executed targeted assertions, not compilation failures |
| G15 receipt security | Two-session cursor and replay isolation, immutable original action, stale CAS rejection, exactly one audit entry, suspended-and-restored membership | Browser and HTTP revocation and races still require scenario-by-scenario review |
| G15 failure recovery | Transactional outbox failure, dispatcher delivery failure with retry, no partial recipients or duplicates | Process-termination and after-commit response-loss remain separate mandatory acceptance cases |
| G15 mutation quality | Eight effective SQL variants across source event capture, read RLS, membership version, signed dispatch, fail-closed trigger, session-bound replay, receipt CAS and audit | Each mutated source must build and the intended test must fail |
| G15 large data | One million synthetic immutable source/outbox facts and matching scoped recipient rows; ten 50-row pages, duplicate exclusion and observed latency | This is a disposable index/keyset load, not a million genuine case occurrences |
| G15 concurrency | Two dispatcher instances, 250 additional eligible memberships, exact-once fanout, 100 simultaneously scheduled scoped reads with 32-connection database admission | Only 32 PostgreSQL reads overlap; this is distinct from a genuine 100-connection or browser-session campaign |
| Cross-milestone | Combined .NET/React contracts, restricted PostgreSQL integration, Chromium/Firefox/WebKit notification and KPI journeys | Neither milestone's S25 review is automatically certified |

## Required policy decisions

M13 D-005 Option A is approved. G13 numerical performance, concurrency, report storage and retention budgets still require explicit acceptance. The existing 25-second request deadline, provisional 10-minute report lifetime, member cap and observed three-sample p50/p95 are technical parameters, not approved production SLAs.

M15-P01 through M15-P14 remain approval pending in [the policy register](M15_POLICY_RECOMMENDATIONS_20261109.md). Especially P10 governs transactional failure coupling to M11, P11 keeps production delivery default-off, and P12 does not authorize retention cleanup. No workflow can grant these approvals.

## Historical manual execution

The historical run used the [registered combined verification workflow](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/workflows/verify-m11-m12-combined.yml). The later final branch must be selected instead of this historical eleven-job candidate. GitHub selects the branch SHA automatically. Do not enter a SHA unless the workflow presents such an input. The reviewer must confirm the run head equals the branch head frozen for review.

The post-run ledger verifies source identifiers and checksums on raw artifact files, reads TRX and three-engine browser reports, checks 23 individual source/SQL mutation variants (13 G13 source, 2 G13 SQL and 8 G15 SQL), and records all fifty scenario dispositions conservatively. The runner cannot treat a green job as a complete milestone gate.

## Outstanding acceptance scope

The expanded tests cover material gaps, but do not replace all G13-S01..S25 and G15-S01..S25 mandatory evidence. Remaining examples include verified real-source M11 recurrence/correction attribution, full timestamp edge matrix, five-role HTTP authorization transitions, genuine crash/restart and after-commit response-loss, dedicated one-to-many tenant skew and measured budget approval, exhaustive mutation/secret scanning, and formal reviewer-separation or accepted self-review criteria. The conservative ledger must remain BLOCKED or NOT EXECUTED for those cases until source-locked evidence is present.

The nine-job predecessor remained technically PASS but its ledger recorded G13: one PASS, fourteen BLOCKED, ten NOT EXECUTED, and G15: one PASS, fifteen BLOCKED, nine NOT EXECUTED. These are not superseded by unexecuted test source.

**No production migration, M10 projector activation, M11 evaluator activation, M15 dispatcher activation, merge, deployment or GitHub Actions dispatch is performed by this source preparation.**
