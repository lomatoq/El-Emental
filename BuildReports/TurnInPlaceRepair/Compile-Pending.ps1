$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$output = Join-Path $PSScriptRoot 'CompileCheck'
[IO.Directory]::CreateDirectory($output) | Out-Null
$compiler = 'C:/Program Files/dotnet/sdk/10.0.400/Roslyn/bincore/csc.dll'
Push-Location $project
try {
    foreach ($assembly in @('Simulation','Presentation','Tests.EditMode','Tests.PlayMode')) {
        $dag = '1900b0aE.dag'
        $folder = if ($assembly.StartsWith('Tests')) { 'Tests/' + $assembly.Substring(6) } else { $assembly }
        $sourcePrefix = '"Assets/Elemental/' + $folder + '/'
        $lines = [IO.File]::ReadAllLines((Join-Path $project "Library/Bee/artifacts/$dag/Elemental.$assembly.rsp")) | Where-Object { $_ -notmatch '^[-/]out:' -and $_ -notmatch '^[-/]refout:' -and !$_.StartsWith($sourcePrefix) }
        foreach ($file in Get-ChildItem -LiteralPath "Assets/Elemental/$folder" -Filter '*.cs' -Recurse) {
            $lines += '"' + [IO.Path]::GetRelativePath($project, $file.FullName).Replace('\','/') + '"'
        }
        $lines = $lines | ForEach-Object {
            $line = $_
            foreach ($dependency in @('Runtime','Input','Core')) {
                $line = $line -replace "Library/Bee/artifacts/[^/]+/Elemental\.$dependency\.ref\.dll", "Library/ScriptAssemblies/Elemental.$dependency.dll"
            }
            foreach ($dependency in @('Simulation','Presentation')) {
                $replacement = Join-Path $output "Elemental.$dependency.dll"
                if (Test-Path -LiteralPath $replacement) { $line = $line -replace "Library/Bee/artifacts/[^/]+/Elemental\.$dependency\.ref\.dll", $replacement.Replace('\','/') }
            }
            if ($line -eq '"Assets/Elemental/Presentation/Animation/HumanoidCharacterPresentation.cs"') {
                $line = '"' + (Join-Path $PSScriptRoot 'Modified/Assets/Elemental/Presentation/Animation/HumanoidCharacterPresentation.cs') + '"'
            }
            $line
        }
        $newRoot = Join-Path $PSScriptRoot "Assets/Elemental/$folder"
        if (Test-Path -LiteralPath $newRoot) { foreach ($file in Get-ChildItem -LiteralPath $newRoot -Filter '*.cs' -Recurse) { $lines += '"' + $file.FullName + '"' } }
        $lines += '-out:"' + (Join-Path $output "Elemental.$assembly.dll") + '"'
        $response = Join-Path $output "$assembly.rsp"
        [IO.File]::WriteAllLines($response, [string[]]$lines, [Text.UTF8Encoding]::new($false))
        & dotnet $compiler ('@' + $response) 2>&1 | Tee-Object -FilePath (Join-Path $output "$assembly.log")
        if ($LASTEXITCODE -ne 0) { throw "$assembly compile failed" }
    }
    $diff = & git -c core.autocrlf=false diff --no-index --src-prefix=a/ --dst-prefix=b/ -- BuildReports/TurnInPlaceRepair/Original/Assets/Elemental/Presentation/Animation/HumanoidCharacterPresentation.cs BuildReports/TurnInPlaceRepair/Modified/Assets/Elemental/Presentation/Animation/HumanoidCharacterPresentation.cs
    if ($LASTEXITCODE -gt 1) { throw 'Diff failed' }
    $patch = ($diff -join "`n").Replace('a/BuildReports/TurnInPlaceRepair/Original/', 'a/').Replace('b/BuildReports/TurnInPlaceRepair/Modified/', 'b/') + "`n"
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'TurnSteps.patch'), $patch, [Text.UTF8Encoding]::new($false))
    & git apply --check BuildReports/TurnInPlaceRepair/TurnSteps.patch
    if ($LASTEXITCODE -ne 0) { throw 'Live source changed; regenerate the narrow patch before applying.' }
} finally { Pop-Location }
