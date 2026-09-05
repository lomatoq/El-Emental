$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$relative = "Assets/Elemental/Tests/PlayMode/SurfPillarJumpVisualQaTests.cs"
$target = Join-Path $root $relative
$expected = "ADC85DBDAAF27937969B684CA7E16C55654AAF57A92BA80330C090FA200BCCBB"
$actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $target).Hash
if ($actual -ne $expected) {
    throw "Surf visual preflight refused newer test source. Expected $expected, actual $actual."
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "after/$relative") -Destination $target -Force
Write-Host "Applied deterministic additive surf visual runway; production sources unchanged."
