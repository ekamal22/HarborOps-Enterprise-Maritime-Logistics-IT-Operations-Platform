# G13/G15 Atomic Acceptance Coverage and Behavioral Verification

**Latest G13/G15 local closure verification (2026-11-11):** Source-locked local campaign `c2` (not a GitHub Actions result) PASSED every technical group at `3e63bd0e71bdf4a80cd8ea1d27e7cceca3c37524`, after remediation of the round 1 findings: 565 TRX tests passed with none failed or skipped, 29 of 29 G13 source mutants, 4 of 4 SQL security mutants and 17 of 17 G15 mutants killed with clean controls, 8 of 8 process SIGKILL phases recovered or verified, 54 of 54 Chromium, Firefox and WebKit browser case executions (the browser job passed on its third attempt; the first two failures were harness defects and are recorded), and 257 atomic assertions reconciled. G13 performance is measured under the restricted API role: realistic skewed 85,000 shipments p50 8.8 s and p95 9.3 s, reference 170,000 p50 9.2 s and p95 11.0 s against 15 s and 20 s; four simultaneous builders return 4 of 4 HTTP 200 at 85,000 and 4 of 4 HTTP 413 at 170,000 with no 503. Independent S25 round 2 at `3e63bd0` returned **NOT PASS** for both gates (narrow and closable: no security, isolation or data-loss defect; mutation and witness gaps, thin concurrency headroom beyond four builders for G13, and three unproven clauses for G15; see the [G13 review](G13_S25_INDEPENDENT_REVIEW_ROUND2_20261111.md) and [G15 review](G15_S25_INDEPENDENT_REVIEW_ROUND2_20261111.md)); round 1 at `5bcb7e5` also returned NOT PASS for both gates. The G13 and G15 acceptance decisions: **acceptance pending**. See the [round 2 results](G13_G15_LOCAL_CLOSURE_RESULTS_C2_20261111.md) and the [round 2 review brief](G13_G15_S25_REVIEW_BRIEF_ROUND2_20261111.md). No merge, deployment or activation is authorized.
**Previous G13/G15 gate-decision checkpoint (2026-11-10, hosted run; the local closure verification above supersedes its evidence, its decisions stand until new decisions are recorded):** [Full source-locked run 42745334415](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415) PASSED 15/15 jobs at `15cabd8c542159753c927c2bd65b4346223e84f6`, with 543/543 passed TRX tests, 227/227 technical assertion witnesses, 48/48 technical scenario checks and artifact integrity PASS. The separate [G13/G15 gate decisions](G13_G15_FORMAL_GATE_DECISIONS_20261110.md) record **G13 NOT ACCEPTED and G15 NOT ACCEPTED**, after [individual S25 assessments](G13_G15_REVIEW_MATRIX_20261110.md). G13 permanent tenant quota/idle cleanup enforcement and some exact mutation and HTTP-outage evidence remain open. P10 verification condition is satisfied; P12 cleanup/deletion remains disabled. Protected `main` is unmodified. No merge, deployment or activation is authorized.
**Historical technical checkpoint (2026-11-10):** [15-job run 42727554951](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed at `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75`, including 541 passing TRX tests, no failures/skips, and 48 passing technical scenario contract checks. The separate 242-assertion audit has 224 technical witnesses present, 3 missing, 7 policy-review and 8 independent-review items. Full G13/G15 acceptance remains pending. [Current source-locked evidence](G13_G15_VERIFIED_EVIDENCE_20261110.md).

**Disposition:** Successful technical verification is distinct from milestone acceptance. This is the live atomic-coverage interpretation. The original evidence candidates are preserved in the repository history.

## Latest executed checkpoint

[Preflight 42726180647](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42726180647) and [full verification 42727554951](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) both passed at `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75`. The full run completed 15 of 15 jobs, 541 TRX passed, 0 failed/skipped, and 48 technical scenario contract checks passed. Evidence integrity PASS.

## Assertion inventory and actual verification

The [242-assertion inventory](G13_G15_ASSERTION_INVENTORY_20261109.json) maps 227 assertions to candidate technical witnesses and 15 to policy or independent reviews. The actual run's `assertion-audit.json` records:

| Executed disposition | Assertions |
| --- | ---: |
| Technical evidence present | 224 |
| Missing technical evidence | 3 |
| Policy review | 7 |
| Independent review | 8 |
| **Total** | **242** |

The missing IDs are `G13-S08-A04` (default-off M10 and opt-in M11 test witness), `G13-S09-A03` (late-worker/out-of-order tracking witness) and `G13-S09-A04` (late-worker correction-generation witness). They are evidence-mapping gaps in this run, not three failed application tests. `full_automated_assertion_coverage` is `false` even though the non-enforcing audit job returned success. No missing check is waived.

## Verified behavioral coverage in the executed campaign

- G15: live notification list/count/detail/action denial after 11 distinct session, account, membership and organization changes; a queued acknowledgment loses authority after a committed change.
- G13: previously persisted report tokens and overview after 12 independent revocation modes, plus an organization-lock queued report that must not publish after a committed security-version change.
- G13/G15: direct SQL denials for restricted API and worker roles attempting to enumerate personal replay/outbox data or modify KPI materialization.
- G13: origin, destination, active/future voyage assignment and allowed record-source filtering with exact member IDs; invalid UTC and filter requests leave report state unchanged.
- G13: empty tenant and Draft-only denominator behavior; exact percentage rendering for 1/3, 2/3 and 8/9 in the browser.
- G15: three approved event actions versus Reevaluated, InputUnavailable and Acknowledged; unique immutable history IDs; low-noise MarkRead, exactly-once acknowledgment audit and same-key replay; archived target scope and historical event preservation.
- UI: notification dependency error/retry, delayed old-organization response denial, KPI empty-data error/retry and exact displayed ratios.
- G13 performance: the existing 170,000-shipment / 30-successful-report sample retains the approved p50 <= 15 seconds and p95 <= 20 seconds envelope. A separate final overcapacity query seeds 16,665 extra Draft shipments, proves the 500,000-member cap fails closed with HTTP 413, and verifies that its failed transaction does not replace the prior committed report.
- G13 measured provisional resource ceilings: 2 GiB additional report-table storage across the 30-report exercise, and 1 GiB peak working set. These are verification bounds justified by the prior 1.63 GB final-size and 609 MB peak-memory observation, **not approved permanent retention or production service limits**.


## Accepted verification limits and pending decisions

The 30-report 170,000-shipment preflight achieved p50 13.531 seconds and p95 14.368 seconds, with 14.473 seconds maximum under the 15-second SQL timeout. This leaves limited timing headroom. The 500,001-member cap test correctly failed closed. Measured test bounds do not authorize permanent storage, quota, retention or deletion limits.

M15 P01-P09, P11, P13 and P14 are approved. P10 is conditional on evidence reconciliation; P12 permits provisional retention periods with cleanup disabled. G13-S25 and G15-S25 need independent raw-evidence reviews and explicit decisions. The full gate ledger still records one PASS, 23 BLOCKED and one NOT EXECUTED for each milestone.

## Verification and integration boundary

[Raw retained evidence](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951/artifacts/13722934430) includes `assertion-audit.json` and the source digest manifest. Any future rerun must be separately authorized and source locked. This documentation-only update does not require a new runtime campaign. No merge, deployment, production activation, migration or cleanup is authorized.
