param(
    [Parameter(Mandatory = $true)][string]$EvidenceDirectory,
    [Parameter(Mandatory = $true)][string]$ExpectedSha,
    [string]$ProjectName = 'ho-verify-m17-local',
    [switch]$StartDisposableStack,
    [switch]$FaultProbe,
    [switch]$VerifyDisposableWorker,
    [switch]$RequireFullG17,
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
if (($FaultProbe -or $VerifyDisposableWorker) -and !$StartDisposableStack) {
    throw 'Fault or worker verification requires a fresh disposable stack'
}
if ($RequireFullG17 -and (!$StartDisposableStack -or !$FaultProbe -or
        !$VerifyDisposableWorker -or !$StopOwnedContainers)) {
    throw 'Full G17 requires disposable stack, collector fault, worker fixture and owned cleanup'
}
$EvidencePath = [System.IO.Path]::GetFullPath($EvidenceDirectory)
if (!$EvidencePath.StartsWith('E:\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The evidence path must be a new directory on E:'
}
if (Test-Path -LiteralPath $EvidencePath) {
    throw 'The evidence directory already exists. Never overwrite historical evidence'
}
New-Item -ItemType Directory -Path $EvidencePath -Force | Out-Null
$StorageRoot = Join-Path (Split-Path -Parent $EvidencePath) (
    (Split-Path -Leaf $EvidencePath) + '-disposable-stack')
if (Test-Path -LiteralPath $StorageRoot) {
    throw 'The disposable stack path already exists; choose a new evidence identifier'
}
$env:HARBOROPS_G17_STORAGE_ROOT = $StorageRoot
$env:HARBOROPS_G17_EVIDENCE_DIR = $EvidencePath
$env:HARBOROPS_G17_DISPOSABLE_STACK_STARTED = "false"
$env:HARBOROPS_G17_BROWSER_JSON = Join-Path $EvidencePath 'm17-browser-results.json'
$env:HARBOROPS_G17_CARDINALITY_JSON = Join-Path $EvidencePath 'm17-cardinality-results.json'
$env:HARBOROPS_G17_BROWSER_ARTIFACT_DIR = Join-Path $EvidencePath 'browser-artifacts'
$env:HARBOROPS_G17_CARDINALITY_ARTIFACT_DIR = Join-Path $EvidencePath 'cardinality-artifacts'
$env:HARBOROPS_G17_PROJECT = $ProjectName
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

$script:stageLog = [System.Collections.Generic.List[object]]::new()
$script:stageNumber = 0
function Invoke-Required(
    [string]$Executable, [string[]]$Arguments,
    [string]$Stage = '', [string]$CaptureFile = ''
) {
    $script:stageNumber++
    $name = if ($Stage) { $Stage } else { "$Executable-$script:stageNumber" }
    $started = [DateTimeOffset]::UtcNow.ToString('O')
    $exit = -1
    try {
        if ($CaptureFile) {
            $output = & $Executable @Arguments 2>&1
            $exit = $LASTEXITCODE
            $output | Out-File -LiteralPath (Join-Path $EvidencePath $CaptureFile) -Encoding utf8
        }
        else {
            & $Executable @Arguments
            $exit = $LASTEXITCODE
        }
        if ($exit -ne 0) { throw "Stage $name failed with exit code $exit" }
        $script:stageLog.Add([pscustomobject]@{
            stage = $name; status = 'PASS'; exit_code = 0; started_at_utc = $started
            ended_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
        })
    }
    catch {
        $script:stageLog.Add([pscustomobject]@{
            stage = $name; status = 'FAIL'; exit_code = $exit; started_at_utc = $started
            ended_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
        })
        throw "Stage failed: $name (exit $exit)"
    }
}
$composeArgs = @('-p', $ProjectName, '-f', 'compose.m17.verify.yaml')
if ($VerifyDisposableWorker) { $composeArgs += @('-f','compose.m9.worker.yaml') }
$composeArgs += @('-f','compose.m17.observability.override.yaml')
if ($VerifyDisposableWorker) { $composeArgs += @('-f','compose.m17.worker.override.yaml') }
$composeArgs += @('--profile','observability')
if ($VerifyDisposableWorker) { $composeArgs += @('--profile','worker') }
$ownedStackStarted = $false
$sourceVerified = $false
$runState = 'FAILED'
$runFailureType = $null
$sha = $null
$required = @('source-sha.txt','run-status.json','expected-artifacts.json',
    'g17-unit.trx','g17-contract.trx','g17-coverage-preflight.json')
if ($StartDisposableStack) {
    $required += @('m17-promtool-check.txt','m17-promtool-test.txt',
        'm17-network-results.json','g17-integration.trx','m17-browser-results.json',
        'm17-cardinality-results.json','m17-cardinality-raw.json')
    if ($VerifyDisposableWorker) { $required += 'm17-worker-results.json' }
}
$expected = @{
    schema_version = 1; gate = 'G17'; source_sha = $ExpectedSha
    project = $ProjectName; required_artifacts = $required
    disposable_stack = [bool]$StartDisposableStack
    fault_probe = [bool]$FaultProbe
    worker_fixture = [bool]$VerifyDisposableWorker
    require_full_g17 = [bool]$RequireFullG17
}
$expected | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (
    Join-Path $EvidencePath 'expected-artifacts.json') -Encoding utf8

Push-Location -LiteralPath $Repo
try {
    $Sha = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $Sha -cne $ExpectedSha) {
        throw 'Refusing to verify a different or abbreviated source SHA'
    }
    if ((& git status --porcelain).Length -gt 0) {
        throw 'Worktree has uncommitted changes; freeze a clean source first'
    }
    $sourceVerified = $true
    $env:HARBOROPS_SOURCE_SHA = $Sha
    Set-Content -LiteralPath (Join-Path $EvidencePath 'source-sha.txt') -Value $Sha -NoNewline

    Invoke-Required 'node' @('--test', 'scripts/check-m17-source.test.mjs',
        'scripts/check-g17-manifest.test.mjs',
        'scripts/audit-g17-test-coverage.test.mjs',
        'scripts/g17-results-contract.test.mjs',
        'scripts/g17-source-boundary.test.mjs') -Stage 'source-and-negative-controls'
    Invoke-Required 'node' @('scripts/audit-g17-test-coverage.mjs','--strict',
        ('--output=' + (Join-Path $EvidencePath 'g17-coverage-preflight.json'))) -Stage 's25-atomic-coverage-preflight'
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
        New-Item -ItemType Directory -Path $StorageRoot | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $StorageRoot 'postgres') | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $StorageRoot 'keyring') | Out-Null
        Invoke-Required 'docker' (@('compose') + $composeArgs + @('config', '--quiet'))
        Invoke-Required 'docker' (@('compose') + $composeArgs +
            @('run', '--rm', '--no-deps', '--entrypoint', '/bin/promtool',
              'prometheus', 'check', 'rules', '/etc/prometheus/alerts.yml')) -Stage 'promtool-check' -CaptureFile 'm17-promtool-check.txt'
        Invoke-Required 'docker' (@('compose') + $composeArgs +
            @('run', '--rm', '--no-deps', '--entrypoint', '/bin/promtool',
              'prometheus', 'test', 'rules', '/etc/prometheus/m17-test.yaml')) -Stage 'promtool-test' -CaptureFile 'm17-promtool-test.txt'
        $ownedStackStarted = $true
        $env:HARBOROPS_G17_DISPOSABLE_STACK_STARTED = 'true'
        Invoke-Required 'docker' (@('compose') + $composeArgs +
            @('up', '-d', '--build', 'postgres', 'migrator'))
        $dbPort = (& docker compose @composeArgs port postgres 5432).Trim()
        if ($LASTEXITCODE -ne 0 -or $dbPort -ne '127.0.0.1:155432') {
            throw 'Owned database did not bind the isolated 155432 loopback port'
        }
        # The integration suite rejects the application database name. Create
        # a second EMPTY database inside the newly owned PostgreSQL container.
        # Never run integration migrations against the application database.
        Invoke-Required 'docker' (@('compose') + $composeArgs + @(
            'exec','-T','postgres','psql','-U','harborops','-d','postgres',
            '-v','ON_ERROR_STOP=1','-c','CREATE DATABASE harborops_integration'
        )) -Stage 'owned-integration-database-create'
        $env:HARBOROPS_TEST_DB = "Host=127.0.0.1;Port=155432;Database=harborops_integration;" +
            "Username=harborops;Password=$env:HARBOROPS_DB_PASSWORD"
        Invoke-Required 'dotnet' @('test', 'tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj',
            '-c', 'Release', '--no-build', '--logger', 'trx;LogFileName=g17-integration.trx',
            '--results-directory', $EvidencePath)
        Invoke-Required 'docker' (@('compose') + $composeArgs +
            @('up', '-d', '--build', 'api', 'web', 'otelcol', 'tempo', 'prometheus', 'grafana'))
        Invoke-Required 'node' @('scripts/check-g17-network.mjs') -Stage 'telemetry-network-isolation'
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
            # Both result directories are written directly to the E: evidence root.
        } finally { Pop-Location }
        if ($VerifyDisposableWorker) {
            Invoke-Required 'docker' (@('compose') + $composeArgs +
                @('up','-d','--build','worker'))
            Invoke-Required 'node' @('scripts/check-g17-worker.mjs')
        }
    }
    $runState = 'COMPLETE'
}
catch {
    $runFailureType = $_.Exception.GetType().Name
    Write-Warning 'The verification campaign failed. Retaining its source-locked failure evidence.'
}
finally {
    if ($StopOwnedContainers -and $ownedStackStarted) {
        try {
            $services = @('grafana','prometheus','tempo','otelcol','web','api','postgres')
            if ($VerifyDisposableWorker) { $services += 'worker' }
            & docker compose @composeArgs stop @services
            if ($LASTEXITCODE -ne 0) { throw 'Owned service stop failed' }
            $script:stageLog.Add([pscustomobject]@{
                stage = 'disposable-cleanup'; status = 'PASS'; exit_code = 0
            })
        } catch {
            $runState = 'FAILED'
            $runFailureType = 'DisposableCleanupFailure'
            $script:stageLog.Add([pscustomobject]@{
                stage = 'disposable-cleanup'; status = 'FAIL'; exit_code = $LASTEXITCODE
            })
            Write-Warning 'Some disposable services could not be stopped'
        }
    }
    $runInfo = @{
        schema_version = 2; gate = 'G17'; source_sha = $sha
        requested_source_sha = $ExpectedSha; source_verified = $sourceVerified
        project = $ProjectName; run_state = $runState
        fault_probe_requested = [bool]$FaultProbe
        worker_fixture_requested = [bool]$VerifyDisposableWorker
        full_campaign_requested = [bool]$RequireFullG17
        failure_type = $runFailureType; stage_results = $script:stageLog.ToArray()
        required_artifacts = $required; g17_assertion_review = 'NOT_REVIEWED'
        gate_acceptance = 'NOT_ACCEPTED'
        finished_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
    }
    $runInfo | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (
        Join-Path $EvidencePath 'run-status.json') -Encoding utf8
    if ($sourceVerified) {
        try {
            & node scripts/g17-manifest.mjs
            if ($LASTEXITCODE -ne 0) { throw 'Evidence manifest creation failed' }
            & node scripts/verify-g17-manifest.mjs (
                Join-Path $EvidencePath 'manifest.json') $sha
            if ($LASTEXITCODE -ne 0) { throw 'Evidence manifest verification failed' }
        } catch {
            $runState = 'FAILED'
            Write-Warning 'Evidence indexing failed. Preserve raw files for investigation.'
        }
    }
    Pop-Location
}
if ($runState -ne 'COMPLETE') {
    throw 'G17 verification failed; preserve the FAILED evidence and inspect stage results'
}
Write-Host "G17 candidate checks complete for $sha; independent acceptance pending."
