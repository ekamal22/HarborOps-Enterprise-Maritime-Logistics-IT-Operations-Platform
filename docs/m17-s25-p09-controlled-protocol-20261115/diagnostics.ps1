# Passive host and Docker VM diagnostics for one controlled P09 run. It reads counters only; it never touches the owned
# project, its containers, its network or the measurement script. Its own cost (about one process wake per second) is
# present for all six rows of a run, and is disclosed in the protocol.
# usage: powershell -File diagnostics.ps1 -Action Start -OutDir <dir> -Project <ho-verify-m17-...> -SidecarImage <local image with sh and cat>
#        powershell -File diagnostics.ps1 -Action Stop  -OutDir <dir>
param(
    [Parameter(Mandatory = $true)][ValidateSet('Start', 'Stop')][string]$Action,
    [Parameter(Mandatory = $true)][string]$OutDir,
    [string]$Project = '',
    [string]$SidecarImage = 'ho-verify-m17-postgres:16.15-rootless'
)
$ErrorActionPreference = 'Stop'
$stateFile = Join-Path $OutDir 'diag-state.json'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

if ($Action -eq 'Start') {
    if (Test-Path $stateFile) { throw 'Diagnostics already started for this directory.' }
    if ($Project -notmatch '^ho-verify-m17-[a-z0-9-]+$') { throw 'An owned ho-verify-m17-* project name is required.' }
    $sidecar = "$Project-p09-diag"
    # Host counters, one row per second, timestamped by the OS.
    $counters = @('\Processor(_Total)\% Processor Time', '\Processor(_Total)\% Privileged Time', '\Memory\Available MBytes',
        '\PhysicalDisk(_Total)\% Disk Time', '\Process(vmmemWSL)\% Processor Time')
    $typeperf = Start-Process -FilePath typeperf -ArgumentList (($counters | ForEach-Object { '"' + $_ + '"' }) + @('-si', '1', '-f', 'CSV', '-o', ('"' + (Join-Path $OutDir 'host-counters.csv') + '"'), '-y')) -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $OutDir 'typeperf.out') -RedirectStandardError (Join-Path $OutDir 'typeperf.err')
    # Container lifecycle events of the owned project give the start time of every measured row (each row recreates the API).
    $eventArgs = 'events --filter "label=com.docker.compose.project=' + $Project + '" --format "{{json .}}"'
    $events = Start-Process -FilePath docker -ArgumentList $eventArgs -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $OutDir 'docker-events.jsonl') -RedirectStandardError (Join-Path $OutDir 'docker-events.err')
    # Docker VM scheduler pressure and CPU time (steal included), read from a throwaway sidecar that shares only the VM process namespace.
    try { docker rm -f $sidecar 2>&1 | Out-Null } catch { }
    $loop = 'while true; do echo T $(date +%s%N); head -1 /proc/stat; cat /proc/pressure/cpu; cat /proc/pressure/memory; cat /proc/pressure/io; sleep 1; done'
    docker run -d --name $sidecar --label owner=p09-controlled-diagnostics --network none --pid=host --entrypoint sh $SidecarImage -c $loop | Out-Null
    $logs = Start-Process -FilePath docker -ArgumentList @('logs', '-f', $sidecar) -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $OutDir 'vm-pressure.log') -RedirectStandardError (Join-Path $OutDir 'vm-pressure.err')
    [pscustomobject]@{ started_utc = [DateTime]::UtcNow.ToString('o'); project = $Project; sidecar = $sidecar
        pids = [pscustomobject]@{ typeperf = $typeperf.Id; events = $events.Id; logs = $logs.Id }
        timezone_offset_minutes = [int][TimeZoneInfo]::Local.GetUtcOffset([DateTime]::Now).TotalMinutes } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $stateFile -Encoding utf8
    'diagnostics started'
    return
}

$state = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json
foreach ($id in @($state.pids.typeperf, $state.pids.events, $state.pids.logs)) { Stop-Process -Id $id -Force -ErrorAction SilentlyContinue }
try { docker rm -f $state.sidecar 2>&1 | Out-Null } catch { }
# Order matters: the final state (including stopped_utc) is written FIRST, so the hash list describes the bytes that remain on disk.
# A previous revision hashed diag-state.json before adding stopped_utc, leaving one internal mismatch per run (finding 2026-11-16).
$state | Add-Member -NotePropertyName stopped_utc -NotePropertyValue ([DateTime]::UtcNow.ToString('o')) -Force
$state | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $stateFile -Encoding utf8
# The hash list is written last and never lists itself (a self-referential hash cannot be correct).
$files = Get-ChildItem -LiteralPath $OutDir -File | Where-Object { $_.Name -ne 'diag-hashes.txt' }
$files | ForEach-Object { '{0}  {1}  {2}' -f (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLower(), $_.Length, $_.Name } |
    Set-Content -LiteralPath (Join-Path $OutDir 'diag-hashes.txt') -Encoding ascii
'diagnostics stopped'
