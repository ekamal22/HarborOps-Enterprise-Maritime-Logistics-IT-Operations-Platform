# M11 Local Rule and Exception Lifecycle Decision

**Decision date:** 2026-11-08
**Scope:** M11-P01 through M11-P12, on the combined M11/M12 integration candidate only.
**Disposition:** Policy choices approved under the delegated M11 correctness and policy instruction. This decision is not G11 technical verification, source acceptance, merge or deployment authorization.
**Rule contract version:** `m11-rules-v1`.
**Accepted baseline:** G0 through G10 and G16 on protected `main`, snapshot `6ead1d076946dd96b53401265a31efc35c4a666c`.
**Integration source:** `feat/m11-m12-combined-integration`. Verify the current branch head before relying on any source-specific finding.

## Approved local policies

| ID | Decision | Evidence boundary |
| --- | --- | --- |
| M11-P01 | Use versioned fixed rule codes `EquipmentConflict`, `ArrivalScheduleOverdue`, `RecordedArrivalLate` and `DepartureSlippageRisk`. | Rules are deterministic and not user-defined SQL. |
| M11-P02 | Evaluate only active, non-Draft and non-Cancelled shipments. Archived/cancelled source produces `Inactive`, preserving historical records. | No alerts on inactive shipments and no deletion of retained evidence. |
| M11-P03 | `EquipmentConflict` requires current accepted `m10-equipment-v2` observed conflict; stale or absent projection yields `Stale` or `Unknown`. | A projection digest alone does not identify every conflicting container and event. Complete attributable lineage remains a mandatory G11-S08 technical requirement, not waived by this policy choice. Never imply physical movement or ETA. |
| M11-P04 | `ArrivalScheduleOverdue` means planned arrival is strictly more than the grace interval past due with no recorded Arrived/Completed workflow transition. | Label as schedule overdue and arrival unconfirmed, not actual transport delay. |
| M11-P05 | `RecordedArrivalLate` compares the first recorded M5 Arrived transition with the planned arrival deadline plus grace. | It is a recorded workflow fact, not independently verified physical arrival. |
| M11-P06 | `DepartureSlippageRisk` needs a recorded late InTransit transition, a still-future scheduled arrival and no recorded arrival. | Schedule-risk heuristic only, no predictive ETA. |
| M11-P07 | Set UTC arrival grace and departure tolerance to **60 minutes** each, with strict comparison after the boundary. Equal-to-boundary is not late. | Values are versioned demonstrator rules, not a universal maritime SLA; changing them requires a new rule/policy revision. |
| M11-P08 | Missing schedules and absent workflow evidence produce `Unknown` where appropriate; stale M10 input cannot prove conflict. | Unknown/Stale is not confirmed clear and cannot by itself end an incident. |
| M11-P09 | Keep separate simultaneous rule occurrences and prioritize UI display: EquipmentConflict, RecordedArrivalLate, ArrivalScheduleOverdue, DepartureSlippageRisk. | Presentation priority never removes another rule's data. |
| M11-P10 | Open on qualifying Active. Auto-close Open after verified Inactive. Preserve an Acknowledged incident for Manager review after Inactive. Persist that verified clearance across any later Unknown/Stale. If Active recurs following verified clearance, automatically close the old Acknowledged occurrence and open a new independently actionable occurrence. Unknown/Stale without an earlier verified clearance does not trigger a new occurrence. | Distinct occurrence number and UUID, append-only history and atomic transitions. |
| M11-P10b | A Manager may close an Acknowledged `RecordedArrivalLate` occurrence while its historical condition remains Active, with a mandatory reason. Closing represents completed review, not reversal of the late workflow fact. The same recorded arrival and same threshold do not open another occurrence merely from source-version churn. Changed arrival evidence or threshold can create a new occurrence. | Preserves provenance and human accountability without an unresolvable permanent alert. |
| M11-P11 | Scoped Analyst/Manager/Auditor reads; Manager-only dispositions. Analyst history redacts actor IDs and free-form reasons. Administrator/Support have no implicit business access. | Live selected-organization/session/role and CSRF/Origin protections remain mandatory. |
| M11-P12 | Signed migration-owned SQL procedures control evaluation and disposition; Manager mutation creates immutable M11 history and a transaction-coupled M16 central audit. Direct API-role mutation of exception tables and direct access to signing keys/nonces are denied. | Real restricted-login, HMAC substitution, audit rollback and cross-tenant proof remain mandatory. The inherited wide API SQL SELECT permissions require separate hardening/review before G11 security closure. |

## Implementation changes on the combined candidate

The M11 migration adds `last_confirmed_inactive_at_utc` to store confirmed clearance independently of the currently observed condition. The signed evaluator keeps this field when conditions become Unknown or Stale. New Active after such a clearance closes the previously acknowledged occurrence and opens a new occurrence atomically.

The signed disposition function permits Manager Resolve for the Active `RecordedArrivalLate` historical exception, but keeps its `condition_state='Active'` and historical evidence. An equivalent previously human-resolved arrival/threshold is not reopened by later source version changes. The frontend exposes Resolve for this case and preserves the recorded late condition label.

The additive M11 migration is independently numbered `20261107180000_ShipmentExceptions` and the M12 export migration is `20261107230000_M12ShipmentExportAudit`; the combined candidate expects fifteen migrations in timestamp order.

## G11-S01 interpretation and remaining obligations

The twelve M11 policy groups now have a recorded decision, including explicit P10 recurrence and immutable-history disposition. **G11-S01 policy approval is recorded but G11-S01 is not marked PASS** in the absence of independent gate reconciliation. G11-S02 through S25 remain NOT EXECUTED until source-locked results exist.

Technical requirements still open include complete M10 conflict event/container lineage, independent database read isolation for M11's current API SQL reader privileges, live pagination over mutable lifecycle state, test execution against a restricted PostgreSQL role, concurrency/restart/outage behavior, UI and three-engine browser regression, security mutants/scans, and independent G11-S25 evidence review. A policy decision is not a waiver of any of these.

M12-P01 through M12-P16 are not approved by this M11 decision. G12 remains independently pending.

## Security, operations and authorization

The M9 hold is unchanged. ARRI and DEPA events stay parked, sustained transport processing is held, the unresolved-work-item cap remains 10,000, M10 ETA remains null and the projector is disabled by default. M11 reevaluation remains opt-in and cannot release the M9 hold.

No GitHub Actions, merge to protected `main`, deployment, production migration or cleanup is authorized by this record. Historical M11 audits and unverified test results remain unchanged as historical records.

**Status:** Policy recorded on the combined source candidate; G11 technical readiness and acceptance pending.
