param(
    [string]$RepositoryPath = (Split-Path -Parent $PSScriptRoot),
    [string]$ExpectedSha = '',
    [switch]$GenerateLockfile,
    [switch]$InstallBrowsers
)

$ErrorActionPreference = 'Stop'
$Repo = (Resolve-Path -LiteralPath $RepositoryPath).Path
if (-not $Repo.StartsWith('E:\APP IDEAS\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Browser tests must run from a checkout or worktree on E:\APP IDEAS'
}
$Tests = Join-Path $Repo 'tests\browser'
if (!(Test-Path -LiteralPath (Join-Path $Tests 'package.json'))) {
    throw 'The selected repository lacks the browser test project'
}
$sha = (& git -C $Repo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve checkout revision' }
if ($ExpectedSha -and !$sha.StartsWith($ExpectedSha, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Wrong revision: expected $ExpectedSha, found $sha"
}
Write-Host "Browser verification on $Repo at $sha"

$Cache = 'E:\APP IDEAS\harborops-m0-cache'
$env:npm_config_cache = Join-Path $Cache 'npm'
$env:PLAYWRIGHT_BROWSERS_PATH = Join-Path $Cache 'playwright-browsers'
$env:PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD = '1'
$env:TEMP = Join-Path $Cache 'temp'
$env:TMP = $env:TEMP
$env:TMPDIR = $env:TEMP
$env:XDG_CACHE_HOME = Join-Path $Cache 'xdg'
foreach ($directory in @($env:npm_config_cache, $env:PLAYWRIGHT_BROWSERS_PATH, $env:TEMP, $env:XDG_CACHE_HOME)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

Push-Location -LiteralPath $Tests
try {
    if ($GenerateLockfile) {
        & npm install --package-lock-only --ignore-scripts --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'Unable to generate browser dependency lockfile' }
        Write-Host 'Review tests/browser/package-lock.json and commit it before reproducibility verification.'
    }
    $lockfile = Join-Path $Tests 'package-lock.json'
    if (!(Test-Path -LiteralPath $lockfile)) {
        throw 'Missing browser package-lock.json. Generate and commit a real npm lockfile before claiming reproducibility.'
    }

    $manifest = Get-Content -LiteralPath (Join-Path $Tests 'package.json') -Raw | ConvertFrom-Json
    $locked = Get-Content -LiteralPath $lockfile -Raw | ConvertFrom-Json
    if ($locked.lockfileVersion -lt 3 -or $locked.packages.PSObject.Properties[''].Value.devDependencies.PSObject.Properties['@playwright/test'].Value -ne $manifest.devDependencies.'@playwright/test') {
        throw 'Browser dependency lockfile does not match the manifest'
    }

    & npm ci --ignore-scripts --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'Clean browser dependency installation failed' }

    if ($InstallBrowsers) {
        $localCli = Join-Path $Tests 'node_modules\.bin\playwright.cmd'
        & $localCli install chromium firefox webkit
        if ($LASTEXITCODE -ne 0) { throw 'Installing the three E-backed browsers failed' }
    }

    Write-Host 'A local isolated Compose stack must be available at http://localhost:5173 and http://localhost:8080'
    & npm test
    if ($LASTEXITCODE -ne 0) { throw 'One or more browser tests failed' }
    Write-Host "Browser suite passed on source revision $sha"
} finally {
    Pop-Location
}
