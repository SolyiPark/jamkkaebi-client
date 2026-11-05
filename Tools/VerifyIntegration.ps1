param(
    [Parameter(Mandatory=$true)][string]$EditorPath
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $projectRoot 'Logs\Integration'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

# Start-Process -Wait also waits for Unity's child processes, which can outlive the editor by minutes.
function Wait-ProcessExit([System.Diagnostics.Process]$Process) {
    $null = $Process.Handle # Keeps ExitCode readable after the process ends.
    $Process.WaitForExit()
    return $Process.ExitCode
}

function Invoke-UnityCheck([string[]]$ExtraArguments, [string]$LogName) {
    $arguments = @('-batchmode', '-projectPath', ('"' + $projectRoot + '"'),
        '-logFile', ('"' + (Join-Path $outputRoot $LogName) + '"')) + $ExtraArguments
    $exitCode = Wait-ProcessExit (Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru)
    if ($exitCode -ne 0) { throw "Unity failed ($exitCode); see $LogName" }
}

# Only the HTTP tests that need JAMKKAEBI_CONTRACT_CONFIG may be skipped here. VerifyServerContract.ps1 runs them.
$allowedSkips = @(
    'Jamkkaebi.Tests.UI.ExhibitionLayoutTransmissionTests.BatchSaveRoundTripsRealOwnedObjectsAndRecall',
    'Jamkkaebi.Tests.UI.ServerContractTests.CancelledSharedRefreshCompletesWaitingRequestWithAnError',
    'Jamkkaebi.Tests.UI.ServerContractTests.UnityHttpTalksToRealSpringServer'
)

function Assert-EditModeResult([xml]$Tests, [string[]]$AllowedSkips) {
    $run = $Tests.'test-run'
    if ($run.result -notin @('Passed', 'Skipped:Ignored')) { throw "EditMode tests did not pass ($($run.result))." }
    if ([int]$run.failed -ne 0 -or [int]$run.inconclusive -ne 0 -or [int]$run.passed -le 0) {
        throw "EditMode tests did not pass (passed=$($run.passed), failed=$($run.failed), inconclusive=$($run.inconclusive))."
    }
    # Compare the listed cases too, so a mismatched skipped count cannot hide an unexpected skip.
    $skipped = @($Tests.SelectNodes("//test-case[@result='Skipped']") | ForEach-Object { $_.GetAttribute('fullname') })
    $unexpected = @($skipped | Where-Object { $_ -notin $AllowedSkips })
    if ($unexpected.Count -gt 0) { throw "Unexpected skipped EditMode tests: $($unexpected -join ', ')" }
    if ([int]$run.skipped -ne $skipped.Count) { throw "EditMode skipped count mismatch ($($run.skipped) vs $($skipped.Count))." }
}

$testPath = Join-Path $outputRoot 'EditMode.xml'
Invoke-UnityCheck @('-nographics', '-runTests', '-testPlatform', 'EditMode', '-testResults', ('"' + $testPath + '"')) 'editmode.log'
[xml]$tests = Get-Content -LiteralPath $testPath -Raw
Assert-EditModeResult $tests $allowedSkips
Invoke-UnityCheck @('-quit', '-executeMethod', 'IntegratedSetup.Build') 'build.log'
$player = Join-Path $projectRoot 'Builds\Integrated\Jamkkaebi.exe'
$runOutput = Join-Path $outputRoot ('Player-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $runOutput | Out-Null
$playerExitCode = Wait-ProcessExit (Start-Process -FilePath $player -ArgumentList @(
    ('"--verify-output=' + $runOutput + '"'), '-screen-width', '640', '-screen-height', '960',
    '-screen-fullscreen', '0', '-logFile', ('"' + (Join-Path $runOutput 'player.log') + '"')
) -WindowStyle Hidden -PassThru)
$report = Join-Path $runOutput 'verification.txt'
if ($playerExitCode -ne 0 -or -not (Test-Path -LiteralPath $report)) { throw "Player verification failed; see $runOutput" }
if ((Get-Content -LiteralPath $report -First 1) -ne 'ALL CHECKS PASSED') { throw "Checks failed; see $report" }
# The in-player report is written before shutdown. Check the final log as well.
$playerLog = Join-Path $runOutput 'player.log'
if (Select-String -LiteralPath $playerLog -Pattern 'GarbageCollector disposing of ComputeBuffer|Unreleased non-evictable resource|Sprite fallback buffer cleanup is unavailable' -Quiet) {
    throw "GPU buffer shutdown validation failed; see $playerLog"
}
Write-Output "EditMode tests and integrated player checks passed. Results: $outputRoot"
