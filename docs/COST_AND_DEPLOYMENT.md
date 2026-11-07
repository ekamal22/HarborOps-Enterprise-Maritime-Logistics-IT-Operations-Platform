# Cost and Deployment Strategy

**Current accepted baseline (2026-11-06):** M0-M9/G0-G9 and M16/G16 are accepted and integrated into protected `main`; see [G9 closure](M9_G9_GATE_CLOSURE.md) and [G16 closure](M16_G16_GATE_CLOSURE.md). M10 source was merged into `main` through [PR #23](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/pull/23) on 2026-11-06 (merge `5edf003`); G10 acceptance is pending and its runtime verification is recorded in [the M10 executed verification](M10_EXECUTED_VERIFICATION.md). The mandatory M9 hold on sustained transport-event processing remains until a separately approved, verified backlog solution prevents exhaustion of the 10,000 unresolved-work-item limit. No deployment or public application hosting is authorized.

## Primary constraint

HarborOps must remain fully usable for essential development, testing, and product demonstration without mandatory paid services. An account with limited free credits is optional, not a prerequisite.

## Local-first stack

| Component | Baseline | Requirement |
| --- | --- | --- |
| API and worker | Locally executed .NET | Free SDK/runtime, no usage billing |
| Frontend | React/Vite local dev/build | No paid frontend dependency |
| Database | PostgreSQL container | Local volumes and backup controls |
| Monitoring | Local Prometheus/Grafana or equivalent | Opt-in and resource bounded |
| Gateway | Local reverse proxy, if required | No commercial ingress service |
| Maps | Compliant public tiles under provider policy, or local/offline fallback | No unlimited free request assumption |
| Dataset | Deterministic synthetic generator | No commercial vessel feed |
| Test tools | Open-source unit, integration, browser, and load tools | Repeatable without subscriptions |

## Optional education and free-tier resources

- Azure for Students: consider available student credits and eligible services only after checking current terms, verification status, and quotas.
- GitHub Education: eligible development tools may reduce IDE and testing costs; verify the current offer and limits.
- JetBrains educational tools: optional for eligible non-commercial educational use, subject to license terms.
- AWS Educate: optional learning and isolated exercises, not a dependency for application hosting.
- Free static hosting: may serve a public frontend demonstration, but does not imply a free ASP.NET Core backend.

Credits, free limits, verification requirements, and student entitlements change. Do not encode a fixed credit amount as a permanent architectural assumption.

## Mandatory local storage and capacity guardrail

The development working tree, dependency caches and verification evidence use configured E:-backed directories. Docker Desktop/WSL persistent data is E-backed in the verified setup. All controlled persistent project storage must remain on E:, including database volumes, browser test output and future tools. C: may be used only for unavoidable, short-lived Windows temporary writes. Follow [Local Storage Policy](LOCAL_STORAGE_POLICY.md) and verify actual paths before introducing a new tool or service. Do not move installed tools or delete existing files without approval.

## Deployment profiles

### G10 remediation deployment restriction, 2026-11-07

M10 correction security changes are source-committed on the separate `fix/m10-g10-l07-l14-remediation` branch. The signed correction operation, private verifier key provisioning and thirteenth migration have not passed new-source runtime verification. Do not upgrade retained data, merge or deploy the remediation based on source inspection alone. The accepted M9 sustained transport-event processing hold and 10,000 unresolved-work limit remain unchanged. See [current G10 status](STATUS.md) and [implementation record](M10_G10_CORRECTION_AUTHORITY_IMPLEMENTATION.md).

### Profile L: Local complete system
All core modules available on one development computer. No externally hosted API. Preferred for automated tests and fault injection.

### Profile S: Public static demonstration
Static built frontend, synthetic snapshot data, prominent demonstration labeling. No real authentication or live operational APIs. Safe and cheap to keep online within provider limits.

### Profile H: Temporary hosted full-stack environment
Only after verifying student/free-tier eligibility, allowed spend, service quotas, data handling, and shutdown procedures. Host API, worker, and storage with restrictive networking and clear cost visibility.

## Cloud spending guardrails

1. Set an explicit monthly maximum acceptable spend; preferred target is zero.
2. Avoid services with unclear billing or charges after free credits expire.
3. Verify quotas, storage, networking/egress, logging, backups, and idle charges.
4. Configure budget alerts, but do not treat an alert as a hard spending cap.
5. Use short-lived environments and remove all resources afterward.
6. Record resource inventory, start/stop instructions, and billing account checks.
7. If a truly zero-charge guarantee cannot be established, stay local.

## Map and data usage

OpenStreetMap data and map tile services have different rights and operational limits. Follow provider attribution, rate limits, and usage conditions; provide a compliant fallback for demos. Historical AIS datasets require license and attribution review. Publicly accessible data does not necessarily grant unrestricted commercial reuse.

## M1 deployment profile and authentication storage

The isolated M1 GitHub Actions verification completed on a disposable runner with no permanent hosted application or recurring infrastructure dependency. The local profile adds a named Data Protection key-ring volume and reuses the existing PostgreSQL and Docker runtime. See [M1 verification](M1_IMPLEMENTATION_VERIFICATION.md). G1 was accepted on 2026-10-31 and PR #4 merged to `main`. This does not authorize hosting.

The accepted [M1 authentication design](M1_AUTHENTICATION_DESIGN.md) fits the existing no-charge React, ASP.NET Core and PostgreSQL Compose stack. It introduces no Redis, identity microservice, hosted account service or paid API. M1 needs only an additive PostgreSQL schema, an internal Nginx/Vite `/api/*` proxy and a small, durable ASP.NET Core Data Protection key-ring mount.

Local persistent application keys and credentials must reside in approved E-backed Docker storage under [Local Storage Policy](LOCAL_STORAGE_POLICY.md), not inside the read-only API image, not on C: and never in Git. Use secrets or interactive provisioning, not plaintext default accounts or reusable demonstration credentials. Hosted authentication remains blocked until HTTPS, host-only Secure cookies, trusted proxy configuration, secret provisioning and encryption of Data Protection keys at rest are verified. Same-origin loopback HTTP is development-only.

Verification must prove session persistence across restarts and immediate database revocation without checking a retained local Docker volume. Historical G1 verification used ephemeral GitHub-hosted runners. Current and future verification must run locally on E: while GitHub Actions remains prohibited. Authentication approval does not authorize cloud charges or public access.

## M2 deployment and verification boundary

M2 organization and membership authorization is included in the accepted local Compose application on `main`; no hosted identity or tenancy service was introduced. G2 passed in isolated local tests on E:, including migration preservation, scoped access denial, recovery, dependency review and high/critical image scans. [G2 evidence](M2_EXECUTED_VERIFICATION.md) identifies the verified source SHA. No public hosting or production deployment is approved. GitHub Actions execution remains prohibited unless explicitly restored.

## Deployment acceptance

- Setup steps work on a clean machine.
- Secrets are injected through protected configuration.
- Database backups are test-restorable.
- Monitoring endpoints are access-controlled.
- The hosted version indicates its synthetic/demo nature.
- Shutdown destroys or disables billable resources.

## Verified M0 modernization costs and storage

The .NET 10 SDK, derived PostgreSQL image, and browser runner can require additional local downloads and image build space. All such storage must remain on E: under [Local Storage Policy](LOCAL_STORAGE_POLICY.md); check free space and effective Docker/WSL paths before running. The PostgreSQL data volume must not be deleted or automatically migrated. These changes do not require paid hosting.

## M0 local infrastructure

`compose.yaml` uses self-hosted PostgreSQL 16, .NET 10 API/migrator containers on `main`, and a locally hosted React static frontend. No cloud account, map feed, or paid license is required for the intended baseline workflow. M0 Docker and dependencies were already installed and used locally for full G0 verification; no new tool installation was necessary. Publication hardening is integrated into `main`, all workflow definitions at current branch tips are manual-only, and repository-level GitHub Actions is disabled. The source repository is public, but no hosted application or new hosted verification is authorized. See [publication remediation](PUBLICATION_REMEDIATION_STATUS.md). G0 was verified on a temporary GitHub-hosted runner in run 41134743879; it did not provision permanent hosted infrastructure. No hosted resources have been provisioned, and no cloud spending has been authorized. Verify any development tooling license terms independently.

## M9 transport-backlog operating condition (approved 2026-11-05)

All six [M9-01 contract decision](M9_LOCAL_CONTRACT_DECISION.md) choices are approved, but sustained transport-event processing must remain disabled until M10 or separately approved, verified backlog handling prevents the `NeedsReconciliation` queue from exhausting the 10,000 unresolved-item bound. The default Compose stack does not start the optional M9 worker. G9, G16 and the later F001 extension have been accepted and merged as documented in [G9 closure](M9_G9_GATE_CLOSURE.md) and [G16 closure](M16_G16_GATE_CLOSURE.md); neither merge authorizes public hosting or production deployment. M10 PR #23 remains an unaccepted draft.
