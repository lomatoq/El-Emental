param(
    [string] $ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string] $PlayerPath,
    [int] $TimeoutSeconds = 240
)

$ErrorActionPreference = 'Stop'
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$reportFolder = Join-Path $ProjectPath "BuildReports\StartupPlayer\$stamp"
New-Item -ItemType Directory -Path $reportFolder | Out-Null

$player = if ([string]::IsNullOrWhiteSpace($PlayerPath)) {
    Join-Path $ProjectPath 'Builds\Windows\ElEmental.exe'
} else {
    $PlayerPath
}
if (-not (Test-Path -LiteralPath $player)) { throw "Player executable missing: $player" }
$playerLog = Join-Path $reportFolder 'Player.log'

function Quote-ProcessArgument([string] $value) {
    return '"' + $value.Replace('"', '\"') + '"'
}

$arguments = @(
    '-force-d3d11',
    '-screen-fullscreen 0',
    '-screen-width 1280',
    '-screen-height 720',
    '-logFile ' + (Quote-ProcessArgument $playerLog),
    '-startupBootstrapEvidenceFolder ' + (Quote-ProcessArgument $reportFolder),
    '-smokeAutoQuit'
) -join ' '

$process = Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    Stop-Process -Id $process.Id -Force
    throw "Player exceeded $TimeoutSeconds seconds. See $playerLog and any failure capture."
}
if ($process.ExitCode -ne 0) { throw "Player exited with code $($process.ExitCode). See $playerLog" }

$json = Join-Path $reportFolder 'BootstrapStartup.json'
if (-not (Test-Path -LiteralPath $json)) { throw "Bootstrap report missing: $json" }
$report = Get-Content -LiteralPath $json -Raw | ConvertFrom-Json
if ($report.status -ne 'Ready') { throw "Bootstrap reported '$($report.status)': $($report.error)" }
foreach ($image in @('BootstrapCover.png', 'PlayableReady.png')) {
    $path = Join-Path $reportFolder $image
    if (-not (Test-Path -LiteralPath $path)) { throw "Required screenshot missing: $path" }
}
if (-not $report.bootstrapCapture.success) {
    throw "Bootstrap offscreen capture failed: $($report.bootstrapCapture.error)"
}
if (-not $report.playableCapture.success) {
    throw "Playable offscreen capture failed: $($report.playableCapture.error)"
}
if ($report.bootstrapCapture.meanLuminance -le 0.004 -or
    $report.bootstrapCapture.nonBlackFraction -le 0.95) {
    throw "Bootstrap capture is effectively black: mean=$($report.bootstrapCapture.meanLuminance), nonBlack=$($report.bootstrapCapture.nonBlackFraction)"
}
if ($report.playableCapture.meanLuminance -le 0.004 -or
    $report.playableCapture.nonBlackFraction -le 0.05 -or
    ($report.playableCapture.maximumLuminance - $report.playableCapture.minimumLuminance) -le 0.01) {
    throw "Playable capture is not visually reviewable: mean=$($report.playableCapture.meanLuminance), nonBlack=$($report.playableCapture.nonBlackFraction), range=$($report.playableCapture.minimumLuminance)..$($report.playableCapture.maximumLuminance)"
}

Write-Host "Fresh Player startup evidence: $reportFolder"
Write-Host ("cover={0:N2}ms activation={1:N2}ms ready={2:N2}ms maxFrame={3:N2}ms" -f `
    $report.coverPresentedUptimeMilliseconds,
    $report.targetActivatedUptimeMilliseconds,
    $report.readyUptimeMilliseconds,
    $report.maximumObservedFrameMilliseconds)
Write-Host ("offscreen coverMean={0:N4} readyMean={1:N4} readyRange={2:N4}" -f `
    $report.bootstrapCapture.meanLuminance,
    $report.playableCapture.meanLuminance,
    ($report.playableCapture.maximumLuminance - $report.playableCapture.minimumLuminance))
