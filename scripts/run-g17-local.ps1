param(
    [Parameter(Mandatory = $true)][string]$EvidenceDirectory,
    [string]$ProjectName = 'ho-verify-m17-local',
    [string]$ExpectedSha = '',
    [switch]$StartDisposableStack,
    [switch]$FaultProbe,
    [switch]$StopOwnedContainers
)
$ErrorActionPreference = 'Stop'
$Repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (!$Repo.StartsWith('E:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use an E: worktree for M17 verification'
}
if ($EvidenceDirectory -notmatch '^[Ee]:\\' -or $ProjectName -notmatch '^ho-verify-m17-[a-z0-9-]+$') {
    throw 'Select an E: evidence directory and a unique ho-verify-m17-* project name'
}
if (!(Test-Path -LiteralPath $EvidenceDirectory)) {
    New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
}
$Cache = Join-Path (Split-Path -Parent $Repo) 'harborops-m0-cache'
$env:DOTNET_CLI_HOME = Join-Path $Cache 'dotnet-home'
$env:NUGET_PACKAGES = Join-Path $Cache 'nuget\packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $Cache 'nuget\http'
$env:npm_config_cache = Join-Path $Cache 'npm'
$env:TEMP = Join-Path $Cache 'temp'
$env:TMP = $env:TEMP
$env:HARBOROPS_G17_EVIDENCE_DIR = $EvidenceDirectory
foreach ($directory in @($env:DOTNET_CLI_HOME, $env:NUGET_PACKAGES, $env:NUGET_HTTP_CACHE_PATH, $env:npm_config_cache, $env:TEMP)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}
function Invoke-Required([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Executable exited with code $LASTEXITCODE"
    }
}
Push-Location -LiteralPath $Repo
try {
    $Sha = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or ($ExpectedSha -and !$Sha.StartsWith($ExpectedSha, [System.StringComparison]::OrdinalIgnoreCase))) {
        throw 'Expected source SHA does not match the checkout'
    }
    Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'source-sha.txt') -Value $Sha -NoNewline
    Invoke-Required 'node' @('--test', 'scripts/check-m17-source.test.mjs')
    Invoke-Required 'dotnet' @('restore', 'HarborOps.sln')
    Invoke-Required 'dotnet' @('build', 'HarborOps.sln', '-c', 'Release', '--no-restore')
    Invoke-Required 'dotnet' @('test', 'tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj', '-c', 'Release', '--no-build')
    Invoke-Required 'dotnet' @('test', 'tests/HarborOps.ContractTests/HarborOps.ContractTests.csproj', '-c', 'Release', '--no-build')
    if (!$env:HARBOROPS_TEST_DB) {
        throw 'A verified disposable HARBOROPS_TEST_DB is required for integration tests'
    }
    Invoke-Required 'dotnet' @('test', 'tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj', '-c', 'Release', '--no-build')
    Invoke-Required 'npm' @('--prefix', 'web', 'ci', '--no-audit', '--no-fund')
    Invoke-Required 'npm' @('--prefix', 'web', 'test')
    Invoke-Required 'npm' @('--prefix', 'web', 'run', 'build')

    if ($StartDisposableStack) {
        if (!$env:HARBOROPS_M17_GRAFANA_PASSWORD) {
            throw 'Set a private disposable HARBOROPS_M17_GRAFANA_PASSWORD'
        }
        $existing = & docker ps -a --filter "label=com.docker.compose.project=$ProjectName" --format '{{.ID}}'
        if ($LASTEXITCODE -ne 0 -or $existing) {
            throw 'The disposable project name is already in use, refusing to modify it'
        }
        $composeArgs = @('-p', $ProjectName, '-f', 'compose.yaml', '-f', 'compose.m17.observability.override.yaml', '--profile', 'observability')
        Invoke-Required 'docker' (@('compose') + $composeArgs + @('config', '--quiet'))
        Invoke-Required 'docker' (@('compose') + $composeArgs + @('up', '-d', '--build', 'postgres', 'migrator', 'api', 'web', 'otelcol', 'tempo', 'prometheus', 'grafana'))
        $port = (& docker compose @composeArgs port grafana 3000).Trim()
        if ($LASTEXITCODE -ne 0 -or !$port.StartsWith('127.0.0.1:')) {
            throw 'The local Grafana port is not loopback bound'
        }
        $ready = $false
        for ($i = 0; $i -lt 45; $i++) {
            try {
                $r = Invoke-WebRequest -Uri 'http://127.0.0.1:8080/health/ready' -TimeoutSec 5
                if ($r.StatusCode -eq 200) { $ready = $true; break }
            } catch { Start-Sleep -Seconds 2 }
        }
        if (!$ready) { throw 'The disposable API never became ready' }
        if ($FaultProbe) {
            Invoke-Required 'docker' (@('compose') + $composeArgs + @('stop', 'otelcol'))
            $alive = Invoke-WebRequest -Uri 'http://127.0.0.1:8080/health/live' -TimeoutSec 5
            if ($alive.StatusCode -ne 200) { throw 'Collector outage affected API liveness' }
            $readyAfter = Invoke-WebRequest -Uri 'http://127.0.0.1:8080/health/ready' -TimeoutSec 5
            if ($readyAfter.StatusCode -ne 200) { throw 'Collector outage affected business readiness' }
            Invoke-Required 'docker' (@('compose') + $composeArgs + @('start', 'otelcol'))
        }
        Invoke-Required 'npm' @('--prefix', 'tests/browser', 'ci', '--no-audit', '--no-fund')
        Invoke-Required 'npm' @('--prefix', 'tests/browser', 'exec', '--', 'playwright', 'test', '--config', 'playwright.m17.config.mjs')
        if ($StopOwnedContainers) {
            Invoke-Required 'docker' (@('compose') + $composeArgs + @('stop', 'grafana', 'prometheus', 'tempo', 'otelcol', 'web', 'api', 'postgres'))
        }
    }
    Invoke-Required 'node' @('scripts/g17-manifest.mjs')
    Write-Host "Local M17 verification completed for $Sha. Independent G17-S25 review and acceptance remain separate."
} finally {
    Pop-Location
}
