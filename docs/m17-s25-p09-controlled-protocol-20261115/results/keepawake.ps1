# Holds the system awake until the stop file appears. Observation of power state only; it changes no setting.
param([Parameter(Mandatory = $true)][string]$StopFile)
Add-Type -Namespace Native -Name Power -MemberDefinition '[DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);'
[Native.Power]::SetThreadExecutionState(0x80000001) | Out-Null   # ES_CONTINUOUS | ES_SYSTEM_REQUIRED
while (-not (Test-Path -LiteralPath $StopFile)) { Start-Sleep -Seconds 5 }
[Native.Power]::SetThreadExecutionState(0x80000000) | Out-Null   # ES_CONTINUOUS only: release
