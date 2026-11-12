param(
    [Parameter(Mandatory = $true)][string]$EvidenceDirectory,
    [Parameter(Mandatory = $true)][string]$ExpectedSha,
    [string]$ProjectName = 'ho-verify-m17-local',
    [switch]$StartDisposableStack,
    [switch]$FaultProbe,
    [switch]$StopOwnedContainers
)
$ErrorActionPreference = 'Stop'
$Repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (!$Repo.StartsWith('E:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Run from an E: worktree. C: is not a retained HarborOps workspace'
}
if ($ExpectedSha -notmatch '^[0-9a-fA-F]{40}$' -or
    $ProjectName -notmatch '^ho-verify-m17-[a-z0-9-]+$') {
    throw 'Require an exact 40-character SHA and a dedicated ho-verify-m17-* project'
}
if ($FaultProbe -and !$StartDisposableStack) {
    throw 'FaultProbe requires an explicitly started disposable stack'
}
$EvidencePath = [System.IO.Path]::GetFullPath($EvidenceDirectory)
if (!$EvidencePath.StartsWith('E:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The evidence path must be a new directory on E:'
}
if (Test-Path -LiteralPath $EvidencePath) {
    throw 'The evidence directory already exists. Never overwrite historical evidence'
}
New-Item -ItemType Directory -Path $EvidencePath -Force | Out-Null
$StorageRoot = Join-Path $EvidencePath 'disposable-stack'
$env:HARBOROPS_G17_STORAGE_ROOT = $StorageRoot
$env:HARBOROPS_G17_EVIDENCE_DIR = $EvidencePath
$env:HARBOROPS_G17_DISPOSABLE_STACK_STARTED = "false"
$env:HARBOROPS_G17_BROWSER_JSON = Join-Path $EvidencePath 'm17-browser-results.json'
$env:HARBOROPS_G17_CARDINALITY_JSON = Join-Path $EvidencePath 'm17-cardinality-results.json'
$env:HARBOROPS_G17_API_URL = 'http://127.0.0.1:18080'
$Cache = Join-Path (Split-Path -Parent $Repo) 'harborops-m0-cache'
$env:DOTNET_CLI_HOME = Join-Path $Cache 'dotnet-home'
$env:NUGET_PACKAGES = Join-Path $Cache 'nuget\packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $Cache 'nuget\http'
$env:npm_config_cache = Join-Path $Cache 'npm'
$env:PLAYWRIGHT_BROWSERS_PATH = Join-Path $Cache 'playwright-browsers'
$env:TEMP = Join-Path $Cache 'temp'
$env:TMP = $env:TEMP
foreach ($directory in @(
    $env:DOTNET_CLI_HOME, $env:NUGET_PACKAGES, $env:NUGET_HTTP_CACHE_PATH,
    $env:npm_config_cache, $env:PLAYWRIGHT_BROWSERS_PATH, $env:TEMP
)) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }

function Invoke-Required([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Executable exited with code $LASTEXITCODE"
    }
}
$composeArgs = @('-p', $ProjectName, '-f', 'compose.m17.verify.yaml',
    '-f', 'compose.m17.observability.override.yaml', '--profile', 'observability')
$ownedStackStarted = $false

Push-Location -LiteralPath $Repo
try {
    $Sha = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $Sha -cne $ExpectedSha) {
        throw 'Refusing to verify a different or abbreviated source SHA'
    }
    if ((& git status --porcelain).Length -gt 0) {
        throw 'Worktree has uncommitted changes; freeze a clean source first'
    }
    $env:HARBOROPS_SOURCE_SHA = $Sha
    Set-Content -LiteralPath (Join-Path $EvidencePath 'source-sha.txt') -Value $Sha -NoNewline

    Invoke-Required 'node' @('--test', 'scripts/check-m17-source.test.mjs', 'scripts/check-g17-manifest.test.mjs')
    Invoke-Required 'dotnet' @('restore', 'HarborOps.sln')
    Invoke-Required 'dotnet' @('build', 'HarborOps.sln', '-c', 'Release', '--no-restore', '-warnaserror')
    Invoke-Required 'dotnet' @('test', 'tests/HarborOps.UnitTests/HarborOps.UnitTests.csproj',
        '-c', 'Release', '--no-build', '--logger', 'trx;LogFileName=g17-unit.trx',
        '--results-directory', $EvidencePath)
    Invoke-Required 'dotnet' @('test', 'tests/HarborOps.ContractTests/HarborOps.ContractTests.csproj',
        '-c', 'Release', '--no-build', '--logger', 'trx;LogFileName=g17-contract.trx',
        '--results-directory', $EvidencePath)
    Invoke-Required 'npm' @('--prefix', 'web', 'ci', '--no-audit', '--no-fund')
    Invoke-Required 'npm' @('--prefix', 'web', 'test')
    Invoke-Required 'npm' @('--prefix', 'web', 'run', 'build')

    if ($StartDisposableStack) {
        foreach ($variable in @('HARBOROPS_DB_PASSWORD', 'HARBOROPS_API_DB_PASSWORD',
            'HARBOROPS_WORKER_DB_PASSWORD', 'HARBOROPS_M10_CORRECTION_PROOF_KEY',
            'HARBOROPS_M17_GRAFANA_PASSWORD', 'HARBOROPS_M17_LINK_KEY')) {
            if (![Environment]::GetEnvironmentVariable($variable)) {
                throw "An ephemeral disposable secret is required: $variable"
            }
        }
        # Refuse any project already referenced by containers, volumes or networks.
        foreach ($kind in @('ps', 'volume', 'network')) {
            if ($kind -eq 'ps') {
                $preexisting = & docker ps -a --filter "label=com.docker.compose.project=$ProjectName" --format '{{.ID}}'
            } elseif ($kind -eq 'volume') {
                $preexisting = & docker volume ls --filter "label=com.docker.compose.project=$ProjectName" --format '{{.Name}}'
            } else {
                $preexisting = & docker network ls --filter "label=com.docker.compose.project=$ProjectName" --format '{{.Name}}'
            }
            if ($LASTEXITCODE -ne 0 -or $preexisting) {
                throw "The disposable project name is not unused ($kind); refusing to touch it"
            }
        }
        New-Item -ItemType Directory -Path (Join-Path $StorageRoot 'postgres') -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $StorageRoot 'keyring') -Force | Out-Null
        Invoke-Required 'docker' (@('compose') + $composeArgs + @('config', '--quiet'))
        Invoke-Required 'docker' (@('compose') + $composeArgs +
            @('run', '--rm', '--no-deps', '--entrypoint', '/bin/promtool',
              'prometheus', 'check', 'rules', '/etc/prometheus/alerts.yml'))
        Invoke-Required 'docker' (@('compose') + $composeArgs +
            @('run', '--rm', '--no-deps', '--entrypoint', '/bin/promtool',
              'prometheus', 'test', 'rules', '/etc/prometheus/m17-test.yaml'))
        $ownedStackStarted = $true
        $env:HARBOROPS_G17_DISPOSABLE_STACK_STARTED = 'true'
        Invoke-Required 'docker' (@('compose') + $composeArgs +
            @('up', '-d', '--build', 'postgres', 'migrator'))
        $dbPort = (& docker compose @composeArgs port postgres 5432).Trim()
        if ($LASTEXITCODE -ne 0 -or $dbPort -ne '127.0.0.1:155432') {
            throw 'Owned database did not bind the isolated 155432 loopback port'
        }
        # The test connection is created only after the owned PostgreSQL and
        # migrator start. Never inherit a possibly retained HARBOROPS_TEST_DB.
        $env:HARBOROPS_TEST_DB = "Host=127.0.0.1;Port=155432;Database=harborops;" +
            "Username=harborops;Password=$env:HARBOROPS_DB_PASSWORD"
        Invoke-Required 'dotnet' @('test', 'tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj',
            '-c', 'Release', '--no-build', '--logger', 'trx;LogFileName=g17-integration.trx',
            '--results-directory', $EvidencePath)
        Invoke-Required 'docker' (@('compose') + $composeArgs +
            @('up', '-d', '--build', 'api', 'web', 'otelcol', 'tempo', 'prometheus', 'grafana'))
        $uiPort = (& docker compose @composeArgs port grafana 3000).Trim()
        if ($LASTEXITCODE -ne 0 -or $uiPort -ne '127.0.0.1:3301') {
            throw 'The diagnostics dashboard is not isolated to loopback port 3301'
        }
        $ready = $false
        for ($i = 0; $i -lt 45; $i++) {
            try {
                $result = Invoke-WebRequest -Uri 'http://127.0.0.1:18080/health/ready' -TimeoutSec 5
                if ($result.StatusCode -eq 200) { $ready = $true; break }
            } catch { Start-Sleep -Seconds 2 }
        }
        if (!$ready) { throw 'The isolated API never became ready' }
        if ($FaultProbe) {
            try {
                Invoke-Required 'docker' (@('compose') + $composeArgs + @('stop', 'otelcol'))
                $liveness = Invoke-WebRequest -Uri 'http://127.0.0.1:18080/health/live' -TimeoutSec 5
                if ($liveness.StatusCode -ne 200) { throw 'Collector outage affected liveness' }
                $readiness = Invoke-WebRequest -Uri 'http://127.0.0.1:18080/health/ready' -TimeoutSec 5
                if ($readiness.StatusCode -ne 200) { throw 'Collector outage affected readiness' }
            } finally {
                # Restore the collector even if an assertion throws.
                Invoke-Required 'docker' (@('compose') + $composeArgs + @('start', 'otelcol'))
            }
        }
        Push-Location -LiteralPath (Join-Path $Repo 'tests/browser')
        try {
            Invoke-Required 'npm' @('ci', '--no-audit', '--no-fund')
            Invoke-Required 'npx' @('--no-install', 'playwright', 'test',
                '--config', 'playwright.m17.config.mjs')
            Invoke-Required 'npx' @('--no-install', 'playwright', 'test',
                '--config', 'playwright.m17.cardinality.config.mjs')
            $artifacts = Join-Path (Get-Location).Path 'test-results\m17-artifacts'
            if (Test-Path -LiteralPath $artifacts) {
                Copy-Item -LiteralPath $artifacts -Destination (Join-Path $EvidencePath 'browser-artifacts') -Recurse
            }
        } finally { Pop-Location }
    }
    Invoke-Required 'node' @('scripts/g17-manifest.mjs')
    Invoke-Required 'node' @('scripts/verify-g17-manifest.mjs',
        (Join-Path $EvidencePath 'manifest.json'), $Sha)
    Write-Host "Candidate checks collected for $Sha. G17 review and acceptance remain separate."
} finally {
    if ($StopOwnedContainers -and $ownedStackStarted) {
        # Stop only the uniquely identified verification project. Never down -v
        # or remove named volumes, retained data, or historical evidence.
        try {
            Invoke-Required 'docker' (@('compose') + $composeArgs + @(
                'stop', 'grafana', 'prometheus', 'tempo', 'otelcol', 'web', 'api', 'postgres'))
        } catch {
            Write-Warning 'Owned verification containers could not all be stopped'
        }
    }
    Pop-Location
}
