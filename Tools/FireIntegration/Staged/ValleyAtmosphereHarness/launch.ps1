param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path)
$executable=Join-Path $ProjectRoot 'Builds/ValleyAtmosphereBench/ValleyAtmosphereBench.exe'
if(!(Test-Path -LiteralPath $executable)){throw 'Build reviewed scene with Elemental/Graphics/Build Valley Atmosphere 1080 Benchmark first.'}
$reportFolder=Join-Path $ProjectRoot 'BuildReports'
New-Item -ItemType Directory -Path $reportFolder -Force | Out-Null
$process=Start-Process -FilePath $executable -WorkingDirectory $ProjectRoot -ArgumentList @('-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','--valley-benchmark','--valley-report=BuildReports/ValleyAtmosphere1080.json','-logFile','BuildReports/ValleyAtmosphere1080.log') -WindowStyle Hidden -PassThru
Write-Output "Benchmark PID $($process.Id). Expected 12 blocks x (240 warm + 600 measured frames); report in BuildReports. No isolated GPU cost is inferred."
