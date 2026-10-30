# Local Workspace and Storage Policy

**Effective:** 2026-10-30  
**Applies to:** All HarborOps milestones, development, testing, build, debugging, scripts, database and container workflows.

## Mandatory workspace locations

| Category | Approved location |
| --- | --- |
| Git working tree | `E:\APP IDEAS\HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform` |
| Project-specific dependency caches and evidence | `E:\APP IDEAS\harborops-m0-cache` for the M0 baseline; future named per-milestone subdirectories may be added under `E:\APP IDEAS` |
| .NET and frontend build outputs | Inside the repository or an explicitly configured E-drive output directory |
| Package downloads and compilation scratch | E-drive scoped cache and temporary directories |
| Database integration-test data | Disposable local PostgreSQL instance, with data held on E or in disposable memory-backed storage |
| Docker Desktop / WSL distribution, volumes, build cache | Verified E-drive-backed storage |
| Browser test profiles, screenshots, trace files, reports | E-drive project evidence directories |
| Application logs, generated fixtures and test artifacts | E-drive project directories |

**Persistent project-specific writes to C: are prohibited.** C: is critically low on free space. Avoid installing or relocating toolchains, Docker/WSL data, databases, cloned repositories, caches, build outputs, screenshots or logs to C:.

Existing OS and tool installations on C: are not instructions to reinstall or relocate them. Some Windows and system processes can make unavoidable, short-lived temporary writes to C:. These are the only permitted exceptions. Do not configure new persistent C-drive storage. Where a tool supports temporary overrides, prefer E:.

This policy does **not** authorize wiping, relocating, pruning or deleting existing data on either drive.

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

## M0 baseline and future milestones

M0 was verified on a Windows 11 host with its working tree under E:, project caches under `E:\APP IDEAS\harborops-m0-cache`, and Docker Desktop's WSL data on E:. The [M0 Verification](https://github.com/ekamal22/HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform/blob/feat/m0-engineering-baseline/docs/M0_VERIFICATION.md) evidence file exists on the implementation branch; the planning branch links to that record.

For future milestones, reuse this E-drive baseline and recheck disk paths before adding new tools or test frameworks. No new service, package manager, browser test runner or build pipeline may silently introduce persistent C-drive usage.

## Scope and verification

This is a development-environment constraint, **not** a claim that Windows itself will never touch C:. Record observed storage paths and any unavoidable temporary exceptions in the relevant milestone evidence register. Do not claim storage compliance based solely on this policy document.
