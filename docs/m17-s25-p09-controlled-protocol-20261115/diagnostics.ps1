# Passive host and Docker VM diagnostics for one controlled P09 run. It reads counters only; it never touches the owned
# project, its containers, its network or the measurement script. Its own cost (about one process wake per second) is
# present for all six rows of a run, and is disclosed in the protocol.
# usage: powershell -File diagnostics.ps1 -Action Start -OutDir <dir> -Project <ho-verify-m17-...> -SidecarImage <local image with sh and cat>
#        powershell -File diagnostics.ps1 -Action Stop  -OutDir <dir>
#
# Stop is deliberately strict. It signals only a process whose identity (executable name, creation time and command line)
# matches what Start recorded and what this script itself expects for that role, it removes only the sidecar container it
# created (matched by id, name and label), it waits for every child to exit, it proves the output files have stopped
# changing before hashing them, and it re-checks them after hashing. Any mismatch stops with an error and touches nothing
# that is not provably owned by this run.
param(
    [Parameter(Mandatory = $true)][ValidateSet('Start', 'Stop')][string]$Action,
    [Parameter(Mandatory = $true)][string]$OutDir,
    [string]$Project = '',
    [string]$SidecarImage = 'ho-verify-m17-postgres:16.15-rootless',
    [int]$SettleMilliseconds = 1000,
    [int]$SettleTimeoutSeconds = 15
)
$ErrorActionPreference = 'Stop'
$OutDir = [System.IO.Path]::GetFullPath($OutDir)
$stateFile = Join-Path $OutDir 'diag-state.json'
$ProjectPattern = '^ho-verify-m17-[a-z0-9-]+$'
$SidecarOwnerLabel = 'p09-controlled-diagnostics'
$HashListName = 'diag-hashes.txt'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Get-ProcessIdentity([int]$ProcessId) {
    $found = Get-CimInstance -ClassName Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction SilentlyContinue
    if (-not $found) { return $null }
    [pscustomobject]@{
        Name        = [string]$found.Name
        CommandLine = [string]$found.CommandLine
        CreationUtc = ([DateTime]$found.CreationDate).ToUniversalTime()
    }
}

# What this script itself requires of each child. These values are never read from the state file.
function Get-ChildExpectation([string]$Kind, [string]$ProjectName, [string]$SidecarName) {
    switch ($Kind) {
        'typeperf' { return [pscustomobject]@{ Name = 'typeperf.exe'; Marker = (Join-Path $OutDir 'host-counters.csv') } }
        'events'   { return [pscustomobject]@{ Name = 'docker.exe'; Marker = "label=com.docker.compose.project=$ProjectName" } }
        'logs'     { return [pscustomobject]@{ Name = 'docker.exe'; Marker = "logs -f $SidecarName" } }
    }
    throw "Unknown child role $Kind"
}

function Invoke-Docker([string[]]$DockerArguments) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & docker @DockerArguments 2>$null
        return [pscustomobject]@{ Code = $LASTEXITCODE; Output = @($output) }
    }
    finally { $ErrorActionPreference = $previous }
}

