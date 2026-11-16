# Ownership-checked inventory and removal of the Docker resources of ONE disposable G17 verification project.
# Dot-sourced by scripts/run-g17-local.ps1 and scripts/cleanup-g17-owned.ps1. Nothing here uses a prune command,
# a name wildcard or a broad filter: every removal is addressed by id, and only after every resource has been
# checked against the exact project, compose service and name it must carry. Any doubt aborts before anything is
# removed. Volumes (retained database state) and images are never removed.

$script:G17ProjectPattern = '^ho-verify-m17-[a-z0-9-]+$'
$script:G17ComposeSubnet = '172.30.247.0/24'

function Invoke-G17Docker {
    param([Parameter(Mandatory = $true)][string[]]$DockerArguments)
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & docker @DockerArguments 2>$null
        $code = $LASTEXITCODE
        return [pscustomobject]@{ Code = $code; Output = @($output) }
    }
    finally { $ErrorActionPreference = $previous }
}

function Get-G17DockerJson {
    param([Parameter(Mandatory = $true)][string[]]$InspectArguments)
    $run = Invoke-G17Docker $InspectArguments
    if ($run.Code -ne 0 -or $run.Output.Count -eq 0) { throw "docker $($InspectArguments -join ' ') failed" }
    return (($run.Output -join "`n") | ConvertFrom-Json)
}

function Assert-G17ProjectName {
    param([string]$Project)
    if ($Project -cnotmatch $script:G17ProjectPattern) { throw "Not an owned ho-verify-m17-* project name: $Project" }
}

# Everything docker labels with this exact compose project, checked for exact ownership. Returns the inventory and a
# list of problems; a non-empty Problems list means the project must not be touched.
function Get-G17OwnedResources {
    param([Parameter(Mandatory = $true)][string]$Project)
    Assert-G17ProjectName $Project
    $problems = [System.Collections.Generic.List[string]]::new()
    $containers = [System.Collections.Generic.List[object]]::new()
    $networks = [System.Collections.Generic.List[object]]::new()
    $volumes = [System.Collections.Generic.List[object]]::new()
    $escaped = [regex]::Escape($Project)

    $containerList = Invoke-G17Docker @('ps', '-a', '-q', '--no-trunc', '--filter', "label=com.docker.compose.project=$Project")
    if ($containerList.Code -ne 0) { throw 'docker could not list containers' }
    foreach ($id in $containerList.Output) {
        $detail = Get-G17DockerJson @('inspect', '--format', '{{json .}}', [string]$id)
        $labels = $detail.Config.Labels
        $name = ([string]$detail.Name).TrimStart('/')
        $service = if ($null -ne $labels -and $null -ne $labels.PSObject.Properties['com.docker.compose.service']) { [string]$labels.'com.docker.compose.service' } else { '' }
        $labelProject = if ($null -ne $labels -and $null -ne $labels.PSObject.Properties['com.docker.compose.project']) { [string]$labels.'com.docker.compose.project' } else { '' }
        if ($labelProject -cne $Project) { $problems.Add("container $name carries project label '$labelProject'"); continue }
        if ($service -eq '' -or $name -cnotmatch ('^' + $escaped + '-' + [regex]::Escape($service) + '-(?:\d+|run-[0-9a-f]+)$'))
        { $problems.Add("container $name is not <project>-<service>-<index> of this project"); continue }
        $containers.Add([pscustomobject]@{ Id = [string]$detail.Id; Name = $name; Service = $service; Status = [string]$detail.State.Status
                Running = [bool]$detail.State.Running; ExitCode = $detail.State.ExitCode; Image = [string]$detail.Config.Image })
    }

    $networkList = Invoke-G17Docker @('network', 'ls', '-q', '--no-trunc', '--filter', "label=com.docker.compose.project=$Project")
    if ($networkList.Code -ne 0) { throw 'docker could not list networks' }
    foreach ($id in $networkList.Output) {
        $detail = (Get-G17DockerJson @('network', 'inspect', [string]$id))[0]
        $labels = $detail.Labels
        $logical = if ($null -ne $labels -and $null -ne $labels.PSObject.Properties['com.docker.compose.network']) { [string]$labels.'com.docker.compose.network' } else { '' }
        $labelProject = if ($null -ne $labels -and $null -ne $labels.PSObject.Properties['com.docker.compose.project']) { [string]$labels.'com.docker.compose.project' } else { '' }
        if ($labelProject -cne $Project) { $problems.Add("network $($detail.Name) carries project label '$labelProject'"); continue }
        if ($logical -eq '' -or [string]$detail.Name -cne "${Project}_$logical") { $problems.Add("network $($detail.Name) is not ${Project}_<network> of this project"); continue }
        $attached = @()
        if ($null -ne $detail.Containers) { $attached = @($detail.Containers.PSObject.Properties | ForEach-Object { $_.Name }) }
        $networks.Add([pscustomobject]@{ Id = [string]$detail.Id; Name = [string]$detail.Name
                Subnets = @($detail.IPAM.Config | ForEach-Object { [string]$_.Subnet }); Attached = $attached })
    }

    $volumeList = Invoke-G17Docker @('volume', 'ls', '-q', '--filter', "label=com.docker.compose.project=$Project")
    if ($volumeList.Code -ne 0) { throw 'docker could not list volumes' }
    foreach ($name in $volumeList.Output) { $volumes.Add([pscustomobject]@{ Name = [string]$name }) }

    # A network may only have endpoints that belong to this project's own containers.
    $ownIds = @($containers | ForEach-Object { $_.Id })
    foreach ($network in $networks) {
        foreach ($endpoint in $network.Attached) {
            if ($ownIds -notcontains $endpoint) { $problems.Add("network $($network.Name) has a foreign endpoint $endpoint") }
        }
    }
    return [pscustomobject]@{ Project = $Project; Containers = @($containers); Networks = @($networks); Volumes = @($volumes); Problems = @($problems) }
}

