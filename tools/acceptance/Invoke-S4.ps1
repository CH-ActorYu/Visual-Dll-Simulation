[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$distributionRoot = Join-Path $repositoryRoot 'artifacts\distribution\OpenCvDistance'
$consumerProject = Join-Path $repositoryRoot 'acceptance\S4.ConsoleConsumer\S4.ConsoleConsumer.csproj'
$demoProject = Join-Path $repositoryRoot 'samples\Visual.Distance.ConsoleDemo\Visual.Distance.ConsoleDemo.csproj'

New-Item -ItemType Directory -Path $distributionRoot -Force | Out-Null
dotnet publish $demoProject -c Release -o $distributionRoot --nologo
if ($LASTEXITCODE -ne 0) { throw 'OpenCV distance distribution publish failed.' }

dotnet run --project $consumerProject -c Release --nologo --property:VisualDistributionRoot=$distributionRoot
if ($LASTEXITCODE -ne 0) { throw 'S4 isolated DLL consumer failed.' }

$projectText = Get-Content -Raw -LiteralPath $consumerProject
if ($projectText -match '<ProjectReference|<PackageReference')
{
    throw 'S4 consumer must not contain ProjectReference or PackageReference.'
}

Write-Output "S4_PASS distribution=$distributionRoot consumer=$consumerProject"
