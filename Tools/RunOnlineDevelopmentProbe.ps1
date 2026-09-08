param(
    [ValidateSet('basic','stone-combat')][string]$Scenario = 'basic',
    [ValidateRange(0,500)][int]$DelayMs = 0,
    [ValidateRange(0,200)][int]$JitterMs = 0,
    [ValidateRange(0,20)][int]$LossPercent = 0
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot
$probeExe = Join-Path $projectRoot 'Builds/OnlineDevelopment/ElEmental.exe'
if (!(Test-Path -LiteralPath $probeExe)) { throw 'Build Online Development From Saved Arena first.' }
$runStamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss')
$probeDir = Join-Path $projectRoot ('BuildReports/OnlineProbe/Run-' + $runStamp)
New-Item -ItemType Directory -Path $probeDir | Out-Null
$hostJson = Join-Path $probeDir 'host.json'
$clientJson = Join-Path $probeDir 'client.json'
$started = @()
foreach ($probeRole in @('host','join')) {
    $fileRole = if ($probeRole -eq 'host') { 'host' } else { 'client' }
    $resultPath = Join-Path $probeDir ($fileRole + '.json')
    $logPath = Join-Path $probeDir ($fileRole + '.log')
    $profile = ('qa-' + $runStamp + '-' + $probeRole).ToLowerInvariant()
    $probeArgs = "-batchmode -force-d3d11 -screen-fullscreen 0 -screen-width 960 -screen-height 600 -logFile `"$logPath`" --online-profile $profile --online-smoke $probeRole --online-result `"$resultPath`" --online-scenario $Scenario --online-delay-ms $DelayMs --online-jitter-ms $JitterMs --online-loss-percent $LossPercent"
    if ($probeRole -eq 'host') { $probeArgs += " --online-peer-file `"$clientJson`"" }
    else { $probeArgs += " --online-join-file `"$hostJson`"" }
    $probeProcess = Start-Process -FilePath $probeExe -WorkingDirectory (Split-Path $probeExe) -WindowStyle Hidden -ArgumentList $probeArgs -PassThru
    $probeProcess.PriorityClass = 'BelowNormal'
    $started += $probeProcess.Id
}
[pscustomobject]@{HostPid=$started[0];ClientPid=$started[1];Exe=$probeExe;Directory=$probeDir;Scenario=$Scenario;DelayMs=$DelayMs;JitterMs=$JitterMs;LossPercent=$LossPercent} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $probeDir 'processes.json')
Get-Content -LiteralPath (Join-Path $probeDir 'processes.json')
