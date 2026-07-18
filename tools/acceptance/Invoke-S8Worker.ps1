[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$runner = Join-Path $repositoryRoot 'artifacts\bin\Visual.AcceptanceRunner\Release\net10.0\Visual.AcceptanceRunner.dll'
$report = Join-Path $repositoryRoot 'artifacts\acceptance\s8-stability.json'
$exitCodeFile = Join-Path $repositoryRoot 'artifacts\acceptance\s8-exit-code.txt'

Push-Location $repositoryRoot
try
{
    dotnet $runner --mode stability --output $report
    $code = $LASTEXITCODE
    Set-Content -LiteralPath $exitCodeFile -Value $code -Encoding ascii
    exit $code
}
catch
{
    Set-Content -LiteralPath $exitCodeFile -Value 1 -Encoding ascii
    throw
}
finally
{
    Pop-Location
}
