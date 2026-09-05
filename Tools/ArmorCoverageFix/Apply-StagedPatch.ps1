$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path

$expected = @{
    "Assets/Elemental/Runtime/Physics/EarthArmorController.cs" = "253D8342159E6A4862B6050A90107281676BAC2F9AC498B6F8F1269E05331C9B"
    "Assets/Elemental/Runtime/Physics/EarthArmorProfile.cs" = "76D3AD94618C96798EF054E21A6CA3D07FA9429092966DB33D45D60EA372837C"
    "Assets/Elemental/Tests/PlayMode/ArmorJumpAimVisualProofRuntimeTests.cs" = "C1178D25BC4BDB0BDAE8ED68AD26BB6DCE9FC4176509AB7018EE91294A156F92"
}

foreach ($entry in $expected.GetEnumerator()) {
    $target = Join-Path $projectRoot $entry.Key
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $target).Hash
    if ($actual -ne $entry.Value) {
        throw "Preflight refused: $($entry.Key) changed after staging. Merge the staged file manually. Expected $($entry.Value), actual $actual."
    }
}

$files = @(
    "Simulation/Bending/EarthArmorCoverageShell.cs",
    "Runtime/Physics/EarthArmorController.cs",
    "Runtime/Physics/EarthArmorProfile.cs",
    "Tests/EditMode/EarthArmorCoverageShellTests.cs",
    "Tests/EditMode/EarthArmorCoverageTestLauncher.cs",
    "Tests/PlayMode/EarthArmorCoverageRuntimeTests.cs",
    "Tests/PlayMode/ArmorJumpAimVisualProofRuntimeTests.cs"
)
foreach ($relative in $files) {
    $source = Join-Path $PSScriptRoot "after/Assets/Elemental/$relative"
    $target = Join-Path $projectRoot "Assets/Elemental/$relative"
    New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
}

Write-Host "Armor coverage patch copied after exact baseline preflight. Refresh Unity once, then run the focused menus in README.md."
