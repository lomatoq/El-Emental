param()

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$files = @(
    @{
        Path = "Assets/Elemental/Runtime/Physics/EarthArmorController.cs"
        Sha256 = "DBC81CE64CBAD7874D0CC7351ADFBDD5E1F4DE38E272D9AF12D2AD7FE46B0C3D"
    },
    @{
        Path = "Assets/Elemental/Tests/PlayMode/ArmorJumpAimVisualProofRuntimeTests.cs"
        Sha256 = "22DDCF3DB4C92191A7981972D48BD714C23B9351998EA8C384886822CAB35794"
    }
)

foreach ($file in $files) {
    $target = Join-Path $projectRoot $file.Path
    if (!(Test-Path -LiteralPath $target)) { throw "Missing target: $target" }
    $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($actual -ne $file.Sha256) {
        throw "Refusing to overwrite newer source. Expected $($file.Sha256), actual $actual at $target"
    }
}

foreach ($file in $files) {
    $target = Join-Path $projectRoot $file.Path
    $source = Join-Path $PSScriptRoot (Join-Path "after" $file.Path)
    if (!(Test-Path -LiteralPath $source)) { throw "Missing staged source: $source" }
    Copy-Item -LiteralPath $source -Destination $target -Force
    Write-Host "Applied final collar repair: $($file.Path)"
}

