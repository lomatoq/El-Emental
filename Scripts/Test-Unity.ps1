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
    & $UnityPath -batchmode -nographics -projectPath $ProjectPath -runTests -testPlatform $Target -testResults $ResultFile -logFile $LogFile
    if ($LASTEXITCODE -ne 0) {
        throw "Unity $Target tests failed with exit code $LASTEXITCODE. See $LogFile"
    }
}
