param()

$ErrorActionPreference = 'Stop'
$Repo = 'E:\APP IDEAS\HarborOps-Enterprise-Maritime-Logistics-IT-Operations-Platform'
$Cache = 'E:\APP IDEAS\harborops-m0-cache'
if (!(Test-Path -LiteralPath $Repo -PathType Container)) {
    throw 'Repository must be under E:\APP IDEAS'
}

$env:DOTNET_CLI_HOME = Join-Path $Cache 'dotnet-home'
$env:NUGET_PACKAGES = Join-Path $Cache 'nuget\packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $Cache 'nuget\http'
$env:TEMP = Join-Path $Cache 'temp'
$env:TMP = $env:TEMP
$env:TMPDIR = $env:TEMP
foreach ($path in @($env:DOTNET_CLI_HOME, $env:NUGET_PACKAGES, $env:NUGET_HTTP_CACHE_PATH, $env:TEMP)) {
    New-Item -ItemType Directory -Force -Path $path | Out-Null
}

$PortableSdk = Join-Path $Cache 'dotnet-sdk'
if (Test-Path -LiteralPath (Join-Path $PortableSdk 'dotnet.exe')) {
    $env:DOTNET_ROOT = $PortableSdk
    $env:PATH = "$PortableSdk;$env:PATH"
}

Push-Location -LiteralPath $Repo
try {
    $version = (& dotnet --version)
    if ($LASTEXITCODE -ne 0 -or $version -notmatch '^10\.') {
        throw 'A .NET 10 SDK is required. Use an existing supported SDK or install a reviewed official .NET 10 SDK into E:\APP IDEAS\harborops-m0-cache\dotnet-sdk. Do not use C: for installation.'
    }
    Write-Host "Using .NET SDK $version"
    & dotnet restore HarborOps.sln
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
    & dotnet build HarborOps.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
} finally {
    Pop-Location
}
