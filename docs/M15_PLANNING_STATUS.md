# M15 Scoped In-App Notifications: Planning Status

**Current G15/M15 integration checkpoint, 2026-11-11:** Combined source verification remains incomplete. [Run 43090424080](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43090424080) completed FAILURE; its G15 notification mutation, million-receipt and fanout jobs passed but other required combined jobs failed. [Run 43091966161](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43091966161) passed the M13/M15 database group and G15 mutation/load jobs but failed browser integration and still had a pending G13 mutation job at inspection. [Run 43093329982](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/43093329982) at `067e1ab57f33fcabefae55727265499d86daf7ae` passed preflight, combined three-browser UI, process-crash, security and G15 load/fanout; further mutation and combined database checks were still running at inspection. Do not combine results across SHAs. G15-S25 round 3 unconditional approval is not established; **G13/G15 acceptance pending**. M15 notification delivery remains disabled by default; its enablement in the authorized disposable browser verification stack is not production activation. P12 retention deletion remains disabled. Protected main unchanged, with no merge or deployment. See [run register](G13_G15_POST_C3_HOSTED_RUN_REGISTER_20261111.md) and [current remediation](G13_POST_C3_REMEDIATION_20261111.md). Older milestones in this file are historical, not current acceptance.

**2026-11-10 final G13/G15 review:** The [final source-locked run](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42745334415) PASSED 15/15 jobs, 543 tests and 227/227 technical assertion witnesses at `15cabd8c542159753c927c2bd65b4346223e84f6`. The separate [formal gate decision](G13_G15_FORMAL_GATE_DECISIONS_20261110.md) is G13 NOT ACCEPTED and G15 NOT ACCEPTED following [S25 reviews](G13_G15_REVIEW_MATRIX_20261110.md). G13 permanent tenant capacity/idle cleanup enforcement and exact G13/G15 mutation/HTTP-outage proof remain pending. P10 conformance is supported; P12 deletion remains disabled. All earlier dates and SHAs in this file describe historical checkpoints only. No merge or production authorization.
**Source-locked successor checkpoint, 2026-11-10:** [G13/G15 workflow 42727554951](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/actions/runs/42727554951) passed 15/15 jobs on `d6ef8dd7eedc1ec1ca480fc17bee500c8b288d75` (541 tests passed, 0 failed/skipped). The separate atomic audit found 224 technical witnesses, 3 missing, 7 policy-review and 8 independent-review assertions; G13 and G15 acceptance remain pending. [Current detailed status](G13_G15_VERIFIED_EVIDENCE_20261110.md). Earlier source-specific reports below remain historical.

**Date:** 2026-11-08  
**Planning-state snapshot (2026-11-08):** Planning had no runtime implementation or G15 verification. Subsequent source implementation and technical checks are recorded in the current checkpoint above; G15 acceptance remains pending.  
**Reviewed main:** eb529368a2447061fc1838cd3967b0ffedae669d  
**Planning branch:** plan/m15-scoped-notifications-20261108  
**Required entry:** G11 accepted. **Canonical dependencies:** M15-01 -> M15-02/M15-03 -> M15-04 -> G15.

## 1. Observed repository and authority

The accepted main baseline contains M0-M12 and M16, with G0-G12 and G16 formally accepted. PR #33 integrates M11/M12, PR #34 reconciles their accepted closure, and PR #35 performs repository-wide documentation reconciliation. The authoritative current status is docs/STATUS.md together with docs/M11_M12_G11_G12_GATE_CLOSURE_20261108.md; older audit banners and generic execution-graph gate wording describe historical stages, not present acceptance.

Current read-only inspection covered the repository tree; main and relevant branch heads; docs/PRD.md, REQUIREMENTS.md, ROADMAP.md, STATUS.md, ACCEPTANCE_CRITERIA.md, ARCHITECTURE.md, DOMAIN_MODEL.md, API_CONTRACTS.md, SECURITY.md, DECISIONS.md, OPERATIONS.md, TEST_STRATEGY.md, PARALLEL_DELIVERY_POLICY.md, execution-graph.json and EXECUTION_GRAPH.md; M11 architecture/contract/implementation/verification and G11/G12 closure; G16 and G9 gate closure; and repository-wide reconciliation. The M11 SQL migration, exception store/endpoints, M2 membership and role policy, M1 sessions, M16 audit role provisioning and React organization/shipment workspace were inspected. The remote GitHub connector does not expose the state of any local E: worktree, so no uncommitted local state is asserted.

At planning branch creation, the separate plan/m13-operational-kpi-dashboards-20261108 branch existed and pointed to the same main SHA, with no committed difference yet. M15 did not write to it. Compare again before future integration because its state may change independently.

