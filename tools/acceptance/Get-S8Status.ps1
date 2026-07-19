[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$artifactRoot = Join-Path $repositoryRoot 'artifacts\acceptance'
$metadataPath = Join-Path $artifactRoot 's8-process.json'
$exitCodePath = Join-Path $artifactRoot 's8-exit-code.txt'
$reportPath = Join-Path $artifactRoot 's8-stability.json'

if (-not (Test-Path -LiteralPath $metadataPath))
{
    throw 'S8 has not been started.'
}

$metadata = Get-Content -Raw -LiteralPath $metadataPath | ConvertFrom-Json
$running = [bool](Get-Process -Id $metadata.ProcessId -ErrorAction SilentlyContinue)
$startedAt = ([DateTimeOffset]$metadata.StartedAt).ToLocalTime()
$expectedEndAt = ([DateTimeOffset]$metadata.ExpectedEndAt).ToLocalTime()
$exitCode = if (Test-Path -LiteralPath $exitCodePath) { [int](Get-Content -Raw -LiteralPath $exitCodePath) } else { $null }
$report = if (-not $running -and (Test-Path -LiteralPath $reportPath))
{
    Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
}
else
{
    $null
}

[pscustomobject]@{
    ProcessId = $metadata.ProcessId
    Running = $running
    StartedAt = $startedAt
    ExpectedEndAt = $expectedEndAt
    ExitCode = $exitCode
    Completed = $report.Completed
    MeasuredSeconds = $report.MeasuredSeconds
    ThroughputPerSecond = $report.ThroughputPerSecond
    PrivateGrowthPercent = $report.PrivateGrowthPercent
    WorkingSetGrowthPercent = $report.WorkingSetGrowthPercent
    MeetsS8 = $report.MeetsS8
} | Format-List

$stdoutPath = Join-Path $repositoryRoot $metadata.StandardOutputPath
if (Test-Path -LiteralPath $stdoutPath)
{
    Write-Output 'Recent progress:'
    Get-Content -LiteralPath $stdoutPath -Tail 5
}
