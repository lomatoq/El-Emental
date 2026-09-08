param(
    [Parameter(Mandatory=$true)][ValidateSet('Host','Join','Stop')][string]$Role,
    [ValidateSet('basic','stone-combat')][string]$Scenario = 'basic',
    [string]$RecordPath
)
$ErrorActionPreference = 'Stop'
$probeRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$probeExe = (Resolve-Path -LiteralPath (Join-Path $probeRepo 'Builds/OnlineDevelopment/ElEmental.exe')).Path
if ($Role -eq 'Host') {
    $probeStamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss')
    $probeDirectory = Join-Path $probeRepo ('BuildReports/OnlineProbe/Run-' + $probeStamp)
    New-Item -ItemType Directory -Path $probeDirectory | Out-Null
    $RecordPath = Join-Path $probeDirectory 'processes.json'
    $probeRecord = [pscustomobject]@{ HostPid=0; ClientPid=0; Exe=$probeExe; Directory=$probeDirectory; Stamp=$probeStamp; Scenario=$Scenario }
} else {
    if ([string]::IsNullOrWhiteSpace($RecordPath)) { throw 'An explicit previous processes.json is required.' }
    $probeRecord = Get-Content -Raw -LiteralPath $RecordPath | ConvertFrom-Json
    if ($probeRecord.Exe -ne $probeExe) { throw 'Recorded executable does not match this project build.' }
}
if ($Role -eq 'Stop') {
    foreach ($probePid in @($probeRecord.HostPid,$probeRecord.ClientPid)) {
        if ($probePid -le 0) { continue }
        $ownedProbe = Get-Process -Id $probePid -ErrorAction SilentlyContinue
        if ($null -ne $ownedProbe) {
            if ($ownedProbe.Path -ne $probeRecord.Exe) { throw "PID $probePid now belongs to a different executable; not stopped." }
            Stop-Process -Id $probePid
        }
    }
    'Recorded probe processes stopped or already exited.'
    return
}
$hostJson = Join-Path $probeRecord.Directory 'host.json'
$clientJson = Join-Path $probeRecord.Directory 'client.json'
if ($Role -eq 'Join') {
    $hostReport = Get-Content -Raw -LiteralPath $hostJson | ConvertFrom-Json
    if (-not $hostReport.connected -or [string]::IsNullOrWhiteSpace($hostReport.code) -or $hostReport.outcome -ne 'in-progress') {
        throw 'Host has not reached its connected, code-ready checkpoint.'
    }
    $hostProcess = Get-Process -Id $probeRecord.HostPid -ErrorAction Stop
    if ($hostProcess.Path -ne $probeRecord.Exe) { throw 'Host PID executable mismatch.' }
    $hostLog = Join-Path $probeRecord.Directory 'host.log'
    if (Select-String -LiteralPath $hostLog -Pattern '0x887A0005|DEVICE_REMOVED|failed to create device' -Quiet) { throw 'Host GPU error: client launch refused.' }
    if ($probeRecord.ClientPid -gt 0) { throw 'Client was already dispatched for this record.' }
}
$probeMode = if ($Role -eq 'Host') { 'host' } else { 'join' }
$probeOutput = if ($Role -eq 'Host') { $hostJson } else { $clientJson }
$probeLog = Join-Path $probeRecord.Directory ($probeMode + '.log')
$probeArguments = '-batchmode -force-d3d11 -screen-fullscreen 0 -screen-width 640 -screen-height 360 -logFile "' + $probeLog + '" --online-profile qa-' + $probeRecord.Stamp + '-' + $probeMode + ' --online-smoke ' + $probeMode + ' --online-result "' + $probeOutput + '" --online-scenario ' + $probeRecord.Scenario + ' --online-delay-ms 0 --online-jitter-ms 0 --online-loss-percent 0'
if ($Role -eq 'Host') { $probeArguments += ' --online-peer-file "' + $clientJson + '"' }
else { $probeArguments += ' --online-join-file "' + $hostJson + '"' }
$probeProcess = Start-Process -FilePath $probeRecord.Exe -WorkingDirectory (Split-Path $probeRecord.Exe) -WindowStyle Hidden -ArgumentList $probeArguments -PassThru
if ($Role -eq 'Host') { $probeRecord.HostPid = $probeProcess.Id } else { $probeRecord.ClientPid = $probeProcess.Id }
$probeRecord | ConvertTo-Json | Set-Content -LiteralPath $RecordPath
try { $probeProcess.PriorityClass = 'BelowNormal' } catch { Write-Warning 'Process priority could not be lowered; process remains recorded.' }
[pscustomobject]@{ RecordPath=$RecordPath; Pid=$probeProcess.Id; Role=$Role; Scenario=$probeRecord.Scenario } | ConvertTo-Json
