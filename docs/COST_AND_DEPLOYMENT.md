# Cost and Deployment Strategy

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

The project working tree resides at `E:\\APP IDEAS\\HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform`, with project caches and M0 test evidence under `E:\\APP IDEAS\\harborops-m0-cache`. Docker Desktop/WSL persistent data is E-backed in the verified setup. All controlled persistent project storage must remain on E:, including database volumes, browser test output and future tools. C: may be used only for unavoidable, short-lived Windows temporary writes. Follow [Local Storage Policy](LOCAL_STORAGE_POLICY.md) and verify actual paths before introducing a new tool or service. Do not move installed tools or delete existing files without approval.

## Deployment profiles

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

## Deployment acceptance

- Setup steps work on a clean machine.
- Secrets are injected through protected configuration.
- Database backups are test-restorable.
- Monitoring endpoints are access-controlled.
- The hosted version indicates its synthetic/demo nature.
- Shutdown destroys or disables billable resources.

## M0 local infrastructure

`compose.yaml` uses self-hosted PostgreSQL 16, .NET 8 API/migrator containers, and a locally hosted React static frontend. No cloud account, map feed, or paid license is required for the intended baseline workflow. M0 Docker and dependencies were already installed and used locally for full G0 verification; no new tool installation was necessary. The GitHub validation workflow is manual-only to avoid unrequested CI usage. No hosted resources have been provisioned, and no cloud spending has been authorized. Verify any development tooling license terms independently.
