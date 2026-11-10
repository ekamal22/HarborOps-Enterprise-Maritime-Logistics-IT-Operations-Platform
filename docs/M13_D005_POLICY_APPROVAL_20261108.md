# D-005 Operational KPI Policy Approval

**2026-11-10 successor checkpoint:** The original design, decision or source review below remains a historical record for its stated date and source SHA. A later [G13/G15 15-job technical campaign](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed at `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75` with 541 passing TRX cases, but the 242-assertion audit still identifies 3 missing technical witnesses and 15 policy/independent-review items. See [current verification and acceptance status](G13_G15_VERIFIED_EVIDENCE_20261110.md). No main merge, release, activation or deletion authority follows from the technical result.

**Decision:** D-005, Option A, approved with safeguards
**Approved on:** 2026-11-08
**Policy version:** m13-kpi-v1
**Scope:** M13 planning branch only, plan/m13-operational-kpi-dashboards-20261108
**Accepted source baseline:** main at eb529368a2447061fc1838cd3967b0ffedae669d
**Status:** POLICY APPROVED. Implementation, verification execution, G13 acceptance, merge, release and deployment are not approved by this record.

## Approved Option A

The fourteen K01-K14 metric definitions, source classes, eligibility/exclusion rules, deterministic fixture expectations and half-open UTC reporting windows in [the M13 KPI catalog](M13_D005_KPI_DECISION_PROPOSAL.md) are approved as the versioned m13-kpi-v1 policy baseline. The proposed fixture arithmetic remains unexecuted and must be independently reconciled to source rows before a G13 PASS.

1. **Source authority:** M5 workflow records and status transitions, M6 tenant-scoped assignments, fresh M10 tracking evidence and current M11 rule evaluations/occurrences. No physical movement or predictive ETA may be inferred from parked M9 transport events or unverified sources.
2. **Arrival metrics:** Keep strictly on-time recorded workflow arrival distinct from arrival within the M11-approved 60-minute grace. Equal to the boundary passes the grace test; strictly after is late. Use accepted m11-rules-v1 thresholds unchanged.
3. **Risk labeling:** Separately show recorded arrival lateness, schedule overdue with unconfirmed arrival, departure slippage risk and equipment conflict. Never combine them into an unqualified physically delayed count. Manager closure of a recorded-late occurrence does not reverse its factual condition.
4. **Historical plan safeguard:** Only include an Arrived transition in historical on-time denominators when the planned arrival applicable to that recorded transition is proven. If that reference is unavailable or changed without reliable history, classify the source plan as unknown, disclose the exclusion and do not fabricate it. A new plan-history mechanism requires separate approval.
5. **UTC and precision:** Default previous seven complete UTC days; explicit UTC half-open [fromUtc,beforeUtc) windows up to 366 days; exact integer numerators and denominators, with ratios rounded only for display. Zero denominator means null / Not enough data, not zero percent.
6. **Current versus historic:** Current shipment, exception and risk counts are snapshots of the report transaction; historical completions, cancellations and opening trends derive from recorded immutable events. Archived shipments are excluded from current active metrics but valid historical events remain in their respective event-window counts.
7. **Uncertainty:** Unknown, Stale or missing M10/M11 data is never counted as proven risk-free. Display eligibility, freshness coverage, exclusions and confidence. Preserve M10 projector default-off, M11 evaluator opt-in and null ETA.
8. **Consistency:** Use one tenant-scoped REPEATABLE READ report build with a bounded, short-lived server-owned membership snapshot for exact total-to-drill reconciliation. Bind protected tokens to actor, session, selected organization, filter, catalog version and expiry, and reauthorize every drill. Do not use M12 live cursors as frozen report snapshots.
9. **Authorization:** Dedicated typed dashboard read permission. Analyst and Manager scoped allow; Auditor only with a current explicit organization-scoped read-only grant; Support and Administrator denied by default. No implied CSV export, mutation or administrative bypass.
10. **Safety and confidentiality:** Preserve M11 restricted PostgreSQL read scope/RLS, M16 protections, session/membership revocation, no-store responses and the ban on sensitive data or cross-tenant caches. The M9 ARRI/DEPA NeedsReconciliation hold and 10,000 unresolved-work cap remain unchanged.

## Engineering parameters reserved for measured verification

D-005 approves the business logic and reporting consistency approach, **not unmeasured numerical resource limits**.

| Parameter | Approved direction | Still required |
| --- | --- | --- |
| Report expiry | Short-lived, with 10 minutes as a provisional target only | Validate TTL, cleanup, retention and multi-user contention before fixing a guaranteed expiry |
| Report storage | Complete atomic report and scoped member set; never silently cap an otherwise reported complete total | Measure/choose row representation, tenant and principal quotas, storage budget, cleanup and failure behavior |
| Query and API budgets | Bounded, compatible with current reverse proxy envelope | Measure actual p50/p95, CPU/memory, query plans, lock contention and timeout thresholds on realistic data |
| Security permissions | Dedicated scoped dashboard grant, not broad Auditor access | Specify and verify Auditor grant storage/revocation and constrained M11 read authority under actual restricted SQL roles |
| Historical accuracy | Source-proven applicable planned arrival or explicit unknown/exclusion | Reconcile accepted M5 update rules, legacy data and seeded transitions |
| Expected fixture values | Proposed twelve-shipment tenant A and two cross-tenant decoys are normative test targets | Independently replay and reconcile every membership, timestamp, denominator and edge case |
| Concurrent M15 work | Separate milestone ownership | Coordinate EF migration ordering, shared role definitions, API registrations, React navigation and integrated tests |

Changing a KPI formula, source authority, permission boundary or snapshot consistency requires a further versioned policy decision. Final numerical limits and implementation mechanisms must be justified by recorded measurements before their corresponding G13 scenarios can pass.

## Milestone and verification authorization

- **D-005:** APPROVED, Option A with safeguards, 2026-11-08.
- **M13-01:** Policy approval prerequisite met; fixture/source reconciliation, exact reporting contracts and verified numerical resource boundaries remain pending. M13 application implementation is not authorized by this request.
- **G13-S01:** POLICY APPROVED, RECONCILIATION PENDING; not marked PASS solely by recording this decision.
- **G13-S02 through S25:** NOT EXECUTED. G13 itself NOT ACCEPTED.
- **GitHub Actions:** Exclusively authorized environment for M13 verification **after M13 implementation is separately authorized**. Use manually dispatched, source-locked jobs, isolated disposable PostgreSQL servers for restricted-role tests, ephemeral runners, sealed redacted evidence and an independent S25 review. Local test execution is not an alternative.
- **This change:** Documentation-only. Do not create/enable/start/rerun GitHub Actions, implement application code, alter migrations, merge, deploy or run production operations.

M15 planning and implementation remain independent. This approval records no new M15 policy or cross-milestone acceptance.
