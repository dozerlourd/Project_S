[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string[]]$TestFilter,

    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe',

    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity executable was not found: $UnityPath"
}

if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath 'Assets'))) {
    throw "Unity project was not found: $ProjectPath"
}

$runningUnity = Get-Process -Name Unity -ErrorAction SilentlyContinue
if ($null -ne $runningUnity) {
    $ids = ($runningUnity | ForEach-Object { $_.Id }) -join ', '
    throw "Unity is already running (PID: $ids). Wait for it to exit before starting a Headless test run."
}

$resultDirectory = Join-Path $ProjectPath 'TestResults\Headless'
New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$failures = @()

foreach ($filter in $TestFilter) {
    $safeName = ($filter -replace '[^a-zA-Z0-9_.-]', '_')
    $resultPath = Join-Path $resultDirectory "$stamp-$safeName.xml"
    $logPath = Join-Path $resultDirectory "$stamp-$safeName.log"
    $unityArguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $ProjectPath,
        '-runTests',
        '-testPlatform', 'playmode',
        '-testFilter', $filter,
        '-testResults', $resultPath,
        '-logFile', $logPath
    )

    Write-Host "Running $filter"
    $process = Start-Process -FilePath $UnityPath -ArgumentList $unityArguments -PassThru -WindowStyle Hidden
    $process.WaitForExit()
    $exitCode = $process.ExitCode

    if (-not (Test-Path -LiteralPath $resultPath)) {
        throw "Unity exited with code $exitCode without writing its dedicated result file: $resultPath"
    }

    [xml]$report = Get-Content -LiteralPath $resultPath
    $run = $report.'test-run'
    if ($null -eq $run -or [string]::IsNullOrWhiteSpace($run.total)) {
        throw "The result file is not a completed Unity test report: $resultPath"
    }

    $failed = [int]$run.failed
    Write-Host "$filter total=$($run.total) passed=$($run.passed) failed=$failed exit=$exitCode"
    if ($failed -gt 0) {
        $failures += $filter
        Select-Xml -Xml $report -XPath "//test-case[@result!='Passed']" | ForEach-Object {
            Write-Host "  $($_.Node.fullname): $($_.Node.result)"
        }
    }

    if ($exitCode -ne 0 -and $failed -eq 0) {
        throw "Unity returned exit code $exitCode despite a passing report. Inspect: $logPath"
    }
}

if ($failures.Count -gt 0) {
    throw "Failed test filters: $($failures -join ', ')"
}
