# G5-S01: M5 Local Contract Compliance Matrix

**Purpose:** Show, rule by rule, that the implementation and tests match the [M5 local contract](M5_LOCAL_CONTRACT_DECISION.md) and the [M5 architecture](M5_ARCHITECTURE_DESIGN.md), so the contract can be accepted or rejected on evidence.
**Contract state:** Accepted 2026-11-02; see [decision record](M5_LOCAL_CONTRACT_DECISION.md).
**Audited source:** the revision named in [M5 executed verification](M5_EXECUTED_VERIFICATION.md). Line numbers below are for that revision.
**How to read the evidence column:** each test named here ran in the fresh and repeated PostgreSQL integration passes, the unit pass or the three-engine browser pass of the final run. Per-test row counts are in the evidence package index (`reports/G5_EVIDENCE_INDEX.md`).

## 1. Compliance matrix

### Rule 1: Workflow Draft, Booked, InTransit, Arrived, Completed, Cancelled, with only the approved transitions

| Layer | Location |
| --- | --- |
| Domain | `src/HarborOps.Domain/Shipments/ShipmentPolicy.cs`: `Workflow` (line 24), `CanTransition` (line 40) |
| Service | `src/HarborOps.Infrastructure/Shipments/ShipmentStore.cs`: transition check at line 277, no-op rejection at line 281 |
| Database | Migration `20261101180000_ShipmentLifecycle.cs`: `CK_shipments_workflow` (line 46), transition rule in trigger `harborops_shipment_validate` (error `Illegal shipment transition`, line 122) |
| UI | `web/src/lib/shipmentDraft.mjs`: `TRANSITIONS`, `workflowOptions`, so the selector offers only legal targets |

Approved graph: Draft to Booked or Cancelled; Booked to InTransit or Cancelled; InTransit to Arrived or Cancelled; Arrived to Completed; Completed and Cancelled are terminal. The service, the trigger and the UI each encode the same graph independently.

Tests: `ShipmentPolicyTests.FullWorkflowMatrix` (all 36 from/to pairs), `ShipmentPolicyTests.WorkflowMatrix`, `ShipmentPolicyTests.RejectUnknownStatus`, `ShipmentSqlMatrixTests.CompositePortsAndImmutableFieldsRejectIndependentSqlMutations` (direct SQL forbidden transition and unknown status rejected with the intended reason), web unit tests for `workflowOptions`.
Discrepancies: none.

### Rule 2: Distinct active origin and destination ports in the shipment's organization; UTC planning constraints

| Layer | Location |
| --- | --- |
| Service | `ShipmentStore.cs` line 199 (same origin and destination rejected), lines 201-204 (both ports must exist, be Active and belong to the organization, else 404) |
| Database | `FK_shipments_origin` and `FK_shipments_destination` (composite on organization and port, lines 34-37), `CK_shipments_route` (line 43), `CK_shipments_times` (line 44) |
| UTC parsing | `src/HarborOps.Domain/Maritime/MaritimePolicy.cs` `Utc` (line 48): explicit instant, offset must be zero; `ShipmentPolicy.ValidateTimes` (line 34): departure before arrival; null means unknown |

Tests: `ShipmentBoundaryTests.ForeignAndArchivedPortsRejectShipmentCreation`, `ShipmentExtendedGateTests.SameParentAndUnknownPortsRejectCreation`, `ShipmentPolicyTests.ReferencesAndUtcAreStrict`, `ShipmentSqlMatrixTests.CompositePortsAndImmutableFieldsRejectIndependentSqlMutations` (direct-SQL foreign-port insert rejected by `FK_shipments_origin`, `FK_shipments_destination` by name; inverted window rejected by `CK_shipments_times`), `ShipmentExtendedGateTests.InvalidJsonContentAndVersionMatrix`.
Note: Active is required at creation only. A port archived later stays a valid historical reference, as the architecture states (section 2.1).
Discrepancies: none.

### Rule 3: Analyst and Manager read and write; Auditor read-only; Administrator and Support denied by default

| Layer | Location |
| --- | --- |
| Domain | `src/HarborOps.Domain/Organizations/OrganizationPolicy.cs` lines 42-45: `ViewShipmentData` for Analyst, Manager, Auditor; `ManageShipmentData` for Analyst and Manager. No other permission grants shipment access |
| Service | `ShipmentStore.cs` `ScopeAsync`: typed permission resolved after the locks are held (read routes use `ViewShipmentData`, writes use `ManageShipmentData`) |
| UI | `OrganizationWorkspace.tsx` renders the shipment workspace for Analyst, Manager and Auditor, with write controls only when the role is not Auditor |

Tests: `ShipmentExtendedGateTests.FullSixRouteFiveRolePermissions` (every route for every role), `ShipmentBoundaryTests.FullFiveRoleShipmentMatrix`, `ShipmentPolicyTests.ShipmentRolesAreIndependentOfM4` (no inherited maritime privilege), `QueuedShipmentReadRechecksEveryAuthorizationDependency` and `QueuedShipmentWriteRechecksEveryAuthorizationDependency` (role changes after the lock is acquired, including Auditor keeping read and Support losing it), browser case `M5 Auditor shipment view does not grant M4 management`.
Discrepancies: none.

### Rule 4: Immutable identity and references, archive semantics, organization isolation, exact-version concurrency

