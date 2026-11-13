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
$Hosted = $env:GITHUB_ACTIONS -eq 'true' -and
    $env:HARBOROPS_G17_HOSTED -eq 'true' -and
    -not [string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)
if ($Hosted) {
    $workspace = [System.IO.Path]::GetFullPath($env:GITHUB_WORKSPACE)
    if ($Repo -cne $workspace) {
        throw 'Hosted verification must use the exact Actions checkout worktree'
    }
}
elseif (!$Repo.StartsWith('E:\', [System.StringComparison]::OrdinalIgnoreCase)) {
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
if ($Hosted) {
    $tempRoot = [System.IO.Path]::GetFullPath($env:RUNNER_TEMP).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    if (!$EvidencePath.StartsWith($tempRoot, [System.StringComparison]::Ordinal)) {
        throw 'Hosted evidence must be under the ephemeral runner temp root'
    }
}
elseif (!$EvidencePath.StartsWith('E:\', [System.StringComparison]::OrdinalIgnoreCase)) {
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
$env:HARBOROPS_G17_VERIFY_WORKER = if ($VerifyDisposableWorker) { 'true' } else { 'false' }
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
    'g17-unit.trx','g17-contract.trx','g17-coverage-preflight.json',
    'g17-node-tests.xml')
if ($StartDisposableStack) {
    $required += @('m17-promtool-check.txt','m17-promtool-test.txt',
        'm17-network-results.json','g17-integration.trx','g17-regressions.trx','g17-regression-matrix-results.json',
        'm17-browser-results.json',
        'm17-cardinality-results.json','m17-cardinality-raw.json',
        'm17-performance-raw.json','m17-restart-results.json',
        'm17-export-privacy-results.json','m17-storage-results.json',
        'm17-retention-age-results.json','m17-tempo-retention-results.json',
        'm17-retention-matrix-results.json',
        'm17-database-fault-results.json','m17-cardinality-memory-results.json',
        'm17-live-latency-alert-results.json')
    if ($VerifyDisposableWorker) { $required += @('m17-worker-results.json','m17-worker-journey-results.json') }
    if ($FaultProbe) { $required += @('m17-collector-fault-results.json',
        'm17-alert-matrix-results.json','m17-pressure-matrix-results.json') }
    if ($RequireFullG17) { $required += 'g17-technical-assertion-ledger.json' }
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

    Invoke-Required 'node' @('--test','--test-reporter=junit', 'scripts/check-m17-source.test.mjs',
        'scripts/check-g17-manifest.test.mjs',
        'scripts/audit-g17-test-coverage.test.mjs',
        'scripts/g17-results-contract.test.mjs',
        'scripts/g17-source-boundary.test.mjs',
        'scripts/reconcile-g17-assertions.test.mjs',
        'scripts/g17-adversarial-evidence.test.mjs') -Stage 'source-and-negative-controls' -CaptureFile 'g17-node-tests.xml'
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
        if ($Hosted) {
            # Only newly created runner-temp directories receive process UIDs.
            & sudo chown '70:70' (Join-Path $StorageRoot 'postgres')
            if ($LASTEXITCODE -ne 0) { throw 'Cannot prepare owned PostgreSQL scratch UID' }
            & sudo chown '1654:1654' (Join-Path $StorageRoot 'keyring')
            if ($LASTEXITCODE -ne 0) { throw 'Cannot prepare owned application keyring UID' }
        }
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
        # Run representative accepted milestone suites with M17 explicitly opted
        # in and a deliberately inaccessible host-side OTLP endpoint. This proves
        # the feature does not block accepted business and hold boundaries.
        $regressionFilter = 'FullyQualifiedName~TrackingWorkerG9ProcessTests|FullyQualifiedName~TrackingProjectionG10Tests|FullyQualifiedName~M11ClosureRuntimeTests|FullyQualifiedName~M13AdmissionControlAcceptanceTests|FullyQualifiedName~M15NotificationRuntimeTests|FullyQualifiedName~G15AuditAcceptanceTests|FullyQualifiedName~ReferenceDataHttpTests|FullyQualifiedName~G13G15FeatureDefaultAcceptanceTests'
        $env:M17__Enabled = 'true'
        $env:M17__OtlpEndpoint = 'http://127.0.0.1:4317'
        $env:M17__LinkKey = $env:HARBOROPS_M17_LINK_KEY
        $env:OTEL_SPAN_EVENT_COUNT_LIMIT = '0'
        $env:OTEL_SPAN_LINK_COUNT_LIMIT = '0'
        try {
            Invoke-Required 'dotnet' @('test',
                'tests/HarborOps.IntegrationTests/HarborOps.IntegrationTests.csproj',
                '-c','Release','--no-build','--filter',$regressionFilter,
                '--logger','trx;LogFileName=g17-regressions.trx',
                '--results-directory',$EvidencePath) -Stage 'accepted-milestone-regressions-opted-in'
        }
        finally {
            foreach ($var in @('M17__Enabled','M17__OtlpEndpoint','M17__LinkKey',
                'OTEL_SPAN_EVENT_COUNT_LIMIT','OTEL_SPAN_LINK_COUNT_LIMIT')) {
                Remove-Item "Env:$var" -ErrorAction SilentlyContinue
            }
        }
        Invoke-Required 'node' @('scripts/check-g17-regression-matrix.mjs') -Stage 'accepted-regression-case-identity'
        if ($FaultProbe) {
            Invoke-Required 'node' @('scripts/check-g17-database-fault.mjs') -Stage 'postgres-readiness-alert-and-durable-recovery'
            Invoke-Required 'node' @('scripts/check-g17-collector-fault.mjs') -Stage 'collector-business-fault-and-recovery'
        }
        Invoke-Required 'node' @('scripts/check-g17-live-latency.mjs') -Stage 'live-read-latency-histogram-and-p95-alert'
        if ($FaultProbe) {
            Invoke-Required 'node' @('scripts/check-g17-alert-matrix.mjs') -Stage 'live-alerts-and-hysteresis-reconciliation'
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
        Invoke-Required 'node' @('scripts/check-g17-export-privacy.mjs') -Stage 'tempo-and-prometheus-negative-privacy-canaries'
        Invoke-Required 'node' @('scripts/check-g17-storage.mjs') -Stage 'owned-telemetry-storage-isolation-and-pressure'
        Invoke-Required 'node' @('scripts/check-g17-retention.mjs') -Stage 'isolated-twenty-four-hour-expiration-proof'
        Invoke-Required 'node' @('scripts/check-g17-tempo-retention.mjs') -Stage 'isolated-tempo-expiration-with-positive-trace-canary'
        Invoke-Required 'node' @('scripts/check-g17-retention-matrix.mjs') -Stage 'combined-telemetry-retention-and-app-history-proof'
        Invoke-Required 'node' @('scripts/check-g17-restart.mjs') -Stage 'api-cancellation-restart-and-durable-invariants'
        Invoke-Required 'node' @('scripts/check-g17-cardinality-memory.mjs') -Stage 'forty-thousand-hostile-paths-memory-stability'
        Invoke-Required 'node' @('scripts/check-g17-performance.mjs') -Stage 'three-paired-overhead-measurements'
        if ($FaultProbe) {
            Invoke-Required 'node' @('scripts/check-g17-pressure-matrix.mjs') -Stage 'tenfold-business-pressure-and-exporter-isolation'
        }
        if ($VerifyDisposableWorker) {
            Invoke-Required 'docker' (@('compose') + $composeArgs +
                @('up','-d','--build','worker'))
            Invoke-Required 'node' @('scripts/check-g17-worker.mjs')
            Invoke-Required 'node' @('scripts/check-g17-worker-journey.mjs') -Stage 'real-restricted-worker-and-causal-tempo-bridge'
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
    if ($sourceVerified -and $RequireFullG17 -and $runState -eq 'COMPLETE') {
        & node scripts/reconcile-g17-assertions.mjs $EvidencePath $sha
        if ($LASTEXITCODE -ne 0) {
            $runState = 'FAILED'
            $runFailureType = 'AtomicAssertionReconciliationFailure'
            $runInfo.run_state = 'FAILED'
            $runInfo.failure_type = $runFailureType
            $runInfo | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (
                Join-Path $EvidencePath 'run-status.json') -Encoding utf8
            Write-Warning 'One or more G17 technical assertions lacked passing raw witnesses'
        }
    }
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
