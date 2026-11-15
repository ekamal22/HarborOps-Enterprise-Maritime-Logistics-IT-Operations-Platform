# Objective go/no-go gate for one controlled P09 run. It only observes; it changes nothing on the machine.
# usage: powershell -File preflight.ps1 -OutFile <preflight.json> -Worktree <path at the tested source> -ExpectedSha <40 hex>
# Exit code 0 means every check passed. A failed check delays the run; it is not an attempt and it is recorded.
param(
    [Parameter(Mandatory = $true)][string]$OutFile,
    [Parameter(Mandatory = $true)][string]$Worktree,
    [Parameter(Mandatory = $true)][string]$ExpectedSha,
    [double]$MaxMeanCpuPercent = 10,
    [double]$MaxPeakCpuPercent = 40,
    [double]$MinFreeMemoryGiB = 3,
    [double]$MinEvidenceDriveFreeGiB = 20,
    [int]$ExpectedDockerCpus = 12,
    [long]$ExpectedDockerMemoryBytes = 8154984448,
    [string]$EvidenceDrive = 'E'
)
$ErrorActionPreference = 'Stop'
$checks = New-Object System.Collections.Generic.List[object]
function Add-Check([string]$name, [bool]$ok, $observed, [string]$rule) {
    $checks.Add([pscustomobject]@{ name = $name; ok = $ok; observed = $observed; rule = $rule }) | Out-Null
}

# 1. Mains power.
$battery = Get-CimInstance Win32_Battery -ErrorAction SilentlyContinue
$onAc = (-not $battery) -or (@($battery | Where-Object { $_.BatteryStatus -in 2, 6, 7, 8, 9 }).Count -gt 0)
Add-Check 'mains-power' $onAc ($(if ($battery) { "BatteryStatus=" + $battery.BatteryStatus } else { 'no battery' })) 'AC power or no battery'

# 2. Host CPU quiet for 60 seconds.
$cpu = (Get-Counter '\Processor(_Total)\% Processor Time' -SampleInterval 1 -MaxSamples 60).CounterSamples | ForEach-Object { $_.CookedValue }
$meanCpu = ($cpu | Measure-Object -Average).Average
$peakCpu = ($cpu | Measure-Object -Maximum).Maximum
Add-Check 'host-cpu-idle' (($meanCpu -le $MaxMeanCpuPercent) -and ($peakCpu -le $MaxPeakCpuPercent)) ([pscustomobject]@{ mean = [math]::Round($meanCpu, 1); peak = [math]::Round($peakCpu, 1) }) ("mean <= $MaxMeanCpuPercent and peak <= $MaxPeakCpuPercent percent over 60 s")

# 3. Free memory.
$os = Get-CimInstance Win32_OperatingSystem
$freeGiB = [math]::Round($os.FreePhysicalMemory / 1MB, 2)
Add-Check 'host-free-memory' ($freeGiB -ge $MinFreeMemoryGiB) $freeGiB ("free physical memory >= $MinFreeMemoryGiB GiB")

# 4. No competing verification workloads of any kind.
$competing = @(Get-CimInstance Win32_Process | Where-Object {
        $_.CommandLine -match 'mutate-|check-g17|run-g17|playwright|dotnet(\.exe)?"? (test|build|run)|msbuild|vstest|node(\.exe)?"? --test' })
Add-Check 'no-competing-workloads' ($competing.Count -eq 0) @($competing | ForEach-Object { $_.ProcessId.ToString() + ' ' + $_.Name }) 'no mutation, test, build or verification process running'

# 5. Antivirus scan and update activity.
$scan = $null
try { $mp = Get-MpComputerStatus; $scan = [bool]($mp.FullScanRunning -or $mp.QuickScanRunning) } catch { $scan = $null }
Add-Check 'no-active-antivirus-scan' ($scan -ne $true) $(if ($null -eq $scan) { 'unknown' } else { $scan }) 'no full or quick scan in progress (unknown is recorded, not failed)'
$installer = @(Get-Process -Name TiWorker, TrustedInstaller, MoUsoCoreWorker -ErrorAction SilentlyContinue)
Add-Check 'no-windows-update-worker' ($installer.Count -eq 0) @($installer | ForEach-Object { $_.Name }) 'no servicing worker running'

# 6. Docker configuration identical to f6 and f7.
$info = docker info --format '{{json .}}' | ConvertFrom-Json
Add-Check 'docker-configuration' (($info.NCPU -eq $ExpectedDockerCpus) -and ([math]::Abs($info.MemTotal - $ExpectedDockerMemoryBytes) -le ($ExpectedDockerMemoryBytes * 0.01))) ([pscustomobject]@{ cpus = $info.NCPU; memory_bytes = $info.MemTotal; server = $info.ServerVersion }) "$ExpectedDockerCpus CPUs and about $ExpectedDockerMemoryBytes bytes"

# 7. No leftover disposable project (ports and subnets are fixed).
$leftover = @(docker ps -a --format '{{.Names}}' | Where-Object { $_ -like 'ho-verify-m17-*' })
Add-Check 'no-leftover-m17-containers' ($leftover.Count -eq 0) $leftover 'no ho-verify-m17-* container, running or stopped'

# 8. Evidence drive space.
$drive = Get-PSDrive -Name $EvidenceDrive
$driveGiB = [math]::Round($drive.Free / 1GB, 1)
Add-Check 'evidence-drive-space' ($driveGiB -ge $MinEvidenceDriveFreeGiB) $driveGiB ("free >= $MinEvidenceDriveFreeGiB GiB")

# 9. Source identity and cleanliness.
$head = (git -C $Worktree rev-parse HEAD).Trim()
$dirty = @(git -C $Worktree status --porcelain)
Add-Check 'source-identity' (($head -eq $ExpectedSha) -and ($dirty.Count -eq 0)) ([pscustomobject]@{ head = $head; uncommitted = $dirty.Count }) 'worktree HEAD equals the tested SHA and is clean'

# 10. Context recorded, not judged.
$plan = [pscustomobject]@{
    power_scheme = (powercfg /getactivescheme | Out-String).Trim()
    os = $os.Caption + ' ' + $os.Version
    logical_processors = (Get-CimInstance Win32_ComputerSystem).NumberOfLogicalProcessors
    total_memory_gib = [math]::Round($os.TotalVisibleMemorySize / 1MB, 1)
}
$result = [pscustomobject]@{
    recorded_utc = [DateTime]::UtcNow.ToString('o')
    all_passed = (@($checks | Where-Object { -not $_.ok }).Count -eq 0)
    checks = $checks
    context = $plan
}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutFile -Encoding utf8
$checks | ForEach-Object { '{0,-6} {1}' -f ($(if ($_.ok) { 'PASS' } else { 'FAIL' })), $_.name }
if (-not $result.all_passed) { exit 1 }
