$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$output = Join-Path $PSScriptRoot 'CompileCheck'
[IO.Directory]::CreateDirectory($output) | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)
$compiler = 'C:/Program Files/dotnet/sdk/10.0.400/Roslyn/bincore/csc.dll'
Push-Location $project
try {
    foreach ($assembly in @('Simulation','Runtime','Input')) {
        $originalRsp = Join-Path $project ('Library/Bee/artifacts/1900b0aPDev.dag/Elemental.' + $assembly + '.rsp')
        $lines = [IO.File]::ReadAllLines($originalRsp) | Where-Object {
            $_ -notmatch '^[-/]out:' -and $_ -notmatch '^[-/]refout:' -and
            $_ -notmatch ('^"Assets/Elemental/' + $assembly + '/.*\.cs"$')
        }
        $lines = $lines | ForEach-Object {
            $_.Replace('Library/Bee/artifacts/1900b0aPDev.dag/Elemental.Core.ref.dll','Library/ScriptAssemblies/Elemental.Core.dll')
        }
        if ($assembly -ne 'Simulation') {
            $lines = $lines | ForEach-Object {
                $_.Replace('Library/Bee/artifacts/1900b0aPDev.dag/Elemental.Simulation.ref.dll',
                    ((Join-Path $output 'Elemental.Simulation.dll').Replace('\','/')))
            }
        }
        if ($assembly -eq 'Input') {
            $lines = $lines | ForEach-Object {
                $_.Replace('Library/Bee/artifacts/1900b0aPDev.dag/Elemental.Runtime.ref.dll',
                    ((Join-Path $output 'Elemental.Runtime.dll').Replace('\','/')))
            }
        }
        $lines += '-out:"' + (Join-Path $output ('Elemental.' + $assembly + '.dll')) + '"'
        foreach ($file in (Get-ChildItem -LiteralPath ('Assets/Elemental/' + $assembly) -Filter '*.cs' -Recurse)) {
            $relative = [IO.Path]::GetRelativePath($project,$file.FullName).Replace('\','/')
            $replacement = Join-Path $PSScriptRoot ('Patches/Modified/' + $relative)
            $lines += '"' + $(if (Test-Path -LiteralPath $replacement) { $replacement } else { $file.FullName }) + '"'
        }
        foreach ($file in (Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot ('Assets/Elemental/' + $assembly)) -Filter '*.cs' -Recurse)) {
            $lines += '"' + $file.FullName + '"'
        }
        $rsp = Join-Path $output ('Elemental.' + $assembly + '.rsp')
        [IO.File]::WriteAllLines($rsp,[string[]]$lines,$utf8)
        & dotnet $compiler ('@' + $rsp) 2>&1 | Tee-Object -FilePath (Join-Path $output ($assembly + '.log'))
        if ($LASTEXITCODE -ne 0) { throw "$assembly compiler exit $LASTEXITCODE" }
    }
} finally { Pop-Location }
