param(
    [switch]$RefreshDatabase
)

$ErrorActionPreference = 'Stop'
$Root = 'E:\APP IDEAS\HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform'
$Cache = 'E:\APP IDEAS\harborops-m0-cache'
$Evidence = Join-Path $Cache 'evidence\image-scans'

if (!(Test-Path -LiteralPath $Root -PathType Container)) {
    throw 'Required E-drive repository is missing'
}
if (!(Get-Command trivy -ErrorAction SilentlyContinue)) {
    throw 'Trivy must be available before container scan results can be recorded'
}

$env:TRIVY_CACHE_DIR = Join-Path $Cache 'trivy'
$env:TEMP = Join-Path $Cache 'temp'
$env:TMP = $env:TEMP
$env:TMPDIR = $env:TEMP
New-Item -ItemType Directory -Force -Path $env:TRIVY_CACHE_DIR, $env:TEMP, $Evidence | Out-Null

Push-Location -LiteralPath $Root
try {
    if ($RefreshDatabase) {
        trivy image --download-db-only
        if ($LASTEXITCODE -ne 0) { throw 'Could not refresh vulnerability database' }
    }
    $images = @(
        @('harborops-postgres:16.15-rootless', 'postgres'),
        @('harborops-api:latest', 'api'),
        @('harborops-web:latest', 'web')
    )

    foreach ($entry in $images) {
        $image = $entry[0]
        $name = $entry[1]
        $report = Join-Path $Evidence "$name.json"
        trivy image --scanners vuln --severity HIGH,CRITICAL --ignore-unfixed --exit-code 1 --format json --output $report $image
        if ($LASTEXITCODE -ne 0) {
            throw "Image scan did not pass: $image. Examine the report at $report"
        }
    }

    Write-Host 'All three local image scans reported no fixable high or critical findings.'
} finally {
    Pop-Location
}
