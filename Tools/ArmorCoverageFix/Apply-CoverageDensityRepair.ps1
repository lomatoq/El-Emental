$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$expected = @{
    "Assets/Elemental/Simulation/Bending/EarthArmorCoverageShell.cs" = "649E616D09C7B05A667A2DA98901AFB53090D87580E34609B4641535A65750BE"
    "Assets/Elemental/Tests/EditMode/EarthArmorCoverageShellTests.cs" = "C320C6A59C04B037A62EF078A7AB7D0CAA61BBF34A49F2DD37DBC3AF7DE13961"
}
foreach ($entry in $expected.GetEnumerator()) {
    $target = Join-Path $projectRoot $entry.Key
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $target).Hash
    if ($actual -ne $entry.Value) {
        throw "Coverage repair preflight refused newer file $($entry.Key). Expected $($entry.Value), actual $actual."
    }
}
foreach ($relative in $expected.Keys) {
    $suffix = $relative.Substring("Assets/Elemental/".Length)
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "after/Assets/Elemental/$suffix") `
        -Destination (Join-Path $projectRoot $relative) -Force
}
Write-Host "Applied denser 8/4/4/6/6 armor junction layout after exact preflight."
