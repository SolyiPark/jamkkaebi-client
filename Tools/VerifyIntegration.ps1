param(
    [Parameter(Mandatory=$true)][string]$EditorPath
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $projectRoot 'Logs\Integration'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

function Invoke-UnityCheck([string[]]$ExtraArguments, [string]$LogName) {
    $arguments = @('-batchmode', '-projectPath', ('"' + $projectRoot + '"'),
        '-logFile', ('"' + (Join-Path $outputRoot $LogName) + '"')) + $ExtraArguments
    $process = Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Unity failed ($($process.ExitCode)); see $LogName" }
}

$testPath = Join-Path $outputRoot 'EditMode.xml'
Invoke-UnityCheck @('-nographics', '-runTests', '-testPlatform', 'EditMode', '-testResults', ('"' + $testPath + '"')) 'editmode.log'
[xml]$tests = Get-Content -LiteralPath $testPath -Raw
if ($tests.'test-run'.result -ne 'Passed') { throw 'EditMode tests did not pass.' }
Invoke-UnityCheck @('-quit', '-executeMethod', 'IntegratedSetup.Build') 'build.log'
$player = Join-Path $projectRoot 'Builds\Integrated\Jamkkaebi.exe'
$runOutput = Join-Path $outputRoot ('Player-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $runOutput | Out-Null
$process = Start-Process -FilePath $player -ArgumentList @(
    ('"--verify-output=' + $runOutput + '"'), '-screen-width', '640', '-screen-height', '960',
    '-screen-fullscreen', '0', '-logFile', ('"' + (Join-Path $runOutput 'player.log') + '"')
) -WindowStyle Hidden -PassThru -Wait
$report = Join-Path $runOutput 'verification.txt'
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $report)) { throw "Player verification failed; see $runOutput" }
if ((Get-Content -LiteralPath $report -First 1) -ne 'ALL CHECKS PASSED') { throw "Checks failed; see $report" }
Write-Output "EditMode tests and integrated player checks passed. Results: $outputRoot"
