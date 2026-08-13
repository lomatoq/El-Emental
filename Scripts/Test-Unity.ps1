param(
    [string]$UnityPath = $env:UNITY_EDITOR_PATH,
    [ValidateSet('EditMode', 'PlayMode', 'All')]
    [string]$Platform = 'All'
)

$ErrorActionPreference = 'Stop'
$ProjectPath = Split-Path -Parent $PSScriptRoot
$ResultsPath = Join-Path $ProjectPath 'TestResults'
New-Item -ItemType Directory -Path $ResultsPath -Force | Out-Null

if ([string]::IsNullOrWhiteSpace($UnityPath) -or -not (Test-Path -LiteralPath $UnityPath)) {
    throw 'Pass -UnityPath or set UNITY_EDITOR_PATH to the Unity executable.'
}

$Targets = if ($Platform -eq 'All') { @('EditMode', 'PlayMode') } else { @($Platform) }

foreach ($Target in $Targets) {
    $ResultFile = Join-Path $ResultsPath ($Target + '.xml')
    $LogFile = Join-Path $ResultsPath ($Target + '.log')
    # Unity.exe is a Windows GUI-subsystem process. Direct invocation returns
    # before it exits, leaving $LASTEXITCODE empty; Start-Process -Wait is
    # required. Use one explicitly quoted argument string because the project
    # and result paths may contain spaces.
    $Quote = [char]34
    $Arguments = '-batchmode -nographics' +
        ' -projectPath ' + $Quote + $ProjectPath + $Quote +
        ' -runTests -testPlatform ' + $Target +
        ' -testResults ' + $Quote + $ResultFile + $Quote +
        ' -logFile ' + $Quote + $LogFile + $Quote
    $Process = Start-Process -FilePath $UnityPath -ArgumentList $Arguments -Wait -PassThru
    if ($Process.ExitCode -ne 0) {
        throw "Unity $Target tests failed with exit code $($Process.ExitCode). See $LogFile"
    }
}