function Read-Utc([string]$Text) {
    $parsed = [DateTime]::MinValue
    if (-not [DateTime]::TryParse($Text, [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::AdjustToUniversal -bor [System.Globalization.DateTimeStyles]::AssumeUniversal, [ref]$parsed)) {
        return $null
    }
    return $parsed
}

if ($Action -eq 'Start') {
    if (Test-Path $stateFile) { throw 'Diagnostics already started for this directory.' }
    if ($Project -cnotmatch $ProjectPattern) { throw 'An owned ho-verify-m17-* project name is required.' }
    $sidecar = "$Project-p09-diag"
    $started = [System.Collections.Generic.List[object]]::new()
    $sidecarId = ''
    try {
        # Host counters, one row per second, timestamped by the OS.
        $counters = @('\Processor(_Total)\% Processor Time', '\Processor(_Total)\% Privileged Time', '\Memory\Available MBytes',
            '\PhysicalDisk(_Total)\% Disk Time', '\Process(vmmemWSL)\% Processor Time')
        $typeperf = Start-Process -FilePath typeperf -ArgumentList (($counters | ForEach-Object { '"' + $_ + '"' }) + @('-si', '1', '-f', 'CSV', '-o', ('"' + (Join-Path $OutDir 'host-counters.csv') + '"'), '-y')) -PassThru -WindowStyle Hidden `
            -RedirectStandardOutput (Join-Path $OutDir 'typeperf.out') -RedirectStandardError (Join-Path $OutDir 'typeperf.err')
        $started.Add([pscustomobject]@{ Kind = 'typeperf'; Id = $typeperf.Id })
        # Container lifecycle events of the owned project give the start time of every measured row (each row recreates the API).
        $eventArgs = 'events --filter "label=com.docker.compose.project=' + $Project + '" --format "{{json .}}"'
        $events = Start-Process -FilePath docker -ArgumentList $eventArgs -PassThru -WindowStyle Hidden `
            -RedirectStandardOutput (Join-Path $OutDir 'docker-events.jsonl') -RedirectStandardError (Join-Path $OutDir 'docker-events.err')
        $started.Add([pscustomobject]@{ Kind = 'events'; Id = $events.Id })
        # Docker VM scheduler pressure and CPU time (steal included), read from a throwaway sidecar that shares only the VM process namespace.
        $previousSidecar = Invoke-Docker @('container', 'ls', '-a', '-q', '--filter', "name=^/$sidecar$")
        if ($previousSidecar.Output.Count -gt 0) { throw "A container named $sidecar already exists; refusing to reuse or remove it." }
        $loop = 'while true; do echo T $(date +%s%N); head -1 /proc/stat; cat /proc/pressure/cpu; cat /proc/pressure/memory; cat /proc/pressure/io; sleep 1; done'
        $run = Invoke-Docker @('run', '-d', '--name', $sidecar, '--label', "owner=$SidecarOwnerLabel", '--network', 'none', '--pid=host', '--entrypoint', 'sh', $SidecarImage, '-c', $loop)
        if ($run.Code -ne 0 -or $run.Output.Count -lt 1 -or ([string]$run.Output[-1]) -cnotmatch '^[0-9a-f]{64}$') { throw 'The diagnostics sidecar container could not be started.' }
        $sidecarId = [string]$run.Output[-1]
        $logs = Start-Process -FilePath docker -ArgumentList @('logs', '-f', $sidecar) -PassThru -WindowStyle Hidden `
            -RedirectStandardOutput (Join-Path $OutDir 'vm-pressure.log') -RedirectStandardError (Join-Path $OutDir 'vm-pressure.err')
        $started.Add([pscustomobject]@{ Kind = 'logs'; Id = $logs.Id })

        $children = [ordered]@{}
        foreach ($child in $started) {
            $identity = Get-ProcessIdentity $child.Id
            if (-not $identity) { throw "The $($child.Kind) child exited immediately; diagnostics cannot be recorded." }
            $children[$child.Kind] = [pscustomobject]@{ pid = $child.Id; name = $identity.Name; creation_utc = $identity.CreationUtc.ToString('o') }
        }
        [pscustomobject]@{ schema_version = 2; started_utc = [DateTime]::UtcNow.ToString('o'); project = $Project; sidecar = $sidecar
            sidecar_id = $sidecarId
            pids = [pscustomobject]@{ typeperf = $typeperf.Id; events = $events.Id; logs = $logs.Id }
            children = [pscustomobject]$children
            timezone_offset_minutes = [int][TimeZoneInfo]::Local.GetUtcOffset([DateTime]::Now).TotalMinutes } |
            ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $stateFile -Encoding utf8
    }
    catch {
        # A failed Start undoes only what this invocation itself created.
        foreach ($child in $started) { Stop-Process -Id $child.Id -Force -ErrorAction SilentlyContinue }
        if ($sidecarId) { Invoke-Docker @('rm', '-f', $sidecarId) | Out-Null }
        throw
    }
    'diagnostics started'
    return
}

# ---------------------------------------------------------------------------------------------- Stop
if (-not (Test-Path -LiteralPath $stateFile)) { throw 'No diagnostics state file; nothing is owned here.' }
$state = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json
function Assert-State($Value) {
    foreach ($name in @('started_utc', 'project', 'sidecar', 'pids')) {
        if ($null -eq $Value.PSObject.Properties[$name]) { throw "diag-state.json lacks $name." }
    }
    if ([string]$Value.project -cnotmatch $ProjectPattern) { throw 'diag-state.json names a project that is not an owned ho-verify-m17-* project.' }
    if ([string]$Value.sidecar -cne ("$($Value.project)-p09-diag")) { throw 'diag-state.json names a sidecar that does not belong to its project.' }
    if ($null -eq (Read-Utc ([string]$Value.started_utc))) { throw 'diag-state.json has no valid started_utc.' }
    foreach ($kind in @('typeperf', 'events', 'logs')) {
        $raw = $Value.pids.$kind
        $number = 0
        if ($null -eq $raw -or -not [int]::TryParse([string]$raw, [ref]$number) -or $number -le 4) { throw "diag-state.json has an invalid pid for $kind." }
    }
    if ($null -ne $Value.PSObject.Properties['sidecar_id'] -and [string]$Value.sidecar_id -cnotmatch '^[0-9a-f]{64}$') { throw 'diag-state.json has an invalid sidecar id.' }
}
Assert-State $state
$alreadyStopped = $null -ne $state.PSObject.Properties['stopped_utc']
$project = [string]$state.project
$sidecar = [string]$state.sidecar
$outcomes = [ordered]@{}

if (-not $alreadyStopped) {
    # 1. Children: identity first, then signal, then wait for the exit and prove it.
    foreach ($kind in @('typeperf', 'events', 'logs')) {
        $id = [int]$state.pids.$kind
        $live = Get-ProcessIdentity $id
        if (-not $live) { $outcomes[$kind] = 'already-exited'; continue }
        $expected = Get-ChildExpectation $kind $project $sidecar
        $recorded = if ($null -ne $state.PSObject.Properties['children'] -and $null -ne $state.children.PSObject.Properties[$kind]) { $state.children.$kind } else { $null }
        if ($null -eq $recorded) { throw "Process $id is running but diag-state.json has no recorded identity for $kind; refusing to signal it." }
        $recordedStart = Read-Utc ([string]$recorded.creation_utc)
        if ($live.Name -ne $expected.Name -or [string]$recorded.name -ne $expected.Name) { throw "Process $id is not this run's $kind child (executable $($live.Name)); refusing to signal it." }
        if ($null -eq $recordedStart -or [Math]::Abs(($live.CreationUtc - $recordedStart).TotalSeconds) -gt 2) { throw "Process $id was not started when $kind was; the id has been reused. Refusing to signal it." }
        if ($live.CommandLine.IndexOf($expected.Marker, [StringComparison]::OrdinalIgnoreCase) -lt 0) { throw "Process $id does not carry this run's $kind command line; refusing to signal it." }
        Stop-Process -Id $id -Force
        try { Wait-Process -Id $id -Timeout 10 -ErrorAction Stop } catch [System.TimeoutException] { throw "Child $kind (process $id) did not exit within 10 seconds." } catch { }
        if (Get-ProcessIdentity $id) { throw "Child $kind (process $id) is still running after the stop request." }
        $outcomes[$kind] = 'terminated'
    }

    # 2. Sidecar: exactly the container this run created, matched by id, name and label.
    $listed = Invoke-Docker @('container', 'ls', '-a', '-q', '--no-trunc', '--filter', "name=^/$sidecar$")
    if ($listed.Code -ne 0) { throw 'Docker could not list the sidecar container; its removal cannot be confirmed.' }
    if ($listed.Output.Count -eq 0) { $outcomes['sidecar'] = 'absent' }
    else {
        if ($listed.Output.Count -ne 1) { throw 'More than one container matches the sidecar name; refusing to remove any.' }
        $containerId = [string]$listed.Output[0]
        $inspect = Invoke-Docker @('container', 'inspect', '--format', '{{json .}}', $containerId)
        if ($inspect.Code -ne 0 -or $inspect.Output.Count -lt 1) { throw 'The sidecar container could not be inspected; refusing to remove it.' }
        $detail = ([string]($inspect.Output -join '')) | ConvertFrom-Json
        $labels = $detail.Config.Labels
        if ([string]$detail.Name -cne "/$sidecar") { throw 'The container does not carry the sidecar name; refusing to remove it.' }
        if ($null -eq $labels -or $null -eq $labels.PSObject.Properties['owner'] -or [string]$labels.owner -cne $SidecarOwnerLabel) { throw 'The container is not labelled as this diagnostics sidecar; refusing to remove it.' }
        if ($null -ne $labels.PSObject.Properties['com.docker.compose.project']) { throw 'The container belongs to a compose project; refusing to remove it.' }
        if ([string]$detail.HostConfig.NetworkMode -cne 'none') { throw 'The container is not the network-less sidecar; refusing to remove it.' }
        if ($null -ne $state.PSObject.Properties['sidecar_id'] -and [string]$state.sidecar_id -cne [string]$detail.Id) { throw 'The container id differs from the one this run created; refusing to remove it.' }
        $removed = Invoke-Docker @('rm', '-f', [string]$detail.Id)
        if ($removed.Code -ne 0) { throw 'The sidecar container could not be removed.' }
        $again = Invoke-Docker @('container', 'ls', '-a', '-q', '--no-trunc', '--filter', "id=$($detail.Id)")
        if ($again.Code -ne 0 -or $again.Output.Count -ne 0) { throw 'The sidecar container is still present after removal.' }
        $outcomes['sidecar'] = 'removed'
    }
}

# 3. The output directory must hold only plain files that have stopped changing.
function Get-OutputSnapshot() {
    $entries = @(Get-ChildItem -LiteralPath $OutDir -Force)
    foreach ($entry in $entries) {
        if ($entry.PSIsContainer) { throw "The output directory contains a subdirectory ($($entry.Name)); its contents cannot be covered by the hash list." }
        if (($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { throw "The output directory contains a link ($($entry.Name))." }
    }
    @($entries | Where-Object { $_.Name -ne $HashListName } | Sort-Object Name |
        ForEach-Object { '{0}|{1}|{2}' -f $_.Name, $_.Length, $_.LastWriteTimeUtc.Ticks })
}
$deadline = [DateTime]::UtcNow.AddSeconds($SettleTimeoutSeconds)
$stable = $false
$previousSnapshot = Get-OutputSnapshot
while ([DateTime]::UtcNow -lt $deadline) {
    Start-Sleep -Milliseconds $SettleMilliseconds
    $currentSnapshot = Get-OutputSnapshot
    if (($currentSnapshot -join "`n") -ceq ($previousSnapshot -join "`n")) { $stable = $true; break }
    $previousSnapshot = $currentSnapshot
}
if (-not $stable) { throw "Output files were still changing after $SettleTimeoutSeconds seconds (late write); they cannot be hashed." }

# Order matters: the final state (including stopped_utc) is written FIRST, so the hash list describes the bytes that remain on disk.
# A previous revision hashed diag-state.json before adding stopped_utc, leaving one internal mismatch per run (finding 2026-11-16).
if (-not $alreadyStopped) {
    $state | Add-Member -NotePropertyName stopped_utc -NotePropertyValue ([DateTime]::UtcNow.ToString('o')) -Force
    $state | Add-Member -NotePropertyName stop_outcomes -NotePropertyValue ([pscustomobject]$outcomes) -Force
    $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $stateFile -Encoding utf8
}
# The hash list is written last and never lists itself (a self-referential hash cannot be correct).
$files = @(Get-ChildItem -LiteralPath $OutDir -File | Where-Object { $_.Name -ne $HashListName })
$measured = @($files | ForEach-Object { [pscustomobject]@{ Name = $_.Name; Length = $_.Length; Written = $_.LastWriteTimeUtc.Ticks
        Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLower() } })
$measured | ForEach-Object { '{0}  {1}  {2}' -f $_.Hash, $_.Length, $_.Name } |
    Set-Content -LiteralPath (Join-Path $OutDir $HashListName) -Encoding ascii
# A writer that outlived the checks above would leave the list describing older bytes. Look again after a short pause.
Start-Sleep -Milliseconds 500
foreach ($item in $measured) {
    $now = Get-Item -LiteralPath (Join-Path $OutDir $item.Name)
    if ($now.Length -ne $item.Length -or $now.LastWriteTimeUtc.Ticks -ne $item.Written) { throw "Late write detected: $($item.Name) changed after it was hashed." }
}
foreach ($entry in (Get-ChildItem -LiteralPath $OutDir -Force)) {
    if ($entry.Name -ne $HashListName -and -not ($measured | Where-Object { $_.Name -eq $entry.Name })) { throw "Late write detected: $($entry.Name) appeared after hashing." }
}
'diagnostics stopped'