| Concern | Location |
| --- | --- |
| Immutable identity | Trigger error `Shipment identity immutable` (migration line 112) covers id, organization, reference code, external reference, both ports, source kind and creation time; the API accepts none of them on update (`ShipmentInput.Parse`, lines 27-33, rejects unknown fields) |
| Reference reservation | Unique indexes `IX_shipments_OrganizationId_ReferenceCode` (line 57) and `IX_shipments_OrganizationId_ExternalReference` (line 59, non-null only), per organization and including archived rows |
| Archive | Service lines 225-237 (archived rows accept only a one-field restore; delete archives); trigger errors `Invalid shipment restoration`, `Archive cannot edit shipment`, `Invalid archive change`; `CK_shipments_archive` (line 51); DELETE of rows forbidden (line 98) |
| Isolation | Every query carries the organization predicate (`ShipmentStore.cs` `Own`, line 128 for lists); route organization must equal the session's selected organization; foreign ids return 404 |
| Exact version | `ExpectedVersion` required for update, archive and restore (`ShipmentInput.Parse`); compared at line 223; trigger rejects version jumps (`Invalid shipment version`, line 115) |

Tests: `ShipmentSqlMatrixTests.ExternalIdentifiersReserveArchivedValuesButPermitCrossTenantReuse`, `ShipmentHttpTests.DuplicateArchivedReferenceAndCrossTenantIdentityStayScoped`, `ShipmentHttpTests.CompetingExpectedVersionUpdatesHaveOneWinnerAndOneHistoryRecord`, `ShipmentExtendedGateTests.ArchivedRestoreAndStaleEditCannotBothCommit`, `DuplicateArchiveAttemptsCannotBothCommit`, `ConcurrentArchiveAndStatusEditProduceOneVersionWinner`, `ShipmentExtendedGateTests.MissingSelectionAndForeignActualShipmentNeverDiscloseData`, `ShipmentBoundaryTests.PaginationAndFiltersStayTenantScoped` (other-tenant rows with the same prefix are seeded; mutation proof shows removing the tenant predicate fails it), browser cases for stale conflict and draft preservation.
Mutation evidence: removing the tenant predicate fails two tests; removing post-lock revalidation fails seven (see [executed verification](M5_EXECUTED_VERIFICATION.md)).
Discrepancy found and corrected: the architecture states every response is `Cache-Control: no-store`, but only handler-produced responses set it. Framework-generated responses such as the anonymous 401 challenge did not (the exception handler's sanitized 503 already carries `no-store`). A small middleware in `Program.cs` now adds `no-store` to every `/api` response that does not already set a cache policy. Covered by `AnonymousAllSixShipmentRoutesRejectAccess`; removing the middleware makes that test fail (mutation proof). The interruption tests also assert the header on the sanitized 503.

### Rule 5: Atomic, immutable manual workflow history

| Layer | Location |
| --- | --- |
| Database | `shipment_status_changes` with composite foreign key, unique `(organization, shipment, version)`; `harborops_shipment_record_transition` inserts one row on creation and on every workflow change in the same transaction (trigger `shipments_status_history`, migration line 173); `harborops_shipment_history_guard` rejects UPDATE, DELETE and any insert not made by the trigger (`pg_trigger_depth() < 2`, line 153) |
| Service | No application code writes history; status-neutral edits, archive and restore write none |

Tests: `ShipmentSqlMatrixTests.StatusNeutralEditAndArchiveNeverFabricateHistory`, `ShipmentHttpTests.DirectSqlCannotDeleteShipmentOrForgeStatusChange`, `ShipmentSqlMatrixTests.CompositePortsAndImmutableFieldsRejectIndependentSqlMutations` (history delete, rewrite and fabricated insert rejected with the intended reason), `ShipmentBoundaryTests.FailedTransitionLeavesNoPartialHistory`, `ShipmentExtendedGateTests.CancelledQueuedShipmentMutationNeverCommitsHistory`, and the six interruption tests in `ShipmentInterruptionTests` (terminated connection mid-transition and mid-create, cancelled request, command timeout, killed application process, lost response after commit).
Discrepancies: none. The architecture describes a BEFORE UPDATE trigger; the implementation is BEFORE INSERT, UPDATE and DELETE, which is stricter. The architecture text was corrected to match.

### Rule 6: Scope limited to basic shipment operations

| Boundary | Evidence |
| --- | --- |
| No container, voyage or assignment link | The shipments table has no foreign key to voyages or containers; create and update reject unknown fields such as `voyageId` and `containerIds` |
| No tracking-derived status | `workflowStatus` is set only by explicit operator transitions; no event ingestion exists in M5 |
| Basic search only | List supports `q`, `recordStatus`, `workflowStatus`, `after`, `limit` (`ShipmentEndpoints.ListFilters`); no export, no ranking, no advanced filters |

Tests: `ShipmentExtendedGateTests.InvalidJsonContentAndVersionMatrix`, `ShipmentBoundaryTests.PaginationAndFiltersStayTenantScoped`, `ShipmentMigrationTests.SeededM4HistorySurvivesM5UpgradeRollbackAndReapply` (no change to M4 voyage and container tables).
Discrepancies: none.

## 2. Documentation inconsistencies corrected

- The architecture state line and section 7 initially described the design as unexecuted. They now describe the accepted implementation.
- The trigger description in architecture section 3 now matches the implementation.
- Architecture section 6 now specifies draft preservation, background refresh and conflict merging for the workspace.

## 3. Findings that are not contract violations

- The accepted M3 reference and M4 maritime workspaces still reload their own lists when the tab becomes visible, and that reload closes their open edit forms. This is the defect the shipment workspace had. It is accepted M3/M4 behavior outside the M5 scope, so it was left unchanged and is recorded as a residual risk and a candidate follow-up that would need its own verification of the affected milestones.
- The M5 workflow history records creation and workflow transitions only, as the contract states. It is not a general audit log (M16).

## 4. Accepted decision

All six contract rules were explicitly accepted as written on 2026-11-02, closing G5-S01. Full G5 was also separately accepted after independent review, as recorded in [gate closure](M5_G5_GATE_CLOSURE.md).