## 2. Planning deliverables, scope and acceptance distinction

| Document | Purpose | Disposition |
| --- | --- | --- |
| M15_ARCHITECTURE_DESIGN.md | M11-triggered event and recipient rules, outbox, scoped persistence, API, UI, security, retention and M13 ownership | Proposed; not approved |
| M15_IMPLEMENTATION_PLAN.md | M15-01..04 and G15 delivery order, file/module boundaries, integration and migration reconciliation | Proposed; not implemented |
| M15_VERIFICATION_PLAN.md | Full G15-S01..S25 matrix, fixtures, tests, raw evidence and independent review | Planned; none executed |
| M15_PLANNING_STATUS.md | Current baseline, decisions, coordination and blocking ledger | Planning published only |

FR-019 is a Should requirement. Its roadmap phrase mentions shipment and infrastructure exceptions; the present M15 milestone deliberately covers in-app M11-backed shipment exceptions only. Infrastructure alerting and M18 incidents, external delivery and wider tracking events require new approved scope. The current G11 dependency is sufficient for M15 planning; G13 is not a mandatory M15 entry gate.

**Runtime safety:** ARRI/DEPA stay parked, 10,000 unresolved-item cap enforced, M10 projector defaults off, ETA remains null, M11 evaluator remains opt-in. No deployment or production migration is authorized. Any later M15 dispatcher must be a separate opt-in mechanism and may not alter these controls.

## 3. M15 local policy decisions awaiting explicit approval

The entries below are **recommended defaults only**. Neither creating these documents nor opening a planning PR approves them.

| ID | Recommended choice | Why approval is required |
| --- | --- | --- |
| M15-P01 | Only M11 immutable-history Opened, Resolved and AutoResolved produce v1 in-app notices. Acknowledged, Reevaluated and InputUnavailable do not. No infrastructure events yet. | Strict FR-019 and M11 trigger scope |
| M15-P02 | Active Analyst, Manager and Auditor with ViewShipmentData may receive/read personal notices; Administrator/Support cannot. No role gets extra M11 mutation rights. | Five-role entitlement boundary |
| M15-P03 | Recipient audience is snapped from current active membership at first committed fanout, not initial M11 event timestamp. No retroactive joiner backfill after fanout. | Delayed-event membership semantics |
| M15-P04 | Persist membership ID/version with each delivery and require current matching version, live role, org and resource at all reads/counts/mutations. Old alerts remain hidden after revocation and rejoin. | Revocation/reappearance and privacy |
| M15-P05 | Personal Unread -> Read -> Acknowledged terminal receipt lifecycle; Acknowledge implies Read. Never change M11 exception state. | Receipt semantics and user expectation |
| M15-P06 | Unique M11 history UUID outbox key, unique outbox/recipient delivery and session/actor-scoped UUID replay key; DB effect-once under retries. | Identity, recurrence and recovery |
| M15-P07 | Fixed rule/evidence templates, no free-form disposition reason, actor ID, raw tracking payload, source fingerprint or unsupported ETA claim. | Privacy and evidentiary accuracy |
| M15-P08 | GET list default 25 max 50, protected 20-minute user/session/org/filter/cutoff cursor; live mutable state, no snapshot guarantee; same-origin personal PATCH. | Public API and pagination contract |
| M15-P09 | Personal acknowledgment has one transaction-coupled M16 notification.receipt_acknowledged audit event, with no event for MarkRead; failed required audit rolls back. | Audit volume and atomicity |
| M15-P10 | Additive M11 history AFTER INSERT trigger stores outbox intent atomically; trigger failure rolls back the originating M11 transaction rather than silently lose a notice. | New coupling to accepted M11 correctness |
| M15-P11 | M15 dispatcher independently opt-in, DB lease and bounded retries; no automatic historical backfill on rollout, no M9/M10/M11 service enablement. | Operational controls and source history |
| M15-P12 | Proposed online notice retention of 90 days for Unread/Read rows and 30 days after personal acknowledgment; any legal/incident holds and any future absolute limit require separate approval. Immutable M11/M16 evidence untouched; cleanup stays off. | Privacy, archival and legal hold policy |
| M15-P13 | Namespaced notification API and isolated React component; shared shell, permission enum additions, DI, migrations and EF snapshot require prior M13 reconciliation. No M13 KPI derivation from notices. | Parallel workstream ownership |
| M15-P14 | Treat archived/cancelled targets as historical only and authorize current deep link independently; removed or inaccessible targets are hidden without existence disclosure. | Resource lifecycle and broken-link safety |

