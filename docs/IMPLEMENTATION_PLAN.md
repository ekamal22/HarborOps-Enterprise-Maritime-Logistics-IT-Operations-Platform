# Implementation Notes

**Authority:** Supporting engineering guidance only. The [Product Requirements Document](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/docs/product-delivery-source-of-truth/docs/PRD.md), [Roadmap](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/docs/product-delivery-source-of-truth/docs/ROADMAP.md), and [Execution Graph](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/docs/product-delivery-source-of-truth/docs/EXECUTION_GRAPH.md) on the independent planning branch define the approved product direction and proposed M0-M24 sequence. This page must not compete with that roadmap.

## Post-verification hardening change

The separate hardening branch adds a supported .NET 10 baseline, modified PostgreSQL 16 runtime image and additional browser/accessibility tests. Its changes are **pending new runtime verification**. Do not replace the original G0 evidence or begin M1 based on source-only changes. Refer to [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md).

## M0 implementation and verification

On `feat/m0-engineering-baseline`, M0-01 through M0-05 had a recorded local verification at code revision `4fb34eb`. A later cold-database failure reproduced on that baseline, so the complete verification conclusion must be reopened. The M0 evidence register includes the actual tool versions, checks, commands, test results, environment, remediation history, and residual risks.

- Originally verified .NET 8 solution with API, application, domain, infrastructure, worker shell and tests
- PostgreSQL 16 schema migration, fixed synthetic bootstrap seed and repeatable tests
- Database-backed `/api/v1/system/baseline`, process liveness and database readiness
- JSON logs, correlation identifiers, sanitized failures and bounded dependency timeouts
- React status interface with synthetic provenance, loading, unavailable and refresh states
- Local Compose profile, one-shot migrator, locked frontend dependencies and verification scripts
- Security checks and local PostgreSQL outage/recovery rehearsal

**Gate G0:** reopened for re-verification, **acceptance pending**. M1 has not started. Verification is separate from final acceptance.

The detailed evidence is [M0 Verification](M0_VERIFICATION.md). The current observed state is in [Status](STATUS.md). The working tree remains separate from the planning branch; reconcile overlapping documentation when integrating branches.

## Granular next steps

The old seven-milestone plan has been superseded. The v2 roadmap divides the complete initial release into 25 narrowly scoped milestones (M0-M24) and G0-G24 evidence gates.

Once G0 has been explicitly accepted and decision D-001 is resolved, the next milestone is **M1 authentication and session lifecycle**. M2 adds organization/membership boundaries. M3-M6 implement maritime reference data, shipment lifecycle and assignments.

Do not start or record later features as completed merely because the original broad roadmap grouped them together.

## Required work-package discipline

1. Inspect the current branch, requirements, unresolved decisions and predecessor gate evidence before modifying source.
2. Keep each change within its named work package and explicit dependencies.
3. Identify API contracts, persistence changes, authorization boundaries, failure behavior and migration implications.
4. Execute tests and record command, environment, result, precise source SHA and dataset.
5. Assess dependency and container vulnerabilities without silently approving unresolved findings.
6. Update affected documentation and evidence records. No source-only completion claim may substitute for executed verification.
7. Mark a gate verified only from supporting evidence and retain `acceptance pending` until explicit acceptance.

## Storage constraint

All controlled persistent HarborOps data belongs on E:, including `E:\APP IDEAS\HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform`, `E:\APP IDEAS\harborops-m0-cache`, Docker/WSL data, dependency caches, build output, test databases, screenshots and logs. Only unavoidable short-lived Windows system temporary writes on C: are permitted. Refer to [Local Storage Policy](LOCAL_STORAGE_POLICY.md). Do not delete, migrate or prune storage without separate approval.

## Documentation changes

When a requirement changes, update the PRD, atomic requirements and affected acceptance checks. When milestone boundaries or prerequisites change, update the roadmap, execution-graph JSON and generated view. [Status](STATUS.md) must reflect observed implementation rather than planned capability. Keep future planning in the planning branch until its integration is approved.
