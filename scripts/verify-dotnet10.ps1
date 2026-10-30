param(
    [string]$RepositoryPath = (Split-Path -Parent $PSScriptRoot),
    [string]$ExpectedSha = '',
    [switch]$UseContainer
)

$ErrorActionPreference = 'Stop'
$Repo = (Resolve-Path -LiteralPath $RepositoryPath).Path
if (-not $Repo.StartsWith('E:\APP IDEAS\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Verification requires an E:\APP IDEAS checkout or worktree'
}
if (!(Test-Path -LiteralPath (Join-Path $Repo 'HarborOps.sln'))) {
    throw 'Expected HarborOps solution not found at the selected repository'
}

$sha = (& git -C $Repo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Unable to resolve checkout commit' }
if ($ExpectedSha -and !$sha.StartsWith($ExpectedSha, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Expected revision $ExpectedSha but found $sha; refusing to verify the wrong code"
}
Write-Host "Verifying checkout $Repo at $sha"

$Cache = 'E:\APP IDEAS\harborops-m0-cache'
$env:DOTNET_CLI_HOME = Join-Path $Cache 'dotnet-home'
$env:NUGET_PACKAGES = Join-Path $Cache 'nuget\packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $Cache 'nuget\http'
$env:TEMP = Join-Path $Cache 'temp'
$env:TMP = $env:TEMP
$env:TMPDIR = $env:TEMP
foreach ($directory in @($env:DOTNET_CLI_HOME, $env:NUGET_PACKAGES, $env:NUGET_HTTP_CACHE_PATH, $env:TEMP)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

Push-Location -LiteralPath $Repo
try {
    if ($UseContainer) {
        if (!(Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker is required for container SDK mode' }
        $script = 'set -euo pipefail; dotnet --version; dotnet restore HarborOps.sln; dotnet build HarborOps.sln -c Release --no-restore'
        & docker run --rm --pull never -v "${Repo}:/source" -v "${Cache}:/cache" -w /source -e 'NUGET_PACKAGES=/cache/nuget/packages' -e 'DOTNET_CLI_HOME=/cache/dotnet-home' mcr.microsoft.com/dotnet/sdk:10.0 bash -ec $script
        if ($LASTEXITCODE -ne 0) { throw 'Container .NET 10 restore/build failed' }
    } else {
        $portable = Join-Path $Cache 'dotnet-sdk'
        if (Test-Path -LiteralPath (Join-Path $portable 'dotnet.exe')) {
            $env:DOTNET_ROOT = $portable
            $env:PATH = "$portable;$env:PATH"
        }
        $version = (& dotnet --version)
        if ($LASTEXITCODE -ne 0 -or $version -notmatch '^10\.') {
            throw 'Required .NET 10 SDK unavailable. Use -UseContainer with an existing local SDK image; do not install on C:.'
        }
        & dotnet restore HarborOps.sln
        if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
        & dotnet build HarborOps.sln -c Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    }
    Write-Host "Restore/build passed for $sha"
} finally {
    Pop-Location
}
