[CmdletBinding()]
param([string]$NuGetConfig)
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$wheel = Join-Path $root 'components\wheel'
& (Join-Path $PSScriptRoot 'Verify-Source.ps1') -NuGetConfig $NuGetConfig
$source = & git -c "safe.directory=$root" -C $root rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot establish source identity' }
$dirty = @(& git -c "safe.directory=$root" -C $root status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Cannot establish working-tree identity' }
$stage = Join-Path $root ('dist\candidate-' + [guid]::NewGuid().ToString('N'))
$plugin = Join-Path $stage 'BepInEx\plugins\WodenRallyEdgeWheel'
$null = New-Item -ItemType Directory -Path $plugin
$bin = Join-Path $wheel 'src\WodenRallyEdge.Plugin\bin\Release\net6.0'
foreach ($name in 'WodenRallyEdgeWheel.dll','WodenRallyEdge.Core.dll','Dbce.Wheel.Telemetry.dll','Dbce.Wheel.Recording.dll','Dbce.Wheel.Ffb.dll','WheelFfb.dll') {
    Copy-Item -LiteralPath (Join-Path $bin $name) -Destination $plugin
}
$version = (Get-Item -LiteralPath (Join-Path $plugin 'WodenRallyEdgeWheel.dll')).VersionInfo.ProductVersion
if ($version -notmatch '^0\.2\.13(?:\+.*)?$') { throw 'Unexpected unified candidate version' }
Copy-Item -LiteralPath (Join-Path $wheel 'lib\recording\provenance.json') -Destination (Join-Path $plugin 'recording-provenance.json')
Copy-Item -LiteralPath (Join-Path $wheel 'lib\toolkit\VERSION') -Destination (Join-Path $plugin 'toolkit.version')
# Release provenance is deliberately outside the legacy installer-owned payload.
Copy-Item -LiteralPath (Join-Path $wheel 'lib\toolkit\INPUT-OVERRIDE.json') -Destination (Join-Path $stage 'input-override.json')
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'docs\GAME-SETUP.md') -Destination (Join-Path $stage 'README.md')
foreach ($name in 'Install.bat','Uninstall.bat','Manage-Install.ps1') { Copy-Item -LiteralPath (Join-Path $wheel ('tools\' + $name)) -Destination $stage }
& dotnet run --project (Join-Path $wheel 'tools\TelemetryInspector') -c Release --no-build -- schema (Join-Path $plugin 'telemetry-schema.json')
if ($LASTEXITCODE -ne 0) { throw 'Schema export failed' }
$override = Get-Content -LiteralPath (Join-Path $stage 'input-override.json') -Raw | ConvertFrom-Json
foreach ($pair in @(@('native/WheelFfb.dll','WheelFfb.dll'),@('dotnet/Dbce.Wheel.Ffb.dll','Dbce.Wheel.Ffb.dll'))) {
    $entry = @($override.artifacts | Where-Object path -eq $pair[0])
    if ($entry.Count -ne 1 -or (Get-FileHash -LiteralPath (Join-Path $plugin $pair[1])).Hash -ne $entry[0].sha256) { throw 'Input override payload mismatch' }
}
[ordered]@{
    schemaVersion=1; metadataKind='source-candidate'; packageId='dbce-mods-super-woden-rally-edge'; gameId='super-woden-rally-edge'; version=$version
    sourceRevision=$source; sourceDirty=($dirty.Count -ne 0); installedDiscovery=$false
    features=@(
        @{featureId='wheel-input'; available=$true; adapterId='dbce.wodenrallyedgewheel'; rigVerified=$false},
        @{featureId='ffb'; available=$true; adapterId='dbce.wodenrallyedgewheel'; rigVerified=$false},
        @{featureId='telemetry'; available=$true; adapterId='dbce.wodenrallyedgewheel'; rigVerified=$false},
        @{featureId='triple'; available=$false; capabilities=@(); reason='Private-target probe only; native ABI and presentation unverified'}
    )
    toolkitInputSource=$override.sourceRevision; settingsMigration='retained legacy identity and receipt; no dual-renderer migration'
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $stage 'game-release.json') -Encoding utf8
$manifest = @(Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName | ForEach-Object { @{path=[IO.Path]::GetRelativePath($stage,$_.FullName); sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()} })
$manifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $stage 'manifest.json') -Encoding utf8
& (Join-Path $PSScriptRoot 'Verify-Package.ps1') -PackageRoot $stage
Write-Host "Unified candidate stage: $stage"
Write-Host 'No archive, install, runtime discovery or rig acceptance performed.'
