# Acceptance Criteria

## Status convention

- Planned: requirements documented, no implementation evidence.
- In progress: changes exist, verification incomplete.
- Implemented: functionality exists but may lack complete verification.
- Verified: documented acceptance checks passed against a specified revision.
- Accepted: verified and explicitly approved.
- Blocked: a prerequisite or mandatory check is failing.
- Acceptance pending: verified evidence is available, explicit acceptance not recorded.

No item is accepted by implication. See STATUS.md for the current ledger.

## Product acceptance gates

### A. Shipment workflows
- [ ] An authorized analyst creates a shipment, assigns a container and voyage, edits with concurrency protection, and finds it through search.
- [ ] Invalid data is rejected with stable errors and no partial changes.
- [ ] Unauthorized users cannot retrieve or mutate the shipment across any route or export.

### B. Tracking integrity
- [ ] Inbound event is validated, durably recorded, processed, and visible in timeline.
- [ ] Duplicate, out-of-order, missing-reference, and correction scenarios preserve event integrity.
- [ ] Projection rebuild produces the expected deterministic current state.
- [ ] Failed processing is diagnosable and recoverable without silent event loss.

### C. Analytics
- [ ] Dashboard KPIs reconcile to their documented underlying records.
- [ ] Delay and at-risk states follow testable rule definitions.
- [ ] Filters, downloads, maps, and detail navigation honor organization permissions.
- [ ] Simulated positions and ETA calculations are labeled as such.

### D. Security
- [ ] Role/function/object authorization matrix passes with at least two organizations.
- [ ] Tokens/secrets are excluded from version control, logs, and exports.
- [ ] Session expiry, account disablement, suspicious ingress, and rate limiting are tested.
- [ ] Security findings are triaged; release blockers are resolved.

### E. IT operations
- [ ] Liveness/readiness distinguish process and dependency health.
- [ ] Monitoring detects a deliberate worker or database failure.
- [ ] Incident records contain detection, impact, investigation, mitigation, and verification.
- [ ] Audit records and metrics can be correlated without exposing sensitive content.

### F. Delivery and recoverability
- [ ] Local stack starts from a clean documented environment.
- [ ] Fresh and historical migrations pass.
- [ ] Database backup restoration succeeds on isolated test target.
- [ ] Automated unit, integration, contract, and browser suites pass.
- [ ] Performance data includes environment, dataset, command, and summary.
- [ ] Hosted deployment uses an explicitly constrained budget and shutdown plan.
- [ ] README, STATUS, API docs, diagrams, and operational runbooks describe actual behavior.

## Required evidence bundle

1. Exact repository revision and dependency lock state.
2. Environment information and deterministic data seed.
3. Commands and pass/fail results for all required test layers.
4. Security isolation matrix and relevant remediation records.
5. Demonstrated incident and recovery logs/screenshots with private data redacted.
6. Known limitations and deferred work.
7. Final status: acceptance pending or accepted, with date and scope.

## Release refusal conditions

A release must not be described as verified if core data can cross organization boundaries, accepted events disappear silently, backups have never been restored, essential tests fail, or the documentation misstates current delivery status.

## M0 engineering baseline verification (separate from product release)

**G0 state:** verified on 2026-10-30 at code revision `4fb34eb`; **acceptance pending**. The evidence source is [M0 Verification](M0_VERIFICATION.md). Checks below refer only to the M0 baseline, not broader M1-M24 product release requirements.

- [x] Restore and build the entire .NET solution from a clean environment (0 warnings, 0 errors).
- [x] Execute M0 unit and HTTP contract tests (1 unit, 10 contract).
- [x] Execute real PostgreSQL integration tests, migration, repeatable seed and snapshot checks (5 integration).
- [x] Install locked frontend dependencies, run utility tests and build React production assets (6 frontend tests).
- [x] Validate Docker Compose configuration, clean-volume startup, migration completion and M0 services.
- [x] Verify the browser-to-API-to-PostgreSQL path, loading, degraded-state error, refresh and 375 px layout in a Chrome 152 engine.
- [x] Verify liveness remains 200 while readiness and baseline return 503 during an isolated database outage; verify restoration to 200.
- [x] Review tracked/history secrets, dependency advisories, container settings and current M0 documentation. Record scan limitations and residual findings.

**Residual risks and boundaries:** the PostgreSQL `gosu` image scan reported one critical and 25 high findings in the upstream helper binary. These findings are documented, **not automatically risk-accepted**. Container-image rescanning and risk disposition remain relevant to the acceptance decision. .NET 8 support ends on 2026-11-10. Manual CI execution, additional browsers, screen readers and the full M19 backup/restore rehearsal are not covered by this G0 verification.

**Development storage:** all persistent project storage must remain on E: under [Local Storage Policy](LOCAL_STORAGE_POLICY.md). Unavoidable short-lived system temporary use on C: is permitted.

The full product-release acceptance sections above remain unchecked. No M1-M24 gate has been verified or accepted.
