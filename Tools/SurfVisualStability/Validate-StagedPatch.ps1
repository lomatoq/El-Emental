$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$bee = Get-ChildItem (Join-Path $root "Library/Bee/artifacts") -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName "Elemental.Tests.PlayMode.rsp") } |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $bee) { throw "No PlayMode Bee response file found." }
$csc = "C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Data/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll"
$work = Join-Path $PSScriptRoot "Compile"
New-Item -ItemType Directory -Force -Path $work | Out-Null
$lines = Get-Content (Join-Path $bee.FullName "Elemental.Tests.PlayMode.rsp")
$source = "Assets/Elemental/Tests/PlayMode/SurfPillarJumpVisualQaTests.cs"
$staged = "Tools/SurfVisualStability/after/$source"
for ($index = 0; $index -lt $lines.Count; $index++) {
    if ($lines[$index] -like "-out:*") { $lines[$index] = "-out:`"$work/SurfVisual.dll`"" }
    elseif ($lines[$index] -like "-refout:*") { $lines[$index] = "-refout:`"$work/SurfVisual.ref.dll`"" }
    elseif ($lines[$index] -eq "`"$source`"") { $lines[$index] = "`"$staged`"" }
}
$rsp = Join-Path $work "SurfVisual.rsp"
Set-Content -LiteralPath $rsp -Value $lines -Encoding UTF8
Push-Location $root
try {
    & dotnet $csc "@$rsp"
    if ($LASTEXITCODE -ne 0) { throw "Surf visual staged compile failed ($LASTEXITCODE)." }
}
finally { Pop-Location }
Write-Host "Surf visual staged PlayMode compile passed."
