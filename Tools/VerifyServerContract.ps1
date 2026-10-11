param(
    [Parameter(Mandatory=$true)][string]$ServerPath,
    [Parameter(Mandatory=$true)][string]$EditorPath,
    [Parameter(Mandatory=$true)][string]$JavaHome
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$serverRoot = (Resolve-Path -LiteralPath $ServerPath).Path
$outputRoot = Join-Path $projectRoot ('Logs\ServerContract\Run-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$fixturePath = Join-Path $serverRoot 'contract-session.json'
$stopPath = Join-Path $serverRoot 'contract-stop'
$harnessPath = Join-Path $serverRoot 'src\test\java\com\example\jamkkaebi\friend\UnityContractHarnessTest.java'
if (!(Test-Path -LiteralPath (Join-Path $serverRoot 'gradlew.bat')) -or
    !(Test-Path -LiteralPath (Join-Path $JavaHome 'bin\java.exe'))) { throw '서버 체크아웃과 Java 21 경로를 확인하세요.' }
foreach ($target in @($fixturePath, $stopPath, $harnessPath)) {
    if (Test-Path -LiteralPath $target) { throw "기존 검증 파일을 덮어쓰지 않습니다: $target" }
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$oldConfig = $env:JAMKKAEBI_CONTRACT_CONFIG
$oldUrl = $env:JAMKKAEBI_SERVER_URL
$oldAccess = $env:JAMKKAEBI_ACCESS_TOKEN
$oldRefresh = $env:JAMKKAEBI_REFRESH_TOKEN
$serverJob = $null
function Invoke-Unity([string[]]$extra, [string]$log) {
    $arguments = @('-batchmode', '-projectPath', ('"' + $projectRoot + '"'), '-logFile', ('"' + (Join-Path $outputRoot $log) + '"')) + $extra
    $process = Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity 검증 실패: $log ($($process.ExitCode))" }
}
try {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityContractHarnessTest.java') -Destination $harnessPath
    $serverJob = Start-Job -ArgumentList $serverRoot, $JavaHome, $outputRoot -ScriptBlock {
        param($root, $runtime, $logs)
        Set-Location -LiteralPath $root
        $env:JAVA_HOME = $runtime
        & .\gradlew.bat test --tests '*UnityContractHarnessTest' --rerun-tasks --no-daemon *> (Join-Path $logs 'spring.log')
        if ($LASTEXITCODE -ne 0) { throw 'Spring 검증 서버가 종료되었습니다.' }
    }
    $deadline = (Get-Date).AddMinutes(2)
    while (!(Test-Path -LiteralPath $fixturePath)) {
        if ($serverJob.State -in @('Completed', 'Failed', 'Stopped') -or (Get-Date) -gt $deadline) { throw 'Spring 검증 서버를 시작하지 못했습니다. spring.log를 확인하세요.' }
        Start-Sleep -Milliseconds 250
    }
    $env:JAMKKAEBI_CONTRACT_CONFIG = $fixturePath
    # 통합 검증은 먼저 모의 UI를 검증하고 마지막에 로컬 서버로 전환합니다.
    $env:JAMKKAEBI_SERVER_URL = $null; $env:JAMKKAEBI_ACCESS_TOKEN = $null; $env:JAMKKAEBI_REFRESH_TOKEN = $null
    $testPath = Join-Path $outputRoot 'EditMode.xml'
    Invoke-Unity @('-runTests', '-testPlatform', 'EditMode', '-testResults', ('"' + $testPath + '"')) 'editmode.log'
    [xml]$tests = Get-Content -LiteralPath $testPath -Raw
    if ($tests.'test-run'.result -ne 'Passed' -or [int]$tests.'test-run'.skipped -ne 0) { throw '서버 통신 테스트가 통과하지 않았거나 생략되었습니다.' }
    Invoke-Unity @('-quit', '-executeMethod', 'IntegratedSetup.Build') 'build.log'
    $playerOutput = Join-Path $outputRoot 'Player'
    New-Item -ItemType Directory -Path $playerOutput | Out-Null
    $player = Start-Process -FilePath (Join-Path $projectRoot 'Builds\Integrated\Jamkkaebi.exe') -ArgumentList @(
        ('"--verify-output=' + $playerOutput + '"'), '-screen-width', '640', '-screen-height', '960', '-screen-fullscreen', '0',
        '-logFile', ('"' + (Join-Path $playerOutput 'player.log') + '"')
    ) -WindowStyle Hidden -PassThru
    $player.WaitForExit()
    if ($player.ExitCode -ne 0 -or (Get-Content -LiteralPath (Join-Path $playerOutput 'verification.txt') -First 1) -ne 'ALL CHECKS PASSED') { throw 'Player 통합 검증이 실패했습니다.' }
    if (Select-String -LiteralPath (Join-Path $playerOutput 'player.log') -Pattern 'GarbageCollector disposing of ComputeBuffer|Unreleased non-evictable resource|Sprite fallback buffer cleanup is unavailable' -Quiet) { throw 'Player GPU 종료 검사가 실패했습니다.' }
    Write-Output "Unity와 로컬 Spring 통신 검증 통과: $outputRoot"
}
finally {
    if ($serverJob) {
        New-Item -ItemType File -Path $stopPath -Force | Out-Null
        $serverJob | Wait-Job -Timeout 30 | Out-Null
        if ($serverJob.State -notin @('Completed', 'Failed', 'Stopped')) { $serverJob | Stop-Job }
        $serverJob | Remove-Job -Force
    }
    foreach ($target in @($harnessPath, $fixturePath, $stopPath)) {
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target }
    }
    $env:JAMKKAEBI_CONTRACT_CONFIG = $oldConfig
    $env:JAMKKAEBI_SERVER_URL = $oldUrl
    $env:JAMKKAEBI_ACCESS_TOKEN = $oldAccess
    $env:JAMKKAEBI_REFRESH_TOKEN = $oldRefresh
}
