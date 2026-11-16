# Removes the containers and networks of ONE disposable G17 verification project after an interrupted or failed
# campaign. The state is captured as evidence first, every resource is checked against the exact project, compose
# service and name it must carry, and any doubt removes nothing. Volumes and images are never removed.
#
# usage: pwsh -File scripts/cleanup-g17-owned.ps1 -ProjectName ho-verify-m17-xyz -EvidenceDirectory E:\...\cleanup-xyz [-StopRunning]
param(
    [Parameter(Mandatory = $true)][string]$ProjectName,
    [Parameter(Mandatory = $true)][string]$EvidenceDirectory,
    [switch]$StopRunning
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'g17-docker-ownership.ps1')
Assert-G17ProjectName $ProjectName
$evidencePath = [System.IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path -LiteralPath $evidencePath) { throw 'The cleanup evidence directory already exists. Never overwrite evidence' }
New-Item -ItemType Directory -Path $evidencePath | Out-Null
$result = Remove-G17OwnedResources -Project $ProjectName -EvidenceDirectory $evidencePath -StopRunning:$StopRunning
"Removed $($result.containers_removed) container(s) and $($result.networks_removed) network(s) of $ProjectName; $($result.volumes_retained.Count) volume(s) retained."