**Approval ledger (updated 2026-11-10):** M15 P01-P09, P11, P13 and P14 approved, P10 approved conditionally on source-locked recovery conformance, and P12 provisional retention periods approved without cleanup authority. G15 full scenario acceptance remains pending. See [policy decision](G13_G15_POLICY_APPROVAL_20261109.md). Changes to recommendations should amend the local decision record before starting implementation.

## 4. M15 work package readiness

| Package | Current source state | Gate state |
| --- | --- | --- |
| M15-01 | Event catalog, entitlement and dedup rules implemented as candidate defaults; P01-P14 not approved | Policy acceptance pending |
| M15-02 | SQL-only outbox/receipt migration, restricted SQL store, opt-in dispatcher and grants committed. Earlier source passed targeted PostgreSQL tests; replay revision not yet executed. | Latest source and full G15 pending |
| M15-03 | HTTP routes and React center committed. Earlier source compiled and web build passed; browser fixtures failed before Playwright. | Three-engine browser execution pending |
| M15-04 | Unit, contract, PostgreSQL and source checks executed on frozen earlier SHA; browser tests authored but not executed. | G15-S01..S25 not fully verified |
| G15 | Full G15-S01..S25 plan exists, independent review outstanding | NOT ACCEPTED |

## 5. M13 interface negotiation and ownership

The M13 workstream independently owns KPI catalog, query service, dashboard charts and aggregate truth. M15 owns notification event intents, per-user rows, personal state/count routes and isolated notification UI. Proposed safe contract is GET/PATCH /api/v1/organizations/{organizationId}/notifications with an org-selected, live-authorized caller and a parent navigation callback accepting shipmentId plus exceptionId.

**Shared intersections to reconcile before joint implementation:** OrganizationWorkspace mounting and a safe deep-link callback; Program.cs registrations; OrganizationPolicy if M13 wants ViewDashboard and M15 later wants distinct ViewNotifications; AuditRoleProvisioner privileges; EF DbContext/model snapshot; migration file naming and ordering; common styles and API error/CSRF utilities; source-lock of integrated M11 regression; final combined G13/G15 evidence. M13 dashboard alert badges are optional M13-owned consumers and are not authorized by the M15 branch.

Do not assume another session approved an API DTO, migration order or component signature. Record a joint decision before a shared-source edit. Current planning avoids touching all shared implementation files and the other branch.

### M13 planning checkpoint read-back, 2026-11-08

A subsequent read-only comparison found the separate M13 planning branch at cf7afa4d58ac3c1e17d9eb66ef83aac32eecaca1, four documentation files ahead of main and still unmerged. M13 proposes a **dedicated dashboard-read permission** for Analyst/Manager and an **explicit read-only Auditor grant** subject to D-005 approval. This is not the current ViewShipmentData permission, so it does not authorize or revoke M15 delivery to an existing M11-read-entitled Auditor. M13 also proposes bounded transactional report/member snapshots with a 10-minute report-token lifetime; M15's proposed 20-minute notification list cursor is independent and is not a report snapshot. Both proposals have unallocated additive schema work, so no migration ordering, new permission enum, shared shell route, or EF snapshot has been approved across tracks. The M13 branch received no M15 writes.

## 6. Risks and remaining blockers

1. **P01-P14 contract approvals:** Must be explicit; G15-S01 cannot pass through technical tests alone.
2. **M11 source coupling:** AFTER INSERT outbox trigger atomicity affects existing M11 writes. Prove rollback, lock order, bounded latency and accepted M11 audit invariants before rollout. Decide if fail-closed is acceptable.
3. **SQL privileges:** Accepted AuditRoleProvisioner broadly grants SELECT to harborops_api before narrower exceptions; new M15 table grants and RLS/function protections need direct restricted-login attack proof and a safe key provisioner.
4. **M13 migration/shell conflicts:** M13 branch is parallel and may introduce migration IDs, DbContext and UI edits after this planning snapshot. Reconcile before any runtime implementation.
5. **Delivery failure/backlog and retention:** Dispatcher capacity, memory and lease behavior require measured performance; privacy retention and hold/disposal have no approval.
6. **Evidence:** No M15 test, SQL migration, role test, browser run, security mutation, independent review or G15 gate has been executed. No shared integrated source SHA exists.

## 7. Current authorization and next decision

Subsequent authorization allowed feature-branch M15 source implementation and repository-wide documentation reconciliation. This does not approve M15-P01 to P14, GitHub Actions, merging, production migration, release or deployment. The source remains a candidate until runtime verification is completed and an independent G15 review obtains explicit acceptance. Source-level implementation status is in [M15 Implementation Status](M15_IMPLEMENTATION_STATUS.md).