# The fixed compose subnet can be held by only one network. Reports its holder, whoever that is; never removes it.
function Test-G17SubnetFree {
    param([string]$Subnet = $script:G17ComposeSubnet, [string]$Project = '')
    $ids = Invoke-G17Docker @('network', 'ls', '-q', '--no-trunc')
    if ($ids.Code -ne 0) { throw 'docker could not list networks' }
    $holders = [System.Collections.Generic.List[object]]::new()
    foreach ($id in $ids.Output) {
        $detail = (Get-G17DockerJson @('network', 'inspect', [string]$id))[0]
        $subnets = @($detail.IPAM.Config | ForEach-Object { [string]$_.Subnet })
        if ($subnets -contains $Subnet) {
            $owner = if ($null -ne $detail.Labels -and $null -ne $detail.Labels.PSObject.Properties['com.docker.compose.project']) { [string]$detail.Labels.'com.docker.compose.project' } else { '' }
            $holders.Add([pscustomobject]@{ Name = [string]$detail.Name; Id = [string]$detail.Id; Project = $owner })
        }
    }
    return [pscustomobject]@{ Subnet = $Subnet; Free = ($holders.Count -eq 0); Holders = @($holders) }
}

function Save-G17OwnedState {
    param([Parameter(Mandatory = $true)]$Owned, [Parameter(Mandatory = $true)][string]$Directory)
    New-Item -ItemType Directory -Force -Path $Directory | Out-Null
    foreach ($container in $Owned.Containers) {
        $run = Invoke-G17Docker @('inspect', '--format', '{{json .State}}', $container.Id)
        ($run.Output -join "`n") | Out-File -LiteralPath (Join-Path $Directory ("$($container.Service)-$($container.Id.Substring(0, 12)).state.json")) -Encoding utf8
    }
    $summary = [ordered]@{ project = $Owned.Project; captured_utc = [DateTime]::UtcNow.ToString('o')
        containers = @($Owned.Containers | ForEach-Object { [ordered]@{ id = $_.Id; name = $_.Name; service = $_.Service; status = $_.Status; exit_code = $_.ExitCode; image = $_.Image } })
        networks = @($Owned.Networks | ForEach-Object { [ordered]@{ id = $_.Id; name = $_.Name; subnets = $_.Subnets; attached_endpoints = $_.Attached.Count } })
        volumes = @($Owned.Volumes | ForEach-Object { $_.Name }); problems = $Owned.Problems }
    $summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Directory 'owned-inventory.json') -Encoding utf8
}

