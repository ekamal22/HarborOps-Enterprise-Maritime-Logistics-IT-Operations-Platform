# Local Workspace and Storage Policy

**Effective:** 2026-10-30  
**Applies to:** All HarborOps milestones, development, testing, build, debugging, scripts, database and container workflows.

## Mandatory workspace locations

| Category | Approved location |
| --- | --- |
| Git working tree | `E:\APP IDEAS\HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform` |
| Project-specific dependency caches and evidence | Historical M0: `E:\APP IDEAS\harborops-m0-cache`; locally verified G2: `E:\HarborOps-Verification` (caches, temporary files, Playwright and evidence); future milestones: explicit E:-backed project directories |
| .NET and frontend build outputs | Inside the repository or an explicitly configured E-drive output directory |
| Package downloads and compilation scratch | E-drive scoped cache and temporary directories |
| Database integration-test data | Disposable local PostgreSQL instance, with data held on E or in disposable memory-backed storage |
| Docker Desktop / WSL distribution, volumes, build cache | Verified E-drive-backed storage |
| Browser test profiles, screenshots, trace files, reports | E-drive project evidence directories |
| Application logs, generated fixtures and test artifacts | E-drive project directories |

**Persistent project-specific writes to C: are prohibited.** C: is critically low on free space. Avoid installing or relocating toolchains, Docker/WSL data, databases, cloned repositories, caches, build outputs, screenshots or logs to C:.

Existing OS and tool installations on C: are not instructions to reinstall or relocate them. Some Windows and system processes can make unavoidable, short-lived temporary writes to C:. These are the only permitted exceptions. Do not configure new persistent C-drive storage. Where a tool supports temporary overrides, prefer E:.

This policy does **not** authorize wiping, relocating, pruning or deleting existing data on either drive.

### Unavoidable short-lived C: writes

Unavoidable temporary writes by Windows or a required tool are permitted only when short-lived and attributable to the current task. Record the exact task-specific paths and C: free-space baseline before execution. Redirect controllable caches, logs, build outputs, browser profiles, Docker/WSL data and test artifacts to E:. After completing the job and preserving relevant logs on E:, stop the task's processes, delete only temporary files proven to have been created for that job, and record any remaining paths and before/after measurements. Never broadly clear Windows or user temp directories, installer caches, crash dumps, Docker images, other applications' caches, or unrelated files. If safe attribution or deletion cannot be established, record the blocker rather than deleting unverified data. Persistent project-specific storage on C: remains prohibited.


## PowerShell session setup

In a **new terminal** before development or test commands:

```powershell
$Workspace = 'E:\APP IDEAS'
$Repo = Join-Path $Workspace 'HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform'
$Cache = Join-Path $Workspace 'harborops-m0-cache'

if (-not (Test-Path -LiteralPath $Repo -PathType Container)) {
    throw "HarborOps repository missing from E:; do not work from a C: clone"
}

$env:NUGET_PACKAGES = Join-Path $Cache 'nuget\packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $Cache 'nuget\http'
$env:DOTNET_CLI_HOME = Join-Path $Cache 'dotnet-home'
$env:npm_config_cache = Join-Path $Cache 'npm'
$env:TEMP = Join-Path $Cache 'temp'
$env:TMP = $env:TEMP
$env:TMPDIR = $env:TEMP
$env:XDG_CACHE_HOME = Join-Path $Cache 'xdg'

@(
    $env:NUGET_PACKAGES,
    $env:NUGET_HTTP_CACHE_PATH,
    $env:DOTNET_CLI_HOME,
    $env:npm_config_cache,
    $env:TEMP,
    $env:XDG_CACHE_HOME
) | ForEach-Object { New-Item -ItemType Directory -Force -Path $_ | Out-Null }

Set-Location -LiteralPath $Repo
```

These variables are scoped to the current shell session. Confirm each tool actually honors its configured path; do not assume environment variables relocate every Windows, browser, Docker or WSL write.

## Preflight before large or destructive operations

