param(
    [Parameter(Mandatory = $true)][string]$Project,
    [string]$RepositoryPath = (Split-Path -Parent $PSScriptRoot),
    [string]$ExpectedSha = '',
    [switch]$RefreshDatabase,
    [switch]$UseContainerScanner,
    [string]$ScannerImage = 'aquasec/trivy:0.58.1'
)

$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path -LiteralPath $RepositoryPath).Path
if (-not $Root.StartsWith('E:\APP IDEAS\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Image verification requires an E-drive checkout'
}
if ($Project -notmatch '^ho-verify(?:-[a-z0-9-]+)?$') {
    throw 'Use an explicitly isolated ho-verify Compose project; refusing to scan unrelated images'
}
$sha = (& git -C $Root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify repository HEAD' }
if ($ExpectedSha -and !$sha.StartsWith($ExpectedSha, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Wrong source revision, expected $ExpectedSha but found $sha"
}

$Cache = 'E:\APP IDEAS\harborops-m0-cache'
$Evidence = Join-Path $Cache "evidence\m0-hardening\image-scans\$sha"
$env:TRIVY_CACHE_DIR = Join-Path $Cache 'trivy'
$env:TEMP = Join-Path $Cache 'temp'
$env:TMP = $env:TEMP
$env:TMPDIR = $env:TEMP
New-Item -ItemType Directory -Force -Path $env:TRIVY_CACHE_DIR, $env:TEMP, $Evidence | Out-Null

if (!$UseContainerScanner -and !(Get-Command trivy -ErrorAction SilentlyContinue)) {
    throw 'Trivy is not installed on the host. Use -UseContainerScanner with a reviewed scanner image already stored on E:.'
}

Push-Location -LiteralPath $Root
try {
    $services = @('postgres', 'api', 'web')
    $images = @{}
    foreach ($service in $services) {
        $container = (& docker compose --project-name $Project ps -q $service)
        if ($LASTEXITCODE -ne 0 -or !$container) { throw "Service $service is not running in project $Project" }
        $container = $container.Trim()
        $actualProject = (& docker inspect --format '{{index .Config.Labels "com.docker.compose.project"}}' $container).Trim()
        if ($LASTEXITCODE -ne 0 -or $actualProject -ne $Project) { throw "Service $service does not belong to $Project" }
        $imageId = (& docker inspect --format '{{.Image}}' $container).Trim()
        if ($LASTEXITCODE -ne 0 -or $imageId -notmatch '^sha256:[0-9a-f]{64}$') {
            throw "Could not identify immutable image ID for $service"
        }
        $imageRevision = (& docker image inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' $imageId).Trim()
        if ($LASTEXITCODE -ne 0 -or $imageRevision -ne $sha) {
            throw "Stale $service image: expected source revision $sha but found $imageRevision"
        }
        $images[$service] = $imageId
    }

    $metadata = [ordered]@{ project = $Project; sourceSha = $sha; imageIds = $images }
    $metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Evidence 'images.json') -Encoding utf8

    if ($UseContainerScanner) {
        $localScanner = & docker image inspect --format '{{.Id}}' $ScannerImage
        if ($LASTEXITCODE -ne 0 -or !$localScanner) {
            throw "Scanner image $ScannerImage is not available locally. Review its version and pull it into the E-backed Docker store before retrying."
        }
        $scannerCmd = {
            param([string[]]$ScannerArguments)
            & docker run --rm --pull never -v "${env:TRIVY_CACHE_DIR}:/root/.cache/trivy" -v "${Evidence}:/evidence" -v /var/run/docker.sock:/var/run/docker.sock $ScannerImage @ScannerArguments
            if ($LASTEXITCODE -ne 0) { throw "Container image scanner failed with exit code $LASTEXITCODE" }
        }
        if ($RefreshDatabase) {
            & $scannerCmd @('image', '--download-db-only')
        }
        foreach ($service in $services) {
            & $scannerCmd @('image', '--scanners', 'vuln', '--severity', 'HIGH,CRITICAL',
                '--exit-code', '1', '--format', 'json', '--output', "/evidence/$service.json", $images[$service])
        }
    } else {
        if ($RefreshDatabase) {
            & trivy image --download-db-only
            if ($LASTEXITCODE -ne 0) { throw 'Trivy vulnerability database update failed' }
        }
        foreach ($service in $services) {
            $report = Join-Path $Evidence "$service.json"
            & trivy image --scanners vuln --severity HIGH,CRITICAL --exit-code 1 --format json --output $report $images[$service]
            if ($LASTEXITCODE -ne 0) {
                throw "High/critical vulnerability finding or scanner failure in $service ($($images[$service])); see $report"
            }
        }
    }
    Write-Host "All final running images passed HIGH/CRITICAL scanning for project $Project at $sha"
} finally {
    Pop-Location
}
