[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$wheel = Join-Path $root 'components\wheel'
& (Join-Path $wheel 'tools\Verify-Dependencies.ps1')
& dotnet build (Join-Path $wheel 'WodenRallyEdgeWheel.sln') -c Release -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Wheel build failed' }
foreach ($project in 'WodenRallyEdge.Tests','WodenRallyEdge.UiTests') {
    & dotnet run --project (Join-Path $wheel "tests\$project") -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "$project failed" }
}
$triple = Join-Path $root 'components\triple'
& dotnet run --project (Join-Path $triple 'tests\WodenTripleScreenProbe.Tests') -c Release -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Triple offline regression failed' }
& dotnet build (Join-Path $triple 'src\WodenTripleScreenProbe.Plugin\WodenTripleScreenProbe.Plugin.csproj') -c Release -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Triple probe build failed' }
