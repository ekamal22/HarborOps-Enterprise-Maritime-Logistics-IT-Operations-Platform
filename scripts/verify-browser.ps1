param(
    [switch]$GenerateLockfile,
    [switch]$InstallBrowsers
)

$ErrorActionPreference = 'Stop'
$Workspace = 'E:\APP IDEAS'
$Repo = Join-Path $Workspace 'HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform'
$Cache = Join-Path $Workspace 'harborops-m0-cache'
$Tests = Join-Path $Repo 'tests\browser'
if (!(Test-Path -LiteralPath $Tests -PathType Container)) {
    throw "Browser test project is missing from the required E-drive repository"
}

$env:npm_config_cache = Join-Path $Cache 'npm'
$env:PLAYWRIGHT_BROWSERS_PATH = Join-Path $Cache 'playwright-browsers'
$env:PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD = '1'
$env:TEMP = Join-Path $Cache 'temp'
$env:TMP = $env:TEMP
$env:TMPDIR = $env:TEMP
$env:XDG_CACHE_HOME = Join-Path $Cache 'xdg'
foreach ($path in @($env:npm_config_cache, $env:PLAYWRIGHT_BROWSERS_PATH, $env:TEMP, $env:XDG_CACHE_HOME)) {
    New-Item -ItemType Directory -Force -Path $path | Out-Null
}

Push-Location -LiteralPath $Tests
try {
    if ($GenerateLockfile) {
        npm install --package-lock-only --ignore-scripts --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw "Could not generate browser lockfile" }
        Write-Host 'Review and commit tests/browser/package-lock.json before treating this as reproducible verification.'
    }
    if (!(Test-Path -LiteralPath 'package-lock.json')) {
        throw 'Missing browser lockfile. Use -GenerateLockfile, review and commit it, then rerun.'
    }
    npm ci --ignore-scripts --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'Browser dependencies did not install cleanly' }

    if ($InstallBrowsers) {
        npx playwright install chromium firefox webkit
        if ($LASTEXITCODE -ne 0) { throw 'Browser binary setup failed' }
    }

    Write-Host 'The local HarborOps Compose web and API must be running before browser tests.'
    npm test
    if ($LASTEXITCODE -ne 0) { throw 'Cross-browser tests failed' }
} finally {
    Pop-Location
}
