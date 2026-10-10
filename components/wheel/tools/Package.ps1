[CmdletBinding()]
param([ValidatePattern('^[a-z0-9][a-z0-9.-]*$')][string]$ArtifactLabel)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Verify-Dependencies.ps1')
& dotnet build (Join-Path $root 'WodenRallyEdgeWheel.sln') -c Release -warnaserror -v q
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
& dotnet run --project (Join-Path $root 'tests\WodenRallyEdge.Tests') -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'Regression failed' }
& dotnet run --project (Join-Path $root 'tests\WodenRallyEdge.UiTests') -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'UI fixture failed' }
& dotnet run --project (Join-Path $root 'tests\WodenRallyEdge.InputTests') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Coherent input/profile-hat fixture failed' }
$stage = Join-Path $root ('dist\stage-' + [guid]::NewGuid().ToString('N'))
$plugin = Join-Path $stage 'BepInEx\plugins\WodenRallyEdgeWheel'
New-Item -ItemType Directory -Force $plugin | Out-Null
$bin = Join-Path $root 'src\WodenRallyEdge.Plugin\bin\Release\net6.0'
# Allowlist prevents game/interop/core/analysis assemblies entering a release.
foreach ($name in 'WodenRallyEdgeWheel.dll','WodenRallyEdge.Core.dll','Dbce.Wheel.Telemetry.dll','Dbce.Wheel.Recording.dll','Dbce.Wheel.Ffb.dll','WheelFfb.dll','Dbce.Wheel.Playback.dll') {
    Copy-Item -LiteralPath (Join-Path $bin $name) -Destination $plugin
}
Copy-Item -LiteralPath (Join-Path $root 'lib\recording\provenance.json') -Destination (Join-Path $plugin 'recording-provenance.json')
$playback = Join-Path $root '../../vendor/playback'
Copy-Item -LiteralPath (Join-Path $playback 'provenance.json') -Destination (Join-Path $plugin 'playback-provenance.json')
Copy-Item -LiteralPath (Join-Path $playback 'LICENSE') -Destination (Join-Path $plugin 'playback-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $playback 'Stage-Session.ps1') -Destination $plugin
Copy-Item -LiteralPath (Join-Path $root 'lib\toolkit\VERSION') -Destination (Join-Path $plugin 'toolkit.version')
& dotnet run --project (Join-Path $root 'tools\TelemetryInspector') -c Release --no-build -- schema (Join-Path $plugin 'telemetry-schema.json')
if ($LASTEXITCODE -ne 0) { throw 'Schema export failed' }
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'docs\DEVELOPMENT-BUILD.md') -Destination (Join-Path $stage 'README.md')
foreach ($name in 'Install.bat','Uninstall.bat','Manage-Install.ps1') { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $stage }
$manifest = @(Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName | ForEach-Object { @{ path = [IO.Path]::GetRelativePath($stage, $_.FullName); sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant() } })
$manifest | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $stage 'manifest.json') -Encoding utf8
$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$identity = if ($ArtifactLabel) { "$version-$ArtifactLabel" } else { $version }
$zip = Join-Path $root "dist\WodenRallyEdgeWheel-$identity-dev.zip"
if (Test-Path -LiteralPath $zip) { throw 'Package already exists. Preserve its identity and choose a distinct version before packaging again.' }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Write-Host "Development package: $zip"
Write-Host "Stage: $stage"
