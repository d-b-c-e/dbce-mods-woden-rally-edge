[CmdletBinding()]
param([string]$NuGetConfig)
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$wheel = Join-Path $root 'components\wheel'
& (Join-Path $wheel 'tools\Verify-Dependencies.ps1')
$restore = @()
if ($NuGetConfig) { $restore = @("-p:RestoreConfigFile=$NuGetConfig") }
& dotnet build (Join-Path $wheel 'WodenRallyEdgeWheel.sln') -c Release -warnaserror @restore
if ($LASTEXITCODE -ne 0) { throw 'Wheel build failed' }
foreach ($project in 'WodenRallyEdge.Tests','WodenRallyEdge.UiTests') {
    & dotnet run --project (Join-Path $wheel "tests\$project") -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "$project failed" }
}
& dotnet build (Join-Path $PSScriptRoot 'ForceEnvelopeFixtures') -c Release -warnaserror @restore
if ($LASTEXITCODE -ne 0) { throw 'Force envelope fixture build failed' }
& dotnet run --project (Join-Path $PSScriptRoot 'ForceEnvelopeFixtures') -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'Force envelope fixture failed' }
