# Reproducible Demonstration Scenarios

## Demo dataset contract

A fixed seed produces two isolated organizations, role-specific users, realistic-looking but fictional ports/routes/containers, event histories, expected KPI values, and service incidents.

No live customer or carrier data is required. The seed version and expected outputs must be tracked with the code.

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

The complete D01-D06 business and incident scenarios remain planned; none has been executed as a full product acceptance demonstration. Separately, the M0 technical baseline and its browser, migration and database-failure smoke scenarios were locally verified at code revision `4fb34eb` (see [M0 Verification](M0_VERIFICATION.md)). G0 is verified, acceptance pending. Local demonstration fixtures, screenshots, logs and caches must follow [Local Storage Policy](LOCAL_STORAGE_POLICY.md).
