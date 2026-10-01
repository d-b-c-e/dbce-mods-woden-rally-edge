[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$PackageRoot)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($PackageRoot)
$release=Get-Content -LiteralPath (Join-Path $root 'game-release.json') -Raw | ConvertFrom-Json
if ($release.packageId -ne 'dbce-mods-super-woden-rally-edge' -or $release.gameId -ne 'super-woden-rally-edge' -or $release.installedDiscovery -ne $false) { throw 'Unexpected package/discovery identity' }
$features=@($release.features.featureId | Sort-Object)
if (($features -join ',') -ne 'ffb,telemetry,triple,wheel-input') { throw 'Unexpected feature set' }
$triple=@($release.features | Where-Object featureId -eq 'triple')
if ($triple.Count -ne 1 -or $triple[0].available -ne $false -or @($triple[0].capabilities).Count -ne 0) { throw 'Unverified triple communication advertised' }
$tripleFields=@('available','reason','featureId','capabilities')
if (@(Compare-Object ($tripleFields | Sort-Object) (@($triple[0].PSObject.Properties.Name) | Sort-Object)).Count -ne 0 -or $triple[0].reason -isnot [string] -or $triple[0].capabilities -isnot [array]) { throw 'Unverified triple communication or fields advertised' }
$prefix='BepInEx/plugins/WodenRallyEdgeWheel/'
$expected=@('LICENSE','README.md','Install.bat','Uninstall.bat','Manage-Install.ps1','game-release.json','input-override.json')
$expected+=@('WodenRallyEdgeWheel.dll','WodenRallyEdge.Core.dll','Dbce.Wheel.Telemetry.dll','Dbce.Wheel.Recording.dll','Dbce.Wheel.Ffb.dll','WheelFfb.dll','recording-provenance.json','toolkit.version','telemetry-schema.json') | ForEach-Object { $prefix+$_ }
$manifest=@(Get-Content -LiteralPath (Join-Path $root 'manifest.json') -Raw | ConvertFrom-Json)
$paths=@($manifest | ForEach-Object { $_.path.Replace('\','/') })
if ($paths.Count -ne $expected.Count -or @($paths | Select-Object -Unique).Count -ne $expected.Count -or @(Compare-Object ($expected | Sort-Object) ($paths | Sort-Object)).Count -ne 0) { throw 'Payload allowlist differs' }
foreach ($entry in $manifest) {
    $path=[IO.Path]::GetFullPath((Join-Path $root $entry.path))
    if (-not $path.StartsWith($root.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest path escapes stage' }
    if ((Get-FileHash -LiteralPath $path).Hash -ne $entry.sha256) { throw "Hash mismatch: $($entry.path)" }
}
$actual=@(Get-ChildItem -LiteralPath $root -File -Recurse | ForEach-Object {[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/')} | Where-Object {$_ -ne 'manifest.json'})
if (@(Compare-Object ($expected | Sort-Object) ($actual | Sort-Object)).Count -ne 0) { throw 'Unmanifested payload present' }
if ((Get-Item -LiteralPath (Join-Path $root ($prefix+'WodenRallyEdgeWheel.dll'))).VersionInfo.ProductVersion -ne $release.version) { throw 'Package and plugin versions differ' }
$override=Get-Content -LiteralPath (Join-Path $root 'input-override.json') -Raw | ConvertFrom-Json
if ($override.sourceRevision -ne $release.toolkitInputSource -or $override.inputCapabilities -ne 1) { throw 'Input provenance differs' }
foreach ($pair in @(@('native/WheelFfb.dll','WheelFfb.dll'),@('dotnet/Dbce.Wheel.Ffb.dll','Dbce.Wheel.Ffb.dll'))) {
    $entry=@($override.artifacts | Where-Object path -eq $pair[0])
    if ($entry.Count -ne 1 -or (Get-FileHash -LiteralPath (Join-Path $root ($prefix+$pair[1]))).Hash -ne $entry[0].sha256) { throw 'Input override artifact differs' }
}
Write-Host "PASS unified stage: $($expected.Count) allowlisted files; one matched version; triple unavailable; exact input override."