1. Check current location with `Get-Location`; repository and target outputs must be on E:.
2. Check free space on both drives with `Get-PSDrive -Name C,E`. Stop if an operation is expected to cause significant C-drive growth.
3. Check `npm config get cache`, `dotnet nuget locals all --list`, and effective temporary directories.
4. Inspect `docker context ls`, `docker info`, and Docker Desktop's configured disk image/WSL storage path. A shell variable does not move Docker-managed storage.
5. Inspect existing Docker project ownership before creating, deleting or interrupting containers or volumes. Never prune the entire Docker daemon.
6. Prefer disposable fixtures and isolated test databases. Never point integration tests at a valued, shared or production database.
7. Keep evidence artifacts, including screenshots and logs, on E:. Redact connection strings, credentials, tokens and unrelated machine details before sharing.
8. If a tool can only use persistent C-drive storage, **stop and document the blocker**; obtain specific approval before installing, moving or changing system-wide storage.

## M1 credential and key-storage boundary

M1 introduces a persistent ASP.NET Core Data Protection key ring, PostgreSQL credential/session tables, and isolated authentication test fixtures. For local work, **all controllable persistent keys, Docker volumes, credential-related logs and test evidence must remain on E:**. Keep key material and certificates out of Git, images, browser storage and CI artifacts. The API container is read-only, so mount a dedicated restricted E-backed volume for Data Protection keys and test restart continuity. Deployable environments require reviewed encryption at rest.

Create test users only inside disposable PostgreSQL databases; never seed an active user into the public M0 synthetic database initializer. Admin provisioning must read passwords interactively and avoid argument histories or logs. No existing local PostgreSQL volume is required for routine G1 verification. Do not relocate, prune or delete old data or key rings without explicit approval. See [M1 Authentication Design](M1_AUTHENTICATION_DESIGN.md) and [G1 Verification](M1_VERIFICATION_PLAN.md).

## M2 local verification and future milestones

G2 passed through `scripts/verify-m2-local.sh` using disposable PostgreSQL/Compose resources and E:-backed caches, browser binaries and evidence under `E:\HarborOps-Verification`. The recorded source is `fbddc1361d79b027275bb53b48133ba0dc7d5cb1`; see [G2 executed verification](M2_EXECUTED_VERIFICATION.md). Earlier test iterations caused WSL crash reports to be written by the operating system under C:; corrected command failure handling and container core-dump controls prevented new crash files in the final G2 run. Existing crash reports were not modified. Never deliberately initiate project storage or dumps on C:, and stop if a tool cannot be constrained to E:.

## M0 baseline and historical context

M0 was verified on a Windows 11 host with its working tree under E:, project caches under `E:\APP IDEAS\harborops-m0-cache`, and Docker Desktop's WSL data on E:. [M0 Verification](M0_VERIFICATION.md) preserves historical evidence, while [M0 Hardening Verification](M0_HARDENING_VERIFICATION.md) records the accepted G0 CI run. Both are available on `main`.

For future milestones, reuse this E-drive baseline and recheck disk paths before adding new tools or test frameworks. No new service, package manager, browser test runner or build pipeline may silently introduce persistent C-drive usage.

## G4 local run storage notes

The G4 runs use `E:HarborOps-Verification` for caches, temporary files, browser binaries, evidence and logs, and the E:-backed Docker Desktop data image checked by `scripts/check-m3-docker-storage.mjs`. Git Bash rewrites a leading slash inside an argument unless `MSYS_NO_PATHCONV=1` is set for that command, which previously hid a leftover disposable database container from the runner. Before and after a run, record C: and E: free space and a file manifest of the candidate C: locations, and delete only files that the manifest comparison attributes to the run. The fifth G4 audit records the method.

## Scope and verification

This is a development-environment constraint, **not** a claim that Windows itself will never touch C:. Record observed storage paths and any unavoidable temporary exceptions in the relevant milestone evidence register. Do not claim storage compliance based solely on this policy document.
