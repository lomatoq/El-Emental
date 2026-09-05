param(
    [string]$UnityVersion = "6000.5.7f1"
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$bee = Get-ChildItem (Join-Path $projectRoot "Library/Bee/artifacts") -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName "Elemental.Tests.PlayMode.rsp") } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if ($null -eq $bee) { throw "No complete Unity Bee response-file directory found." }

$csc = "C:/Program Files/Unity/Hub/Editor/$UnityVersion/Editor/Data/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll"
if (!(Test-Path $csc)) { throw "Roslyn compiler not found: $csc" }
$work = Join-Path $PSScriptRoot "Compile"
New-Item -ItemType Directory -Force -Path $work | Out-Null

function Invoke-StagedCompile {
    param(
        [string]$Assembly,
        [hashtable]$Replace,
        [string[]]$Append,
        [hashtable]$ReferenceReplace
    )
    $sourceRsp = Join-Path $bee.FullName "$Assembly.rsp"
    $lines = Get-Content -LiteralPath $sourceRsp
    $output = Join-Path $work "$Assembly.dll"
    $refOutput = Join-Path $work "$Assembly.ref.dll"
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -like "-out:*") { $lines[$index] = "-out:`"$output`""; continue }
        if ($lines[$index] -like "-refout:*") { $lines[$index] = "-refout:`"$refOutput`""; continue }
        foreach ($entry in $Replace.GetEnumerator()) {
            if ($lines[$index] -eq "`"$($entry.Key)`"") {
                $lines[$index] = "`"$($entry.Value)`""
            }
        }
        foreach ($entry in $ReferenceReplace.GetEnumerator()) {
            if ($lines[$index] -eq "-r:`"$($entry.Key)`"") {
                $lines[$index] = "-r:`"$($entry.Value)`""
            }
        }
    }
    foreach ($path in $Append) { $lines += "`"$path`"" }
    $generatedRsp = Join-Path $work "$Assembly.staged.rsp"
    Set-Content -LiteralPath $generatedRsp -Value $lines -Encoding UTF8
    Push-Location $projectRoot
    try {
        & dotnet $csc "@$generatedRsp"
        if ($LASTEXITCODE -ne 0) { throw "$Assembly staged compile failed ($LASTEXITCODE)." }
    }
    finally { Pop-Location }
}

$stage = "Tools/ArmorCoverageFix/after/Assets/Elemental"
$empty = @{}
$simulationReplace = @{
    "Assets/Elemental/Simulation/Bending/EarthArmorCoverageShell.cs" = "$stage/Simulation/Bending/EarthArmorCoverageShell.cs"
}
Invoke-StagedCompile "Elemental.Simulation" $simulationReplace @() $empty

$runtimeReplace = @{
    "Assets/Elemental/Runtime/Physics/EarthArmorController.cs" = "$stage/Runtime/Physics/EarthArmorController.cs"
    "Assets/Elemental/Runtime/Physics/EarthArmorProfile.cs" = "$stage/Runtime/Physics/EarthArmorProfile.cs"
}
$simulationReference = @{
    "Library/Bee/artifacts/$($bee.Name)/Elemental.Simulation.ref.dll" = (Join-Path $work "Elemental.Simulation.ref.dll")
}
Invoke-StagedCompile "Elemental.Runtime" $runtimeReplace @() $simulationReference

$testReferences = @{
    "Library/Bee/artifacts/$($bee.Name)/Elemental.Simulation.ref.dll" = (Join-Path $work "Elemental.Simulation.ref.dll")
    "Library/Bee/artifacts/$($bee.Name)/Elemental.Runtime.ref.dll" = (Join-Path $work "Elemental.Runtime.ref.dll")
}
$editReplace = @{
    "Assets/Elemental/Tests/EditMode/EarthArmorCoverageShellTests.cs" = "$stage/Tests/EditMode/EarthArmorCoverageShellTests.cs"
    "Assets/Elemental/Tests/EditMode/EarthArmorCoverageTestLauncher.cs" = "$stage/Tests/EditMode/EarthArmorCoverageTestLauncher.cs"
}
Invoke-StagedCompile "Elemental.Tests.EditMode" $editReplace @() $testReferences

$playReplace = @{
    "Assets/Elemental/Tests/PlayMode/ArmorJumpAimVisualProofRuntimeTests.cs" = "$stage/Tests/PlayMode/ArmorJumpAimVisualProofRuntimeTests.cs"
    "Assets/Elemental/Tests/PlayMode/EarthArmorCoverageRuntimeTests.cs" = "$stage/Tests/PlayMode/EarthArmorCoverageRuntimeTests.cs"
}
Invoke-StagedCompile "Elemental.Tests.PlayMode" $playReplace @() $testReferences

Write-Host "Armor coverage staged compile passed for Simulation, Runtime, EditMode and PlayMode."
