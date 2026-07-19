[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$artifactRoot = Join-Path $repositoryRoot 'artifacts\acceptance'
$metadataPath = Join-Path $artifactRoot 's8-process.json'
$stdoutPath = Join-Path $artifactRoot 's8-stdout.log'
$stderrPath = Join-Path $artifactRoot 's8-stderr.log'
$exitCodePath = Join-Path $artifactRoot 's8-exit-code.txt'
$reportPath = Join-Path $artifactRoot 's8-stability.json'
$project = Join-Path $repositoryRoot 'acceptance\Visual.AcceptanceRunner\Visual.AcceptanceRunner.csproj'
$worker = Join-Path $repositoryRoot 'tools\acceptance\Invoke-S8Worker.ps1'

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
if (Test-Path -LiteralPath $metadataPath)
{
    $existing = Get-Content -Raw -LiteralPath $metadataPath | ConvertFrom-Json
    if (Get-Process -Id $existing.ProcessId -ErrorAction SilentlyContinue)
    {
        throw "S8 is already running with PID $($existing.ProcessId)."
    }
}

dotnet build $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Acceptance runner Release build failed.' }

Remove-Item -LiteralPath $stdoutPath,$stderrPath,$exitCodePath,$reportPath -Force -ErrorAction SilentlyContinue
$startedAt = [DateTimeOffset]::Now
$powershell = (Get-Process -Id $PID).Path
$workerArguments = "-NoProfile -ExecutionPolicy Bypass -File `"$worker`""
$process = Start-Process -FilePath $powershell `
    -ArgumentList $workerArguments `
    -WorkingDirectory $repositoryRoot `
    -RedirectStandardOutput $stdoutPath `
    -RedirectStandardError $stderrPath `
    -WindowStyle Hidden `
    -PassThru

[pscustomobject]@{
    ProcessId = $process.Id
    StartedAt = $startedAt.ToString('o')
    ExpectedEndAt = $startedAt.AddSeconds(7230).ToString('o')
    ReportPath = 'artifacts/acceptance/s8-stability.json'
    StandardOutputPath = 'artifacts/acceptance/s8-stdout.log'
    StandardErrorPath = 'artifacts/acceptance/s8-stderr.log'
} | ConvertTo-Json | Set-Content -LiteralPath $metadataPath -Encoding utf8

Get-Content -Raw -LiteralPath $metadataPath
