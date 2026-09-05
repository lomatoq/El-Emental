param()

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$relativePath = "Assets/Elemental/Tests/PlayMode/EarthArmorCoverageRuntimeTests.cs"
$target = Join-Path $projectRoot $relativePath
$source = Join-Path $PSScriptRoot (Join-Path "after" $relativePath)
$expectedSha256 = "2C8989F128EFD84BF126C1E0CDB03F1BA009BD8CBCF3A1F37D91FA98CD3D9B87"

if (!(Test-Path -LiteralPath $target)) { throw "Missing target: $target" }
if (!(Test-Path -LiteralPath $source)) { throw "Missing staged source: $source" }
$actualSha256 = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
if ($actualSha256 -ne $expectedSha256) {
    throw "Refusing to overwrite newer test source. Expected $expectedSha256, actual $actualSha256 at $target"
}

Copy-Item -LiteralPath $source -Destination $target -Force
Write-Host "Applied armor coverage diagnostic test: $relativePath"