# Preserve first, verify everything, remove last. Returns the result record; throws (after recording) on any failure.
function Remove-G17OwnedResources {
    param(
        [Parameter(Mandatory = $true)][string]$Project,
        [Parameter(Mandatory = $true)][string]$EvidenceDirectory,
        [switch]$StopRunning
    )
    Assert-G17ProjectName $Project
    $record = [ordered]@{ project = $Project; started_utc = [DateTime]::UtcNow.ToString('o'); outcome = 'FAILED'; error = $null
        containers_before = 0; networks_before = 0; volumes_retained = @(); containers_removed = 0; networks_removed = 0
        containers_after = $null; networks_after = $null; subnets_released = @(); subnet_still_held = $null }
    $recordPath = Join-Path $EvidenceDirectory 'm17-owned-cleanup.json'
    try {
        $owned = Get-G17OwnedResources $Project
        Save-G17OwnedState $owned (Join-Path $EvidenceDirectory 'm17-cleanup-state')
        $record.containers_before = $owned.Containers.Count
        $record.networks_before = $owned.Networks.Count
        $record.volumes_retained = @($owned.Volumes | ForEach-Object { $_.Name })
        if ($owned.Problems.Count -gt 0) { throw ('Ambiguous ownership, nothing was removed: ' + ($owned.Problems -join '; ')) }
        $running = @($owned.Containers | Where-Object { $_.Running })
        if ($running.Count -gt 0 -and -not $StopRunning) { throw ('Owned containers are still running and -StopRunning was not given: ' + (($running | ForEach-Object { $_.Name }) -join ', ')) }
        foreach ($container in $running) {
            $stop = Invoke-G17Docker @('stop', '-t', '10', $container.Id)
            if ($stop.Code -ne 0) { throw "Could not stop $($container.Name)" }
        }
        foreach ($container in $owned.Containers) {
            # No -f (a running container is never force removed) and no -v (volumes are retained).
            $remove = Invoke-G17Docker @('rm', $container.Id)
            if ($remove.Code -ne 0) { throw "Could not remove container $($container.Name)" }
            $record.containers_removed++
        }
        foreach ($network in $owned.Networks) {
            $remove = Invoke-G17Docker @('network', 'rm', $network.Id)
            if ($remove.Code -ne 0) { throw "Could not remove network $($network.Name)" }
            $record.networks_removed++
        }
        $after = Get-G17OwnedResources $Project
        $record.containers_after = $after.Containers.Count
        $record.networks_after = $after.Networks.Count
        if ($after.Containers.Count -ne 0 -or $after.Networks.Count -ne 0) { throw 'Owned resources remain after removal' }
        # The point of removing the network is releasing its address range: prove every range it held is free again.
        $held = [System.Collections.Generic.List[string]]::new()
        foreach ($subnet in @($owned.Networks | ForEach-Object { $_.Subnets } | Where-Object { $_ } | Select-Object -Unique)) {
            $check = Test-G17SubnetFree $subnet
            if ($check.Free) { $record.subnets_released += $subnet }
            else { $held.Add("$subnet by " + (($check.Holders | ForEach-Object { $_.Name }) -join ', ')) }
        }
        $record.subnet_still_held = @($held)
        if ($held.Count -gt 0) { throw ('An address range is still held after removal: ' + ($held -join '; ')) }
        $record.outcome = 'REMOVED'
    }
    catch {
        $record.error = [string]$_.Exception.Message
        throw
    }
    finally {
        $record.finished_utc = [DateTime]::UtcNow.ToString('o')
        New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
        ([pscustomobject]$record) | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $recordPath -Encoding utf8
    }
    return [pscustomobject]$record
}
