# Reproducible Demonstration Scenarios

**Verification reference (2026-11-10):** [G13/G15 15-job technical campaign](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed at `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75` (541 TRX passed, no failures/skips, integrity PASS). The atomic evidence audit reports 224 present and 3 missing technical witnesses, plus 15 policy/independent-review items. This is not G13/G15 acceptance or merge authorization. [Current record](G13_G15_VERIFIED_EVIDENCE_20261110.md).

## Demo dataset contract

The planned final demonstration dataset will contain two isolated organizations, role-specific users, realistic-looking but fictional ports/routes/containers, event histories, expected KPI values and service incidents. Those full business entities and datasets are not implemented at M2. G2 separately verified two synthetic organizations and role-specific users in disposable test fixtures.

No live customer or carrier data is required. The seed version and expected outputs must be tracked with the code.

## M1 authentication demonstration (executed in isolated verification)

G1 verification used a disposable synthetic account and executed the [M1 functional matrix](M1_VERIFICATION_PLAN.md), including: sign in, read protected session state, reload the browser, open a second session, log out the first, revoke all, disable the account, confirm old cookies are rejected, and demonstrate safe database-outage behavior. Verify that invalid credentials, CSRF and login-rate-limit negative cases fail without secrets appearing in responses or logs. Prove that key persistence survives API restart and browser origin remains same-origin.

This demonstration is separate from the later complete business scenarios D01-D06 and does not require two organization fixtures, external tracking data, retained Docker volume checks or M24 accessibility tests. The synthetic M1 account, real browser sign-in/logout, session restart, outage fail-closed behavior, global revocation and rejected-cookie replay passed in [M1 verification](M1_IMPLEMENTATION_VERIFICATION.md). G1 was accepted on 2026-10-31 and M1 merged into `main`. The design is in [M1 Authentication Design](M1_AUTHENTICATION_DESIGN.md).

## M2 organization authorization demonstration (verified in disposable fixtures)

G2 verified local organization bootstrap for two synthetic tenants, five fixed role permissions, an authenticated organization switch, denial of foreign organization and member IDs, last-administrator protection, suspension and revocation across browser sessions, and database outage/recovery with durable authorization denial. The [G2 executed record](M2_EXECUTED_VERIFICATION.md) identifies the tested revision and exact suites. These fixtures are not a permanent production tenant seed and do not imply that port, vessel or shipment reference data already exists.

## Scenario D01: Shipment from registration to arrival

**Preconditions:** An authorized analyst is logged into organization Alpha and the demonstration data is loaded.

1. Create shipment with origin Istanbul and destination Rotterdam.
2. Link container and voyage with an expected arrival window.
3. Submit departure and transshipment events through simulator.
4. Inspect event provenance, timeline ordering, and current state.
5. Complete arrival milestone.
6. Confirm the active/completed shipment counts update with documented KPI rules.

**Pass condition:** API, persisted events, timeline, and dashboard agree.

## Scenario D02: Delayed milestone and alert

1. Start from a known on-time shipment.
2. Inject a late arrival estimate that meets the documented delay rule.
3. Confirm an exception is generated with an explanation.
4. Verify only appropriately authorized recipients see the notification.
5. Acknowledge and investigate the exception.
6. Replay the same event; confirm no duplicate exception or alert.

**Pass condition:** Delay classification, recipient routing, and replay behavior are correct.

## Scenario D03: Ordering and correction

1. Deliver two events in reversed receipt order.
2. Verify the timeline uses documented occurrence/source ordering.
3. Deliver a linked correction to an earlier milestone.
4. Rebuild the shipment projection from accepted history.
5. Compare state and KPI values to expected fixtures.

**Pass condition:** History is preserved and recomputation is deterministic.

## Scenario D04: Cross-organization isolation

1. Create a shipment in organization Alpha.
2. Authenticate as a user limited to organization Beta.
3. Attempt direct lookup, list filtering, export, route map, and modification using Alpha identifiers.
4. Verify no sensitive data is returned and no changes are made.
5. Confirm audit behavior follows the security policy.

**Pass condition:** All foreign-resource attempts are denied without leakage.

## Scenario D05: Infrastructure incident

1. Run the fully instrumented local stack.
2. Interrupt the worker after it records an event inbox receipt.
3. Verify monitoring detects the condition and an incident is opened.
4. Inspect correlated structured logs and processing lag.
5. Restore the worker.
6. Verify the event is processed exactly once in business effects.
7. Complete root-cause analysis and close the incident with evidence.

**Pass condition:** No silent data loss, no duplicate business effect, and complete incident trail.

## Scenario D06: Recovery rehearsal

1. Seed a known dataset and compute reference checksums/counts.
2. Create a database backup.
3. Restore into a clean isolated instance.
4. Run migrations if required.
5. Verify reference records, event state, projections, API smoke tests, and totals.

**Pass condition:** Restored state passes the documented integrity checks.

## Recording template

For each scenario: scenario ID, run date, git SHA, seed version, environment, steps observed, screenshots or logs, expected versus actual, issues, and status.

The complete D01-D06 business and incident scenarios remain planned; none has been executed as a full product acceptance demonstration. Separately, the M0 technical baseline and its browser, migration and database-failure smoke scenarios were locally verified at code revision `4fb34eb` (see [M0 Verification](M0_VERIFICATION.md)). The hardened M0 implementation was reverified on `fec7429415fede121db8a4a34f0df72cd4fac609` by GitHub Actions run #2, and G0 was accepted on 2026-10-31. The later D01-D06 scenarios remain planned. Local demonstration fixtures, screenshots, logs and caches must follow [Local Storage Policy](LOCAL_STORAGE_POLICY.md).

## M9 demonstration boundary (G9 accepted, full product demo pending)

Accepted M9 provides processing infrastructure to claim committed M8 receipts, reconcile an active same-organization equipment identity and event-time-valid shipment assignment with a receipt-unique effect, or park unresolved references in `NeedsReconciliation`. A Manager may request bounded, attributed redrive. G9's source-locked technical, integration, security and browser verification was accepted on 2026-11-05; this does not mean the complete D01-D06 product demonstration has run. M10 projection timelines, delay exceptions, notifications and KPI screens remain later work. See [M9 evidence](M9_EXECUTED_VERIFICATION.md) and [G9 closure](M9_G9_GATE_CLOSURE.md).

**M9 decision and demonstration boundary:** The six [M9-01 contract decision](M9_LOCAL_CONTRACT_DECISION.md) choices were approved on 2026-11-05 with a mandatory transport-backlog operating condition. Passing technical and browser evidence does not make transport-event processing suitable for sustained deployment. Do not enable that traffic until M10 or another explicitly approved mechanism manages the parked `NeedsReconciliation` backlog. G9 independent review, acceptance and merge are complete; see [G9 closure](M9_G9_GATE_CLOSURE.md). M16/G16 was independently reviewed, accepted and merged on 2026-11-06. The planned G10 correction demonstration must now include an authorized Manager correction with exactly one atomic M16 audit event, and restricted SQL forgery denial. No new-source correction-security demonstration has yet executed or been accepted. See [G16 closure](M16_G16_GATE_CLOSURE.md) and [M10 implementation](M10_G10_CORRECTION_AUTHORITY_IMPLEMENTATION.md).
